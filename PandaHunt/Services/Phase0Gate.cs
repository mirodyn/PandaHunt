using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using PandaHunt.Data;

namespace PandaHunt.Services;

public enum CodeCheckResult
{
    Correct,
    Wrong,
    TooManyAttempts,
}

/// <summary>
/// Phase 0: before registering, a visitor has to enter a code found in the real world.
/// Enabled/code come from <see cref="GameSettings"/> (set on the admin page).
/// Solving it is remembered in a signed (tamper-proof) cookie that is tied to the current code,
/// so changing the code invalidates old cookies.
/// </summary>
public sealed class Phase0Gate
{
    public const string CookieName = "PandaHunt.Phase0";

    // Wrong guesses allowed per IP address within the window, to stop brute-forcing short PINs.
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan CookieLifetime = TimeSpan.FromDays(90);

    private readonly GameSettingsStore settings;
    private readonly IDataProtector protector;
    private readonly IMemoryCache cache;
    private readonly TimeProvider time;

    public Phase0Gate(GameSettingsStore settings, IDataProtectionProvider dataProtection, IMemoryCache cache, TimeProvider time)
    {
        this.settings = settings;
        protector = dataProtection.CreateProtector("PandaHunt.Phase0.v1");
        this.cache = cache;
        this.time = time;
    }

    private string Code => GameSettings.NormalizeCode(settings.Current.Phase0Code);

    public bool IsEnabled => settings.Current.Phase0Enabled;

    /// <summary>Number of characters in the code (the code page shows one box per character).</summary>
    public int CodeLength => Code.Length;

    /// <summary>True when the code is digits only, so phones can show the number pad.</summary>
    public bool IsNumericCode => Code.Length > 0 && Code.All(char.IsAsciiDigit);

    /// <summary>True when the visitor may continue to registration (phase 0 is off, or they solved it).</summary>
    public bool HasPassed(HttpContext context)
    {
        if (!IsEnabled)
            return true;
        if (!context.Request.Cookies.TryGetValue(CookieName, out var cookie) || string.IsNullOrEmpty(cookie))
            return false;

        try
        {
            return protector.Unprotect(cookie) == ExpectedCookiePayload();
        }
        catch (CryptographicException)
        {
            return false; // forged, corrupted, or signed with old keys
        }
    }

    public CodeCheckResult CheckCode(HttpContext context, string? input)
    {
        var attemptsKey = "phase0-attempts:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        var failed = cache.GetOrCreate(attemptsKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = AttemptWindow;
            return new FailedAttempts();
        })!;

        if (failed.Count >= MaxFailedAttempts)
            return CodeCheckResult.TooManyAttempts;

        var expected = Code;
        if (expected.Length == 0 || GameSettings.NormalizeCode(input) != expected)
        {
            Interlocked.Increment(ref failed.Count);
            return CodeCheckResult.Wrong;
        }

        context.Response.Cookies.Append(CookieName, protector.Protect(ExpectedCookiePayload()), new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Expires = time.GetUtcNow().Add(CookieLifetime),
        });
        return CodeCheckResult.Correct;
    }

    private string ExpectedCookiePayload()
    {
        var codeHash = SHA256.HashData(Encoding.UTF8.GetBytes(Code));
        return "passed:" + Convert.ToHexString(codeHash, 0, 8);
    }

    private sealed class FailedAttempts
    {
        public int Count;
    }
}
