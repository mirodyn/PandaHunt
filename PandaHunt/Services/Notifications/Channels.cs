using Microsoft.EntityFrameworkCore;
using PandaHunt.Data;

namespace PandaHunt.Services.Notifications;

/// <summary>What gets sent. <see cref="Url"/> is a path on this site ("/lov") that opens when the notification is clicked.</summary>
public sealed record NotificationMessage(string Title, string Body, string? Url = null, string? Tag = null);

/// <summary>One login and the ways it wants to be reached.</summary>
public sealed record Recipient(
    string UserId, string UserName, string? Email, string? Phone, bool NotifyByEmail, bool NotifyBySms,
    IReadOnlyList<WebPushDevice> Devices);

public sealed record ChannelResult(int Sent, int Failed);

/// <summary>
/// One way of reaching a player (e-mail, browser push, SMS). To add a channel, implement this and register it
/// in Program.cs; <see cref="Notifier"/> then uses it for everybody whose <see cref="Wants"/> says yes.
/// </summary>
public interface INotificationChannel
{
    /// <summary>Short Czech name for the admin's send report, e.g. "e-mail".</summary>
    string Name { get; }

    /// <summary>The channel is set up on the server (SMTP filled in, keys exist, SMS gateway connected...).</summary>
    bool IsAvailable { get; }

    /// <summary>The recipient chose this channel.</summary>
    bool Wants(Recipient recipient);

    Task<ChannelResult> SendAsync(Recipient recipient, NotificationMessage message, CancellationToken cancellationToken);
}

public sealed class EmailChannel(EmailSender sender, GameSettingsStore game) : INotificationChannel
{
    public string Name => "e-mail";

    public bool IsAvailable => sender.IsConfigured;

    public bool Wants(Recipient recipient) => recipient.NotifyByEmail && !string.IsNullOrWhiteSpace(recipient.Email);

    public async Task<ChannelResult> SendAsync(Recipient recipient, NotificationMessage message, CancellationToken cancellationToken)
    {
        var baseUrl = game.Current.QrBaseUrl;
        var text = message.Body;
        if (baseUrl is not null)
        {
            // The mail is read outside the app, so links need the full address.
            var link = baseUrl + (message.Url is { } path && path.StartsWith('/') ? path : "/");
            text += $"\n\n{link}\n\n--\nTohle oznámení si vypneš na {baseUrl}/oznameni";
        }
        else
        {
            text += "\n\n--\nTohle oznámení si vypneš v PandaHunt IV v nabídce Oznámení.";
        }

        return await sender.SendAsync(recipient.Email!, message.Title, text, cancellationToken) is null
            ? new ChannelResult(1, 0)
            : new ChannelResult(0, 1);
    }
}

public sealed class WebPushChannel(WebPushSender sender, IDbContextFactory<AppDbContext> dbFactory) : INotificationChannel
{
    public string Name => "prohlížeč";

    public bool IsAvailable => sender.IsAvailable;

    public bool Wants(Recipient recipient) => recipient.Devices.Count > 0;

    public async Task<ChannelResult> SendAsync(Recipient recipient, NotificationMessage message, CancellationToken cancellationToken)
    {
        int sent = 0, failed = 0;
        var gone = new List<int>();
        foreach (var device in recipient.Devices)
        {
            switch (await sender.SendAsync(device, message, cancellationToken))
            {
                case PushOutcome.Delivered: sent++; break;
                case PushOutcome.Gone: gone.Add(device.Id); break;
                default: failed++; break;
            }
        }

        if (gone.Count > 0)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            await db.WebPushDevices.Where(d => gone.Contains(d.Id)).ExecuteDeleteAsync(cancellationToken);
        }

        return new ChannelResult(sent, failed);
    }
}

/// <summary>
/// Prepared, not built: needs an SMS gateway (account, API key, price per message). Until then it is never
/// available and nobody can choose it. To build it: fill in <see cref="SendAsync"/>, return true from
/// <see cref="IsAvailable"/> once the gateway is configured, and enable the SMS card on /oznameni.
/// </summary>
public sealed class SmsChannel : INotificationChannel
{
    public string Name => "SMS";

    public bool IsAvailable => false;

    public bool Wants(Recipient recipient) => false;

    public Task<ChannelResult> SendAsync(Recipient recipient, NotificationMessage message, CancellationToken cancellationToken) =>
        throw new NotSupportedException("SMS zatím není implementované.");
}
