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
            throw new LmStudioException("LM Studio chua load model nao. Mo LM Studio, load model (vd qwen3-vl-8b) roi thu lai.");
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
            throw new LmStudioException("LM Studio tra ve danh sach model khong doc duoc.", error);
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
            throw new LmStudioException("LM Studio tra ve du lieu khong doc duoc.", error);
        }

        var answer = CleanAnswer(content);
        if (string.IsNullOrWhiteSpace(answer))
        {
            throw new LmStudioException("LM Studio khong tra loi. Thu hoi lai ngan gon hon nhe.");
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
            throw new LmStudioException("LM Studio tra loi qua lau. Thu lai hoac dung model nho hon.", error);
        }
        catch (HttpRequestException error)
        {
            logger.LogWarning(error, "Cannot reach LM Studio at {BaseUrl}.", settings.BaseUrl);
            throw new LmStudioException(
                $"Khong ket noi duoc LM Studio tai {settings.BaseUrl}. Hay bat Developer > Start Server trong LM Studio.",
                error);
        }

        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("LM Studio returned {Status}: {Detail}", (int)response.StatusCode, detail);
            response.Dispose();
            throw new LmStudioException($"LM Studio bao loi {(int)response.StatusCode}. Kiem tra model da duoc load chua.");
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
