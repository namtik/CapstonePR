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

    [Header("각성 콤보 슬롯")]
    [SerializeField] private Sprite awakenSlotBackground; // 슬롯 배경. 비우면 Resources/UI/UI_Awakeskillslot_BG
    [SerializeField] private Sprite awakenSlotFrame; // 슬롯 프레임. 비우면 Resources/UI/UI_AwakeSkillSlot_Frame
    [SerializeField] private Sprite awakenCooldownIcon; // 쿨타임 아이콘. 비우면 Resources/UI/UI_Cooldown_Icon
    [SerializeField, Range(0.05f, 1f)] private float awakenUsedAlpha = 0.5f; // 사용 후 슬롯 불투명도

    [Header("각성 콤보 이름")]
    [SerializeField] private TMP_FontAsset awakenNameFont; // 비우면 기존 폰트 유지
    [SerializeField] private float awakenNameFontSize = 24f; // 이름 크기
    [SerializeField] private Color awakenNameColor = Color.white; // 이름 색
    [SerializeField] private FontStyles awakenNameFontStyle = FontStyles.Bold; // 이름 스타일
    [SerializeField] private TextAlignmentOptions awakenNameAlignment = TextAlignmentOptions.MidlineLeft; // 이름 정렬

    [Header("각성 쿨타임 숫자")]
    [SerializeField] private float awakenCooldownFontSize = 28f; // 쿨타임 숫자 크기
    [SerializeField] private Color awakenCooldownColor = Color.white; // 쿨타임 숫자 색
    [SerializeField] private FontStyles awakenCooldownFontStyle = FontStyles.Bold; // 쿨타임 숫자 스타일

    private GameObject tooltipObj;   // 생성된 툴팁 오브젝트
    private bool _awakenVisualReady; // 각성 슬롯 구성 완료
    private CanvasGroup _awakenSlotGroup; // 슬롯만 흐리게 하는 그룹(쿨타임 표시는 제외)
    private Image _awakenBgImage; // 슬롯 배경
    private Image _awakenFrameImage; // 슬롯 프레임
    private Image _awakenCooldownImage; // 모래시계
    private TextMeshProUGUI _awakenCooldownText; // 남은 쿨타임
    private float _awakenElementIconSize = 50f; // 오른쪽 원소 아이콘 한 변

    // HUD 인스펙터에서 이름·쿨타임 글자 모양을 덮어쓴다
    public void ApplyAwakenTextStyle(
        TMP_FontAsset nameFont, float nameSize, Color nameColor, FontStyles nameStyle, TextAlignmentOptions nameAlignment,
        float cooldownSize, Color cooldownColor, FontStyles cooldownStyle)
    {
        if (nameFont != null) awakenNameFont = nameFont;
        awakenNameFontSize = nameSize;
        awakenNameColor = nameColor;
        awakenNameFontStyle = nameStyle;
        awakenNameAlignment = nameAlignment;
        awakenCooldownFontSize = cooldownSize;
        awakenCooldownColor = cooldownColor;
        awakenCooldownFontStyle = cooldownStyle;
    }

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

        EnsureAwakenComboVisual();
        ApplyAwakenText();

        if (nameText != null)
            nameText.text = string.IsNullOrWhiteSpace(combo.displayName) ? "콤보 스킬" : combo.displayName;

        CreateComboElementIcons(combo, fireSp, waterSp, windSp, earthSp);

        bool onCooldown = cooldownRemaining > 0;
        if (_awakenSlotGroup != null)
            _awakenSlotGroup.alpha = onCooldown ? awakenUsedAlpha : 1f;

        if (_awakenCooldownImage != null)
            _awakenCooldownImage.gameObject.SetActive(onCooldown);
        if (_awakenCooldownText != null)
        {
            _awakenCooldownText.gameObject.SetActive(onCooldown);
            if (onCooldown) _awakenCooldownText.text = cooldownRemaining.ToString();
        }
    }

    const string AwakenSlotBgPath = "UI/UI_Awakeskillslot_BG";
    const string AwakenSlotFramePath = "UI/UI_AwakeSkillSlot_Frame";
    const string AwakenCooldownIconPath = "UI/UI_Cooldown_Icon";
    const float CooldownIconWidth = 20f; // 모래시계 너비
    const float CooldownIconHeight = 24f; // 모래시계 높이

    // 배경·프레임·이름·원소·쿨타임 아이콘을 한 번만 구성한다
    void EnsureAwakenComboVisual()
    {
        if (_awakenVisualReady) return;
        _awakenVisualReady = true;

        var root = (RectTransform)transform;
        var rootLayout = GetComponent<HorizontalLayoutGroup>();
        if (rootLayout != null) rootLayout.enabled = false;
        var rootImage = GetComponent<Image>();
        if (rootImage != null) rootImage.enabled = false;

        var visualGo = new GameObject("SlotVisual", typeof(RectTransform), typeof(CanvasGroup));
        visualGo.transform.SetParent(transform, false);
        var visual = (RectTransform)visualGo.transform;
        StretchFull(visual);
        _awakenSlotGroup = visualGo.GetComponent<CanvasGroup>();
        _awakenSlotGroup.alpha = 1f;

        _awakenBgImage = CreateStretchImage("SlotBg", visual);
        _awakenFrameImage = CreateStretchImage("SlotFrame", visual);
        _awakenBgImage.sprite = LoadAwakenSprite(ref awakenSlotBackground, AwakenSlotBgPath);
        _awakenFrameImage.sprite = LoadAwakenSprite(ref awakenSlotFrame, AwakenSlotFramePath);
        _awakenBgImage.preserveAspect = false;
        _awakenFrameImage.preserveAspect = false;

        Sprite frame = _awakenFrameImage.sprite;
        if (frame != null && frame.rect.height > 1f)
        {
            float aspect = frame.rect.width / frame.rect.height;
            float width = 400f;
            float height = width / aspect;
            if (height < 44f)
            {
                height = 44f;
                width = height * aspect;
            }
            root.sizeDelta = new Vector2(width, height);
        }

        if (nameText != null)
            nameText.transform.SetParent(visual, false);
        if (commandIconContainer != null)
            commandIconContainer.SetParent(visual, false);

        float heightNow = root.sizeDelta.y;
        float pad = Mathf.Max(8f, heightNow * 0.14f);
        _awakenElementIconSize = Mathf.Max(28f, heightNow - pad * 2f);
        float iconsWidth = _awakenElementIconSize * 3f + 8f;

        if (commandIconContainer is RectTransform icons)
        {
            icons.anchorMin = new Vector2(1f, 0.5f);
            icons.anchorMax = new Vector2(1f, 0.5f);
            icons.pivot = new Vector2(1f, 0.5f);
            icons.anchoredPosition = new Vector2(-pad, 0f);
            icons.sizeDelta = new Vector2(iconsWidth, _awakenElementIconSize);
            var iconLayout = icons.GetComponent<HorizontalLayoutGroup>();
            if (iconLayout != null)
            {
                iconLayout.childAlignment = TextAnchor.MiddleRight;
                iconLayout.spacing = 4f;
            }
        }

        if (nameText != null)
        {
            var nameRect = nameText.rectTransform;
            nameRect.anchorMin = Vector2.zero;
            nameRect.anchorMax = Vector2.one;
            nameRect.offsetMin = new Vector2(pad + 6f, 4f);
            nameRect.offsetMax = new Vector2(-(iconsWidth + pad + 4f), -4f);
            nameRect.localScale = Vector3.one;
        }

        _awakenCooldownImage = CreateSideImage("CooldownIcon", root, CooldownIconWidth, CooldownIconHeight, -10f);
        _awakenCooldownImage.sprite = LoadAwakenSprite(ref awakenCooldownIcon, AwakenCooldownIconPath);
        _awakenCooldownImage.preserveAspect = false;
        _awakenCooldownImage.gameObject.SetActive(false);

        var textGo = new GameObject("CooldownText", typeof(RectTransform));
        textGo.transform.SetParent(transform, false);
        var textRect = (RectTransform)textGo.transform;
        textRect.anchorMin = textRect.anchorMax = new Vector2(0f, 0.5f);
        textRect.pivot = new Vector2(1f, 0.5f);
        textRect.anchoredPosition = new Vector2(-10f - CooldownIconWidth - 6f, 0f);
        textRect.sizeDelta = new Vector2(72f, 40f);
        _awakenCooldownText = textGo.AddComponent<TextMeshProUGUI>();
        _awakenCooldownText.raycastTarget = false;
        _awakenCooldownText.alignment = TextAlignmentOptions.MidlineRight;
        _awakenCooldownText.enableWordWrapping = false;
        _awakenCooldownText.overflowMode = TextOverflowModes.Overflow;
        _awakenCooldownText.gameObject.SetActive(false);
    }

    // 인스펙터에 지정한 이름·쿨타임 글자 모양을 적용한다
    void ApplyAwakenText()
    {
        if (nameText != null)
        {
            if (awakenNameFont != null) nameText.font = awakenNameFont;
            nameText.fontSize = Mathf.Max(1f, awakenNameFontSize);
            nameText.color = awakenNameColor;
            nameText.fontStyle = awakenNameFontStyle;
            nameText.alignment = awakenNameAlignment;
            nameText.enableWordWrapping = false;
            nameText.overflowMode = TextOverflowModes.Ellipsis;
            nameText.raycastTarget = false;
        }

        if (_awakenCooldownText != null)
        {
            if (nameText != null && nameText.font != null)
            {
                _awakenCooldownText.font = nameText.font;
                _awakenCooldownText.fontSharedMaterial = nameText.fontSharedMaterial;
                _awakenCooldownText.fontStyle = nameText.fontStyle;
            }
            _awakenCooldownText.fontSize = Mathf.Max(1f, awakenCooldownFontSize);
            _awakenCooldownText.color = new Color32(0xCE, 0xAE, 0x67, 0xFF);
        }

        if (_awakenCooldownImage != null)
        {
            var iconRect = _awakenCooldownImage.rectTransform;
            iconRect.sizeDelta = new Vector2(CooldownIconWidth, CooldownIconHeight);
            iconRect.anchoredPosition = new Vector2(-10f, 0f);
        }
    }

    // 슬롯 왼쪽에 붙는 쿨타임 아이콘을 만든다
    static Image CreateSideImage(string name, RectTransform parent, float width, float height, float x)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(x, 0f);
        rect.sizeDelta = new Vector2(width, height);
        var image = go.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    static Image CreateStretchImage(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        StretchFull((RectTransform)go.transform);
        var image = go.GetComponent<Image>();
        image.raycastTarget = false;
        image.color = Color.white;
        return image;
    }

    static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    static Sprite LoadAwakenSprite(ref Sprite slot, string resourcePath)
    {
        if (slot != null) return slot;
        slot = Resources.Load<Sprite>(resourcePath);
        if (slot == null)
            Debug.LogWarning("[SkillListItemUI] 스프라이트를 찾지 못했습니다: " + resourcePath);
        return slot;
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
            rect.sizeDelta = new Vector2(_awakenElementIconSize, _awakenElementIconSize);
            Image img = iconObj.AddComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;

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
