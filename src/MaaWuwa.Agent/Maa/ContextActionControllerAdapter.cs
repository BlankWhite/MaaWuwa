using System.Text.Json;
using MaaFramework.Binding;
using MaaFramework.Binding.Buffers;
using MaaWuwa.Core.Input;

namespace MaaWuwa.Agent.Maa;

public sealed class ContextActionControllerAdapter : IMaaControllerAdapter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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
        RunAction("Click", new { target = new[] { 640, 360 }, contact = 0 });
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
        RunAction("TouchDown", new { target = new[] { 640, 360, 1, 1 }, contact = button });
        return Task.CompletedTask;
    }

    public Task MouseUpAsync(int button, CancellationToken cancellationToken)
    {
        RunAction("TouchUp", new { contact = button });
        return Task.CompletedTask;
    }

    public Task PressKeyAsync(int keyCode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RunAction("ClickKey", new { key = keyCode });
        return Task.CompletedTask;
    }

    public Task KeyDownAsync(int keyCode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RunAction("KeyDown", new { key = keyCode });
        return Task.CompletedTask;
    }

    public Task KeyUpAsync(int keyCode, CancellationToken cancellationToken)
    {
        RunAction("KeyUp", new { key = keyCode });
        return Task.CompletedTask;
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
