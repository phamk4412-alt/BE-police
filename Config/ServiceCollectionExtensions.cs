using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using PoliceBackend.Database;
using PoliceBackend.Models;
using PoliceBackend.Services;
using System.Security.Claims;

namespace PoliceBackend.Config;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPoliceBackend(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
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

        var authSettings = new AuthSettings(configuration);
        services.AddSingleton(authSettings);

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role,
                    ValidIssuer = AuthSettings.TokenIssuer,
                    ValidAudience = AuthSettings.TokenAudience,
                    IssuerSigningKey = authSettings.SigningKey,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        // SignalR websockets cannot send headers, so the hub client passes the token in the query string.
                        var accessToken = context.Request.Query["access_token"].ToString();
                        if (!string.IsNullOrWhiteSpace(accessToken) &&
                            context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async context =>
                    {
                        var logger = GetAuthDebugLogger(context.HttpContext);
                        var subject = context.Principal?.FindFirstValue("sub");

                        if (!Guid.TryParse(subject, out var accountId))
                        {
                            context.Fail("Token missing account id.");
                            return;
                        }

                        // Role and status are read from the database on every request so admin changes apply immediately.
                        var dbContext = context.HttpContext.RequestServices.GetRequiredService<IncidentDbContext>();
                        var profile = await dbContext.AccountProfiles
                            .AsNoTracking()
                            .FirstOrDefaultAsync(item => item.Id == accountId, context.HttpContext.RequestAborted);

                        if (profile is null || profile.Status != AccountStatuses.Active)
                        {
                            logger.LogInformation("Rejected token for missing or locked account {AccountId}.", accountId);
                            context.Fail("Account is missing or locked.");
                            return;
                        }

                        var role = ToAppRole(profile.Role);
                        if (role is null)
                        {
                            context.Fail("Account role is invalid.");
                            return;
                        }

                        if (context.Principal?.Identity is ClaimsIdentity identity)
                        {
                            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, profile.Id.ToString()));
                            identity.AddClaim(new Claim(ClaimTypes.Name, profile.DisplayName));
                            identity.AddClaim(new Claim(ClaimTypes.Email, profile.Email));
                            identity.AddClaim(new Claim(ClaimTypes.Role, role));
                        }
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

            var requireProductionDatabase = configuration.GetValue(
                "POLICE_REQUIRE_PRODUCTION_DATABASE",
                false);

            if (!environment.IsDevelopment() &&
                requireProductionDatabase &&
                provider == DatabaseProviders.InMemory)
            {
                throw new InvalidOperationException(
                    "Production khong duoc dung in-memory database. Cau hinh POLICE_DATABASE_PROVIDER=sqlserver va POLICE_SQLSERVER_CONNECTION de ket noi DB that.");
            }

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
        services.AddHttpClient<DiditVerificationService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddSingleton<IdentityVerificationSessionService>();
        services.AddSingleton<GoogleAuthService>();
        services.AddSingleton<AppleAuthService>();
        services.AddSingleton<AuthTokenService>();
        services.AddSingleton<PasswordService>();
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
            "{Message} path={Path}, method={Method}, isAuthenticated={IsAuthenticated}, authType={AuthType}, user={User}, role={Role}, hasAuthorizationHeader={HasAuthorizationHeader}.",
            message,
            context.Request.Path,
            context.Request.Method,
            user.Identity?.IsAuthenticated == true,
            user.Identity?.AuthenticationType ?? "(none)",
            user.Identity?.Name ?? "(none)",
            user.FindFirstValue(ClaimTypes.Role) ?? "(none)",
            context.Request.Headers.ContainsKey("Authorization"));
    }

    private static string? ToAppRole(string? role)
    {
        return AccountRoles.Normalize(role) switch
        {
            AccountRoles.Admin => AppRoles.Admin,
            AccountRoles.Police => AppRoles.Police,
            AccountRoles.Support => AppRoles.Support,
            AccountRoles.User => AppRoles.User,
            _ => null
        };
    }
}
