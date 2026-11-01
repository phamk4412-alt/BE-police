using PoliceBackend.Services.Realtime;
using PoliceBackend.Config;

namespace PoliceBackend.Routes;

public static class RealtimeRoutes
{
    public static IEndpointRouteBuilder MapRealtimeEndpoints(this IEndpointRouteBuilder app, bool demoOpenAccess)
    {
        app.MapHub<IncidentHub>("/hubs/incidents")
            .RequireAuthorization(AuthorizationPolicies.CanTrackIncident);
        return app;
    }
}
