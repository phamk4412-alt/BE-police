using System.Collections.Concurrent;
using PoliceBackend.Config;
using PoliceBackend.Models;
using PoliceBackend.Utils;

namespace PoliceBackend.Services;

public sealed class PolicePresenceService
{
    private readonly ConcurrentDictionary<string, PoliceLocationResponse> _activeLocations =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<PoliceLocationResponse> GetActiveLocations()
    {
        return _activeLocations.Values
            .OrderBy(item => item.DisplayName)
            .ToArray();
    }

    public (PoliceLocationResponse? Location, string? Error) UpdateLocation(
        ActorSnapshot actor,
        PoliceLocationRequest request)
    {
        var username = string.IsNullOrWhiteSpace(actor.Username) || actor.Username == "demo-user"
            ? request.Username?.Trim()
            : actor.Username;
        var displayName = string.IsNullOrWhiteSpace(actor.DisplayName) || actor.DisplayName == "Nguoi dung demo"
            ? request.DisplayName?.Trim()
            : actor.DisplayName;

        if (string.IsNullOrWhiteSpace(username))
        {
            return (null, "Can co dinh danh canh sat de chia se vi tri.");
        }

        if (request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180)
        {
            return (null, "Toa do khong hop le.");
        }

        var status = string.IsNullOrWhiteSpace(request.Status)
            ? "Dang trong ca"
            : request.Status.Trim();

        if (status.Length > 80)
        {
            status = status[..80];
        }

        var shiftId = string.IsNullOrWhiteSpace(request.ShiftId)
            ? null
            : request.ShiftId.Trim();

        if (shiftId?.Length > 120)
        {
            shiftId = shiftId[..120];
        }

        var location = new PoliceLocationResponse(
            username,
            string.IsNullOrWhiteSpace(displayName) ? username : displayName,
            AppRoles.Police,
            request.Latitude,
            request.Longitude,
            GeoLocationUtils.ResolveDistrict(request.Latitude, request.Longitude),
            shiftId,
            status,
            DateTimeOffset.UtcNow);

        _activeLocations.AddOrUpdate(username, location, (_, _) => location);

        return (location, null);
    }

    public PoliceLocationResponse? RemoveLocation(ActorSnapshot actor)
    {
        return _activeLocations.TryRemove(actor.Username, out var removed)
            ? removed
            : null;
    }
}
