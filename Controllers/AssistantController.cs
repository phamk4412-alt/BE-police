using System.Security.Claims;
using PoliceBackend.Database;
using PoliceBackend.Models;
using PoliceBackend.Services;

namespace PoliceBackend.Controllers;

public static class AssistantController
{
    public static async Task<IResult> GetMessagesAsync(
        HttpContext context,
        IncidentDbContext dbContext,
        AssistantService assistantService,
        int? limit,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(context.User, out var accountId))
        {
            return Results.Unauthorized();
        }

        var messages = await assistantService.GetHistoryAsync(dbContext, accountId, limit ?? 50, cancellationToken);
        return Results.Ok(messages);
    }

    public static async Task<IResult> ChatAsync(
        AssistantChatRequest request,
        HttpContext context,
        IncidentDbContext dbContext,
        AssistantService assistantService,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(context.User, out var accountId))
        {
            return Results.Unauthorized();
        }

        if (!AssistantService.TryValidate(request, out var message, out var imageDataUrl, out var error))
        {
            return Results.BadRequest(new { message = error });
        }

        try
        {
            var result = await assistantService.ChatAsync(
                dbContext,
                accountId,
                context.User.Identity?.Name ?? "Người dùng",
                context.User.FindFirstValue(ClaimTypes.Role) ?? "User",
                message,
                imageDataUrl,
                cancellationToken);

            return Results.Ok(result);
        }
        catch (LmStudioException lmError)
        {
            return Results.Json(new { message = lmError.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    public static async Task<IResult> ClearMessagesAsync(
        HttpContext context,
        IncidentDbContext dbContext,
        AssistantService assistantService,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(context.User, out var accountId))
        {
            return Results.Unauthorized();
        }

        var deleted = await assistantService.ClearHistoryAsync(dbContext, accountId, cancellationToken);
        return Results.Ok(new { deleted });
    }

    public static async Task<IResult> GetStatusAsync(
        AssistantService assistantService,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await assistantService.GetStatusAsync(cancellationToken));
    }

    private static bool TryGetAccountId(ClaimsPrincipal user, out Guid accountId) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out accountId);
}
