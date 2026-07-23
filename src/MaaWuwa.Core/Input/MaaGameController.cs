namespace MaaWuwa.Core.Input;

public sealed class MaaGameController : IGameInputController
{
    private const int VkQ = 'Q';
    private const int VkE = 'E';
    private const int VkR = 'R';
    private const int Vk1 = '1';
    private const int Vk2 = '2';
    private const int Vk3 = '3';

    private readonly IMaaControllerAdapter _controller;

    public MaaGameController(IMaaControllerAdapter controller)
    {
        _controller = controller;
    }

    public Task PressAsync(GameKey key, CancellationToken cancellationToken)
    {
        return key switch
        {
            GameKey.NormalAttack => _controller.ClickLeftMouseAsync(cancellationToken),
            GameKey.Dodge => _controller.ClickRightMouseAsync(cancellationToken),
            GameKey.LockTarget => _controller.ClickMiddleMouseAsync(cancellationToken),
            GameKey.ResonanceSkill => _controller.PressKeyAsync(VkE, cancellationToken),
            GameKey.Liberation => _controller.PressKeyAsync(VkR, cancellationToken),
            GameKey.Echo => _controller.PressKeyAsync(VkQ, cancellationToken),
            GameKey.SwitchCharacter1 => _controller.PressKeyAsync(Vk1, cancellationToken),
            GameKey.SwitchCharacter2 => _controller.PressKeyAsync(Vk2, cancellationToken),
            GameKey.SwitchCharacter3 => _controller.PressKeyAsync(Vk3, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, null)
        };
    }

    public async Task HoldAsync(GameKey key, TimeSpan duration, CancellationToken cancellationToken)
    {
        switch (key)
        {
            case GameKey.NormalAttack:
                await _controller.MouseDownAsync(0, cancellationToken).ConfigureAwait(false);
                try
                {
                    await Task.Delay(duration, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    await _controller.MouseUpAsync(0, CancellationToken.None).ConfigureAwait(false);
                }
                break;
            default:
                var keyCode = ToKeyCode(key);
                await _controller.KeyDownAsync(keyCode, cancellationToken).ConfigureAwait(false);
                try
                {
                    await Task.Delay(duration, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    await _controller.KeyUpAsync(keyCode, CancellationToken.None).ConfigureAwait(false);
                }
                break;
        }
    }

    private static int ToKeyCode(GameKey key)
    {
        return key switch
        {
            GameKey.ResonanceSkill => VkE,
            GameKey.Liberation => VkR,
            GameKey.Echo => VkQ,
            GameKey.SwitchCharacter1 => Vk1,
            GameKey.SwitchCharacter2 => Vk2,
            GameKey.SwitchCharacter3 => Vk3,
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, null)
        };
    }
}
