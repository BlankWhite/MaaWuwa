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
        var aliveState = _slotRecognizer.DetectAlive(frame);
        var characterName = _characterRecognizer.Detect(currentSlot);
        if (skillState.ChisaForteVisible)
        {
            characterName = "Chisa";
            currentSlot = 3;
        }
        else if (skillState.FeixueForteVisible && (currentSlot <= 0 || currentSlot == 1))
        {
            characterName = "Feixue";
            currentSlot = 1;
        }
        else if ((skillState.LinnaiForteVisible || skillState.LinnaiAcceleratedForteVisible)
                 && (currentSlot <= 0 || currentSlot == 2))
        {
            characterName = "Linnai";
            currentSlot = 2;
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
            FeixueForteStage = skillState.FeixueForteStage,
            FeixueForteVisible = skillState.FeixueForteVisible,
            FeixueForte1FullScore = skillState.FeixueForte1FullScore,
            FeixueForte1NotFullScore = skillState.FeixueForte1NotFullScore,
            FeixueForte2FullScore = skillState.FeixueForte2FullScore,
            FeixueForte2NotFullScore = skillState.FeixueForte2NotFullScore,
            FeixueForte3FullScore = skillState.FeixueForte3FullScore,
            LinnaiForteVisible = skillState.LinnaiForteVisible,
            LinnaiForteFull = skillState.LinnaiForteFull,
            LinnaiAcceleratedForteVisible = skillState.LinnaiAcceleratedForteVisible,
            LinnaiAcceleratedForteFull = skillState.LinnaiAcceleratedForteFull,
            LinnaiForteFullScore = skillState.LinnaiForteFullScore,
            LinnaiForteNotFullScore = skillState.LinnaiForteNotFullScore,
            LinnaiAcceleratedForteFullScore = skillState.LinnaiAcceleratedForteFullScore,
            LinnaiAcceleratedForteNotFullScore = skillState.LinnaiAcceleratedForteNotFullScore,
            CurrentSlot = currentSlot,
            Slot1Alive = aliveState.Slot1Alive,
            Slot2Alive = aliveState.Slot2Alive,
            Slot3Alive = aliveState.Slot3Alive,
            CharacterName = characterName
        });
    }
}
