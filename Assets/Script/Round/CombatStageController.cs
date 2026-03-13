using UnityEngine;
using UnityEngine.UI;

public class CombatStageController : MonoBehaviour
{
    [Header("���")]
    [SerializeField] private Image backgroundImage;

    [Header("��� ��������Ʈ")]
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite eliteSprite;
    [SerializeField] private Sprite bossSprite;

    public void Initialize(RoundData roundData)
    {
        SwitchBackground(roundData);

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

    void SwitchBackground(RoundData roundData)
    {
        if (backgroundImage == null) return;

        backgroundImage.sprite = roundData switch
        {
            CombatRoundData => normalSprite,
            EliteRoundData => eliteSprite,
            BossRoundData => bossSprite,
            _ => normalSprite
        };
    }


    void OnValidate()
    {
        if (backgroundImage == null) Debug.LogWarning("CombatStageController: backgroundImage�� �����ϴ�.");
        if (normalSprite == null) Debug.LogWarning("CombatStageController: normalSprite�� �����ϴ�.");
        if (eliteSprite == null) Debug.LogWarning("CombatStageController: eliteSprite�� �����ϴ�.");
        if (bossSprite == null) Debug.LogWarning("CombatStageController: bossSprite�� �����ϴ�.");
    }
}
