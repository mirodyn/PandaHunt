using Microsoft.EntityFrameworkCore;
using PandaHunt.Data;

namespace PandaHunt.Services;

public sealed record MessageView(int Id, string Author, string Text, DateTime CreatedAtUtc);

/// <summary>
/// Reads and writes message-board posts and notifies every connected circuit when a new one arrives.
/// Notifications are in-process, which is fine for a single server instance.
/// </summary>
public sealed class MessageBoard(IDbContextFactory<AppDbContext> dbFactory, TimeProvider time)
{
    public const int ShownCount = 200;

    /// <summary>Raised after a message is posted.</summary>
    public event Action? Changed;

    public async Task<IReadOnlyList<MessageView>> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Messages
            .AsNoTracking()
            .OrderByDescending(m => m.Id)
            .Take(ShownCount)
            .Select(m => new MessageView(m.Id, m.Author!.UserName!, m.Text, m.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task PostAsync(string authorId, string text, CancellationToken cancellationToken = default)
    {
        text = text.Trim();
        if (text.Length == 0)
            return;
        if (text.Length > Message.MaxLength)
            text = text[..Message.MaxLength];

        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            db.Messages.Add(new Message
            {
                AuthorId = authorId,
                Text = text,
                CreatedAtUtc = time.GetUtcNow().UtcDateTime,
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        Changed?.Invoke();
    }

    /// <summary>Tells open boards to reload, e.g. after an admin renamed or deleted a player.</summary>
    public void NotifyChanged() => Changed?.Invoke();
}
