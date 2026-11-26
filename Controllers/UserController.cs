using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PoliceBackend.Config;
using PoliceBackend.Database;
using PoliceBackend.Models;
using PoliceBackend.Services;
using PoliceBackend.Services.Realtime;

namespace PoliceBackend.Controllers;

public static class UserController
{
    private static readonly JsonSerializerOptions MetadataJson = new(JsonSerializerDefaults.Web);

    public static async Task<IResult> GetPhotoMetadataAsync([FromForm] IFormFile image, CancellationToken cancellationToken)
    {
        if (!IncidentService.TryValidateImages([image], out var error))
            return Results.BadRequest(new { message = error });

        var bytes = await ReadPhotoAsync(image, cancellationToken);
        var extension = IncidentPhotoMetadata.GetExtension(bytes);
        return extension is null
            ? Results.BadRequest(new { message = "Nội dung ảnh không hợp lệ." })
            : Results.Ok(new { capturedAt = IncidentPhotoMetadata.GetCaptureTime(bytes, extension) });
    }

    public static async Task<IResult> AnalyzeIncidentAsync(
        AnalyzeIncidentRequest request,
        HttpContext context,
        IncidentDbContext dbContext,
        IncidentService incidentService,
        AuditService auditService,
        AuthService authService,
        CancellationToken cancellationToken)
    {
        var assessment = incidentService.AnalyzeAssessment(request.Title, request.Detail);
        var actor = authService.GetActorSnapshot(context.User);

        await auditService.WriteAsync(
            dbContext,
            context,
            action: AuditActions.AnalyzeIncident,
            entityType: AuditEntities.Incident,
            entityId: "preview",
            summary: "Phan tich loai vu viec.",
            detail: $"He thong phan tich yeu cau preview va goi y loai {assessment.Category}.",
            actor: actor,
            cancellationToken: cancellationToken);

        return Results.Ok(assessment.ToResponse());
    }

