namespace PandaHunt.Data;

/// <summary>
/// Remembers that an automatic notification (e.g. "task 5 opened") was already sent, so it goes out only once,
/// even after a restart. See <see cref="Services.NotificationScheduler"/>.
/// </summary>
public class SentNotification
{
    public const int MaxKeyLength = 80;

    public required string Key { get; set; }

    public DateTime SentAtUtc { get; set; }
}
