using PoliceBackend.Config;
using PoliceBackend.Models;
using PoliceBackend.Utils;

namespace PoliceBackend.Services;

public sealed class IncidentAnalysisService
{
    public IncidentAssessment Analyze(string? title, string? detail, string? requestedLevel)
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
        var reasons = new List<string>();

        if (bestProfile is not null)
        {
            reasons.Add($"phat hien tu khoa: {string.Join(", ", bestProfile.Matches.Take(3))}");
        }
        else
        {
            reasons.Add("mo ta chua co tu khoa ro rang, can xac minh them");
        }

        var urgencyBoosters = new Dictionary<string, int>
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

        foreach (var booster in urgencyBoosters)
        {
            if (normalized.Contains(booster.Key))
            {
                score += booster.Value;
                reasons.Add($"co dau hieu tang muc khan: {booster.Key}");
            }
        }

        var requestedNormalized = NormalizeLevel(requestedLevel);
        score = Math.Max(score, requestedNormalized switch
        {
            "high" => 82,
            "medium" => 58,
            _ => 35
        });

        score = Math.Clamp(score, 15, 99);

        var level = score >= 85 ? "high" : score >= 55 ? "medium" : "low";
        var shouldCallEmergency = score >= 88;
        var category = bestProfile?.Profile.Category ?? "Tinh huong can xac minh";

        var recommendation = shouldCallEmergency
            ? "Uu tien ket noi 113 ngay, dong thoi bo sung vi tri va dau hieu nhan dang."
            : level == "medium"
                ? "Can xac minh them thong tin va theo doi phan hoi tu trung tam."
                : "Luu vao hang doi, uu tien bo sung chi tiet de phan loai chinh xac hon.";

        return new IncidentAssessment(
            Category: category,
            Level: level,
            UrgencyScore: score,
            Reason: string.Join("; ", reasons),
            ShouldCallEmergency: shouldCallEmergency,
            Recommendation: recommendation);
    }

    public string NormalizeLevel(string? level)
    {
        return level?.Trim().ToLowerInvariant() switch
        {
            "high" => "high",
            "medium" => "medium",
            "low" => "low",
            "khancap" => "high",
            "cao" => "high",
            "trungbinh" => "medium",
            "thap" => "low",
            _ => "high"
        };
    }

    public string NormalizeStatus(string? status)
    {
        return status?.Trim().ToLowerInvariant() switch
        {
            "moi tiep nhan" => IncidentStatuses.MoiTiepNhan,
            "da tiep nhan" => IncidentStatuses.DaTiepNhan,
            "dang xac minh" => IncidentStatuses.DangXacMinh,
            "da dieu phoi" => IncidentStatuses.DaDieuPhoi,
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

        if (string.Equals(role, AppRoles.Police, StringComparison.OrdinalIgnoreCase))
        {
            return status is IncidentStatuses.DaTiepNhan
                or IncidentStatuses.DangXacMinh
                or IncidentStatuses.DaDieuPhoi
                or IncidentStatuses.DaXuLy;
        }

        return false;
    }
}
