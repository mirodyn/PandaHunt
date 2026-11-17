namespace PandaHunt.Data;

/// <summary>Colours of bonus stars. What each is worth is in <see cref="Services.StarColors"/>; the number is only the stored id.</summary>
public enum StarColor
{
    Bronze = 1,
    Silver = 2,
    Gold = 3,
}

/// <summary>
/// A task's bonus star and the player who holds it right now. Each task has at most one star of each colour.
/// Unlike points, a star can change hands: when another player beats the holder (how depends on the task),
/// the star moves to them. While someone holds it, it counts towards their score.
/// </summary>
public class TaskStar
{
    public int TaskId { get; set; }

    public HuntTask? Task { get; set; }

    public StarColor Color { get; set; }

    public required string PlayerId { get; set; }

    public Player? Player { get; set; }

    /// <summary>When the current holder got it.</summary>
    public DateTime SinceUtc { get; set; }
}
