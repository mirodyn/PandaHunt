namespace PandaHunt.Services;

/// <summary>What a task page knows about its task and the player viewing it. Handed out by TaskShell.</summary>
public sealed class TaskContext(HuntTasks tasks, HuntTaskView? huntTask, string playerId, bool isCompleted)
{
    /// <summary>The task linked to this page. Null only while an admin previews a page no task links to yet.</summary>
    public HuntTaskView? HuntTask { get; } = huntTask;

    public string PlayerId { get; } = playerId;

    /// <summary>Has this player already finished the task?</summary>
    public bool IsCompleted { get; private set; } = isCompleted;

    /// <summary>Call when the player solves the task: their tile on /lov gets the checkmark.</summary>
    public async Task CompleteAsync()
    {
        if (HuntTask is null || IsCompleted)
            return;

        await tasks.MarkCompletedAsync(HuntTask.Id, PlayerId);
        IsCompleted = true;
    }
}
