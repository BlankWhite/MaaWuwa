using MaaWuwa.Core.Characters;
using MaaWuwa.Core.Configuration;
using MaaWuwa.Core.Input;
using MaaWuwa.Core.Recognition;

namespace MaaWuwa.Core.Combat;

public sealed class AutoCombatService
{
    private readonly IGameCapture _capture;
    private readonly IGameInputController _input;
    private readonly ICombatDetector _detector;
    private readonly ICharacterStrategyFactory _strategyFactory;
    private readonly DebugFrameWriter? _debugFrameWriter;
    private readonly AutoCombatOptions _options;

    public AutoCombatService(
        IGameCapture capture,
        IGameInputController input,
        ICombatDetector detector,
        ICharacterStrategyFactory strategyFactory,
        AutoCombatOptions options,
        DebugFrameWriter? debugFrameWriter = null)
    {
        _capture = capture;
        _input = input;
        _detector = detector;
        _strategyFactory = strategyFactory;
        _options = options;
        _debugFrameWriter = debugFrameWriter;
    }

    public async Task<bool> RunAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var context = new CombatContext
        {
            InCombat = true,
            StartedAt = now,
            LastEnemySeenAt = now,
            LastSwitchAt = now
        };

        while (!cancellationToken.IsCancellationRequested)
        {
            if (DateTimeOffset.UtcNow - context.StartedAt >= TimeSpan.FromSeconds(_options.DurationSeconds))
            {
                context.InCombat = false;
                return true;
            }

            using var frame = await _capture.CaptureAsync(cancellationToken).ConfigureAwait(false);
            context.FrameIndex++;

            var state = await _detector.DetectAsync(frame, cancellationToken).ConfigureAwait(false);
            context.CurrentSlot = state.CurrentSlot;
            UpdateEnemyState(context, state);
            _debugFrameWriter?.Write(frame, context);

            if (ShouldFinish(context))
            {
                context.InCombat = false;
                return true;
            }

            if (!state.HasTarget)
            {
                await TryLockTargetAsync(context, cancellationToken).ConfigureAwait(false);
            }

            var strategy = _strategyFactory.Create(state.CharacterName);
            await strategy.PerformAsync(state, context, _input, cancellationToken).ConfigureAwait(false);

            await Task.Delay(_options.LoopDelayMilliseconds, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    private static void UpdateEnemyState(CombatContext context, CombatState state)
    {
        if (state.EnemyFound)
        {
            context.LastEnemySeenAt = DateTimeOffset.UtcNow;
            context.ConsecutiveNoEnemyFrames = 0;
        }
        else
        {
            context.ConsecutiveNoEnemyFrames++;
        }
    }

    private bool ShouldFinish(CombatContext context)
    {
        var noEnemyDuration = DateTimeOffset.UtcNow - context.LastEnemySeenAt;
        return context.ConsecutiveNoEnemyFrames >= _options.NoEnemyFramesToFinish
            && noEnemyDuration >= TimeSpan.FromMilliseconds(_options.NoEnemyFinishMilliseconds);
    }

    private async Task TryLockTargetAsync(CombatContext context, CancellationToken cancellationToken)
    {
        var elapsed = DateTimeOffset.UtcNow - context.LastLockAttemptAt;
        if (elapsed < TimeSpan.FromMilliseconds(_options.LockTargetIntervalMilliseconds))
        {
            return;
        }

        await _input.PressAsync(GameKey.LockTarget, cancellationToken).ConfigureAwait(false);
        context.LastLockAttemptAt = DateTimeOffset.UtcNow;
    }
}
