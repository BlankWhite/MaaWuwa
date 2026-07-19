namespace MaaWuwa.Core.Recognition;

public sealed class CharacterRecognizer
{
    private readonly IReadOnlyList<string> _team;

    public CharacterRecognizer(IReadOnlyList<string> team)
    {
        _team = team;
    }

    public string? Detect(int currentSlot)
    {
        if (currentSlot <= 0 || currentSlot > _team.Count)
        {
            return null;
        }

        return _team[currentSlot - 1];
    }
}
