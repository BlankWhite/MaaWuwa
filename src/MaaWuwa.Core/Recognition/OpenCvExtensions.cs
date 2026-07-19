using MaaWuwa.Core.Configuration;
using OpenCvSharp;

namespace MaaWuwa.Core.Recognition;

internal static class OpenCvExtensions
{
    public static Rect ClampTo(this RectOptions rect, Mat frame)
    {
        var x = Math.Clamp(rect.X, 0, Math.Max(0, frame.Width - 1));
        var y = Math.Clamp(rect.Y, 0, Math.Max(0, frame.Height - 1));
        var width = Math.Clamp(rect.Width, 1, frame.Width - x);
        var height = Math.Clamp(rect.Height, 1, frame.Height - y);
        return new Rect(x, y, width, height);
    }
}
