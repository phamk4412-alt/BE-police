using PoliceBackend.Config;
using PoliceBackend.Controllers;
using PoliceBackend.Models;
using PoliceBackend.Utils;

namespace PoliceBackend.Modules.User;

public static class UserModule
{
    public static IEndpointRouteBuilder MapUserModule(this IEndpointRouteBuilder app, bool demoOpenAccess)
    {
        var api = app.MapGroup("/api");

        api.MapPost("/incidents/analyze", UserController.AnalyzeIncidentAsync)
            .AllowAnonymous();

        api.MapPost("/incidents", UserController.CreateIncidentAsync)
            .Accepts<CreateIncidentRequest>("multipart/form-data")
            .DisableAntiforgery()
            .AllowAnonymous();

        api.MapGet("/incidents/{id:guid}", UserController.GetIncidentByIdAsync)
            .AllowAnonymous();

        api.MapGet("/user/report-history", UserController.GetReportHistoryAsync)
            .AllowAnonymous();

        api.MapGet("/user/nearby-alerts", UserController.GetNearbyAlertsAsync)
            .AllowAnonymous();

        api.MapPost("/user/location/resolve", UserController.ResolveLocationAsync)
            .AllowAnonymous();

        return app;
    }
}
