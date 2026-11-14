namespace PandaHunt.Data;

/// <summary>A post on the landing-page message board.</summary>
public class Message
{
    public const int MaxLength = 500;

    public int Id { get; set; }

    public required string Text { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public required string AuthorId { get; set; }

    public ApplicationUser? Author { get; set; }
}
