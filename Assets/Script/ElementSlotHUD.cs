using UnityEngine;
using UnityEngine.UI;

public class ElementSlotHUD : MonoBehaviour
{
    private ElementSlotSystem slotSystem;
    private ComboSystem comboSystem;
    private Canvas hudCanvas;
    private RectTransform frameRoot;
    private readonly Image[] slotPanels = new Image[4];
    private readonly Image[] slotIcons = new Image[4];
    private readonly Text[] keyLabels = new Text[4];
    private readonly Text[] infoLabels = new Text[4];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindFirstObjectByType<ElementSlotHUD>() != null)
            return;

        GameObject hudObject = new GameObject("ElementSlotHUD");
        DontDestroyOnLoad(hudObject);
        hudObject.AddComponent<ElementSlotHUD>();
    }

    void Awake()
    {
        BuildUI();
    }

    void Update()
    {
        if (slotSystem == null)
            slotSystem = ElementSlotSystem.Instance ?? FindFirstObjectByType<ElementSlotSystem>();

        if (comboSystem == null)
            comboSystem = ComboSystem.Instance ?? FindFirstObjectByType<ComboSystem>();

        SuppressLegacyHandUI();

        if (slotSystem == null)
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

    void BuildUI()
    {
        GameObject canvasObject = new GameObject("ElementSlotHUD_Canvas");
        canvasObject.transform.SetParent(transform, false);

        hudCanvas = canvasObject.AddComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = 5000;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject frameObject = new GameObject("SlotFrame");
        frameObject.transform.SetParent(canvasObject.transform, false);
        frameRoot = frameObject.AddComponent<RectTransform>();
        frameRoot.anchorMin = new Vector2(0.5f, 0f);
        frameRoot.anchorMax = new Vector2(0.5f, 0f);
        frameRoot.pivot = new Vector2(0.5f, 0f);
        frameRoot.anchoredPosition = new Vector2(0f, 26f);
        frameRoot.sizeDelta = new Vector2(880f, 240f);

        Image frameImage = frameObject.AddComponent<Image>();
        frameImage.color = new Color(0.08f, 0.05f, 0.03f, 0.88f);

        Outline frameOutline = frameObject.AddComponent<Outline>();
        frameOutline.effectColor = new Color(0f, 0f, 0f, 0.65f);
        frameOutline.effectDistance = new Vector2(4f, -4f);

        HorizontalLayoutGroup layout = frameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 14f;
        layout.padding = new RectOffset(22, 22, 16, 16);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;
        layout.childControlHeight = false;
        layout.childControlWidth = false;

        for (int index = 0; index < 4; index++)
            BuildSlotPanel(index);

        SetHudVisible(false);
    }

    void BuildSlotPanel(int index)
    {
        GameObject slotObject = new GameObject("Slot_" + ElementSlotSystem.SLOT_KEYS[index]);
        slotObject.transform.SetParent(frameRoot, false);

        RectTransform slotRect = slotObject.AddComponent<RectTransform>();
        slotRect.sizeDelta = new Vector2(198f, 208f);

        Image panelImage = slotObject.AddComponent<Image>();
        panelImage.color = new Color(0.22f, 0.2f, 0.18f, 0.96f);
        slotPanels[index] = panelImage;

        Outline panelOutline = slotObject.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0f, 0f, 0f, 0.75f);
        panelOutline.effectDistance = new Vector2(3f, -3f);

        GameObject keyObject = new GameObject("KeyLabel");
        keyObject.transform.SetParent(slotObject.transform, false);
        RectTransform keyRect = keyObject.AddComponent<RectTransform>();
        keyRect.anchorMin = new Vector2(0.5f, 1f);
        keyRect.anchorMax = new Vector2(0.5f, 1f);
        keyRect.pivot = new Vector2(0.5f, 1f);
        keyRect.anchoredPosition = new Vector2(0f, -10f);
        keyRect.sizeDelta = new Vector2(140f, 38f);

        Text keyText = keyObject.AddComponent<Text>();
        keyText.font = Font.CreateDynamicFontFromOSFont("Arial", 28);
        keyText.fontSize = 28;
        keyText.fontStyle = FontStyle.Bold;
        keyText.alignment = TextAnchor.MiddleCenter;
        keyText.color = new Color(1f, 0.96f, 0.86f, 1f);
        keyLabels[index] = keyText;

        GameObject iconObject = new GameObject("Icon");
        iconObject.transform.SetParent(slotObject.transform, false);
        RectTransform iconRect = iconObject.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(0f, 8f);
        iconRect.sizeDelta = new Vector2(110f, 140f);

        Image iconImage = iconObject.AddComponent<Image>();
        iconImage.color = new Color(1f, 1f, 1f, 0.95f);
        iconImage.preserveAspect = true;
        slotIcons[index] = iconImage;

        GameObject infoObject = new GameObject("InfoLabel");
        infoObject.transform.SetParent(slotObject.transform, false);
        RectTransform infoRect = infoObject.AddComponent<RectTransform>();
        infoRect.anchorMin = new Vector2(0.5f, 0f);
        infoRect.anchorMax = new Vector2(0.5f, 0f);
        infoRect.pivot = new Vector2(0.5f, 0f);
        infoRect.anchoredPosition = new Vector2(0f, 10f);
        infoRect.sizeDelta = new Vector2(176f, 54f);

        Text infoText = infoObject.AddComponent<Text>();
        infoText.font = Font.CreateDynamicFontFromOSFont("Arial", 20);
        infoText.fontSize = 20;
        infoText.alignment = TextAnchor.MiddleCenter;
        infoText.color = Color.white;
        infoLabels[index] = infoText;
    }

    void UpdateSlot(int index)
    {
        ElementSlotSystem.SlotState slot = slotSystem.GetSlot(index);
        if (slot == null)
            return;

        bool isNeutral = slot.hasNeutralCard;
        bool isEmpty = slot.currentCard == null && !isNeutral;
        bool isCursed = slot.curseTurns > 0;

        keyLabels[index].text = ElementSlotSystem.SLOT_KEYS[index];
        slotPanels[index].color = GetSlotColor(slot.elementKey, isCursed, isNeutral, isEmpty);

        Sprite iconSprite = GetSlotSprite(index);
        slotIcons[index].sprite = iconSprite;
        slotIcons[index].enabled = iconSprite != null;
        slotIcons[index].color = GetIconColor(isCursed, isNeutral, isEmpty);

        infoLabels[index].text = GetStatusLine(slot, isNeutral, isEmpty, isCursed) + "\nLeft " + slot.RemainingCount;
    }

    void SetHudVisible(bool visible)
    {
        if (hudCanvas != null && hudCanvas.gameObject.activeSelf != visible)
            hudCanvas.gameObject.SetActive(visible);
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
        if (comboSystem == null || comboSystem.cardSprites == null)
            return null;

        if (index < 0 || index >= comboSystem.cardSprites.Length)
            return null;

        return comboSystem.cardSprites[index];
    }

    string GetStatusLine(ElementSlotSystem.SlotState slot, bool isNeutral, bool isEmpty, bool isCursed)
    {
        if (isNeutral)
            return "Neutral";

        if (isEmpty)
            return "Empty";

        if (isCursed)
            return slot.elementKey + "  Curse " + slot.curseTurns;

        return slot.elementKey;
    }

    Color GetSlotColor(string elementKey, bool cursed, bool neutral, bool empty)
    {
        if (neutral)
            return new Color(0.42f, 0.42f, 0.42f, 0.96f);

        if (cursed)
            return new Color(0.42f, 0.12f, 0.12f, 0.98f);

        if (empty)
            return new Color(0.18f, 0.18f, 0.18f, 0.9f);

        switch (elementKey)
        {
            case "fire":
                return new Color(0.62f, 0.17f, 0.1f, 0.98f);
            case "water":
                return new Color(0.11f, 0.32f, 0.67f, 0.98f);
            case "wind":
                return new Color(0.15f, 0.5f, 0.32f, 0.98f);
            case "earth":
                return new Color(0.48f, 0.34f, 0.16f, 0.98f);
            default:
                return new Color(0.22f, 0.2f, 0.18f, 0.96f);
        }
    }

    Color GetIconColor(bool cursed, bool neutral, bool empty)
    {
        if (empty)
            return new Color(1f, 1f, 1f, 0.18f);

        if (neutral)
            return new Color(1f, 1f, 1f, 0.75f);

        if (cursed)
            return new Color(1f, 0.84f, 0.84f, 0.95f);

        return Color.white;
    }
}
