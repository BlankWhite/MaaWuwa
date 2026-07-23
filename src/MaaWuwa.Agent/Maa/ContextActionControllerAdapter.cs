using System.Diagnostics;
using System.Text.Json;
using MaaFramework.Binding;
using MaaFramework.Binding.Buffers;
using MaaWuwa.Core.Input;

namespace MaaWuwa.Agent.Maa;

public sealed class ContextActionControllerAdapter : IMaaControllerAdapter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] X11KeyScriptCandidates =
    [
        "tools/x11_key.sh",
        "/home/shulk/src/MaaWuwa/tools/x11_key.sh"
    ];
    private static readonly string[] X11ClickScriptCandidates =
    [
        "tools/x11_click.sh",
        "/home/shulk/src/MaaWuwa/tools/x11_click.sh"
    ];

    private readonly IMaaContext _context;
    private readonly IMaaRectBuffer _box;

    public ContextActionControllerAdapter(IMaaContext context, IMaaRectBuffer box)
    {
        _context = context;
        _box = box;
    }

    public Task ClickLeftMouseAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryRunX11Click(640, 360))
        {
            RunAction("Click", new { target = new[] { 640, 360 }, contact = 0 });
        }
        return Task.CompletedTask;
    }

    public Task ClickRightMouseAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RunAction("Click", new { target = new[] { 640, 360 }, contact = 1 });
        return Task.CompletedTask;
    }

    public Task ClickMiddleMouseAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RunAction("Click", new { target = new[] { 640, 360 }, contact = 2 });
        return Task.CompletedTask;
    }

    public Task MouseDownAsync(int button, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = button == 0 && TryRunX11Click(640, 360, "down");
        RunAction("TouchDown", new { target = new[] { 640, 360, 1, 1 }, contact = button });
        return Task.CompletedTask;
    }

    public Task MouseUpAsync(int button, CancellationToken cancellationToken)
    {
        _ = button == 0 && TryRunX11Click(640, 360, "up");
        RunAction("TouchUp", new { contact = button });
        return Task.CompletedTask;
    }

    public Task PressKeyAsync(int keyCode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = TryRunX11Key(KeyName(keyCode), "click");
        RunAction("ClickKey", new { key = keyCode });
        return Task.CompletedTask;
    }

    public Task KeyDownAsync(int keyCode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = TryRunX11Key(KeyName(keyCode), "down");
        RunAction("KeyDown", new { key = keyCode });
        return Task.CompletedTask;
    }

    public Task KeyUpAsync(int keyCode, CancellationToken cancellationToken)
    {
        _ = TryRunX11Key(KeyName(keyCode), "up");
        RunAction("KeyUp", new { key = keyCode });
        return Task.CompletedTask;
    }

    private static bool TryRunX11Click(int x, int y, string mode = "click")
    {
        var script = ResolveFirstExisting(X11ClickScriptCandidates);
        return script is not null && (mode == "click"
            ? RunProcess(script, [x.ToString(), y.ToString()])
            : RunProcess(script, [$"--{mode}", x.ToString(), y.ToString()]));
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
            'Q' => "q",
            'E' => "e",
            'R' => "r",
            '1' => "1",
            '2' => "2",
            '3' => "3",
            _ when key is >= 'A' and <= 'Z' => ((char)char.ToLowerInvariant((char)key)).ToString(),
            _ when key is >= '0' and <= '9' => ((char)key).ToString(),
            _ => key.ToString()
        };
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
        catch
        {
            return false;
        }
    }

    private void RunAction(string type, object param)
    {
        var json = JsonSerializer.Serialize(param, JsonOptions);
        using var detail = _context.RunActionDirect(type, json, _box);
        if (detail is null || !detail.IsSucceeded)
        {
            throw new InvalidOperationException($"Maa action failed: {type} {json} detail={detail}");
        }
    }
}
