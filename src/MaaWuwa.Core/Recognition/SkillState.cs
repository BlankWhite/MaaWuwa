namespace MaaWuwa.Core.Recognition;

public sealed record SkillState
{
    public bool ResonanceReady { get; init; }

    public bool LiberationReady { get; init; }

    public bool EchoReady { get; init; }

    public bool ConcertoFull { get; init; }
}
