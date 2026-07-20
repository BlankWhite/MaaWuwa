using System.Diagnostics;
using System.Text.Json;
using MaaFramework.Binding;
using MaaFramework.Binding.Custom;

namespace MaaWuwa.Agent.Actions.Daily;

public sealed class DebugFInteractTickRecognition : IMaaCustomRecognition
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Name { get; set; } = "DebugFInteractTick";

    public bool Analyze<T>(T context, in AnalyzeArgs args, in AnalyzeResults results) where T : IMaaContext
    {
        try
        {
            if (HasFInteract(context, args.Image))
            {
                Console.WriteLine("DebugFInteractTick: f_interact found, press f and finish.");
                PressF(context, args);
                context.OverrideNext(args.NodeName, []);
                results.Box.TrySetValues(0, 0, 1, 1);
                results.Detail.TrySetValue("{\"status\":\"found\"}");
                return true;
            }

            Console.WriteLine("DebugFInteractTick: f_interact not found, left click and retry.");
            ClickCenter(context, args);
            context.OverrideNext(args.NodeName, [args.NodeName]);
            results.Box.TrySetValues(0, 0, 1, 1);
            results.Detail.TrySetValue("{\"status\":\"click_retry\"}");
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            context.OverrideNext(args.NodeName, []);
            return false;
        }
    }

    private static bool HasFInteract(IMaaContext context, MaaFramework.Binding.Buffers.IMaaImageBuffer image)
    {
        var param = JsonSerializer.Serialize(new
        {
            template = "f_interact.png",
            roi = new[] { 851, 348, 108, 48 },
            threshold = 0.75
        }, JsonOptions);
        using var detail = context.RunRecognitionDirect("TemplateMatch", param, image);
        return detail is not null && detail.Hit;
    }

    private static void ClickCenter<T>(T context, AnalyzeArgs args) where T : IMaaContext
    {
        if (RunScript("/home/shulk/src/MaaWuwa/tools/x11_click.sh", ["640", "360"]))
        {
            return;
        }

        using var detail = context.RunActionDirect(
            "Click",
            "{\"target\":[640,360],\"contact\":0}",
            args.Roi);
        if (detail is null || !detail.IsSucceeded)
        {
            throw new InvalidOperationException("DebugFInteractTick click failed.");
        }
    }

    private static void PressF<T>(T context, AnalyzeArgs args) where T : IMaaContext
    {
        if (RunScript("/home/shulk/src/MaaWuwa/tools/x11_key.sh", ["f"]))
        {
            return;
        }

        using var detail = context.RunActionDirect(
            "ClickKey",
            "{\"key\":70}",
            args.Roi);
        if (detail is null || !detail.IsSucceeded)
        {
            throw new InvalidOperationException("DebugFInteractTick press f failed.");
        }
    }

    private static bool RunScript(string fileName, IReadOnlyList<string> arguments)
    {
        if (!File.Exists(fileName))
        {
            return false;
        }

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
            Console.Error.WriteLine($"DebugFInteractTick script failed: {ex.Message}");
            return false;
        }
    }
}
