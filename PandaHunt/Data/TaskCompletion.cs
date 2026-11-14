namespace PandaHunt.Data;

/// <summary>A player has finished a task.</summary>
public class TaskCompletion
{
    public int TaskId { get; set; }

    public HuntTask? Task { get; set; }

    public required string PlayerId { get; set; }

    public ApplicationUser? Player { get; set; }

    public DateTime CompletedAtUtc { get; set; }
}
