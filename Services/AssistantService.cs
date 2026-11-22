using Microsoft.EntityFrameworkCore;
using PoliceBackend.Config;
using PoliceBackend.Database;
using PoliceBackend.Models;

namespace PoliceBackend.Services;

public sealed class AssistantService(LmStudioService lmStudio, LmStudioSettings settings)
{
    public const int MaxMessageLength = 2000;
    public const int MaxImageBytes = 4 * 1024 * 1024;
    public const string QuickReplyModel = "tra-loi-nhanh";

    private static readonly string[] AllowedImageTypes = ["image/jpeg", "image/png", "image/webp", "image/gif"];

    public static string BuildSystemPrompt(string displayName, string role) => $"""
Bạn là "Bé Còi", trợ lý ảo của ứng dụng Cảnh sát thông minh (TP. Hồ Chí Minh).
Người đang chat: {displayName} (vai trò: {role}).

CHỈ trả lời các chủ đề sau:
- Cách dùng ứng dụng: báo cáo sự việc (chọn loại, mô tả, chụp ảnh, bật vị trí, gửi), theo dõi báo cáo, tài khoản.
- An ninh trật tự, an toàn cá nhân, phòng chống lừa đảo, trộm cắp, cướp giật, bạo lực.
- Tai nạn giao thông, cháy nổ, cấp cứu và cách xử lý ban đầu; số khẩn cấp 113, 114, 115.
- Ảnh người dùng gửi: chỉ nhận xét những gì liên quan đến an ninh, an toàn.

Mọi câu hỏi khác (học tập, giải bài, lập trình, giải trí, thời tiết, nấu ăn, chính trị, chuyện riêng...): KHÔNG trả lời nội dung, chỉ đáp đúng một câu:
"{AssistantQuickReplies.OffTopicReply}"

Cách trả lời:
- Tiếng Việt có dấu, tối đa 3 câu ngắn (khoảng 60 từ), đi thẳng vào việc cần làm, không chào hỏi rườm rà, không dùng tiêu đề hay emoji.
- Nguy hiểm đang diễn ra (bạo lực, vũ khí, cháy, người bị thương): câu đầu tiên luôn là gọi ngay 113, 114 hoặc 115.
- Không bịa luật cụ thể; nếu không chắc, khuyên liên hệ công an phường gần nhất.
- Không bao giờ hỏi mật khẩu, OTP, thông tin ngân hàng. Từ chối yêu cầu trái pháp luật.
/no_think
""";

