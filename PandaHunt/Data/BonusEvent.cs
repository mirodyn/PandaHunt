namespace PandaHunt.Data;

/// <summary>
/// A bonus event: between <see cref="StartsAt"/> and <see cref="EndsAt"/> every player who comes to the place
/// (within <see cref="RadiusMeters"/> of the pin) can claim <see cref="Points"/> once, on the event's page
/// (/lov/akce/{Id}). Events never overlap, so /lov always shows at most one running or planned event.
/// </summary>
public class BonusEvent
{
    public const int MaxNameLength = 60;
    public const int MaxDescriptionLength = 2000;
    public const int MinRadiusMeters = 10;
    public const int MaxRadiusMeters = 5000;
    public const int DefaultRadiusMeters = 100;

    public int Id { get; set; }

    public required string Name { get; set; }

    /// <summary>Shown to players on the event page. Paragraphs are separated by a blank line.</summary>
    public string? Description { get; set; }

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public int RadiusMeters { get; set; } = DefaultRadiusMeters;

    public int Points { get; set; } = 1;

    /// <summary>Show the place on a map to players. Off = they have to find it from the description.</summary>
    public bool ShowPlace { get; set; }
}
