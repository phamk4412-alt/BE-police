using System.Text.Json;
using PoliceBackend.Models;
using PoliceBackend.Services;

namespace PoliceBackend.Controllers;

public static class IdentityController
{
    public static IResult GetState(
        HttpContext context,
        IdentityVerificationSessionService identityVerificationSessionService)
    {
        return Results.Ok(identityVerificationSessionService.GetState(context));
    }

    public static IResult SaveCccdState(
        HttpContext context,
        UpdateCccdVerificationRequest request,
        IdentityVerificationSessionService identityVerificationSessionService)
    {
        return Results.Ok(identityVerificationSessionService.SaveCccd(context, request));
    }

    public static IResult SaveFaceState(
        HttpContext context,
        UpdateFaceVerificationRequest request,
        IdentityVerificationSessionService identityVerificationSessionService)
    {
        try
        {
            return Results.Ok(identityVerificationSessionService.SaveFace(context, request));
        }
        catch (InvalidOperationException exception)
        {
            return Results.Problem(
                title: "Khong the cap nhat xac thuc khuon mat",
                detail: exception.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    }

    public static IResult ResetState(
        HttpContext context,
        IdentityVerificationSessionService identityVerificationSessionService)
    {
        return Results.Ok(identityVerificationSessionService.Reset(context));
    }

    public static async Task<IResult> CompareFaceAsync(
        HttpContext context,
        FaceCompareRequest request,
        FacePlusPlusService facePlusPlusService,
        IdentityVerificationSessionService identityVerificationSessionService,
        CancellationToken cancellationToken)
    {
        try
        {
            var state = identityVerificationSessionService.GetState(context);
            var compareRequest = request with
            {
                CccdImage = state.CccdImage ?? request.CccdImage
            };

            if (!state.CccdVerified || string.IsNullOrWhiteSpace(compareRequest.CccdImage))
            {
                return Results.Problem(
                    title: "Chua co CCCD de doi chieu",
                    detail: "Can xac thuc va luu CCCD truoc khi quet khuon mat.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var result = await facePlusPlusService.CompareAsync(compareRequest, cancellationToken);

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
