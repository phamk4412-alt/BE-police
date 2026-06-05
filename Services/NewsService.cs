using Microsoft.EntityFrameworkCore;
using PoliceBackend.Config;
using PoliceBackend.Database;
using PoliceBackend.Models;

namespace PoliceBackend.Services;

public sealed class NewsService
{
    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        NewsStatuses.Draft,
        NewsStatuses.Published,
        NewsStatuses.Hidden
    };

    public async Task<IReadOnlyCollection<NewsResponse>> GetFeaturedAsync(
        IncidentDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var news = await dbContext.News
            .AsNoTracking()
            .Where(item => item.Status.ToLower() == NewsStatuses.Published && item.IsFeatured && item.FeaturedOrder != null)
            .OrderBy(item => item.FeaturedOrder)
            .ThenByDescending(item => item.PublishedAt ?? item.CreatedAt)
            .Take(4)
            .ToListAsync(cancellationToken);

        return news.Select(item => item.ToResponse()).ToArray();
    }

    public async Task<NewsListResponse> GetPublishedNewsAsync(
        IncidentDbContext dbContext,
        NewsQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(parameters.Page ?? 1, 1);
        var pageSize = Math.Clamp(parameters.PageSize ?? 10, 1, 50);

        var query = dbContext.News
            .AsNoTracking()
            .Where(item => item.Status.ToLower() == NewsStatuses.Published);

        if (!string.IsNullOrWhiteSpace(parameters.Category))
        {
            var category = parameters.Category.Trim();
            query = query.Where(item => item.Category == category);
        }

        query = parameters.Newest == false
            ? query.OrderBy(item => item.PublishedAt ?? item.CreatedAt).ThenBy(item => item.CreatedAt)
            : query.OrderByDescending(item => item.PublishedAt ?? item.CreatedAt).ThenByDescending(item => item.CreatedAt);

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new NewsListResponse(
            items.Select(item => item.ToResponse()).ToArray(),
            page,
            pageSize,
            totalItems,
            (int)Math.Ceiling(totalItems / (double)pageSize));
    }

    public async Task<NewsResponse?> GetPublishedByIdAsync(
        IncidentDbContext dbContext,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var news = await dbContext.News
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id && item.Status.ToLower() == NewsStatuses.Published, cancellationToken);

        return news?.ToResponse();
    }

    public async Task<NewsListResponse> GetAllForSupportAsync(
        IncidentDbContext dbContext,
        NewsQueryParameters parameters,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(parameters.Page ?? 1, 1);
        var pageSize = Math.Clamp(parameters.PageSize ?? 20, 1, 100);

        var query = dbContext.News.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(parameters.Category))
        {
            var category = parameters.Category.Trim();
            query = query.Where(item => item.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = NormalizeStatus(status);
            query = query.Where(item => item.Status.ToLower() == normalizedStatus);
        }

        query = query
            .OrderByDescending(item => item.PublishedAt ?? item.CreatedAt)
            .ThenByDescending(item => item.CreatedAt);

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new NewsListResponse(
            items.Select(item => item.ToResponse()).ToArray(),
            page,
            pageSize,
            totalItems,
            (int)Math.Ceiling(totalItems / (double)pageSize));
    }

    public async Task<(NewsResponse? News, string? Error)> CreateAsync(
        IncidentDbContext dbContext,
        CreateNewsRequest request,
        ActorSnapshot actor,
        CancellationToken cancellationToken = default)
    {
        if (!ValidateRequired(request.Title, request.Content, out var error))
        {
            return (null, error);
        }

        var status = NormalizeStatus(request.Status);
        var now = DateTime.UtcNow;
        var news = new NewsRecord
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Summary = request.Summary?.Trim() ?? string.Empty,
            Content = request.Content.Trim(),
            ThumbnailUrl = request.ThumbnailUrl?.Trim() ?? string.Empty,
            Category = request.Category?.Trim() ?? string.Empty,
            IsFeatured = request.IsFeatured == true,
            FeaturedOrder = request.FeaturedOrder,
            Status = status,
            PublishedAt = ResolvePublishedAt(status, request.PublishedAt, null, now),
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = actor.DisplayName,
            UpdatedBy = actor.DisplayName
        };

        if (!ValidateFeatured(news.IsFeatured, news.FeaturedOrder, out error))
        {
            return (null, error);
        }

        dbContext.News.Add(news);
        await NormalizeFeaturedOrdersAsync(dbContext, news, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (news.ToResponse(), null);
    }

    public async Task<(NewsResponse? News, string? Error)> UpdateAsync(
        IncidentDbContext dbContext,
        Guid id,
        UpdateNewsRequest request,
        ActorSnapshot actor,
        CancellationToken cancellationToken = default)
    {
        var news = await dbContext.News.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (news is null)
        {
            return (null, "Khong tim thay tin tuc.");
        }

        var title = request.Title ?? news.Title;
        var content = request.Content ?? news.Content;
        if (!ValidateRequired(title, content, out var error))
        {
            return (null, error);
        }

        var now = DateTime.UtcNow;
        var nextStatus = request.Status is null ? news.Status : NormalizeStatus(request.Status);
        var nextIsFeatured = request.IsFeatured ?? news.IsFeatured;
        var nextFeaturedOrder = request.FeaturedOrder ?? news.FeaturedOrder;

        if (!ValidateFeatured(nextIsFeatured, nextFeaturedOrder, out error))
        {
            return (null, error);
        }

        news.Title = title.Trim();
        news.Content = content.Trim();
        news.Summary = request.Summary?.Trim() ?? news.Summary;
        news.ThumbnailUrl = request.ThumbnailUrl?.Trim() ?? news.ThumbnailUrl;
        news.Category = request.Category?.Trim() ?? news.Category;
        news.IsFeatured = nextIsFeatured;
        news.FeaturedOrder = nextIsFeatured ? nextFeaturedOrder : null;
        news.Status = nextStatus;
        news.PublishedAt = ResolvePublishedAt(nextStatus, request.PublishedAt, news.PublishedAt, now);
        news.UpdatedAt = now;
        news.UpdatedBy = actor.DisplayName;

        await NormalizeFeaturedOrdersAsync(dbContext, news, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (news.ToResponse(), null);
    }

    public async Task<bool> DeleteAsync(
        IncidentDbContext dbContext,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var news = await dbContext.News.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (news is null)
        {
            return false;
        }

        dbContext.News.Remove(news);
        await dbContext.SaveChangesAsync(cancellationToken);
        await NormalizeFeaturedOrdersAsync(dbContext, null, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<(NewsResponse? News, string? Error)> UpdateStatusAsync(
        IncidentDbContext dbContext,
        Guid id,
        UpdateNewsStatusRequest request,
        ActorSnapshot actor,
        CancellationToken cancellationToken = default)
    {
        var news = await dbContext.News.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (news is null)
        {
            return (null, "Khong tim thay tin tuc.");
        }

        var now = DateTime.UtcNow;
        news.Status = NormalizeStatus(request.Status);
        news.PublishedAt = ResolvePublishedAt(news.Status, null, news.PublishedAt, now);
        news.UpdatedAt = now;
        news.UpdatedBy = actor.DisplayName;

        await dbContext.SaveChangesAsync(cancellationToken);
        return (news.ToResponse(), null);
    }

    public async Task<(NewsResponse? News, string? Error)> UpdateFeaturedAsync(
        IncidentDbContext dbContext,
        Guid id,
        UpdateNewsFeaturedRequest request,
        ActorSnapshot actor,
        CancellationToken cancellationToken = default)
    {
        var news = await dbContext.News.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (news is null)
        {
            return (null, "Khong tim thay tin tuc.");
        }

        if (!ValidateFeatured(request.IsFeatured, request.FeaturedOrder, out var error))
        {
            return (null, error);
        }

        news.IsFeatured = request.IsFeatured;
        news.FeaturedOrder = request.IsFeatured ? request.FeaturedOrder : null;
        news.UpdatedAt = DateTime.UtcNow;
        news.UpdatedBy = actor.DisplayName;

        await NormalizeFeaturedOrdersAsync(dbContext, news, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (news.ToResponse(), null);
    }

    private static bool ValidateRequired(string title, string content, out string? error)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            error = "Title khong duoc rong.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            error = "Content khong duoc rong.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool ValidateFeatured(bool isFeatured, int? featuredOrder, out string? error)
    {
        if (!isFeatured)
        {
            error = null;
            return true;
        }

        if (featuredOrder is < 1 or > 4 or null)
        {
            error = "FeaturedOrder phai nam trong khoang 1 den 4 khi IsFeatured = true.";
            return false;
        }

        error = null;
        return true;
    }

    private static string NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return NewsStatuses.Draft;
        }

        var normalized = status.Trim().ToLowerInvariant();
        if (!ValidStatuses.Contains(normalized))
        {
            throw new ArgumentException("Status chi duoc la draft, published hoac hidden.", nameof(status));
        }

        return normalized;
    }

    private static DateTime? ResolvePublishedAt(
        string status,
        DateTime? requested,
        DateTime? current,
        DateTime now)
    {
        if (requested is not null)
        {
            return requested;
        }

        if (status == NewsStatuses.Published)
        {
            return current ?? now;
        }

        return current;
    }

    private static async Task NormalizeFeaturedOrdersAsync(
        IncidentDbContext dbContext,
        NewsRecord? prioritizedNews,
        CancellationToken cancellationToken)
    {
        var featuredNews = await dbContext.News
            .Where(item => item.IsFeatured)
            .OrderBy(item => item.FeaturedOrder ?? 5)
            .ThenByDescending(item => item.UpdatedAt)
            .ToListAsync(cancellationToken);

        if (prioritizedNews is { IsFeatured: true, FeaturedOrder: not null })
        {
            featuredNews.RemoveAll(item => item.Id == prioritizedNews.Id);
            featuredNews.Insert(0, prioritizedNews);
        }

        var order = 1;
        foreach (var news in featuredNews)
        {
            if (order > 4)
            {
                news.IsFeatured = false;
                news.FeaturedOrder = null;
                continue;
            }

            if (prioritizedNews is not null && news.Id == prioritizedNews.Id)
            {
                news.FeaturedOrder = prioritizedNews.FeaturedOrder;
                continue;
            }

            if (prioritizedNews?.FeaturedOrder == order)
            {
                order++;
            }

            if (order > 4)
            {
                news.IsFeatured = false;
                news.FeaturedOrder = null;
                continue;
            }

            news.FeaturedOrder = order++;
        }
    }
}
