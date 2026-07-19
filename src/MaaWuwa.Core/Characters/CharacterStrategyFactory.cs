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

        return _strategies.TryGetValue(characterName, out var strategy) ? strategy : _generic;
    }
}
