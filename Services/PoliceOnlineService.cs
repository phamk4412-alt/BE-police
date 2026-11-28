using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using PoliceBackend.Config;

namespace PoliceBackend.Services;

public sealed record OnlinePoliceAccount(Guid AccountId, string DisplayName, DateTimeOffset OnlineSince);

public sealed class PoliceOnlineService(TimeProvider clock)
{
    private static readonly TimeSpan Lease = TimeSpan.FromSeconds(75);
    private readonly object _gate = new();
    private readonly Dictionary<(Guid AccountId, string TokenId), Connection> _connections = new();
    private readonly Dictionary<(Guid AccountId, string TokenId), DateTimeOffset> _ended = new();

    public void Start(Guid accountId, string tokenId, string name, DateTimeOffset expiresAt) =>
        Touch(new Session((accountId, tokenId), expiresAt), name);

    public bool Heartbeat(HttpContext context)
    {
        var session = GetSession(context, clock.GetUtcNow());
        return session is not null && Touch(session, context.User.Identity?.Name ?? "");
    }

    private bool Touch(Session session, string name)
    {
        lock (_gate)
        {
            var now = clock.GetUtcNow();
            if (session.ExpiresAt <= now)
                return false;
            Prune(now);
            if (_ended.ContainsKey(session.Key))
                return false;

            var since = _connections.TryGetValue(session.Key, out var previous) ? previous.Since : now;
            var until = session.ExpiresAt < now + Lease ? session.ExpiresAt : now + Lease;
            _connections[session.Key] = new Connection(name, since, until);
            return true;
        }
    }

    public void End(HttpContext context)
    {
        var now = clock.GetUtcNow();
        if (GetSession(context, now) is not { } session)
            return;

        lock (_gate)
        {
            Prune(now);
            _connections.Remove(session.Key);
            // An already in-flight heartbeat must not restore the ended session.
            _ended[session.Key] = session.ExpiresAt;
        }
    }

    public IReadOnlyList<OnlinePoliceAccount> GetOnline()
    {
        lock (_gate)
        {
            Prune(clock.GetUtcNow());
            return _connections
                .GroupBy(entry => entry.Key.AccountId)
                .Select(group => new OnlinePoliceAccount(
                    group.Key,
                    group.MaxBy(entry => entry.Value.Until).Value.Name,
                    group.Min(entry => entry.Value.Since)))
                .OrderBy(account => account.DisplayName)
                .ThenBy(account => account.AccountId)
                .ToArray();
        }
    }

    private void Prune(DateTimeOffset now)
    {
        foreach (var (key, connection) in _connections)
            if (connection.Until <= now) _connections.Remove(key);

        foreach (var (key, expiresAt) in _ended)
            if (expiresAt <= now) _ended.Remove(key);
    }

    private static Session? GetSession(HttpContext context, DateTimeOffset now)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated != true || !user.IsInRole(AppRoles.Police)
            || !Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId)
            || !long.TryParse(user.FindFirstValue("exp"), NumberStyles.None, CultureInfo.InvariantCulture, out var expiry)
            || expiry <= now.ToUnixTimeSeconds() || expiry > DateTimeOffset.MaxValue.ToUnixTimeSeconds())
            return null;

        var tokenId = user.FindFirstValue("jti");
        if (string.IsNullOrWhiteSpace(tokenId))
        {
            // Previously issued JWTs have no jti. Fingerprint the validated bearer token for their remaining lifetime.
            var authorization = context.Request.Headers.Authorization.ToString();
            if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return null;
            tokenId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(authorization[7..].Trim())));
        }

        return new Session((accountId, tokenId), DateTimeOffset.FromUnixTimeSeconds(expiry));
    }

    private sealed record Session((Guid AccountId, string TokenId) Key, DateTimeOffset ExpiresAt);
    private sealed record Connection(string Name, DateTimeOffset Since, DateTimeOffset Until);
}
