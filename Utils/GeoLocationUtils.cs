using System.Globalization;

namespace PoliceBackend.Utils;

public static class GeoLocationUtils
{
    public static bool TryParseLocation(string raw, out double latitude, out double longitude)
    {
        latitude = 0;
        longitude = 0;

        var parts = raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out latitude) ||
            !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out longitude))
        {
            return false;
        }

        return IsWithinCoverage(latitude, longitude);
    }

    public static bool IsWithinCoverage(double latitude, double longitude)
    {
        return latitude is >= 10.3 and <= 11.1 && longitude is >= 106.4 and <= 107.1;
    }

    public static string ResolveDistrict(double latitude, double longitude)
    {
        if (latitude >= 10.76 && latitude <= 10.79 && longitude >= 106.69 && longitude <= 106.71)
        {
            return "Quan 1";
        }

        if (latitude >= 10.77 && longitude >= 106.72)
        {
            return "Thu Duc";
        }

        if (latitude >= 10.79 && longitude <= 106.69)
        {
            return "Binh Thanh";
        }

        if (latitude < 10.76 && longitude <= 106.69)
        {
            return "Quan 3";
        }

        if (latitude < 10.74)
        {
            return "Quan 7";
        }

        return "TP.HCM";
    }

    public static double CalculateDistanceKm(
        double originLatitude,
        double originLongitude,
        double destinationLatitude,
        double destinationLongitude)
    {
        const double earthRadiusKm = 6371;

        var deltaLatitude = DegreesToRadians(destinationLatitude - originLatitude);
        var deltaLongitude = DegreesToRadians(destinationLongitude - originLongitude);
        var a = Math.Pow(Math.Sin(deltaLatitude / 2), 2)
            + Math.Cos(DegreesToRadians(originLatitude))
            * Math.Cos(DegreesToRadians(destinationLatitude))
            * Math.Pow(Math.Sin(deltaLongitude / 2), 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusKm * c;
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180d;
    }
}
