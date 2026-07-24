using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using MaaFramework.Binding;
using MaaFramework.Binding.Buffers;
using MaaFramework.Binding.Custom;
using MaaWuwa.Agent.Maa;
using MaaWuwa.Core.Characters;
using MaaWuwa.Core.Combat;
using MaaWuwa.Core.Configuration;
using MaaWuwa.Core.Daily;
using MaaWuwa.Core.Input;
using MaaWuwa.Core.Recognition;
using OpenCvSharp;

namespace MaaWuwa.Agent.Actions.Daily;

public sealed partial class DailyTickRecognition : IMaaCustomRecognition
{
    private const int VkEscape = 0x1B;
    private const int VkF2 = 0x71;
    private const int VkF = 'F';
    private const int VkAlt = 0x12;

    private static readonly string[] X11KeyScriptCandidates =
    [
        "/home/shulk/src/MaaWuwa/tools/x11_key.sh",
        "tools/x11_key.sh"
    ];

    private static readonly string[] X11ClickScriptCandidates =
    [
        "/home/shulk/src/MaaWuwa/tools/x11_click.sh",
        "tools/x11_click.sh"
    ];

    private static readonly ConcurrentDictionary<string, DailyAgentRun> Runs = new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Name { get; set; } = "DailyTick";

    public bool Analyze<T>(T context, in AnalyzeArgs args, in AnalyzeResults results) where T : IMaaContext
    {
        var runKey = args.TaskDetail.Id.ToString();
        var run = Runs.GetOrAdd(runKey, _ => new DailyAgentRun());
        run.Options = DailyOptions.Load(ReadConfigPath(args.RecognitionParam));
        run.Context.Tick++;

        try
        {
            SafeReleaseInput(context, args.Roi);

            if (DateTimeOffset.UtcNow - run.Context.StartedAt > TimeSpan.FromSeconds(run.Options.DurationSeconds))
            {
                return Finish(context, args, results, runKey, run, "timeout");
            }

            switch (run.Context.Step)
            {
                case DailyStep.Start:
                    Log(run, "start daily task");
                    Next(run, DailyStep.OpenDaily);
                    break;

                case DailyStep.OpenDaily:
                    OpenDailyPage(context, args.Roi, run.Options);
                    Next(run, DailyStep.ReadDailyStatus);
                    break;

                case DailyStep.ReadDailyStatus:
                    ReadDailyStatus(context, args, run);
                    Next(run, DailyStep.Decide);
                    break;

                case DailyStep.Decide:
                    DecideNext(run);
                    break;

                case DailyStep.OpenSimulation:
                    OpenSimulation(context, args.Roi, run);
                    Next(run, DailyStep.EnterSimulation);
                    break;

                case DailyStep.EnterSimulation:
                    ClickRelative(context, args.Roi, run.Options, run.Options.Coordinates.TeamChallengeX, run.Options.Coordinates.TeamChallengeY);
                    Sleep(6000);
                    Next(run, DailyStep.WaitDomain);
                    break;

                case DailyStep.WaitDomain:
                    if (Elapsed(run) > TimeSpan.FromSeconds(3))
                    {
                        Next(run, DailyStep.StartDomainChallenge);
                    }
                    break;

                case DailyStep.StartDomainChallenge:
                    if (StartDomainChallenge(context, args, run))
                    {
                        Next(run, DailyStep.Combat);
                    }
                    break;

                case DailyStep.Combat:
                    CombatTick(context, args, run);
                    break;

                case DailyStep.ClaimDomainReward:
                    ClaimDomainReward(context, args.Roi, run);
                    Next(run, DailyStep.ContinueOrFinishFarm);
                    break;

                case DailyStep.ContinueOrFinishFarm:
                    ContinueOrFinishFarm(context, args.Roi, run);
                    break;

                case DailyStep.ClaimDaily:
                    ClaimDaily(context, args.Roi, run.Options);
                    Next(run, run.Options.ClaimMail ? DailyStep.ClaimMail : run.Options.ClaimBattlePass ? DailyStep.ClaimBattlePass : DailyStep.Completed);
                    break;

                case DailyStep.ClaimMail:
                    ClaimMail(context, args.Roi, run.Options);
                    Next(run, run.Options.ClaimBattlePass ? DailyStep.ClaimBattlePass : DailyStep.Completed);
                    break;

                case DailyStep.ClaimBattlePass:
                    ClaimBattlePass(context, args.Roi, run.Options);
                    Next(run, run.Options.CheckWeeklyGarden ? DailyStep.CheckWeeklyGarden : DailyStep.Completed);
                    break;

                case DailyStep.CheckWeeklyGarden:
                    Log(run, "weekly garden is not implemented in MaaWuwa daily MVP; skip");
                    Next(run, DailyStep.Completed);
                    break;

                case DailyStep.Completed:
                    return Finish(context, args, results, runKey, run, "completed");

                case DailyStep.Failed:
                    return Finish(context, args, results, runKey, run, "failed");
            }

            context.OverrideNext(args.NodeName, [args.NodeName]);
            return SetResult(results, run, "running");
        }
        catch (Exception ex)
        {
            SafeReleaseInput(context, args.Roi);
            Console.Error.WriteLine(ex);
            run.Context.LastMessage = ex.Message;
            Next(run, DailyStep.Failed);
            context.OverrideNext(args.NodeName, []);
            return false;
        }
    }

