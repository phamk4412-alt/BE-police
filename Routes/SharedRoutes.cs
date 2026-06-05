using PoliceBackend.Controllers;

namespace PoliceBackend.Routes;

public static class SharedRoutes
{
    public static IEndpointRouteBuilder MapSharedRoutes(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", SystemController.GetRoot);
        app.MapGet("/api/health", SystemController.GetHealth);

        app.MapPost("/api/auth/logout", AuthController.LogoutAsync);
        app.MapGet("/api/auth/me", AuthController.GetCurrentUser);
        app.MapPost("/api/accounts/sync", AccountController.SyncAsync);
        app.MapGet("/api/accounts", AccountController.GetProfilesAsync);
        app.MapGet("/api/identity/state", IdentityController.GetState);
        app.MapPost("/api/identity/cccd", IdentityController.SaveCccdState);
        app.MapPost("/api/identity/face", IdentityController.SaveFaceState);
        app.MapPost("/api/identity/reset", IdentityController.ResetState);
        app.MapPost("/api/identity/didit/session", IdentityController.CreateDiditSessionAsync);
        app.MapPost("/api/identity/didit/session/{sessionId}/complete", IdentityController.CompleteDiditSessionAsync);

        app.MapGet("/api/maps/hcm-boundary", SystemController.GetBoundaryGeoJson);
        app.MapGet("/api/maps/geojson/{fileName}", SystemController.GetGeoJson);
        app.MapGet("/data/maps/{fileName}", SystemController.GetGeoJson);
        app.MapGet("/hcm-boundary.geojson", SystemController.GetBoundaryGeoJson);
        return app;
    }
}
