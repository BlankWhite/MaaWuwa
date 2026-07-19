using MaaWuwa.Core.Combat;
using MaaWuwa.Core.Configuration;
using OpenCvSharp;

namespace MaaWuwa.Core.Recognition;

public sealed class DebugFrameWriter
{
    private readonly AutoCombatOptions _options;
    private readonly EnemyHealthBarRecognizer _enemyRecognizer;
    private readonly SkillRecognizer _skillRecognizer;

    public DebugFrameWriter(
        AutoCombatOptions options,
        EnemyHealthBarRecognizer enemyRecognizer,
        SkillRecognizer skillRecognizer)
    {
        _options = options;
        _enemyRecognizer = enemyRecognizer;
        _skillRecognizer = skillRecognizer;
    }

    public void Write(Mat frame, CombatContext context)
    {
        if (!_options.EnableDebugCapture || context.FrameIndex % 10 != 0)
        {
            return;
        }

        Directory.CreateDirectory(_options.DebugDirectory);
        var prefix = Path.Combine(_options.DebugDirectory, $"frame-{context.FrameIndex:D5}");

        Cv2.ImWrite($"{prefix}.png", frame);
        using (var mask = _enemyRecognizer.CreateDebugMask(frame))
        {
            Cv2.ImWrite($"{prefix}-enemy-mask.png", mask);
        }

        using (var resonance = _skillRecognizer.CropRoi(frame, _options.Recognition.ResonanceRoi))
        using (var liberation = _skillRecognizer.CropRoi(frame, _options.Recognition.LiberationRoi))
        using (var echo = _skillRecognizer.CropRoi(frame, _options.Recognition.EchoRoi))
        {
            Cv2.ImWrite($"{prefix}-resonance-roi.png", resonance);
            Cv2.ImWrite($"{prefix}-liberation-roi.png", liberation);
            Cv2.ImWrite($"{prefix}-echo-roi.png", echo);
        }
    }
}
