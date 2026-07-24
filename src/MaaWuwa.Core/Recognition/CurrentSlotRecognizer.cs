using System.Collections.Concurrent;
using MaaWuwa.Core.Configuration;
using OpenCvSharp;

namespace MaaWuwa.Core.Recognition;

public sealed class CurrentSlotRecognizer
{
    private static readonly ConcurrentDictionary<string, Mat?> TemplateCache = new(StringComparer.Ordinal);

    private readonly RecognitionOptions _options;

    public CurrentSlotRecognizer(RecognitionOptions options)
    {
        _options = options;
    }

    public int Detect(Mat frame)
    {
        var textSlot = DetectBySlotText(frame);
        if (textSlot is not null)
        {
            return textSlot.Value;
        }

        var scores = new[]
        {
            Score(frame, _options.Slot1Roi),
            Score(frame, _options.Slot2Roi),
            Score(frame, _options.Slot3Roi)
        };

        var maxIndex = Array.IndexOf(scores, scores.Max());
        return scores[maxIndex] >= _options.CurrentSlotBrightRatio ? maxIndex + 1 : -1;
    }

    public SlotAliveState DetectAlive(Mat frame)
    {
        return new SlotAliveState(
            IsAlive(frame, _options.Slot1Roi),
            IsAlive(frame, _options.Slot2Roi),
            IsAlive(frame, _options.Slot3Roi));
    }

    private int? DetectBySlotText(Mat frame)
    {
        var templates = new[]
        {
            LoadTemplate(_options.Slot1TextTemplate),
            LoadTemplate(_options.Slot2TextTemplate),
            LoadTemplate(_options.Slot3TextTemplate)
        };

        if (templates.Any(template => template is null || template.Empty()))
        {
            return null;
        }

        var rois = new[] {_options.Slot1TextRoi, _options.Slot2TextRoi, _options.Slot3TextRoi};
        var textVisible = new bool[3];
        for (var i = 0; i < 3; i++)
        {
            textVisible[i] = MatchTemplateScore(frame, rois[i], templates[i]!) >= _options.SlotTextThreshold;
        }

        var missing = textVisible
            .Select((visible, index) => new {visible, index})
            .Where(item => !item.visible)
            .Select(item => item.index + 1)
            .ToArray();

        return missing.Length == 1 ? missing[0] : null;
    }

    private bool IsAlive(Mat frame, RectOptions rect)
    {
        using var roi = new Mat(frame, rect.ClampTo(frame));
        if (roi.Empty())
        {
            return false;
        }

        using var hsv = new Mat();
        Cv2.CvtColor(roi, hsv, ColorConversionCodes.BGR2HSV);
        using var mask = new Mat();
        Cv2.InRange(
            hsv,
            new Scalar(0, _options.SlotAliveSaturationThreshold, _options.SlotAliveValueThreshold),
            new Scalar(179, 255, 255),
            mask);
        var ratio = Cv2.CountNonZero(mask) / (double)Math.Max(1, roi.Rows * roi.Cols);
        return ratio >= _options.SlotAliveColorRatio;
    }

    private static double Score(Mat frame, RectOptions rect)
    {
        using var roi = new Mat(frame, rect.ClampTo(frame));
        using var gray = new Mat();
        Cv2.CvtColor(roi, gray, ColorConversionCodes.BGR2GRAY);
        using var bright = gray.Threshold(190, 255, ThresholdTypes.Binary);
        return Cv2.CountNonZero(bright) / (double)Math.Max(1, gray.Rows * gray.Cols);
    }

    private static double MatchTemplateScore(Mat frame, RectOptions rect, Mat template)
    {
        using var roi = new Mat(frame, rect.ClampTo(frame));
        if (roi.Empty() || roi.Width < template.Width || roi.Height < template.Height)
        {
            return 0;
        }

        using var sourceBgr = EnsureBgr(roi);
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
}

public sealed record SlotAliveState(bool Slot1Alive, bool Slot2Alive, bool Slot3Alive);
