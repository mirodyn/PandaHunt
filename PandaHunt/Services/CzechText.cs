namespace PandaHunt.Services;

/// <summary>Small helpers for Czech UI text.</summary>
public static class CzechText
{
    /// <summary>Picks the Czech plural form: 1 bod, 2–4 body, 0/5+ bodů.</summary>
    public static string Plural(int n, string one, string few, string many) =>
        Math.Abs(n) switch
        {
            1 => one,
            >= 2 and <= 4 => few,
            _ => many,
        };

    /// <summary>"1 bod", "3 body", "12 bodů", "-2 body".</summary>
    public static string Points(int n) => $"{n} {Plural(n, "bod", "body", "bodů")}";

    /// <summary>Signed change for the admin history: "+5", "−3".</summary>
    public static string SignedPoints(int n) => n > 0 ? $"+{n}" : n < 0 ? $"−{-(long)n}" : "0";

    /// <summary>"35 m", "1,2 km", "24 km".</summary>
    public static string Distance(int meters) =>
        meters < 1000 ? $"{meters} m"
        : meters < 10_000 ? (meters / 1000.0).ToString("0.# 'km'", Czech)
        : (meters / 1000.0).ToString("N0", Czech) + " km";

    private static readonly System.Globalization.CultureInfo Czech = System.Globalization.CultureInfo.GetCultureInfo("cs-CZ");
}