    private static void OpenDailyPage(IMaaContext context, IMaaRectBuffer box, DailyOptions options)
    {
        ClickKey(context, box, VkF2);
        Sleep(3500);
        ClickRelative(context, box, options, options.Coordinates.GuidebookQuestX, options.Coordinates.GuidebookQuestY);
        Sleep(1000);
    }

    private static void ReadDailyStatus(IMaaContext context, AnalyzeArgs args, DailyAgentRun run)
    {
        var usedResult = ReadFirstInt(context, args.Image, run.Options.Recognition.UsedStaminaRoi, run.Options.Recognition.UsedStaminaRegex);
        if (usedResult.Value is null)
        {
            ClickRelative(context, args.Roi, run.Options, run.Options.Coordinates.DailyNextPageX, run.Options.Coordinates.DailyNextPageY);
            Sleep(1000);
            usedResult = ReadFirstInt(context, args.Image, run.Options.Recognition.UsedStaminaRoi, run.Options.Recognition.UsedStaminaRegex);
        }

        var activity = ReadFirstInt(context, args.Image, run.Options.Recognition.ActivityPointsRoi, run.Options.Recognition.NumberRegex).Value;
        run.Context.UsedStamina = usedResult.Value ?? 0;
        run.Context.UsedStaminaTextCenterY = usedResult.CenterY;
        run.Context.ActivityPoints = activity ?? 0;
        run.Context.HasReadStatus = true;
        run.Context.RequiredUse = Math.Max(0, run.Options.NeededDailyStamina - run.Context.UsedStamina);
        Log(run, $"daily status: used={run.Context.UsedStamina}, activity={run.Context.ActivityPoints}, requiredUse={run.Context.RequiredUse}, staminaY={run.Context.UsedStaminaTextCenterY}");
    }

    private static void DecideNext(DailyAgentRun run)
    {
        if (run.Context.ActivityPoints >= run.Options.RequiredActivityPoints)
        {
            Next(run, DailyStep.ClaimDaily);
            return;
        }

        var needStamina = run.Context.UsedStamina < run.Options.NeededDailyStamina;
        if (!needStamina && !run.Options.ContinueAfterDaily)
        {
            Next(run, DailyStep.ClaimDaily);
            return;
        }

        if (run.Options.FarmType != DailyFarmType.Simulation)
        {
            Log(run, $"farm type {run.Options.FarmType} is not implemented yet; skip farming");
            Next(run, DailyStep.ClaimDaily);
            return;
        }

        Next(run, DailyStep.OpenSimulation);
    }

