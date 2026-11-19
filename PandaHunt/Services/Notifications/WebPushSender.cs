using System.Net;
using System.Text.Json;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using PandaHunt.Data;

namespace PandaHunt.Services.Notifications;

public enum PushOutcome { Delivered, Gone, Failed }

/// <summary>Sends Web Push messages to browsers, signing them with the server's VAPID keys.</summary>
public sealed class WebPushSender(NotificationSettingsStore settings, GameSettingsStore game, ILogger<WebPushSender> logger)
    : IDisposable
{
    private static readonly string[] AllowedPushHosts =
    [
        "fcm.googleapis.com",                // Chrome, Edge, Brave, Opera, Samsung Internet (Android and desktop)
        "updates.push.services.mozilla.com", // Firefox
        "push.services.mozilla.com",
        "push.apple.com",                    // Safari and iPhone web apps (web.push.apple.com)
        "notify.windows.com",                // Edge on Windows (wns2-*.notify.windows.com)
    ];

    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public bool IsAvailable => settings.Vapid is not null;

    /// <summary>The public key the browser needs to subscribe (base64url), or null.</summary>
    public string? PublicKey => settings.Vapid?.PublicKey;

    /// <summary>
    /// Only real push services are accepted as an address: the server POSTs to this address, so an arbitrary one
    /// would let a logged-in player make the server call any URL (including ones inside the hosting network).
    /// </summary>
    public static bool IsAllowedEndpoint(string? endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || uri.HostNameType != UriHostNameType.Dns)
            return false;
        return AllowedPushHosts.Any(h => uri.Host.Equals(h, StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith("." + h, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<PushOutcome> SendAsync(WebPushDevice device, NotificationMessage message, CancellationToken cancellationToken = default)
    {
        if (settings.Vapid is not { } keys || !IsAllowedEndpoint(device.Endpoint))
            return PushOutcome.Failed;

        var subscription = new PushSubscription { Endpoint = device.Endpoint };
        subscription.SetKey(PushEncryptionKeyName.P256DH, device.P256dh);
        subscription.SetKey(PushEncryptionKeyName.Auth, device.Auth);

        var payload = JsonSerializer.Serialize(new { title = message.Title, body = message.Body, url = message.Url ?? "/", tag = message.Tag });
        var push = new PushMessage(payload) { Urgency = PushMessageUrgency.High, TimeToLive = (int)TimeSpan.FromHours(12).TotalSeconds };

        try
        {
            using var vapid = new VapidAuthentication(keys.PublicKey, keys.PrivateKey) { Subject = Subject() };
            await new PushServiceClient(http).RequestPushMessageDeliveryAsync(subscription, push, vapid, cancellationToken);
            return PushOutcome.Delivered;
        }
        catch (PushServiceClientException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            return PushOutcome.Gone; // the browser unsubscribed or the subscription expired
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Push to device {DeviceId} failed", device.Id);
            return PushOutcome.Failed;
        }
    }

    // Push services want a contact for the sender: the site address, else the sender e-mail.
    private string Subject() =>
        game.Current.QrBaseUrl is { } url && url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? url
        : settings.Current.SmtpFromAddress is { Length: > 0 } mail ? "mailto:" + mail
        : "mailto:pandahunt@example.com";

    public void Dispose() => http.Dispose();
}
