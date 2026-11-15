using Microsoft.EntityFrameworkCore;
using PandaHunt.Data;

namespace PandaHunt.Services;

public enum BonusEventState { Planned, Live, Over }

public sealed record BonusEventView(
    int Id, string Name, string? Description, DateTimeOffset StartsAt, DateTimeOffset EndsAt,
    double Latitude, double Longitude, int RadiusMeters, int Points, bool ShowPlace, int ClaimCount)
{
    public BonusEventState StateAt(DateTimeOffset now) =>
        now < StartsAt ? BonusEventState.Planned : now < EndsAt ? BonusEventState.Live : BonusEventState.Over;

    /// <summary>Paragraphs of <see cref="Description"/> (separated by a blank line).</summary>
    public IEnumerable<string> Paragraphs =>
        (Description ?? "").ReplaceLineEndings("\n")
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

public sealed record BonusEventEdit(
    int Id, string Name, string? Description, DateTimeOffset? StartsAt, DateTimeOffset? EndsAt,
    double? Latitude, double? Longitude, int RadiusMeters, int Points, bool ShowPlace);

public sealed record BonusClaimView(string PlayerId, string PlayerName, DateTime ClaimedAtUtc, int DistanceMeters, int AccuracyMeters, int Points);

public enum ClaimOutcome { Claimed, AlreadyClaimed, TooFar, NotStarted, Over, NotFound }

public sealed record ClaimResult(ClaimOutcome Outcome, int DistanceMeters = 0);

/// <summary>
/// Bonus events (Administrace → Bonusové akce) and their claims: a player standing at the event's place while it
/// runs gets its points, once. Connected pages are notified on every change (in-process, single server).
/// </summary>
public sealed class BonusEvents(IDbContextFactory<AppDbContext> dbFactory, TimeProvider time, Scoreboard scoreboard)
{
    private readonly SemaphoreSlim claimGate = new(1, 1);

    /// <summary>Raised after an event is added, edited or deleted, or someone claims one.</summary>
    public event Action? Changed;

    /// <summary>Tags the points entry, so the points history can tell where points came from.</summary>
    public static string SourceOf(int eventId) => $"bonusova-akce-{eventId}";

    /// <summary>The comment players see next to the points.</summary>
    public static string CommentFor(string eventName) => $"Bonusová akce „{eventName}“";

    /// <summary>The address of the event's page.</summary>
    public static string PageOf(int eventId) => $"/lov/akce/{eventId}";

    /// <summary>All events, newest first (planned ones on top).</summary>
    public async Task<IReadOnlyList<BonusEventView>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var events = await db.BonusEvents
            .AsNoTracking()
            .Select(e => new BonusEventView(e.Id, e.Name, e.Description, e.StartsAt, e.EndsAt, e.Latitude, e.Longitude,
                e.RadiusMeters, e.Points, e.ShowPlace, db.BonusEventClaims.Count(c => c.EventId == e.Id)))
            .ToListAsync(cancellationToken);

        // Sorted here: SQLite can't order by DateTimeOffset, and there are only a handful of events.
        return events.OrderByDescending(e => e.StartsAt).ThenByDescending(e => e.Id).ToList();
    }

    public async Task<BonusEventView?> GetAsync(int eventId, CancellationToken cancellationToken = default) =>
        (await GetAllAsync(cancellationToken)).FirstOrDefault(e => e.Id == eventId);

    /// <summary>What /lov shows: the running event, otherwise the next planned one, otherwise null.</summary>
    public static BonusEventView? CurrentOf(IEnumerable<BonusEventView> events, DateTimeOffset now)
    {
        var list = events.ToList();
        return list.Where(e => e.StateAt(now) == BonusEventState.Live).MinBy(e => e.EndsAt)
            ?? list.Where(e => e.StateAt(now) == BonusEventState.Planned).MinBy(e => e.StartsAt);
    }

    public async Task<IReadOnlySet<int>> GetClaimedEventIdsAsync(string playerId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.BonusEventClaims
            .Where(c => c.PlayerId == playerId)
            .Select(c => c.EventId)
            .ToHashSetAsync(cancellationToken);
    }

    /// <summary>The player's claim of one event, or null if they haven't claimed it.</summary>
    public async Task<BonusClaimView?> GetClaimAsync(int eventId, string playerId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.BonusEventClaims
            .AsNoTracking()
            .Where(c => c.EventId == eventId && c.PlayerId == playerId)
            .Select(c => new BonusClaimView(c.PlayerId, c.Player!.UserName!, c.ClaimedAtUtc, c.DistanceMeters, c.AccuracyMeters, c.Points))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>Who claimed an event, in the order they came.</summary>
    public async Task<IReadOnlyList<BonusClaimView>> GetClaimsAsync(int eventId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.BonusEventClaims
            .AsNoTracking()
            .Where(c => c.EventId == eventId)
            .OrderBy(c => c.ClaimedAtUtc)
            .Select(c => new BonusClaimView(c.PlayerId, c.Player!.UserName!, c.ClaimedAtUtc, c.DistanceMeters, c.AccuracyMeters, c.Points))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Creates (Id = 0) or updates an event. Returns Czech errors; empty means success.</summary>
    public async Task<IReadOnlyList<string>> SaveAsync(BonusEventEdit edit, CancellationToken cancellationToken = default)
    {
        var name = edit.Name.Trim();
        var description = string.IsNullOrWhiteSpace(edit.Description) ? null : edit.Description.Trim();

        var errors = new List<string>();
        if (name.Length == 0)
            errors.Add("Vyplň název akce.");
        else if (name.Length > BonusEvent.MaxNameLength)
            errors.Add($"Název může mít nejvýš {BonusEvent.MaxNameLength} znaků.");
        if (description?.Length > BonusEvent.MaxDescriptionLength)
            errors.Add($"Popis může mít nejvýš {BonusEvent.MaxDescriptionLength} znaků.");
        if (edit.StartsAt is null || edit.EndsAt is null)
            errors.Add("Vyplň začátek i konec akce.");
        else if (edit.EndsAt <= edit.StartsAt)
            errors.Add("Konec musí být až po začátku.");
        if (edit.Latitude is not { } lat || edit.Longitude is not { } lng || !Geo.IsValid(lat, lng))
            errors.Add("Vyber místo na mapě (nebo vlož souřadnice).");
        if (edit.RadiusMeters is < BonusEvent.MinRadiusMeters or > BonusEvent.MaxRadiusMeters)
            errors.Add($"Okruh musí být {BonusEvent.MinRadiusMeters}–{BonusEvent.MaxRadiusMeters} m.");
        if (edit.Points is < 1 or > GameSettings.MaxBonusPoints)
            errors.Add($"Body musí být 1–{GameSettings.MaxBonusPoints}.");
        if (errors.Count > 0)
            return errors;

        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            // One event at a time, so /lov never has to choose between two.
            var others = await db.BonusEvents.AsNoTracking().Where(e => e.Id != edit.Id).ToListAsync(cancellationToken);
            if (others.FirstOrDefault(e => e.StartsAt < edit.EndsAt && edit.StartsAt < e.EndsAt) is { } clash)
                return [$"V tu dobu už je akce „{clash.Name}“. Akce se nesmí časově překrývat."];

            BonusEvent? bonusEvent;
            if (edit.Id == 0)
            {
                bonusEvent = new BonusEvent { Name = name };
                db.BonusEvents.Add(bonusEvent);
            }
            else
            {
                bonusEvent = await db.BonusEvents.FindAsync([edit.Id], cancellationToken);
                if (bonusEvent is null)
                    return ["Tahle akce už neexistuje."];
            }

            bonusEvent.Name = name;
            bonusEvent.Description = description;
            bonusEvent.StartsAt = edit.StartsAt!.Value;
            bonusEvent.EndsAt = edit.EndsAt!.Value;
            bonusEvent.Latitude = edit.Latitude!.Value;
            bonusEvent.Longitude = edit.Longitude!.Value;
            bonusEvent.RadiusMeters = edit.RadiusMeters;
            bonusEvent.Points = edit.Points;
            bonusEvent.ShowPlace = edit.ShowPlace;
            await db.SaveChangesAsync(cancellationToken);
        }

        Changed?.Invoke();
        return [];
    }

    /// <summary>Deletes an event and its claims. The points players got for it stay.</summary>
    public async Task DeleteAsync(int eventId, CancellationToken cancellationToken = default)
    {
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
            await db.BonusEvents.Where(e => e.Id == eventId).ExecuteDeleteAsync(cancellationToken);

        Changed?.Invoke();
    }

    /// <summary>How far a position is from the event's pin, and whether that is close enough.</summary>
    public static (int DistanceMeters, bool Inside) Measure(BonusEventView bonusEvent, double latitude, double longitude)
    {
        var distance = Geo.DistanceMeters(bonusEvent.Latitude, bonusEvent.Longitude, latitude, longitude);
        return ((int)Math.Round(distance), distance <= bonusEvent.RadiusMeters);
    }

    /// <summary>
    /// A player says "I'm here": if the event is running and the position is within its radius, they get its points
    /// (once). The position comes from the player's browser.
    /// </summary>
    public async Task<ClaimResult> ClaimAsync(
        int eventId, string playerId, double latitude, double longitude, double accuracyMeters, CancellationToken cancellationToken = default)
    {
        if (!Geo.IsValid(latitude, longitude))
            return new ClaimResult(ClaimOutcome.TooFar);

        // One at a time, so two tabs can't both pass the "already claimed?" check.
        await claimGate.WaitAsync(cancellationToken);
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var bonusEvent = await GetAsync(eventId, cancellationToken);
            if (bonusEvent is null)
                return new ClaimResult(ClaimOutcome.NotFound);

            var now = time.GetUtcNow();
            switch (bonusEvent.StateAt(now))
            {
                case BonusEventState.Planned:
                    return new ClaimResult(ClaimOutcome.NotStarted);
                case BonusEventState.Over:
                    return new ClaimResult(ClaimOutcome.Over);
            }

            if (await db.BonusEventClaims.AnyAsync(c => c.EventId == eventId && c.PlayerId == playerId, cancellationToken))
                return new ClaimResult(ClaimOutcome.AlreadyClaimed);

            var (distance, inside) = Measure(bonusEvent, latitude, longitude);
            if (!inside)
                return new ClaimResult(ClaimOutcome.TooFar, distance);

            var accuracy = double.IsFinite(accuracyMeters) ? (int)Math.Clamp(Math.Round(accuracyMeters), 0, 1_000_000) : 0;
            db.BonusEventClaims.Add(new BonusEventClaim
            {
                EventId = eventId,
                PlayerId = playerId,
                ClaimedAtUtc = now.UtcDateTime,
                DistanceMeters = distance,
                AccuracyMeters = accuracy,
                Points = bonusEvent.Points,
            });
            // Same save as the claim: either both are there or neither.
            db.PointEntries.Add(new PointEntry
            {
                PlayerId = playerId,
                Points = bonusEvent.Points,
                Comment = CommentFor(bonusEvent.Name),
                Source = SourceOf(eventId),
                CreatedAtUtc = now.UtcDateTime,
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            claimGate.Release();
        }

        Changed?.Invoke();
        scoreboard.NotifyChanged(); // leaderboards + the "you got points" popup
        return new ClaimResult(ClaimOutcome.Claimed);
    }
}
