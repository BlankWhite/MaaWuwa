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
        var feixue = DetectFeixueForte(frame);
        var linnai = DetectLinnaiForte(frame);
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
            ChisaForteNotFullScore = chisa.NotFullScore,
            FeixueForteStage = feixue.Stage,
            FeixueForteVisible = feixue.Visible,
            FeixueForte1FullScore = feixue.Stage1FullScore,
            FeixueForte1NotFullScore = feixue.Stage1NotFullScore,
            FeixueForte2FullScore = feixue.Stage2FullScore,
            FeixueForte2NotFullScore = feixue.Stage2NotFullScore,
            FeixueForte3FullScore = feixue.Stage3FullScore,
            LinnaiForteVisible = linnai.NormalVisible,
            LinnaiForteFull = linnai.NormalFull,
            LinnaiAcceleratedForteVisible = linnai.AcceleratedVisible,
            LinnaiAcceleratedForteFull = linnai.AcceleratedFull,
            LinnaiForteFullScore = linnai.NormalFullScore,
            LinnaiForteNotFullScore = linnai.NormalNotFullScore,
            LinnaiAcceleratedForteFullScore = linnai.AcceleratedFullScore,
            LinnaiAcceleratedForteNotFullScore = linnai.AcceleratedNotFullScore
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

    private FeixueForteResult DetectFeixueForte(Mat frame)
    {
        using var roi = new Mat(frame, _options.FeixueForteRoi.ClampTo(frame));
        if (roi.Empty())
        {
            return new FeixueForteResult(false, 0, 0, 0, 0, 0, 0);
        }

        var stage1Full = MatchTemplateScoreOrZero(roi, _options.FeixueForte1FullTemplate);
        var stage1NotFull = MatchTemplateScoreOrZero(roi, _options.FeixueForte1NotFullTemplate);
        var stage2Full = MatchTemplateScoreOrZero(roi, _options.FeixueForte2FullTemplate);
        var stage2NotFull = MatchTemplateScoreOrZero(roi, _options.FeixueForte2NotFullTemplate);
        var stage3Full = MatchTemplateScoreOrZero(roi, _options.FeixueForte3FullTemplate);

        var notFullBest = Math.Max(stage1NotFull, stage2NotFull);
        var bestFull = Math.Max(stage1Full, Math.Max(stage2Full, stage3Full));
        var stage = 0;
        if (bestFull >= _options.FeixueForteFullThreshold && bestFull >= notFullBest + _options.FeixueForteFullMargin)
        {
            stage = bestFull == stage3Full ? 3 : bestFull == stage2Full ? 2 : 1;
        }

        var visible = stage > 0 || Math.Max(bestFull, notFullBest) >= 0.70;
        return new FeixueForteResult(visible, stage, stage1Full, stage1NotFull, stage2Full, stage2NotFull, stage3Full);
    }

    private LinnaiForteResult DetectLinnaiForte(Mat frame)
    {
        using var roi = new Mat(frame, _options.LinnaiForteRoi.ClampTo(frame));
        if (roi.Empty())
        {
            return new LinnaiForteResult(false, false, false, false, 0, 0, 0, 0);
        }

        var normalFull = MatchTemplateScoreOrZero(roi, _options.LinnaiForteFullTemplate);
        var normalNotFull = MatchTemplateScoreOrZero(roi, _options.LinnaiForteNotFullTemplate);
        var acceleratedFull = MatchTemplateScoreOrZero(roi, _options.LinnaiAcceleratedForteFullTemplate);
        var acceleratedNotFull = MatchTemplateScoreOrZero(roi, _options.LinnaiAcceleratedForteNotFullTemplate);

        var normalBest = Math.Max(normalFull, normalNotFull);
        var acceleratedBest = Math.Max(acceleratedFull, acceleratedNotFull);
        var normalVisible = normalBest >= _options.LinnaiForteVisibleThreshold
            && normalBest >= acceleratedBest - 0.03;
        var acceleratedVisible = acceleratedBest >= _options.LinnaiForteVisibleThreshold
            && acceleratedBest > normalBest + 0.03;
        var normalIsFull = normalVisible
            && normalFull >= _options.LinnaiForteFullThreshold
            && normalFull >= normalNotFull + _options.LinnaiForteFullMargin;
        var acceleratedIsFull = acceleratedVisible
            && acceleratedFull >= _options.LinnaiForteFullThreshold
            && acceleratedFull >= acceleratedNotFull + _options.LinnaiForteFullMargin;

        return new LinnaiForteResult(
            normalVisible,
            normalIsFull,
            acceleratedVisible,
            acceleratedIsFull,
            normalFull,
            normalNotFull,
            acceleratedFull,
            acceleratedNotFull);
    }

    private static double MatchTemplateScoreOrZero(Mat roi, string templateName)
    {
        var template = LoadTemplate(templateName);
        if (template is null || template.Empty() || roi.Width < template.Width || roi.Height < template.Height)
        {
            return 0;
        }

        return MatchTemplateScore(roi, template);
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

    private sealed record FeixueForteResult(
        bool Visible,
        int Stage,
        double Stage1FullScore,
        double Stage1NotFullScore,
        double Stage2FullScore,
        double Stage2NotFullScore,
        double Stage3FullScore);

    private sealed record LinnaiForteResult(
        bool NormalVisible,
        bool NormalFull,
        bool AcceleratedVisible,
        bool AcceleratedFull,
        double NormalFullScore,
        double NormalNotFullScore,
        double AcceleratedFullScore,
        double AcceleratedNotFullScore);
}
