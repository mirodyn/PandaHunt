using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PandaHunt.Data;

namespace PandaHunt.Services.Notifications;

public sealed record DeviceView(int Id, string Label, string Endpoint, DateTime CreatedAtUtc);

/// <summary>A login's notification choices. Browser notifications are "on" when at least one device is registered.</summary>
public sealed record NotificationState(
    string? Email, bool NotifyByEmail, bool NotifyBySms, string? Phone, IReadOnlyList<DeviceView> Devices)
{
    /// <summary>Can this login be reached at all? (SMS doesn't count until it is built.)</summary>
    public bool HasAnyChannel => NotifyByEmail || Devices.Count > 0;
}

/// <summary>What the browser gives when the player allows notifications (a Web Push subscription).</summary>
public sealed record DeviceRegistration(string Endpoint, string P256dh, string Auth, string Label);

/// <summary>
/// Each login's notification choices: e-mail on/off and the browsers (devices) registered for push.
/// Connected pages are notified on every change, so the "set up notifications" banner disappears at once.
/// </summary>
public sealed partial class NotificationPreferences(
    IDbContextFactory<AppDbContext> dbFactory, Notifier notifier, EmailSender email, NotificationSettingsStore settings,
    TimeProvider time)
{
    public const int MaxDevicesPerUser = 10;

    private static readonly TimeSpan EmailTestCooldown = TimeSpan.FromSeconds(30);
    private readonly Dictionary<string, DateTimeOffset> lastEmailTest = [];

    /// <summary>Raised after anyone's choices change.</summary>
    public event Action? Changed;

    public async Task<NotificationState?> GetAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.AsNoTracking().Include(u => u.PushDevices).FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        return user is null
            ? null
            : new NotificationState(user.Email, user.NotifyByEmail, user.NotifyBySms, user.PhoneNumber,
                user.PushDevices.OrderBy(d => d.CreatedAtUtc).Select(d => new DeviceView(d.Id, d.Label, d.Endpoint, d.CreatedAtUtc)).ToList());
    }

    public async Task<DateTime?> GetRegisteredAtAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Users.Where(u => u.Id == userId).Select(u => (DateTime?)u.RegisteredAtUtc).FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>Counts for the admin page: how many logins use each channel and how many have none.</summary>
    public async Task<(int Total, int WithEmail, int WithBrowser, int WithNone)> CountAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var users = await db.Users.AsNoTracking()
            .Select(u => new { u.NotifyByEmail, Devices = u.PushDevices.Count })
            .ToListAsync(cancellationToken);
        return (users.Count, users.Count(u => u.NotifyByEmail), users.Count(u => u.Devices > 0),
            users.Count(u => !u.NotifyByEmail && u.Devices == 0));
    }

    /// <summary>
    /// Turns e-mail notifications on or off. Turning them on first sends a test message to the login e-mail
    /// (addresses aren't verified at registration), and only switches on when it was sent.
    /// Returns Czech errors; empty means success.
    /// </summary>
    public async Task<IReadOnlyList<string>> SetEmailAsync(string userId, bool on, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
            return ["Tenhle účet už neexistuje."];

        if (on)
        {
            if (!email.IsConfigured)
                return ["E-maily zatím nejsou nastavené, admin musí vyplnit SMTP."];
            if (string.IsNullOrWhiteSpace(user.Email))
                return ["K účtu není přiřazený e-mail."];

            var now = time.GetUtcNow();
            lock (lastEmailTest)
            {
                if (lastEmailTest.TryGetValue(userId, out var last) && now - last < EmailTestCooldown)
                    return ["Zkušební e-mail jsi právě dostal. Chvilku počkej a zkus to znovu."];
                lastEmailTest[userId] = now;
            }

            var error = await email.SendAsync(user.Email, "PandaHunt IV: e-mailová oznámení jsou zapnutá",
                "Ahoj! Tohle je zkušební zpráva: od teď ti budou chodit oznámení z PandaHunt IV na tenhle e-mail.\n\n"
                + "Pokud ti zpráva přišla do spamu, označ ji jako důvěryhodnou, ať ti nic neunikne.", cancellationToken);
            if (error is not null)
                return [$"Zkušební e-mail se nepodařilo poslat na {user.Email}. {error}"];
        }

        user.NotifyByEmail = on;
        await db.SaveChangesAsync(cancellationToken);
        Changed?.Invoke();
        return [];
    }

    /// <summary>Registers the browser the player just allowed notifications in. Returns Czech errors; empty means success.</summary>
    public async Task<IReadOnlyList<string>> AddDeviceAsync(string userId, DeviceRegistration registration, CancellationToken cancellationToken = default)
    {
        var endpoint = registration.Endpoint?.Trim() ?? "";
        if (!WebPushSender.IsAllowedEndpoint(endpoint) || endpoint.Length > WebPushDevice.MaxEndpointLength)
            return ["Tenhle prohlížeč používá nepodporovanou službu oznámení."];
        if (!Base64UrlKey().IsMatch(registration.P256dh ?? "") || !Base64UrlKey().IsMatch(registration.Auth ?? ""))
            return ["Prohlížeč poslal neplatné klíče. Zkus to znovu."];

        var label = ControlCharacters().Replace(registration.Label ?? "", "").Trim();
        if (label.Length == 0)
            label = "Neznámé zařízení";
        if (label.Length > WebPushDevice.MaxLabelLength)
            label = label[..WebPushDevice.MaxLabelLength];

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.Users.AnyAsync(u => u.Id == userId, cancellationToken))
            return ["Tenhle účet už neexistuje."];

        var device = await db.WebPushDevices.FirstOrDefaultAsync(d => d.Endpoint == endpoint, cancellationToken);
        if (device is null)
        {
            if (await db.WebPushDevices.CountAsync(d => d.UserId == userId, cancellationToken) >= MaxDevicesPerUser)
                return [$"Můžeš mít nejvýš {MaxDevicesPerUser} zařízení. Nepoužívaná odeber."];

            device = new WebPushDevice
            {
                UserId = userId, Endpoint = endpoint, P256dh = registration.P256dh!, Auth = registration.Auth!,
                Label = label, CreatedAtUtc = time.GetUtcNow().UtcDateTime,
            };
            db.WebPushDevices.Add(device);
        }
        else
        {
            // The same browser again (maybe a different login on it): it now belongs to this login.
            device.UserId = userId;
            device.P256dh = registration.P256dh!;
            device.Auth = registration.Auth!;
            device.Label = label;
        }

        await db.SaveChangesAsync(cancellationToken);
        Changed?.Invoke();
        return [];
    }

    /// <summary>Forgets a device, by its id or by the browser's push address. Only the login's own devices.</summary>
    public async Task RemoveDeviceAsync(string userId, int? deviceId = null, string? endpoint = null, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var removed = await db.WebPushDevices
            .Where(d => d.UserId == userId && ((deviceId != null && d.Id == deviceId) || (endpoint != null && d.Endpoint == endpoint)))
            .ExecuteDeleteAsync(cancellationToken);
        if (removed > 0)
            Changed?.Invoke();
    }

    /// <summary>Does this login have this browser registered? (A device can be removed on the server while the browser still thinks it is subscribed.)</summary>
    public async Task<bool> HasDeviceAsync(string userId, string endpoint, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.WebPushDevices.AnyAsync(d => d.UserId == userId && d.Endpoint == endpoint, cancellationToken);
    }

    /// <summary>Sends the login a test message through one channel ("prohlížeč" / "e-mail").</summary>
    public Task<NotifyResult> SendTestAsync(string userId, string channel, CancellationToken cancellationToken = default) =>
        notifier.NotifyUserAsync(userId,
            new NotificationMessage("PandaHunt IV", "Zkouška oznámení: funguje to. 🐼", "/oznameni", "test"),
            channel, cancellationToken);

    /// <summary>E-mail notifications can be switched on (the admin has filled in SMTP).</summary>
    public bool EmailAvailable => email.IsConfigured;

    /// <summary>The key a browser needs to subscribe to push, or null while push isn't available.</summary>
    public string? PushPublicKey => settings.Vapid?.PublicKey;

    [GeneratedRegex("^[A-Za-z0-9_-]{16,200}$")]
    private static partial Regex Base64UrlKey();

    [GeneratedRegex(@"\p{C}")]
    private static partial Regex ControlCharacters();
}
