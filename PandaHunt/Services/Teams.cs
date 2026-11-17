using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PandaHunt.Account;
using PandaHunt.Data;

namespace PandaHunt.Services;

/// <summary>
/// Logins and players. Everything in the game (points, stars, QR codes, bonus events...) belongs to a
/// <see cref="Player"/>; each login plays as one. Normally that's a player of its own, but an admin can join
/// several logins into a team that shares one player. Pages call <see cref="GetPlayerIdAsync"/> to turn
/// "who is logged in" into "who is playing". Methods return Czech errors; empty means success.
/// </summary>
public sealed class Teams(IDbContextFactory<AppDbContext> dbFactory, Scoreboard scoreboard, MessageBoard board)
{
    /// <summary>The player a login plays as, or null when it isn't logged in (or the login no longer exists).</summary>
    public async Task<string?> GetPlayerIdAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return null;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.PlayerId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Joins logins into one team. The logins of every involved player all end up on <paramref name="keepPlayerId"/>
    /// (one of those players) and get <paramref name="teamName"/>. The other players are deleted together with
    /// everything they own (points, stars, completions, claims) – that's the rule, no transfer.
    /// </summary>
    public async Task<IReadOnlyList<string>> MergeAsync(
        IReadOnlyCollection<string> userIds, string keepPlayerId, string teamName, CancellationToken cancellationToken = default)
    {
        teamName = teamName.Trim();
        if (teamName.Length is 0 or > Player.MaxNameLength)
            return [$"Název týmu musí mít 1 až {Player.MaxNameLength} znaků."];

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var selected = await db.Users.Where(u => userIds.Contains(u.Id)).ToListAsync(cancellationToken);
        var involved = selected.Select(u => u.PlayerId).Distinct().ToList();
        if (involved.Count < 2 && !(involved.Count == 1 && selected.Count > 1))
            return ["Vyber aspoň dva různé hráče."];
        if (!involved.Contains(keepPlayerId))
            return ["Hráč, který zůstane, musí být jedním z vybraných."];

        var adminPlayers = await Scoreboard.AdminPlayerIds(db).Where(id => involved.Contains(id)).AnyAsync(cancellationToken);
        if (adminPlayers)
            return ["Admini jsou organizátoři, do týmu se dát nedají."];

        var taken = await db.Players.AnyAsync(p => p.Name == teamName && p.Id != keepPlayerId && !involved.Contains(p.Id), cancellationToken);
        if (taken)
            return ["Takhle už se jmenuje jiný hráč nebo tým."];

        // Everybody on the involved players (also teammates who weren't ticked) moves to the kept player.
        await db.Users.Where(u => involved.Contains(u.PlayerId)).ExecuteUpdateAsync(
            set => set.SetProperty(u => u.PlayerId, keepPlayerId), cancellationToken);

        var others = involved.Where(id => id != keepPlayerId).ToList();
        await db.Players.Where(p => others.Contains(p.Id)).ExecuteDeleteAsync(cancellationToken); // cascades to their data

        await db.Players.Where(p => p.Id == keepPlayerId).ExecuteUpdateAsync(
            set => set.SetProperty(p => p.Name, teamName).SetProperty(p => p.IsTeam, true), cancellationToken);

        await tx.CommitAsync(cancellationToken);
        Notify();
        return [];
    }

    public async Task<IReadOnlyList<string>> RenameTeamAsync(string playerId, string teamName, CancellationToken cancellationToken = default)
    {
        teamName = teamName.Trim();
        if (teamName.Length is 0 or > Player.MaxNameLength)
            return [$"Název týmu musí mít 1 až {Player.MaxNameLength} znaků."];

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (await db.Players.AnyAsync(p => p.Name == teamName && p.Id != playerId, cancellationToken))
            return ["Takhle už se jmenuje jiný hráč nebo tým."];

        var changed = await db.Players.Where(p => p.Id == playerId && p.IsTeam).ExecuteUpdateAsync(
            set => set.SetProperty(p => p.Name, teamName), cancellationToken);
        if (changed == 0)
            return ["Tohle není tým."];

        Notify();
        return [];
    }

    /// <summary>
    /// A login leaves its team and starts over as a fresh player with no points. If one login is left in the
    /// team, that one becomes an ordinary player again (keeping the points).
    /// </summary>
    public async Task<IReadOnlyList<string>> LeaveAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var user = await db.Users.Include(u => u.Player).FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user?.Player is not { IsTeam: true } team)
            return ["Tenhle hráč není v týmu."];

        var fresh = new Player { Id = Guid.NewGuid().ToString(), Name = user.UserName! };
        db.Players.Add(fresh);
        user.PlayerId = fresh.Id;
        await db.SaveChangesAsync(cancellationToken);

        await DissolveIfAloneAsync(db, team.Id, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        Notify();
        return [];
    }

    /// <summary>After a login was deleted or left: a team with a single login becomes an ordinary player again; a player nobody plays as is removed.</summary>
    public async Task AfterMemberRemovedAsync(string playerId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await DissolveIfAloneAsync(db, playerId, cancellationToken);
        Notify();
    }

    private static async Task DissolveIfAloneAsync(AppDbContext db, string playerId, CancellationToken cancellationToken)
    {
        var members = await db.Users.Where(u => u.PlayerId == playerId).Select(u => u.UserName!).ToListAsync(cancellationToken);
        if (members.Count == 0)
            await db.Players.Where(p => p.Id == playerId).ExecuteDeleteAsync(cancellationToken); // and its data
        else if (members.Count == 1)
            await db.Players.Where(p => p.Id == playerId && p.IsTeam).ExecuteUpdateAsync(
                set => set.SetProperty(p => p.IsTeam, false).SetProperty(p => p.Name, members[0]), cancellationToken);
    }

    private void Notify()
    {
        scoreboard.NotifyChanged();
        board.NotifyChanged();
    }
}
