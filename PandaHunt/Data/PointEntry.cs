namespace PandaHunt.Data;

/// <summary>
/// One change of a player's score (positive or negative). A player's score is the sum of their entries,
/// so the admin page keeps a full history of who got what and why.
/// </summary>
public class PointEntry
{
    public const int MaxCommentLength = 200;
    public const int MaxSourceLength = 50;

    public int Id { get; set; }

    public required string PlayerId { get; set; }

    public ApplicationUser? Player { get; set; }

    public int Points { get; set; }

    /// <summary>Optional note shown to the player next to these points (e.g. "Únor – nejrychlejší řešení").</summary>
    public string? Comment { get; set; }

    /// <summary>What gave these points automatically (e.g. <c>RegistrationBonuses.FirstPlayerSource</c>); null = an admin.</summary>
    public string? Source { get; set; }

    /// <summary>Has the player already seen the "you got points" popup for this entry?</summary>
    public bool Seen { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
