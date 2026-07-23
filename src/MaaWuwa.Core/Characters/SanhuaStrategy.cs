using MaaWuwa.Core.Combat;
using MaaWuwa.Core.Input;

namespace MaaWuwa.Core.Characters;

/// <summary>
/// 散华二号位速切策略。
///
/// 参考 ok-wuthering-waves/src/char/Sanhua.py：散华入场后按住普攻，
/// 尝试大招/共鸣技能，等待冰棱爆发窗口，必要时放声骸，然后切回主 C。
/// 这里保留状态驱动：R/E/Q 仅在识别亮起时按；散华完成短轴后固定切 3 号千咲。
/// </summary>
public sealed class SanhuaStrategy : ICharacterStrategy
{
    public string Name => "Sanhua";

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

        Console.WriteLine($"SanhuaStrategy: start short rotation E={state.ResonanceReady} R={state.LiberationReady} Q={state.EchoReady} con={state.ConcertoFull}");

        var usedLiberation = false;
        await input.HoldAsync(GameKey.NormalAttack, TimeSpan.FromMilliseconds(120), cancellationToken).ConfigureAwait(false);

        if (state.LiberationReady)
        {
            Console.WriteLine("SanhuaStrategy: liberation ready -> R");
            await input.PressAsync(GameKey.Liberation, cancellationToken).ConfigureAwait(false);
            usedLiberation = true;
            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        }
        else if (state.ResonanceReady)
        {
            Console.WriteLine("SanhuaStrategy: resonance ready -> E");
            await input.PressAsync(GameKey.ResonanceSkill, cancellationToken).ConfigureAwait(false);
            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        }

        // ok Sanhua waits roughly 0.85s before releasing the held slash timing.
        await input.HoldAsync(GameKey.NormalAttack, TimeSpan.FromMilliseconds(850), cancellationToken).ConfigureAwait(false);
        await Task.Delay(500, cancellationToken).ConfigureAwait(false);

        if (usedLiberation && state.ResonanceReady)
        {
            Console.WriteLine("SanhuaStrategy: post-liberation resonance -> E");
            await input.PressAsync(GameKey.ResonanceSkill, cancellationToken).ConfigureAwait(false);
            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        }

        if (state.ConcertoFull && state.EchoReady)
        {
            Console.WriteLine("SanhuaStrategy: concerto full and echo ready -> Q");
            await input.PressAsync(GameKey.Echo, cancellationToken).ConfigureAwait(false);
            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        }

        var switchTargetSlot = GetSwitchTargetSlot(state, context);
        if (switchTargetSlot == 0)
        {
            Console.WriteLine("SanhuaStrategy: no alive switch target, stay current");
            return;
        }

        var switchTarget = ToSwitchKey(switchTargetSlot);
        Console.WriteLine($"SanhuaStrategy: switch target -> slot {switchTargetSlot}");
        await input.PressAsync(switchTarget, cancellationToken).ConfigureAwait(false);
        context.RecordSwitchAttempt(switchTargetSlot);
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
}
