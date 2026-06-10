using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Battle;
using Battle.Card;
using static SkillDataParser;

public class SkillListItemUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI 참조")]
    public TextMeshProUGUI nameText;   // 스킬 이름 텍스트
    public Transform commandIconContainer;   // 속성 아이콘이 들어갈 부모

    [Header("커맨드 속성 아이콘 매핑 (Q/W/E/R)")]
    public Sprite iconQ;   // Q 속성 아이콘
    public Sprite iconW;   // W 속성 아이콘
    public Sprite iconE;   // E 속성 아이콘
    public Sprite iconR;   // R 속성 아이콘

    private GameObject tooltipObj;   // 생성된 툴팁 오브젝트

    // 기존 SkillData로 이름/아이콘/툴팁을 구성한다
    public void Setup(SkillData skill)
    {
        if (nameText != null) nameText.text = skill.name;
        CreateCommandIcons(skill.combo);
        CreateTooltip(skill);
    }

    // 새 전투 시스템의 ComboSkillDef로 표시를 구성하고 쿨다운 상태를 반영한다
    public void SetupForCombo(ComboSkillDef combo, int cooldownRemaining,
        Sprite fireSp, Sprite waterSp, Sprite windSp, Sprite earthSp)
    {
        if (combo == null) return;

        bool onCooldown = cooldownRemaining > 0;

        if (nameText != null)
            nameText.text = onCooldown
                ? $"{combo.displayName}  ({cooldownRemaining})"
                : combo.displayName;

        CreateComboElementIcons(combo, fireSp, waterSp, windSp, earthSp);

        var canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = onCooldown ? 0.4f : 1f;
    }

    // 콤보의 3슬롯 속성에 맞춰 아이콘들을 생성해 컨테이너에 배치한다
    void CreateComboElementIcons(ComboSkillDef combo,
        Sprite fireSp, Sprite waterSp, Sprite windSp, Sprite earthSp)
    {
        if (commandIconContainer == null) return;

        foreach (Transform child in commandIconContainer)
            Destroy(child.gameObject);

        CardElement[] slots = { combo.slot1, combo.slot2, combo.slot3 };
        foreach (var element in slots)
        {
            Sprite s = element switch
            {
                CardElement.Fire    => fireSp,
                CardElement.Water   => waterSp,
                CardElement.Wind    => windSp,
                CardElement.Earth   => earthSp,
                _ => null
            };

            GameObject iconObj = new GameObject($"Element_{element}");
            iconObj.transform.SetParent(commandIconContainer, false);
            RectTransform rect = iconObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(50f, 50f);
            Image img = iconObj.AddComponent<Image>();
            img.raycastTarget = false;

            if (s != null)
            {
                img.sprite = s;
                img.color = Color.white;
            }
            else
            {
                img.color = ColorForElement(element);
            }
        }
    }

    // 속성에 대응하는 기본 색상을 반환한다(스프라이트 없을 때 대체용)
    static Color ColorForElement(CardElement element) => element switch
    {
        CardElement.Fire    => new Color(0.95f, 0.55f, 0.40f, 1f),
        CardElement.Water   => new Color(0.50f, 0.75f, 0.95f, 1f),
        CardElement.Wind    => new Color(0.60f, 0.90f, 0.60f, 1f),
        CardElement.Earth   => new Color(0.85f, 0.70f, 0.45f, 1f),
        CardElement.Neutral => new Color(0.70f, 0.70f, 0.70f, 1f),
        _ => new Color(0.5f, 0.5f, 0.5f, 1f)
    };

    // 기존 콤보 문자열(Q/W/E/R)에 맞춰 속성 아이콘들을 생성한다
    private void CreateCommandIcons(string combo)
    {
        if (commandIconContainer == null || string.IsNullOrEmpty(combo)) return;

        foreach (Transform child in commandIconContainer)
            Destroy(child.gameObject);

        combo = combo.ToLower();

        foreach (char c in combo)
        {
            GameObject iconObj = new GameObject($"Element_{c}");
            iconObj.transform.SetParent(commandIconContainer, false);

            RectTransform rect = iconObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(50f, 50f);

            Image img = iconObj.AddComponent<Image>();

            Sprite elementSprite = GetElementSprite(c);
            if (elementSprite != null)
            {
                img.sprite = elementSprite;
            }
            else
            {
                img.color = Color.gray;
            }
        }
    }

    // 콤보 문자에 대응하는 속성 아이콘 스프라이트를 반환한다
    private Sprite GetElementSprite(char c)
    {
        switch (c)
        {
            case 'q': return iconQ;
            case 'w': return iconW;
            case 'e': return iconE;
            case 'r': return iconR;
            default: return null;
        }
    }

    // 포인터 진입 시 툴팁을 표시한다
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (tooltipObj != null) tooltipObj.SetActive(true);
    }

    // 포인터 이탈 시 툴팁을 숨긴다
    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltipObj != null) tooltipObj.SetActive(false);
    }

    // 스킬 정보를 담은 툴팁 UI를 동적으로 생성한다
    private void CreateTooltip(SkillData skillData)
    {
        tooltipObj = new GameObject("Tooltip");
        tooltipObj.transform.SetParent(transform, false);

        RectTransform tooltipRect = tooltipObj.AddComponent<RectTransform>();
        tooltipRect.anchorMin = new Vector2(0f, 0.5f);
        tooltipRect.anchorMax = new Vector2(0f, 0.5f);
        tooltipRect.pivot = new Vector2(1f, 0.5f);
        tooltipRect.anchoredPosition = new Vector2(-15f, 0f);
        tooltipRect.sizeDelta = new Vector2(200f, 150f);

        Image bgImage = tooltipObj.AddComponent<Image>();
        bgImage.color = new Color(0.9f, 0.7f, 0.3f, 1f);

        Outline bgOutline = tooltipObj.AddComponent<Outline>();
        bgOutline.effectColor = Color.black;
        bgOutline.effectDistance = new Vector2(2, -2);

        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(tooltipObj.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.sizeDelta = new Vector2(-10f, 40f);
        titleRect.anchoredPosition = new Vector2(0f, -5f);

        Text titleText = titleObj.AddComponent<Text>();
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 20;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = Color.black;
        titleText.fontStyle = FontStyle.Bold;
        titleText.text = skillData.name;

        GameObject descObj = new GameObject("Description");
        descObj.transform.SetParent(tooltipObj.transform, false);
        RectTransform descRect = descObj.AddComponent<RectTransform>();
        descRect.anchorMin = Vector2.zero;
        descRect.anchorMax = Vector2.one;
        descRect.sizeDelta = new Vector2(-10f, -50f);
        descRect.anchoredPosition = new Vector2(0f, -10f);

        Text descText = descObj.AddComponent<Text>();
        descText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        descText.fontSize = 16;
        descText.alignment = TextAnchor.MiddleCenter;
        descText.color = Color.black;
        descText.text = $"콤보: {skillData.combo}\n\n{skillData.description}";

        tooltipObj.SetActive(false);
    }
}
