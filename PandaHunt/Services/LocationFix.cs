namespace PandaHunt.Services;

/// <summary>
/// What bonus-map.js locate() answers: the browser's position (with its precision in metres), or why it couldn't
/// get one ("denied", "timeout", "unavailable", "unsupported").
/// </summary>
public sealed record LocationFix(double? Latitude, double? Longitude, double? Accuracy, string? Error)
{
    /// <summary>Czech explanation of <see cref="Error"/> for the player.</summary>
    public static string Explain(string? error) => error switch
    {
        "denied" => "Prohlížeč nemá povolený přístup k poloze. Povol ho pro tuhle stránku (obvykle přes ikonu vedle adresy) a zkus to znovu.",
        "timeout" => "Zjišťování polohy trvalo moc dlouho. Zkus to znovu – venku to jde nejlíp.",
        "unsupported" => "Tenhle prohlížeč polohu zjistit neumí (nebo stránka neběží přes https).",
        _ => "Polohu se nepodařilo zjistit. Zkontroluj, že máš v telefonu zapnutou polohu, a zkus to znovu.",
    };
}
