using Microsoft.Extensions.Caching.Memory;
using PoliceBackend.Models;

namespace PoliceBackend.Services;

public sealed class IdentityVerificationSessionService
{
    private const string SessionCookieName = "PoliceSmartHub.Identity";
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(12);

    private readonly IMemoryCache _memoryCache;
    private readonly IConfiguration _configuration;

    public IdentityVerificationSessionService(IMemoryCache memoryCache, IConfiguration configuration)
    {
        _memoryCache = memoryCache;
        _configuration = configuration;
    }

    public IdentityVerificationStateResponse GetState(HttpContext context)
    {
        var sessionId = EnsureSessionId(context);
        return _memoryCache.GetOrCreate(sessionId, CreateCacheEntry) ?? CreateDefaultState();
    }

    public IdentityVerificationStateResponse SaveCccd(
        HttpContext context,
        UpdateCccdVerificationRequest request)
    {
        var currentState = GetState(context);
        var nextState = currentState with
        {
            CccdImage = request.CccdSkipped ? null : request.CccdImage,
            CccdSkipped = request.CccdSkipped,
            CccdVerified = request.CccdVerified,
            FaceImage = null,
            FaceScanned = false,
            FaceSkipped = false,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        SaveState(context, nextState);
        return nextState;
    }

    public IdentityVerificationStateResponse SaveFace(
        HttpContext context,
        UpdateFaceVerificationRequest request)
    {
        var currentState = GetState(context);
        if (!currentState.CccdVerified)
        {
            throw new InvalidOperationException("Can xac thuc CCCD truoc khi quet khuon mat.");
        }

        var nextState = currentState with
        {
            FaceImage = request.FaceSkipped ? null : request.FaceImage,
            FaceScanned = request.FaceScanned,
            FaceSkipped = request.FaceSkipped,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        SaveState(context, nextState);
        return nextState;
    }

    public IdentityVerificationStateResponse Reset(HttpContext context)
    {
        var nextState = CreateDefaultState();
        SaveState(context, nextState);
        return nextState;
    }

    private void SaveState(HttpContext context, IdentityVerificationStateResponse state)
    {
        var sessionId = EnsureSessionId(context);
        _memoryCache.Set(sessionId, state, CreateCacheEntryOptions());
    }

    private string EnsureSessionId(HttpContext context)
    {
        if (context.Request.Cookies.TryGetValue(SessionCookieName, out var existingSessionId) &&
            !string.IsNullOrWhiteSpace(existingSessionId))
        {
            AppendCookie(context, existingSessionId);
            return existingSessionId;
        }

        var sessionId = Guid.NewGuid().ToString("N");
        AppendCookie(context, sessionId);
        return sessionId;
    }

    private void AppendCookie(HttpContext context, string sessionId)
    {
        var useCrossSiteCookies = _configuration.GetValue("POLICE_CROSS_SITE_COOKIES", true);

        context.Response.Cookies.Append(SessionCookieName, sessionId, new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = useCrossSiteCookies ? SameSiteMode.None : SameSiteMode.Lax,
            Secure = useCrossSiteCookies,
            Expires = DateTimeOffset.UtcNow.Add(SessionLifetime)
        });
    }

    private IdentityVerificationStateResponse? CreateCacheEntry(ICacheEntry entry)
    {
        entry.SetOptions(CreateCacheEntryOptions());
        return CreateDefaultState();
    }

    private static MemoryCacheEntryOptions CreateCacheEntryOptions()
    {
        return new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = SessionLifetime,
            SlidingExpiration = SessionLifetime
        };
    }

    private static IdentityVerificationStateResponse CreateDefaultState()
    {
        return new IdentityVerificationStateResponse(
            false,
            false,
            false,
            false,
            null,
            null,
            DateTimeOffset.UtcNow);
    }
}
