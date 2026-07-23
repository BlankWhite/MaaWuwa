using MaaWuwa.Core.Combat;
using MaaWuwa.Core.Input;

namespace MaaWuwa.Core.Characters;

/// <summary>
/// 琳奈二号位首版速切策略。
///
/// 参考 ok-wuthering-waves/src/char/Linnai.py：
/// - 入场后先普攻/蓄力攒回路；
/// - 可用时放声骸、共鸣技能；
/// - 大招可用时按 R；
/// - 短轴结束后切到下一个主输出。
///
/// 已接入普通回路/加速回路满与未满模板。当前仍缺少 ok 中的目标状态/颜色条细分模板，
/// 因此加速后的 E/R 释放仍以技能亮和协奏状态作为门控。
/// </summary>
public sealed class LinnaiStrategy : ICharacterStrategy
{
    public string Name => "Linnai";

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

        Console.WriteLine($"LinnaiStrategy: start rotation E={state.ResonanceReady} R={state.LiberationReady} Q={state.EchoReady} con={state.ConcertoFull} normal={state.LinnaiForteVisible}/{state.LinnaiForteFull} accel={state.LinnaiAcceleratedForteVisible}/{state.LinnaiAcceleratedForteFull} score={state.LinnaiForteFullScore:F3}/{state.LinnaiForteNotFullScore:F3}/{state.LinnaiAcceleratedForteFullScore:F3}/{state.LinnaiAcceleratedForteNotFullScore:F3}");

        if (state.EchoReady)
        {
            Console.WriteLine("LinnaiStrategy: echo ready -> Q");
            await input.PressAsync(GameKey.Echo, cancellationToken).ConfigureAwait(false);
            await Task.Delay(160, cancellationToken).ConfigureAwait(false);
        }

        if (state.LinnaiAcceleratedForteVisible)
        {
            await PerformAcceleratedAsync(state, context, input, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (state.LinnaiForteFull)
        {
            // ok charge_heavy(): wait_until is_mouse_forte_full -> mouse_down until not full。
            Console.WriteLine("LinnaiStrategy: normal forte full -> hold heavy until accelerated state");
            await input.HoldAsync(GameKey.NormalAttack, TimeSpan.FromMilliseconds(1600), cancellationToken).ConfigureAwait(false);
            await Task.Delay(300, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (state.LinnaiForteVisible)
        {
            Console.WriteLine("LinnaiStrategy: normal forte not full -> build with normals/E/R");
            await BuildNormalForteAsync(state, input, cancellationToken).ConfigureAwait(false);
            if (state.ConcertoFull)
            {
                await SwitchNextAsync(state, context, input, cancellationToken).ConfigureAwait(false);
            }
            return;
        }

        Console.WriteLine("LinnaiStrategy: forte not visible -> fallback build");
        await BuildNormalForteAsync(state, input, cancellationToken).ConfigureAwait(false);
        if (state.ConcertoFull)
        {
            await SwitchNextAsync(state, context, input, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task BuildNormalForteAsync(
        CombatState state,
        IGameInputController input,
        CancellationToken cancellationToken)
    {
        await QuickNormalAsync(input, cancellationToken, count: 6).ConfigureAwait(false);

        if (!state.ConcertoFull && state.LiberationReady)
        {
            Console.WriteLine("LinnaiStrategy: liberation ready while building -> R");
            await input.PressAsync(GameKey.Liberation, cancellationToken).ConfigureAwait(false);
            await Task.Delay(260, cancellationToken).ConfigureAwait(false);
        }

        if (state.ResonanceReady)
        {
            Console.WriteLine("LinnaiStrategy: resonance ready while building -> E");
            await input.PressAsync(GameKey.ResonanceSkill, cancellationToken).ConfigureAwait(false);
            await Task.Delay(200, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task PerformAcceleratedAsync(
        CombatState state,
        CombatContext context,
        IGameInputController input,
        CancellationToken cancellationToken)
    {
        if (!state.LinnaiAcceleratedForteFull)
        {
            Console.WriteLine("LinnaiStrategy: accelerated forte not full -> quick normals/E");
            await QuickNormalAsync(input, cancellationToken, count: 5).ConfigureAwait(false);
            if (state.ResonanceReady)
            {
                Console.WriteLine("LinnaiStrategy: accelerated resonance ready -> E");
                await input.PressAsync(GameKey.ResonanceSkill, cancellationToken).ConfigureAwait(false);
                await Task.Delay(240, cancellationToken).ConfigureAwait(false);
            }

            if (!state.ConcertoFull && state.LiberationReady)
            {
                Console.WriteLine("LinnaiStrategy: accelerated liberation ready before concerto -> R");
                await input.PressAsync(GameKey.Liberation, cancellationToken).ConfigureAwait(false);
                await Task.Delay(280, cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        Console.WriteLine("LinnaiStrategy: accelerated forte full -> finish and switch");
        await QuickNormalAsync(input, cancellationToken, count: 3).ConfigureAwait(false);
        if (state.ResonanceReady)
        {
            Console.WriteLine("LinnaiStrategy: accelerated full resonance -> E");
            await input.PressAsync(GameKey.ResonanceSkill, cancellationToken).ConfigureAwait(false);
            await Task.Delay(260, cancellationToken).ConfigureAwait(false);
        }

        if (state.LiberationReady)
        {
            Console.WriteLine("LinnaiStrategy: accelerated full liberation -> R");
            await input.PressAsync(GameKey.Liberation, cancellationToken).ConfigureAwait(false);
            await Task.Delay(300, cancellationToken).ConfigureAwait(false);
        }

        await QuickNormalAsync(input, cancellationToken, count: 4).ConfigureAwait(false);
        await SwitchNextAsync(state, context, input, cancellationToken).ConfigureAwait(false);
    }

    private static async Task SwitchNextAsync(
        CombatState state,
        CombatContext context,
        IGameInputController input,
        CancellationToken cancellationToken)
    {
        var targetSlot = GetSwitchTargetSlot(state, context);
        if (targetSlot == 0)
        {
            Console.WriteLine("LinnaiStrategy: no alive switch target, stay current");
            return;
        }

        Console.WriteLine($"LinnaiStrategy: switch target -> slot {targetSlot}");
        await input.PressAsync(ToSwitchKey(targetSlot), cancellationToken).ConfigureAwait(false);
        context.RecordSwitchAttempt(targetSlot);
        context.LastSwitchAt = DateTimeOffset.UtcNow;
        await Task.Delay(300, cancellationToken).ConfigureAwait(false);
    }

    private static int GetSwitchTargetSlot(CombatState state, CombatContext context)
    {
        if (state.Slot3Alive && !context.IsSlotDisabled(3))
        {
            return 3;
        }

        if (state.Slot1Alive && !context.IsSlotDisabled(1))
        {
            return 1;
        }

        return 0;
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

    private static async Task QuickNormalAsync(
        IGameInputController input,
        CancellationToken cancellationToken,
        int count)
    {
        for (var i = 0; i < count; i++)
        {
            await input.PressAsync(GameKey.NormalAttack, cancellationToken).ConfigureAwait(false);
            await Task.Delay(75, cancellationToken).ConfigureAwait(false);
        }
    }
}
