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
        if (!state.HasTarget)
        {
            return;
        }

        // State-driven only: do not rotate by time. Unknown characters should not
        // auto-switch until their role/concerto ROI is calibrated; false concerto
        // positives caused repeated switching to slot 2.
        // 未识别到当前角色时不释放大招。绯雪二阶段槽位经常识别为 -1，
        // 若 Generic 看到 R 亮就按，会绕过角色策略并提前打出 R2。
        if (state.EchoReady)
        {
            await input.PressAsync(GameKey.Echo, cancellationToken).ConfigureAwait(false);
            await Task.Delay(180, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (state.ResonanceReady)
        {
            await input.PressAsync(GameKey.ResonanceSkill, cancellationToken).ConfigureAwait(false);
            await Task.Delay(180, cancellationToken).ConfigureAwait(false);
            return;
        }

        await input.PressAsync(GameKey.NormalAttack, cancellationToken).ConfigureAwait(false);
    }
}
