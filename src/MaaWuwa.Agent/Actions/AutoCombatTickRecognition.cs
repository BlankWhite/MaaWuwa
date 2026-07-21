using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MaaWuwa.Agent.Maa;
using MaaWuwa.Core.Characters;
using MaaWuwa.Core.Combat;
using MaaWuwa.Core.Configuration;
using MaaWuwa.Core.Input;
using MaaWuwa.Core.Recognition;
using OpenCvSharp;

namespace MaaWuwa.Agent.Actions;

public sealed class AutoCombatTickRecognition : IMaaCustomRecognition
{
    private static readonly ConcurrentDictionary<string, CombatContext> Contexts = new();

    public string Name { get; set; } = "AutoCombatTick";

    public bool Analyze<T>(T context, in AnalyzeArgs args, in AnalyzeResults results) where T : IMaaContext
    {
        try
        {
            var options = AutoCombatOptions.Load(ReadConfigPath(args.RecognitionParam));
            var combatContext = Contexts.GetOrAdd(args.TaskDetail.Id.ToString(), _ => CreateContext());

            if (ShouldTimeout(combatContext, options) || context.IsCancellationRequested)
            {
                return Finish(context, args, results, combatContext, "timeout_or_cancelled");
            }

            using var frame = DecodeFrame(args.Image);
            combatContext.FrameIndex++;

            var detectorParts = CreateDetectorParts(options);
            var detector = detectorParts.Detector;
            var state = detector.DetectAsync(frame, CancellationToken.None).GetAwaiter().GetResult();
            Console.WriteLine($"AutoCombatTick state: slot={state.CurrentSlot} char={state.CharacterName ?? "?"} target={state.HasTarget} enemy={state.EnemyFound} con={state.ConcertoFull} conRatio={state.ConcertoRatio:F2} E={state.ResonanceReady} R={state.LiberationReady} Q={state.EchoReady} chisaVisible={state.ChisaForteVisible} chisaFull={state.ChisaForteFull} chisaScore={state.ChisaForteFullScore:F3}/{state.ChisaForteNotFullScore:F3}");

            combatContext.CurrentSlot = state.CurrentSlot;
            UpdateEnemyState(combatContext, state);
            detectorParts.DebugFrameWriter.Write(frame, combatContext);

            if (ShouldFinish(combatContext, options))
            {
                return Finish(context, args, results, combatContext, "no_enemy");
            }

            if (ShouldCheckCombatEndOcr(combatContext, options) && HasCombatEndText(context, args.Image, options))
            {
                return Finish(context, args, results, combatContext, "combat_end_text");
            }

            var input = new MaaGameController(new ContextActionControllerAdapter(context, args.Roi));
            if (!state.HasTarget)
            {
                TryLockTarget(combatContext, input, options);
                context.OverrideNext(args.NodeName, [args.NodeName]);
                return SetResult(results, state, "searching_target");
            }

            var generic = new GenericStrategy(options);
            ICharacterStrategyFactory strategyFactory = new CharacterStrategyFactory([new ChisaStrategy(), generic], generic);
            var strategy = strategyFactory.Create(state.CharacterName);
            strategy.PerformAsync(state, combatContext, input, CancellationToken.None).GetAwaiter().GetResult();

            context.OverrideNext(args.NodeName, [args.NodeName]);
            return SetResult(results, state, "running");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            context.OverrideNext(args.NodeName, []);
            return false;
        }
    }

    private static CombatContext CreateContext()
    {
        var now = DateTimeOffset.UtcNow;
        return new CombatContext
        {
            InCombat = true,
            StartedAt = now,
            LastEnemySeenAt = now,
            LastSwitchAt = now
        };
    }

    private static (ICombatDetector Detector, DebugFrameWriter DebugFrameWriter) CreateDetectorParts(AutoCombatOptions options)
    {
        var enemyRecognizer = new EnemyHealthBarRecognizer(options.Recognition);
        var bossRecognizer = new BossHealthBarRecognizer(options.Recognition);
        var skillRecognizer = new SkillRecognizer(options.Recognition);
        var slotRecognizer = new CurrentSlotRecognizer(options.Recognition);
        var characterRecognizer = new CharacterRecognizer(options.Team);
        var detector = new CombatDetector(enemyRecognizer, bossRecognizer, skillRecognizer, slotRecognizer, characterRecognizer);
        var debugFrameWriter = new DebugFrameWriter(options, enemyRecognizer, skillRecognizer);
        return (detector, debugFrameWriter);
    }

    private static Mat DecodeFrame(MaaFramework.Binding.Buffers.IMaaImageBuffer image)
    {
        if (!image.TryGetEncodedData(out byte[]? encoded) || encoded.Length == 0)
        {
            throw new InvalidOperationException("AutoCombatTick received an empty image buffer.");
        }

        var frame = Cv2.ImDecode(encoded, ImreadModes.Color);
        if (frame.Empty())
        {
            frame.Dispose();
            throw new InvalidOperationException("OpenCvSharp failed to decode AutoCombatTick image.");
        }

        return frame;
    }

