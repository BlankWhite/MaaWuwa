using MaaWuwa.Core.Combat;
using MaaWuwa.Core.Configuration;
using MaaWuwa.Core.Input;

namespace MaaWuwa.Core.Characters;

public sealed class GenericStrategy : ICharacterStrategy
{
    private readonly AutoCombatOptions _options;

    public GenericStrategy(AutoCombatOptions options)
    {
        _options = options;
    }

    public string Name => "Generic";

    public async Task PerformAsync(
        CombatState state,
        CombatContext context,
        IGameInputController input,
        CancellationToken cancellationToken)
    {
        if (state.LiberationReady)
        {
            await input.PressAsync(GameKey.Liberation, cancellationToken).ConfigureAwait(false);
            await Task.Delay(300, cancellationToken).ConfigureAwait(false);
        }

        if (state.ResonanceReady)
        {
            await input.PressAsync(GameKey.ResonanceSkill, cancellationToken).ConfigureAwait(false);
            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        }

        if (state.EchoReady)
        {
            await input.PressAsync(GameKey.Echo, cancellationToken).ConfigureAwait(false);
            await Task.Delay(200, cancellationToken).ConfigureAwait(false);
        }

        await input.HoldAsync(
            GameKey.NormalAttack,
            TimeSpan.FromMilliseconds(_options.NormalAttackHoldMilliseconds),
            cancellationToken).ConfigureAwait(false);

        if (ShouldSwitch(state, context))
        {
            var nextCharacter = GetNextCharacterKey(state.CurrentSlot);
            await input.PressAsync(nextCharacter, cancellationToken).ConfigureAwait(false);
            context.LastSwitchAt = DateTimeOffset.UtcNow;
        }
    }

    private bool ShouldSwitch(CombatState state, CombatContext context)
    {
        if (state.ConcertoFull)
        {
            return true;
        }

        return DateTimeOffset.UtcNow - context.LastSwitchAt >= TimeSpan.FromMilliseconds(_options.SwitchIntervalMilliseconds);
    }

    private static GameKey GetNextCharacterKey(int currentSlot)
    {
        return currentSlot switch
        {
            1 => GameKey.SwitchCharacter2,
            2 => GameKey.SwitchCharacter3,
            3 => GameKey.SwitchCharacter1,
            _ => GameKey.SwitchCharacter2
        };
    }
}
