using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using PandaHunt.Data;

namespace PandaHunt.Services.Notifications;

/// <summary>Sends e-mails through the SMTP account set on /admin. Methods return a Czech error, or null on success.</summary>
public sealed class EmailSender(NotificationSettingsStore settings, ILogger<EmailSender> logger)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    public bool IsConfigured => settings.IsSmtpConfigured;

    public Task<string?> SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default) =>
        SendAsync(settings.Smtp, to, subject, body, cancellationToken);

    public async Task<string?> SendAsync(
        SmtpConfig? smtp, string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (smtp is null)
            return "E-maily zatím nejsou nastavené (admin musí vyplnit SMTP).";

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(smtp.FromName, smtp.FromAddress));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = body };

            using var client = new SmtpClient { Timeout = (int)Timeout.TotalMilliseconds };
            var security = smtp.Security switch
            {
                SmtpSecurity.Ssl => SecureSocketOptions.SslOnConnect,
                SmtpSecurity.StartTls => SecureSocketOptions.StartTls,
                _ => SecureSocketOptions.None,
            };
            await client.ConnectAsync(smtp.Host, smtp.Port, security, cancellationToken);
            if (!string.IsNullOrEmpty(smtp.User))
                await client.AuthenticateAsync(smtp.User, smtp.Password ?? "", cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Sending an e-mail through {Host}:{Port} failed", smtp.Host, smtp.Port);
            return ex switch
            {
                AuthenticationException => "Server odmítl přihlášení (špatné jméno nebo heslo).",
                SslHandshakeException => "Nepovedlo se zabezpečené spojení se serverem (zkus jiné zabezpečení nebo port).",
                SmtpCommandException => $"Server e-mail odmítl: {ex.Message}",
                OperationCanceledException or TimeoutException => "Server neodpověděl včas.",
                _ => $"E-mail se nepodařilo odeslat: {ex.Message}",
            };
        }
    }
}