    private static void UpdateEnemyState(CombatContext combatContext, CombatState state)
    {
        if (state.EnemyFound)
        {
            combatContext.LastEnemySeenAt = DateTimeOffset.UtcNow;
            combatContext.ConsecutiveNoEnemyFrames = 0;
        }
        else
        {
            combatContext.ConsecutiveNoEnemyFrames++;
        }
    }

    private static bool ShouldTimeout(CombatContext combatContext, AutoCombatOptions options)
    {
        return DateTimeOffset.UtcNow - combatContext.StartedAt >= TimeSpan.FromSeconds(options.DurationSeconds);
    }

    private static bool ShouldFinish(CombatContext combatContext, AutoCombatOptions options)
    {
        var noEnemyDuration = DateTimeOffset.UtcNow - combatContext.LastEnemySeenAt;
        return combatContext.ConsecutiveNoEnemyFrames >= options.NoEnemyFramesToFinish
            && noEnemyDuration >= TimeSpan.FromMilliseconds(options.NoEnemyFinishMilliseconds);
    }

    private static bool ShouldCheckCombatEndOcr(CombatContext combatContext, AutoCombatOptions options)
    {
        return options.Recognition.CombatEndOcrEnabled
            && DateTimeOffset.UtcNow - combatContext.StartedAt >= TimeSpan.FromSeconds(options.CombatEndOcrMinSeconds)
            && combatContext.FrameIndex % 2 == 0;
    }

    private static bool HasCombatEndText<T>(T context, MaaFramework.Binding.Buffers.IMaaImageBuffer image, AutoCombatOptions options)
        where T : IMaaContext
    {
        var combatEndRoi = options.Recognition.CombatEndOcrRoi;
        var roi = new[] { combatEndRoi.X, combatEndRoi.Y, combatEndRoi.Width, combatEndRoi.Height };
        var param = JsonSerializer.Serialize(new
        {
            roi,
            expected = options.Recognition.CombatEndTextRegex,
            order_by = "Horizontal"
        });
        using var detail = context.RunRecognitionDirect("OCR", param, image);
        if (detail is null || !detail.Hit)
        {
            return false;
        }

        foreach (var text in ExtractStrings(detail.Detail))
        {
            if (Regex.IsMatch(text, options.Recognition.CombatEndTextRegex))
            {
                Console.WriteLine($"AutoCombatTick combat end OCR hit: {text}");
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> ExtractStrings(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            yield break;
        }

        JsonDocument? doc = null;
        try
        {
            doc = JsonDocument.Parse(json);
            foreach (var value in ExtractStrings(doc.RootElement))
            {
                yield return value;
            }
        }
        finally
        {
            doc?.Dispose();
        }
    }

    private static IEnumerable<string> ExtractStrings(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                yield return element.GetString() ?? string.Empty;
                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    foreach (var value in ExtractStrings(property.Value))
                    {
                        yield return value;
                    }
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var value in ExtractStrings(item))
                    {
                        yield return value;
                    }
                }
                break;
        }
    }

    private static void TryLockTarget(CombatContext combatContext, IGameInputController input, AutoCombatOptions options)
    {
        var elapsed = DateTimeOffset.UtcNow - combatContext.LastLockAttemptAt;
        if (elapsed < TimeSpan.FromMilliseconds(options.LockTargetIntervalMilliseconds))
        {
            return;
        }

        input.PressAsync(GameKey.LockTarget, CancellationToken.None).GetAwaiter().GetResult();
        combatContext.LastLockAttemptAt = DateTimeOffset.UtcNow;
    }

    private static bool Finish<T>(T context, AnalyzeArgs args, AnalyzeResults results, CombatContext combatContext, string reason)
        where T : IMaaContext
    {
        combatContext.InCombat = false;
        Contexts.TryRemove(args.TaskDetail.Id.ToString(), out _);
        context.OverrideNext(args.NodeName, []);
        results.Box.TrySetValues(0, 0, 1, 1);
        results.Detail.TrySetValue(JsonSerializer.Serialize(new
        {
            status = "finished",
            reason,
            frames = combatContext.FrameIndex
        }));
        Console.WriteLine($"AutoCombatTick finished: reason={reason}, frames={combatContext.FrameIndex}");
        return true;
    }

    private static bool SetResult(AnalyzeResults results, CombatState state, string status)
    {
        return results.Box.TrySetValues(0, 0, 1, 1)
            && results.Detail.TrySetValue(JsonSerializer.Serialize(new
            {
                status,
                state.EnemyFound,
                state.BossFound,
                state.HasTarget,
                state.ResonanceReady,
                state.LiberationReady,
                state.EchoReady,
                state.ConcertoFull,
                state.ChisaForteFull,
                state.ChisaForteVisible,
                state.ChisaForteFullScore,
                state.ChisaForteNotFullScore,
                state.ConcertoRatio,
                state.CurrentSlot,
                state.CharacterName
            }));
    }

    private static string? ReadConfigPath(string recognitionParam)
    {
        using var doc = ParseParam(recognitionParam);
        if (doc is null)
        {
            return null;
        }

        return doc.RootElement.TryGetProperty("config", out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static JsonDocument? ParseParam(string param)
    {
        if (string.IsNullOrWhiteSpace(param) || param == "null")
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(param, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
