namespace PandaHunt.Data;

/// <summary>One task of the hunt (one per month, named after the month). Players see all of them on /lov.</summary>
public class HuntTask
{
    public const int MaxNameLength = 40;
    public const int MaxTypeLength = 30;
    public const int MaxLinkLength = 300;

    public int Id { get; set; }

    public required string Name { get; set; }

    /// <summary>Key of a <see cref="Services.TaskTypes"/> entry; decides the icon.</summary>
    public required string Type { get; set; }

    /// <summary>When the task becomes active. Before that (or while null) it is shown as planned.</summary>
    public DateTimeOffset? OpensAt { get; set; }

    /// <summary>
    /// Where the tile on /lov leads once the task is active: a task page in this app (e.g. "/lov/leden",
    /// see Components/Pages/Tasks) or an external https:// address. Null = no page yet.
    /// </summary>
    public string? Link { get; set; }
}
