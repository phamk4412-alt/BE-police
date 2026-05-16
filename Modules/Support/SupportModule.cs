using PoliceBackend.Config;
using PoliceBackend.Controllers;

namespace PoliceBackend.Modules.Support;

public static class SupportModule
{
    public static IEndpointRouteBuilder MapSupportModule(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/support/dispatch", SupportController.GetDispatchBoardAsync)
            .RequireAuthorization(AuthorizationPolicies.CanViewIncidents);

        api.MapGet("/support/dispatch-board", SupportController.GetDispatchBoardAsync)
            .RequireAuthorization(AuthorizationPolicies.CanViewIncidents);

        api.MapGet("/support/call-intake", SupportController.GetCallIntakeAsync)
            .RequireAuthorization(AuthorizationPolicies.CanViewIncidents);

        api.MapGet("/support/center-overview", SupportController.GetCenterOverviewAsync)
            .RequireAuthorization(AuthorizationPolicies.CanViewIncidents);

        api.MapDelete("/support/incidents/{id:guid}", SupportController.DeleteIncidentAsync)
            .RequireAuthorization(AuthorizationPolicies.CanUpdateIncidents);

        api.MapGet("/support/news", SupportNewsController.GetNewsAsync)
            .RequireAuthorization(AuthorizationPolicies.CanManageNews);

        api.MapPost("/support/news", SupportNewsController.CreateNewsAsync)
            .RequireAuthorization(AuthorizationPolicies.CanManageNews);

        api.MapPut("/support/news/{id:guid}", SupportNewsController.UpdateNewsAsync)
            .RequireAuthorization(AuthorizationPolicies.CanManageNews);

        api.MapDelete("/support/news/{id:guid}", SupportNewsController.DeleteNewsAsync)
            .RequireAuthorization(AuthorizationPolicies.CanManageNews);

        api.MapPatch("/support/news/{id:guid}/status", SupportNewsController.UpdateNewsStatusAsync)
            .RequireAuthorization(AuthorizationPolicies.CanManageNews);

        api.MapPatch("/support/news/{id:guid}/featured", SupportNewsController.UpdateNewsFeaturedAsync)
            .RequireAuthorization(AuthorizationPolicies.CanManageNews);

        return app;
    }
}
