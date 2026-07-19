using System.Text.Json;
using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MaaWuwa.Agent.Maa;
using MaaWuwa.Core.Characters;
using MaaWuwa.Core.Combat;
using MaaWuwa.Core.Configuration;
using MaaWuwa.Core.Input;
using MaaWuwa.Core.Recognition;

namespace MaaWuwa.Agent.Actions;

public sealed class AutoCombatAction : IMaaCustomAction
{
    public string Name { get; set; } = "AutoCombat";

    public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
    {
        Console.WriteLine($"{Name} called. node={args.NodeName}, param={args.ActionParam}");

        using var cts = CreateCancellationTokenSource(context, args.ActionParam);
        try
        {
            var options = AutoCombatOptions.Load(ReadConfigPath(args.ActionParam));
            var service = CreateService(context.Tasker.Controller, options);
            return service.RunAsync(cts.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("AutoCombat canceled.");
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return false;
        }
    }

    private static AutoCombatService CreateService(IMaaController controller, AutoCombatOptions options)
    {
        var inputAdapter = new MaaControllerAdapter(controller);
        IGameInputController input = new MaaGameController(inputAdapter);
        IGameCapture capture = new MaaGameCapture(controller);

        var enemyRecognizer = new EnemyHealthBarRecognizer(options.Recognition);
        var bossRecognizer = new BossHealthBarRecognizer(options.Recognition);
        var skillRecognizer = new SkillRecognizer(options.Recognition);
        var slotRecognizer = new CurrentSlotRecognizer(options.Recognition);
        var characterRecognizer = new CharacterRecognizer(options.Team);
        ICombatDetector detector = new CombatDetector(
            enemyRecognizer,
            bossRecognizer,
            skillRecognizer,
            slotRecognizer,
            characterRecognizer);

        var generic = new GenericStrategy(options);
        ICharacterStrategyFactory strategyFactory = new CharacterStrategyFactory([generic], generic);
        var debugWriter = new DebugFrameWriter(options, enemyRecognizer, skillRecognizer);

        return new AutoCombatService(capture, input, detector, strategyFactory, options, debugWriter);
    }

    private static CancellationTokenSource CreateCancellationTokenSource<T>(T context, string actionParam)
        where T : IMaaContext
    {
        var timeoutSeconds = ReadTimeoutSeconds(actionParam);
        var cts = timeoutSeconds > 0
            ? new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds))
            : new CancellationTokenSource();

        _ = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                if (context.IsCancellationRequested)
                {
                    await cts.CancelAsync().ConfigureAwait(false);
                    return;
                }

                await Task.Delay(100).ConfigureAwait(false);
            }
        });

        return cts;
    }

    private static string? ReadConfigPath(string actionParam)
    {
        using var doc = ParseActionParam(actionParam);
        if (doc is null)
        {
            return null;
        }

        return doc.RootElement.TryGetProperty("config", out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int ReadTimeoutSeconds(string actionParam)
    {
        using var doc = ParseActionParam(actionParam);
        if (doc is null)
        {
            return 0;
        }

        return doc.RootElement.TryGetProperty("timeoutSeconds", out var value) && value.TryGetInt32(out var seconds)
            ? seconds
            : 0;
    }

    private static JsonDocument? ParseActionParam(string actionParam)
    {
        if (string.IsNullOrWhiteSpace(actionParam) || actionParam == "null")
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(actionParam, new JsonDocumentOptions
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
