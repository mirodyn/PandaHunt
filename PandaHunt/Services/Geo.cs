using System.Globalization;
using System.Text.RegularExpressions;

namespace PandaHunt.Services;

/// <summary>GPS coordinates: distances, and reading/writing them as text.</summary>
public static partial class Geo
{
    private const double EarthRadiusMeters = 6_371_008.8;

    /// <summary>Straight-line distance over the Earth's surface (haversine), in metres.</summary>
    public static double DistanceMeters(double lat1, double lng1, double lat2, double lng2)
    {
        static double Rad(double degrees) => degrees * Math.PI / 180;

        var dLat = Rad(lat2 - lat1);
        var dLng = Rad(lng2 - lng1);
        var a = Math.Pow(Math.Sin(dLat / 2), 2)
              + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Pow(Math.Sin(dLng / 2), 2);
        return 2 * EarthRadiusMeters * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    public static bool IsValid(double latitude, double longitude) =>
        double.IsFinite(latitude) && double.IsFinite(longitude)
        && latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;

    /// <summary>"50.087506, 14.421254" – the way Google Maps writes them (latitude first).</summary>
    public static string Format(double latitude, double longitude) =>
        string.Create(CultureInfo.InvariantCulture, $"{latitude:0.000000}, {longitude:0.000000}");

    /// <summary>
    /// Reads coordinates copied from a map app: "50.0875, 14.4214" (Google Maps), "50.0875061N, 14.4212536E"
    /// (Mapy.com), "N 50.0875 E 14.4214", or with Czech decimal commas "50,0875 14,4214".
    /// </summary>
    public static bool TryParse(string? text, out double latitude, out double longitude)
    {
        latitude = longitude = 0;
        var matches = CoordinateRegex().Matches((text ?? "").ToUpperInvariant());
        if (matches.Count != 2)
            return false;

        var values = new double[2];
        var letters = new char?[2];
        for (var i = 0; i < 2; i++)
        {
            var match = matches[i];
            if (!double.TryParse(match.Groups["value"].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
                return false;
            var letter = match.Groups["before"].Success ? match.Groups["before"].Value : match.Groups["after"].Success ? match.Groups["after"].Value : null;
            letters[i] = letter?[0];
            if (letter is "S" or "W")
                values[i] = -Math.Abs(values[i]);
        }

        // "14.42E, 50.08N": longitude first.
        var swapped = letters[0] is 'E' or 'W' || letters[1] is 'N' or 'S';
        (latitude, longitude) = swapped ? (values[1], values[0]) : (values[0], values[1]);
        return IsValid(latitude, longitude);
    }

    [GeneratedRegex(@"(?<before>[NSEW])?\s*(?<value>[+-]?\d+(?:[.,]\d+)?)\s*°?\s*(?<after>[NSEW](?![A-Z]))?")]
    private static partial Regex CoordinateRegex();
}
