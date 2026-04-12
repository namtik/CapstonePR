using UnityEngine;
using UnityEngine.UI;
using TMPro;
using static SkillDataParser;

public class SkillCardUI : MonoBehaviour
{
    [Header("UI 컴포넌트 연결")]
    public Image skillIcon;
    public TextMeshProUGUI Nametxt;
    public TextMeshProUGUI Combotxt;
    public TextMeshProUGUI Desctxt;
    public Button selectButton;
    public Sprite nonimage; // 스킬이 없는 경우 표시할 이미지

    // 데이터 세팅 함수
    public void Setup(SkillData data, System.Action<SkillData> onClickAction)
    {
        // UI
        Nametxt.text = data.name;
        Desctxt.text = data.description;
        Combotxt.text = $"Combo: {data.combo}";

        if (data.skillIcon != null)
        {
            skillIcon.sprite = data.skillIcon;
        }
        else
        {
            Debug.LogWarning($"[SkillCardUI] 스킬 아이콘이 없습니다: {data.name}");
            skillIcon.sprite = nonimage; // 기본 아이콘으로 설정하거나 빈 이미지로 유지
        }

        //버튼 클릭 이벤트
        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(() => onClickAction(data));
    }
}