using PoliceBackend.Database;
using PoliceBackend.Models;
using PoliceBackend.Services;

namespace PoliceBackend.Controllers;

public static class NewsController
{
    public static async Task<IResult> GetFeaturedAsync(
        IncidentDbContext dbContext,
        NewsService newsService,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await newsService.GetFeaturedAsync(dbContext, cancellationToken));
    }

    public static async Task<IResult> GetNewsAsync(
        string? category,
        bool? newest,
        int? page,
        int? pageSize,
        IncidentDbContext dbContext,
        NewsService newsService,
        CancellationToken cancellationToken)
    {
        var result = await newsService.GetPublishedNewsAsync(
            dbContext,
            new NewsQueryParameters(category, newest, page, pageSize),
            cancellationToken);

        return Results.Ok(result);
    }

    public static async Task<IResult> GetNewsByIdAsync(
        Guid id,
        IncidentDbContext dbContext,
        NewsService newsService,
        CancellationToken cancellationToken)
    {
        var news = await newsService.GetPublishedByIdAsync(dbContext, id, cancellationToken);
        return news is null
            ? Results.NotFound(new { message = "Khong tim thay tin tuc da xuat ban." })
            : Results.Ok(news);
    }

}
