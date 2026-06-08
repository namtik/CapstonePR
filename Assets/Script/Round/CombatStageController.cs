using UnityEngine;
using UnityEngine.UI;

public class CombatStageController : MonoBehaviour
{
    [Header("배경")]
    [SerializeField] private Image backgroundImage;

    [Header("라운드 타입별 기본 배경 (몬스터 전용 배경이 없을 때 폴백)")]
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite eliteSprite;
    [SerializeField] private Sprite bossSprite;

    public void Initialize(RoundData roundData)
    {
        SwitchBackground(roundData);

        // 새 전투 시스템 사용 중이면 레거시 덱/콤보 초기화 스킵
        if (Battle.NewBattleController.Instance != null)
            return;

        // 덱/손패 초기화
        var cardSystem = FindFirstObjectByType<CardSystem>();
        if (cardSystem != null)
        {
            cardSystem.ResetDeck();
        }

        // 콤보 슬롯 초기화 및 스킬 UI 갱신
        if (ComboSystem.Instance != null)
        {
            ComboSystem.Instance.ResetComboInput();
            ComboSystem.Instance.RefreshSkillUI();
            ComboSystem.Instance.RefreshComboSlotUI();
        }
    }

    /// <summary>라운드 시작 시 타입별 기본 배경을 깔아둔다. 몬스터 스폰 후 ApplyEnemyBackground가 덮어쓸 수 있다.</summary>
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

    /// <summary>
    /// 스폰된 몬스터에 매칭되는 전투 배경을 적용한다.
    /// EnemyData.backgroundSprites 풀에서 null을 제외하고 하나를 랜덤 선택한다.
    /// 풀이 비어 있으면 기존(라운드 타입 기본) 배경을 그대로 유지한다.
    /// SpawnEnemy에서 적 1마리당 1회 호출되므로 매번 새로 뽑혀도 깜빡임이 없다.
    /// </summary>
    public void ApplyEnemyBackground(EnemyData enemy)
    {
        if (backgroundImage == null || enemy == null) return;

        Sprite picked = PickRandom(enemy.backgroundSprites);
        if (picked != null)
            backgroundImage.sprite = picked;
    }

    /// <summary>배열에서 null을 제외하고 하나를 랜덤 선택. 후보가 없으면 null.</summary>
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

    void OnValidate()
    {
        if (backgroundImage == null) Debug.LogWarning("CombatStageController: backgroundImage가 없습니다.");
        if (normalSprite == null) Debug.LogWarning("CombatStageController: normalSprite가 없습니다.");
        if (eliteSprite == null) Debug.LogWarning("CombatStageController: eliteSprite가 없습니다.");
        if (bossSprite == null) Debug.LogWarning("CombatStageController: bossSprite가 없습니다.");
    }
}
