using PoliceBackend.Database;
using PoliceBackend.Models;
using PoliceBackend.Services;

namespace PoliceBackend.Controllers;

public static class SupportNewsController
{
    public static async Task<IResult> GetNewsAsync(
        string? category,
        string? status,
        int? page,
        int? pageSize,
        IncidentDbContext dbContext,
        NewsService newsService,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await newsService.GetAllForSupportAsync(
                dbContext,
                new NewsQueryParameters(category, true, page, pageSize),
                status,
                cancellationToken);

            return Results.Ok(result);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    public static async Task<IResult> CreateNewsAsync(
        CreateNewsRequest request,
        HttpContext context,
        IncidentDbContext dbContext,
        NewsService newsService,
        AuthService authService,
        CancellationToken cancellationToken)
    {
        try
        {
            var actor = authService.GetActorSnapshot(context.User);
            var (news, error) = await newsService.CreateAsync(dbContext, request, actor, cancellationToken);
            return error is not null
                ? Results.BadRequest(new { message = error })
                : Results.Ok(news);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    public static async Task<IResult> UpdateNewsAsync(
        Guid id,
        UpdateNewsRequest request,
        HttpContext context,
        IncidentDbContext dbContext,
        NewsService newsService,
        AuthService authService,
        CancellationToken cancellationToken)
    {
        try
        {
            var actor = authService.GetActorSnapshot(context.User);
            var (news, error) = await newsService.UpdateAsync(dbContext, id, request, actor, cancellationToken);
            return error switch
            {
                null => Results.Ok(news),
                "Khong tim thay tin tuc." => Results.NotFound(new { message = error }),
                _ => Results.BadRequest(new { message = error })
            };
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    public static async Task<IResult> DeleteNewsAsync(
        Guid id,
        IncidentDbContext dbContext,
        NewsService newsService,
        CancellationToken cancellationToken)
    {
        return await newsService.DeleteAsync(dbContext, id, cancellationToken)
            ? Results.NoContent()
            : Results.NotFound(new { message = "Khong tim thay tin tuc." });
    }

    public static async Task<IResult> UpdateNewsStatusAsync(
        Guid id,
        UpdateNewsStatusRequest request,
        HttpContext context,
        IncidentDbContext dbContext,
        NewsService newsService,
        AuthService authService,
        CancellationToken cancellationToken)
    {
        try
        {
            var actor = authService.GetActorSnapshot(context.User);
            var (news, error) = await newsService.UpdateStatusAsync(dbContext, id, request, actor, cancellationToken);
            return error is null
                ? Results.Ok(news)
                : Results.NotFound(new { message = error });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    public static async Task<IResult> UpdateNewsFeaturedAsync(
        Guid id,
        UpdateNewsFeaturedRequest request,
        HttpContext context,
        IncidentDbContext dbContext,
        NewsService newsService,
        AuthService authService,
        CancellationToken cancellationToken)
    {
        var actor = authService.GetActorSnapshot(context.User);
        var (news, error) = await newsService.UpdateFeaturedAsync(dbContext, id, request, actor, cancellationToken);
        return error switch
        {
            null => Results.Ok(news),
            "Khong tim thay tin tuc." => Results.NotFound(new { message = error }),
            _ => Results.BadRequest(new { message = error })
        };
    }
}
