using PoliceBackend.Controllers;

namespace PoliceBackend.Modules.News;

public static class NewsModule
{
    public static IEndpointRouteBuilder MapNewsModule(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/news/featured", NewsController.GetFeaturedAsync);
        api.MapGet("/news", NewsController.GetNewsAsync);
        api.MapGet("/news/{id:guid}", NewsController.GetNewsByIdAsync);
        api.MapGet("/events/upcoming", NewsController.GetUpcomingEventsAsync);

        return app;
    }
}
