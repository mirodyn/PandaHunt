using Microsoft.EntityFrameworkCore;
using PandaHunt.Data;

namespace PandaHunt.Services;

/// <summary>
/// Holds the current <see cref="GameSettings"/> in memory (read on every request, so no DB hit)
/// and persists changes made on the admin page.
/// </summary>
public sealed class GameSettingsStore(IDbContextFactory<AppDbContext> dbFactory)
{
    private GameSettings current = new();

    /// <summary>The saved settings. Treat as read-only; use <see cref="Edit"/> to get a copy to change.</summary>
    public GameSettings Current => Volatile.Read(ref current);

    /// <summary>Raised after settings are saved, so open pages can update immediately.</summary>
    public event Action? Changed;

    public GameSettings Edit() => Current.Clone();

    /// <summary>Loads settings at startup, creating the row with defaults (everything off) on first run.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var settings = await db.GameSettings.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == GameSettings.SingletonId, cancellationToken);

        if (settings is null)
        {
            settings = new GameSettings();
            db.GameSettings.Add(settings);
            await db.SaveChangesAsync(cancellationToken);
        }

        Volatile.Write(ref current, settings);
    }

    /// <summary>Validates and saves. Returns Czech error messages; empty means saved.</summary>
    public async Task<IReadOnlyList<string>> SaveAsync(GameSettings settings, CancellationToken cancellationToken = default)
    {
        var copy = settings.Clone();
        copy.Id = GameSettings.SingletonId;
        copy.Phase0Code = GameSettings.NormalizeCode(copy.Phase0Code);
        copy.QrBaseUrl = string.IsNullOrWhiteSpace(copy.QrBaseUrl) ? null : copy.QrBaseUrl.Trim().TrimEnd('/');

        var errors = Validate(copy);
        if (errors.Count > 0)
            return errors;

        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            db.GameSettings.Update(copy);
            await db.SaveChangesAsync(cancellationToken);
        }

        Volatile.Write(ref current, copy);
        Changed?.Invoke();
        return errors;
    }

    private static List<string> Validate(GameSettings s)
    {
        var errors = new List<string>();

        if (s.Phase0Code.Length > GameSettings.MaxCodeLength)
            errors.Add($"Kód může mít nejvýš {GameSettings.MaxCodeLength} znaků.");
        if (s.Phase0Enabled && s.Phase0Code.Length == 0)
            errors.Add("Fáze 0 je zapnutá, ale kód je prázdný. Bez kódu by se nikdo nezaregistroval.");
        if (s.Phase0BonusPoints is < 0 or > GameSettings.MaxBonusPoints)
            errors.Add($"Bonus za registraci musí být 0 až {GameSettings.MaxBonusPoints} bodů.");
        if (s.FirstPlayerBonusPoints is < 0 or > GameSettings.MaxBonusPoints)
            errors.Add($"Bonus pro prvního registrovaného musí být 0 až {GameSettings.MaxBonusPoints} bodů.");
        if (s.PandaFeedingBonusPoints is < 0 or > GameSettings.MaxBonusPoints)
            errors.Add($"Body za nakrmení pandy musí být 0 až {GameSettings.MaxBonusPoints}.");
        if (s.PandaPesteringPenaltyPoints is < 0 or > GameSettings.MaxBonusPoints)
            errors.Add($"Srážka za otravování pandy musí být 0 až {GameSettings.MaxBonusPoints} bodů.");
        if (s.RegistrationOpensAt is { } opens && s.RegistrationClosesAt is { } closes && closes <= opens)
            errors.Add("Registrace se musí zavírat až po otevření.");
        if (s.HuntStartsAt is { } starts && s.HuntEndsAt is { } ends && ends <= starts)
            errors.Add("Lov musí končit až po začátku.");
        if (s.HuntIntro?.Length > GameSettings.MaxHuntIntroLength)
            errors.Add($"Úvodní text lovu může mít nejvýš {GameSettings.MaxHuntIntroLength} znaků.");
        if (s.QrBaseUrl is { } qr && (qr.Length > GameSettings.MaxQrBaseUrlLength
                || !Uri.TryCreate(qr, UriKind.Absolute, out var qrUri)
                || qrUri.Scheme is not ("https" or "http")
                || qrUri.Query.Length > 0 || qrUri.Fragment.Length > 0))
            errors.Add("Adresa pro QR kódy musí být celá adresa webu, např. https://www.pandahunt.cz.");

        return errors;
    }
}
