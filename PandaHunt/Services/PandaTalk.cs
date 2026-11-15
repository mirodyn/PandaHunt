namespace PandaHunt.Services;

/// <summary>
/// What the panda on the home page says when someone clicks on her. ===== Edit the texts here. =====
/// The first <see cref="PatienceClicks"/> clicks get a random <see cref="Phrases"/> line. After that every click
/// moves one step along <see cref="AnnoyedChain"/>; reaching its last line costs the player points (once, see
/// <see cref="PandaPestering"/>). Leaving her alone for <see cref="CalmDown"/> starts the count over.
/// </summary>
public static class PandaTalk
{
    public const int PatienceClicks = 6;

    public static readonly TimeSpan CalmDown = TimeSpan.FromSeconds(30);

    /// <summary>Random chatter (never the same line twice in a row).</summary>
    public static readonly IReadOnlyList<string> Phrases =
    [
        "Ahoj!",
        "Není tady někde bambus?",
        "Jestli mě hledáš, jsem tady!",
        "Bambus je nejlepší jídlo na světě.",
        "*pandí zvuky*",
        "Pst! Ještě mě nikdo nenašel.",
        "Nejsi ty výrostek?",
        "Dneska je tu lidí ...",
        "Budu pod kořenem.",
        "Klidně mi říkej Zdeno.",
    ];

    /// <summary>
    /// One line per click after the first <see cref="PatienceClicks"/>. The last one comes with the point deduction.
    /// Add or remove lines freely.
    /// </summary>
    public static readonly IReadOnlyList<string> AnnoyedChain =
    [
        "Už stačilo ne?",
        "Nech mě!",
        "Říkám, dost!",
        "Zmrde!!!",
        "Po-móóc!",
        "Ještě jednou a vezmu ti bod!",
        "Neriskuj!",
        "Fakt. Poslední varování!",
        "TAK A MÁŠ TO TY SVINĚ!",
    ];
}

/// <summary>Counts one page visit's clicks on the panda and picks what she says (kept per browser tab, on the server).</summary>
public sealed class PesterStreak(TimeProvider time)
{
    private DateTimeOffset? lastClick;
    private int clicks;
    private int lastPhrase = -1;

    /// <returns>The line to show, whether she's annoyed, and whether this click finished the whole chain.</returns>
    public (string Text, bool Annoyed, bool ChainFinished) Clicked()
    {
        var now = time.GetUtcNow();
        if (lastClick is { } last && now - last > PandaTalk.CalmDown)
            clicks = 0;
        lastClick = now;
        clicks++;

        var step = clicks - PandaTalk.PatienceClicks - 1;
        if (step < 0 || PandaTalk.AnnoyedChain.Count == 0)
            return (RandomPhrase(), false, false);

        var finished = step >= PandaTalk.AnnoyedChain.Count - 1;
        if (finished)
            clicks = 0; // she's said her piece; the next click is friendly chatter again
        return (PandaTalk.AnnoyedChain[Math.Min(step, PandaTalk.AnnoyedChain.Count - 1)], true, finished);
    }

    private string RandomPhrase()
    {
        var phrases = PandaTalk.Phrases;
        var index = Random.Shared.Next(phrases.Count);
        if (index == lastPhrase && phrases.Count > 1)
            index = (index + 1 + Random.Shared.Next(phrases.Count - 1)) % phrases.Count;
        lastPhrase = index;
        return phrases[index];
    }
}

/// <summary>The point deduction for going through the panda's whole <see cref="PandaTalk.AnnoyedChain"/>.</summary>
public sealed class PandaPestering(GameSettingsStore settings, Scoreboard scoreboard)
{
    public const string Source = "otravovani-pandy";

    /// <summary>Takes the points set in Administrace, at most once per player (0 = no deduction).</summary>
    public async Task TryPenalizeAsync(string playerId, CancellationToken cancellationToken = default)
    {
        var points = settings.Current.PandaPesteringPenaltyPoints;
        if (points > 0)
            await scoreboard.AwardOnceAsync(playerId, -points, "Za nasrání pandy", Source, cancellationToken);
    }
}
