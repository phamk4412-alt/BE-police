using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using PoliceBackend.Models;
using PoliceBackend.Services;

namespace PoliceBackend.Services.Realtime;

public sealed class IncidentHub(
    AuthService authService,
    PolicePresenceService policePresenceService) : Hub
{
    public IReadOnlyCollection<PoliceLocationResponse> GetPoliceLocations()
    {
        return policePresenceService.GetActiveLocations();
    }

    public async Task UpdatePoliceLocation(PoliceLocationRequest request)
    {
        var actor = authService.GetActorSnapshot(Context.User ?? new ClaimsPrincipal());
        var (location, error) = policePresenceService.UpdateLocation(actor, request);

        if (location is null)
        {
            throw new HubException(error ?? "Khong the cap nhat vi tri.");
        }

        await Clients.All.SendAsync("PoliceLocationUpdated", location);
        await Clients.All.SendAsync("PoliceLocationsSnapshot", policePresenceService.GetActiveLocations());
    }

    public async Task EndPoliceShift()
    {
        var actor = authService.GetActorSnapshot(Context.User ?? new ClaimsPrincipal());
        var removed = policePresenceService.RemoveLocation(actor);

        if (removed is not null)
        {
            await Clients.All.SendAsync("PoliceLocationRemoved", removed);
            await Clients.All.SendAsync("PoliceLocationsSnapshot", policePresenceService.GetActiveLocations());
        }
    }
}
