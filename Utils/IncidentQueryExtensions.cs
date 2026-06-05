using PoliceBackend.Models;

namespace PoliceBackend.Utils;

public static class IncidentQueryExtensions
{
    public static IQueryable<IncidentRecord> ApplyFilters(
        this IQueryable<IncidentRecord> query,
        IncidentQueryParameters parameters,
        Func<string?, string> normalizeStatus)
    {
        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            var normalizedSearch = parameters.Search.Trim().ToLowerInvariant();
            query = query.Where(item =>
                item.Title.ToLower().Contains(normalizedSearch) ||
                item.Detail.ToLower().Contains(normalizedSearch) ||
                item.Category.ToLower().Contains(normalizedSearch) ||
                item.District.ToLower().Contains(normalizedSearch));
        }

        if (!string.IsNullOrWhiteSpace(parameters.Status))
        {
            var normalizedStatus = normalizeStatus(parameters.Status);
            query = query.Where(item => item.Status == normalizedStatus);
        }

        if (!string.IsNullOrWhiteSpace(parameters.Source))
        {
            var normalizedSource = parameters.Source.Trim().ToLowerInvariant();
            query = query.Where(item => item.Source.ToLower() == normalizedSource);
        }

        if (!string.IsNullOrWhiteSpace(parameters.District))
        {
            var normalizedDistrict = parameters.District.Trim().ToLowerInvariant();
            query = query.Where(item => item.District.ToLower().Contains(normalizedDistrict));
        }

        if (parameters.From.HasValue)
        {
            query = query.Where(item => item.CreatedAt >= parameters.From.Value);
        }

        if (parameters.To.HasValue)
        {
            query = query.Where(item => item.CreatedAt <= parameters.To.Value);
        }

        return query;
    }

    public static IQueryable<IncidentRecord> ApplySort(this IQueryable<IncidentRecord> query, string? sort)
    {
        return sort?.Trim().ToLowerInvariant() switch
        {
            "created_asc" => query.OrderBy(item => item.CreatedAt),
            "updated_desc" => query.OrderByDescending(item => item.UpdatedAt),
            "updated_asc" => query.OrderBy(item => item.UpdatedAt),
            _ => query.OrderByDescending(item => item.CreatedAt)
        };
    }
}
