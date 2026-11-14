using System.Globalization;
using PandaHunt.Data;

namespace PandaHunt.Services;

/// <summary>
/// Answers "is X available right now?" from the date/time switches in <see cref="GameSettings"/>,
/// and converts between stored moments and the local time (Europe/Prague by default) shown to people.
/// </summary>
public sealed class GameSchedule(GameSettingsStore settings, TimeProvider time, IConfiguration configuration)
{
    private static readonly CultureInfo Czech = CultureInfo.GetCultureInfo("cs-CZ");

    private readonly TimeZoneInfo zone = FindZone(configuration["TimeZone"] ?? "Europe/Prague");

    private GameSettings Current => settings.Current;

    public DateTimeOffset Now => time.GetUtcNow();

    public DateTimeOffset? RegistrationOpensAt => Current.RegistrationOpensAt;
    public DateTimeOffset? HuntStartsAt => Current.HuntStartsAt;

    public bool IsRegistrationOpen => IsWithin(Current.RegistrationOpensAt, Current.RegistrationClosesAt);

    public bool IsRegistrationClosed => Current.RegistrationClosesAt is { } closes && Now >= closes;

    public bool IsHuntOpen => Current.HuntStartsAt is not null && IsWithin(Current.HuntStartsAt, Current.HuntEndsAt);

    public bool IsHuntOver => Current.HuntEndsAt is { } ends && Now >= ends;

    /// <summary>True when now is in [from, to). Null bounds are open-ended.</summary>
    public bool IsWithin(DateTimeOffset? from, DateTimeOffset? to) =>
        (from is null || Now >= from) && (to is null || Now < to);

    /// <summary>Formats a moment for players, e.g. "24. 12. 2026 v 18:00".</summary>
    public string Format(DateTimeOffset value) =>
        TimeZoneInfo.ConvertTime(value, zone).ToString("d. M. yyyy 'v' H:mm", Czech);

    /// <summary>Formats a UTC timestamp read from the database.</summary>
    public string Format(DateTime utc) =>
        Format(new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)));

    /// <summary>Formats just the date, e.g. "24. 12. 2026".</summary>
    public string FormatDate(DateTimeOffset value) =>
        TimeZoneInfo.ConvertTime(value, zone).ToString("d. M. yyyy", Czech);

    /// <inheritdoc cref="FormatDate(DateTimeOffset)"/>
    public string FormatDate(DateTime utc) =>
        FormatDate(new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)));

    /// <summary>Current local time, rounded down to the minute (for "now" buttons on the admin page).</summary>
    public DateTime LocalNow
    {
        get
        {
            var local = TimeZoneInfo.ConvertTime(Now, zone).DateTime;
            return new DateTime(local.Year, local.Month, local.Day, local.Hour, local.Minute, 0);
        }
    }

    /// <summary>Stored moment → local wall-clock time for editing.</summary>
    public DateTime? ToLocal(DateTimeOffset? value) =>
        value is { } v ? TimeZoneInfo.ConvertTime(v, zone).DateTime : null;

    /// <summary>Local wall-clock time from the admin page → moment to store (handles summer/winter time).</summary>
    public DateTimeOffset? FromLocal(DateTime? local)
    {
        if (local is not { } value)
            return null;
        var unspecified = DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, zone.GetUtcOffset(unspecified));
    }

    private static TimeZoneInfo FindZone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.Local; }
    }
}
