namespace PoliceBackend.Models;

public sealed class ChatMessageRecord
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool HasImage { get; set; }
    public string Model { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

public static class ChatRoles
{
    public const string User = "user";
    public const string Assistant = "assistant";
}

public sealed record AssistantChatRequest(string? Message, string? ImageBase64);

public sealed record ChatMessageResponse(
    Guid Id,
    string Role,
    string Content,
    bool HasImage,
    DateTimeOffset CreatedAt);

public sealed record AssistantChatResult(
    ChatMessageResponse Question,
    ChatMessageResponse Answer,
    string Model);

public sealed record AssistantStatusResponse(
    bool Online,
    string BaseUrl,
    string? Model,
    string? Message);

public static class ChatMessageMapping
{
    public static ChatMessageResponse ToResponse(this ChatMessageRecord record) =>
        new(record.Id, record.Role, record.Content, record.HasImage, record.CreatedAt);
}
