using Microsoft.EntityFrameworkCore;
using PoliceBackend.Config;
using PoliceBackend.Database;
using PoliceBackend.Models;

namespace PoliceBackend.Services;

public sealed class AssistantService(LmStudioService lmStudio, LmStudioSettings settings)
{
    public const int MaxMessageLength = 2000;
    public const int MaxImageBytes = 4 * 1024 * 1024;

    private static readonly string[] AllowedImageTypes = ["image/jpeg", "image/png", "image/webp", "image/gif"];

    public static string BuildSystemPrompt(string displayName, string role) => $"""
Bạn là "Bé Còi", trợ lý ảo thân thiện của ứng dụng Cảnh sát thông minh (TP. Hồ Chí Minh).
Luôn trả lời bằng tiếng Việt có dấu, ngắn gọn (tối đa khoảng 120 từ), giọng văn gần gũi, lịch sự.
Người đang chat: {displayName} (vai trò trong ứng dụng: {role}).

Việc của bạn:
- Hướng dẫn người dân cách báo cáo sự việc trong ứng dụng: chọn loại sự việc, mô tả ngắn gọn, chụp ảnh hiện trường, cập nhật vị trí.
- Tư vấn an toàn, phòng chống lừa đảo, trộm cắp, xử lý tai nạn giao thông và các tình huống thường gặp.
- Nếu người dùng gửi ảnh, mô tả những gì thấy được liên quan đến an ninh, an toàn và gợi ý bước tiếp theo.

Quy tắc:
- Nếu có nguy hiểm đang diễn ra (bạo lực, vũ khí, cháy nổ, người bị thương): bảo người dùng gọi ngay 113 (công an), 114 (cứu hỏa), 115 (cấp cứu) trước mọi lời khuyên khác.
- Không bịa thông tin pháp luật cụ thể; nếu không chắc, khuyên liên hệ cơ quan công an gần nhất.
- Không bao giờ hỏi mật khẩu, mã OTP hay thông tin ngân hàng.
- Bạn không tự tạo báo cáo hay gọi cảnh sát thay người dùng được; hãy hướng dẫn họ bấm nút tương ứng trong ứng dụng.
- Từ chối lịch sự các yêu cầu trái pháp luật.
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
            error = "Hay nhap cau hoi hoac gui mot anh.";
            return false;
        }

        if (message.Length > MaxMessageLength)
        {
            error = $"Cau hoi toi da {MaxMessageLength} ky tu.";
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
                error = "Anh phai o dang base64.";
                return false;
            }

            mediaType = header[..^";base64".Length].ToLowerInvariant();
            base64 = image[(comma + 1)..];
        }

        if (!AllowedImageTypes.Contains(mediaType))
        {
            error = "Chi ho tro anh JPG, PNG, WEBP hoac GIF.";
            return false;
        }

        var buffer = new byte[base64.Length];
        if (!Convert.TryFromBase64String(base64, buffer, out var size))
        {
            error = "Anh base64 khong hop le.";
            return false;
        }

        if (size > MaxImageBytes)
        {
            error = "Anh qua lon, toi da 4 MB.";
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

        var now = DateTimeOffset.UtcNow;
        var question = new ChatMessageRecord
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Role = ChatRoles.User,
            Content = message,
            HasImage = imageDataUrl is not null,
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
                ? new AssistantStatusResponse(false, lmStudio.BaseUrl, null, "LM Studio chua load model nao.")
                : new AssistantStatusResponse(true, lmStudio.BaseUrl, model, null);
        }
        catch (LmStudioException error)
        {
            return new AssistantStatusResponse(false, lmStudio.BaseUrl, null, error.Message);
        }
    }
}
