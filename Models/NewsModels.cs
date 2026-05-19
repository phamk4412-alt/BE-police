using System.Text.Json.Serialization;
using PoliceBackend.Config;

namespace PoliceBackend.Models;

public sealed class NewsRecord
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsFeatured { get; set; }
    public int? FeaturedOrder { get; set; }
    public string Status { get; set; } = NewsStatuses.Draft;
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string UpdatedBy { get; set; } = string.Empty;
}

public sealed record NewsQueryParameters(
    string? Category,
    bool? Newest,
    int? Page,
    int? PageSize);

public sealed record CreateNewsRequest(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("summary")] string? Summary,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("thumbnailUrl")] string? ThumbnailUrl,
    [property: JsonPropertyName("category")] string? Category,
    [property: JsonPropertyName("isFeatured")] bool? IsFeatured,
    [property: JsonPropertyName("featuredOrder")] int? FeaturedOrder,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("publishedAt")] DateTime? PublishedAt);

public sealed record UpdateNewsRequest(
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("summary")] string? Summary,
    [property: JsonPropertyName("content")] string? Content,
    [property: JsonPropertyName("thumbnailUrl")] string? ThumbnailUrl,
    [property: JsonPropertyName("category")] string? Category,
    [property: JsonPropertyName("isFeatured")] bool? IsFeatured,
    [property: JsonPropertyName("featuredOrder")] int? FeaturedOrder,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("publishedAt")] DateTime? PublishedAt);

public sealed record UpdateNewsStatusRequest(
    [property: JsonPropertyName("status")] string Status);

public sealed record UpdateNewsFeaturedRequest(
    [property: JsonPropertyName("isFeatured")] bool IsFeatured,
    [property: JsonPropertyName("featuredOrder")] int? FeaturedOrder);

public sealed record NewsResponse(
    Guid Id,
    string Title,
    string Summary,
    string Content,
    string ThumbnailUrl,
    string Category,
    bool IsFeatured,
    int? FeaturedOrder,
    string Status,
    DateTime? PublishedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string CreatedBy,
    string UpdatedBy);

public sealed record NewsListResponse(
    IReadOnlyCollection<NewsResponse> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);
