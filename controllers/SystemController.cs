using PoliceBackend.Config;
using PoliceBackend.Services;

namespace PoliceBackend.Controllers;

public static class SystemController
{
    public static IResult GetRoot(IConfiguration configuration)
    {
        return Results.Ok(new
        {
            service = "PoliceBackend",
            status = "ok",
            databaseProvider = DatabaseConfiguration.ResolveProvider(configuration),
            modules = new[] { "user", "police", "support", "admin" },
            signalRHub = "/hubs/incidents",
            timestamp = DateTimeOffset.UtcNow
        });
    }

    public static IResult GetHealth(IConfiguration configuration)
    {
        return Results.Ok(new
        {
            status = "ok",
            databaseProvider = DatabaseConfiguration.ResolveProvider(configuration),
            signalRHub = "/hubs/incidents",
            timestamp = DateTimeOffset.UtcNow
        });
    }

    public static IResult GetBoundaryGeoJson(MapDataService mapDataService)
    {
        var filePath = mapDataService.GetBoundaryFilePath();
        return File.Exists(filePath)
            ? Results.File(filePath, "application/geo+json")
            : Results.NotFound(new { message = "Khong tim thay file ban do hcm-boundary.geojson." });
    }

    public static IResult GetGeoJson(string fileName, MapDataService mapDataService)
    {
        return mapDataService.TryResolveGeoJsonFile(fileName, out var filePath)
            ? Results.File(filePath, "application/geo+json")
            : Results.NotFound(new { message = "Khong tim thay file GeoJSON." });
    }
}
