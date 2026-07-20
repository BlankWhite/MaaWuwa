using System.Text.Json;
using System.Text.Json.Serialization;

namespace MaaWuwa.Core.Daily;

public enum DailyFarmType
{
    Simulation,
    Tacet,
    Forgery
}

public enum SimulationMaterial
{
    ResonatorExp,
    WeaponExp,
    ShellCredit
}

public sealed record DailyOptions
{
    public DailyFarmType FarmType { get; init; } = DailyFarmType.Simulation;

    public SimulationMaterial Material { get; init; } = SimulationMaterial.ShellCredit;

    public bool FarmAllNightmare { get; init; }

    public bool FarmEchoForDaily { get; init; } = true;

    public bool ContinueAfterDaily { get; init; }

    public bool ClaimMail { get; init; } = true;

    public bool ClaimBattlePass { get; init; } = true;

    public bool CheckWeeklyGarden { get; init; }

    public int DurationSeconds { get; init; } = 900;

    public int LoopDelayMilliseconds { get; init; } = 500;

    public int NeededDailyStamina { get; init; } = 180;

    public int RequiredActivityPoints { get; init; } = 100;

    public int SimulationCostPerRun { get; init; } = 40;

    public int DomainCombatSeconds { get; init; } = 75;

    public bool AllowOverUseForDaily { get; init; } = true;

    public bool EnableDebugCapture { get; init; }

    public string DebugDirectory { get; init; } = "debug/daily";

    public DailyRecognitionOptions Recognition { get; init; } = new();

    public DailyCoordinateOptions Coordinates { get; init; } = new();

    public static DailyOptions Load(string? explicitPath = null)
    {
        foreach (var path in CandidatePaths(explicitPath))
        {
            if (!File.Exists(path))
            {
                continue;
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize(json, DailyJsonContext.Default.DailyOptions) ?? new DailyOptions();
        }

        return new DailyOptions();
    }

    private static IEnumerable<string> CandidatePaths(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            yield return Path.GetFullPath(explicitPath);
        }

        yield return Path.GetFullPath("assets/resource/config/daily.json");
        yield return Path.GetFullPath("resource/config/daily.json");
        yield return Path.GetFullPath("config/daily.json");
        yield return Path.GetFullPath("daily.json");
    }
}

public sealed record DailyRecognitionOptions
{
    public int FrameWidth { get; init; } = 1280;

    public int FrameHeight { get; init; } = 720;

    public int[] UsedStaminaRoi { get; init; } = [128, 72, 512, 468];

    public int[] ActivityPointsRoi { get; init; } = [243, 576, 141, 94];

    public int[] TopStaminaRoi { get; init; } = [627, 0, 550, 72];

    public int[] FInteractRoi { get; init; } = [851, 348, 108, 48];

    public string FInteractTemplate { get; init; } = "f_interact.png";

    public double FInteractThreshold { get; init; } = 0.75;

    public string UsedStaminaRegex { get; init; } = @"(\d+)\s*/\s*180";

    public string NumberRegex { get; init; } = @"\d+";
}

public sealed record DailyCoordinateOptions
{
    public double GuidebookQuestX { get; init; } = 0.17;

    public double GuidebookQuestY { get; init; } = 0.12;

    public double DailyNextPageX { get; init; } = 0.974;

    public double DailyNextPageY { get; init; } = 0.6;

    public double DailyStaminaProceedX { get; init; } = 0.885;

    public double DailyStaminaProceedFallbackY { get; init; } = 0.25;

    public double ClaimDailyEntryX { get; init; } = 0.885;

    public double ClaimDailyEntryY { get; init; } = 0.25;

    public double ClaimDailyRewardX { get; init; } = 0.93;

    public double ClaimDailyRewardY { get; init; } = 0.882;

    public double SimulationBookX { get; init; } = 0.24;

    public double SimulationBookY { get; init; } = 0.3;

    public double BookBottomX { get; init; } = 0.973;

    public double BookBottomY { get; init; } = 0.8806;

    public double SimulationMaterialX { get; init; } = 0.898;

    public double SimulationMaterialBaseY { get; init; } = 0.533;

    public double SimulationMaterialStepY { get; init; } = 0.14;

    public double TravelButtonX { get; init; } = 0.93;

    public double TravelButtonY { get; init; } = 0.9;

    public double TeamChallengeX { get; init; } = 0.93;

    public double TeamChallengeY { get; init; } = 0.9;

    public double StaminaSingleX { get; init; } = 0.32;

    public double StaminaDoubleX { get; init; } = 0.67;

    public double StaminaClaimY { get; init; } = 0.62;

    public double FarmAgainX { get; init; } = 0.68;

    public double FarmAgainY { get; init; } = 0.84;

    public double BackToWorldX { get; init; } = 0.42;

    public double BackToWorldY { get; init; } = 0.84;

    public double MailEntryX { get; init; } = 0.64;

    public double MailEntryY { get; init; } = 0.95;

    public double MailClaimAllX { get; init; } = 0.14;

    public double MailClaimAllY { get; init; } = 0.9;

    public double BattlePassEntryX { get; init; } = 0.86;

    public double BattlePassEntryY { get; init; } = 0.05;

    public double BattlePassTaskTabX { get; init; } = 0.04;

    public double BattlePassTaskTabY { get; init; } = 0.3;

    public double BattlePassRewardTabX { get; init; } = 0.04;

    public double BattlePassRewardTabY { get; init; } = 0.17;

    public double BattlePassClaimX { get; init; } = 0.68;

    public double BattlePassClaimY { get; init; } = 0.91;
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    Converters = [typeof(JsonStringEnumConverter<DailyFarmType>), typeof(JsonStringEnumConverter<SimulationMaterial>)],
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    WriteIndented = true)]
[JsonSerializable(typeof(DailyOptions))]
public sealed partial class DailyJsonContext : JsonSerializerContext;
