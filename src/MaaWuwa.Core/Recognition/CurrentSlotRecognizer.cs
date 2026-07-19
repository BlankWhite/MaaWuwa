using MaaWuwa.Core.Configuration;
using OpenCvSharp;

namespace MaaWuwa.Core.Recognition;

public sealed class CurrentSlotRecognizer
{
    private readonly RecognitionOptions _options;

    public CurrentSlotRecognizer(RecognitionOptions options)
    {
        _options = options;
    }

    public int Detect(Mat frame)
    {
        var scores = new[]
        {
            Score(frame, _options.Slot1Roi),
            Score(frame, _options.Slot2Roi),
            Score(frame, _options.Slot3Roi)
        };

        var maxIndex = Array.IndexOf(scores, scores.Max());
        return scores[maxIndex] >= _options.CurrentSlotBrightRatio ? maxIndex + 1 : -1;
    }

    private static double Score(Mat frame, RectOptions rect)
    {
        using var roi = new Mat(frame, rect.ClampTo(frame));
        using var gray = new Mat();
        Cv2.CvtColor(roi, gray, ColorConversionCodes.BGR2GRAY);
        using var bright = gray.Threshold(190, 255, ThresholdTypes.Binary);
        return Cv2.CountNonZero(bright) / (double)Math.Max(1, gray.Rows * gray.Cols);
    }
}
