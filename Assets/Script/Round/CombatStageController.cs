using UnityEngine;
using UnityEngine.UI;

public class CombatStageController : MonoBehaviour
{
    [Header("배경")]
    [SerializeField] private Image backgroundImage; // 배경 이미지 컴포넌트

    [Header("라운드 타입별 기본 배경 (몬스터 전용 배경이 없을 때 폴백)")]
    [SerializeField] private Sprite normalSprite; // 일반 전투 기본 배경
    [SerializeField] private Sprite eliteSprite; // 정예 전투 기본 배경
    [SerializeField] private Sprite bossSprite; // 보스 전투 기본 배경

    // 라운드 시작 시 배경 전환과 덱/콤보 초기화를 수행한다.
    public void Initialize(RoundData roundData)
    {
        SwitchBackground(roundData);

        if (Battle.NewBattleController.Instance != null)
            return;

        var cardSystem = FindFirstObjectByType<CardSystem>();
        if (cardSystem != null)
        {
            cardSystem.ResetDeck();
        }

        if (ComboSystem.Instance != null)
        {
            ComboSystem.Instance.ResetComboInput();
            ComboSystem.Instance.RefreshSkillUI();
            ComboSystem.Instance.RefreshComboSlotUI();
        }
    }

    // 라운드 타입에 맞는 기본 배경을 적용한다.
    void SwitchBackground(RoundData roundData)
    {
        if (backgroundImage == null) return;

        Sprite s = roundData switch
        {
            CombatRoundData => normalSprite,
            EliteRoundData => eliteSprite,
            BossRoundData => bossSprite,
            _ => normalSprite
        };

        if (s != null)
            backgroundImage.sprite = s;
    }

    // 스폰된 몬스터의 배경 풀에서 랜덤 배경을 적용한다(비면 기존 배경 유지).
    public void ApplyEnemyBackground(EnemyData enemy)
    {
        if (backgroundImage == null || enemy == null) return;

        Sprite picked = PickRandom(enemy.backgroundSprites);
        if (picked != null)
            backgroundImage.sprite = picked;
    }

    // 배열에서 null을 제외하고 하나를 랜덤 선택한다(후보가 없으면 null).
    static Sprite PickRandom(Sprite[] list)
    {
        if (list == null) return null;

        int count = 0;
        for (int i = 0; i < list.Length; i++)
            if (list[i] != null) count++;
        if (count == 0) return null;

        int pick = Random.Range(0, count);
        for (int i = 0; i < list.Length; i++)
        {
            if (list[i] == null) continue;
            if (pick == 0) return list[i];
            pick--;
        }
        return null;
    }

    // 에디터에서 필수 배경 참조 누락 여부를 검사한다.
    void OnValidate()
    {
        if (backgroundImage == null) Debug.LogWarning("CombatStageController: backgroundImage가 없습니다.");
        if (normalSprite == null) Debug.LogWarning("CombatStageController: normalSprite가 없습니다.");
        if (eliteSprite == null) Debug.LogWarning("CombatStageController: eliteSprite가 없습니다.");
        if (bossSprite == null) Debug.LogWarning("CombatStageController: bossSprite가 없습니다.");
    }
}