    private static void OpenSimulation(IMaaContext context, IMaaRectBuffer box, DailyAgentRun run)
    {
        var options = run.Options;
        var proceedY = run.Context.UsedStaminaTextCenterY is { } centerY
            ? centerY / (double)options.Recognition.FrameHeight
            : options.Coordinates.DailyStaminaProceedFallbackY;
        Log(run, $"click daily stamina proceed: x={options.Coordinates.DailyStaminaProceedX:F3}, y={proceedY:F3}");
        ClickRelative(context, box, options, options.Coordinates.DailyStaminaProceedX, proceedY);
        Sleep(2500);
        ClickRelative(context, box, options, options.Coordinates.SimulationBookX, options.Coordinates.SimulationBookY);
        Sleep(1000);
        ClickRelative(context, box, options, options.Coordinates.BookBottomX, options.Coordinates.BookBottomY);
        Sleep(800);
        var index = options.Material switch
        {
            SimulationMaterial.ResonatorExp => 0,
            SimulationMaterial.WeaponExp => 1,
            _ => 2
        };
        ClickRelative(context, box, options, options.Coordinates.SimulationMaterialX, options.Coordinates.SimulationMaterialBaseY + index * options.Coordinates.SimulationMaterialStepY);
        Sleep(800);
        ClickRelative(context, box, options, options.Coordinates.TravelButtonX, options.Coordinates.TravelButtonY);
        Sleep(6000);
    }

    private static bool StartDomainChallenge(IMaaContext context, AnalyzeArgs args, DailyAgentRun run)
    {
        // Mirror ok-wuthering-waves: poke the instance room so the F prompt
        // appears, then pick_f() only after f_interact is visible.
        Click(context, args.Roi, 640, 360);
        Sleep(500);

        if (HasFInteract(context, args.Image, run.Options))
        {
            Log(run, "f_interact found, press f to start domain challenge");
            PressKey(context, args.Roi, "f", VkF);
            Sleep(2500);
            return true;
        }

        if (Elapsed(run) > TimeSpan.FromSeconds(18))
        {
            Log(run, "f_interact not found after timeout, fallback press f once");
            PressKey(context, args.Roi, "f", VkF);
            Sleep(2500);
            return true;
        }

        Log(run, "searching f_interact: left click sent, wait next frame");
        return false;
    }

