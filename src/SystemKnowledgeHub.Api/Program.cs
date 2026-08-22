using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;
using SystemKnowledgeHub.Api.Features.BusinessFunctions.Application;
using SystemKnowledgeHub.Api.Features.BusinessFunctions.Persistence;
using SystemKnowledgeHub.Api.Features.BusinessRules.Application;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Application;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Persistence;
using SystemKnowledgeHub.Api.Features.Dashboard.Application;
using SystemKnowledgeHub.Api.Features.Evidence.Application;
using SystemKnowledgeHub.Api.Features.Integrations.Application;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Application;
using SystemKnowledgeHub.Api.Features.StatusProgression.Application;
using SystemKnowledgeHub.Api.Features.Relationships.Application;
using SystemKnowledgeHub.Api.Features.Search.Application;
using SystemKnowledgeHub.Api.Features.Systems.Application;
using SystemKnowledgeHub.Api.Features.UnknownItems.Application;
using SystemKnowledgeHub.Api.Features.Users.Application;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Shared.Api.Contracts;
using SystemKnowledgeHub.Api.Shared.Security;

var builder = WebApplication.CreateBuilder(args);

var oidc = builder.Configuration.GetSection("Authentication:Oidc").Get<OidcAuthenticationOptions>()
    ?? new OidcAuthenticationOptions();
var local = builder.Configuration.GetSection("Authentication:Local").Get<LocalAuthenticationOptions>()
    ?? new LocalAuthenticationOptions();
if (builder.Configuration["Authentication:Local:Enabled"] is null && builder.Environment.IsDevelopment())
{
    local = new LocalAuthenticationOptions
    {
        Enabled = true,
        Lockout = local.Lockout,
        RateLimit = local.RateLimit,
    };
}
var requiresProductionConfiguration = !builder.Environment.IsDevelopment()
    && !builder.Environment.IsEnvironment("Testing");
if (!builder.Environment.IsEnvironment("Testing") && !local.Enabled && !oidc.Enabled)
{
    throw new InvalidOperationException("至少必须启用 Authentication:Local 或 Authentication:Oidc 之一。");
}
if (!builder.Environment.IsEnvironment("Testing") && oidc.Enabled
    && (string.IsNullOrWhiteSpace(oidc.Provider)
        || string.IsNullOrWhiteSpace(oidc.Authority)
        || string.IsNullOrWhiteSpace(oidc.ClientId)))
{
    throw new InvalidOperationException(
        "启用 OIDC 时必须配置 Authentication:Oidc Provider、Authority 和 ClientId。");
}
if (local.Lockout.MaxFailedAttempts <= 0 || local.Lockout.WindowMinutes <= 0 || local.Lockout.DurationMinutes <= 0
    || local.RateLimit.PermitLimit <= 0 || local.RateLimit.WindowMinutes <= 0)
{
    throw new InvalidOperationException("Authentication:Local 的 lockout 和 rate limit 配置必须为正数。");
}

var dataProtectionKeyPath = builder.Configuration["DataProtection:KeyPath"];
if (requiresProductionConfiguration && string.IsNullOrWhiteSpace(dataProtectionKeyPath))
{
    throw new InvalidOperationException(
        "Production Data Protection requires DataProtection:KeyPath.");
}

var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName(builder.Configuration["DataProtection:ApplicationName"] ?? "SystemKnowledgeHub");
if (!string.IsNullOrWhiteSpace(dataProtectionKeyPath))
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyPath));
}

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddKnowledgeHubPersistence(builder.Configuration, builder.Environment);
builder.Services.AddScoped<BusinessFunctionQueries>();
builder.Services.AddScoped<BusinessFunctionService>();
builder.Services.AddScoped<BusinessRuleQueries>();
builder.Services.AddScoped<BusinessRuleService>();
builder.Services.AddScoped<DatabaseKnowledgeQueries>();
builder.Services.AddScoped<DatabaseKnowledgeService>();
builder.Services.AddScoped<DashboardQueries>();
builder.Services.AddScoped<EvidenceSubjectResolver>();
builder.Services.AddScoped<EvidenceQueries>();
builder.Services.AddScoped<EvidenceService>();
builder.Services.AddScoped<IntegrationQueries>();
builder.Services.AddScoped<IntegrationService>();
builder.Services.AddScoped<KnowledgeDocumentQueries>();
builder.Services.AddScoped<KnowledgeDocumentService>();
builder.Services.AddScoped<KnowledgeStatusPolicy>();
builder.Services.AddScoped<KnowledgeStatusService>();
builder.Services.AddScoped<RelationshipEndpointPolicy>();
builder.Services.AddScoped<RelationshipTargetResolver>();
builder.Services.AddScoped<RelationshipQueries>();
builder.Services.AddScoped<RelationshipService>();
builder.Services.AddScoped<SearchQueries>();
builder.Services.AddScoped<SystemQueries>();
builder.Services.AddScoped<SystemService>();
builder.Services.AddScoped<UnknownItemQueries>();
builder.Services.AddScoped<UnknownItemService>();
builder.Services.AddScoped<KnowledgeResolutionService>();
builder.Services.AddScoped<UserQueries>();
builder.Services.AddScoped<UserService>();
builder.Services.AddSingleton<IOptions<LocalAuthenticationOptions>>(Options.Create(local));
builder.Services.AddSingleton<IOptions<OidcAuthenticationOptions>>(Options.Create(oidc));
builder.Services.Configure<PasswordHasherOptions>(options =>
{
    options.CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3;
    options.IterationCount = 220_000;
});
builder.Services.AddSingleton<LocalPasswordService>();
builder.Services.AddSingleton<AuthenticationPrincipalBuilder>();
builder.Services.AddScoped<LocalLoginService>();
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
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
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
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .AddRequirements(new AccessLevelRequirement(AccessLevel.Viewer))
        .Build();
    options.AddPolicy(AccessPolicies.Editor, policy => policy
        .AddRequirements(new AccessLevelRequirement(AccessLevel.Editor)));
    options.AddPolicy(AccessPolicies.Administrator, policy => policy
        .AddRequirements(new AccessLevelRequirement(AccessLevel.Administrator)));
});
builder.Services.AddScoped<IAuthorizationHandler, AccessLevelAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ApiAuthorizationMiddlewareResultHandler>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("ViteDevelopment", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
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
