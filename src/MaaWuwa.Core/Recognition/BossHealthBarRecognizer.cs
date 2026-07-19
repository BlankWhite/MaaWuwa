using MaaWuwa.Core.Configuration;
using OpenCvSharp;

namespace MaaWuwa.Core.Recognition;

public sealed class BossHealthBarRecognizer
{
    private readonly RecognitionOptions _options;

    public BossHealthBarRecognizer(RecognitionOptions options)
    {
        _options = options;
    }

    public bool Detect(Mat frame)
    {
        using var roi = new Mat(frame, _options.BossHealthRoi.ClampTo(frame));
        using var hsv = new Mat();
        Cv2.CvtColor(roi, hsv, ColorConversionCodes.BGR2HSV);

        using var mask1 = new Mat();
        using var mask2 = new Mat();
        using var mask = new Mat();
        Cv2.InRange(hsv, new Scalar(0, 90, 70), new Scalar(12, 255, 255), mask1);
        Cv2.InRange(hsv, new Scalar(165, 90, 70), new Scalar(179, 255, 255), mask2);
        Cv2.BitwiseOr(mask1, mask2, mask);

        Cv2.FindContours(mask, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        return contours.Any(contour =>
        {
            var rect = Cv2.BoundingRect(contour);
            var aspectRatio = rect.Width / (double)Math.Max(rect.Height, 1);
            return rect.Width >= 80 && rect.Height is >= 3 and <= 20 && aspectRatio >= 5.0;
        });
    }
}
