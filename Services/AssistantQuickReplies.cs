using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PoliceBackend.Services;

/// <summary>
/// Câu trả lời có sẵn cho các câu hỏi hay gặp, trả về ngay không cần gọi LM Studio.
/// </summary>
public static partial class AssistantQuickReplies
{
    public const string OffTopicReply =
        "Bé Còi chỉ hỗ trợ các việc trong ứng dụng Cảnh sát thông minh: báo cáo sự việc, an ninh, an toàn, phòng chống lừa đảo và số khẩn cấp. Bạn hỏi Bé Còi về những chuyện đó nhé!";

    private const string GreetingReply =
        "Chào bạn, mình là Bé Còi! Mình giúp bạn báo cáo sự việc, tư vấn an toàn, phòng chống lừa đảo và chỉ số khẩn cấp. Bạn cần giúp gì nào?";

    private const string EmergencyReply =
        "Số khẩn cấp: 113 công an, 114 cứu hỏa và cứu hộ, 115 cấp cứu. Gọi miễn phí, kể cả khi điện thoại hết tiền. Nếu đang nguy hiểm, hãy gọi ngay trước khi làm gì khác.";

    private const string ReportReply =
        "Để báo cáo: vào mục Báo cáo sự cố ở trang chủ, chọn loại vụ việc (trộm cắp, tai nạn, bạo lực...), mô tả ngắn gọn, chụp tối đa 3 ảnh nếu an toàn, để app lấy vị trí tự động rồi bấm Gửi báo cáo.";

    private const string ThanksReply = "Không có gì đâu! Cần gì cứ gọi Bé Còi nhé.";

    private static readonly string[] Greetings =
    [
        "xin chao", "chao", "chao ban", "chao be coi", "hi", "hello", "helo", "alo", "hey", "be coi oi", "ban oi", "chao em", "chao anh", "chao chi"
    ];

    private static readonly string[] Thanks = ["cam on", "cam on ban", "cam on nhe", "thanks", "thank you", "ok cam on", "tks"];

    public static string? Find(string message)
    {
        var text = Normalize(message);
        if (text.Length == 0)
        {
            return null;
        }

        if (Greetings.Contains(text))
        {
            return GreetingReply;
        }

        if (Thanks.Contains(text))
        {
            return ThanksReply;
        }

        if (text.Length <= 80 && EmergencyPattern().IsMatch(text))
        {
            return EmergencyReply;
        }

        if (text.Length <= 80 && ReportPattern().IsMatch(text))
        {
            return ReportReply;
        }

        return null;
    }

    /// <summary>Chữ thường, bỏ dấu tiếng Việt, bỏ dấu câu, gộp khoảng trắng.</summary>
    public static string Normalize(string value)
    {
        var decomposed = value.Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }

        return Whitespace().Replace(builder.ToString(), " ").Trim();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    // "số khẩn cấp", "số điện thoại công an/cứu hỏa/cấp cứu", "gọi cảnh sát số mấy"
    [GeneratedRegex(@"\b(so (dien thoai )?(khan cap|cong an|canh sat|cuu hoa|cap cuu|cuu thuong)|(goi|so) (cong an|canh sat|cuu hoa|cap cuu) (so )?(nao|may|gi)|so khan cap)\b")]
    private static partial Regex EmergencyPattern();

    // "cách báo cáo sự việc", "làm sao để báo cáo", "gửi báo cáo thế nào"
    [GeneratedRegex(@"\b(cach|lam sao|lam the nao|huong dan|the nao de|muon) (de )?(bao cao|gui bao cao|to giac|trinh bao)|(bao cao|gui bao cao) (su viec )?(nhu the nao|the nao|ra sao|o dau)\b")]
    private static partial Regex ReportPattern();
}
