using PoliceBackend.Config;
using PoliceBackend.Controllers;
using PoliceBackend.Utils;

namespace PoliceBackend.Modules.Support;

public static class SupportModule
{
    public static IEndpointRouteBuilder MapSupportModule(this IEndpointRouteBuilder app, bool demoOpenAccess)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/support/dispatch", SupportController.GetDispatchBoardAsync)
            .AllowAnonymous();

        api.MapGet("/support/dispatch-board", SupportController.GetDispatchBoardAsync)
            .AllowAnonymous();

        api.MapGet("/support/call-intake", SupportController.GetCallIntakeAsync)
            .AllowAnonymous();

        api.MapGet("/support/center-overview", SupportController.GetCenterOverviewAsync)
            .AllowAnonymous();

        api.MapDelete("/support/incidents/{id:guid}", SupportController.DeleteIncidentAsync)
            .AllowAnonymous();

        api.MapGet("/support/news", SupportNewsController.GetNewsAsync)
            .AllowAnonymous();

        api.MapPost("/support/news", SupportNewsController.CreateNewsAsync)
            .AllowAnonymous();

        api.MapPut("/support/news/{id:guid}", SupportNewsController.UpdateNewsAsync)
            .AllowAnonymous();

        api.MapDelete("/support/news/{id:guid}", SupportNewsController.DeleteNewsAsync)
            .AllowAnonymous();

        api.MapPatch("/support/news/{id:guid}/status", SupportNewsController.UpdateNewsStatusAsync)
            .AllowAnonymous();

        api.MapPatch("/support/news/{id:guid}/featured", SupportNewsController.UpdateNewsFeaturedAsync)
            .AllowAnonymous();

        return app;
    }
}
