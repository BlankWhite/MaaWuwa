using MaaFramework.Binding;
using MaaWuwa.Core.Input;

namespace MaaWuwa.Agent.Maa;

public sealed class MaaControllerAdapter : IMaaControllerAdapter
{
    private const int CenterX = 640;
    private const int CenterY = 360;

    private readonly IMaaController _controller;

    public MaaControllerAdapter(IMaaController controller)
    {
        _controller = controller;
    }

    public Task ClickLeftMouseAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return RunJobAsync(() => _controller.Click(CenterX, CenterY, contact: 0), cancellationToken);
    }

    public Task ClickRightMouseAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return RunJobAsync(() => _controller.Click(CenterX, CenterY, contact: 1), cancellationToken);
    }

    public Task ClickMiddleMouseAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return RunJobAsync(() => _controller.Click(CenterX, CenterY, contact: 2), cancellationToken);
    }

    public Task MouseDownAsync(int button, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return RunJobAsync(() => _controller.TouchDown(button, CenterX, CenterY), cancellationToken);
    }

    public Task MouseUpAsync(int button, CancellationToken cancellationToken)
    {
        return RunJobAsync(() => _controller.TouchUp(button), cancellationToken);
    }

    public Task PressKeyAsync(int keyCode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return RunJobAsync(() => _controller.ClickKey(keyCode), cancellationToken);
    }

    public Task KeyDownAsync(int keyCode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return RunJobAsync(() => _controller.KeyDown(keyCode), cancellationToken);
    }

    public Task KeyUpAsync(int keyCode, CancellationToken cancellationToken)
    {
        return RunJobAsync(() => _controller.KeyUp(keyCode), cancellationToken);
    }

    private static Task RunJobAsync(Func<MaaJob> post, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var status = post().Wait();
        if (status != MaaJobStatus.Succeeded)
        {
            throw new InvalidOperationException($"Maa controller job failed: {status}");
        }

        return Task.CompletedTask;
    }
}
