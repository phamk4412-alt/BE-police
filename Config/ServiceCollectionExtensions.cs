using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using PoliceBackend.Database;
using PoliceBackend.Services;
using PoliceBackend.Utils;

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

        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
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
                    OnRedirectToLogin = context => AuthRedirectUtils.HandleRedirectAsync(
                        context,
                        StatusCodes.Status401Unauthorized),
                    OnRedirectToAccessDenied = context => AuthRedirectUtils.HandleRedirectAsync(
                        context,
                        StatusCodes.Status403Forbidden)
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
        services.AddHttpClient<FacePlusPlusService>();
        services.AddSingleton<IdentityVerificationSessionService>();
        services.AddScoped<AuthService>();
        services.AddScoped<AuditService>();
        services.AddScoped<IncidentService>();
        services.AddScoped<MapDataService>();
        services.AddScoped<NewsService>();

        return services;
    }
}
