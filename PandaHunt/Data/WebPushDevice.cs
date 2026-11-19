namespace PandaHunt.Data;

/// <summary>
/// One browser (on one device) that a login allowed push notifications in. A login can have several
/// (phone, laptop...). Removed automatically when the push service says the browser no longer wants them.
/// </summary>
public class WebPushDevice
{
    public const int MaxEndpointLength = 600;
    public const int MaxKeyLength = 200;
    public const int MaxLabelLength = 60;

    public int Id { get; set; }

    public required string UserId { get; set; }

    public ApplicationUser? User { get; set; }

    /// <summary>The address of the browser's push service for this device (unique per device).</summary>
    public required string Endpoint { get; set; }

    public required string P256dh { get; set; }

    public required string Auth { get; set; }

    /// <summary>What the player sees in their list of devices, e.g. "Android · Chrome".</summary>
    public required string Label { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
