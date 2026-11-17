using Microsoft.EntityFrameworkCore;
using PandaHunt.Data;

namespace PandaHunt.Services;

/// <summary>How many stars of each colour a player holds right now.</summary>
public sealed record StarCounts(int Gold, int Silver, int Bronze)
{
    public static readonly StarCounts None = new(0, 0, 0);

    public int Count => Gold + Silver + Bronze;

    /// <summary>What the stars add to the score.</summary>
    public int Points => StarColors.All.Sum(color => this[color] * StarColors.Points(color));

    public int this[StarColor color] => color switch
    {
        StarColor.Gold => Gold,
        StarColor.Silver => Silver,
        _ => Bronze,
    };

    public static StarCounts From(IEnumerable<StarColor> colors)
    {
        var list = colors as IReadOnlyCollection<StarColor> ?? colors.ToList();
        return new(list.Count(c => c == StarColor.Gold), list.Count(c => c == StarColor.Silver), list.Count(c => c == StarColor.Bronze));
    }
}

/// <summary>A star someone holds: from which task, and since when.</summary>
public sealed record HeldStar(int TaskId, string TaskName, StarColor Color, DateTime SinceUtc);

/// <summary>Who holds one of a task's stars.</summary>
public sealed record StarHolder(StarColor Color, string PlayerId, string PlayerName, DateTime SinceUtc);

/// <summary>What the star colours are worth and what they're called.</summary>
public static class StarColors
{
    /// <summary>Best first.</summary>
    public static readonly IReadOnlyList<StarColor> All = [StarColor.Gold, StarColor.Silver, StarColor.Bronze];

    public static int Points(StarColor color) => color switch
    {
        StarColor.Gold => 3,
        StarColor.Silver => 2,
        _ => 1,
    };

    /// <summary>"Zlatá hvězda".</summary>
    public static string Label(StarColor color) => color switch
    {
        StarColor.Gold => "Zlatá hvězda",
        StarColor.Silver => "Stříbrná hvězda",
        _ => "Bronzová hvězda",
    };

    /// <summary>"1 zlatá hvězda", "3 stříbrné hvězdy", "0 bronzových hvězd".</summary>
    public static string Count(StarColor color, int n) => color switch
    {
        StarColor.Gold => $"{n} {CzechText.Plural(n, "zlatá hvězda", "zlaté hvězdy", "zlatých hvězd")}",
        StarColor.Silver => $"{n} {CzechText.Plural(n, "stříbrná hvězda", "stříbrné hvězdy", "stříbrných hvězd")}",
        _ => $"{n} {CzechText.Plural(n, "bronzová hvězda", "bronzové hvězdy", "bronzových hvězd")}",
    };

    public static string CssClass(StarColor color) => color switch
    {
        StarColor.Gold => "ph-star-gold",
        StarColor.Silver => "ph-star-silver",
        _ => "ph-star-bronze",
    };
}

/// <summary>
/// Bonus stars of the tasks: who holds which. Each task has a gold, a silver and a bronze star (worth
/// <see cref="StarColors.Points"/>); a star counts towards its holder's score for as long as they hold it,
/// and moves when someone takes it over. For now the admin hands them out (Administrace → Úkoly); later
/// the task pages will call <see cref="SetHoldersAsync"/> by their own rules.
/// </summary>
public sealed class TaskStars(IDbContextFactory<AppDbContext> dbFactory, TimeProvider time, Scoreboard scoreboard)
{
    // One change at a time, so two players finishing a task together can't hand out the same star twice.
    private readonly SemaphoreSlim gate = new(1, 1);

    /// <summary>The stars a player holds, in task order (best colour first within a task).</summary>
    public async Task<IReadOnlyList<HeldStar>> GetHeldAsync(string playerId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var stars = await db.TaskStars
            .AsNoTracking()
            .Where(s => s.PlayerId == playerId)
            .Select(s => new { s.TaskId, s.Task!.Name, s.Task.OpensAt, s.Color, s.SinceUtc })
            .ToListAsync(cancellationToken);

        // Same order as the tasks on /lov (sorted here: SQLite can't order by DateTimeOffset).
        return stars
            .OrderBy(s => s.OpensAt is null)
            .ThenBy(s => s.OpensAt)
            .ThenBy(s => s.TaskId)
            .ThenByDescending(s => StarColors.Points(s.Color))
            .Select(s => new HeldStar(s.TaskId, s.Name, s.Color, s.SinceUtc))
            .ToList();
    }

    /// <summary>Who holds the task's stars (colours nobody holds yet are missing).</summary>
    public async Task<IReadOnlyList<StarHolder>> GetHoldersAsync(int taskId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var holders = await db.TaskStars
            .AsNoTracking()
            .Where(s => s.TaskId == taskId)
            .Select(s => new StarHolder(s.Color, s.PlayerId, s.Player!.Name, s.SinceUtc))
            .ToListAsync(cancellationToken);
        return holders.OrderByDescending(h => StarColors.Points(h.Color)).ToList();
    }

    /// <summary>
    /// Says who holds each of the task's stars now; a colour missing from <paramref name="holders"/> goes to
    /// nobody. A star that moves gets a new "since" date, the others keep theirs. One player can hold only one
    /// star of a task, and only players (not admins) get stars. Returns Czech errors; empty means success.
    /// </summary>
    public async Task<IReadOnlyList<string>> SetHoldersAsync(
        int taskId, IReadOnlyDictionary<StarColor, string> holders, CancellationToken cancellationToken = default)
    {
        var playerIds = holders.Values.ToList();
        if (playerIds.Distinct().Count() != playerIds.Count)
            return ["Jeden lovec může mít z jednoho úkolu jen jednu hvězdu."];

        var changed = false;
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            if (!await db.HuntTasks.AnyAsync(t => t.Id == taskId, cancellationToken))
                return ["Tenhle úkol už neexistuje."];

            var adminIds = Scoreboard.AdminPlayerIds(db);
            var players = await db.Players.CountAsync(p => playerIds.Contains(p.Id) && !adminIds.Contains(p.Id), cancellationToken);
            if (players != playerIds.Count)
                return ["Hvězdy můžou dostat jen hráči – někdo z vybraných už neexistuje, nebo je admin."];

            var current = await db.TaskStars.Where(s => s.TaskId == taskId).ToListAsync(cancellationToken);
            var now = time.GetUtcNow().UtcDateTime;
            foreach (var color in StarColors.All)
            {
                var star = current.FirstOrDefault(s => s.Color == color);
                var holder = holders.GetValueOrDefault(color);
                if (star?.PlayerId == holder)
                    continue;

                changed = true;
                if (holder is null)
                {
                    db.TaskStars.Remove(star!);
                }
                else if (star is null)
                {
                    db.TaskStars.Add(new TaskStar { TaskId = taskId, Color = color, PlayerId = holder, SinceUtc = now });
                }
                else
                {
                    star.PlayerId = holder;
                    star.SinceUtc = now;
                }
            }
            await db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            gate.Release();
        }

        if (changed)
            scoreboard.NotifyChanged(); // scores and leaderboards
        return [];
    }
}
