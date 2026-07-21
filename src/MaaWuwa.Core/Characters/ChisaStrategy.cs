using MaaWuwa.Core.Combat;
using MaaWuwa.Core.Input;

namespace MaaWuwa.Core.Characters;

/// <summary>
/// 千咲主 C 首版策略。
///
/// 参考 ok-wuthering-waves/src/char/Chisa.py 的 DPS 分支：先处理声骸/大招，
/// 特殊能量未满时才点共鸣技能；特殊能量满时触发长按 E + 重击连段。
/// 仍保持纯状态驱动：技能亮了才点，协奏满了才切人。
/// </summary>
public sealed class ChisaStrategy : ICharacterStrategy
{
    public string Name => "Chisa";

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

        if (state.EchoReady)
        {
            Console.WriteLine("ChisaStrategy: echo ready -> Q");
            await input.PressAsync(GameKey.Echo, cancellationToken).ConfigureAwait(false);
            await Task.Delay(180, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (state.LiberationReady)
        {
            Console.WriteLine("ChisaStrategy: liberation ready -> R");
            await input.PressAsync(GameKey.Liberation, cancellationToken).ConfigureAwait(false);
            await Task.Delay(300, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (state.ChisaForteFull)
        {
            Console.WriteLine("ChisaStrategy: forte full -> hold E + heavy attack");
            await PerformForteAsync(state, context, input, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (state.ResonanceReady)
        {
            Console.WriteLine("ChisaStrategy: resonance ready -> E");
            await input.PressAsync(GameKey.ResonanceSkill, cancellationToken).ConfigureAwait(false);
            await Task.Delay(180, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (state.ConcertoFull)
        {
            Console.WriteLine("ChisaStrategy: concerto full -> switch next");
            await SwitchNextAsync(state, context, input, cancellationToken).ConfigureAwait(false);
            return;
        }

        await input.PressAsync(GameKey.NormalAttack, cancellationToken).ConfigureAwait(false);
    }

    private static async Task PerformForteAsync(
        CombatState state,
        CombatContext context,
        IGameInputController input,
        CancellationToken cancellationToken)
    {
        // ok Chisa.perform_forte(): send resonance key down_time=1.2, then heavy_attack(3.5).
        await input.HoldAsync(GameKey.ResonanceSkill, TimeSpan.FromMilliseconds(1200), cancellationToken).ConfigureAwait(false);
        await Task.Delay(150, cancellationToken).ConfigureAwait(false);
        await input.HoldAsync(GameKey.NormalAttack, TimeSpan.FromMilliseconds(3500), cancellationToken).ConfigureAwait(false);

        // ok 的 DPS 分支在成功打出 forte 后会 switch_next_char()。
        await SwitchNextAsync(state, context, input, cancellationToken).ConfigureAwait(false);
    }

    private static async Task SwitchNextAsync(
        CombatState state,
        CombatContext context,
        IGameInputController input,
        CancellationToken cancellationToken)
    {
        var nextCharacter = GetNextCharacterKey(state.CurrentSlot);
        await input.PressAsync(nextCharacter, cancellationToken).ConfigureAwait(false);
        context.LastSwitchAt = DateTimeOffset.UtcNow;
        await Task.Delay(300, cancellationToken).ConfigureAwait(false);
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
