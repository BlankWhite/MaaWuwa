using System.Collections.Concurrent;
using MaaWuwa.Core.Configuration;
using OpenCvSharp;

namespace MaaWuwa.Core.Recognition;

public sealed class CharacterRecognizer
{
    private static readonly ConcurrentDictionary<string, Mat?> TemplateCache = new(StringComparer.Ordinal);

    private readonly IReadOnlyList<string> _team;
    private readonly RecognitionOptions _options;

    public CharacterRecognizer(IReadOnlyList<string> team, RecognitionOptions options)
    {
        _team = team;
        _options = options;
    }

    public string? Detect(Mat frame, int currentSlot)
    {
        var templateHit = DetectByTemplate(frame, currentSlot);
        if (!string.IsNullOrWhiteSpace(templateHit))
        {
            return templateHit;
        }

        if (currentSlot <= 0 || currentSlot > _team.Count)
        {
            return null;
        }

        return _team[currentSlot - 1];
    }

    private string? DetectByTemplate(Mat frame, int currentSlot)
    {
        if (currentSlot <= 0 || _options.CharacterTemplates.Count == 0)
        {
            return null;
        }

        var roi = GetSlotRoi(currentSlot);
        var bestName = string.Empty;
        var bestScore = 0.0;
        foreach (var (name, templateName) in _options.CharacterTemplates)
        {
            var template = LoadTemplate(templateName);
            if (template is null || template.Empty())
            {
                continue;
            }

            var score = MatchTemplateScore(frame, roi, template);
            if (score > bestScore)
            {
                bestScore = score;
                bestName = name;
            }
        }

        return bestScore >= _options.CharacterTemplateThreshold ? bestName : null;
    }

    private RectOptions GetSlotRoi(int slot)
    {
        return slot switch
        {
            1 => _options.Slot1CharacterRoi,
            2 => _options.Slot2CharacterRoi,
            3 => _options.Slot3CharacterRoi,
            _ => _options.Slot1Roi
        };
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
