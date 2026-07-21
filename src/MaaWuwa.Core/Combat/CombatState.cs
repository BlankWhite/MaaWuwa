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

    public int CurrentSlot { get; init; } = -1;

    public string? CharacterName { get; init; }
}
