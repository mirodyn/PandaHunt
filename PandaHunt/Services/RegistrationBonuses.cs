using Microsoft.EntityFrameworkCore;
using PandaHunt.Account;
using PandaHunt.Data;

namespace PandaHunt.Services;

/// <summary>
/// Points a player gets just for registering. Amounts are set in Administrace → Nastavení hry (0 = off).
/// Organisers (configured admins) never get them; they aren't on the leaderboard.
/// </summary>
public sealed class RegistrationBonuses(
    IDbContextFactory<AppDbContext> dbFactory,
    GameSettingsStore settings,
    Scoreboard scoreboard,
    AdminBootstrap bootstrap)
{
    public const string Phase0Source = "registrace-faze0";
    public const string FirstPlayerSource = "prvni-registrace";

    /// <param name="phase0Active">Phase 0 was on when they registered, i.e. they found the code.</param>
    public async Task AwardAsync(ApplicationUser user, bool phase0Active, CancellationToken cancellationToken = default)
    {
        if (bootstrap.IsConfiguredAdmin(user.Email))
            return;

        var current = settings.Current;

        if (phase0Active && current.Phase0BonusPoints > 0)
            await scoreboard.AwardAsync(user.Id, current.Phase0BonusPoints,
                "Bonus za nalezení kódu na Vánočním večírku Výrostků", Phase0Source, cancellationToken);

        if (current.FirstPlayerBonusPoints > 0 && await IsFirstPlayerAsync(user.Id, cancellationToken))
            await scoreboard.AwardAsync(user.Id, current.FirstPlayerBonusPoints,
                "Bonus za první registraci ze všech", FirstPlayerSource, cancellationToken);
    }

    /// <summary>
    /// Nobody has the first-player bonus yet and this is the earliest registered player (admins don't count).
    /// Comparing registration times, not "am I alone?", keeps it fair if two people register at once.
    /// </summary>
    private async Task<bool> IsFirstPlayerAsync(string userId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        if (await db.PointEntries.AnyAsync(e => e.Source == FirstPlayerSource, cancellationToken))
            return false;

        var adminIds = db.UserRoles
            .Where(ur => db.Roles.Any(r => r.Id == ur.RoleId && r.Name == AppRoles.Admin))
            .Select(ur => ur.UserId);

        var firstPlayerId = await db.Users
            .Where(u => !adminIds.Contains(u.Id))
            .OrderBy(u => u.RegisteredAtUtc)
            .ThenBy(u => u.Id)
            .Select(u => u.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return firstPlayerId == userId;
    }
}
