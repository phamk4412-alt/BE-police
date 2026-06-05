using PoliceBackend.Config;
using PoliceBackend.Models;
using PoliceBackend.Utils;

namespace PoliceBackend.Services;

public sealed class IncidentAnalysisService
{
    public IncidentAssessment Analyze(string? title, string? detail)
    {
        var combined = $"{title} {detail}".Trim();
        var normalized = TextNormalizationUtils.RemoveDiacritics(combined).ToLowerInvariant();

        var profiles = new[]
        {
            new IncidentProfile("Bao luc / vu khi", "Nguy co bao luc", 96, "giet nguoi", "sat hai", "dam chet", "thu tieu", "co vu khi", "dao", "sung", "hanh hung", "bi tan cong", "chem", "cuop"),
            new IncidentProfile("Tai nan / cap cuu", "Su co hien truong", 88, "tai nan", "va cham", "chay", "no", "bi thuong", "mau nhieu", "ngat"),
            new IncidentProfile("Mat cap tai san", "Mat cap tai san", 70, "mat cap", "trom", "giat", "xe may bi lay", "dot nhap"),
            new IncidentProfile("Lua dao", "Nghi ngo lua dao", 64, "lua dao", "otp", "gia mao", "chuyen khoan", "tai khoan ngan hang"),
            new IncidentProfile("Gay roi cong cong", "Mat trat tu cong cong", 52, "gay roi", "danh nhau", "tap trung dong nguoi", "on ao", "tu tap"),
            new IncidentProfile("Nghi van can xac minh", "Tinh huong can xac minh", 40, "dang nghi", "kha nghi", "la mat", "theo doi")
        };

        var bestProfile = profiles
            .Select(profile => new
            {
                Profile = profile,
                Matches = profile.Keywords.Where(normalized.Contains).ToArray()
            })
            .OrderByDescending(item => item.Matches.Length)
            .ThenByDescending(item => item.Profile.BaseScore)
            .FirstOrDefault(item => item.Matches.Length > 0);

        var score = bestProfile?.Profile.BaseScore ?? 38;
        var emergencySignals = new Dictionary<string, int>
        {
            ["ngay bay gio"] = 8,
            ["dang"] = 6,
            ["vua xay ra"] = 8,
            ["tre em"] = 10,
            ["nguoi gia"] = 10,
            ["co nguoi bi thuong"] = 14,
            ["bat tinh"] = 16,
            ["chay lon"] = 16,
            ["de doa"] = 10
        };

        foreach (var signal in emergencySignals)
        {
            if (normalized.Contains(signal.Key))
            {
                score += signal.Value;
            }
        }

        score = Math.Clamp(score, 15, 99);
        var shouldCallEmergency = score >= 88;
        var category = bestProfile?.Profile.Category ?? "Tinh huong can xac minh";

        return new IncidentAssessment(
            Category: category,
            ShouldCallEmergency: shouldCallEmergency);
    }

    public string NormalizeStatus(string? status)
    {
        return status?.Trim().ToLowerInvariant() switch
        {
            "new" => IncidentStatuses.MoiTiepNhan,
            "processing" => IncidentStatuses.DangXacMinh,
            "done" => IncidentStatuses.DaXuLy,
            "moi tiep nhan" => IncidentStatuses.MoiTiepNhan,
            "da tiep nhan" => IncidentStatuses.DaTiepNhan,
            "dang xac minh" => IncidentStatuses.DangXacMinh,
            "da dieu phoi" => IncidentStatuses.DaDieuPhoi,
            "completed" => IncidentStatuses.DaXuLy,
            "resolved" => IncidentStatuses.DaXuLy,
            "da xu ly" => IncidentStatuses.DaXuLy,
            _ => string.IsNullOrWhiteSpace(status) ? IncidentStatuses.MoiTiepNhan : status.Trim()
        };
    }

    public bool CanUpdateIncidentStatus(string role, string status)
    {
        if (string.Equals(role, AppRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(role, "Anonymous", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(role, AppRoles.Police, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, AppRoles.Support, StringComparison.OrdinalIgnoreCase))
        {
            return status is IncidentStatuses.DaTiepNhan
                or IncidentStatuses.DangXacMinh
                or IncidentStatuses.DaDieuPhoi
                or IncidentStatuses.DaXuLy;
        }

        if (string.Equals(role, AppRoles.Support, StringComparison.OrdinalIgnoreCase))
        {
            return status is IncidentStatuses.MoiTiepNhan
                or IncidentStatuses.DaTiepNhan
                or IncidentStatuses.DangXacMinh
                or IncidentStatuses.DaXuLy;
        }

        return false;
    }
}
