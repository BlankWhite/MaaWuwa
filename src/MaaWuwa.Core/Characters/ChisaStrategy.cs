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
            // 千咲刚由绯雪 R2 满协奏入场时，协奏会保持满值一段时间。
            // 这里不能立刻按协奏切回绯雪，否则千咲完全没有出手机会。
            Console.WriteLine("ChisaStrategy: concerto full but no priority action -> keep Chisa on field and attack");
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
        var nextSlot = GetNextAliveSlot(state, context);
        if (nextSlot == 0)
        {
            Console.WriteLine("ChisaStrategy: no alive switch target, stay current");
            return;
        }

        var nextCharacter = ToSwitchKey(nextSlot);
        Console.WriteLine($"ChisaStrategy: switch target -> slot {nextSlot}");
        await input.PressAsync(nextCharacter, cancellationToken).ConfigureAwait(false);
        context.RecordSwitchAttempt(nextSlot);
        context.LastSwitchAt = DateTimeOffset.UtcNow;
        await Task.Delay(300, cancellationToken).ConfigureAwait(false);
    }

    private static int GetNextAliveSlot(CombatState state, CombatContext context)
    {
        var order = state.CurrentSlot switch
        {
            1 => new[] {2, 3},
            2 => new[] {3, 1},
            3 => new[] {1, 2},
            _ => new[] {1, 2, 3}
        };

        return order.FirstOrDefault(slot => slot != state.CurrentSlot && IsSlotAlive(state, slot) && !context.IsSlotDisabled(slot));
    }

    private static bool IsSlotAlive(CombatState state, int slot)
    {
        return slot switch
        {
            1 => state.Slot1Alive,
            2 => state.Slot2Alive,
            3 => state.Slot3Alive,
            _ => false
        };
    }

    private static GameKey ToSwitchKey(int slot)
    {
        return slot switch
        {
            1 => GameKey.SwitchCharacter1,
            2 => GameKey.SwitchCharacter2,
            3 => GameKey.SwitchCharacter3,
            _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, null)
        };
    }
}
