namespace MaaWuwa.Core.Recognition;

public sealed record SkillState
{
    public bool ResonanceReady { get; init; }

    public bool LiberationReady { get; init; }

    public bool EchoReady { get; init; }

    public bool ConcertoFull { get; init; }

    public bool ChisaForteFull { get; init; }

    public bool ChisaForteVisible { get; init; }

    public double ChisaForteFullScore { get; init; }

    public double ChisaForteNotFullScore { get; init; }

    public double ConcertoRatio { get; init; }
}
