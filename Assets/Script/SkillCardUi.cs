using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Serialization;
using static SkillDataParser;

public class SkillCardUI : MonoBehaviour
{
    [Header("UI ������Ʈ ����")]
    public Image skillIcon;
    [FormerlySerializedAs("Nametxt")]
    public TextMeshProUGUI nameText;
    [FormerlySerializedAs("Combotxt")]
    public TextMeshProUGUI comboText;
    [FormerlySerializedAs("Desctxt")]
    public TextMeshProUGUI descText;
    public Button selectButton;
    [FormerlySerializedAs("nonimage")]
    public Sprite noneImage; // ��ų�� ���� ��� ǥ���� �̹���

    // ������ ���� �Լ�
    public void Setup(SkillData data, System.Action<SkillData> onClickAction)
    {
        // UI
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
            skillIcon.sprite = noneImage; // �⺻ ���������� �����ϰų� �� �̹����� ����
        }

        //��ư Ŭ�� �̺�Ʈ
        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(() => onClickAction(data));
    }
}