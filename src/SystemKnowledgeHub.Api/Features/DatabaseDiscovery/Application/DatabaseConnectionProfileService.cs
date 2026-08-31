using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application.Models;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;
using SystemKnowledgeHub.Api.Shared.Api;

namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;

public sealed class DatabaseConnectionProfileService(
    KnowledgeHubDbContext dbContext,
    ConcurrencyTokenCodec tokenCodec,
    IDatabaseConnectionSecretStore secretStore)
{
    private const string CanonicalOptionsJson = "{\"version\":1}";

    public async Task<IReadOnlyList<DatabaseConnectionProfileResponse>> List(CancellationToken cancellationToken)
    {
        var profiles = await dbContext.DatabaseConnectionProfiles
            .AsNoTracking()
            .Include(item => item.Secret)
            .Include(item => item.DatabaseSource)
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Id)
            .ToArrayAsync(cancellationToken);
        return profiles.Select(ToResponse).ToArray();
    }

    public async Task<DatabaseConnectionProfileResponse?> Get(long id, CancellationToken cancellationToken)
    {
        if (!ApiIdParser.IsSafePositive(id)) return null;
        var profile = await dbContext.DatabaseConnectionProfiles
            .AsNoTracking()
            .Include(item => item.Secret)
            .Include(item => item.DatabaseSource)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return profile is null ? null : ToResponse(profile);
    }

    public async Task<IReadOnlyList<DatabaseConnectionSourceOptionResponse>> ListSourceOptions(
        string? search,
        CancellationToken cancellationToken)
    {
        var term = search?.Trim();
        var query = dbContext.DatabaseSources.AsNoTracking().Include(item => item.System).AsQueryable();
        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(item => item.Name.Contains(term) || item.System.Name.Contains(term));
        }
        return await query.OrderBy(item => item.System.Name).ThenBy(item => item.Name).Take(100)
            .Select(item => new DatabaseConnectionSourceOptionResponse(
                item.Id,
                item.Name,
                item.Engine,
                item.System.Name,
                dbContext.DatabaseConnectionProfiles.Any(profile => profile.DatabaseSourceId == item.Id)))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<DatabaseConnectionOperationResult<DatabaseConnectionProfileResponse>> Create(
        DatabaseConnectionProfileInput input,
        DatabaseConnectionActor actor,
        CancellationToken cancellationToken)
    {
        var errors = Validate(input, out var normalized);
        if (errors.Count > 0) return Validation(errors);
        if (!ApiIdParser.IsSafePositive(input.DatabaseSourceId))
        {
            errors["databaseSourceId"] = ["数据库来源必须是有效 ID。"];
            return Validation(errors);
        }

        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var source = await dbContext.DatabaseSources
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.Id == input.DatabaseSourceId, cancellationToken);
        if (source is null || source.IsDeleted || !EngineMatches(normalized.ProviderType, source.Engine))
        {
            return Failure(DatabaseConnectionFailure.ReferenceInvalid);
        }
        if (await dbContext.DatabaseConnectionProfiles.AnyAsync(
                item => item.DatabaseSourceId == source.Id, cancellationToken))
        {
            return Failure(DatabaseConnectionFailure.DuplicateSource);
        }
        if (await dbContext.DatabaseConnectionProfiles.AnyAsync(
                item => item.Name == normalized.Name, cancellationToken))
        {
            return Failure(DatabaseConnectionFailure.DuplicateName);
        }

        var now = DateTimeOffset.UtcNow;
        var profile = new DatabaseConnectionProfile
        {
            DatabaseSourceId = source.Id,
            DatabaseSource = source,
            Name = normalized.Name,
            ProviderType = normalized.ProviderType,
            Host = normalized.Host,
            Port = normalized.Port,
            DatabaseName = normalized.DatabaseName,
            ServiceName = normalized.ServiceName,
            AuthenticationMode = normalized.AuthenticationMode,
            Username = normalized.Username,
            ProviderSpecificOptionsJson = CanonicalOptionsJson,
            IncludedSchemasJson = JsonSerializer.Serialize(normalized.IncludedSchemas),
            IsEnabled = input.IsEnabled,
            ConnectionStatus = DatabaseConnectionStatus.Unknown,
            ConfigurationRevision = 1,
            CreatedByUserId = actor.Creator.UserId,
            CreatedByDisplayName = actor.Creator.DisplayName,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1,
        };
        dbContext.DatabaseConnectionProfiles.Add(profile);
        await dbContext.SaveChangesAsync(cancellationToken);
        AddAudit(profile.Id, DatabaseConnectionAuditAction.ProfileCreated, DatabaseConnectionAuditOutcome.Succeeded, actor, now);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Failure(DatabaseConnectionFailure.DuplicateSource);
        }
        return Success(ToResponse(profile));
    }

    public async Task<DatabaseConnectionOperationResult<DatabaseConnectionProfileResponse>> Update(
        DatabaseConnectionProfileUpdateInput input,
        DatabaseConnectionActor actor,
        CancellationToken cancellationToken)
    {
        var validationInput = new DatabaseConnectionProfileInput(
            1, input.Name, input.ProviderType, input.Host, input.Port, input.DatabaseName,
            input.ServiceName, input.AuthenticationMode, input.Username,
            input.ProviderSpecificOptions, input.IncludedSchemas, false);
        var errors = Validate(validationInput, out var normalized);
        if (!ApiIdParser.IsSafePositive(input.Id)) errors["id"] = ["连接配置必须是有效 ID。"];
        if (!tokenCodec.TryDecode(input.ConcurrencyToken, out var expectedVersion))
            errors["concurrencyToken"] = ["并发标记无效，请重新加载后重试。"];
        if (errors.Count > 0) return Validation(errors);

        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var profile = await dbContext.DatabaseConnectionProfiles
            .Include(item => item.Secret)
            .SingleOrDefaultAsync(item => item.Id == input.Id, cancellationToken);
        if (profile is null) return Failure(DatabaseConnectionFailure.NotFound);
        if (await HasActiveRun(profile.Id, cancellationToken)) return Failure(DatabaseConnectionFailure.ActiveDiscoveryRun);
        if (profile.Version != expectedVersion) return Failure(DatabaseConnectionFailure.ConcurrencyConflict);
        var source = await dbContext.DatabaseSources.IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.Id == profile.DatabaseSourceId, cancellationToken);
        if (source is null || source.IsDeleted || !EngineMatches(normalized.ProviderType, source.Engine))
            return Failure(DatabaseConnectionFailure.ReferenceInvalid);
        if (await dbContext.DatabaseConnectionProfiles.AnyAsync(
                item => item.Id != profile.Id && item.Name == normalized.Name, cancellationToken))
            return Failure(DatabaseConnectionFailure.DuplicateName);

        var discoveryTargetChanged = profile.ProviderType != normalized.ProviderType
            || profile.Host != normalized.Host
            || profile.Port != normalized.Port
            || profile.DatabaseName != normalized.DatabaseName
            || profile.ServiceName != normalized.ServiceName;
        if (discoveryTargetChanged
            && await dbContext.DatabaseDiscoverySnapshots.AnyAsync(item => item.ProfileId == profile.Id, cancellationToken))
        {
            return Failure(DatabaseConnectionFailure.DiscoveryTargetImmutable);
        }

        var includedJson = JsonSerializer.Serialize(normalized.IncludedSchemas);
        var connectionConfigurationChanged = profile.ProviderType != normalized.ProviderType
            || profile.Host != normalized.Host
            || profile.Port != normalized.Port
            || profile.DatabaseName != normalized.DatabaseName
            || profile.ServiceName != normalized.ServiceName
            || profile.AuthenticationMode != normalized.AuthenticationMode
            || profile.Username != normalized.Username
            || profile.ProviderSpecificOptionsJson != CanonicalOptionsJson
            || profile.IncludedSchemasJson != includedJson;
        var changed = connectionConfigurationChanged || profile.Name != normalized.Name;
        if (!changed) return Success(ToResponse(profile));

        profile.Name = normalized.Name;
        profile.ProviderType = normalized.ProviderType;
        profile.Host = normalized.Host;
        profile.Port = normalized.Port;
        profile.DatabaseName = normalized.DatabaseName;
        profile.ServiceName = normalized.ServiceName;
        profile.AuthenticationMode = normalized.AuthenticationMode;
        profile.Username = normalized.Username;
        profile.ProviderSpecificOptionsJson = CanonicalOptionsJson;
        profile.IncludedSchemasJson = includedJson;
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        profile.Version++;
        if (connectionConfigurationChanged)
        {
            profile.ConfigurationRevision++;
            ResetConnectionStatus(profile);
        }
        AddAudit(profile.Id, DatabaseConnectionAuditAction.ProfileUpdated, DatabaseConnectionAuditOutcome.Succeeded, actor, profile.UpdatedAt);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Failure(DatabaseConnectionFailure.ConcurrencyConflict);
        }
        return Success(ToResponse(profile));
    }

    public async Task<DatabaseConnectionOperationResult<DatabaseConnectionProfileResponse>> SetEnabled(
        long id,
        bool? isEnabled,
        string? concurrencyToken,
        DatabaseConnectionActor actor,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (!ApiIdParser.IsSafePositive(id)) errors["id"] = ["连接配置必须是有效 ID。"];
        if (isEnabled is null) errors["isEnabled"] = ["必须明确指定启用状态。"];
        if (!tokenCodec.TryDecode(concurrencyToken, out var expectedVersion))
            errors["concurrencyToken"] = ["并发标记无效，请重新加载后重试。"];
        if (errors.Count > 0) return Validation(errors);
        var enabled = isEnabled.GetValueOrDefault();

        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var profile = await dbContext.DatabaseConnectionProfiles
            .Include(item => item.Secret)
            .Include(item => item.DatabaseSource)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (profile is null) return Failure(DatabaseConnectionFailure.NotFound);
        if (await HasActiveRun(profile.Id, cancellationToken)) return Failure(DatabaseConnectionFailure.ActiveDiscoveryRun);
        if (profile.Version != expectedVersion) return Failure(DatabaseConnectionFailure.ConcurrencyConflict);
        if (profile.IsEnabled == enabled) return Success(ToResponse(profile));
        if (enabled)
        {
            var sourceAvailable = await dbContext.DatabaseSources.AnyAsync(
                item => item.Id == profile.DatabaseSourceId, cancellationToken);
            if (!sourceAvailable) return Failure(DatabaseConnectionFailure.ReferenceInvalid);
        }

        var now = DateTimeOffset.UtcNow;
        profile.IsEnabled = enabled;
        profile.ConnectionStatus = DatabaseConnectionStatus.Unknown;
        profile.ConfigurationRevision++;
        profile.Version++;
        profile.UpdatedAt = now;
        AddAudit(
            profile.Id,
            enabled ? DatabaseConnectionAuditAction.ProfileEnabled : DatabaseConnectionAuditAction.ProfileDisabled,
            DatabaseConnectionAuditOutcome.Succeeded,
            actor,
            now);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Failure(DatabaseConnectionFailure.ConcurrencyConflict);
        }
        return Success(ToResponse(profile));
    }

    public Task<DatabaseConnectionOperationResult<DatabaseConnectionProfileResponse>> SetSecret(
        long id, string? password, string? token, DatabaseConnectionActor actor, CancellationToken cancellationToken) =>
        WriteSecret(id, password, token, replace: false, actor, cancellationToken);

    public Task<DatabaseConnectionOperationResult<DatabaseConnectionProfileResponse>> ReplaceSecret(
        long id, string? password, string? token, DatabaseConnectionActor actor, CancellationToken cancellationToken) =>
        WriteSecret(id, password, token, replace: true, actor, cancellationToken);

    public async Task<DatabaseConnectionOperationResult<DatabaseConnectionProfileResponse>> ClearSecret(
        long id,
        string? concurrencyToken,
        DatabaseConnectionActor actor,
        CancellationToken cancellationToken)
    {
        var errors = ValidateSecretCommand(id, null, concurrencyToken, requiresPassword: false, out var expectedVersion);
        if (errors.Count > 0) return Validation(errors);
        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var profile = await dbContext.DatabaseConnectionProfiles
            .Include(item => item.Secret)
            .Include(item => item.DatabaseSource)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (profile is null) return Failure(DatabaseConnectionFailure.NotFound);
        if (await HasActiveRun(profile.Id, cancellationToken)) return Failure(DatabaseConnectionFailure.ActiveDiscoveryRun);
        if (profile.Version != expectedVersion) return Failure(DatabaseConnectionFailure.ConcurrencyConflict);
        if (profile.Secret?.ProtectedPayload is null) return Failure(DatabaseConnectionFailure.SecretMissing);

        var now = DateTimeOffset.UtcNow;
        profile.Secret.ProtectedPayload = null;
        profile.Secret.PayloadFormatVersion = 1;
        profile.Secret.UpdatedAt = now;
        profile.Secret.Version++;
        profile.Version++;
        profile.UpdatedAt = now;
        ResetConnectionStatus(profile);
        AddAudit(profile.Id, DatabaseConnectionAuditAction.SecretCleared, DatabaseConnectionAuditOutcome.Succeeded, actor, now);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Failure(DatabaseConnectionFailure.ConcurrencyConflict);
        }
        return Success(ToResponse(profile));
    }

    private async Task<DatabaseConnectionOperationResult<DatabaseConnectionProfileResponse>> WriteSecret(
        long id,
        string? password,
        string? concurrencyToken,
        bool replace,
        DatabaseConnectionActor actor,
        CancellationToken cancellationToken)
    {
        var errors = ValidateSecretCommand(id, password, concurrencyToken, requiresPassword: true, out var expectedVersion);
        if (errors.Count > 0) return Validation(errors);
        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var profile = await dbContext.DatabaseConnectionProfiles
            .Include(item => item.Secret)
            .Include(item => item.DatabaseSource)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (profile is null) return Failure(DatabaseConnectionFailure.NotFound);
        if (await HasActiveRun(profile.Id, cancellationToken)) return Failure(DatabaseConnectionFailure.ActiveDiscoveryRun);
        if (profile.Version != expectedVersion) return Failure(DatabaseConnectionFailure.ConcurrencyConflict);
        var currentlySet = profile.Secret?.ProtectedPayload is not null;
        if (replace && !currentlySet) return Failure(DatabaseConnectionFailure.SecretMissing);
        if (!replace && currentlySet) return Failure(DatabaseConnectionFailure.SecretAlreadySet);

        var now = DateTimeOffset.UtcNow;
        var protectedPayload = secretStore.Protect(profile.Id, password!);
        if (profile.Secret is null)
        {
            profile.Secret = new DatabaseConnectionSecret
            {
                ProfileId = profile.Id,
                ProtectedPayload = protectedPayload,
                PayloadFormatVersion = 1,
                UpdatedAt = now,
                Version = 1,
            };
        }
        else
        {
            profile.Secret.ProtectedPayload = protectedPayload;
            profile.Secret.PayloadFormatVersion = 1;
            profile.Secret.UpdatedAt = now;
            profile.Secret.Version++;
        }
        profile.Version++;
        profile.UpdatedAt = now;
        ResetConnectionStatus(profile);
        AddAudit(
            profile.Id,
            replace ? DatabaseConnectionAuditAction.SecretReplaced : DatabaseConnectionAuditAction.SecretSet,
            DatabaseConnectionAuditOutcome.Succeeded,
            actor,
            now);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Failure(DatabaseConnectionFailure.ConcurrencyConflict);
        }
        return Success(ToResponse(profile));
    }

    private Dictionary<string, string[]> ValidateSecretCommand(
        long id,
        string? password,
        string? concurrencyToken,
        bool requiresPassword,
        out long expectedVersion)
    {
        var errors = new Dictionary<string, string[]>();
        if (!ApiIdParser.IsSafePositive(id)) errors["id"] = ["连接配置必须是有效 ID。"];
        if (requiresPassword && (password is null || password.Length == 0)) errors["password"] = ["密码不能为空。"];
        if (password?.Length > 4096) errors["password"] = ["密码长度不能超过 4096 个字符。"];
        if (!tokenCodec.TryDecode(concurrencyToken, out expectedVersion))
            errors["concurrencyToken"] = ["并发标记无效，请重新加载后重试。"];
        return errors;
    }

    private static Dictionary<string, string[]> Validate(
        DatabaseConnectionProfileInput input,
        out NormalizedProfileInput normalized)
    {
        var errors = new Dictionary<string, string[]>();
        var name = input.Name.Trim();
        var host = input.Host.Trim();
        var databaseName = NormalizeOptional(input.DatabaseName);
        var serviceName = NormalizeOptional(input.ServiceName);
        var username = input.Username.Trim();
        if (name.Length is < 1 or > 160) errors["name"] = ["配置名称长度必须为 1–160 个字符。"];
        if (!Enum.TryParse<DatabaseProviderType>(input.ProviderType, false, out var providerType)
            || providerType.ToString() != input.ProviderType)
            errors["providerType"] = ["ProviderType 必须是 Oracle、PostgreSql 或 SqlServer。"];
        if (!Enum.TryParse<DatabaseAuthenticationMode>(input.AuthenticationMode, false, out var authenticationMode)
            || authenticationMode != DatabaseAuthenticationMode.UsernamePassword
            || authenticationMode.ToString() != input.AuthenticationMode)
            errors["authenticationMode"] = ["AuthenticationMode 仅支持 UsernamePassword。"];
        if (!IsValidHost(host)) errors["host"] = ["Host 必须是有效的 DNS 名称或 IP 地址，且不能包含连接描述符。"];
        if (input.Port is null or < 1 or > 65535) errors["port"] = ["Port 必须是 1–65535。"];
        if (username.Length is < 1 or > 128 || username.Any(char.IsControl))
            errors["username"] = ["用户名长度必须为 1–128 个字符且不能包含控制字符。"];

        if (providerType == DatabaseProviderType.Oracle)
        {
            if (!IsValidOracleServiceName(serviceName)) errors["serviceName"] = ["Oracle ServiceName 必须是 1–128 个安全字符。"];
            if (databaseName is not null) errors["databaseName"] = ["Oracle Service 模式不能设置 DatabaseName。"];
        }
        else
        {
            if (databaseName is null || databaseName.Length > 128 || databaseName.Any(char.IsControl))
                errors["databaseName"] = ["PostgreSql/SqlServer 必须设置 1–128 个字符的 DatabaseName。"];
            if (serviceName is not null) errors["serviceName"] = ["PostgreSql/SqlServer 不能设置 ServiceName。"];
        }

        var includedSchemas = NormalizeIncludedSchemas(input.IncludedSchemas, errors);
        ValidateOptions(input.ProviderSpecificOptions, errors);
        normalized = new(
            name, providerType, host, input.Port ?? 0, databaseName, serviceName,
            authenticationMode, username, includedSchemas);
        return errors;
    }

    private static string[] NormalizeIncludedSchemas(
        IReadOnlyList<string>? values,
        IDictionary<string, string[]> errors)
    {
        if (values is null || values.Count is < 1 or > 128)
        {
            errors["includedSchemas"] = ["IncludedSchemas 必须明确包含 1–128 个 Schema。"];
            return [];
        }
        var normalized = values.Select(value => value?.Trim() ?? string.Empty).ToArray();
        if (normalized.Any(value => value.Length is < 1 or > 128 || value.Any(char.IsControl)))
            errors["includedSchemas"] = ["Schema 名称必须是 1–128 个字符且不能包含控制字符。"];
        else if (normalized.Distinct(StringComparer.Ordinal).Count() != normalized.Length)
            errors["includedSchemas"] = ["Schema 名称不能重复。"];
        return normalized;
    }

    private static void ValidateOptions(JsonElement? options, IDictionary<string, string[]> errors)
    {
        if (options is null) return;
        var value = options.Value;
        if (value.ValueKind != JsonValueKind.Object)
        {
            errors["providerSpecificOptions"] = ["ProviderSpecificOptions 必须是版本化对象。"];
            return;
        }
        var properties = value.EnumerateObject().ToArray();
        if (properties.Length != 1
            || properties[0].Name != "version"
            || properties[0].Value.ValueKind != JsonValueKind.Number
            || !properties[0].Value.TryGetInt32(out var version)
            || version != 1)
        {
            errors["providerSpecificOptions"] = ["ProviderSpecificOptions 仅允许 {\"version\":1}，不得包含凭据或连接片段。"];
        }
    }

    private static bool IsValidHost(string host)
    {
        if (host.Length is < 1 or > 253 || host.Any(char.IsWhiteSpace) || host.Any(char.IsControl)) return false;
        if (host.IndexOfAny(['(', ')', '=', ';', '/', '\\', '@', '?', ',']) >= 0) return false;
        var candidate = host.Trim('[', ']');
        return IPAddress.TryParse(candidate, out _) || Uri.CheckHostName(candidate) == UriHostNameType.Dns;
    }

    private static bool IsValidOracleServiceName(string? value) =>
        value is not null
        && value.Length <= 128
        && value.All(character => char.IsLetterOrDigit(character) || character is '_' or '$' or '#' or '.' or '-');

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private static bool EngineMatches(DatabaseProviderType providerType, string engine) => providerType switch
    {
        DatabaseProviderType.Oracle => string.Equals(engine.Trim(), "Oracle", StringComparison.OrdinalIgnoreCase),
        DatabaseProviderType.PostgreSql => string.Equals(engine.Trim(), "PostgreSQL", StringComparison.OrdinalIgnoreCase),
        DatabaseProviderType.SqlServer => string.Equals(engine.Trim(), "SQL Server", StringComparison.OrdinalIgnoreCase),
        _ => false,
    };

    private void AddAudit(
        long profileId,
        DatabaseConnectionAuditAction action,
        DatabaseConnectionAuditOutcome outcome,
        DatabaseConnectionActor actor,
        DateTimeOffset occurredAt,
        string? errorCode = null,
        string? vendorCode = null)
    {
        dbContext.DatabaseConnectionAuditEvents.Add(new DatabaseConnectionAuditEvent
        {
            ProfileId = profileId,
            Action = action,
            Outcome = outcome,
            ErrorCode = errorCode,
            VendorCode = vendorCode,
            ActorUserId = actor.Creator.UserId,
            ActorDisplayName = actor.Creator.DisplayName,
            OccurredAt = occurredAt,
        });
    }

    private static void ResetConnectionStatus(DatabaseConnectionProfile profile)
    {
        profile.ConnectionStatus = DatabaseConnectionStatus.Unknown;
        profile.LastConnectionTestAt = null;
        profile.LastConnectionTestErrorCode = null;
        profile.LastConnectionTestVendorCode = null;
        profile.LastConnectionTestSummary = null;
    }

    internal DatabaseConnectionProfileResponse ToResponse(DatabaseConnectionProfile profile) => new(
        profile.Id,
        profile.DatabaseSourceId,
        profile.DatabaseSource?.Name ?? string.Empty,
        profile.Name,
        profile.ProviderType,
        profile.Host,
        profile.Port,
        profile.DatabaseName,
        profile.ServiceName,
        profile.AuthenticationMode,
        profile.Username,
        new DatabaseProviderSpecificOptionsResponse(1),
        JsonSerializer.Deserialize<string[]>(profile.IncludedSchemasJson) ?? [],
        profile.IsEnabled,
        profile.ConnectionStatus,
        profile.Secret?.ProtectedPayload is not null,
        profile.Secret?.ProtectedPayload is null ? null : profile.Secret.UpdatedAt,
        profile.LastConnectionTestAt,
        profile.LastConnectionTestErrorCode,
        profile.LastConnectionTestVendorCode,
        profile.LastConnectionTestSummary,
        profile.LastDiscoveryAt,
        profile.LastSuccessfulDiscoveryAt,
        profile.ConfigurationRevision,
        tokenCodec.Encode(profile.Version));

    private static DatabaseConnectionOperationResult<T> Success<T>(T response) =>
        new(response, null, DatabaseConnectionFailure.None);
    private static DatabaseConnectionOperationResult<DatabaseConnectionProfileResponse> Failure(DatabaseConnectionFailure failure) =>
        new(null, null, failure);
    private static DatabaseConnectionOperationResult<DatabaseConnectionProfileResponse> Validation(
        IReadOnlyDictionary<string, string[]> errors) =>
        new(null, errors, DatabaseConnectionFailure.Validation);

    private Task<bool> HasActiveRun(long profileId, CancellationToken cancellationToken) =>
        dbContext.DatabaseDiscoveryRuns.AnyAsync(item => item.ProfileId == profileId
            && (item.Status == DatabaseDiscoveryRunStatus.Queued || item.Status == DatabaseDiscoveryRunStatus.Running), cancellationToken);

    private sealed record NormalizedProfileInput(
        string Name,
        DatabaseProviderType ProviderType,
        string Host,
        int Port,
        string? DatabaseName,
        string? ServiceName,
        DatabaseAuthenticationMode AuthenticationMode,
        string Username,
        string[] IncludedSchemas);
}
