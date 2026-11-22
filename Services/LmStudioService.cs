using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PoliceBackend.Config;

namespace PoliceBackend.Services;

public sealed class LmStudioException(string message, Exception? inner = null) : Exception(message, inner);

public sealed record LmStudioTurn(string Role, string Content, string? ImageDataUrl = null);

public sealed partial class LmStudioService(HttpClient httpClient, LmStudioSettings settings, ILogger<LmStudioService> logger)
{
    private static readonly TimeSpan ModelCacheLifetime = TimeSpan.FromMinutes(2);
    private static string? _cachedModel;
    private static DateTimeOffset _cachedModelAt;

    public string BaseUrl => settings.BaseUrl;

    public async Task<string> ResolveModelAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(settings.Model))
        {
            return settings.Model;
        }

        if (_cachedModel is not null && DateTimeOffset.UtcNow - _cachedModelAt < ModelCacheLifetime)
        {
            return _cachedModel;
        }

        var model = (await GetModelsAsync(cancellationToken)).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new LmStudioException("LM Studio chưa load model nào. Mở LM Studio, load model (vd qwen3-vl-8b) rồi thử lại.");
        }

        _cachedModel = model;
        _cachedModelAt = DateTimeOffset.UtcNow;
        return model;
    }

    public async Task<IReadOnlyList<string>> GetModelsAsync(CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, "models");
        using var response = await SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        try
        {
            // Embedding models cannot chat, so leave them out.
            return (JsonNode.Parse(body)?["data"]?.AsArray() ?? [])
                .Select(item => item?["id"]?.GetValue<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id) && !id.Contains("embed", StringComparison.OrdinalIgnoreCase))
                .Select(id => id!)
                .ToList();
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException)
        {
            throw new LmStudioException("LM Studio trả về danh sách model không đọc được.", error);
        }
    }

    public async Task<string> ChatAsync(
        string model,
        string systemPrompt,
        IReadOnlyList<LmStudioTurn> turns,
        CancellationToken cancellationToken)
    {
        var messages = new JsonArray
        {
            new JsonObject { ["role"] = "system", ["content"] = systemPrompt }
        };

        foreach (var turn in turns)
        {
            if (string.IsNullOrWhiteSpace(turn.ImageDataUrl))
            {
                messages.Add(new JsonObject { ["role"] = turn.Role, ["content"] = turn.Content });
                continue;
            }

            messages.Add(new JsonObject
            {
                ["role"] = turn.Role,
                ["content"] = new JsonArray
                {
                    new JsonObject { ["type"] = "text", ["text"] = turn.Content },
                    new JsonObject
                    {
                        ["type"] = "image_url",
                        ["image_url"] = new JsonObject { ["url"] = turn.ImageDataUrl }
                    }
                }
            });
        }

        var payload = new JsonObject
        {
            ["model"] = model,
            ["messages"] = messages,
            ["temperature"] = settings.Temperature,
            ["max_tokens"] = settings.MaxTokens,
            ["stream"] = false
        };

        using var request = CreateRequest(HttpMethod.Post, "chat/completions");
        request.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");

        using var response = await SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        string? content;
        try
        {
            content = JsonNode.Parse(body)?["choices"]?[0]?["message"]?["content"]?.GetValue<string>();
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException)
        {
            throw new LmStudioException("LM Studio trả về dữ liệu không đọc được.", error);
        }

        var answer = CleanAnswer(content);
        if (string.IsNullOrWhiteSpace(answer))
        {
            throw new LmStudioException("LM Studio không trả lời. Bạn thử hỏi lại ngắn gọn hơn nhé.");
        }

        return answer;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, $"{settings.BaseUrl}/{path}");
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        }

        // ngrok free tunnels show a warning page instead of the API unless this header is sent.
        request.Headers.TryAddWithoutValidation("ngrok-skip-browser-warning", "true");

        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (TaskCanceledException error) when (!cancellationToken.IsCancellationRequested)
        {
            throw new LmStudioException("LM Studio trả lời quá lâu. Thử lại hoặc dùng model nhỏ hơn.", error);
        }
        catch (HttpRequestException error)
        {
            logger.LogWarning(error, "Cannot reach LM Studio at {BaseUrl}.", settings.BaseUrl);
            throw new LmStudioException(
                settings.IsLocalAddress
                    ? $"Không kết nối được LM Studio tại {settings.BaseUrl}. Nếu backend chạy trên server (Render), nó không thấy LM Studio trên máy bạn: hãy mở LM Studio ra internet bằng ngrok và đặt LMSTUDIO_BASE_URL thành link đó. Nếu backend chạy trên máy bạn, hãy bật Developer > Start Server trong LM Studio và kiểm tra đúng port."
                    : $"Không kết nối được LM Studio tại {settings.BaseUrl}. Kiểm tra LM Studio đã Start Server và ngrok còn đang chạy.",
                error);
        }

        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("LM Studio returned {Status}: {Detail}", (int)response.StatusCode, detail);
            response.Dispose();
            if (detail.Contains("ERR_NGROK_3200", StringComparison.Ordinal) || detail.Contains("ERR_NGROK_8012", StringComparison.Ordinal))
            {
                throw new LmStudioException(detail.Contains("ERR_NGROK_3200", StringComparison.Ordinal)
                    ? "Bé Còi đang nghỉ: ngrok trên laptop đã tắt. Mở lại cửa sổ ngrok trên laptop rồi thử lại nhé."
                    : "Bé Còi đang nghỉ: ngrok vẫn chạy nhưng LM Studio trên laptop chưa bật server. Mở LM Studio, bật Model Server ở cổng 1235 rồi thử lại.");
            }

            throw new LmStudioException(
                $"LM Studio báo lỗi {(int)response.StatusCode}. Kiểm tra LM Studio đã Start Server, model đã được load và ngrok (nếu dùng) còn đang chạy.");
        }

        return response;
    }

    // Qwen3 and other reasoning models put their thinking inside <think> tags.
    private static string CleanAnswer(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var withoutThinking = ThinkBlock().Replace(content, string.Empty);
        var openThink = withoutThinking.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
        if (openThink >= 0)
        {
            withoutThinking = withoutThinking[..openThink];
        }

        return withoutThinking.Trim();
    }

    [GeneratedRegex(@"<think>[\s\S]*?</think>", RegexOptions.IgnoreCase)]
    private static partial Regex ThinkBlock();
}
