using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using System.Threading.RateLimiting;
using SystemKnowledgeHub.Api.Features.Attachments.Application;
using SystemKnowledgeHub.Api.Features.BusinessFunctions.Application;
using SystemKnowledgeHub.Api.Features.BusinessFunctions.Persistence;
using SystemKnowledgeHub.Api.Features.BusinessRules.Application;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Application;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Persistence;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Providers.Oracle;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Providers.PostgreSql;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Providers.SqlServer;
using SystemKnowledgeHub.Api.Features.Dashboard.Application;
using SystemKnowledgeHub.Api.Features.Evidence.Application;
using SystemKnowledgeHub.Api.Features.Integrations.Application;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Application;
using SystemKnowledgeHub.Api.Features.StatusProgression.Application;
using SystemKnowledgeHub.Api.Features.Relationships.Application;
using SystemKnowledgeHub.Api.Features.Search.Application;
using SystemKnowledgeHub.Api.Features.SoftDelete.Application;
using SystemKnowledgeHub.Api.Features.Systems.Application;
using SystemKnowledgeHub.Api.Features.Traceability.Application;
using SystemKnowledgeHub.Api.Features.UnknownItems.Application;
using SystemKnowledgeHub.Api.Features.Users.Application;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Shared.Api.Contracts;
using SystemKnowledgeHub.Api.Shared.Configuration;
using SystemKnowledgeHub.Api.Shared.Security;

var builder = WebApplication.CreateBuilder(args);

string? runtimeBindingError;
if (!TryBindRuntimeOptions(
        builder.Configuration,
        "Authentication:Oidc",
        new OidcAuthenticationOptions(),
        out OidcAuthenticationOptions oidc,
        out runtimeBindingError)
    || !TryBindRuntimeOptions(
        builder.Configuration,
        "Authentication:Local",
        new LocalAuthenticationOptions(),
        out LocalAuthenticationOptions local,
        out runtimeBindingError)
    || !TryBindRuntimeOptions(
        builder.Configuration,
        AuthenticationCookieOptions.SectionName,
        new AuthenticationCookieOptions(),
        out AuthenticationCookieOptions cookie,
        out runtimeBindingError)
    || !TryBindRuntimeOptions(
        builder.Configuration,
        PasswordHashingOptions.SectionName,
        new PasswordHashingOptions(),
        out PasswordHashingOptions passwordHashing,
        out runtimeBindingError)
    || !TryBindRuntimeOptions(
        builder.Configuration,
        SqlitePersistenceOptions.SectionName,
        new SqlitePersistenceOptions(),
        out SqlitePersistenceOptions sqlite,
        out runtimeBindingError)
    || !TryBindRuntimeOptions(
        builder.Configuration,
        CorsRuntimeOptions.SectionName,
        new CorsRuntimeOptions(),
        out CorsRuntimeOptions cors,
        out runtimeBindingError)
    || !TryBindRuntimeOptions(
        builder.Configuration,
        DatabaseDiscoveryOptions.SectionName,
        new DatabaseDiscoveryOptions(),
        out DatabaseDiscoveryOptions databaseDiscovery,
        out runtimeBindingError))
{
    ReportStartupConfigurationFailure(builder.Environment, runtimeBindingError!);
    return;
}
if (!IsolatedRuntimeStorageGuard.TryResolve(
        builder.Configuration,
        builder.Environment,
        out var isolatedRuntimeStorage,
        out var isolatedRuntimeStorageError))
{
    ReportStartupConfigurationFailure(builder.Environment, isolatedRuntimeStorageError!);
    return;
}
builder.Host.UseSerilog(
    (context, services, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services);
    },
    preserveStaticLogger: false,
    writeToProviders: false);
var requiresProductionConfiguration = !builder.Environment.IsDevelopment()
    && !builder.Environment.IsEnvironment("Testing");
