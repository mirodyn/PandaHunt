namespace PandaHunt.Data;

/// <summary>
/// Game-wide settings edited on the admin page (/admin). Exactly one row (Id = 1).
/// Add new phases/switches here as properties (+ a migration) and expose them on the admin page.
/// </summary>
public class GameSettings
{
    public const int SingletonId = 1;
    public const int MaxCodeLength = 8;
    public const int MaxHuntIntroLength = 4000;
    public const int MaxBonusPoints = 1000;

    public int Id { get; set; } = SingletonId;

    /// <summary>Phase 0: visitors must enter <see cref="Phase0Code"/> before they can register.</summary>
    public bool Phase0Enabled { get; set; }

    /// <summary>Stored normalized (no whitespace, upper case). The code page shows one box per character.</summary>
    public string Phase0Code { get; set; } = "";

    /// <summary>Points for registering while phase 0 is on (i.e. for finding the code). 0 = no bonus.</summary>
    public int Phase0BonusPoints { get; set; } = 1;

    /// <summary>Points for the very first player to register. 0 = no bonus.</summary>
    public int FirstPlayerBonusPoints { get; set; } = 1;

    /// <summary>Points for feeding the panda 5 bamboos on the home page (once per player). 0 = no points.</summary>
    public int PandaFeedingBonusPoints { get; set; } = 1;

    /// <summary>Points taken (once per player) for clicking the panda through her whole annoyed chain. 0 = none.</summary>
    public int PandaPesteringPenaltyPoints { get; set; } = 1;

    // Date/time switches. null = no limit, except HuntStartsAt where null = hunt not scheduled yet.
    public DateTimeOffset? RegistrationOpensAt { get; set; }
    public DateTimeOffset? RegistrationClosesAt { get; set; }
    public DateTimeOffset? HuntStartsAt { get; set; }
    public DateTimeOffset? HuntEndsAt { get; set; }

    /// <summary>Intro text on the hunt page (/lov). Paragraphs are separated by a blank line; empty = sample text.</summary>
    public string? HuntIntro { get; set; }

    public const string SampleHuntIntro =
        """
        Vítej na lovu! Každý měsíc na tebe čeká jeden nový úkol. Za splněné úkoly sbíráš body a postupně se blížíš k pandě.

        Křížek u úkolu znamená, že ho ještě nemáš splněný. Fajfka, že je hotovo.

        Tohle je zatím jen ukázkový text – změníš ho v administraci.
        """;

    public GameSettings Clone() => (GameSettings)MemberwiseClone();

    /// <summary>Removes whitespace and upper-cases, so " ab 12 " and "AB12" are the same code.</summary>
    public static string NormalizeCode(string? code) =>
        string.Concat((code ?? "").Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();
}
