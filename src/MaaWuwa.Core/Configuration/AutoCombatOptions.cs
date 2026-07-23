using System.Text.Json;
using System.Text.Json.Serialization;

namespace MaaWuwa.Core.Configuration;

public sealed record AutoCombatOptions
{
    public int DurationSeconds { get; init; } = 120;

    public int LoopDelayMilliseconds { get; init; } = 80;

    public int NoEnemyFramesToFinish { get; init; } = 4;

    public int NoEnemyFinishMilliseconds { get; init; } = 1000;

    public int CombatEndOcrMinSeconds { get; init; } = 5;

    public int LockTargetIntervalMilliseconds { get; init; } = 500;

    public int SwitchIntervalMilliseconds { get; init; } = 6000;

    public int NormalAttackHoldMilliseconds { get; init; } = 120;

    public int FrameWidth { get; init; } = 1280;

    public int FrameHeight { get; init; } = 720;

    public bool EnableDebugCapture { get; init; }

    public string DebugDirectory { get; init; } = "debug/auto-combat";

    public IReadOnlyList<string> Team { get; init; } = ["Character1", "Character2", "Character3"];

    public RecognitionOptions Recognition { get; init; } = new();

    public static AutoCombatOptions Load(string? explicitPath = null)
    {
        foreach (var path in EnumerateCandidatePaths(explicitPath))
        {
            if (!File.Exists(path))
            {
                continue;
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize(json, AutoCombatJsonContext.Default.AutoCombatOptions) ?? new AutoCombatOptions();
        }

        return new AutoCombatOptions();
    }

    private static IEnumerable<string> EnumerateCandidatePaths(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            yield return Path.GetFullPath(explicitPath);
        }

        yield return Path.GetFullPath("assets/resource/config/auto_combat.json");
        yield return Path.GetFullPath("resource/config/auto_combat.json");
        yield return Path.GetFullPath("config/auto_combat.json");
        yield return Path.GetFullPath("auto_combat.json");
    }
}

public sealed record RecognitionOptions
{
    public RectOptions EnemyHealthRoi { get; init; } = new() { X = 100, Y = 80, Width = 1080, Height = 320 };

    public RectOptions BossHealthRoi { get; init; } = new() { X = 250, Y = 20, Width = 780, Height = 90 };

    public RectOptions ResonanceRoi { get; init; } = new() { X = 930, Y = 520, Width = 90, Height = 90 };

    public RectOptions LiberationRoi { get; init; } = new() { X = 1030, Y = 455, Width = 90, Height = 90 };

    public RectOptions EchoRoi { get; init; } = new() { X = 825, Y = 520, Width = 90, Height = 90 };

    public RectOptions ConcertoRoi { get; init; } = new() { X = 477, Y = 647, Width = 42, Height = 42 };

    public RectOptions ChisaForteRoi { get; init; } = new() { X = 537, Y = 651, Width = 205, Height = 25 };

    public RectOptions FeixueForteRoi { get; init; } = new() { X = 523, Y = 636, Width = 233, Height = 43 };

    public RectOptions LinnaiForteRoi { get; init; } = new() { X = 518, Y = 639, Width = 233, Height = 43 };

    public string ChisaForteFullTemplate { get; init; } = "千咲特殊能量条_已满.png";

    public string ChisaForteNotFullTemplate { get; init; } = "千咲特殊能量条_未满.png";

    public double ChisaForteFullThreshold { get; init; } = 0.92;

    public double ChisaForteFullMargin { get; init; } = 0.04;

    public string FeixueForte1FullTemplate { get; init; } = "绯雪特殊能量条1_已满.png";

    public string FeixueForte1NotFullTemplate { get; init; } = "绯雪特殊能量条1_未满.png";

    public string FeixueForte2FullTemplate { get; init; } = "绯雪特殊能量条2_已满.png";

    public string FeixueForte2NotFullTemplate { get; init; } = "绯雪特殊能量条2_未满.png";

    public string FeixueForte3FullTemplate { get; init; } = "绯雪特殊能量条3_已满.png";

    public double FeixueForteFullThreshold { get; init; } = 0.45;

    public double FeixueForteFullMargin { get; init; } = 0.08;

    public string LinnaiForteFullTemplate { get; init; } = "林奈回路_已满.png";

    public string LinnaiForteNotFullTemplate { get; init; } = "林奈回路_未满.png";

    public string LinnaiAcceleratedForteFullTemplate { get; init; } = "林奈加速回路_已满.png";

    public string LinnaiAcceleratedForteNotFullTemplate { get; init; } = "林奈加速回路_未满.png";

    public double LinnaiForteFullThreshold { get; init; } = 0.72;

    public double LinnaiForteFullMargin { get; init; } = 0.04;

    public double LinnaiForteVisibleThreshold { get; init; } = 0.62;

    public RectOptions Slot1Roi { get; init; } = new() { X = 1160, Y = 175, Width = 90, Height = 60 };

    public RectOptions Slot2Roi { get; init; } = new() { X = 1160, Y = 255, Width = 90, Height = 60 };

    public RectOptions Slot3Roi { get; init; } = new() { X = 1160, Y = 335, Width = 90, Height = 60 };

    public double EnemyMinAspectRatio { get; init; } = 3.0;

    public int EnemyMinWidth { get; init; } = 20;

    public int EnemyMinHeight { get; init; } = 2;

    public int EnemyMaxHeight { get; init; } = 14;

    public int SkillReadyThreshold { get; init; } = 180;

    public double SkillReadyBrightRatio { get; init; } = 0.12;

    public double ConcertoFullRingRatio { get; init; } = 0.32;

    public double CurrentSlotBrightRatio { get; init; } = 0.16;

    public int SlotAliveSaturationThreshold { get; init; } = 35;

    public int SlotAliveValueThreshold { get; init; } = 45;

    public double SlotAliveColorRatio { get; init; } = 0.035;

    public bool CombatEndOcrEnabled { get; init; } = true;

    public RectOptions CombatEndOcrRoi { get; init; } = new() { X = 300, Y = 80, Width = 680, Height = 420 };

    public string CombatEndTextRegex { get; init; } = "挑战成功|战斗胜利|领取奖励|获得奖励|继续挑战|离开|退出|完成|结算|复活|复苏";
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    WriteIndented = true)]
[JsonSerializable(typeof(AutoCombatOptions))]
public sealed partial class AutoCombatJsonContext : JsonSerializerContext;
