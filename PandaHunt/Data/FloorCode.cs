namespace PandaHunt.Data;

/// <summary>
/// "Body na zemi": a QR code printed out and stuck somewhere in town. A logged-in player who scans it while the
/// hunt runs gets <see cref="Points"/>, once per player. The QR holds the address /q/{Token}.
/// </summary>
public class FloorCode
{
    public const int MaxNameLength = 60;
    public const int TokenLength = 16;

    public int Id { get; set; }

    /// <summary>Random and unguessable (96 bits, URL-safe base64); the only thing in the QR address.</summary>
    public required string Token { get; set; }

    /// <summary>For the admin only (players never see it), e.g. where the code is stuck.</summary>
    public required string Name { get; set; }

    public int Points { get; set; }

    /// <summary>Switched off = scanning it gives nothing (e.g. the sticker got torn off or someone posted it online).</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>A player scanned a floor code and got its points.</summary>
public class FloorCodeClaim
{
    public int CodeId { get; set; }

    public FloorCode? Code { get; set; }

    public required string PlayerId { get; set; }

    public ApplicationUser? Player { get; set; }

    public DateTime ClaimedAtUtc { get; set; }

    /// <summary>Points given for it (the code's points may be changed later).</summary>
    public int Points { get; set; }
}
