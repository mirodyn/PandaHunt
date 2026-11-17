namespace PandaHunt.Data;

/// <summary>
/// A player claimed a bonus event's points on the spot. Only the distance from the pin is kept,
/// never the player's coordinates.
/// </summary>
public class BonusEventClaim
{
    public int EventId { get; set; }

    public BonusEvent? Event { get; set; }

    public required string PlayerId { get; set; }

    public Player? Player { get; set; }

    public DateTime ClaimedAtUtc { get; set; }

    /// <summary>How far from the pin the player's phone said they were.</summary>
    public int DistanceMeters { get; set; }

    /// <summary>How precise the phone said that position was (±).</summary>
    public int AccuracyMeters { get; set; }

    /// <summary>Points given for it (the event's points may be changed later).</summary>
    public int Points { get; set; }
}
