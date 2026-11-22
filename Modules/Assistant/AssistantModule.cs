using PoliceBackend.Config;
using PoliceBackend.Controllers;

namespace PoliceBackend.Modules.Assistant;

public static class AssistantModule
{
    public static IEndpointRouteBuilder MapAssistantModule(this IEndpointRouteBuilder app)
    {
        // Every signed-in role can chat; each account only sees its own messages.
        var api = app.MapGroup("/api/assistant")
            .RequireAuthorization(AuthorizationPolicies.CanTrackIncident);

        api.MapGet("/status", AssistantController.GetStatusAsync);
        api.MapGet("/messages", AssistantController.GetMessagesAsync);
        api.MapPost("/chat", AssistantController.ChatAsync);
        api.MapDelete("/messages", AssistantController.ClearMessagesAsync);

        return app;
    }
}
