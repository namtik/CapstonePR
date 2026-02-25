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

    // 데이터 세팅 함수
    public void Setup(SkillData data, System.Action<SkillData> onClickAction)
    {
        // UI
        Nametxt.text = data.name;
        Desctxt.text = data.description;
        Combotxt.text = $"Combo: {data.combo}";

        if (data.skillIcon != null)
            skillIcon.sprite = data.skillIcon;

        //버튼 클릭 이벤트
        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(() => onClickAction(data));
    }
}