using MaaFramework.Binding;
using MaaFramework.Binding.Buffers;
using MaaWuwa.Core.Input;
using OpenCvSharp;

namespace MaaWuwa.Agent.Maa;

public sealed class MaaGameCapture : IGameCapture
{
    private readonly IMaaController _controller;

    public MaaGameCapture(IMaaController controller)
    {
        _controller = controller;
    }

    public Task<Mat> CaptureAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var status = _controller.Screencap().Wait();
        if (status != MaaJobStatus.Succeeded)
        {
            throw new InvalidOperationException($"Maa screencap failed: {status}");
        }

        using var image = new MaaImageBuffer();
        if (!_controller.GetCachedImage(image))
        {
            throw new InvalidOperationException("Maa controller has no cached image after screencap.");
        }

        if (!image.TryGetEncodedData(out byte[]? encoded) || encoded.Length == 0)
        {
            throw new InvalidOperationException("Failed to read encoded image data from Maa image buffer.");
        }

        var frame = Cv2.ImDecode(encoded, ImreadModes.Color);
        if (frame.Empty())
        {
            frame.Dispose();
            throw new InvalidOperationException("OpenCvSharp failed to decode Maa screenshot.");
        }

        return Task.FromResult(frame);
    }
}
