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
    [SerializeField] private string purchasedLabel = "\uAD6C\uB9E4\uC644\uB8CC"; // 구매 완료 라벨
    [SerializeField] private float cardVisualScale = 1f; // 카드 비주얼 전체 스케일
    [SerializeField] private float moneyIconScale = 1.35f; // 재화 아이콘 스케일
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

    // 카드 비주얼에 설정 스케일을 적용
    void ApplyCardVisualScale()
    {
        if (cardVisualRect == null)
            cardVisualRect = newCardView != null ? newCardView.GetComponent<RectTransform>() : GetComponent<RectTransform>();
        if (cardVisualRect == null) return;

        CaptureBaseCardScaleIfNeeded();
        float scale = Mathf.Clamp(cardVisualScale, 0.5f, 1.2f);
        cardVisualRect.localScale = baseCardVisualScale * scale;
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
        moneyIconScale = scale;
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
        if (rootBuyButton == null) return;

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

            if (relicIconOnlyMode)
            {
                float scale = Mathf.Clamp(relicIconScale, 1f, 2.5f);
                Vector3 desiredScale = iconBaseScale * scale;

                var hover = iconImage.GetComponent<ButtonHoverScale>();
                if (hover != null)
                    hover.SetBaseScale(desiredScale);
                else
                    iconImage.rectTransform.localScale = desiredScale;

                iconImage.rectTransform.SetAsLastSibling();
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
        bool canBuy = !isPurchased && MoneyManager.Instance != null && MoneyManager.Instance.CurrentMoney >= price;
        if (buyButton != null)
            buyButton.interactable = canBuy;

        if (iconBuyButton != null)
            iconBuyButton.interactable = canBuy;

        if (rootBuyButton != null)
            rootBuyButton.interactable = canBuy;
    }

    // 구매 버튼 클릭 처리(구매 콜백 실행)
    void OnBuyClicked()
    {
        if (isPurchased) return;
        onBuy?.Invoke();
        RefreshBuyState();
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

    // 가격 텍스트 옆에 런타임 재화 아이콘 생성
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

    // 재화 아이콘 스케일을 적용
    void ApplyMoneyIconScale()
    {
        if (moneyIconImage == null) return;

        RectTransform rt = moneyIconImage.rectTransform;
        if (rt == null) return;

        float scale = Mathf.Clamp(moneyIconScale, 1f, 2.5f);
        rt.localScale = new Vector3(scale, scale, 1f);
    }

    // 아이콘에 구매 버튼을 부착하고 클릭 콜백 연결
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
