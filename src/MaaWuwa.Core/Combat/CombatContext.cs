namespace MaaWuwa.Core.Combat;

public sealed class CombatContext
{
    public bool InCombat { get; set; }

    public int CurrentSlot { get; set; } = -1;

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset LastEnemySeenAt { get; set; }

    public DateTimeOffset LastSwitchAt { get; set; }

    public DateTimeOffset LastLockAttemptAt { get; set; }

    public bool FeixueFirstForteReleased { get; set; }

    public DateTimeOffset FeixueFirstForteReleasedAt { get; set; }

    public DateTimeOffset FeixueFirstLiberationProbeAt { get; set; }

    public bool FeixueSecondForm { get; set; }

    public bool FeixueSecondForteComboDone { get; set; }

    public bool FeixueThirdForteReleased { get; set; }

    public int FeixueSecondFormBasicAttackCount { get; set; }

    public int PendingSwitchTargetSlot { get; set; }

    public DateTimeOffset PendingSwitchRequestedAt { get; set; }

    public DateTimeOffset Slot1DisabledUntil { get; set; }

    public DateTimeOffset Slot2DisabledUntil { get; set; }

    public DateTimeOffset Slot3DisabledUntil { get; set; }

    public int ConsecutiveNoEnemyFrames { get; set; }

    public bool IsSlotDisabled(int slot)
    {
        var now = DateTimeOffset.UtcNow;
        return slot switch
        {
            1 => Slot1DisabledUntil > now,
            2 => Slot2DisabledUntil > now,
            3 => Slot3DisabledUntil > now,
            _ => true
        };
    }

    public void DisableSlot(int slot, TimeSpan duration)
    {
        var until = DateTimeOffset.UtcNow + duration;
        switch (slot)
        {
            case 1:
                Slot1DisabledUntil = until;
                break;
            case 2:
                Slot2DisabledUntil = until;
                break;
            case 3:
                Slot3DisabledUntil = until;
                break;
        }
    }

    public void RecordSwitchAttempt(int targetSlot)
    {
        PendingSwitchTargetSlot = targetSlot;
        PendingSwitchRequestedAt = DateTimeOffset.UtcNow;
    }

    public int FrameIndex { get; set; }
}