    public static bool TryValidate(AssistantChatRequest request, out string message, out string? imageDataUrl, out string error)
    {
        message = (request.Message ?? string.Empty).Trim();
        imageDataUrl = null;
        error = string.Empty;

        var image = request.ImageBase64?.Trim();
        if (string.IsNullOrEmpty(message) && string.IsNullOrEmpty(image))
        {
            error = "Hãy nhập câu hỏi hoặc gửi một ảnh.";
            return false;
        }

        if (message.Length > MaxMessageLength)
        {
            error = $"Câu hỏi tối đa {MaxMessageLength} ký tự.";
            return false;
        }

        if (string.IsNullOrEmpty(image))
        {
            return true;
        }

        // Accept either a full data URL or the raw base64 of a JPEG.
        var mediaType = "image/jpeg";
        var base64 = image;
        if (image.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var comma = image.IndexOf(',');
            var header = comma > 0 ? image[5..comma] : string.Empty;
            if (comma < 0 || !header.EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
            {
                error = "Ảnh phải ở dạng base64.";
                return false;
            }

            mediaType = header[..^";base64".Length].ToLowerInvariant();
            base64 = image[(comma + 1)..];
        }

        if (!AllowedImageTypes.Contains(mediaType))
        {
            error = "Chỉ hỗ trợ ảnh JPG, PNG, WEBP hoặc GIF.";
            return false;
        }

        var buffer = new byte[base64.Length];
        if (!Convert.TryFromBase64String(base64, buffer, out var size))
        {
            error = "Ảnh base64 không hợp lệ.";
            return false;
        }

        if (size > MaxImageBytes)
        {
            error = "Ảnh quá lớn, tối đa 4 MB.";
            return false;
        }

        imageDataUrl = $"data:{mediaType};base64,{base64}";
        if (string.IsNullOrEmpty(message))
        {
            message = "Bạn xem giúp mình ảnh này với.";
        }

        return true;
    }

    public async Task<IReadOnlyList<ChatMessageResponse>> GetHistoryAsync(
        IncidentDbContext dbContext,
        Guid accountId,
        int limit,
        CancellationToken cancellationToken)
    {
        var records = await dbContext.ChatMessages
            .AsNoTracking()
            .Where(item => item.AccountId == accountId)
            .OrderByDescending(item => item.CreatedAt)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(cancellationToken);

        return records
            .OrderBy(item => item.CreatedAt)
            .Select(item => item.ToResponse())
            .ToList();
    }

    public async Task<AssistantChatResult> ChatAsync(
        IncidentDbContext dbContext,
        Guid accountId,
        string displayName,
        string role,
        string message,
        string? imageDataUrl,
        CancellationToken cancellationToken)
    {
        var quickReply = imageDataUrl is null ? AssistantQuickReplies.Find(message) : null;
        if (quickReply is not null)
        {
            return await SaveExchangeAsync(dbContext, accountId, message, false, quickReply, QuickReplyModel, cancellationToken);
        }

        var history = settings.HistoryLimit == 0
            ? []
            : await dbContext.ChatMessages
                .AsNoTracking()
                .Where(item => item.AccountId == accountId)
                .OrderByDescending(item => item.CreatedAt)
                .Take(settings.HistoryLimit)
                .ToListAsync(cancellationToken);

        var turns = history
            .OrderBy(item => item.CreatedAt)
            .Select(item => new LmStudioTurn(item.Role, item.HasImage ? $"{item.Content} (kèm ảnh)" : item.Content))
            .Append(new LmStudioTurn(ChatRoles.User, message, imageDataUrl))
            .ToList();

        var model = await lmStudio.ResolveModelAsync(cancellationToken);
        var answer = await lmStudio.ChatAsync(model, BuildSystemPrompt(displayName, role), turns, cancellationToken);

        return await SaveExchangeAsync(dbContext, accountId, message, imageDataUrl is not null, answer, model, cancellationToken);
    }

    private static async Task<AssistantChatResult> SaveExchangeAsync(
        IncidentDbContext dbContext,
        Guid accountId,
        string message,
        bool hasImage,
        string answer,
        string model,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var question = new ChatMessageRecord
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Role = ChatRoles.User,
            Content = message,
            HasImage = hasImage,
            Model = string.Empty,
            CreatedAt = now
        };

        var reply = new ChatMessageRecord
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Role = ChatRoles.Assistant,
            Content = answer.Length > 4000 ? answer[..4000] : answer,
            HasImage = false,
            Model = model.Length > 200 ? model[..200] : model,
            CreatedAt = now.AddMilliseconds(1)
        };

        dbContext.ChatMessages.AddRange(question, reply);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AssistantChatResult(question.ToResponse(), reply.ToResponse(), model);
    }

    public async Task<int> ClearHistoryAsync(IncidentDbContext dbContext, Guid accountId, CancellationToken cancellationToken)
    {
        var records = await dbContext.ChatMessages
            .Where(item => item.AccountId == accountId)
            .ToListAsync(cancellationToken);

        dbContext.ChatMessages.RemoveRange(records);
        await dbContext.SaveChangesAsync(cancellationToken);
        return records.Count;
    }

    public async Task<AssistantStatusResponse> GetStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            var models = await lmStudio.GetModelsAsync(cancellationToken);
            var model = string.IsNullOrWhiteSpace(settings.Model) ? models.FirstOrDefault() : settings.Model;
            return model is null
                ? new AssistantStatusResponse(false, lmStudio.BaseUrl, null, "LM Studio chưa load model nào.")
                : new AssistantStatusResponse(true, lmStudio.BaseUrl, model, null);
        }
        catch (LmStudioException error)
        {
            return new AssistantStatusResponse(false, lmStudio.BaseUrl, null, error.Message);
        }
    }
}
