namespace MaaWuwa.Core.Combat;

public sealed class CombatContext
{
    public bool InCombat { get; set; }

    public int CurrentSlot { get; set; } = -1;

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset LastEnemySeenAt { get; set; }

    public DateTimeOffset LastSwitchAt { get; set; }

    public DateTimeOffset LastLockAttemptAt { get; set; }

    public int ConsecutiveNoEnemyFrames { get; set; }

    public int FrameIndex { get; set; }
}
