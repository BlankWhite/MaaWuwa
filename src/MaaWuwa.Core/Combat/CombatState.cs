namespace MaaWuwa.Core.Combat;

public sealed record CombatState
{
    public bool EnemyFound { get; init; }

    public bool HasTarget { get; init; }

    public bool BossFound { get; init; }

    public bool ResonanceReady { get; init; }

    public bool LiberationReady { get; init; }

    public bool EchoReady { get; init; }

    public bool ConcertoFull { get; init; }

    public bool ChisaForteFull { get; init; }

    public bool ChisaForteVisible { get; init; }

    public double ChisaForteFullScore { get; init; }

    public double ChisaForteNotFullScore { get; init; }

    public double ConcertoRatio { get; init; }

    public int FeixueForteStage { get; init; }

    public bool FeixueForteVisible { get; init; }

    public double FeixueForte1FullScore { get; init; }

    public double FeixueForte1NotFullScore { get; init; }

    public double FeixueForte2FullScore { get; init; }

    public double FeixueForte2NotFullScore { get; init; }

    public double FeixueForte3FullScore { get; init; }

    public bool LinnaiForteVisible { get; init; }

    public bool LinnaiForteFull { get; init; }

    public bool LinnaiAcceleratedForteVisible { get; init; }

    public bool LinnaiAcceleratedForteFull { get; init; }

    public double LinnaiForteFullScore { get; init; }

    public double LinnaiForteNotFullScore { get; init; }

    public double LinnaiAcceleratedForteFullScore { get; init; }

    public double LinnaiAcceleratedForteNotFullScore { get; init; }

    public int CurrentSlot { get; init; } = -1;

    public bool Slot1Alive { get; init; } = true;

    public bool Slot2Alive { get; init; } = true;

    public bool Slot3Alive { get; init; } = true;

    public string? CharacterName { get; init; }
}