if (!builder.Environment.IsEnvironment("Testing") && !local.Enabled && !oidc.Enabled)
{
    ReportStartupConfigurationFailure(
        builder.Environment,
        "至少必须启用 Authentication:Local 或 Authentication:Oidc 之一。");
    return;
}
if (!builder.Environment.IsEnvironment("Testing") && oidc.Enabled
    && (string.IsNullOrWhiteSpace(oidc.Provider)
        || string.IsNullOrWhiteSpace(oidc.Authority)
        || string.IsNullOrWhiteSpace(oidc.ClientId)))
{
    ReportStartupConfigurationFailure(
        builder.Environment,
        "启用 OIDC 时必须配置 Authentication:Oidc Provider、Authority 和 ClientId。");
    return;
}
if (local.Lockout.MaxFailedAttempts <= 0 || local.Lockout.WindowMinutes <= 0 || local.Lockout.DurationMinutes <= 0
    || local.RateLimit.PermitLimit <= 0 || local.RateLimit.WindowMinutes <= 0)
{
    ReportStartupConfigurationFailure(
        builder.Environment,
        "Authentication:Local 的 lockout 和 rate limit 配置必须为正数。");
    return;
}
foreach (var runtimeConfigurationError in new[]
{
    cookie.GetValidationError(),
    passwordHashing.GetValidationError(),
    sqlite.GetValidationError(),
    cors.GetValidationError(builder.Environment.IsDevelopment()),
    SerilogConfigurationValidator.GetValidationError(builder.Configuration),
})
{
    if (runtimeConfigurationError is null) continue;
    ReportStartupConfigurationFailure(builder.Environment, runtimeConfigurationError);
    return;
}
try
{
    databaseDiscovery.Validate();
}
catch (InvalidOperationException exception)
{
    ReportStartupConfigurationFailure(builder.Environment, exception.Message);
    return;
}

var dataProtectionApplicationName = builder.Configuration["DataProtection:ApplicationName"];
if (requiresProductionConfiguration && string.IsNullOrWhiteSpace(dataProtectionApplicationName))
{
    ReportStartupConfigurationFailure(
        builder.Environment,
        "Production Data Protection requires DataProtection:ApplicationName.");
    return;
}
var dataProtectionKeyPath = builder.Configuration["DataProtection:KeyPath"];
if (requiresProductionConfiguration && string.IsNullOrWhiteSpace(dataProtectionKeyPath))
{
    ReportStartupConfigurationFailure(
        builder.Environment,
        "Production Data Protection requires DataProtection:KeyPath.");
    return;
}
if (requiresProductionConfiguration && !Path.IsPathRooted(dataProtectionKeyPath))
{
    ReportStartupConfigurationFailure(
        builder.Environment,
        "Production DataProtection:KeyPath must be an absolute persistent path outside the application deployment directory.");
    return;
}
if (requiresProductionConfiguration
    && IsPathWithinDirectory(dataProtectionKeyPath!, builder.Environment.ContentRootPath))
{
    ReportStartupConfigurationFailure(
        builder.Environment,
        "Production DataProtection:KeyPath must be outside the application deployment directory.");
    return;
}
var productionConnectionStringError = requiresProductionConfiguration
    ? DbContextConfiguration.GetProductionConfigurationError(builder.Configuration)
    : null;
if (productionConnectionStringError is not null)
{
    ReportStartupConfigurationFailure(builder.Environment, productionConnectionStringError);
    return;
}
if (!AttachmentOptions.TryCreate(builder.Configuration, builder.Environment, out var attachmentOptions, out var attachmentConfigurationError))
{
    ReportStartupConfigurationFailure(builder.Environment, attachmentConfigurationError!);
    return;
}
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = attachmentOptions!.MaximumRequestBodyBytes;
});

var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName(dataProtectionApplicationName ?? "SystemKnowledgeHub");
if (!string.IsNullOrWhiteSpace(dataProtectionKeyPath))
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyPath));
}

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.Configure<FormOptions>(options =>
{
    options.MemoryBufferThreshold = attachmentOptions!.MemoryBufferThresholdBytes;
    options.MultipartBodyLengthLimit = attachmentOptions.MaximumRequestBodyBytes;
});

