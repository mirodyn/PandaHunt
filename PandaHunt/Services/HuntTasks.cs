using Microsoft.EntityFrameworkCore;
using PandaHunt.Data;

namespace PandaHunt.Services;

public sealed record HuntTaskView(int Id, string Name, string Type, DateTimeOffset? OpensAt, string? Link, int CompletedCount);

public sealed record TaskEdit(int Id, string Name, string Type, DateTimeOffset? OpensAt, string? Link);

/// <summary>
/// The hunt's tasks and who has finished them. Connected pages are notified on every change
/// (in-process, fine for a single server instance).
/// </summary>
public sealed class HuntTasks(IDbContextFactory<AppDbContext> dbFactory, TimeProvider time, Scoreboard scoreboard)
{
    /// <summary>Raised after a task is added, edited or deleted, or completions change.</summary>
    public event Action? Changed;

    /// <summary>All tasks in calendar order (by opening date; tasks without a date last).</summary>
    public async Task<IReadOnlyList<HuntTaskView>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var tasks = await db.HuntTasks
            .AsNoTracking()
            .Select(t => new HuntTaskView(t.Id, t.Name, t.Type, t.OpensAt, t.Link, db.TaskCompletions.Count(c => c.TaskId == t.Id)))
            .ToListAsync(cancellationToken);

        // Sorted here: SQLite can't order by DateTimeOffset, and there are only a handful of tasks.
        return tasks
            .OrderBy(t => t.OpensAt is null)
            .ThenBy(t => t.OpensAt)
            .ThenBy(t => t.Id)
            .ToList();
    }

    public async Task<IReadOnlySet<int>> GetCompletedTaskIdsAsync(string playerId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.TaskCompletions
            .Where(c => c.PlayerId == playerId)
            .Select(c => c.TaskId)
            .ToHashSetAsync(cancellationToken);
    }

    public async Task<IReadOnlySet<string>> GetCompletedPlayerIdsAsync(int taskId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.TaskCompletions
            .Where(c => c.TaskId == taskId)
            .Select(c => c.PlayerId)
            .ToHashSetAsync(cancellationToken);
    }

    /// <summary>Creates (Id = 0) or updates a task. Returns Czech errors; empty means success.</summary>
    public async Task<IReadOnlyList<string>> SaveAsync(TaskEdit edit, CancellationToken cancellationToken = default)
    {
        var name = edit.Name.Trim();
        var link = TaskPages.Normalize(edit.Link);

        var errors = new List<string>();
        if (name.Length == 0)
            errors.Add("Vyplň název úkolu.");
        else if (name.Length > HuntTask.MaxNameLength)
            errors.Add($"Název může mít nejvýš {HuntTask.MaxNameLength} znaků.");
        if (!TaskTypes.Exists(edit.Type))
            errors.Add("Vyber typ úkolu.");
        if (link is not null)
        {
            if (link.Length > HuntTask.MaxLinkLength)
                errors.Add($"Odkaz může mít nejvýš {HuntTask.MaxLinkLength} znaků.");
            else if (link.Any(char.IsWhiteSpace))
                errors.Add("Odkaz nesmí obsahovat mezery.");
        }
        if (errors.Count > 0)
            return errors;

        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            if (link is not null)
            {
                var others = await db.HuntTasks.Where(t => t.Id != edit.Id && t.Link != null)
                    .Select(t => new { t.Name, t.Link }).ToListAsync(cancellationToken);
                if (others.FirstOrDefault(t => TaskPages.SameLink(t.Link, link)) is { } other)
                    return [$"Tuhle stránku už má úkol „{other.Name}“."];
            }

            HuntTask? task;
            if (edit.Id == 0)
            {
                task = new HuntTask { Name = name, Type = edit.Type };
                db.HuntTasks.Add(task);
            }
            else
            {
                task = await db.HuntTasks.FindAsync([edit.Id], cancellationToken);
                if (task is null)
                    return ["Tenhle úkol už neexistuje."];
            }

            task.Name = name;
            task.Type = edit.Type;
            task.OpensAt = edit.OpensAt;
            task.Link = link;
            await db.SaveChangesAsync(cancellationToken);
        }

        Changed?.Invoke();
        return [];
    }

    /// <summary>Deletes a task together with its completions and stars.</summary>
    public async Task DeleteAsync(int taskId, CancellationToken cancellationToken = default)
    {
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
            await db.HuntTasks.Where(t => t.Id == taskId).ExecuteDeleteAsync(cancellationToken);

        Changed?.Invoke();
        scoreboard.NotifyChanged(); // the task's stars are gone from their holders' scores
    }

    /// <summary>The task whose <see cref="HuntTask.Link"/> is this address (a task page asking "which task am I?").</summary>
    public async Task<HuntTaskView?> FindByLinkAsync(string path, CancellationToken cancellationToken = default) =>
        (await GetAllAsync(cancellationToken)).FirstOrDefault(t => TaskPages.SameLink(t.Link, path));

    /// <summary>Marks one player as having finished a task (does nothing if they already have).</summary>
    public async Task MarkCompletedAsync(int taskId, string playerId, CancellationToken cancellationToken = default)
    {
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            if (await db.TaskCompletions.AnyAsync(c => c.TaskId == taskId && c.PlayerId == playerId, cancellationToken))
                return;

            db.TaskCompletions.Add(new TaskCompletion { TaskId = taskId, PlayerId = playerId, CompletedAtUtc = time.GetUtcNow().UtcDateTime });
            await db.SaveChangesAsync(cancellationToken);
        }

        Changed?.Invoke();
    }

    /// <summary>Makes exactly <paramref name="playerIds"/> the players who finished the task.</summary>
    public async Task SetCompletedPlayersAsync(int taskId, IReadOnlySet<string> playerIds, CancellationToken cancellationToken = default)
    {
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            var existing = await db.TaskCompletions.Where(c => c.TaskId == taskId).ToListAsync(cancellationToken);
            var existingIds = existing.Select(c => c.PlayerId).ToHashSet();
            var now = time.GetUtcNow().UtcDateTime;

            db.TaskCompletions.RemoveRange(existing.Where(c => !playerIds.Contains(c.PlayerId)));
            db.TaskCompletions.AddRange(playerIds
                .Where(id => !existingIds.Contains(id))
                .Select(id => new TaskCompletion { TaskId = taskId, PlayerId = id, CompletedAtUtc = now }));
            await db.SaveChangesAsync(cancellationToken);
        }

        Changed?.Invoke();
    }
}
