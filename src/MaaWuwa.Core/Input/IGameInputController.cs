namespace MaaWuwa.Core.Input;

public interface IGameInputController
{
    Task PressAsync(GameKey key, CancellationToken cancellationToken);

    Task HoldAsync(GameKey key, TimeSpan duration, CancellationToken cancellationToken);
}