builder.Services.AddKnowledgeHubPersistence(builder.Configuration, builder.Environment, sqlite);
builder.Services.AddSingleton(attachmentOptions!);
builder.Services.AddSingleton<AttachmentFilePolicy>();
builder.Services.AddSingleton<AttachmentStorage>();
builder.Services.AddScoped<AttachmentPreviewService>();
builder.Services.AddScoped<AttachmentReferenceService>();
builder.Services.AddScoped<AttachmentService>();
builder.Services.AddScoped<AdministratorAttachmentQueries>();
builder.Services.AddScoped<BusinessFunctionQueries>();
builder.Services.AddScoped<BusinessFunctionService>();
builder.Services.AddScoped<BusinessFunctionDeleteService>();
builder.Services.AddScoped<BusinessRuleQueries>();
builder.Services.AddScoped<BusinessRuleService>();
builder.Services.AddScoped<BusinessRuleDeleteService>();
builder.Services.AddScoped<DatabaseKnowledgeQueries>();
builder.Services.AddScoped<DatabaseKnowledgeService>();
builder.Services.AddScoped<DatabaseKnowledgeDeleteService>();
builder.Services.AddScoped<DatabaseConnectionProfileService>();
builder.Services.AddScoped<DatabaseConnectionTestService>();
builder.Services.Configure<DatabaseDiscoveryOptions>(
    builder.Configuration.GetSection(DatabaseDiscoveryOptions.SectionName));
