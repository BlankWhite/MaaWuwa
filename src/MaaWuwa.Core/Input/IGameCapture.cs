using OpenCvSharp;

namespace MaaWuwa.Core.Input;

public interface IGameCapture
{
    Task<Mat> CaptureAsync(CancellationToken cancellationToken);
}
