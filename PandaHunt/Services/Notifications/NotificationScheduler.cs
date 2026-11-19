using Microsoft.EntityFrameworkCore;
using PandaHunt.Data;

namespace PandaHunt.Services.Notifications;

/// <summary>
/// Sends the automatic notifications that depend on the clock: a task opens, a bonus event shows up on /lov,
/// a bonus event starts. Checks every 30 seconds; what was already sent is remembered in the database
/// (<see cref="SentNotification"/>), so each goes out once, even across restarts.
/// Nothing is sent while players can't see the hunt page (before the hunt starts / after it ends).
/// </summary>
public sealed class NotificationScheduler(
    HuntTasks tasks, BonusEvents events, GameSchedule schedule, Notifier notifier,
    IDbContextFactory<AppDbContext> dbFactory, TimeProvider time, ILogger<NotificationScheduler> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    /// <summary>Something that happened longer ago than this is not announced (e.g. after the app was down for a day).</summary>
    private static readonly TimeSpan MaxLateness = TimeSpan.FromHours(12);

    private const string InitializedKey = "scheduler-initialized";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // The very first run only records what is already true (tasks that opened before this feature
        // existed), so deploying doesn't announce the whole past.
        var silent = !await WasInitializedAsync(stoppingToken);

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await CheckAsync(silent, stoppingToken);
                if (silent)
                {
                    await MarkAsync(InitializedKey, stoppingToken);
                    silent = false;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Checking scheduled notifications failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>One pass. With <paramref name="silent"/> nothing is sent, the due notifications are just marked as sent.</summary>
    public async Task CheckAsync(bool silent, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        if (!schedule.IsHuntOpen)
            return;

        var sent = await SentKeysAsync(cancellationToken);
        var huntStart = schedule.HuntStartsAt ?? now;

        foreach (var task in await tasks.GetAllAsync(cancellationToken))
        {
            var key = $"task-open:{task.Id}";
            if (task.OpensAt is not { } opens || opens > now || sent.Contains(key))
                continue;

            await DeliverAsync(key, silent || now - Max(opens, huntStart) > MaxLateness,
                new NotificationMessage("Nový úkol: " + task.Name, "Právě se otevřel nový úkol. Hodně štěstí!", task.Link is { } l && !TaskPages.IsExternal(l) ? l : "/lov", key),
                cancellationToken);
        }

        var all = await events.GetAllAsync(cancellationToken);
        if (BonusEvents.CurrentOf(all, now) is { } current)
        {
            // Visible on /lov (the running one or the next planned one).
            var visibleKey = $"event-visible:{current.Id}";
            if (!sent.Contains(visibleKey))
            {
                var live = current.StateAt(now) == BonusEventState.Live;
                await DeliverAsync(visibleKey, silent || (live && now - Max(current.StartsAt, huntStart) > MaxLateness),
                    live
                        ? LiveMessage(current)
                        : new NotificationMessage("Chystá se bonusová akce", $"„{current.Name}“ – {schedule.FormatRange(current.StartsAt, current.EndsAt)}. Podrobnosti najdeš na stránce Lov.",
                            "/lov", visibleKey),
                    cancellationToken);
                if (live)
                    await MarkAsync($"event-live:{current.Id}", cancellationToken); // that message already said it is running
                sent.Add($"event-live:{current.Id}");
            }

            var liveKey = $"event-live:{current.Id}";
            if (current.StateAt(now) == BonusEventState.Live && !sent.Contains(liveKey))
                await DeliverAsync(liveKey, silent || now - Max(current.StartsAt, huntStart) > MaxLateness, LiveMessage(current), cancellationToken);
        }
    }

    private NotificationMessage LiveMessage(BonusEventView bonus) =>
        new($"Bonusová akce začala: {bonus.Name}", $"Můžeš získat {CzechText.Points(bonus.Points)}. Končí {schedule.Format(bonus.EndsAt)}.",
            BonusEvents.PageOf(bonus.Id), $"event-live:{bonus.Id}");

    // Marks first, then sends: if sending crashes the app, a missed notification is better than a repeated one.
    private async Task DeliverAsync(string key, bool silent, NotificationMessage message, CancellationToken cancellationToken)
    {
        await MarkAsync(key, cancellationToken);
        if (silent)
            return;

        var result = await notifier.NotifyAllPlayersAsync(message, cancellationToken);
        logger.LogInformation("Notification {Key}: {Summary}", key, result.Summary);
    }

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;

    private async Task<HashSet<string>> SentKeysAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.SentNotifications.Select(s => s.Key).ToHashSetAsync(cancellationToken);
    }

    private async Task<bool> WasInitializedAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.SentNotifications.AnyAsync(s => s.Key == InitializedKey, cancellationToken);
    }

    private async Task MarkAsync(string key, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.SentNotifications.AnyAsync(s => s.Key == key, cancellationToken))
        {
            db.SentNotifications.Add(new SentNotification { Key = key, SentAtUtc = time.GetUtcNow().UtcDateTime });
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
