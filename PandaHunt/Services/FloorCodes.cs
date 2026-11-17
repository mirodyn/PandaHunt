using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using PandaHunt.Data;

namespace PandaHunt.Services;

public sealed record FloorCodeView(int Id, string Token, string Name, int Points, bool IsActive, DateTime CreatedAtUtc, int ClaimCount);

public sealed record FloorCodeEdit(int Id, string Name, int Points, bool IsActive);

public sealed record FloorClaimView(string PlayerId, string PlayerName, DateTime ClaimedAtUtc, int Points);

public enum FloorClaimOutcome { Claimed, AlreadyClaimed, NotFound, HuntNotOpen }

public sealed record FloorClaimResult(FloorClaimOutcome Outcome, int Points = 0, DateTime? ClaimedAtUtc = null);

/// <summary>
/// "Body na zemi" (Administrace → Body na zemi): QR codes stuck around town. A logged-in player who scans one
/// while the hunt runs gets its points, once per code. Connected pages are notified on every change
/// (in-process, single server).
/// </summary>
public sealed class FloorCodes(
    IDbContextFactory<AppDbContext> dbFactory, TimeProvider time, Scoreboard scoreboard, GameSchedule schedule, GameSettingsStore settings)
{
    /// <summary>Most codes the admin can create in one go.</summary>
    public const int MaxBatch = 50;

    /// <summary>The comment players see next to the points. Deliberately says nothing about where the code was.</summary>
    public const string Comment = "Sebráno ze země";

    private readonly SemaphoreSlim claimGate = new(1, 1);

    /// <summary>Raised after a code is added, edited or deleted, or someone claims one.</summary>
    public event Action? Changed;

    /// <summary>Tags the points entry, so the points history can tell where points came from.</summary>
    public static string SourceOf(int codeId) => $"body-na-zemi-{codeId}";

    /// <summary>The page a code's QR leads to (short, so the QR stays small and easy to scan).</summary>
    public static string PathOf(string token) => $"/q/{token}";

    /// <summary>The admin-only QR image (SVG) of a code.</summary>
    public static string QrImageOf(int codeId, bool download = false) => $"/admin/qr/{codeId}.svg{(download ? "?download=true" : "")}";

    /// <summary>The full address in a code's QR: the admin's setting, else the site the admin is on now.</summary>
    public string UrlOf(string token, string currentBaseUri) =>
        (settings.Current.QrBaseUrl ?? currentBaseUri.TrimEnd('/')) + PathOf(token);

    /// <summary>All codes, newest first.</summary>
    public async Task<IReadOnlyList<FloorCodeView>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.FloorCodes
            .AsNoTracking()
            .OrderByDescending(c => c.Id)
            .Select(c => new FloorCodeView(c.Id, c.Token, c.Name, c.Points, c.IsActive, c.CreatedAtUtc,
                db.FloorCodeClaims.Count(cl => cl.CodeId == c.Id)))
            .ToListAsync(cancellationToken);
    }

    public async Task<FloorCodeView?> GetAsync(int codeId, CancellationToken cancellationToken = default) =>
        (await GetAllAsync(cancellationToken)).FirstOrDefault(c => c.Id == codeId);

    public async Task<FloorCodeView?> FindByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        if (token.Length != FloorCode.TokenLength)
            return null;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.FloorCodes
            .AsNoTracking()
            .Where(c => c.Token == token)
            .Select(c => new FloorCodeView(c.Id, c.Token, c.Name, c.Points, c.IsActive, c.CreatedAtUtc,
                db.FloorCodeClaims.Count(cl => cl.CodeId == c.Id)))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>Who found a code, first finder first.</summary>
    public async Task<IReadOnlyList<FloorClaimView>> GetClaimsAsync(int codeId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var claims = await db.FloorCodeClaims
            .AsNoTracking()
            .Where(c => c.CodeId == codeId)
            .Select(c => new FloorClaimView(c.PlayerId, c.Player!.Name, c.ClaimedAtUtc, c.Points))
            .ToListAsync(cancellationToken);
        return claims.OrderBy(c => c.ClaimedAtUtc).ToList();
    }

    /// <summary>
    /// Creates <paramref name="count"/> codes with the same points. With more than one, they're named
    /// "name 1", "name 2", ... Returns Czech errors; empty means success.
    /// </summary>
    public async Task<IReadOnlyList<string>> CreateAsync(string name, int points, bool isActive, int count, CancellationToken cancellationToken = default)
    {
        name = name.Trim();
        var errors = Validate(name, points);
        if (count is < 1 or > MaxBatch)
            errors.Add($"Najednou jde vytvořit 1 až {MaxBatch} kódů.");
        else if (count > 1 && name.Length + 1 + count.ToString().Length > FloorCode.MaxNameLength)
            errors.Add($"Název je na číslování moc dlouhý (nejvýš {FloorCode.MaxNameLength} znaků i s číslem).");
        if (errors.Count > 0)
            return errors;

        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            var now = time.GetUtcNow().UtcDateTime;
            for (var i = 1; i <= count; i++)
            {
                db.FloorCodes.Add(new FloorCode
                {
                    Token = NewToken(),
                    Name = count == 1 ? name : $"{name} {i}",
                    Points = points,
                    IsActive = isActive,
                    CreatedAtUtc = now,
                });
            }
            await db.SaveChangesAsync(cancellationToken);
        }

        Changed?.Invoke();
        return [];
    }

    /// <summary>Renames a code, changes its points or switches it on/off (the QR stays the same).</summary>
    public async Task<IReadOnlyList<string>> UpdateAsync(FloorCodeEdit edit, CancellationToken cancellationToken = default)
    {
        var name = edit.Name.Trim();
        var errors = Validate(name, edit.Points);
        if (errors.Count > 0)
            return errors;

        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            var code = await db.FloorCodes.FindAsync([edit.Id], cancellationToken);
            if (code is null)
                return ["Tenhle kód už neexistuje."];

            code.Name = name;
            code.Points = edit.Points;
            code.IsActive = edit.IsActive;
            await db.SaveChangesAsync(cancellationToken);
        }

        Changed?.Invoke();
        return [];
    }

    /// <summary>Deletes a code and the record of who found it. Points players got for it stay.</summary>
    public async Task DeleteAsync(int codeId, CancellationToken cancellationToken = default)
    {
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
            await db.FloorCodes.Where(c => c.Id == codeId).ExecuteDeleteAsync(cancellationToken);

        Changed?.Invoke();
    }

    /// <summary>When the player found this code, or null if they haven't yet.</summary>
    public async Task<DateTime?> GetClaimedAtAsync(int codeId, string playerId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.FloorCodeClaims
            .Where(c => c.CodeId == codeId && c.PlayerId == playerId)
            .Select(c => (DateTime?)c.ClaimedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>A player scanned a code: gives them its points unless they already have them. Not for admins.</summary>
    public async Task<FloorClaimResult> ClaimAsync(string token, string playerId, CancellationToken cancellationToken = default)
    {
        // One at a time, so two tabs can't both pass the "already claimed?" check.
        await claimGate.WaitAsync(cancellationToken);
        FloorClaimResult result;
        try
        {
            var code = await FindByTokenAsync(token, cancellationToken);
            if (code is not { IsActive: true })
                return new FloorClaimResult(FloorClaimOutcome.NotFound);

            if (await GetClaimedAtAsync(code.Id, playerId, cancellationToken) is { } claimedAt)
                return new FloorClaimResult(FloorClaimOutcome.AlreadyClaimed, code.Points, claimedAt);

            if (!schedule.IsHuntOpen)
                return new FloorClaimResult(FloorClaimOutcome.HuntNotOpen);

            var now = time.GetUtcNow().UtcDateTime;
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            db.FloorCodeClaims.Add(new FloorCodeClaim { CodeId = code.Id, PlayerId = playerId, ClaimedAtUtc = now, Points = code.Points });
            // Same save as the claim: either both are there or neither.
            db.PointEntries.Add(new PointEntry
            {
                PlayerId = playerId,
                Points = code.Points,
                Comment = Comment,
                Source = SourceOf(code.Id),
                CreatedAtUtc = now,
            });
            await db.SaveChangesAsync(cancellationToken);
            result = new FloorClaimResult(FloorClaimOutcome.Claimed, code.Points, now);
        }
        finally
        {
            claimGate.Release();
        }

        Changed?.Invoke();
        scoreboard.NotifyChanged(); // leaderboards + the "you got points" popup
        return result;
    }

    private static List<string> Validate(string name, int points)
    {
        var errors = new List<string>();
        if (name.Length == 0)
            errors.Add("Vyplň název (hráči ho neuvidí – klidně napiš, kde kód visí).");
        else if (name.Length > FloorCode.MaxNameLength)
            errors.Add($"Název může mít nejvýš {FloorCode.MaxNameLength} znaků.");
        if (points is < 1 or > GameSettings.MaxBonusPoints)
            errors.Add($"Body musí být 1 až {GameSettings.MaxBonusPoints}.");
        return errors;
    }

    // 12 random bytes = 16 URL-safe characters, impossible to guess.
    private static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(12)).Replace('+', '-').Replace('/', '_');
}

public static class FloorCodeEndpoints
{
    /// <summary>The QR images (SVG), for admins: shown in the admin page and print sheet, or downloaded.</summary>
    public static IEndpointRouteBuilder MapFloorCodeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/admin/qr/{id:int}.svg", async (int id, bool? download, HttpContext context, FloorCodes codes) =>
            {
                if (await codes.GetAsync(id) is not { } code)
                    return Results.NotFound();

                var request = context.Request;
                var url = codes.UrlOf(code.Token, $"{request.Scheme}://{request.Host}{request.PathBase}");
                if (download == true)
                    context.Response.Headers.ContentDisposition = $"attachment; filename=\"pandahunt-qr-{id}.svg\"";
                context.Response.Headers.CacheControl = "no-store";
                return Results.Content(QrSvg.Render(url), "image/svg+xml");
            })
            .RequireAuthorization(policy => policy.RequireRole(Account.AppRoles.Admin));

        return endpoints;
    }
}