builder.Services.AddSingleton<CanonicalSnapshotService>();
builder.Services.AddSingleton<DatabaseDiscoveryDiffService>();
builder.Services.AddScoped<DatabaseDiscoveryRunService>();
builder.Services.AddScoped<DatabaseDiscoverySyncService>();
builder.Services.AddScoped<DatabaseDiscoveryRunProcessor>();
builder.Services.AddScoped<DatabaseDiscoveryTerminalWriter>();
builder.Services.AddSingleton<DatabaseDiscoveryWorkerReadiness>();
builder.Services.AddHostedService<DatabaseDiscoveryWorker>();
builder.Services.AddSingleton<IDatabaseConnectionSecretStore, DataProtectionDatabaseConnectionSecretStore>();
builder.Services.AddSingleton<IOracleConnectionProbe, OracleManagedConnectionProbe>();
builder.Services.AddSingleton<IDatabaseConnectionTester, OracleConnectionTester>();
builder.Services.AddSingleton<IPostgreSqlConnectionProbe, NpgsqlConnectionProbe>();
builder.Services.AddSingleton<IDatabaseConnectionTester, PostgreSqlConnectionTester>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IOracleDiscoveryCatalogReader, OracleManagedDiscoveryCatalogReader>();
builder.Services.AddSingleton<IDatabaseDiscoveryProvider, OracleDiscoveryProvider>();
builder.Services.AddSingleton<IPostgreSqlDiscoveryCatalogReader, NpgsqlPostgreSqlDiscoveryCatalogReader>();
builder.Services.AddSingleton<IDatabaseDiscoveryProvider, PostgreSqlDiscoveryProvider>();
builder.Services.AddSingleton<ISqlServerConnectionProbe, SqlClientConnectionProbe>();
builder.Services.AddSingleton<IDatabaseConnectionTester, SqlServerConnectionTester>();
builder.Services.AddSingleton<ISqlServerDiscoveryCatalogReader, SqlClientSqlServerDiscoveryCatalogReader>();
builder.Services.AddSingleton<IDatabaseDiscoveryProvider, SqlServerDiscoveryProvider>();
builder.Services.AddScoped<DashboardQueries>();
builder.Services.AddScoped<EvidenceSubjectResolver>();
builder.Services.AddScoped<EvidenceQueries>();
builder.Services.AddScoped<EvidenceService>();
builder.Services.AddScoped<IntegrationQueries>();
builder.Services.AddScoped<IntegrationService>();
builder.Services.AddScoped<IntegrationDeleteService>();
builder.Services.AddScoped<KnowledgeDocumentQueries>();
builder.Services.AddScoped<KnowledgeDocumentService>();
builder.Services.AddScoped<KnowledgeDocumentDeleteService>();
builder.Services.AddScoped<KnowledgeStatusPolicy>();
builder.Services.AddScoped<KnowledgeStatusService>();
builder.Services.AddScoped<RelationshipEndpointPolicy>();
builder.Services.AddScoped<RelationshipTargetResolver>();
builder.Services.AddScoped<RelationshipQueries>();
builder.Services.AddScoped<RelationshipService>();
builder.Services.AddScoped<SearchQueries>();
builder.Services.AddScoped<KnowledgeDocumentSearchIndex>();
builder.Services.AddScoped<HistoricalTargetResolver>();
builder.Services.AddScoped<SoftDeleteCapabilityResolver>();
builder.Services.AddScoped<SystemQueries>();
builder.Services.AddScoped<SystemKnowledgeViewQueries>();
builder.Services.AddScoped<SystemService>();
builder.Services.AddScoped<SystemDeleteService>();
builder.Services.AddScoped<TraceabilityQueries>();
builder.Services.AddScoped<ImpactQueries>();
builder.Services.AddScoped<UnknownItemQueries>();
builder.Services.AddScoped<UnknownItemService>();
builder.Services.AddScoped<KnowledgeResolutionService>();
builder.Services.AddScoped<UserQueries>();
builder.Services.AddScoped<UsableAdministratorResolver>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<LocalCredentialManagementService>();
builder.Services.AddSingleton<IOptions<LocalAuthenticationOptions>>(Options.Create(local));
builder.Services.AddSingleton<IOptions<OidcAuthenticationOptions>>(Options.Create(oidc));
builder.Services.AddSingleton<IOptions<AuthenticationCookieOptions>>(Options.Create(cookie));
builder.Services.AddSingleton<IOptions<PasswordHashingOptions>>(Options.Create(passwordHashing));
builder.Services.AddSingleton<IOptions<SqlitePersistenceOptions>>(Options.Create(sqlite));
builder.Services.AddSingleton<IOptions<CorsRuntimeOptions>>(Options.Create(cors));
builder.Services.Configure<PasswordHasherOptions>(options =>
{
    options.CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3;
    options.IterationCount = passwordHashing.IterationCount;
});
builder.Services.AddSingleton<LocalPasswordService>();
builder.Services.AddSingleton<AuthenticationPrincipalBuilder>();
builder.Services.AddScoped<LocalLoginService>();
builder.Services.AddScoped<LocalPasswordLifecycleService>();
builder.Services.AddScoped<LocalAdminBootstrapService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContext, CurrentUserContext>();
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        await context.HttpContext.Response.WriteAsJsonAsync(new ApiErrorResponse(
            "too_many_requests",
            "登录尝试过于频繁，请稍后再试。",
            null,
            null), cancellationToken);
    };
    options.AddPolicy(LocalLoginRateLimitPolicy.Name, httpContext =>
    {
        var address = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(address, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = local.RateLimit.PermitLimit,
            Window = TimeSpan.FromMinutes(local.RateLimit.WindowMinutes),
            QueueLimit = 0,
            AutoReplenishment = true,
        });
    });
});

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = CurrentUserContext.CookieScheme;
        options.DefaultChallengeScheme = "EnterpriseOidc";
        options.DefaultSignInScheme = CurrentUserContext.CookieScheme;
    })
    .AddCookie(CurrentUserContext.CookieScheme, options =>
    {
        options.Cookie.Name = CurrentUserContext.CookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(cookie.ExpireHours);
        options.SlidingExpiration = cookie.SlidingExpiration;
        options.Events.OnRedirectToLogin = context => WriteApiAuthenticationErrorAsync(
            context.Request,
            context.Response,
            context.RedirectUri,
            "unauthenticated",
            "尚未登录。",
            "missing");
        options.Events.OnRedirectToAccessDenied = context => WriteApiAuthenticationErrorAsync(
            context.Request,
            context.Response,
            context.RedirectUri,
            "forbidden",
            "无权执行此操作。",
            "forbidden");
    })
    .AddOpenIdConnect("EnterpriseOidc", options =>
    {
        options.SignInScheme = CurrentUserContext.CookieScheme;
        options.Authority = string.IsNullOrWhiteSpace(oidc.Authority)
            ? "https://invalid.local"
            : oidc.Authority;
        options.ClientId = string.IsNullOrWhiteSpace(oidc.ClientId)
            ? "system-knowledge-hub-development"
            : oidc.ClientId;
        options.ClientSecret = oidc.ClientSecret;
        options.CallbackPath = oidc.CallbackPath;
        options.ResponseType = "code";
        options.SaveTokens = false;
        options.Scope.Clear();
        foreach (var scope in oidc.Scopes.Where(scope => !string.IsNullOrWhiteSpace(scope)))
        {
            options.Scope.Add(scope);
        }
        options.Events.OnTokenValidated = async context =>
        {
            var subject = context.Principal?.FindFirstValue("sub");
            if (string.IsNullOrEmpty(subject))
            {
                context.Fail("identity_unmapped");
                return;
            }

            var dbContext = context.HttpContext.RequestServices.GetRequiredService<KnowledgeHubDbContext>();
            var mapping = await dbContext.LoginIdentities
                .AsNoTracking()
                .Where(identity => identity.Provider == oidc.Provider && identity.Subject == subject)
                .Join(
                    dbContext.Users.AsNoTracking(),
                    identity => identity.UserId,
                    user => user.Id,
                    (identity, user) => new { identity, user })
                .SingleOrDefaultAsync(context.HttpContext.RequestAborted);
            if (mapping is null)
            {
                context.Fail("identity_unmapped");
                return;
            }
            if (!mapping.identity.IsActive)
            {
                context.Fail("identity_inactive");
                return;
            }
            if (!mapping.user.IsActive)
            {
                context.Fail("account_inactive");
                return;
            }

            if (context.Principal?.Identity is not ClaimsIdentity identity)
            {
                context.Fail("session_expired");
                return;
            }
            context.HttpContext.RequestServices.GetRequiredService<AuthenticationPrincipalBuilder>()
                .AddDescriptorClaims(
                    identity,
                    AuthenticationClaims.OidcMethod,
                    mapping.identity.Id,
                    mapping.identity.Version,
                    mapping.user.Id,
                    mapping.user.AccessLevel);
        };
        options.Events.OnRemoteFailure = async context =>
        {
            context.HandleResponse();
            var code = context.Failure?.Message switch
            {
                "identity_unmapped" => "identity_unmapped",
                "identity_inactive" => "identity_inactive",
                "account_inactive" => "account_inactive",
                _ => "session_expired",
            };
            var authStatus = code switch
            {
                "identity_unmapped" => "unmapped",
                "identity_inactive" => "identity_inactive",
                "account_inactive" => "inactive",
                _ => "expired",
            };
            context.Response.StatusCode = code == "session_expired"
                ? StatusCodes.Status401Unauthorized
                : StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new ApiErrorResponse(
                code,
                "登录身份无法建立系统会话。",
                null,
                new { authStatus }));
        };
    });
