using MaaWuwa.Core.Configuration;
using OpenCvSharp;

namespace MaaWuwa.Core.Recognition;

public sealed class EnemyHealthBarRecognizer
{
    private readonly RecognitionOptions _options;

    public EnemyHealthBarRecognizer(RecognitionOptions options)
    {
        _options = options;
    }

    public bool Detect(Mat frame)
    {
        using var roi = new Mat(frame, _options.EnemyHealthRoi.ClampTo(frame));
        using var mask = CreateRedMask(roi);
        return ContainsHealthBar(mask);
    }

    public Mat CreateDebugMask(Mat frame)
    {
        using var roi = new Mat(frame, _options.EnemyHealthRoi.ClampTo(frame));
        return CreateRedMask(roi);
    }

    private Mat CreateRedMask(Mat source)
    {
        var hsv = new Mat();
        Cv2.CvtColor(source, hsv, ColorConversionCodes.BGR2HSV);

        using var mask1 = new Mat();
        using var mask2 = new Mat();
        var mask = new Mat();

        Cv2.InRange(hsv, new Scalar(0, 120, 80), new Scalar(12, 255, 255), mask1);
        Cv2.InRange(hsv, new Scalar(165, 120, 80), new Scalar(179, 255, 255), mask2);
        Cv2.BitwiseOr(mask1, mask2, mask);
        hsv.Dispose();

        return mask;
    }

    private bool ContainsHealthBar(Mat mask)
    {
        Cv2.FindContours(
            mask,
            out Point[][] contours,
            out _,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        foreach (var contour in contours)
        {
            var rect = Cv2.BoundingRect(contour);
            if (rect.Height < _options.EnemyMinHeight || rect.Height > _options.EnemyMaxHeight)
            {
                continue;
            }

            var aspectRatio = rect.Width / (double)Math.Max(rect.Height, 1);
            if (rect.Width >= _options.EnemyMinWidth && aspectRatio >= _options.EnemyMinAspectRatio)
            {
                return true;
            }
        }

        return false;
    }
}
