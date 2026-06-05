using System.Text;
using PoliceBackend.Models;

namespace PoliceBackend.Utils;

public static class IncidentCsvBuilder
{
    public static string Build(IReadOnlyCollection<IncidentResponse> incidents)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Id,Title,Category,District,Status,Source,ReporterName,LastUpdatedBy,CreatedAt,UpdatedAt");

        foreach (var incident in incidents)
        {
            builder.AppendLine(string.Join(",",
                Escape(incident.Id.ToString()),
                Escape(incident.Title),
                Escape(incident.Category),
                Escape(incident.District),
                Escape(incident.Status),
                Escape(incident.Source),
                Escape(incident.ReporterName),
                Escape(incident.LastUpdatedBy),
                Escape(incident.CreatedAt.ToString("O")),
                Escape(incident.UpdatedAt.ToString("O"))));
        }

        return builder.ToString();
    }

    private static string Escape(string? value)
    {
        var safe = value ?? string.Empty;
        return $"\"{safe.Replace("\"", "\"\"")}\"";
    }
}
