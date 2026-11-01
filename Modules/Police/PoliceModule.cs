using PoliceBackend.Config;
using PoliceBackend.Controllers;
using PoliceBackend.Utils;

namespace PoliceBackend.Modules.Police;

public static class PoliceModule
{
    public static IEndpointRouteBuilder MapPoliceModule(this IEndpointRouteBuilder app, bool demoOpenAccess)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/incidents", PoliceController.GetIncidentBoardAsync)
            .RequireAuthorization(AuthorizationPolicies.CanTrackIncident);

        api.MapPatch("/incidents/{id:guid}/status", PoliceController.UpdateIncidentStatusAsync)
            .RequireAuthorization(AuthorizationPolicies.CanUpdateIncidents);

        api.MapPut("/incidents/{id:guid}/status", SupportController.UpdateIncidentStatusAsync)
            .RequireAuthorization(AuthorizationPolicies.CanUpdateIncidents);

        api.MapDelete("/incidents/{id:guid}", PoliceController.DeleteIncidentAsync)
            .RequireAuthorization(AuthorizationPolicies.CanUpdateIncidents);

        api.MapGet("/police/cases", PoliceController.GetIncidentBoardAsync)
            .RequireAuthorization(AuthorizationPolicies.PoliceOnly);

        api.MapPatch("/police/cases/{id:guid}/status", PoliceController.UpdateIncidentStatusAsync)
            .RequireAuthorization(AuthorizationPolicies.PoliceOnly);

        api.MapGet("/police/hotspots", PoliceController.GetHotspotsAsync)
            .RequireAuthorization(AuthorizationPolicies.PoliceOnly);

        api.MapGet("/police/patrol-vehicles", PoliceController.GetPatrolVehiclesAsync)
            .RequireAuthorization(AuthorizationPolicies.PoliceOnly);

        api.MapGet("/police/locations", PoliceController.GetActivePoliceLocations)
            .RequireAuthorization(AuthorizationPolicies.CanTrackIncident);

        api.MapPost("/police/me/location", PoliceController.UpdateMyLocationAsync)
            .RequireAuthorization(AuthorizationPolicies.PoliceOnly);

        api.MapDelete("/police/me/location", PoliceController.EndMyShiftAsync)
            .RequireAuthorization(AuthorizationPolicies.PoliceOnly);

        api.MapPost("/police/me/location/end", PoliceController.EndMyShiftByRequestAsync)
            .RequireAuthorization(AuthorizationPolicies.PoliceOnly);

        return app;
    }
}
