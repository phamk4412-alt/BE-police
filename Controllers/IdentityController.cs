using System.Text.Json;
using PoliceBackend.Models;
using PoliceBackend.Services;

namespace PoliceBackend.Controllers;

public static class IdentityController
{
    public static async Task<IResult> CompareFaceAsync(
        FaceCompareRequest request,
        FacePlusPlusService facePlusPlusService,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await facePlusPlusService.CompareAsync(request, cancellationToken);

            return Results.Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return Results.Problem(
                title: "Khong the so khop khuon mat",
                detail: exception.Message,
                statusCode: StatusCodes.Status502BadGateway);
        }
        catch (JsonException)
        {
            return Results.Problem(
                title: "Khong the doc phan hoi Face++",
                detail: "Face++ tra ve phan hoi khong hop le.",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }
}
