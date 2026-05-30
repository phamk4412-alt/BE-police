using PoliceBackend.Services.Realtime;

namespace PoliceBackend.Routes;

public static class RealtimeRoutes
{
    public static IEndpointRouteBuilder MapRealtimeEndpoints(this IEndpointRouteBuilder app, bool demoOpenAccess)
    {
        app.MapHub<IncidentHub>("/hubs/incidents")
            .AllowAnonymous();
        return app;
    }
}
