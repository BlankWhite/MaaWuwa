using MaaWuwa.Core.Combat;
using OpenCvSharp;

namespace MaaWuwa.Core.Recognition;

public sealed class CombatDetector : ICombatDetector
{
    private readonly EnemyHealthBarRecognizer _enemyRecognizer;
    private readonly BossHealthBarRecognizer _bossRecognizer;
    private readonly SkillRecognizer _skillRecognizer;
    private readonly CurrentSlotRecognizer _slotRecognizer;
    private readonly CharacterRecognizer _characterRecognizer;

    public CombatDetector(
        EnemyHealthBarRecognizer enemyRecognizer,
        BossHealthBarRecognizer bossRecognizer,
        SkillRecognizer skillRecognizer,
        CurrentSlotRecognizer slotRecognizer,
        CharacterRecognizer characterRecognizer)
    {
        _enemyRecognizer = enemyRecognizer;
        _bossRecognizer = bossRecognizer;
        _skillRecognizer = skillRecognizer;
        _slotRecognizer = slotRecognizer;
        _characterRecognizer = characterRecognizer;
    }

    public Task<CombatState> DetectAsync(Mat frame, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var enemyFound = _enemyRecognizer.Detect(frame);
        var bossFound = _bossRecognizer.Detect(frame);
        var skillState = _skillRecognizer.Detect(frame);
        var currentSlot = _slotRecognizer.Detect(frame);
        var characterName = skillState.ChisaForteVisible ? "Chisa" : _characterRecognizer.Detect(currentSlot);
        if (skillState.ChisaForteVisible)
        {
            currentSlot = 3;
        }

        return Task.FromResult(new CombatState
        {
            EnemyFound = enemyFound || bossFound,
            BossFound = bossFound,
            HasTarget = enemyFound || bossFound,
            ResonanceReady = skillState.ResonanceReady,
            LiberationReady = skillState.LiberationReady,
            EchoReady = skillState.EchoReady,
            ConcertoFull = skillState.ConcertoFull,
            ChisaForteFull = skillState.ChisaForteFull,
            ChisaForteVisible = skillState.ChisaForteVisible,
            ChisaForteFullScore = skillState.ChisaForteFullScore,
            ChisaForteNotFullScore = skillState.ChisaForteNotFullScore,
            ConcertoRatio = skillState.ConcertoRatio,
            CurrentSlot = currentSlot,
            CharacterName = characterName
        });
    }
}
