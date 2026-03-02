using UnityEngine;
using UnityEngine.UI;

public class CombatStageController : MonoBehaviour
{
    [Header("배경")]
    [SerializeField] private Image backgroundImage;

    [Header("배경 스프라이트")]
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite eliteSprite;
    [SerializeField] private Sprite bossSprite;

    public void Initialize(RoundData roundData)
    {
        SwitchBackground(roundData);
        if (ComboSystem.Instance != null)
        {
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
        if (backgroundImage == null) Debug.LogWarning("CombatStageController: backgroundImage가 없습니다.");
        if (normalSprite == null) Debug.LogWarning("CombatStageController: normalSprite가 없습니다.");
        if (eliteSprite == null) Debug.LogWarning("CombatStageController: eliteSprite가 없습니다.");
        if (bossSprite == null) Debug.LogWarning("CombatStageController: bossSprite가 없습니다.");
    }
}
