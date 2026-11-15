using Microsoft.EntityFrameworkCore;
using PandaHunt.Account;
using PandaHunt.Data;

namespace PandaHunt.Services;

/// <summary>
/// A row of the leaderboard: <see cref="Points"/> from the point log plus the stars held right now.
/// Players are ranked by <see cref="Total"/>; tied players share a rank (1, 1, 3, ...).
/// </summary>
public sealed record Standing(int Rank, string PlayerId, string PlayerName, int Points, StarCounts Stars)
{
    public int Total => Points + Stars.Points;
}

public sealed record PointEntryView(int Id, string PlayerId, string PlayerName, int Points, string? Comment, DateTime CreatedAtUtc);

/// <summary>
/// Player scores. A score is the sum of the player's <see cref="PointEntry"/> rows plus the value of the
/// stars they hold right now (<see cref="TaskStars"/>). Admins (organisers) are not players, so they are left
/// off the leaderboard. Connected pages are notified on every change (in-process, fine for a single server instance).
/// </summary>
public sealed class Scoreboard(IDbContextFactory<AppDbContext> dbFactory, TimeProvider time)
{
    public const int MaxPointsPerEntry = 10_000;
    public const int HistoryCount = 500;

    private readonly SemaphoreSlim onceGate = new(1, 1);

    /// <summary>Raised after points or stars change or the list of players changes (register, rename, delete, role).</summary>
    public event Action? Changed;

    /// <summary>Ids of the admins, who aren't players (for use inside a query).</summary>
    internal static IQueryable<string> AdminIds(AppDbContext db) =>
        db.UserRoles
            .Where(ur => db.Roles.Any(r => r.Id == ur.RoleId && r.Name == AppRoles.Admin))
            .Select(ur => ur.UserId);

    public async Task<IReadOnlyList<Standing>> GetStandingsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var adminIds = AdminIds(db);

        var stars = (await db.TaskStars
                .AsNoTracking()
                .Select(s => new { s.PlayerId, s.Color })
                .ToListAsync(cancellationToken))
            .GroupBy(s => s.PlayerId)
            .ToDictionary(g => g.Key, g => StarCounts.From(g.Select(s => s.Color)));

        var players = await db.Users
            .AsNoTracking()
            .Where(u => !adminIds.Contains(u.Id))
            .Select(u => new
            {
                u.Id,
                u.UserName,
                Points = db.PointEntries.Where(p => p.PlayerId == u.Id).Sum(p => p.Points),
            })
            .ToListAsync(cancellationToken);

        var ordered = players
            .Select(p => new Standing(0, p.Id, p.UserName ?? "", p.Points, stars.GetValueOrDefault(p.Id, StarCounts.None)))
            .OrderByDescending(p => p.Total)
            .ThenBy(p => p.PlayerName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var standings = new List<Standing>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var player = ordered[i];
            var rank = i > 0 && ordered[i - 1].Total == player.Total ? standings[i - 1].Rank : i + 1;
            standings.Add(player with { Rank = rank });
        }
        return standings;
    }

    /// <summary>The newest point changes first, for the admin history.</summary>
    public async Task<IReadOnlyList<PointEntryView>> GetHistoryAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.PointEntries
            .AsNoTracking()
            .OrderByDescending(e => e.Id)
            .Take(HistoryCount)
            .Select(e => new PointEntryView(e.Id, e.PlayerId, e.Player!.UserName!, e.Points, e.Comment, e.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    /// <summary>All of one player's point changes, newest first (their total is the sum).</summary>
    public async Task<IReadOnlyList<PointEntryView>> GetPlayerHistoryAsync(string playerId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.PointEntries
            .AsNoTracking()
            .Where(e => e.PlayerId == playerId)
            .OrderByDescending(e => e.Id)
            .Select(e => new PointEntryView(e.Id, e.PlayerId, e.Player!.UserName!, e.Points, e.Comment, e.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Adds (or with a negative number takes away) points. Returns Czech errors; empty means success.</summary>
    public async Task<IReadOnlyList<string>> AwardAsync(
        string? playerId, int points, string? comment, string? source = null, CancellationToken cancellationToken = default)
    {
        comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();

        var errors = new List<string>();
        if (string.IsNullOrEmpty(playerId))
            errors.Add("Vyber hráče.");
        if (points == 0)
            errors.Add("Zadej počet bodů (záporný body ubere).");
        else if (Math.Abs((long)points) > MaxPointsPerEntry)
            errors.Add($"Najednou jde přidat nebo ubrat nejvýš {MaxPointsPerEntry:N0} bodů.");
        if (comment?.Length > PointEntry.MaxCommentLength)
            errors.Add($"Komentář může mít nejvýš {PointEntry.MaxCommentLength} znaků.");
        if (errors.Count > 0)
            return errors;

        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            if (!await db.Users.AnyAsync(u => u.Id == playerId, cancellationToken))
                return ["Tenhle hráč už neexistuje."];

            db.PointEntries.Add(new PointEntry
            {
                PlayerId = playerId!,
                Points = points,
                Comment = comment,
                Source = source,
                CreatedAtUtc = time.GetUtcNow().UtcDateTime,
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        Changed?.Invoke();
        return [];
    }

    /// <summary>
    /// Like <see cref="AwardAsync"/>, but at most once per player and <paramref name="source"/> (minigame bonuses,
    /// penalties, ...). Returns true when the points were given now.
    /// </summary>
    public async Task<bool> AwardOnceAsync(string playerId, int points, string comment, string source, CancellationToken cancellationToken = default)
    {
        // One at a time, so two tabs finishing together can't both pass the "already has it?" check.
        await onceGate.WaitAsync(cancellationToken);
        try
        {
            await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
            {
                if (await db.PointEntries.AnyAsync(e => e.PlayerId == playerId && e.Source == source, cancellationToken))
                    return false;
            }

            return (await AwardAsync(playerId, points, comment, source, cancellationToken)).Count == 0;
        }
        finally
        {
            onceGate.Release();
        }
    }

    /// <summary>The player's point changes they haven't been shown yet (for the "you got points" popup), oldest first.</summary>
    public async Task<IReadOnlyList<PointEntryView>> GetUnseenAsync(string playerId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.PointEntries
            .AsNoTracking()
            .Where(e => e.PlayerId == playerId && !e.Seen)
            .OrderBy(e => e.Id)
            .Select(e => new PointEntryView(e.Id, e.PlayerId, e.Player!.UserName!, e.Points, e.Comment, e.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    /// <summary>The player closed the popup. (Doesn't raise <see cref="Changed"/>: nothing visible changed.)</summary>
    public async Task MarkSeenAsync(string playerId, IEnumerable<int> entryIds, CancellationToken cancellationToken = default)
    {
        var ids = entryIds.ToList();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.PointEntries
            .Where(e => e.PlayerId == playerId && ids.Contains(e.Id))
            .ExecuteUpdateAsync(set => set.SetProperty(e => e.Seen, true), cancellationToken);
    }

    /// <summary>Removes one point change (e.g. a mistake); the player's score drops or rises accordingly.</summary>
    public async Task DeleteEntryAsync(int entryId, CancellationToken cancellationToken = default)
    {
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
            await db.PointEntries.Where(e => e.Id == entryId).ExecuteDeleteAsync(cancellationToken);

        Changed?.Invoke();
    }

    /// <summary>Tells open leaderboards to reload, e.g. after a player registered or was renamed.</summary>
    public void NotifyChanged() => Changed?.Invoke();
}
