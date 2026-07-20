namespace MaaWuwa.Core.Daily;

public sealed record DailyStatus(int UsedStamina, int ActivityPoints)
{
    public bool IsRewardReady(int requiredActivityPoints) => ActivityPoints >= requiredActivityPoints;
}

public enum DailyStep
{
    Start,
    OpenDaily,
    ReadDailyStatus,
    Decide,
    OpenSimulation,
    EnterSimulation,
    WaitDomain,
    StartDomainChallenge,
    Combat,
    ClaimDomainReward,
    ContinueOrFinishFarm,
    ClaimDaily,
    ClaimMail,
    ClaimBattlePass,
    CheckWeeklyGarden,
    Completed,
    Failed
}

public sealed class DailyRunContext
{
    public DailyStep Step { get; set; } = DailyStep.Start;

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset StepStartedAt { get; set; } = DateTimeOffset.UtcNow;

    public int Tick { get; set; }

    public int UsedStamina { get; set; }

    public int ActivityPoints { get; set; }

    public int? UsedStaminaTextCenterY { get; set; }

    public int RequiredUse { get; set; }

    public int FarmRuns { get; set; }

    public bool HasReadStatus { get; set; }

    public string? LastMessage { get; set; }
}
