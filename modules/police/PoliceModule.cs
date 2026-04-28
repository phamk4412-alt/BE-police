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
            .ApplyOptionalAuthorization(demoOpenAccess, AuthorizationPolicies.CanViewIncidents);

        api.MapPatch("/incidents/{id:guid}/status", PoliceController.UpdateIncidentStatusAsync)
            .RequireAuthorization(AuthorizationPolicies.CanUpdateIncidents);

        api.MapGet("/police/cases", PoliceController.GetIncidentBoardAsync)
            .ApplyOptionalAuthorization(demoOpenAccess, AuthorizationPolicies.CanViewIncidents);

        api.MapPatch("/police/cases/{id:guid}/status", PoliceController.UpdateIncidentStatusAsync)
            .RequireAuthorization(AuthorizationPolicies.CanUpdateIncidents);

        api.MapGet("/police/hotspots", PoliceController.GetHotspotsAsync)
            .ApplyOptionalAuthorization(demoOpenAccess, AuthorizationPolicies.CanViewIncidents);

        api.MapGet("/police/patrol-vehicles", PoliceController.GetPatrolVehiclesAsync)
            .RequireAuthorization(AuthorizationPolicies.CanUpdateIncidents);

        return app;
    }
}
