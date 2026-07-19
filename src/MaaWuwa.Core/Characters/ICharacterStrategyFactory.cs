namespace MaaWuwa.Core.Characters;

public interface ICharacterStrategyFactory
{
    ICharacterStrategy Create(string? characterName);
}
