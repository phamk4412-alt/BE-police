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
            .AllowAnonymous();

        api.MapPatch("/incidents/{id:guid}/status", PoliceController.UpdateIncidentStatusAsync)
            .AllowAnonymous();

        api.MapPut("/incidents/{id:guid}/status", SupportController.UpdateIncidentStatusAsync)
            .AllowAnonymous();

        api.MapDelete("/incidents/{id:guid}", PoliceController.DeleteIncidentAsync)
            .AllowAnonymous();

        api.MapGet("/police/cases", PoliceController.GetIncidentBoardAsync)
            .AllowAnonymous();

        api.MapPatch("/police/cases/{id:guid}/status", PoliceController.UpdateIncidentStatusAsync)
            .AllowAnonymous();

        api.MapGet("/police/hotspots", PoliceController.GetHotspotsAsync)
            .AllowAnonymous();

        api.MapGet("/police/patrol-vehicles", PoliceController.GetPatrolVehiclesAsync)
            .AllowAnonymous();

        api.MapGet("/police/locations", PoliceController.GetActivePoliceLocations)
            .AllowAnonymous();

        api.MapPost("/police/me/location", PoliceController.UpdateMyLocationAsync)
            .AllowAnonymous();

        api.MapDelete("/police/me/location", PoliceController.EndMyShiftAsync)
            .AllowAnonymous();

        api.MapPost("/police/me/location/end", PoliceController.EndMyShiftByRequestAsync)
            .AllowAnonymous();

        return app;
    }
}
