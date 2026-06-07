using UnityEngine;
using UnityEngine.UI;
using TMPro;
using static SkillDataParser;

public class SkillCardUI : MonoBehaviour
{
    [Header("UI ������Ʈ ����")]
    public Image skillIcon;
    public TextMeshProUGUI Nametxt;
    public TextMeshProUGUI Combotxt;
    public TextMeshProUGUI Desctxt;
    public Button selectButton;
    public Sprite nonimage; // ��ų�� ���� ��� ǥ���� �̹���

    // ������ ���� �Լ�
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
            Debug.LogWarning($"[SkillCardUI] ��ų �������� �����ϴ�: {data.name}");
            skillIcon.sprite = nonimage; // �⺻ ���������� �����ϰų� �� �̹����� ����
        }

        //��ư Ŭ�� �̺�Ʈ
        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(() => onClickAction(data));
    }
}