using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Battle.Card;
using Battle.UI;

[RequireComponent(typeof(CanvasGroup))]
public class ShopOfferItemUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private Image moneyIconImage;
    [SerializeField] private Button buyButton;
    [SerializeField] private bool enableIconClickPurchase = true;
    [SerializeField] private float purchasedAlpha = 0.45f;
    [SerializeField] private string purchasedLabel = "\uAD6C\uB9E4\uC644\uB8CC";
    [SerializeField] private float cardVisualScale = 1f;
    [SerializeField] private float moneyIconScale = 1.35f;
    [SerializeField] private float relicIconScale = 1.45f;

    [Header("Auto Bind")]
    [SerializeField] private bool autoBindFieldsByName = true;
    [SerializeField] private bool autoBindPriceTextByName = true;
    [SerializeField] private string priceNameSuffix = "_Price";

    private int price;
    private System.Action onBuy;
    private Button iconBuyButton;
    private Button rootBuyButton;
    private TextMeshProUGUI defaultPriceText;
    private TextMeshProUGUI externalPriceText;
    private NewCardView newCardView;
    private CanvasGroup canvasGroup;
    private bool isPurchased;
    private const string PurchasedDoneText = "\uAD6C\uB9E4\uC644\uB8CC";
    private RectTransform cardVisualRect;
    private Vector3 baseCardVisualScale = Vector3.one;
    private bool baseCardVisualScaleCaptured;
    private bool runtimeMoneyIconCreated;
    private bool runtimeOfferIconCreated;
    private readonly System.Collections.Generic.List<Graphic> cardVisualGraphics = new System.Collections.Generic.List<Graphic>();
    private bool relicIconOnlyMode;
    private Vector2 iconBaseSizeDelta;
    private Vector3 iconBaseScale = Vector3.one;
    private bool iconBaseCaptured;

    void Awake()
    {
        TryAutoBindFields();
        NormalizePurchasedLabel();
    }

    public void Setup(string title, string description, Sprite icon, int offerPrice, System.Action onBuyClick)
    {
        price = Mathf.Max(0, offerPrice);
        onBuy = onBuyClick;

        ApplyCardVisualScale();
        EnsureOfferIconReady();

        if (titleText != null)
            titleText.text = string.IsNullOrWhiteSpace(title) ? "상품" : title;

        if (descriptionText != null)
            descriptionText.text = string.IsNullOrWhiteSpace(description) ? "설명 없음" : description;

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
            BindIconPurchaseIfNeeded();
        }

        ApplyPriceText();

        EnsureMoneyIconReady();

        ApplyMoneyIconState();

        if (buyButton != null)
        {
            buyButton.onClick.RemoveListener(OnBuyClicked);
            buyButton.onClick.AddListener(OnBuyClicked);
        }

        BindRootPurchaseIfNeeded();

        ApplyRelicIconOnlyMode();
        RefreshBuyState();
    }

    void OnEnable()
    {
        TryAutoBindFields();

        if (MoneyManager.Instance != null)
            MoneyManager.Instance.OnMoneyChanged += OnMoneyChanged;

        RefreshBuyState();
    }

    void OnValidate()
    {
        TryAutoBindFields();
        NormalizePurchasedLabel();
        ApplyCardVisualScale();
    }

    void NormalizePurchasedLabel()
    {
        if (string.IsNullOrWhiteSpace(purchasedLabel))
        {
            purchasedLabel = PurchasedDoneText;
            return;
        }

        // 기존 직렬화 값이 깨진 경우(예: 인코딩 변환) 자동 보정
        if (purchasedLabel.Contains("�") || purchasedLabel.Contains("êµ¬") || purchasedLabel.Contains("êµ¬ë§¤"))
            purchasedLabel = PurchasedDoneText;
    }

    void TryAutoBindFields()
    {
        if (!autoBindFieldsByName) return;

        if (iconImage == null)
            iconImage = FindChildComponentByName<Image>("IconImage", "icon", "CardIcon");

        if (titleText == null)
            titleText = FindChildComponentByName<TextMeshProUGUI>("Nametxt", "Title", "Name");

        if (descriptionText == null)
            descriptionText = FindChildComponentByName<TextMeshProUGUI>("Desctxt", "Description", "Desc");

        if (buyButton == null)
            buyButton = FindChildComponentByName<Button>("BuyButton", "PurchaseButton", "Buy");

        if (moneyIconImage == null)
            moneyIconImage = FindChildComponentByName<Image>("MoneyIcon", "CostIcon");

        if (iconImage == null)
            TryBindFallbackIconImage();

        if (newCardView == null)
            newCardView = GetComponent<NewCardView>() ?? GetComponentInChildren<NewCardView>(true);

        if (cardVisualRect == null)
            cardVisualRect = newCardView != null ? newCardView.GetComponent<RectTransform>() : GetComponent<RectTransform>();

        CaptureBaseCardScaleIfNeeded();
        ApplyCardVisualScale();

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

        TryAutoBindPriceText();
    }

    void EnsureOfferIconReady()
    {
        if (iconImage != null) return;

        TryBindFallbackIconImage();
        if (iconImage != null) return;

        // Relic 슬롯처럼 아이콘 전용 이미지가 없는 구조를 대비해 런타임으로 생성한다.
        if (runtimeOfferIconCreated) return;

        var go = new GameObject("OfferIconImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(transform, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(120f, 120f);

        iconImage = go.GetComponent<Image>();
        runtimeOfferIconCreated = true;
    }

    void TryBindFallbackIconImage()
    {
        if (iconImage != null) return;

        Image selfImage = GetComponent<Image>();
        if (selfImage != null && selfImage != moneyIconImage)
        {
            iconImage = selfImage;
            return;
        }

        Image[] images = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image img = images[i];
            if (img == null || img == moneyIconImage) continue;

            string n = img.name;
            if (!string.IsNullOrWhiteSpace(n) && (n == "MoneyIcon" || n == "CostIcon"))
                continue;

            iconImage = img;
            return;
        }
    }

    void CaptureBaseCardScaleIfNeeded()
    {
        if (baseCardVisualScaleCaptured) return;
        if (cardVisualRect == null) return;

        baseCardVisualScale = cardVisualRect.localScale;
        baseCardVisualScaleCaptured = true;
    }

    void ApplyCardVisualScale()
    {
        if (cardVisualRect == null)
            cardVisualRect = newCardView != null ? newCardView.GetComponent<RectTransform>() : GetComponent<RectTransform>();
        if (cardVisualRect == null) return;

        CaptureBaseCardScaleIfNeeded();
        float scale = Mathf.Clamp(cardVisualScale, 0.5f, 1.2f);
        cardVisualRect.localScale = baseCardVisualScale * scale;
    }

    public void SetupCardVisual(CardData cardData, Sprite iconOverride = null)
    {
        if (cardData == null) return;
        if (newCardView == null)
            newCardView = GetComponent<NewCardView>() ?? GetComponentInChildren<NewCardView>(true);

        newCardView?.ApplyShopPreview(cardData, iconOverride);
    }

    public void SetCardVisualScale(float scale)
    {
        cardVisualScale = scale;
        ApplyCardVisualScale();
    }

    public void SetMoneyIconScale(float scale)
    {
        moneyIconScale = scale;
        ApplyMoneyIconScale();
    }

    public void SetPriceFontSize(float size)
    {
        if (size <= 0f) return;

        if (externalPriceText != null)
            externalPriceText.fontSize = size;

        if (defaultPriceText != null)
            defaultPriceText.fontSize = size;

        if (priceText != null)
            priceText.fontSize = size;
    }

    public void SetRelicIconScale(float scale)
    {
        relicIconScale = Mathf.Clamp(scale, 1f, 2.5f);
        ApplyRelicIconOnlyMode();
    }

    public void SetExternalPriceText(TextMeshProUGUI target)
    {
        externalPriceText = target;
        priceText = externalPriceText != null ? externalPriceText : defaultPriceText;
        ApplyPriceText();
    }

    public void SetRelicIconOnlyMode(bool enabled)
    {
        relicIconOnlyMode = enabled;
        if (newCardView != null)
            newCardView.enabled = !enabled;

        if (relicIconOnlyMode)
            EnableIconOnlyPurchaseMode();
        else
            BindRootPurchaseIfNeeded();

        ApplyRelicIconOnlyMode();
    }

    void EnableIconOnlyPurchaseMode()
    {
        // 렐릭 모드에서는 아이콘 클릭만 구매 판정으로 사용.
        BindIconPurchaseIfNeeded();

        if (buyButton != null && buyButton != iconBuyButton)
        {
            buyButton.onClick.RemoveListener(OnBuyClicked);
            buyButton.interactable = false;
            buyButton.enabled = false;
        }

        DisableRootPurchaseFallback();

        if (iconBuyButton != null)
        {
            iconBuyButton.enabled = true;
            iconBuyButton.interactable = !isPurchased;
            iconBuyButton.onClick.RemoveListener(OnBuyClicked);
            iconBuyButton.onClick.AddListener(OnBuyClicked);
        }
    }

    void DisableRootPurchaseFallback()
    {
        if (rootBuyButton == null)
            rootBuyButton = GetComponent<Button>();
        if (rootBuyButton == null) return;

        rootBuyButton.onClick.RemoveListener(OnBuyClicked);
        rootBuyButton.interactable = false;
        rootBuyButton.enabled = false;
    }

    public void SetPurchasedState(bool purchased)
    {
        isPurchased = purchased;
        ApplyPurchasedVisual();
        ApplyPriceText();
        ApplyMoneyIconState();
        RefreshBuyState();
    }

    public void ClearPriceText()
    {
        if (externalPriceText != null)
            externalPriceText.text = string.Empty;

        if (defaultPriceText != null && defaultPriceText != externalPriceText)
            defaultPriceText.text = string.Empty;
    }

    void TryAutoBindPriceText()
    {
        if (!autoBindPriceTextByName) return;
        if (defaultPriceText != null && externalPriceText == null)
        {
            priceText = defaultPriceText;
            return;
        }

        string targetName = string.IsNullOrWhiteSpace(priceNameSuffix)
            ? "Price"
            : gameObject.name + priceNameSuffix;

        Transform found = FindChildRecursiveByName(transform, targetName);
        if (found != null)
        {
            defaultPriceText = found.GetComponent<TextMeshProUGUI>();
            if (defaultPriceText != null)
            {
                if (externalPriceText == null) priceText = defaultPriceText;
                return;
            }
        }

        // NewSkillCard 프리팹 호환: 기본 가격 텍스트 이름
        defaultPriceText = FindChildComponentByName<TextMeshProUGUI>("guageCost", "GaugeCost", "Price");
        if (externalPriceText == null)
            priceText = defaultPriceText;
    }

    void ApplyPriceText()
    {
        if (isPurchased)
        {
            string done = ResolvePurchasedLabelFor(priceText != null ? priceText : externalPriceText);

            if (externalPriceText != null)
                externalPriceText.text = done;

            if (defaultPriceText != null)
                defaultPriceText.text = externalPriceText != null && defaultPriceText != externalPriceText ? string.Empty : done;

            if (priceText != null && priceText != defaultPriceText && priceText != externalPriceText)
                priceText.text = done;

            return;
        }

        string value = price.ToString();

        if (externalPriceText != null)
            externalPriceText.text = value;

        if (defaultPriceText != null)
            defaultPriceText.text = externalPriceText != null && defaultPriceText != externalPriceText ? string.Empty : value;

        if (priceText != null && priceText != defaultPriceText && priceText != externalPriceText)
            priceText.text = value;
    }

    void ApplyRelicIconOnlyMode()
    {
        if (relicIconOnlyMode)
            SetCardVisualVisible(false, true);
        else
            SetCardVisualVisible(true, false);

        if (iconImage != null)
        {
            CaptureIconBaseIfNeeded();
            iconImage.enabled = true;

            if (relicIconOnlyMode)
            {
                float scale = Mathf.Clamp(relicIconScale, 1f, 2f);
                iconImage.rectTransform.localScale = iconBaseScale * scale;
            }
            else
            {
                iconImage.rectTransform.localScale = iconBaseScale;
                iconImage.rectTransform.sizeDelta = iconBaseSizeDelta;
            }
        }
    }

    void CaptureIconBaseIfNeeded()
    {
        if (iconBaseCaptured) return;
        if (iconImage == null) return;

        iconBaseSizeDelta = iconImage.rectTransform.sizeDelta;
        iconBaseScale = iconImage.rectTransform.localScale;
        iconBaseCaptured = true;
    }

    string ResolvePurchasedLabelFor(TextMeshProUGUI target)
    {
        string done = string.IsNullOrWhiteSpace(purchasedLabel) ? PurchasedDoneText : purchasedLabel;

        // 가격 텍스트 폰트가 한글 글리프를 지원하지 않으면 fallback 폰트로 깨져 보일 수 있음.
        // 이 경우 같은 폰트에서 안정적으로 보이는 ASCII 라벨을 사용한다.
        if (target != null && target.font != null)
        {
            if (!target.font.HasCharacters(done))
                return "SOLD";
        }

        return done;
    }

    T FindChildComponentByName<T>(params string[] candidateNames) where T : Component
    {
        if (candidateNames == null || candidateNames.Length == 0) return null;

        for (int i = 0; i < candidateNames.Length; i++)
        {
            string name = candidateNames[i];
            if (string.IsNullOrWhiteSpace(name)) continue;

            Transform found = FindChildRecursiveByName(transform, name);
            if (found == null) continue;

            T comp = found.GetComponent<T>();
            if (comp != null) return comp;
        }

        return null;
    }

    static Transform FindChildRecursiveByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName)) return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == targetName) return child;

            Transform nested = FindChildRecursiveByName(child, targetName);
            if (nested != null) return nested;
        }

        return null;
    }

    void OnDisable()
    {
        if (MoneyManager.Instance != null)
            MoneyManager.Instance.OnMoneyChanged -= OnMoneyChanged;
    }

    void OnDestroy()
    {
        if (MoneyManager.Instance != null)
            MoneyManager.Instance.OnMoneyChanged -= OnMoneyChanged;
    }

    void OnMoneyChanged(int _)
    {
        RefreshBuyState();
    }

    void RefreshBuyState()
    {
        bool canBuy = !isPurchased && MoneyManager.Instance != null && MoneyManager.Instance.CurrentMoney >= price;
        if (buyButton != null)
            buyButton.interactable = canBuy;

        if (iconBuyButton != null)
            iconBuyButton.interactable = canBuy;

        if (rootBuyButton != null)
            rootBuyButton.interactable = canBuy;
    }

    void OnBuyClicked()
    {
        if (isPurchased) return;
        onBuy?.Invoke();
        RefreshBuyState();
    }

    void ApplyPurchasedVisual()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

        // 구매 시 카드 컨테이너의 시각 요소를 전부 숨긴다.
        canvasGroup.alpha = 1f;
        SetCardVisualVisible(!isPurchased, false);
    }

    void SetCardVisualVisible(bool visible, bool keepIconVisible)
    {
        CacheCardVisualGraphics();
        for (int i = 0; i < cardVisualGraphics.Count; i++)
        {
            Graphic graphic = cardVisualGraphics[i];
            if (graphic == null) continue;
            if (keepIconVisible && graphic == iconImage) continue;
            graphic.enabled = visible;
        }
    }

    void CacheCardVisualGraphics()
    {
        if (cardVisualGraphics.Count > 0) return;

        Transform root = newCardView != null ? newCardView.transform : transform;
        if (root == null) return;

        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null) continue;
            if (graphic == priceText || graphic == defaultPriceText || graphic == externalPriceText || graphic == moneyIconImage)
                continue;

            cardVisualGraphics.Add(graphic);
        }
    }

    void ApplyMoneyIconState()
    {
        if (moneyIconImage == null) return;

        bool show = !isPurchased;
        if (moneyIconImage.gameObject.activeSelf != show)
            moneyIconImage.gameObject.SetActive(show);
    }

    void EnsureMoneyIconReady()
    {
        if (moneyIconImage == null)
            TryCreateRuntimeMoneyIcon();

        if (moneyIconImage == null) return;

        ApplyMoneyIconScale();

        if (MoneyManager.Instance != null)
        {
            if (MoneyManager.Instance.MoneyIcon != null)
            {
                moneyIconImage.sprite = MoneyManager.Instance.MoneyIcon;
                moneyIconImage.enabled = true;
            }
            else
            {
                Debug.LogWarning("[ShopOfferItemUI] MoneyManager.moneyIconSprite가 비어 있어 재화 아이콘을 표시할 수 없습니다.");
            }
        }
    }

    void TryCreateRuntimeMoneyIcon()
    {
        if (runtimeMoneyIconCreated) return;

        TextMeshProUGUI anchorText = externalPriceText != null ? externalPriceText : (defaultPriceText != null ? defaultPriceText : priceText);
        if (anchorText == null) return;

        Transform parent = anchorText.transform.parent;
        if (parent == null) return;

        var go = new GameObject("MoneyIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        RectTransform textRt = anchorText.rectTransform;
        rt.anchorMin = textRt.anchorMin;
        rt.anchorMax = textRt.anchorMax;
        rt.pivot = new Vector2(1f, 0.5f);
        rt.sizeDelta = new Vector2(22f, 22f);
        rt.anchoredPosition = textRt.anchoredPosition + new Vector2(-48f, 0f);

        moneyIconImage = go.GetComponent<Image>();
        runtimeMoneyIconCreated = true;
    }

    void ApplyMoneyIconScale()
    {
        if (moneyIconImage == null) return;

        RectTransform rt = moneyIconImage.rectTransform;
        if (rt == null) return;

        float scale = Mathf.Clamp(moneyIconScale, 1f, 2.5f);
        rt.localScale = new Vector3(scale, scale, 1f);
    }

    void BindIconPurchaseIfNeeded()
    {
        if (!enableIconClickPurchase || iconImage == null) return;

        if (iconBuyButton == null)
            iconBuyButton = iconImage.GetComponent<Button>();

        if (iconBuyButton == null)
            iconBuyButton = iconImage.gameObject.AddComponent<Button>();

        iconBuyButton.onClick.RemoveListener(OnBuyClicked);
        iconBuyButton.onClick.AddListener(OnBuyClicked);
    }

    void BindRootPurchaseIfNeeded()
    {
        if (!enableIconClickPurchase) return;
        if (relicIconOnlyMode) return;

        if (buyButton != null)
            buyButton.enabled = true;

        // 명시적 BuyButton이 없을 때 카드 루트 클릭으로도 구매되게 폴백.
        if (buyButton != null) return;

        if (rootBuyButton == null)
            rootBuyButton = GetComponent<Button>();

        if (rootBuyButton == null)
            rootBuyButton = gameObject.AddComponent<Button>();

        rootBuyButton.enabled = true;
        rootBuyButton.transition = Selectable.Transition.None;
        rootBuyButton.onClick.RemoveListener(OnBuyClicked);
        rootBuyButton.onClick.AddListener(OnBuyClicked);
    }
}