    private static void CombatTick(IMaaContext context, AnalyzeArgs args, DailyAgentRun run)
    {
        using var frame = DecodeFrame(args.Image);
        run.Combat.FrameIndex++;
        if (run.Options.EnableDebugCapture && run.Context.Tick % 8 == 0)
        {
            Directory.CreateDirectory(run.Options.DebugDirectory);
            Cv2.ImWrite(Path.Combine(run.Options.DebugDirectory, $"daily-{run.Context.Tick:D5}.png"), frame);
        }

        var enemy = new EnemyHealthBarRecognizer(run.AutoCombatOptions.Recognition);
        var boss = new BossHealthBarRecognizer(run.AutoCombatOptions.Recognition);
        var skill = new SkillRecognizer(run.AutoCombatOptions.Recognition);
        var slot = new CurrentSlotRecognizer(run.AutoCombatOptions.Recognition);
        var character = new CharacterRecognizer(run.AutoCombatOptions.Team, run.AutoCombatOptions.Recognition);
        var detector = new CombatDetector(enemy, boss, skill, slot, character);
        var state = detector.DetectAsync(frame, CancellationToken.None).GetAwaiter().GetResult();

        if (state.EnemyFound)
        {
            run.Combat.LastEnemySeenAt = DateTimeOffset.UtcNow;
            run.Combat.ConsecutiveNoEnemyFrames = 0;
        }
        else
        {
            run.Combat.ConsecutiveNoEnemyFrames++;
        }

        var timedOut = Elapsed(run) > TimeSpan.FromSeconds(run.Options.DomainCombatSeconds);
        var minCombat = Elapsed(run) > TimeSpan.FromSeconds(10);
        var noEnemy = run.Combat.ConsecutiveNoEnemyFrames >= run.AutoCombatOptions.NoEnemyFramesToFinish
            && DateTimeOffset.UtcNow - run.Combat.LastEnemySeenAt > TimeSpan.FromMilliseconds(run.AutoCombatOptions.NoEnemyFinishMilliseconds);
        var combatEndText = minCombat && ShouldCheckCombatEndOcr(run.Combat, run.AutoCombatOptions)
            && HasCombatEndText(context, args.Image, run.AutoCombatOptions);
        if (timedOut || combatEndText || (minCombat && noEnemy))
        {
            Log(run, timedOut ? "combat timed out, claim reward" : combatEndText ? "combat end text found, claim reward" : "no enemy, claim reward");
            Next(run, DailyStep.ClaimDomainReward);
            return;
        }

        var input = new MaaGameController(new ContextActionControllerAdapter(context, args.Roi));
        if (!state.HasTarget && !CanRunWithoutTarget(state))
        {
            TryLockTarget(run.Combat, input, run.AutoCombatOptions);
            return;
        }

        var generic = new GenericStrategy(run.AutoCombatOptions);
        ICharacterStrategyFactory strategyFactory = new CharacterStrategyFactory([new FeixueStrategy(), new LinnaiStrategy(), new SanhuaStrategy(), new ChisaStrategy(), generic], generic);
        var strategy = strategyFactory.Create(state.CharacterName);
        strategy.PerformAsync(state, run.Combat, input, CancellationToken.None).GetAwaiter().GetResult();
    }

    private static void ClaimDomainReward(IMaaContext context, IMaaRectBuffer box, DailyAgentRun run)
    {
        PressKey(context, box, "F", VkF);
        Sleep(2000);
        var useDouble = run.Options.ContinueAfterDaily || run.Context.RequiredUse > run.Options.SimulationCostPerRun;
        ClickRelative(context, box, run.Options, useDouble ? run.Options.Coordinates.StaminaDoubleX : run.Options.Coordinates.StaminaSingleX, run.Options.Coordinates.StaminaClaimY);
        Sleep(2500);
        var used = useDouble ? run.Options.SimulationCostPerRun * 2 : run.Options.SimulationCostPerRun;
        run.Context.UsedStamina += used;
        run.Context.RequiredUse = Math.Max(0, run.Context.RequiredUse - used);
        run.Context.FarmRuns++;
        Log(run, $"claim simulation reward: used={used}, totalUsed={run.Context.UsedStamina}, remaining={run.Context.RequiredUse}");
    }

    private static void ContinueOrFinishFarm(IMaaContext context, IMaaRectBuffer box, DailyAgentRun run)
    {
        var continueFarm = run.Options.ContinueAfterDaily || run.Context.RequiredUse > 0;
        if (!continueFarm || (!run.Options.AllowOverUseForDaily && run.Context.RequiredUse < run.Options.SimulationCostPerRun))
        {
            ClickRelative(context, box, run.Options, run.Options.Coordinates.BackToWorldX, run.Options.Coordinates.BackToWorldY);
            Sleep(3000);
            Next(run, DailyStep.ClaimDaily);
            return;
        }

        ClickRelative(context, box, run.Options, run.Options.Coordinates.FarmAgainX, run.Options.Coordinates.FarmAgainY);
        Sleep(3000);
        run.Combat = CreateCombatContext();
        Next(run, DailyStep.WaitDomain);
    }

    private static void ClaimDaily(IMaaContext context, IMaaRectBuffer box, DailyOptions options)
    {
        OpenDailyPage(context, box, options);
        ClickRelative(context, box, options, options.Coordinates.ClaimDailyEntryX, options.Coordinates.ClaimDailyEntryY);
        Sleep(1500);
        ClickRelative(context, box, options, options.Coordinates.ClaimDailyRewardX, options.Coordinates.ClaimDailyRewardY);
        Sleep(1500);
        ClickKey(context, box, VkEscape);
        Sleep(1000);
    }

