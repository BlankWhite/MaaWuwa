using MaaWuwa.Core.Combat;
using OpenCvSharp;

namespace MaaWuwa.Core.Recognition;

public interface ICombatDetector
{
    Task<CombatState> DetectAsync(Mat frame, CancellationToken cancellationToken);
}
