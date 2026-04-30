using PoliceBackend.Config;
using PoliceBackend.Database;
using PoliceBackend.Services;

namespace PoliceBackend.Controllers;

public static class SupportController
{
    public static async Task<IResult> GetDispatchBoardAsync(
        IncidentDbContext dbContext,
        IncidentService incidentService,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await incidentService.GetDispatchBoardAsync(dbContext, cancellationToken));
    }

    public static async Task<IResult> GetCallIntakeAsync(
        IncidentDbContext dbContext,
        IncidentService incidentService,
        CancellationToken cancellationToken)
    {
        var dispatchBoard = await incidentService.GetDispatchBoardAsync(dbContext, cancellationToken);
        var intakeQueue = dispatchBoard
            .Where(item => item.Status is IncidentStatuses.MoiTiepNhan or IncidentStatuses.DangXacMinh)
            .Take(10)
            .ToArray();

        return Results.Ok(intakeQueue);
    }

    public static async Task<IResult> GetCenterOverviewAsync(
        IncidentDbContext dbContext,
        IncidentService incidentService,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await incidentService.GetSupportCenterOverviewAsync(dbContext, cancellationToken));
    }
}
