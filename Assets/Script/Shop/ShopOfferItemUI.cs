using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Battle.Card;
using Battle.UI;

[RequireComponent(typeof(CanvasGroup))]
public class ShopOfferItemUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image iconImage; // 상품 아이콘 이미지
    [SerializeField] private TextMeshProUGUI titleText; // 상품 이름 텍스트
    [SerializeField] private TextMeshProUGUI descriptionText; // 상품 설명 텍스트
    [SerializeField] private TextMeshProUGUI priceText; // 현재 사용 중인 가격 텍스트
    [SerializeField] private Image moneyIconImage; // 가격 옆 재화 아이콘
    [SerializeField] private Button buyButton; // 구매 버튼
    [SerializeField] private bool enableIconClickPurchase = true; // 아이콘 클릭 구매 허용 여부
    [SerializeField] private float purchasedAlpha = 0.45f; // 구매 완료 시 알파값
    [SerializeField] private Color unaffordablePriceColor = new Color(0.9f, 0.16f, 0.16f, 1f); // 돈 부족 시 가격 색
    [SerializeField] private string purchasedLabel = "\uAD6C\uB9E4\uC644\uB8CC"; // 구매 완료 라벨
    [SerializeField] private float cardVisualScale = 1f; // 카드 비주얼 전체 스케일
    [SerializeField] private float moneyIconScale = 1f; // 씬에 MoneyIcon이 없을 때만 쓰는 배율
    [SerializeField] private float relicIconScale = 1.45f; // 렐릭 아이콘 스케일

    [Header("Auto Bind")]
    [SerializeField] private bool autoBindFieldsByName = true; // 이름 기반 필드 자동 바인딩 여부
    [SerializeField] private bool autoBindPriceTextByName = true; // 이름 기반 가격 텍스트 자동 바인딩 여부
    [SerializeField] private string priceNameSuffix = "_Price"; // 가격 텍스트 이름 접미사

    private int price; // 현재 상품 가격
    private System.Action onBuy; // 구매 시 실행할 콜백
    private Button iconBuyButton; // 아이콘에 부착된 구매 버튼
    private Button rootBuyButton; // 루트에 부착된 폴백 구매 버튼
    private TextMeshProUGUI defaultPriceText; // 자체 가격 텍스트
    private TextMeshProUGUI externalPriceText; // 외부에서 지정한 가격 텍스트
    private NewCardView newCardView; // 카드 비주얼 뷰
    private CanvasGroup canvasGroup; // 알파 제어용 캔버스 그룹
    private bool isPurchased; // 구매 완료 상태
    private const string PurchasedDoneText = "\uAD6C\uB9E4\uC644\uB8CC"; // 구매 완료 기본 텍스트
    private RectTransform cardVisualRect; // 카드 비주얼 RectTransform
    private Vector3 baseCardVisualScale = Vector3.one; // 카드 비주얼 기준 스케일
    private bool baseCardVisualScaleCaptured; // 기준 스케일 캡처 여부
    private bool runtimeMoneyIconCreated; // 런타임 재화 아이콘 생성 여부
    private bool runtimeOfferIconCreated; // 런타임 상품 아이콘 생성 여부
    private readonly System.Collections.Generic.List<Graphic> cardVisualGraphics = new System.Collections.Generic.List<Graphic>(); // 카드 비주얼 그래픽 캐시
    private bool relicIconOnlyMode; // 렐릭 아이콘 전용 모드 여부
    private string offerTitle; // 호버 툴팁용 상품 이름
    private string offerDescription; // 호버 툴팁용 상품 설명
    private bool hoverTooltipWired; // 호버 툴팁 이벤트 연결 여부
    private TextMeshProUGUI priceColorSource; // 가격 색을 되돌릴 텍스트
    private Color normalPriceColor = Color.white; // 가격 원래 색
    private bool normalPriceColorCaptured; // 가격 원래 색 캡처 여부
    private Vector2 iconBaseSizeDelta; // 아이콘 기준 크기
    private Vector3 iconBaseScale = Vector3.one; // 아이콘 기준 스케일
    private bool iconBaseCaptured; // 아이콘 기준값 캡처 여부

    // 자동 바인딩 및 구매 라벨 정규화
    void Awake()
    {
        TryAutoBindFields();
        NormalizePurchasedLabel();
    }

    // 상품 정보(제목/설명/아이콘/가격/구매콜백)로 슬롯 초기화
    public void Setup(string title, string description, Sprite icon, int offerPrice, System.Action onBuyClick)
    {
        cardVisualGraphics.Clear();
        price = Mathf.Max(0, offerPrice);
        onBuy = onBuyClick;
        offerTitle = title;
        offerDescription = description;

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

    // 활성화 시 자동 바인딩 및 재화 변동 구독
    void OnEnable()
    {
        TryAutoBindFields();

        if (MoneyManager.Instance != null)
            MoneyManager.Instance.OnMoneyChanged += OnMoneyChanged;

        RefreshBuyState();
    }

    // 인스펙터 값 변경 시 바인딩/라벨/스케일 갱신
    void OnValidate()
    {
        TryAutoBindFields();
        NormalizePurchasedLabel();
        ApplyCardVisualScale();
    }

    // 구매 완료 라벨이 비었거나 깨진 경우 기본값으로 보정
    void NormalizePurchasedLabel()
    {
        if (string.IsNullOrWhiteSpace(purchasedLabel))
        {
            purchasedLabel = PurchasedDoneText;
            return;
        }

        if (purchasedLabel.Contains("�") || purchasedLabel.Contains("êµ¬") || purchasedLabel.Contains("êµ¬ë§¤"))
            purchasedLabel = PurchasedDoneText;
    }

    // 이름 규칙으로 UI 필드들을 자동 탐색해 바인딩
    void TryAutoBindFields()
    {
        if (!autoBindFieldsByName) return;

        if (iconImage == null)
            iconImage = FindChildComponentByName<Image>("IconImage", "icon", "CardIcon", "RelicIcon", "ItemIcon", "Icon");

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

    // 아이콘 이미지가 없으면 폴백 탐색 후 런타임으로 생성
    void EnsureOfferIconReady()
    {
        if (iconImage != null) return;

        TryBindFallbackIconImage();
        if (iconImage != null) return;

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

    // 자식 이미지 중 재화 아이콘이 아닌 것을 아이콘으로 폴백 바인딩
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

    // 카드 비주얼의 기준 스케일을 1회 캡처
    void CaptureBaseCardScaleIfNeeded()
    {
        if (baseCardVisualScaleCaptured) return;
        if (cardVisualRect == null) return;

        baseCardVisualScale = cardVisualRect.localScale;
        baseCardVisualScaleCaptured = true;
    }

    // 카드 비주얼에 설정 스케일을 적용. 상점 슬롯 자식이면 슬롯 Rect 크기에 맞춘다.
    void ApplyCardVisualScale()
    {
        if (cardVisualRect == null)
            cardVisualRect = newCardView != null ? newCardView.GetComponent<RectTransform>() : GetComponent<RectTransform>();
        if (cardVisualRect == null) return;

        if (TryFitCardVisualToParentSlot())
            return;

        CaptureBaseCardScaleIfNeeded();
        float scale = Mathf.Clamp(cardVisualScale, 0.5f, 1.2f);
        cardVisualRect.localScale = baseCardVisualScale * scale;
    }

    // 부모 슬롯(Card1~5)이 있으면 그 Rect 크기에 맞춘다.
    bool TryFitCardVisualToParentSlot()
    {
        if (newCardView == null || cardVisualRect == null) return false;

        RectTransform slot = cardVisualRect.parent as RectTransform;
        if (slot == null || slot == cardVisualRect) return false;

        return FitCardVisualToSlot(slot);
    }

    // 슬롯 Rect의 가로·세로에 카드 비주얼을 맞춘다.
    // 프리팹 sizeDelta는 유지하고, 자식 레이아웃이 같이 줄어들도록 localScale만 바꾼다.
    public bool FitCardVisualToSlot(RectTransform slot)
    {
        if (slot == null) return false;

        if (newCardView == null)
            newCardView = GetComponent<NewCardView>() ?? GetComponentInChildren<NewCardView>(true);
        if (cardVisualRect == null)
            cardVisualRect = newCardView != null ? newCardView.GetComponent<RectTransform>() : null;
        if (cardVisualRect == null || newCardView == null) return false;

        Vector2 native = cardVisualRect.sizeDelta;
        if (native.x < 1f || native.y < 1f) return false;

        Vector2 slotSize = slot.rect.size;
        if (slotSize.x < 1f || slotSize.y < 1f)
            slotSize = slot.sizeDelta;
        if (slotSize.x < 1f || slotSize.y < 1f) return false;

        Vector3 fitted = new Vector3(slotSize.x / native.x, slotSize.y / native.y, 1f);
        baseCardVisualScale = fitted;
        baseCardVisualScaleCaptured = true;
        cardVisualRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardVisualRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardVisualRect.pivot = new Vector2(0.5f, 0.5f);
        cardVisualRect.anchoredPosition = Vector2.zero;
        cardVisualRect.localScale = fitted;
        return true;
    }

    // 카드 데이터로 카드 비주얼(NewCardView) 미리보기 적용
    public void SetupCardVisual(CardData cardData, Sprite iconOverride = null)
    {
        if (cardData == null) return;
        if (newCardView == null)
            newCardView = GetComponent<NewCardView>() ?? GetComponentInChildren<NewCardView>(true);

        newCardView?.ApplyShopPreview(cardData, iconOverride);
    }

    // 카드 비주얼 스케일 설정 및 적용
    public void SetCardVisualScale(float scale)
    {
        cardVisualScale = scale;
        ApplyCardVisualScale();
    }

    // 재화 아이콘 스케일 설정 및 적용
    public void SetMoneyIconScale(float scale)
    {
        moneyIconScale = Mathf.Clamp(scale, 0.5f, 2.5f);
        if (runtimeMoneyIconCreated)
            ApplyMoneyIconScale();
    }

    // 가격 텍스트들의 폰트 크기 설정
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

    // 렐릭 아이콘 스케일 설정 및 적용
    public void SetRelicIconScale(float scale)
    {
        relicIconScale = Mathf.Clamp(scale, 1f, 2.5f);
        ApplyRelicIconOnlyMode();
    }

    // 외부 가격 텍스트를 지정하고 가격 표시 갱신
    public void SetExternalPriceText(TextMeshProUGUI target)
    {
        externalPriceText = target;
        priceText = externalPriceText != null ? externalPriceText : defaultPriceText;
        ApplyPriceText();
    }

    // 렐릭 아이콘 전용 모드 토글
    public void SetRelicIconOnlyMode(bool enabled)
    {
        relicIconOnlyMode = enabled;
        if (newCardView != null)
            newCardView.enabled = !enabled;

        if (relicIconOnlyMode)
        {
            SplitRelicIconFromSlot();
            EnableIconOnlyPurchaseMode();
            WireHoverTooltip();
        }
        else
            BindRootPurchaseIfNeeded();

        ApplyRelicIconOnlyMode();
    }

    // 렐릭 아이콘에 호버 시 이름/설명 툴팁을 띄우는 이벤트 연결(1회)
    void WireHoverTooltip()
    {
        if (hoverTooltipWired || iconImage == null) return;

        var trigger = iconImage.GetComponent<EventTrigger>();
        if (trigger == null) trigger = iconImage.gameObject.AddComponent<EventTrigger>();

        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ =>
        {
            ShopRelicTooltip.Instance?.Show(offerTitle, offerDescription);
        });
        trigger.triggers.Add(enter);

        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => ShopRelicTooltip.Instance?.Hide());
        trigger.triggers.Add(exit);

        hoverTooltipWired = true;
    }

    // 아이콘 클릭만 구매로 처리하고 다른 구매 버튼 비활성화
    void EnableIconOnlyPurchaseMode()
    {
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

    // 루트 폴백 구매 버튼을 비활성화
    void DisableRootPurchaseFallback()
    {
        if (rootBuyButton == null)
            rootBuyButton = GetComponent<Button>();
        if (rootBuyButton == null || rootBuyButton == iconBuyButton) return;

        rootBuyButton.onClick.RemoveListener(OnBuyClicked);
        rootBuyButton.interactable = false;
        rootBuyButton.enabled = false;
    }

    // 구매 상태를 설정하고 비주얼/가격/버튼 갱신
    public void SetPurchasedState(bool purchased)
    {
        isPurchased = purchased;
        ApplyPurchasedVisual();
        ApplyPriceText();
        ApplyMoneyIconState();
        RefreshBuyState();
    }

    // 가격 텍스트를 비움
    public void ClearPriceText()
    {
        if (externalPriceText != null)
            externalPriceText.text = string.Empty;

        if (defaultPriceText != null && defaultPriceText != externalPriceText && !ShouldPreserveDefaultPriceText())
            defaultPriceText.text = string.Empty;

        if (moneyIconImage == null)
            moneyIconImage = FindSceneMoneyIcon();
        if (moneyIconImage != null)
            moneyIconImage.gameObject.SetActive(false);
    }

    // 이름 규칙으로 가격 텍스트를 자동 탐색해 바인딩
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

        defaultPriceText = newCardView != null
            ? FindChildComponentByName<TextMeshProUGUI>("Price", "price", "guageCost", "GaugeCost")
            : FindChildComponentByName<TextMeshProUGUI>("guageCost", "GaugeCost", "Price");
        if (externalPriceText == null)
            priceText = defaultPriceText;
    }

    // 구매 여부에 따라 가격 또는 완료 라벨을 가격 텍스트에 반영
    void ApplyPriceText()
    {
        if (isPurchased)
        {
            string done = ResolvePurchasedLabelFor(priceText != null ? priceText : externalPriceText);

            if (externalPriceText != null)
                externalPriceText.text = done;

            if (defaultPriceText != null && !ShouldPreserveDefaultPriceText())
                defaultPriceText.text = externalPriceText != null && defaultPriceText != externalPriceText ? string.Empty : done;

            if (priceText != null && priceText != defaultPriceText && priceText != externalPriceText)
                priceText.text = done;

            return;
        }

        string value = price.ToString();

        if (externalPriceText != null)
            externalPriceText.text = value;

        if (defaultPriceText != null && !ShouldPreserveDefaultPriceText())
            defaultPriceText.text = externalPriceText != null && defaultPriceText != externalPriceText ? string.Empty : value;

        if (priceText != null && priceText != defaultPriceText && priceText != externalPriceText)
            priceText.text = value;
    }

    // guageCost/GaugeCost 텍스트는 카드 코스트 전용이라 상점 가격으로 덮지 않도록 판정
    bool ShouldPreserveDefaultPriceText()
    {
        if (defaultPriceText == null) return false;

        if (newCardView == null) return false;

        string n = defaultPriceText.name;
        return string.Equals(n, "guageCost", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(n, "GaugeCost", System.StringComparison.OrdinalIgnoreCase);
    }

    // 유물 아이콘(슬롯과 다른 자식)을 슬롯 Rect 크기에 맞춘다. 맞출 수 없으면 zero.
    static Vector3 FitRelicIconToSlot(RectTransform icon, RectTransform slot)
    {
        if (icon == null || slot == null || icon == slot) return Vector3.zero;

        Vector2 native = icon.sizeDelta;
        if (native.x < 1f || native.y < 1f) return Vector3.zero;

        Vector2 slotSize = slot.rect.size;
        if (slotSize.x < 1f || slotSize.y < 1f)
            slotSize = slot.sizeDelta;
        if (slotSize.x < 1f || slotSize.y < 1f) return Vector3.zero;

        Vector3 fitted = new Vector3(slotSize.x / native.x, slotSize.y / native.y, 1f);
        icon.anchorMin = new Vector2(0.5f, 0.5f);
        icon.anchorMax = new Vector2(0.5f, 0.5f);
        icon.pivot = new Vector2(0.5f, 0.5f);
        icon.anchoredPosition = Vector2.zero;
        icon.localScale = fitted;
        return fitted;
    }

    // 렐릭 전용 모드 여부에 따라 카드 비주얼 숨김과 아이콘 스케일/순서를 조정
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

            if (relicIconOnlyMode && iconImage.gameObject.name == "RelicIconVisual")
            {
                iconImage.enabled = true;
            }
            else if (relicIconOnlyMode)
            {
                RectTransform iconRt = iconImage.rectTransform;
                bool iconIsSlot = iconRt == transform as RectTransform;
                Vector3 desiredScale = iconBaseScale;

                if (!iconIsSlot)
                {
                    RectTransform slotRt = iconRt.parent as RectTransform;
                    Vector3 fitted = FitRelicIconToSlot(iconRt, slotRt);
                    desiredScale = fitted == Vector3.zero
                        ? iconBaseScale * Mathf.Clamp(relicIconScale, 1f, 2.5f)
                        : fitted;
                }

                var hover = iconImage.GetComponent<ButtonHoverScale>();
                if (hover != null)
                    hover.SetBaseScale(desiredScale);
                else
                    iconRt.localScale = desiredScale;

                if (!iconIsSlot)
                    iconRt.SetAsLastSibling();
            }
            else
            {
                var hover = iconImage.GetComponent<ButtonHoverScale>();
                if (hover != null)
                    hover.SetBaseScale(iconBaseScale);
                else
                    iconImage.rectTransform.localScale = iconBaseScale;
                iconImage.rectTransform.sizeDelta = iconBaseSizeDelta;
            }
        }
    }

    // 아이콘의 기준 크기/스케일을 1회 캡처
    void CaptureIconBaseIfNeeded()
    {
        if (iconBaseCaptured) return;
        if (iconImage == null) return;

        iconBaseSizeDelta = iconImage.rectTransform.sizeDelta;
        iconBaseScale = iconImage.rectTransform.localScale;
        iconBaseCaptured = true;
    }

    // 폰트가 한글을 지원하지 않으면 ASCII 라벨로 대체한 구매 완료 라벨 반환
    string ResolvePurchasedLabelFor(TextMeshProUGUI target)
    {
        string done = string.IsNullOrWhiteSpace(purchasedLabel) ? PurchasedDoneText : purchasedLabel;

        if (target != null && target.font != null)
        {
            if (!target.font.HasCharacters(done))
                return "SOLD";
        }

        return done;
    }

    // 후보 이름들로 자식에서 지정 타입 컴포넌트를 탐색
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

    // 이름이 일치하는 자식 Transform을 재귀 탐색
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

    // 비활성화 시 재화 변동 구독 해제
    void OnDisable()
    {
        if (MoneyManager.Instance != null)
            MoneyManager.Instance.OnMoneyChanged -= OnMoneyChanged;
    }

    // 파괴 시 재화 변동 구독 해제
    void OnDestroy()
    {
        if (MoneyManager.Instance != null)
            MoneyManager.Instance.OnMoneyChanged -= OnMoneyChanged;
    }

    // 재화 변동 시 구매 가능 상태 갱신
    void OnMoneyChanged(int _)
    {
        RefreshBuyState();
    }

    // 보유 골드와 구매 상태로 구매 버튼들의 활성화 갱신
    void RefreshBuyState()
    {
        bool canBuy = CanAffordNow();
        SuppressDisabledTint(buyButton);
        SuppressDisabledTint(iconBuyButton);
        SuppressDisabledTint(rootBuyButton);

        if (buyButton != null)
            buyButton.interactable = canBuy;

        if (iconBuyButton != null)
            iconBuyButton.interactable = canBuy;

        if (rootBuyButton != null)
            rootBuyButton.interactable = canBuy;

        ApplyUnaffordablePriceColor(canBuy);
    }

    // 구매 불가가 카드 그림을 흐리게 만들지 않도록 버튼 색 전이를 끈다
    static void SuppressDisabledTint(Button button)
    {
        if (button == null) return;

        button.transition = Selectable.Transition.None;
        if (button.targetGraphic != null)
            button.targetGraphic.CrossFadeColor(Color.white, 0f, true, true);
    }

    // 돈이 부족하면 가격 글자만 빨갛게, 살 수 있으면 원래 색으로
    void ApplyUnaffordablePriceColor(bool canAfford)
    {
        TextMeshProUGUI label = externalPriceText != null ? externalPriceText : priceText;
        if (label == null || (label == defaultPriceText && ShouldPreserveDefaultPriceText()))
            return;

        if (!normalPriceColorCaptured || priceColorSource != label)
        {
            normalPriceColor = label.color;
            priceColorSource = label;
            normalPriceColorCaptured = true;
        }

        bool broke = !isPurchased && !canAfford;
        label.color = broke ? unaffordablePriceColor : normalPriceColor;
    }

    // 슬롯 호버는 끄고, 이 상품 오브젝트만 커지게 한다
    public void KeepHoverOnItemOnly()
    {
        DisableHover(transform.parent);
        EnableHover(gameObject);
    }

    // 가격·머니 아이콘이 딸린 슬롯 호버를 끈다
    public void DisableSlotHover()
    {
        DisableHover(transform);
    }

    static void DisableHover(Transform target)
    {
        if (target == null) return;

        var hover = target.GetComponent<ButtonHoverScale>();
        if (hover == null) return;

        hover.enabled = false;
        var rt = target as RectTransform;
        if (rt != null && rt.localScale.sqrMagnitude < 0.01f)
            rt.localScale = Vector3.one;
    }

    static void EnableHover(GameObject target)
    {
        if (target == null) return;

        var hover = target.GetComponent<ButtonHoverScale>();
        if (hover == null)
            hover = target.AddComponent<ButtonHoverScale>();

        hover.enabled = true;
        var rt = target.GetComponent<RectTransform>();
        if (rt != null)
            hover.SetBaseScale(rt.localScale);
    }

    // 유물 그림이 슬롯 자체면, 가격과 분리된 자식으로 옮겨 그 그림만 호버되게 한다
    void SplitRelicIconFromSlot()
    {
        if (iconImage == null) return;

        RectTransform iconRt = iconImage.rectTransform;
        if (iconRt != transform as RectTransform) return;

        const string childName = "RelicIconVisual";
        Transform existing = iconRt.Find(childName);
        Image visual = existing != null ? existing.GetComponent<Image>() : null;
        if (visual == null)
        {
            var go = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(iconRt, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            visual = go.GetComponent<Image>();
            existing = go.transform;
        }

        visual.sprite = iconImage.sprite;
        visual.color = Color.white;
        visual.preserveAspect = true;
        visual.raycastTarget = true;
        visual.enabled = true;
        visual.type = Image.Type.Simple;
        existing.SetSiblingIndex(0);

        var slotColor = iconImage.color;
        slotColor.a = 0f;
        iconImage.color = slotColor;
        iconImage.raycastTarget = false;
        iconImage = visual;

        DisableHover(transform);
        EnableHover(visual.gameObject);
    }

    // 구매 버튼 클릭 처리(구매 콜백 실행)
    void OnBuyClicked()
    {
        if (!CanAffordNow()) return;
        onBuy?.Invoke();
        RefreshBuyState();
    }

    // 보유 골드로 이 상품을 살 수 있는지
    bool CanAffordNow()
    {
        return !isPurchased && MoneyManager.Instance != null && MoneyManager.Instance.CurrentMoney >= price;
    }

    // 구매 상태에 따른 카드 비주얼 표시/숨김 적용
    void ApplyPurchasedVisual()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 1f;
        SetCardVisualVisible(!isPurchased, false);
    }

    // 카드 비주얼 그래픽들의 표시 여부 일괄 설정
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

    // 숨김 대상 카드 비주얼 그래픽 목록을 캐시(가격/재화 아이콘은 제외)
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

            if (graphic.name == "guageCost" || graphic.name == "GaugeCost")
            {
                cardVisualGraphics.Add(graphic);
                continue;
            }

            if (graphic == priceText || graphic == defaultPriceText || graphic == externalPriceText || graphic == moneyIconImage)
                continue;

            cardVisualGraphics.Add(graphic);
        }
    }

    // 구매 상태에 따라 재화 아이콘 표시 여부 갱신
    void ApplyMoneyIconState()
    {
        if (moneyIconImage == null) return;

        bool show = !isPurchased;
        if (moneyIconImage.gameObject.activeSelf != show)
            moneyIconImage.gameObject.SetActive(show);
    }

    // 재화 아이콘을 준비하고 스프라이트/스케일 적용
    void EnsureMoneyIconReady()
    {
        if (moneyIconImage == null)
            moneyIconImage = FindSceneMoneyIcon();

        if (moneyIconImage == null)
            TryCreateRuntimeMoneyIcon();

        if (moneyIconImage == null) return;

        if (runtimeMoneyIconCreated)
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

    // 가격 텍스트 옆, 또는 이 오브젝트 자식에 씬에 배치된 재화 아이콘을 찾는다
    Image FindSceneMoneyIcon()
    {
        Image nested = FindChildComponentByName<Image>("MoneyIcon", "CostIcon");
        if (nested != null) return nested;

        TextMeshProUGUI anchorText = externalPriceText != null ? externalPriceText : (defaultPriceText != null ? defaultPriceText : priceText);
        if (anchorText == null || anchorText.transform.parent == null) return null;

        Transform parent = anchorText.transform.parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name != "MoneyIcon" && child.name != "CostIcon") continue;

            Image image = child.GetComponent<Image>();
            if (image != null) return image;
        }

        return null;
    }

    // 씬에 아이콘이 없을 때만 가격 텍스트 옆에 38x38 아이콘을 만든다
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
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(38f, 38f);
        rt.localScale = Vector3.one;
        rt.anchoredPosition = textRt.anchoredPosition + new Vector2(-63f, 0f);

        Image image = go.GetComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        moneyIconImage = image;
        runtimeMoneyIconCreated = true;
    }

    // 런타임 생성 아이콘만 배율을 적용한다. 씬 배치 아이콘의 위치·크기는 유지한다
    void ApplyMoneyIconScale()
    {
        if (!runtimeMoneyIconCreated || moneyIconImage == null) return;

        RectTransform rt = moneyIconImage.rectTransform;
        if (rt == null) return;

        float scale = Mathf.Clamp(moneyIconScale, 0.5f, 2.5f);
        rt.localScale = new Vector3(scale, scale, 1f);
    }

    // 아이콘에 구매 버튼을 부착하고 클릭 콜백 연결
    void BindIconPurchaseIfNeeded()
    {
        if (!enableIconClickPurchase || iconImage == null) return;

        Button onIcon = iconImage.GetComponent<Button>();
        if (onIcon == null)
            onIcon = iconImage.gameObject.AddComponent<Button>();

        if (iconBuyButton != null && iconBuyButton != onIcon)
            iconBuyButton.onClick.RemoveListener(OnBuyClicked);

        iconBuyButton = onIcon;
        iconBuyButton.enabled = true;
        iconBuyButton.onClick.RemoveListener(OnBuyClicked);
        iconBuyButton.onClick.AddListener(OnBuyClicked);
    }

    // 전용 구매 버튼이 없으면 카드 루트 클릭을 구매 폴백으로 연결
    void BindRootPurchaseIfNeeded()
    {
        if (!enableIconClickPurchase) return;
        if (relicIconOnlyMode) return;

        if (buyButton != null)
            buyButton.enabled = true;

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
