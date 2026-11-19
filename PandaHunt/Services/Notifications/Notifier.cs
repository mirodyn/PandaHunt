using Microsoft.EntityFrameworkCore;
using PandaHunt.Account;
using PandaHunt.Data;

namespace PandaHunt.Services.Notifications;

/// <param name="Recipients">How many logins were addressed.</param>
/// <param name="SentByChannel">Messages that went out, per channel name.</param>
/// <param name="Failed">Messages that could not be delivered.</param>
/// <param name="Unreachable">Logins that have no way of being reached.</param>
public sealed record NotifyResult(int Recipients, IReadOnlyDictionary<string, int> SentByChannel, int Failed, int Unreachable)
{
    public int Sent => SentByChannel.Values.Sum();

    /// <summary>"Odesláno 5 zpráv (e-mail 3, prohlížeč 2), selhalo 1, bez oznámení 2 lidi."</summary>
    public string Summary
    {
        get
        {
            var parts = new List<string>
            {
                Sent == 0
                    ? "Nic se neodeslalo"
                    : $"Odesláno {Sent} {CzechText.Plural(Sent, "zpráva", "zprávy", "zpráv")} ({string.Join(", ", SentByChannel.Where(c => c.Value > 0).Select(c => $"{c.Key} {c.Value}"))})",
            };
            if (Failed > 0)
                parts.Add($"selhalo {Failed}");
            if (Unreachable > 0)
                parts.Add($"bez oznámení {Unreachable} {CzechText.Plural(Unreachable, "člověk", "lidé", "lidí")}");
            return string.Join(", ", parts) + ".";
        }
    }
}

/// <summary>
/// Sends a notification to players on every channel they chose (e-mail, browser, later SMS). Teams share a
/// player but each login has its own settings, so everyone in a team is notified separately.
/// </summary>
public sealed class Notifier(
    IDbContextFactory<AppDbContext> dbFactory, IEnumerable<INotificationChannel> channels, ILogger<Notifier> logger)
{
    private const int Parallelism = 4;

    private readonly IReadOnlyList<INotificationChannel> channels = channels.ToList();

    /// <summary>Notifies the logins of these players. Never throws for a failing channel (see the result).</summary>
    public async Task<NotifyResult> NotifyPlayersAsync(
        IReadOnlyCollection<string> playerIds, NotificationMessage message, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await SendAsync(await LoadAsync(db.Users.Where(u => playerIds.Contains(u.PlayerId)), cancellationToken), message, null, cancellationToken);
    }

    /// <summary>Notifies every login except the admins (the organisers aren't playing).</summary>
    public async Task<NotifyResult> NotifyAllPlayersAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var adminPlayers = Scoreboard.AdminPlayerIds(db);
        return await SendAsync(await LoadAsync(db.Users.Where(u => !adminPlayers.Contains(u.PlayerId)), cancellationToken), message, null, cancellationToken);
    }

    /// <summary>Notifies one login, optionally only through one channel (the "send me a test" buttons).</summary>
    public async Task<NotifyResult> NotifyUserAsync(
        string userId, NotificationMessage message, string? onlyChannel = null, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await SendAsync(await LoadAsync(db.Users.Where(u => u.Id == userId), cancellationToken), message, onlyChannel, cancellationToken);
    }

    /// <summary>Same as <see cref="NotifyPlayersAsync"/>, for callers that must not wait or fail (e.g. while saving something else).</summary>
    public void NotifyPlayersInBackground(IReadOnlyCollection<string> playerIds, NotificationMessage message) =>
        _ = Task.Run(async () =>
        {
            try
            {
                await NotifyPlayersAsync(playerIds, message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Notifying players failed");
            }
        });

    private static async Task<List<Recipient>> LoadAsync(IQueryable<ApplicationUser> users, CancellationToken cancellationToken) =>
        (await users.AsNoTracking().Include(u => u.PushDevices).ToListAsync(cancellationToken))
            .Select(u => new Recipient(u.Id, u.UserName ?? "", u.Email, u.PhoneNumber, u.NotifyByEmail, u.NotifyBySms, u.PushDevices))
            .ToList();

    private async Task<NotifyResult> SendAsync(
        List<Recipient> recipients, NotificationMessage message, string? onlyChannel, CancellationToken cancellationToken)
    {
        var active = channels.Where(c => c.IsAvailable && (onlyChannel is null || c.Name == onlyChannel)).ToList();
        var sentByChannel = channels.ToDictionary(c => c.Name, _ => 0);
        var failed = 0;
        var unreachable = 0;
        var gate = new object();

        await Parallel.ForEachAsync(recipients, new ParallelOptions { MaxDegreeOfParallelism = Parallelism, CancellationToken = cancellationToken },
            async (recipient, token) =>
            {
                var wanted = active.Where(c => c.Wants(recipient)).ToList();
                if (wanted.Count == 0)
                {
                    lock (gate)
                        unreachable++;
                    return;
                }

                foreach (var channel in wanted)
                {
                    ChannelResult result;
                    try
                    {
                        result = await channel.SendAsync(recipient, message, token);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Channel {Channel} failed for user {UserId}", channel.Name, recipient.UserId);
                        result = new ChannelResult(0, 1);
                    }

                    lock (gate)
                    {
                        sentByChannel[channel.Name] += result.Sent;
                        failed += result.Failed;
                    }
                }
            });

        return new NotifyResult(recipients.Count, sentByChannel, failed, unreachable);
    }
}