    private static void ClaimMail(IMaaContext context, IMaaRectBuffer box, DailyOptions options)
    {
        ClickKey(context, box, VkEscape);
        Sleep(1500);
        ClickRelative(context, box, options, options.Coordinates.MailEntryX, options.Coordinates.MailEntryY);
        Sleep(1000);
        ClickRelative(context, box, options, options.Coordinates.MailClaimAllX, options.Coordinates.MailClaimAllY);
        Sleep(1500);
        ClickKey(context, box, VkEscape);
        Sleep(1000);
    }

    private static void ClaimBattlePass(IMaaContext context, IMaaRectBuffer box, DailyOptions options)
    {
        KeyDown(context, box, VkAlt);
        Sleep(80);
        ClickRelative(context, box, options, options.Coordinates.BattlePassEntryX, options.Coordinates.BattlePassEntryY);
        Sleep(80);
        KeyUp(context, box, VkAlt);
        Sleep(1500);
        ClickRelative(context, box, options, options.Coordinates.BattlePassTaskTabX, options.Coordinates.BattlePassTaskTabY);
        Sleep(800);
        ClickRelative(context, box, options, options.Coordinates.BattlePassClaimX, options.Coordinates.BattlePassClaimY);
        Sleep(1200);
        ClickRelative(context, box, options, options.Coordinates.BattlePassRewardTabX, options.Coordinates.BattlePassRewardTabY);
        Sleep(800);
        ClickRelative(context, box, options, options.Coordinates.BattlePassClaimX, options.Coordinates.BattlePassClaimY);
        Sleep(1200);
        ClickKey(context, box, VkEscape);
        Sleep(1000);
    }

    private static bool CanRunWithoutTarget(CombatState state)
    {
        return string.Equals(state.CharacterName, "Feixue", StringComparison.OrdinalIgnoreCase)
            && state.FeixueForteStage > 0;
    }

    private static bool ShouldCheckCombatEndOcr(CombatContext combatContext, AutoCombatOptions options)
    {
        return options.Recognition.CombatEndOcrEnabled
            && DateTimeOffset.UtcNow - combatContext.StartedAt >= TimeSpan.FromSeconds(options.CombatEndOcrMinSeconds)
            && combatContext.FrameIndex % 2 == 0;
    }

