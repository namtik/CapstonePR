using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class ElementSlotHUD : MonoBehaviour
{
    [Header("Prefab Driven Slot UI")]
    [SerializeField] private string rootObjectName = "ElementSlotRoot";
    [SerializeField] private string combatStageName = "CombatStage";
    [SerializeField] private string combatCanvasName = "Canvas";
    [SerializeField] private GameObject qwerCardPrefab;
    [SerializeField] private Sprite neutralCardSprite;
    [SerializeField] private Vector2 rootAnchoredPosition = new Vector2(0f, 26f);
    [SerializeField] private Vector2 rootSize = new Vector2(880f, 240f);
    [SerializeField] private Vector2 slotSize = new Vector2(180f, 208f);
    [SerializeField] private Vector2 statusBoxSize = new Vector2(180f, 44f);
    [SerializeField] private Vector2 statusBoxOffset = new Vector2(0f, 0f);
    [SerializeField] private Font statusFont;
    [SerializeField] private int statusFontSize = 18;
    [SerializeField] private Vector2 upgradeTextSize = new Vector2(56f, 30f);
    [SerializeField] private Vector2 upgradeTextOffset = new Vector2(-8f, -8f);
    [SerializeField] private int upgradeTextFontSize = 22;
    [SerializeField] private Color upgradeTextColor = new Color(1f, 0.93f, 0.35f, 1f);
    [SerializeField] private Color statusBackgroundColor = new Color(0f, 0f, 0f, 0.7f);
    [SerializeField] private Color cursedStatusBackgroundColor = new Color(0.05f, 0.02f, 0.08f, 0.97f);
    [SerializeField] private Color statusTextColor = Color.white;
    [SerializeField] private float slotAlphaWhenEmpty = 0.2f;
    [SerializeField] private float slotAlphaWhenActive = 1f;
    [SerializeField] private float slotAlphaWhenNeutral = 0.75f;
    [SerializeField] private float slotAlphaWhenCursed = 0.95f;

    private ElementSlotSystem slotSystem;
    private ComboSystem comboSystem;
    private Canvas hudCanvas;
    private RectTransform rootTransform;
    private readonly Image[] slotIcons = new Image[4];
    private readonly Image[] statusBoxes = new Image[4];
    private readonly Text[] statusTexts = new Text[4];
    private readonly Text[] upgradeTexts = new Text[4];
    private readonly ElementSlotSpriteOverride[] slotSpriteOverrides = new ElementSlotSpriteOverride[4];

    void Awake()
    {
        ResolveSystems();
        BuildOrBindUI();
        SuppressLegacyHandUI();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene _, LoadSceneMode __)
    {
        ResolveSystems();
        BuildOrBindUI();
        SuppressLegacyHandUI();
    }

    void Update()
    {
        if (slotSystem == null || hudCanvas == null)
        {
            SetHudVisible(false);
            return;
        }

        SetHudVisible(slotSystem.InBattle);
        if (!slotSystem.InBattle)
            return;

        for (int index = 0; index < 4; index++)
            UpdateSlot(index);
    }

    void ResolveSystems()
    {
        slotSystem = ElementSlotSystem.Instance ?? FindFirstObjectByType<ElementSlotSystem>();
        comboSystem = ComboSystem.Instance ?? FindFirstObjectByType<ComboSystem>();

        if (qwerCardPrefab == null && comboSystem != null)
            qwerCardPrefab = comboSystem.cardPrefab;
    }

    void BuildOrBindUI()
    {
        Canvas targetCanvas = ResolveTargetCanvas();
        if (targetCanvas == null)
            return;

        hudCanvas = targetCanvas;

        Transform existingRoot = hudCanvas.transform.Find(rootObjectName);
        if (existingRoot != null)
        {
            rootTransform = existingRoot as RectTransform;
            CacheOrCreateSlotIcons();
            return;
        }

        GameObject rootObject = new GameObject(rootObjectName);
        rootObject.transform.SetParent(hudCanvas.transform, false);
        rootTransform = rootObject.AddComponent<RectTransform>();
        rootTransform.anchorMin = new Vector2(0.5f, 0f);
        rootTransform.anchorMax = new Vector2(0.5f, 0f);
        rootTransform.pivot = new Vector2(0.5f, 0f);
        rootTransform.anchoredPosition = rootAnchoredPosition;
        rootTransform.sizeDelta = rootSize;

        HorizontalLayoutGroup layout = rootObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 14f;
        layout.padding = new RectOffset(22, 22, 16, 16);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;
        layout.childControlHeight = false;
        layout.childControlWidth = false;

        for (int index = 0; index < 4; index++)
            BuildSlotPanel(index, rootTransform);

        SetHudVisible(false);
    }

    Canvas ResolveTargetCanvas()
    {
        GameObject combatStageObject = GameObject.Find(combatStageName);
        if (combatStageObject == null)
            return FindFirstObjectByType<Canvas>();

        Transform stageTransform = combatStageObject.transform;

        Transform namedCanvas = stageTransform.Find(combatCanvasName);
        if (namedCanvas != null)
        {
            Canvas target = namedCanvas.GetComponent<Canvas>();
            if (target != null)
                return target;
        }

        return combatStageObject.GetComponentInChildren<Canvas>(true);
    }

    void CacheOrCreateSlotIcons()
    {
        for (int index = 0; index < 4; index++)
        {
            string slotName = "Slot_" + ElementSlotSystem.SLOT_KEYS[index];
            Transform slotTransform = rootTransform.Find(slotName);
            if (slotTransform == null)
            {
                BuildSlotPanel(index, rootTransform);
                continue;
            }

            Image iconImage = slotTransform.GetComponent<Image>();
            if (iconImage == null)
                iconImage = slotTransform.gameObject.AddComponent<Image>();

            slotIcons[index] = iconImage;

            RectTransform slotRect = slotTransform as RectTransform;
            if (slotRect != null)
                slotRect.sizeDelta = slotSize;

            EnsureSlotSpriteOverride(index, slotTransform);

            EnsureStatusBox(index, slotTransform);
            EnsureUpgradeText(index, slotTransform);
        }
    }

    void BuildSlotPanel(int index, Transform parent)
    {
        GameObject slotObject;

        // Prefab-first construction keeps slot visuals editable in project assets.
        if (qwerCardPrefab != null)
            slotObject = Instantiate(qwerCardPrefab, parent, false);
        else
            slotObject = new GameObject();

        slotObject.name = "Slot_" + ElementSlotSystem.SLOT_KEYS[index];

        RectTransform slotRect = slotObject.GetComponent<RectTransform>();
        if (slotRect == null)
            slotRect = slotObject.AddComponent<RectTransform>();
        slotRect.sizeDelta = slotSize;

        Image iconImage = slotObject.GetComponent<Image>();
        if (iconImage == null)
            iconImage = slotObject.AddComponent<Image>();
        iconImage.preserveAspect = true;

        slotIcons[index] = iconImage;
        EnsureSlotSpriteOverride(index, slotObject.transform);

        EnsureStatusBox(index, slotObject.transform);
        EnsureUpgradeText(index, slotObject.transform);
    }

    void EnsureSlotSpriteOverride(int index, Transform slotTransform)
    {
        ElementSlotSpriteOverride spriteOverride = slotTransform.GetComponent<ElementSlotSpriteOverride>();
        if (spriteOverride == null)
            spriteOverride = slotTransform.gameObject.AddComponent<ElementSlotSpriteOverride>();

        slotSpriteOverrides[index] = spriteOverride;
    }

    void EnsureUpgradeText(int index, Transform slotTransform)
    {
        Font resolvedStatusFont = ResolveStatusFont();

        Transform existingText = slotTransform.Find("UpgradeText");
        if (existingText == null)
        {
            GameObject textObject = new GameObject("UpgradeText");
            textObject.transform.SetParent(slotTransform, false);
            existingText = textObject.transform;

            RectTransform textRect = textObject.AddComponent<RectTransform>();
            ApplyUpgradeTextLayout(textRect);

            Text upgradeText = textObject.AddComponent<Text>();
            upgradeText.font = resolvedStatusFont;
            upgradeText.fontSize = upgradeTextFontSize;
            upgradeText.alignment = TextAnchor.MiddleRight;
            upgradeText.color = upgradeTextColor;
            upgradeText.horizontalOverflow = HorizontalWrapMode.Overflow;
            upgradeText.verticalOverflow = VerticalWrapMode.Overflow;
            upgradeText.text = "";
            upgradeText.gameObject.SetActive(false);
            upgradeTexts[index] = upgradeText;
            return;
        }

        RectTransform existingRect = existingText as RectTransform;
        if (existingRect != null)
            ApplyUpgradeTextLayout(existingRect);

        Text textComponent = existingText.GetComponent<Text>();
        if (textComponent == null)
            textComponent = existingText.gameObject.AddComponent<Text>();
        textComponent.font = resolvedStatusFont;
        textComponent.fontSize = upgradeTextFontSize;
        textComponent.alignment = TextAnchor.MiddleRight;
        textComponent.color = upgradeTextColor;
        textComponent.horizontalOverflow = HorizontalWrapMode.Overflow;
        textComponent.verticalOverflow = VerticalWrapMode.Overflow;
        upgradeTexts[index] = textComponent;
    }

    void ApplyUpgradeTextLayout(RectTransform textRect)
    {
        textRect.anchorMin = new Vector2(1f, 1f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(1f, 1f);
        textRect.anchoredPosition = upgradeTextOffset;
        textRect.sizeDelta = upgradeTextSize;
    }

    void EnsureStatusBox(int index, Transform slotTransform)
    {
        Font resolvedStatusFont = ResolveStatusFont();

        Transform existingBox = slotTransform.Find("StatusBox");
        if (existingBox == null)
        {
            GameObject statusObject = new GameObject("StatusBox");
            statusObject.transform.SetParent(slotTransform, false);
            existingBox = statusObject.transform;

            RectTransform statusRect = statusObject.AddComponent<RectTransform>();
            ApplyStatusBoxLayout(statusRect, slotTransform);

            Image statusImage = statusObject.AddComponent<Image>();
            statusImage.color = statusBackgroundColor;
            statusBoxes[index] = statusImage;

            Outline outline = statusObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.75f);
            outline.effectDistance = new Vector2(1f, -1f);

            GameObject textObject = new GameObject("StatusText");
            textObject.transform.SetParent(statusObject.transform, false);
            RectTransform textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text statusText = textObject.AddComponent<Text>();
            statusText.font = resolvedStatusFont;
            statusText.fontSize = statusFontSize;
            statusText.alignment = TextAnchor.MiddleCenter;
            statusText.color = statusTextColor;
            statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            statusText.verticalOverflow = VerticalWrapMode.Overflow;
            statusTexts[index] = statusText;
            return;
        }

        RectTransform existingRect = existingBox as RectTransform;
        if (existingRect != null)
            ApplyStatusBoxLayout(existingRect, slotTransform);

        Image existingImage = existingBox.GetComponent<Image>();
        if (existingImage == null)
            existingImage = existingBox.gameObject.AddComponent<Image>();
        existingImage.color = statusBackgroundColor;
        statusBoxes[index] = existingImage;

        Transform textTransform = existingBox.Find("StatusText");
        if (textTransform == null)
        {
            GameObject textObject = new GameObject("StatusText");
            textObject.transform.SetParent(existingBox, false);
            textTransform = textObject.transform;
            RectTransform textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }

        Text textComponent = textTransform.GetComponent<Text>();
        if (textComponent == null)
            textComponent = textTransform.gameObject.AddComponent<Text>();
        textComponent.font = resolvedStatusFont;
        textComponent.fontSize = statusFontSize;
        textComponent.alignment = TextAnchor.MiddleCenter;
        textComponent.color = statusTextColor;
        textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
        textComponent.verticalOverflow = VerticalWrapMode.Overflow;
        statusTexts[index] = textComponent;
    }

    Font ResolveStatusFont()
    {
        if (statusFont != null)
            return statusFont;

        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    void ApplyStatusBoxLayout(RectTransform statusRect, Transform slotTransform)
    {
        statusRect.anchorMin = new Vector2(0.5f, 0f);
        statusRect.anchorMax = new Vector2(0.5f, 0f);
        statusRect.pivot = new Vector2(0.5f, 1f);
        statusRect.anchoredPosition = statusBoxOffset;

        float slotWidth = slotSize.x;
        RectTransform slotRect = slotTransform as RectTransform;
        if (slotRect != null)
            slotWidth = slotRect.sizeDelta.x;

        statusRect.sizeDelta = new Vector2(slotWidth, statusBoxSize.y);
    }

    void UpdateSlot(int index)
    {
        ElementSlotSystem.SlotState slot = slotSystem.GetSlot(index);
        if (slot == null || slotIcons[index] == null)
            return;

        bool isNeutral = slot.hasNeutralCard;
        bool isEmpty = slot.currentCard == null && !isNeutral;
        bool isCursed = slot.curseTurns > 0;

        Sprite iconSprite = isNeutral && neutralCardSprite != null ? neutralCardSprite : GetSlotSprite(index);
        slotIcons[index].sprite = iconSprite;
        slotIcons[index].enabled = iconSprite != null;
        slotIcons[index].color = GetIconColor(isCursed, isNeutral, isEmpty);

        if (statusTexts[index] != null)
            statusTexts[index].text = BuildStatusText(slot.RemainingCount, slot.curseTurns, isCursed, isNeutral, isEmpty);

        if (statusBoxes[index] != null)
        {
            statusBoxes[index].enabled = true;
            statusBoxes[index].color = isCursed ? cursedStatusBackgroundColor : statusBackgroundColor;
        }

        if (upgradeTexts[index] != null)
        {
            bool showUpgrade = slot.currentCard != null && !isNeutral && !isEmpty;
            upgradeTexts[index].gameObject.SetActive(showUpgrade);
            if (showUpgrade)
                upgradeTexts[index].text = "+" + Mathf.Max(0, slot.currentCard.upgradeLevel);
        }
    }

    void SetHudVisible(bool visible)
    {
        if (rootTransform != null && rootTransform.gameObject.activeSelf != visible)
            rootTransform.gameObject.SetActive(visible);
    }

    void SuppressLegacyHandUI()
    {
        CardSystem[] legacySystems = FindObjectsByType<CardSystem>(FindObjectsSortMode.None);
        foreach (CardSystem legacy in legacySystems)
        {
            if (legacy != null)
                legacy.ForceDisableForElementSystem();
        }
    }

    Sprite GetSlotSprite(int index)
    {
        if (index >= 0 && index < slotSpriteOverrides.Length)
        {
            ElementSlotSpriteOverride spriteOverride = slotSpriteOverrides[index];
            if (spriteOverride != null && spriteOverride.slotSprite != null)
                return spriteOverride.slotSprite;
        }

        if (comboSystem == null || comboSystem.cardSprites == null)
            return null;

        if (index < 0 || index >= comboSystem.cardSprites.Length)
            return null;

        return comboSystem.cardSprites[index];
    }

    Color GetIconColor(bool cursed, bool neutral, bool empty)
    {
        if (empty)
            return new Color(1f, 1f, 1f, slotAlphaWhenEmpty);

        if (neutral)
            return new Color(1f, 1f, 1f, 1f);

        if (cursed)
            return new Color(1f, 0.84f, 0.84f, slotAlphaWhenCursed);

        return new Color(1f, 1f, 1f, slotAlphaWhenActive);
    }

    string BuildStatusText(int remainingCount, int curseTurns, bool cursed, bool neutral, bool empty)
    {
        string stateText = "일반";

        if (empty)
            stateText = "비어있음";

        if (neutral)
            stateText = "중립";

        if (cursed)
        {
            string curseText = "저주" + Mathf.Max(1, curseTurns);
            stateText = neutral ? "중립, " + curseText : curseText;
        }

        return "남은 장수 " + remainingCount + " | " + stateText;
    }
}
