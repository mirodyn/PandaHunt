namespace PandaHunt.Services;

/// <summary>
/// The home-page minigame: click an empty spot to grow a bamboo, the panda walks over and eats it.
/// Feeding her <see cref="Goal"/> bamboos with at most <see cref="Timeout"/> between two of them earns
/// the points set in Administrace (once per player). The browser only draws the game; the counting
/// and timing checks happen here (see <see cref="FeedingStreak"/>), so the point can't just be "called".
/// </summary>
public sealed class PandaFeeding(GameSettingsStore settings, Scoreboard scoreboard)
{
    public const string Source = "nakrmeni-pandy";
    public const int Goal = 5;

    /// <summary>Longest allowed pause between two eaten bamboos. (wwwroot/js/panda.js uses the same 30 s.)</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>The panda needs at least this long to eat one bamboo, so faster reports are ignored.</summary>
    public static readonly TimeSpan MinGap = TimeSpan.FromMilliseconds(600);

    /// <summary>Awards the feeding points unless the player already has them (or they're switched off).</summary>
    public async Task TryAwardAsync(string playerId, CancellationToken cancellationToken = default)
    {
        var points = settings.Current.PandaFeedingBonusPoints;
        if (points > 0)
            await scoreboard.AwardOnceAsync(playerId, points, "Za nakrmení pandy", Source, cancellationToken);
    }
}

/// <summary>One page visit's feeding progress (kept per browser tab, on the server).</summary>
public sealed class FeedingStreak(TimeProvider time)
{
    private DateTimeOffset? lastEaten;

    public int Count { get; private set; }

    /// <summary>Counts one eaten bamboo. Returns true when this one completed the streak (which then starts over).</summary>
    public bool Eaten()
    {
        var now = time.GetUtcNow();
        if (lastEaten is { } last)
        {
            if (now - last < PandaFeeding.MinGap)
                return false; // too fast to be real; ignore
            if (now - last > PandaFeeding.Timeout)
                Count = 0;
        }

        lastEaten = now;
        Count++;
        if (Count < PandaFeeding.Goal)
            return false;

        Count = 0;
        lastEaten = null;
        return true;
    }
}
