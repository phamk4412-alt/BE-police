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
        if (!string.Equals(actor.Role, AppRoles.Police, StringComparison.OrdinalIgnoreCase))
        {
            return (null, "Chi tai khoan canh sat moi duoc chia se vi tri trong ca.");
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
            actor.Username,
            actor.DisplayName,
            actor.Role,
            request.Latitude,
            request.Longitude,
            GeoLocationUtils.ResolveDistrict(request.Latitude, request.Longitude),
            shiftId,
            status,
            DateTimeOffset.UtcNow);

        _activeLocations.AddOrUpdate(actor.Username, location, (_, _) => location);

        return (location, null);
    }

    public PoliceLocationResponse? RemoveLocation(ActorSnapshot actor)
    {
        return _activeLocations.TryRemove(actor.Username, out var removed)
            ? removed
            : null;
    }
}
