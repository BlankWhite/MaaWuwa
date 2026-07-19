using MaaWuwa.Core.Configuration;
using OpenCvSharp;

namespace MaaWuwa.Core.Recognition;

public sealed class SkillRecognizer
{
    private readonly RecognitionOptions _options;

    public SkillRecognizer(RecognitionOptions options)
    {
        _options = options;
    }

    public SkillState Detect(Mat frame)
    {
        return new SkillState
        {
            ResonanceReady = IsReady(frame, _options.ResonanceRoi),
            LiberationReady = IsReady(frame, _options.LiberationRoi),
            EchoReady = IsReady(frame, _options.EchoRoi),
            ConcertoFull = false
        };
    }

    public Mat CropRoi(Mat frame, RectOptions rect)
    {
        using var roi = new Mat(frame, rect.ClampTo(frame));
        return roi.Clone();
    }

    private bool IsReady(Mat frame, RectOptions rect)
    {
        using var roi = new Mat(frame, rect.ClampTo(frame));
        using var gray = new Mat();
        Cv2.CvtColor(roi, gray, ColorConversionCodes.BGR2GRAY);
        using var bright = gray.Threshold(_options.SkillReadyThreshold, 255, ThresholdTypes.Binary);
        var brightPixels = Cv2.CountNonZero(bright);
        var brightRatio = brightPixels / (double)Math.Max(1, gray.Rows * gray.Cols);
        return brightRatio > _options.SkillReadyBrightRatio;
    }
}
