using PoliceBackend.Config;
using PoliceBackend.Services.Realtime;

namespace PoliceBackend.Routes;

public static class RealtimeRoutes
{
    public static IEndpointRouteBuilder MapRealtimeEndpoints(this IEndpointRouteBuilder app, bool demoOpenAccess)
    {
        var incidentHub = app.MapHub<IncidentHub>("/hubs/incidents");
        if (!demoOpenAccess)
        {
            incidentHub.RequireAuthorization(AuthorizationPolicies.CanTrackIncident);
        }

        return app;
    }
}
