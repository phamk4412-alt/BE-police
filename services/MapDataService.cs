namespace PoliceBackend.Services;

public sealed class MapDataService(IWebHostEnvironment environment)
{
    private readonly string _mapsDirectory = Path.Combine(environment.ContentRootPath, "data", "maps");

    public string GetBoundaryFilePath()
    {
        return Path.Combine(_mapsDirectory, "hcm-boundary.geojson");
    }

    public bool TryResolveGeoJsonFile(string fileName, out string fullPath)
    {
        fullPath = string.Empty;

        var safeFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeFileName) ||
            !safeFileName.EndsWith(".geojson", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var candidatePath = Path.GetFullPath(Path.Combine(_mapsDirectory, safeFileName));
        var rootPath = Path.GetFullPath(_mapsDirectory);

        if (!candidatePath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase) || !File.Exists(candidatePath))
        {
            return false;
        }

        fullPath = candidatePath;
        return true;
    }
}
