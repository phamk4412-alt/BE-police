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

        app.MapGet("/api/police/online/shift", (HttpContext context, PoliceOnlineService presence) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return presence.GetOrUpdateShift(context) is { } shift ? Results.Ok(shift) : Results.Unauthorized();
        }).RequireAuthorization(AuthorizationPolicies.PoliceOnly);

        app.MapPut("/api/police/online/shift", (PoliceShiftUpdate request, HttpContext context, PoliceOnlineService presence) =>
        {
            if (request.Status is not ("off-duty" or "on-duty" or "break"))
                return Results.BadRequest();

            return presence.GetOrUpdateShift(context, request.Status) is { } shift ? Results.Ok(shift) : Results.Unauthorized();
        }).RequireAuthorization(AuthorizationPolicies.PoliceOnly);

        app.MapGet("/api/support/police/online", (HttpContext context, PoliceOnlineService presence) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(presence.GetOnline());
        }).RequireAuthorization(AuthorizationPolicies.SupportOnly);
        return app;
    }

    private sealed record PoliceShiftUpdate(string? Status);
}
