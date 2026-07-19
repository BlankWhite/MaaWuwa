namespace MaaWuwa.Core.Input;

public interface IMaaControllerAdapter
{
    Task ClickLeftMouseAsync(CancellationToken cancellationToken);

    Task ClickMiddleMouseAsync(CancellationToken cancellationToken);

    Task MouseDownAsync(int button, CancellationToken cancellationToken);

    Task MouseUpAsync(int button, CancellationToken cancellationToken);

    Task PressKeyAsync(int keyCode, CancellationToken cancellationToken);

    Task KeyDownAsync(int keyCode, CancellationToken cancellationToken);

    Task KeyUpAsync(int keyCode, CancellationToken cancellationToken);
}