builder.Services.AddAuthorization(options =>
{
    var viewerPolicy = new AuthorizationPolicyBuilder()
        .AddRequirements(new AccessLevelRequirement(AccessLevel.Viewer))
        .Build();
    options.DefaultPolicy = viewerPolicy;
    options.FallbackPolicy = viewerPolicy;
    options.AddPolicy(AccessPolicies.PasswordLifecycle, policy => policy
        .AddRequirements(new CurrentSessionRequirement()));
    options.AddPolicy(AccessPolicies.Editor, policy => policy
        .AddRequirements(new AccessLevelRequirement(AccessLevel.Editor)));
    options.AddPolicy(AccessPolicies.Administrator, policy => policy
        .AddRequirements(new AccessLevelRequirement(AccessLevel.Administrator)));
});
builder.Services.AddScoped<IAuthorizationHandler, CurrentSessionAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, AccessLevelAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ApiAuthorizationMiddlewareResultHandler>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("ViteDevelopment", policy =>
    {
        policy
            .WithOrigins(cors.AllowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

if (BootstrapAdministratorCommand.IsRequested(args))
{
    Environment.ExitCode = await BootstrapAdministratorCommand.RunAsync(args, app.Services, oidc);
    return;
}
if (BootstrapLocalAdministratorCommand.IsRequested(args))
{
    Environment.ExitCode = await BootstrapLocalAdministratorCommand.RunAsync(args, app.Services, local);
    return;
}
if (KnowledgeDocumentSearchMaintenanceCommand.IsRequested(args))
{
    Environment.ExitCode = await KnowledgeDocumentSearchMaintenanceCommand.RunAsync(args, app.Services);
    return;
}

app.Logger.LogInformation(
    "System Knowledge Hub host is starting in {EnvironmentName}.",
    app.Environment.EnvironmentName);

if (app.Environment.IsEnvironment(IsolatedRuntimeStorageGuard.VerificationEnvironmentName))
{
    app.Logger.LogInformation(
        "Verification runtime SQLite Data Source resolved to {SqliteDataSourcePath}.",
        isolatedRuntimeStorage!.SqliteDataSourcePath);
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
    await dbContext.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    await DatabaseKnowledgeDevelopmentData.InitializeAsync(app.Services);
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
    await BusinessFunctionDevelopmentData.SeedAsync(dbContext);
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseCors("ViteDevelopment");
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    var requiresAntiforgery = HttpMethods.IsPost(context.Request.Method)
        || HttpMethods.IsPut(context.Request.Method)
        || HttpMethods.IsPatch(context.Request.Method)
        || HttpMethods.IsDelete(context.Request.Method);
    var isLocalLogin = context.Request.Path.StartsWithSegments("/auth/local/login", StringComparison.OrdinalIgnoreCase);
    var isProtectedApplicationEndpoint = context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
        || context.Request.Path.StartsWithSegments("/auth/logout", StringComparison.OrdinalIgnoreCase)
        || (local.Enabled && isLocalLogin);

    if (!requiresAntiforgery || !isProtectedApplicationEndpoint)
    {
        await next();
        return;
    }

    try
    {
        await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context);
    }
    catch (AntiforgeryValidationException)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new ApiErrorResponse(
            "antiforgery_failed",
            "请求验证失败，请刷新页面后重试。",
            null,
            null));
        return;
    }
    await next();
});
app.MapControllers();

