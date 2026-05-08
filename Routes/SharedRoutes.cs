using PoliceBackend.Controllers;

namespace PoliceBackend.Routes;

public static class SharedRoutes
{
    public static IEndpointRouteBuilder MapSharedRoutes(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", SystemController.GetRoot);
        app.MapGet("/api/health", SystemController.GetHealth);

        app.MapPost("/api/auth/login", AuthController.LoginAsync);
        app.MapPost("/api/auth/register", AuthController.RegisterAsync);
        app.MapPost("/api/auth/logout", AuthController.LogoutAsync);
        app.MapGet("/api/auth/me", AuthController.GetCurrentUser);

        app.MapGet("/api/maps/hcm-boundary", SystemController.GetBoundaryGeoJson);
        app.MapGet("/api/maps/geojson/{fileName}", SystemController.GetGeoJson);
        app.MapGet("/data/maps/{fileName}", SystemController.GetGeoJson);
        app.MapGet("/hcm-boundary.geojson", SystemController.GetBoundaryGeoJson);

        return app;
    }
}
