using Microsoft.AspNetCore.DataProtection;

namespace PandaHunt.Services.Notifications;

/// <summary>
/// Encrypts the secrets kept in the database (SMTP password, push private key) with the app's data-protection
/// keys, so a copy of the database alone doesn't reveal them. Those keys live next to the database on the
/// volume, and a hard reset wipes both together.
/// </summary>
public sealed class SecretProtector(IDataProtectionProvider provider)
{
    private readonly IDataProtector protector = provider.CreateProtector("PandaHunt.NotificationSecrets.v1");

    public string Protect(string secret) => protector.Protect(secret);

    /// <summary>The secret, or null when there is none or it can't be read (e.g. the keys were replaced).</summary>
    public string? Unprotect(string? protectedSecret)
    {
        if (string.IsNullOrEmpty(protectedSecret))
            return null;

        try
        {
            return protector.Unprotect(protectedSecret);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }
}