app.Run();

static void ReportStartupConfigurationFailure(
    IHostEnvironment environment,
    string message)
{
    Console.Error.WriteLine($"启动配置错误（环境：{environment.EnvironmentName}）：{message}");
    Console.Error.WriteLine(
        $"请通过 appsettings.{environment.EnvironmentName}.json、环境变量或命令行显式修正配置；" +
        "直接启动 SystemKnowledgeHub.Api.exe 不会应用 Properties/launchSettings.json。");
    Environment.ExitCode = 1;
}

static bool TryBindRuntimeOptions<TOptions>(
    IConfiguration configuration,
    string sectionName,
    TOptions fallback,
    out TOptions options,
    out string? error)
    where TOptions : class
{
    try
    {
        options = configuration.GetSection(sectionName).Get<TOptions>() ?? fallback;
        error = null;
        return true;
    }
    catch (InvalidOperationException)
    {
        options = fallback;
        error = $"{sectionName} contains a value with an invalid type.";
        return false;
    }
}

static bool IsPathWithinDirectory(string path, string directory)
{
    var relativePath = Path.GetRelativePath(
        Path.GetFullPath(directory),
        Path.GetFullPath(path));
    return !Path.IsPathRooted(relativePath)
        && (relativePath == "."
        || (!relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && relativePath != ".."));
}

static Task WriteApiAuthenticationErrorAsync(
    HttpRequest request,
    HttpResponse response,
    string redirectUri,
    string code,
    string message,
    string authStatus)
{
    if (!request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
    {
        response.Redirect(redirectUri);
        return Task.CompletedTask;
    }

    response.StatusCode = code == "unauthenticated"
        ? StatusCodes.Status401Unauthorized
        : StatusCodes.Status403Forbidden;
    return response.WriteAsJsonAsync(new ApiErrorResponse(
        code,
        message,
        null,
        new { authStatus }));
}

public partial class Program;