    private static bool HasCombatEndText(IMaaContext context, IMaaImageBuffer image, AutoCombatOptions options)
    {
        var combatEndRoi = options.Recognition.CombatEndOcrRoi;
        var roi = new[] { combatEndRoi.X, combatEndRoi.Y, combatEndRoi.Width, combatEndRoi.Height };
        var param = JsonSerializer.Serialize(new
        {
            roi,
            expected = options.Recognition.CombatEndTextRegex,
            order_by = "Horizontal"
        }, JsonOptions);
        using var detail = context.RunRecognitionDirect("OCR", param, image);
        if (detail is null || !detail.Hit)
        {
            return false;
        }

        foreach (var text in ExtractStrings(detail.Detail))
        {
            if (Regex.IsMatch(text, options.Recognition.CombatEndTextRegex))
            {
                return true;
            }
        }

        return false;
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

    private static bool HasFInteract(IMaaContext context, IMaaImageBuffer image, DailyOptions options)
    {
        var param = JsonSerializer.Serialize(new
        {
            template = options.Recognition.FInteractTemplate,
            roi = options.Recognition.FInteractRoi,
            threshold = options.Recognition.FInteractThreshold
        }, JsonOptions);
        using var detail = context.RunRecognitionDirect("TemplateMatch", param, image);
        return detail is not null && detail.Hit;
    }

    private static OcrIntResult ReadFirstInt(IMaaContext context, IMaaImageBuffer image, int[] roi, string regex)
    {
        var param = JsonSerializer.Serialize(new { roi, expected = regex, order_by = "Horizontal" }, JsonOptions);
        using var detail = context.RunRecognitionDirect("OCR", param, image);
        if (detail is null || !detail.Hit)
        {
            return new OcrIntResult(null, null);
        }

        foreach (var text in ExtractStrings(detail.Detail))
        {
            var match = Regex.Match(text, regex);
            if (!match.Success)
            {
                continue;
            }

            var digits = Regex.Match(match.Value, @"\d+");
            if (digits.Success && int.TryParse(digits.Value, out var value))
            {
                int? centerY = detail.HitBox is null ? null : detail.HitBox.Y + detail.HitBox.Height / 2;
                return new OcrIntResult(value, centerY);
            }
        }

        return new OcrIntResult(null, null);
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

    private static void ClickRelative(IMaaContext context, IMaaRectBuffer box, DailyOptions options, double x, double y)
    {
        Click(context, box, (int)Math.Round(options.Recognition.FrameWidth * x), (int)Math.Round(options.Recognition.FrameHeight * y));
    }

    private static void Click(IMaaContext context, IMaaRectBuffer box, int x, int y)
    {
        if (TryRunX11Click(x, y))
        {
            return;
        }

        RunAction(context, box, "Click", new { target = new[] { x, y }, contact = 0 });
    }

    private static void ClickKey(IMaaContext context, IMaaRectBuffer box, int key)
    {
        RunAction(context, box, "ClickKey", new { key });
    }

    private static void PressKey(IMaaContext context, IMaaRectBuffer box, string keyName, int keyCode)
    {
        // Send through both paths. X11 is more reliable for the nested Xwayland
        // game, while Maa keeps the action visible in MaaFW logs and may work for
        // other controllers.
        _ = TryRunX11Key(keyName, "click");
        ClickKey(context, box, keyCode);
    }

    private static void KeyDown(IMaaContext context, IMaaRectBuffer box, int key)
    {
        _ = TryRunX11Key(KeyName(key), "down");
        RunAction(context, box, "KeyDown", new { key });
    }

    private static void KeyUp(IMaaContext context, IMaaRectBuffer box, int key)
    {
        _ = TryRunX11Key(KeyName(key), "up");
        RunAction(context, box, "KeyUp", new { key });
    }

    private static bool TryRunX11Key(string keyName, string mode)
    {
        var script = ResolveFirstExisting(X11KeyScriptCandidates);
        return script is not null && (mode == "click"
            ? RunProcess(script, [keyName])
            : RunProcess(script, [$"--{mode}", keyName]));
    }

    private static string KeyName(int key)
    {
        return key switch
        {
            VkAlt => "Alt_L",
            VkEscape => "Escape",
            VkF2 => "F2",
            _ when key is >= 'A' and <= 'Z' => ((char)char.ToLowerInvariant((char)key)).ToString(),
            _ when key is >= '0' and <= '9' => ((char)key).ToString(),
            _ => key.ToString()
        };
    }

    private static bool TryRunX11Click(int x, int y)
    {
        var script = ResolveFirstExisting(X11ClickScriptCandidates);
        return script is not null && RunProcess(script, [x.ToString(), y.ToString()]);
    }

    private static string? ResolveFirstExisting(IEnumerable<string> candidates)
    {
        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }

    private static bool RunProcess(string fileName, IReadOnlyList<string> arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo(fileName)
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo);
            return process is not null && process.WaitForExit(3000) && process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"X11 helper failed: {fileName} {string.Join(' ', arguments)}: {ex.Message}");
            return false;
        }
    }

    private static void RunAction(IMaaContext context, IMaaRectBuffer box, string type, object param)
    {
        var json = JsonSerializer.Serialize(param, JsonOptions);
        using var detail = context.RunActionDirect(type, json, box);
        if (detail is null || !detail.IsSucceeded)
        {
            throw new InvalidOperationException($"Daily action failed: {type} {json} detail={detail}");
        }
    }

    private static void SafeReleaseInput(IMaaContext context, IMaaRectBuffer box)
    {
        foreach (var contact in new[] { 0, 1, 2 })
        {
            TryRunAction(context, box, "TouchUp", new { contact });
        }

        foreach (var key in new[] { VkAlt, VkF, 'W', 'A', 'S', 'D', 'Q', 'E', 'R', '1', '2', '3' })
        {
            TryRunAction(context, box, "KeyUp", new { key });
        }
    }

    private static void TryRunAction(IMaaContext context, IMaaRectBuffer box, string type, object param)
    {
        try
        {
            var json = JsonSerializer.Serialize(param, JsonOptions);
            using var detail = context.RunActionDirect(type, json, box);
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }

    private static Mat DecodeFrame(IMaaImageBuffer image)
    {
        if (!image.TryGetEncodedData(out byte[]? encoded) || encoded.Length == 0)
        {
            throw new InvalidOperationException("DailyTick received an empty image buffer.");
        }

        var frame = Cv2.ImDecode(encoded, ImreadModes.Color);
        if (frame.Empty())
        {
            frame.Dispose();
            throw new InvalidOperationException("OpenCvSharp failed to decode DailyTick image.");
        }

        return frame;
    }

    private static bool SetResult(AnalyzeResults results, DailyAgentRun run, string status)
    {
        return results.Box.TrySetValues(0, 0, 1, 1)
            && results.Detail.TrySetValue(JsonSerializer.Serialize(new
            {
                status,
                step = run.Context.Step.ToString(),
                run.Context.Tick,
                run.Context.UsedStamina,
                run.Context.ActivityPoints,
                run.Context.RequiredUse,
                run.Context.FarmRuns,
                message = run.Context.LastMessage
            }, JsonOptions));
    }

    private static bool Finish<T>(T context, AnalyzeArgs args, AnalyzeResults results, string runKey, DailyAgentRun run, string reason)
        where T : IMaaContext
    {
        Runs.TryRemove(runKey, out _);
        SafeReleaseInput(context, args.Roi);
        context.OverrideNext(args.NodeName, []);
        results.Box.TrySetValues(0, 0, 1, 1);
        results.Detail.TrySetValue(JsonSerializer.Serialize(new
        {
            status = "finished",
            reason,
            step = run.Context.Step.ToString(),
            run.Context.Tick,
            run.Context.UsedStamina,
            run.Context.ActivityPoints,
            run.Context.FarmRuns,
            message = run.Context.LastMessage
        }, JsonOptions));
        Console.WriteLine($"DailyTick finished: reason={reason}, step={run.Context.Step}, tick={run.Context.Tick}");
        return true;
    }

    private static void Next(DailyAgentRun run, DailyStep step)
    {
        run.Context.Step = step;
        run.Context.StepStartedAt = DateTimeOffset.UtcNow;
        Log(run, $"step -> {step}");
    }

    private static TimeSpan Elapsed(DailyAgentRun run) => DateTimeOffset.UtcNow - run.Context.StepStartedAt;

    private static void Log(DailyAgentRun run, string message)
    {
        run.Context.LastMessage = message;
        Console.WriteLine($"DailyTick: {message}");
    }

    private static void Sleep(int milliseconds) => Thread.Sleep(milliseconds);

    private static string? ReadConfigPath(string recognitionParam)
    {
        if (string.IsNullOrWhiteSpace(recognitionParam) || recognitionParam == "null")
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(recognitionParam, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });
            return doc.RootElement.TryGetProperty("config", out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static CombatContext CreateCombatContext()
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

    private readonly record struct OcrIntResult(int? Value, int? CenterY);

    private sealed class DailyAgentRun
    {
        public DailyRunContext Context { get; } = new();

        public CombatContext Combat { get; set; } = CreateCombatContext();

        public DailyOptions Options { get; set; } = new();

        public int NextCharacterSlot { get; set; }

        public AutoCombatOptions AutoCombatOptions { get; } = AutoCombatOptions.Load();
    }
}
