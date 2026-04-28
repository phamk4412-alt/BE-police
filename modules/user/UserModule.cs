using PoliceBackend.Config;
using PoliceBackend.Controllers;
using PoliceBackend.Utils;

namespace PoliceBackend.Modules.User;

public static class UserModule
{
    public static IEndpointRouteBuilder MapUserModule(this IEndpointRouteBuilder app, bool demoOpenAccess)
    {
        var api = app.MapGroup("/api");

        api.MapPost("/incidents/analyze", UserController.AnalyzeIncidentAsync)
            .ApplyOptionalAuthorization(demoOpenAccess, AuthorizationPolicies.CanSubmitIncident);

        api.MapPost("/incidents", UserController.CreateIncidentAsync)
            .ApplyOptionalAuthorization(demoOpenAccess, AuthorizationPolicies.CanSubmitIncident);

        api.MapGet("/incidents/{id:guid}", UserController.GetIncidentByIdAsync)
            .RequireAuthorization(AuthorizationPolicies.CanTrackIncident);

        api.MapGet("/user/report-history", UserController.GetReportHistoryAsync)
            .ApplyOptionalAuthorization(demoOpenAccess, AuthorizationPolicies.CanSubmitIncident);

        api.MapGet("/user/nearby-alerts", UserController.GetNearbyAlertsAsync)
            .ApplyOptionalAuthorization(demoOpenAccess, AuthorizationPolicies.CanSubmitIncident);

        api.MapPost("/user/location/resolve", UserController.ResolveLocationAsync)
            .ApplyOptionalAuthorization(demoOpenAccess, AuthorizationPolicies.CanSubmitIncident);

        return app;
    }
}
