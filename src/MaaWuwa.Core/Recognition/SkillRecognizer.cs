using System.Collections.Concurrent;
using MaaWuwa.Core.Configuration;
using OpenCvSharp;

namespace MaaWuwa.Core.Recognition;

public sealed class SkillRecognizer
{
    private static readonly ConcurrentDictionary<string, Mat?> TemplateCache = new(StringComparer.Ordinal);

    private readonly RecognitionOptions _options;

    public SkillRecognizer(RecognitionOptions options)
    {
        _options = options;
    }

    public SkillState Detect(Mat frame)
    {
        var chisa = DetectChisaForte(frame);
        var concertoRatio = GetConcertoRatio(frame);
        return new SkillState
        {
            ResonanceReady = IsReady(frame, _options.ResonanceRoi),
            LiberationReady = IsReady(frame, _options.LiberationRoi),
            EchoReady = IsReady(frame, _options.EchoRoi),
            ConcertoFull = concertoRatio >= _options.ConcertoFullRingRatio,
            ConcertoRatio = concertoRatio,
            ChisaForteFull = chisa.Full,
            ChisaForteVisible = chisa.Visible,
            ChisaForteFullScore = chisa.FullScore,
            ChisaForteNotFullScore = chisa.NotFullScore
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

    private ChisaForteResult DetectChisaForte(Mat frame)
    {
        var fullTemplate = LoadTemplate(_options.ChisaForteFullTemplate);
        if (fullTemplate is null || fullTemplate.Empty())
        {
            return new ChisaForteResult(false, false, 0, 0);
        }

        var notFullTemplate = LoadTemplate(_options.ChisaForteNotFullTemplate);
        using var roi = new Mat(frame, _options.ChisaForteRoi.ClampTo(frame));
        if (roi.Empty() || roi.Width < fullTemplate.Width || roi.Height < fullTemplate.Height)
        {
            return new ChisaForteResult(false, false, 0, 0);
        }

        var fullScore = MatchTemplateScore(roi, fullTemplate);
        var notFullScore = notFullTemplate is null || notFullTemplate.Empty() || roi.Width < notFullTemplate.Width || roi.Height < notFullTemplate.Height
            ? 0
            : MatchTemplateScore(roi, notFullTemplate);
        var bestScore = Math.Max(fullScore, notFullScore);
        var visible = bestScore >= 0.72;
        var full = fullScore >= _options.ChisaForteFullThreshold
            && fullScore >= notFullScore + _options.ChisaForteFullMargin;

        return new ChisaForteResult(visible, full, fullScore, notFullScore);
    }

    private static double MatchTemplateScore(Mat source, Mat template)
    {
        using var sourceBgr = EnsureBgr(source);
        using var templateBgr = EnsureBgr(template);
        using var result = new Mat();
        Cv2.MatchTemplate(sourceBgr, templateBgr, result, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(result, out _, out var maxVal, out _, out _);
        return maxVal;
    }

    private static Mat EnsureBgr(Mat source)
    {
        if (source.Channels() == 3)
        {
            return source.Clone();
        }

        var converted = new Mat();
        if (source.Channels() == 4)
        {
            Cv2.CvtColor(source, converted, ColorConversionCodes.BGRA2BGR);
        }
        else if (source.Channels() == 1)
        {
            Cv2.CvtColor(source, converted, ColorConversionCodes.GRAY2BGR);
        }
        else
        {
            converted = source.Clone();
        }

        return converted;
    }

    private static Mat? LoadTemplate(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return TemplateCache.GetOrAdd(name, static templateName =>
        {
            foreach (var path in EnumerateTemplatePaths(templateName))
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                var image = Cv2.ImRead(path, ImreadModes.Unchanged);
                if (!image.Empty())
                {
                    return image;
                }

                image.Dispose();
            }

            return null;
        });
    }

    private static IEnumerable<string> EnumerateTemplatePaths(string name)
    {
        yield return Path.GetFullPath(name);
        yield return Path.GetFullPath(Path.Combine("assets/resource/image", name));
        yield return Path.GetFullPath(Path.Combine("resource/image", name));
        yield return Path.GetFullPath(Path.Combine("image", name));
    }

    private double GetConcertoRatio(Mat frame)
    {
        using var roi = new Mat(frame, _options.ConcertoRoi.ClampTo(frame));
        if (roi.Empty())
        {
            return 0;
        }

        using var hsv = new Mat();
        Cv2.CvtColor(roi, hsv, ColorConversionCodes.BGR2HSV);

        using var colored = new Mat();
        Cv2.InRange(hsv, new Scalar(0, 45, 70), new Scalar(179, 255, 255), colored);

        using var ringMask = Mat.Zeros(roi.Rows, roi.Cols, MatType.CV_8UC1).ToMat();
        var center = new Point(roi.Cols / 2, roi.Rows / 2);
        var outer = Math.Max(2, (int)Math.Round(Math.Min(roi.Rows, roi.Cols) * 0.48));
        var inner = Math.Max(1, (int)Math.Round(Math.Min(roi.Rows, roi.Cols) * 0.34));
        Cv2.Circle(ringMask, center, outer, Scalar.White, -1);
        Cv2.Circle(ringMask, center, inner, Scalar.Black, -1);

        using var ring = new Mat();
        Cv2.BitwiseAnd(colored, ringMask, ring);
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
        Cv2.MorphologyEx(ring, ring, MorphTypes.Close, kernel);

        var ringPixels = Cv2.CountNonZero(ringMask);
        if (ringPixels <= 0)
        {
            return 0;
        }

        return Cv2.CountNonZero(ring) / (double)ringPixels;
    }

    private sealed record ChisaForteResult(bool Visible, bool Full, double FullScore, double NotFullScore);
}
