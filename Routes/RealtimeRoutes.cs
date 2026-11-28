using PoliceBackend.Services.Realtime;
using PoliceBackend.Config;
using PoliceBackend.Services;

namespace PoliceBackend.Routes;

public static class RealtimeRoutes
{
    public static IEndpointRouteBuilder MapRealtimeEndpoints(this IEndpointRouteBuilder app, bool demoOpenAccess)
    {
        app.MapHub<IncidentHub>("/hubs/incidents")
            .RequireAuthorization(AuthorizationPolicies.CanTrackIncident);

        app.MapPost("/api/police/online/heartbeat", (HttpContext context, PoliceOnlineService presence) =>
            presence.Heartbeat(context) ? Results.NoContent() : Results.Unauthorized())
            .RequireAuthorization(AuthorizationPolicies.PoliceOnly);

        app.MapGet("/api/support/police/online", (HttpContext context, PoliceOnlineService presence) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(presence.GetOnline());
        }).RequireAuthorization(AuthorizationPolicies.SupportOnly);
        return app;
    }
}
