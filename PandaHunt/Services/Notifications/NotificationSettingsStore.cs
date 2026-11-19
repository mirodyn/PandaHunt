using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using PandaHunt.Data;

namespace PandaHunt.Services.Notifications;

/// <summary>A usable SMTP account (the password already decrypted).</summary>
public sealed record SmtpConfig(
    string Host, int Port, SmtpSecurity Security, string? User, string? Password, string FromAddress, string FromName);

/// <summary>The push key pair (both base64url).</summary>
public sealed record VapidKeys(string PublicKey, string PrivateKey);

/// <summary>What the admin types into the SMTP form. An empty <see cref="Password"/> keeps the saved one.</summary>
public sealed record SmtpEdit(
    string? Host, int Port, SmtpSecurity Security, string? User, string? Password, string? FromAddress, string? FromName);

/// <summary>
/// Holds the <see cref="NotificationSettings"/> in memory and persists changes from Administrace → Oznámení.
/// Also creates the push key pair on first start.
/// </summary>
public sealed class NotificationSettingsStore(IDbContextFactory<AppDbContext> dbFactory, SecretProtector protector)
{
    private NotificationSettings current = new();

    /// <summary>The saved settings (read-only; secrets are still encrypted).</summary>
    public NotificationSettings Current => Volatile.Read(ref current);

    /// <summary>Raised after the SMTP settings are saved.</summary>
    public event Action? Changed;

    /// <summary>The SMTP account, or null while the admin hasn't filled it in.</summary>
    public SmtpConfig? Smtp
    {
        get
        {
            var s = Current;
            if (string.IsNullOrWhiteSpace(s.SmtpHost) || string.IsNullOrWhiteSpace(s.SmtpFromAddress))
                return null;
            return new SmtpConfig(s.SmtpHost, s.SmtpPort, s.SmtpSecurity, s.SmtpUser,
                protector.Unprotect(s.SmtpPasswordProtected), s.SmtpFromAddress, s.SmtpFromName ?? "");
        }
    }

    public bool IsSmtpConfigured => Smtp is not null;

    public bool HasSmtpPassword => !string.IsNullOrEmpty(Current.SmtpPasswordProtected);

    public VapidKeys? Vapid =>
        Current is { VapidPublicKey: { Length: > 0 } pub } s && protector.Unprotect(s.VapidPrivateKeyProtected) is { } priv
            ? new VapidKeys(pub, priv)
            : null;

    /// <summary>Loads the row at startup, creating it (and the push keys) on first run.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var settings = await db.NotificationSettings.SingleOrDefaultAsync(s => s.Id == NotificationSettings.SingletonId, cancellationToken);
        if (settings is null)
        {
            settings = new NotificationSettings();
            db.NotificationSettings.Add(settings);
        }

        // Keys that can't be read any more (the data-protection keys were replaced) are replaced too:
        // browsers then have to subscribe again, which they have to do anyway.
        if (string.IsNullOrEmpty(settings.VapidPublicKey) || protector.Unprotect(settings.VapidPrivateKeyProtected) is null)
        {
            var keys = GenerateVapidKeys();
            settings.VapidPublicKey = keys.PublicKey;
            settings.VapidPrivateKeyProtected = protector.Protect(keys.PrivateKey);
            await db.WebPushDevices.ExecuteDeleteAsync(cancellationToken); // subscriptions made with the old key are useless
        }

        await db.SaveChangesAsync(cancellationToken);
        Volatile.Write(ref current, settings);
    }

    /// <summary>Validates and saves the SMTP form. Returns Czech errors; empty means saved.</summary>
    public async Task<IReadOnlyList<string>> SaveSmtpAsync(SmtpEdit edit, CancellationToken cancellationToken = default)
    {
        var host = Clean(edit.Host);
        var user = Clean(edit.User);
        var from = Clean(edit.FromAddress);
        var name = Clean(edit.FromName);

        var errors = new List<string>();
        if (host is null != from is null)
            errors.Add("Vyplň server i adresu odesílatele (nebo obojí nech prázdné, e-maily se pak neposílají).");
        if (host is not null && (host.Contains(' ') || host.Length > NotificationSettings.MaxTextLength))
            errors.Add("Server (host) vypadá špatně, má to být třeba smtp.example.com.");
        if (edit.Port is < 1 or > 65535)
            errors.Add("Port musí být 1 až 65535.");
        if (from is not null && !IsEmailAddress(from))
            errors.Add("Adresa odesílatele nevypadá jako e-mail.");
        if (name?.Length > NotificationSettings.MaxTextLength || user?.Length > NotificationSettings.MaxTextLength)
            errors.Add($"Jméno odesílatele i uživatel můžou mít nejvýš {NotificationSettings.MaxTextLength} znaků.");
        if (errors.Count > 0)
            return errors;

        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            var settings = await db.NotificationSettings.SingleAsync(s => s.Id == NotificationSettings.SingletonId, cancellationToken);
            settings.SmtpHost = host;
            settings.SmtpPort = edit.Port;
            settings.SmtpSecurity = edit.Security;
            settings.SmtpUser = user;
            settings.SmtpFromAddress = from;
            settings.SmtpFromName = name;
            if (user is null)
                settings.SmtpPasswordProtected = null; // no login, no password
            else if (!string.IsNullOrEmpty(edit.Password))
                settings.SmtpPasswordProtected = protector.Protect(edit.Password);

            await db.SaveChangesAsync(cancellationToken);
            Volatile.Write(ref current, settings);
        }

        Changed?.Invoke();
        return [];
    }

    public static bool IsEmailAddress(string value) =>
        MimeKit.MailboxAddress.TryParse(value, out var address) && address.Address.Contains('@') && !value.Contains(' ');

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static VapidKeys GenerateVapidKeys()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = key.ExportParameters(includePrivateParameters: true);
        var publicKey = new byte[65]; // uncompressed point: 0x04 | X | Y
        publicKey[0] = 0x04;
        p.Q.X!.CopyTo(publicKey, 1);
        p.Q.Y!.CopyTo(publicKey, 33);
        return new VapidKeys(Base64Url(publicKey), Base64Url(p.D!));
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
