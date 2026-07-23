namespace MaaWuwa.Core.Characters;

public sealed class CharacterStrategyFactory : ICharacterStrategyFactory
{
    private readonly IReadOnlyDictionary<string, ICharacterStrategy> _strategies;
    private readonly GenericStrategy _generic;

    public CharacterStrategyFactory(IEnumerable<ICharacterStrategy> strategies, GenericStrategy generic)
    {
        _generic = generic;
        _strategies = strategies.ToDictionary(strategy => strategy.Name, StringComparer.OrdinalIgnoreCase);
    }

    public ICharacterStrategy Create(string? characterName)
    {
        if (string.IsNullOrWhiteSpace(characterName))
        {
            return _generic;
        }

        var normalized = NormalizeCharacterName(characterName);
        return _strategies.TryGetValue(normalized, out var strategy) ? strategy : _generic;
    }

    private static string NormalizeCharacterName(string characterName)
    {
        return characterName.Trim() switch
        {
            "千咲" => "Chisa",
            "chisa" => "Chisa",
            "散华" => "Sanhua",
            "散華" => "Sanhua",
            "sanhua" => "Sanhua",
            "琳奈" => "Linnai",
            "林奈" => "Linnai",
            "linnai" => "Linnai",
            "绯雪" => "Feixue",
            "緋雪" => "Feixue",
            "feixue" => "Feixue",
            _ => characterName.Trim()
        };
    }
}