    public static async Task<IResult> CreateIncidentAsync(
        [FromForm] CreateIncidentRequest request,
        HttpContext context,
        IWebHostEnvironment environment,
        IncidentDbContext dbContext,
        IncidentService incidentService,
        AuditService auditService,
        AuthService authService,
        IHubContext<IncidentHub> hubContext,
        CancellationToken cancellationToken)
    {
        var actor = authService.GetActorSnapshot(context.User);
        var form = await context.Request.ReadFormAsync(cancellationToken);
        var images = form.Files.GetFiles("images").ToArray();
        if (!IncidentService.TryValidateImages(images, out var imageError))
        {
            return Results.BadRequest(new { message = imageError });
        }

        if (!incidentService.TryBuildIncident(request, actor, out var incident, out var assessment, out var error))
        {
            return Results.BadRequest(new { message = error });
        }

        var draft = incident!;
        if (request.SubmissionId is Guid submissionId)
        {
            var digest = SHA256.HashData(Encoding.UTF8.GetBytes($"{actor.Username.ToUpperInvariant()}:{submissionId:D}"));
            draft.Id = new Guid(digest.AsSpan(0, 16));
            var previous = await dbContext.Incidents.AsNoTracking().FirstOrDefaultAsync(
                item => item.Id == draft.Id, cancellationToken);
            if (previous is not null) return ExistingReport(previous, incidentService);
        }

        var address = request.Address?.Trim();
        if (address?.Length > 500 || request.PhotoMetadata?.Length > 4096)
            return Results.BadRequest(new { message = "Thông tin ảnh hoặc địa chỉ quá dài." });

        CapturePhotoInput?[]? supplied = null;
        if (!string.IsNullOrWhiteSpace(request.PhotoMetadata))
        {
            try { supplied = JsonSerializer.Deserialize<CapturePhotoInput?[]>(request.PhotoMetadata, MetadataJson); }
            catch (JsonException) { return Results.BadRequest(new { message = "Thông tin ảnh không hợp lệ." }); }
            if (supplied?.Length != images.Length)
                return Results.BadRequest(new { message = "Thông tin ảnh không khớp với tệp đính kèm." });
        }

        var photos = new List<CapturePhoto>();
        var uploads = new List<(string Name, byte[] Bytes)>();
        for (var index = 0; index < images.Length; index++)
        {
            var bytes = await ReadPhotoAsync(images[index], cancellationToken);
            var extension = IncidentPhotoMetadata.GetExtension(bytes);
            if (extension is null)
                return Results.BadRequest(new { message = $"Nội dung ảnh {index + 1} không hợp lệ." });
            var fileName = $"{Guid.NewGuid():N}{extension}";
            var exif = IncidentPhotoMetadata.GetCaptureTime(bytes, extension);
            var photo = supplied?[index];
            var hasCameraTime = photo?.CapturedAt is DateTimeOffset capturedAt &&
                capturedAt >= draft.CreatedAt.AddDays(-7) && capturedAt <= draft.CreatedAt.AddMinutes(5);
            var hasCoordinates = photo?.Latitude is >= -90 and <= 90 && photo.Longitude is >= -180 and <= 180;
            photos.Add(new CapturePhoto(
                $"/uploads/{fileName}", exif ?? (hasCameraTime ? photo!.CapturedAt!.Value.ToString("O") : null),
                exif is not null ? "exif" : hasCameraTime ? "client_camera" : null,
                hasCoordinates ? photo!.Latitude : null, hasCoordinates ? photo!.Longitude : null,
                hasCoordinates ? (hasCameraTime ? "client_camera" : "client_selection") : null));
            uploads.Add((fileName, bytes));
        }
        draft.ImageUrls = string.Join(",", photos.Select(photo => photo.Url));
        if (photos.Count > 0 || !string.IsNullOrEmpty(address))
            draft.CaptureMetadata = JsonSerializer.Serialize(new { address, photos }, MetadataJson);

        dbContext.Incidents.Add(incident!);
        await auditService.WriteAsync(
            dbContext,
            context,
            action: AuditActions.CreateIncident,
            entityType: AuditEntities.Incident,
            entityId: incident!.Id.ToString(),
            summary: "Tao bao cao moi.",
            detail: $"{actor.DisplayName} tao bao cao {incident.Title} ({incident.Category}).",
            actor: actor,
            saveChanges: false,
            cancellationToken: cancellationToken);

        var uploadRoot = Path.Combine(environment.ContentRootPath, "wwwroot", "uploads");
        try
        {
            if (uploads.Count > 0) Directory.CreateDirectory(uploadRoot);
            foreach (var upload in uploads)
                await File.WriteAllBytesAsync(Path.Combine(uploadRoot, upload.Name), upload.Bytes, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (request.SubmissionId.HasValue &&
            (ex is DbUpdateException || (ex is ArgumentException && dbContext.Database.IsInMemory())))
        {
            DeleteUploads();
            var previous = await dbContext.Incidents.AsNoTracking().FirstOrDefaultAsync(
                item => item.Id == draft.Id, cancellationToken);
            if (previous is not null) return ExistingReport(previous, incidentService);
            throw;
        }
        catch
        {
            DeleteUploads();
            throw;
        }
        await incidentService.BroadcastCreatedAsync(hubContext, incident, cancellationToken);

        var response = new CreateIncidentResult(
            assessment!.ShouldCallEmergency
                ? "Da gui bao cao thanh cong. He thong khuyen nghi lien he 113 neu tinh huong dang dien ra."
                : "Da gui bao cao thanh cong.",
            assessment.ToResponse(),
            incident.ToResponse());

        return Results.Ok(response);

        void DeleteUploads()
        {
            foreach (var upload in uploads)
            {
                try { System.IO.File.Delete(Path.Combine(uploadRoot, upload.Name)); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    private static IResult ExistingReport(IncidentRecord incident, IncidentService service) =>
        Results.Ok(new CreateIncidentResult("Báo cáo đã được tiếp nhận.",
            service.AnalyzeAssessment(incident.Title, incident.Detail).ToResponse(), incident.ToResponse()));

    private static async Task<byte[]> ReadPhotoAsync(IFormFile image, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream((int)image.Length);
        await image.CopyToAsync(stream, cancellationToken);
        return stream.ToArray();
    }

    public static async Task<IResult> GetIncidentByIdAsync(
        Guid id,
        IncidentDbContext dbContext,
        IncidentService incidentService,
        CancellationToken cancellationToken)
    {
        var incident = await incidentService.GetIncidentByIdAsync(dbContext, id, cancellationToken);
        return incident is null
            ? Results.NotFound(new { message = "Khong tim thay vu viec." })
            : Results.Ok(incident);
    }

    public static async Task<IResult> GetReportHistoryAsync(
        HttpContext context,
        IncidentDbContext dbContext,
        IncidentService incidentService,
        AuthService authService,
        int? limit,
        CancellationToken cancellationToken)
    {
        var actor = authService.GetActorSnapshot(context.User);
        var history = await incidentService.GetReportHistoryAsync(
            dbContext,
            actor,
            limit,
            cancellationToken);

        return Results.Ok(history);
    }

    public static async Task<IResult> GetNearbyAlertsAsync(
        string location,
        double? radiusKm,
        int? limit,
        IncidentDbContext dbContext,
        IncidentService incidentService,
        CancellationToken cancellationToken)
    {
        if (!incidentService.TryResolveLocation(location, out var resolvedLocation))
        {
            return Results.BadRequest(new { message = "Toa do khong hop le. Dung dinh dang '10.7769, 106.7009'." });
        }

        var alerts = await incidentService.GetNearbyAlertsAsync(
            dbContext,
            resolvedLocation!.Latitude,
            resolvedLocation.Longitude,
            Math.Clamp(radiusKm ?? 3, 0.5, 30),
            limit,
            cancellationToken);

        return Results.Ok(new
        {
            location = resolvedLocation,
            alerts
        });
    }

    public static IResult ResolveLocationAsync(
        LocationResolutionRequest request,
        IncidentService incidentService)
    {
        return incidentService.TryResolveLocation(request.Location, out var resolution)
            ? Results.Ok(resolution)
            : Results.BadRequest(new { message = "Toa do khong hop le. Dung dinh dang '10.7769, 106.7009'." });
    }
}
