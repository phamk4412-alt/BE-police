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

        return app;
    }
}
