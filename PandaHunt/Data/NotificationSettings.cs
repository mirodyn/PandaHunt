namespace PandaHunt.Data;

public enum SmtpSecurity
{
    /// <summary>Plain, unencrypted (only for a local test server).</summary>
    None,

    /// <summary>Starts plain and switches to TLS (usually port 587).</summary>
    StartTls,

    /// <summary>TLS from the first byte (usually port 465).</summary>
    Ssl,
}

/// <summary>
/// How the app sends notifications: the SMTP account for e-mails (edited on /admin → Oznámení) and the VAPID
/// key pair that identifies the server to browsers' push services. Exactly one row (Id = 1). Secrets are stored
/// encrypted with the app's data-protection keys (<see cref="Services.SecretProtector"/>).
/// </summary>
public class NotificationSettings
{
    public const int SingletonId = 1;
    public const int MaxTextLength = 200;

    public int Id { get; set; } = SingletonId;

    public string? SmtpHost { get; set; }

    public int SmtpPort { get; set; } = 587;

    public SmtpSecurity SmtpSecurity { get; set; } = SmtpSecurity.StartTls;

    /// <summary>Empty = the server needs no login.</summary>
    public string? SmtpUser { get; set; }

    /// <summary>The SMTP password, encrypted. Never shown again after saving.</summary>
    public string? SmtpPasswordProtected { get; set; }

    /// <summary>The sender address e-mails come from.</summary>
    public string? SmtpFromAddress { get; set; }

    /// <summary>The sender name e-mails come from, e.g. "PandaHunt".</summary>
    public string? SmtpFromName { get; set; }

    /// <summary>Public half of the push key pair (base64url). Generated on first start.</summary>
    public string? VapidPublicKey { get; set; }

    /// <summary>Private half of the push key pair (base64url), encrypted.</summary>
    public string? VapidPrivateKeyProtected { get; set; }

    public NotificationSettings Clone() => (NotificationSettings)MemberwiseClone();
}
