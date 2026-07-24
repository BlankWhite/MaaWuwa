using MaaWuwa.Core.Combat;
using MaaWuwa.Core.Input;

namespace MaaWuwa.Core.Characters;

/// <summary>
/// 绯雪一号位主轴首版。
///
/// 用户提供轴：
/// 1) 一形态：E + 普攻攒特殊能量条1；条1满后长按普攻约3s释放重击；
///    大招亮后释放第一段大招进入第二形态。
/// 2) 二形态：E 两下 + 普攻攒特殊能量条2；条2满后普攻一下、闪避、三次普攻消耗；
///    进入条3满形态后长按普攻约3s释放；若第二段大招亮则长按大招约3s释放。
///
/// 当前完全依赖技能亮/特殊能量条模板状态，不做固定时间轮询切人。
/// </summary>
public sealed class FeixueStrategy : ICharacterStrategy
{
    public string Name => "Feixue";

    public async Task PerformAsync(
        CombatState state,
        CombatContext context,
        IGameInputController input,
        CancellationToken cancellationToken)
    {
        if (!state.HasTarget
            && state.FeixueForteStage == 0
            && !context.FeixueFirstForteReleased
            && !context.FeixueSecondForm
            && !context.FeixueThirdForteReleased)
        {
            return;
        }

        if (state.FeixueForteStage is 2 or 3)
        {
            context.FeixueSecondForm = true;
        }

        Console.WriteLine($"FeixueStrategy: stage={state.FeixueForteStage} second={context.FeixueSecondForm} firstForteReleased={context.FeixueFirstForteReleased} secondComboDone={context.FeixueSecondForteComboDone} thirdReleased={context.FeixueThirdForteReleased} E={state.ResonanceReady} R={state.LiberationReady} Q={state.EchoReady} con={state.ConcertoFull}");

        if (NeedsFeixueOnField(context) && state.CurrentSlot > 0 && state.CurrentSlot != 1 && state.Slot1Alive)
        {
            Console.WriteLine($"FeixueStrategy: expected Feixue on field but current slot={state.CurrentSlot} -> switch slot 1");
            await input.PressAsync(GameKey.SwitchCharacter1, cancellationToken).ConfigureAwait(false);
            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
            return;
        }

        // 一形态重击释放后，只等待/释放第一段 R；不再被 Q/E/普攻抢优先级。
        if (!context.FeixueSecondForm && context.FeixueFirstForteReleased)
        {
            await PerformFirstLiberationWaitAsync(state, context, input, cancellationToken).ConfigureAwait(false);
            return;
        }

        // 二阶段必须先：条2连段完成 -> 条3满 -> 长按普攻 3s。之后新帧确认 R 亮，才长按 R 3s。
        // R2 长按输入可能被动画/镜头吞掉；不要立刻清掉 thirdReleased，保留数秒用于二次确认/重试。
        if (context.FeixueSecondForm && context.FeixueThirdForteReleased)
        {
            if (!EnemyRecentlySeen(state, context, TimeSpan.FromMilliseconds(2500)))
            {
                Console.WriteLine("FeixueStrategy: third forte released but no enemy recently -> pause R2 and allow no-enemy finish");
                return;
            }

            var now = DateTimeOffset.UtcNow;
            var attemptedElapsed = context.FeixueSecondLiberationAttemptedAt == default
                ? TimeSpan.MaxValue
                : now - context.FeixueSecondLiberationAttemptedAt;

            if (state.LiberationReady
                && context.FeixueSecondLiberationAttempts < 2
                && attemptedElapsed >= TimeSpan.FromMilliseconds(1200))
            {
                Console.WriteLine($"FeixueStrategy: third forte released and second liberation ready -> hold R 3s (attempt {context.FeixueSecondLiberationAttempts + 1})");
                await input.HoldAsync(GameKey.Liberation, TimeSpan.FromMilliseconds(3000), cancellationToken).ConfigureAwait(false);
                context.FeixueSecondLiberationAttempts++;
                context.FeixueSecondLiberationAttemptedAt = DateTimeOffset.UtcNow;
                context.NoEnemyFinishSuppressedUntil = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(10);
                Console.WriteLine("FeixueStrategy: second liberation attempted -> suppress no-enemy finish for 10s");
                await Task.Delay(300, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (context.FeixueSecondLiberationAttempts > 0)
            {
                if (state.ConcertoFull
                    && attemptedElapsed >= TimeSpan.FromMilliseconds(3500)
                    && state.CurrentSlot > 0
                    && state.CurrentSlot != 3
                    && state.Slot3Alive
                    && !context.IsSlotDisabled(3))
                {
                    Console.WriteLine($"FeixueStrategy: second liberation attempted and concerto full -> switch slot 3 Chisa ({attemptedElapsed.TotalMilliseconds:F0}ms)");
                    await input.PressAsync(GameKey.SwitchCharacter3, cancellationToken).ConfigureAwait(false);
                    context.RecordSwitchAttempt(3);
                    context.LastSwitchAt = DateTimeOffset.UtcNow;
                    context.FeixueSecondForm = false;
                    context.FeixueSecondForteComboDone = false;
                    context.FeixueThirdForteReleased = false;
                    context.FeixueSecondLiberationAttemptedAt = default;
                    context.FeixueSecondLiberationAttempts = 0;
                    context.NoEnemyFinishSuppressedUntil = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
                    await Task.Delay(300, cancellationToken).ConfigureAwait(false);
                    return;
                }

                if (attemptedElapsed < TimeSpan.FromSeconds(8))
                {
                    Console.WriteLine($"FeixueStrategy: second liberation attempted, keep pending for retry/confirmation ({attemptedElapsed.TotalMilliseconds:F0}ms)");
                    await QuickNormalAsync(input, cancellationToken, count: 1).ConfigureAwait(false);
                    return;
                }

                Console.WriteLine("FeixueStrategy: second liberation pending window ended -> reset second liberation gate");
                context.FeixueThirdForteReleased = false;
                context.FeixueSecondForteComboDone = false;
                context.FeixueSecondLiberationAttemptedAt = default;
                context.FeixueSecondLiberationAttempts = 0;
                await Task.Delay(120, cancellationToken).ConfigureAwait(false);
                return;
            }

            Console.WriteLine("FeixueStrategy: third forte released -> wait second liberation, skip Q/E");
            await QuickNormalAsync(input, cancellationToken, count: 2).ConfigureAwait(false);
            return;
        }

        if (context.FeixueSecondForm || state.FeixueForteStage is 2 or 3)
        {
            await PerformSecondFormAsync(state, context, input, cancellationToken).ConfigureAwait(false);
            return;
        }

        // 条1满的重击优先级也高于 Q，避免声骸抢掉进入二阶段前置动作。
        if (state.FeixueForteStage == 1)
        {
            await PerformFirstFormAsync(state, context, input, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (state.EchoReady)
        {
            Console.WriteLine("FeixueStrategy: echo ready -> Q");
            await input.PressAsync(GameKey.Echo, cancellationToken).ConfigureAwait(false);
            await Task.Delay(180, cancellationToken).ConfigureAwait(false);
            return;
        }

        await PerformFirstFormAsync(state, context, input, cancellationToken).ConfigureAwait(false);
    }

    private static async Task PerformFirstFormAsync(
        CombatState state,
        CombatContext context,
        IGameInputController input,
        CancellationToken cancellationToken)
    {
        if (state.FeixueForteStage == 1 && !context.FeixueFirstForteReleased)
        {
            Console.WriteLine("FeixueStrategy: forte1 full -> hold normal attack 3s");
            await input.HoldAsync(GameKey.NormalAttack, TimeSpan.FromMilliseconds(3200), cancellationToken).ConfigureAwait(false);
            context.FeixueFirstForteReleased = true;
            context.FeixueFirstForteReleasedAt = DateTimeOffset.UtcNow;
            context.FeixueFirstLiberationProbeAt = DateTimeOffset.MinValue;
            await Task.Delay(120, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (state.FeixueForteStage == 1 && context.FeixueFirstForteReleased)
        {
            Console.WriteLine("FeixueStrategy: forte1 already released, wait/normal until liberation ready");
            await QuickNormalAsync(input, cancellationToken, count: 2).ConfigureAwait(false);
            return;
        }

        if (state.ResonanceReady)
        {
            Console.WriteLine("FeixueStrategy: first form resonance ready -> E");
            await input.PressAsync(GameKey.ResonanceSkill, cancellationToken).ConfigureAwait(false);
            await Task.Delay(180, cancellationToken).ConfigureAwait(false);
            return;
        }

        await QuickNormalAsync(input, cancellationToken, count: 3).ConfigureAwait(false);
    }

    private static async Task PerformFirstLiberationWaitAsync(
        CombatState state,
        CombatContext context,
        IGameInputController input,
        CancellationToken cancellationToken)
    {
        if (state.LiberationReady)
        {
            Console.WriteLine("FeixueStrategy: first forte released and liberation ready -> R, enter second form");
            await input.PressAsync(GameKey.Liberation, cancellationToken).ConfigureAwait(false);
            context.FeixueSecondForm = true;
            context.FeixueFirstForteReleased = false;
            context.FeixueSecondForteComboDone = false;
            context.FeixueThirdForteReleased = false;
            context.FeixueSecondLiberationAttemptedAt = default;
            context.FeixueSecondLiberationAttempts = 0;
            context.NoEnemyFinishSuppressedUntil = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var releasedElapsed = context.FeixueFirstForteReleasedAt == default
            ? TimeSpan.Zero
            : now - context.FeixueFirstForteReleasedAt;

        // 日志显示条1长按后有时 stage=1 会持续十几秒，说明重击没有真正吃掉条1。
        // 此时不能一直点普攻等待 R，需要重新长按普攻补释放。
        if (state.FeixueForteStage == 1 && releasedElapsed >= TimeSpan.FromMilliseconds(2200))
        {
            Console.WriteLine($"FeixueStrategy: first forte still full after {releasedElapsed.TotalMilliseconds:F0}ms -> retry hold normal");
            await input.HoldAsync(GameKey.NormalAttack, TimeSpan.FromMilliseconds(3200), cancellationToken).ConfigureAwait(false);
            context.FeixueFirstForteReleasedAt = DateTimeOffset.UtcNow;
            await Task.Delay(120, cancellationToken).ConfigureAwait(false);
            return;
        }

        Console.WriteLine("FeixueStrategy: first forte released -> wait first liberation with quick normals, skip Q/E");
        await QuickNormalAsync(input, cancellationToken, count: 2).ConfigureAwait(false);
    }

    private static async Task PerformSecondFormAsync(
        CombatState state,
        CombatContext context,
        IGameInputController input,
        CancellationToken cancellationToken)
    {
        if (state.FeixueForteStage == 3 && context.FeixueSecondForteComboDone && !context.FeixueThirdForteReleased)
        {
            Console.WriteLine("FeixueStrategy: forte3 full -> hold normal attack 3s, then wait second liberation");
            await input.HoldAsync(GameKey.NormalAttack, TimeSpan.FromMilliseconds(3000), cancellationToken).ConfigureAwait(false);
            context.FeixueThirdForteReleased = true;
            context.FeixueSecondLiberationAttemptedAt = default;
            context.FeixueSecondLiberationAttempts = 0;
            context.NoEnemyFinishSuppressedUntil = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
            await Task.Delay(300, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (state.LiberationReady)
        {
            Console.WriteLine($"FeixueStrategy: second liberation ready but gate not satisfied -> ignore R (comboDone={context.FeixueSecondForteComboDone}, stage={state.FeixueForteStage}, thirdReleased={context.FeixueThirdForteReleased})");
        }

        if (state.FeixueForteStage == 2)
        {
            Console.WriteLine("FeixueStrategy: forte2 full -> normal, dodge, quick normals until forte3");
            await input.PressAsync(GameKey.NormalAttack, cancellationToken).ConfigureAwait(false);
            await Task.Delay(90, cancellationToken).ConfigureAwait(false);
            await input.PressAsync(GameKey.Dodge, cancellationToken).ConfigureAwait(false);
            await Task.Delay(120, cancellationToken).ConfigureAwait(false);
            await QuickNormalAsync(input, cancellationToken, count: 5).ConfigureAwait(false);
            context.FeixueSecondForteComboDone = true;
            return;
        }

        if (state.ResonanceReady)
        {
            Console.WriteLine("FeixueStrategy: second form resonance ready -> E");
            await input.PressAsync(GameKey.ResonanceSkill, cancellationToken).ConfigureAwait(false);
            await Task.Delay(180, cancellationToken).ConfigureAwait(false);
            return;
        }

        Console.WriteLine("FeixueStrategy: second form building forte2 -> quick normals");
        await QuickNormalAsync(input, cancellationToken, count: 3).ConfigureAwait(false);
    }

    private static bool NeedsFeixueOnField(CombatContext context)
    {
        return context.FeixueFirstForteReleased
            || context.FeixueSecondForm
            || context.FeixueThirdForteReleased;
    }

    private static bool EnemyRecentlySeen(CombatState state, CombatContext context, TimeSpan grace)
    {
        if (state.EnemyFound || state.HasTarget)
        {
            return true;
        }

        return context.LastActualEnemySeenAt != default
            && DateTimeOffset.UtcNow - context.LastActualEnemySeenAt <= grace;
    }

    private static async Task QuickNormalAsync(
        IGameInputController input,
        CancellationToken cancellationToken,
        int count)
    {
        for (var i = 0; i < count; i++)
        {
            await input.PressAsync(GameKey.NormalAttack, cancellationToken).ConfigureAwait(false);
            await Task.Delay(70, cancellationToken).ConfigureAwait(false);
        }
    }
}
