using MudBlazor;

namespace PandaHunt.Services;

public sealed record TaskType(string Key, string Label, string Icon);

/// <summary>
/// Kinds of tasks, each with its own icon. Apart from <see cref="Unknown"/> these are placeholders until
/// the real types are decided: add, rename or re-icon them here. The key is what's stored in the database,
/// so keep a key once tasks use it (changing the label or icon is always fine).
/// Icons: https://mudblazor.com/features/icons
/// </summary>
public static class TaskTypes
{
    /// <summary>Not decided yet. New tasks (and the 12 monthly ones created at start) use this.</summary>
    public static readonly TaskType Unknown = new("neznamy", "Zatím neznámý", Icons.Material.Filled.QuestionMark);

    public static readonly IReadOnlyList<TaskType> All =
    [
        Unknown,
        new("sifra", "Šifra", Icons.Material.Filled.Key),
        new("teren", "V terénu", Icons.Material.Filled.Hiking),
        new("kviz", "Kvíz", Icons.Material.Filled.Lightbulb),
        new("foto", "Fotoúkol", Icons.Material.Filled.PhotoCamera),
        new("jine", "Jiné", Icons.Material.Filled.Star),
    ];

    public static bool Exists(string? key) => All.Any(t => t.Key == key);

    /// <summary>The type for a key; an unknown key (e.g. a removed type) counts as <see cref="Unknown"/>.</summary>
    public static TaskType Get(string? key) => All.FirstOrDefault(t => t.Key == key) ?? Unknown;
}
