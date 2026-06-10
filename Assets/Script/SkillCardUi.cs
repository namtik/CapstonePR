using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Serialization;
using static SkillDataParser;

// 스킬 선택지 카드 한 장의 UI를 구성한다
public class SkillCardUI : MonoBehaviour
{
    [Header("UI ������Ʈ ����")]
    public Image skillIcon; // 스킬 아이콘 이미지
    [FormerlySerializedAs("Nametxt")]
    public TextMeshProUGUI nameText; // 스킬 이름 텍스트
    [FormerlySerializedAs("Combotxt")]
    public TextMeshProUGUI comboText; // 콤보 커맨드 텍스트
    [FormerlySerializedAs("Desctxt")]
    public TextMeshProUGUI descText; // 스킬 설명 텍스트
    public Button selectButton; // 선택 버튼
    [FormerlySerializedAs("nonimage")]
    public Sprite noneImage; // 아이콘 없을 때 표시할 대체 이미지

    // 스킬 데이터로 카드 UI와 선택 콜백을 설정한다
    public void Setup(SkillData data, System.Action<SkillData> onClickAction)
    {
        nameText.text = data.name;
        descText.text = data.description;
        comboText.text = $"Combo: {data.combo}";

        if (data.skillIcon != null)
        {
            skillIcon.sprite = data.skillIcon;
        }
        else
        {
            Debug.LogWarning($"[SkillCardUI] ��ų �������� �����ϴ�: {data.name}");
            skillIcon.sprite = noneImage;
        }

        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(() => onClickAction(data));
    }
}