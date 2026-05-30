using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using PoliceBackend.Database;
using PoliceBackend.Services;
using PoliceBackend.Utils;
using System.Security.Claims;

namespace PoliceBackend.Config;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPoliceBackend(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var useCrossSiteCookies = configuration.GetValue(
            "POLICE_CROSS_SITE_COOKIES",
            !environment.IsDevelopment());

        services.Configure<JsonOptions>(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = null;
        });

        services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = 10 * 1024 * 1024;
        });

        services.AddMemoryCache();
        services.AddHttpContextAccessor();

        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyNames.OpenRealtime, policy =>
                policy
                    .SetIsOriginAllowed(AllowedOriginPolicy.IsAllowedOrigin)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials());
        });

        var clerkAuthority =
            configuration["CLERK_AUTHORITY"] ??
            configuration["Clerk:Authority"];

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "PoliceSmartHub.AuthSelector";
                options.DefaultChallengeScheme = "PoliceSmartHub.AuthSelector";
            })
            .AddPolicyScheme("PoliceSmartHub.AuthSelector", "Cookie or Clerk bearer", options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    var authorization = context.Request.Headers.Authorization.ToString();
                    return authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                        ? JwtBearerDefaults.AuthenticationScheme
                        : CookieAuthenticationDefaults.AuthenticationScheme;
                };
            })
            .AddCookie(options =>
            {
                options.Cookie.Name = "PoliceSmartHub.Auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = useCrossSiteCookies ? SameSiteMode.None : SameSiteMode.Lax;
                options.Cookie.SecurePolicy = useCrossSiteCookies
                    ? CookieSecurePolicy.Always
                    : CookieSecurePolicy.SameAsRequest;
                options.LoginPath = "/";
                options.AccessDeniedPath = "/";
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.Events = new CookieAuthenticationEvents
                {
                    OnRedirectToLogin = context =>
                    {
                        LogAuthDebug(
                            context.HttpContext,
                            "Cookie auth rejected request with 401: missing or expired PoliceSmartHub.Auth cookie.");
                        return AuthRedirectUtils.HandleRedirectAsync(
                            context,
                            StatusCodes.Status401Unauthorized);
                    },
                    OnRedirectToAccessDenied = context =>
                    {
                        LogAuthDebug(
                            context.HttpContext,
                            "Cookie auth rejected request with 403: authenticated user does not satisfy required policy.");
                        return AuthRedirectUtils.HandleRedirectAsync(
                            context,
                            StatusCodes.Status403Forbidden);
                    }
                };
            })
            .AddJwtBearer(options =>
            {
                if (!string.IsNullOrWhiteSpace(clerkAuthority))
                {
                    options.Authority = clerkAuthority;
                }

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role,
                    ValidateAudience = false,
                    ValidateIssuer = !string.IsNullOrWhiteSpace(clerkAuthority)
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        LogAuthDebug(
                            context.HttpContext,
                            $"Bearer auth selected: hasBearerHeader={context.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)}.");
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async context =>
                    {
                        var logger = GetAuthDebugLogger(context.HttpContext);
                        var subject = context.Principal?.FindFirstValue("sub");

                        if (string.IsNullOrWhiteSpace(subject))
                        {
                            logger.LogWarning("Clerk token validated but no subject claim was present.");
                            context.Fail("Clerk token missing sub claim.");
                            return;
                        }

                        try
                        {
                            var clerkAdminService = context.HttpContext.RequestServices.GetRequiredService<ClerkAdminService>();
                            var clerkUser = await clerkAdminService.GetUserAsync(subject, context.HttpContext.RequestAborted);
                            var role = NormalizeRole(clerkUser.Role);

                            if (role is null)
                            {
                                logger.LogWarning(
                                    "Clerk user {Subject} has invalid role metadata value {Role}.",
                                    subject,
                                    clerkUser.Role);
                                context.Fail("Clerk user role metadata is missing or invalid.");
                                return;
                            }

                            if (context.Principal?.Identity is ClaimsIdentity identity)
                            {
                                identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, clerkUser.Id));
                                identity.AddClaim(new Claim(ClaimTypes.Name, clerkUser.Name));
                                identity.AddClaim(new Claim(ClaimTypes.Email, clerkUser.Email));
                                identity.AddClaim(new Claim(ClaimTypes.Role, role));
                            }

                            logger.LogInformation(
                                "Clerk auth success: subject={Subject}, name={Name}, role={Role}.",
                                subject,
                                clerkUser.Name,
                                role);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Clerk auth failed while loading user metadata for subject {Subject}.", subject);
                            context.Fail("Cannot load Clerk user metadata.");
                        }
                    },
                    OnAuthenticationFailed = context =>
                    {
                        GetAuthDebugLogger(context.HttpContext).LogWarning(
                            context.Exception,
                            "Bearer auth failed before authorization.");
                        return Task.CompletedTask;
                    },
                    OnChallenge = context =>
                    {
                        LogAuthDebug(
                            context.HttpContext,
                            $"Bearer auth rejected request with 401: error={context.Error}, description={context.ErrorDescription}.");
                        return Task.CompletedTask;
                    },
                    OnForbidden = context =>
                    {
                        LogAuthDebug(
                            context.HttpContext,
                            "Bearer auth rejected request with 403: authenticated user does not satisfy required policy.");
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.AdminOnly, policy => policy.RequireRole(AppRoles.Admin));
            options.AddPolicy(AuthorizationPolicies.UserOnly, policy => policy.RequireRole(AppRoles.User));
            options.AddPolicy(AuthorizationPolicies.PoliceOnly, policy => policy.RequireRole(AppRoles.Police));
            options.AddPolicy(AuthorizationPolicies.SupportOnly, policy => policy.RequireRole(AppRoles.Support));
            options.AddPolicy(AuthorizationPolicies.CanSubmitIncident, policy => policy.RequireRole(AppRoles.User));
            options.AddPolicy(AuthorizationPolicies.CanViewIncidents, policy => policy.RequireRole(AppRoles.Admin, AppRoles.Police, AppRoles.Support));
            options.AddPolicy(AuthorizationPolicies.CanTrackIncident, policy => policy.RequireRole(AppRoles.Admin, AppRoles.Police, AppRoles.Support, AppRoles.User));
            options.AddPolicy(AuthorizationPolicies.CanUpdateIncidents, policy => policy.RequireRole(AppRoles.Admin, AppRoles.Police, AppRoles.Support));
            options.AddPolicy(AuthorizationPolicies.CanAuditAndExport, policy => policy.RequireRole(AppRoles.Admin));
            options.AddPolicy(AuthorizationPolicies.CanManageNews, policy => policy.RequireRole(AppRoles.Admin, AppRoles.Support));
        });

        services.AddSignalR().AddJsonProtocol(options =>
        {
            options.PayloadSerializerOptions.PropertyNamingPolicy = null;
        });

        services.AddDbContext<IncidentDbContext>(options =>
        {
            var provider = DatabaseConfiguration.ResolveProvider(configuration);
            var sqlServerConnection = DatabaseConfiguration.ResolveConnectionString(configuration, "SqlServer");
            var postgresConnection = DatabaseConfiguration.ResolveConnectionString(configuration, "Postgres");

            options.ConfigureWarnings(warnings =>
                warnings.Ignore(RelationalEventId.PendingModelChangesWarning));

            switch (provider)
            {
                case DatabaseProviders.SqlServer:
                    if (string.IsNullOrWhiteSpace(sqlServerConnection))
                    {
                        throw new InvalidOperationException("ConnectionStrings:SqlServer chua duoc cau hinh.");
                    }

                    options.UseSqlServer(sqlServerConnection);
                    break;

                case DatabaseProviders.Postgres:
                    if (string.IsNullOrWhiteSpace(postgresConnection))
                    {
                        throw new InvalidOperationException("ConnectionStrings:Postgres chua duoc cau hinh.");
                    }

                    options.UseNpgsql(postgresConnection);
                    break;

                case DatabaseProviders.InMemory:
                    options.UseInMemoryDatabase("PoliceSmartHub");
                    break;

                default:
                    throw new InvalidOperationException("DatabaseProvider phai la 'inmemory', 'sqlserver' hoac 'postgres'.");
            }
        });

        services.AddSingleton<IncidentAnalysisService>();
        services.AddSingleton<PolicePresenceService>();
        services.AddHttpClient<ClerkAdminService>(client =>
        {
            client.BaseAddress = new Uri("https://api.clerk.com/v1/");
        });
        services.AddHttpClient<DiditVerificationService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddSingleton<IdentityVerificationSessionService>();
        services.AddScoped<AuthService>();
        services.AddScoped<AccountProfileService>();
        services.AddScoped<AuditService>();
        services.AddScoped<IncidentService>();
        services.AddScoped<MapDataService>();
        services.AddScoped<NewsService>();

        return services;
    }

    private static ILogger GetAuthDebugLogger(HttpContext context) =>
        context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("PoliceBackend.AuthDebug");

    private static void LogAuthDebug(HttpContext context, string message)
    {
        var user = context.User;
        GetAuthDebugLogger(context).LogInformation(
            "{Message} path={Path}, method={Method}, isAuthenticated={IsAuthenticated}, authType={AuthType}, user={User}, role={Role}, hasAuthorizationHeader={HasAuthorizationHeader}, hasBackendCookie={HasBackendCookie}.",
            message,
            context.Request.Path,
            context.Request.Method,
            user.Identity?.IsAuthenticated == true,
            user.Identity?.AuthenticationType ?? "(none)",
            user.Identity?.Name ?? "(none)",
            user.FindFirstValue(ClaimTypes.Role) ?? "(none)",
            context.Request.Headers.ContainsKey("Authorization"),
            context.Request.Cookies.ContainsKey("PoliceSmartHub.Auth"));
    }

    private static string? NormalizeRole(string? role)
    {
        return role?.ToLowerInvariant() switch
        {
            "admin" => AppRoles.Admin,
            "police" => AppRoles.Police,
            "support" => AppRoles.Support,
            "user" => AppRoles.User,
            _ => null
        };
    }
}
