using System.Collections;
using System.Collections.Generic;
using Battle;
using Battle.Card;
using Battle.Relic;
using Battle.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopStageController : MonoBehaviour
{
    [Header("Canvas 1 (상점 입구)")]
    [SerializeField] private GameObject shopCanvas1;
    [SerializeField] private Button shopOwnerButton;
    [SerializeField] private Button leaveShopButton;

    [Header("Canvas 1 - 말풍선 타이핑")]
    [SerializeField] private TextMeshProUGUI shopBubbleText;
    [SerializeField, TextArea(2, 4)] private string shopBubbleMessage = "무엇을 원하시오, 여행자여?";
    [SerializeField, Min(1f)] private float shopBubbleCharsPerSecond = 28f;
    [SerializeField] private bool completeTypingOnOwnerClick = true;

    [Header("Canvas 2 (상품 화면)")]
    [SerializeField] private GameObject shopCanvas2;
    [SerializeField] private Button backToCanvas1Button;

    [Header("Canvas 2 - 상품 루트")]
    [SerializeField] private Transform cardOffersRoot;
    [SerializeField] private Transform relicOffersRoot;
    [SerializeField] private Transform comboOffersRoot;

    [Header("구조 강제 루트명")]
    [SerializeField] private string topCardsRootName = "Top_Cards";
    [SerializeField] private string bottomLeftRelicsRootName = "Bottom_LeftRelics";

    [Header("Canvas 2 - 이름 기반 슬롯 찾기")]
    [SerializeField] private bool useNamedSlots = true;
    [SerializeField] private string cardSlotPrefix = "Card";
    [SerializeField] private string relicSlotPrefix = "Relic";
    [SerializeField] private string comboSlotPrefix = "ComboSkill";

    [Header("Canvas 2 - 공용 프리팹")]
    [SerializeField] private GameObject shopOfferItemPrefab;

    [Header("Canvas 2 - 컨테이너별 템플릿 (Optional)")]
    [SerializeField] private GameObject cardOfferTemplate;
    [SerializeField] private GameObject relicOfferTemplate;
    [SerializeField] private GameObject comboOfferTemplate;

    [Header("렐릭 소스")]
    [SerializeField] private RelicStageController relicStageController;

    [Header("Canvas 2 - 카드 제거")]
    [SerializeField] private Button removeCardButton;
    [SerializeField] private TextMeshProUGUI removeCardCostText;
    [SerializeField] private int removeCardCost = 120;

    [Header("표시 개수")]
    [SerializeField] private int cardOfferCount = 5;
    [SerializeField] private int relicOfferCount = 3;
    [SerializeField] private int comboOfferCount = 1;

    [Header("가격 범위")]
    [SerializeField] private int cardMinPrice = 60;
    [SerializeField] private int cardMaxPrice = 150;
    [SerializeField] private int relicMinPrice = 140;
    [SerializeField] private int relicMaxPrice = 260;
    [SerializeField] private int comboMinPrice = 180;
    [SerializeField] private int comboMaxPrice = 320;

    [Header("카드 표시 (Shop)")]
    [Tooltip("상점 카드(NewSkillCard) 전체 스케일")]
    [SerializeField, Range(0.5f, 1.2f)] private float shopCardVisualScale = 1f;
    [Tooltip("상점 카드 가격 옆 재화 아이콘 스케일")]
    [SerializeField, Range(1f, 2.5f)] private float shopMoneyIconScale = 1.35f;

    [Header("렐릭 표시 (Shop)")]
    [Tooltip("상점 렐릭 아이콘 스케일")]
    [SerializeField, Range(1f, 2.5f)] private float shopRelicIconScale = 1.45f;
    [Tooltip("상점 렐릭 가격 텍스트 폰트 크기")]
    [SerializeField, Range(12f, 72f)] private float shopRelicPriceFontSize = 36f;

    [Header("콤보 책 표시 (Shop)")]
    [SerializeField] private BookRewardItemUI comboBookItemPrefab;
    [SerializeField] private string comboBookMountName = "Mid_Combo";
    [SerializeField, Range(0.3f, 1.5f)] private float comboBookScale = 1f;
    [SerializeField] private Sprite comboBookFireSprite;
    [SerializeField] private Sprite comboBookWaterSprite;
    [SerializeField] private Sprite comboBookWindSprite;
    [SerializeField] private Sprite comboBookEarthSprite;

    private readonly List<CardOffer> cardOffers = new List<CardOffer>();
    private readonly List<RelicOffer> relicOffers = new List<RelicOffer>();
    private readonly List<ComboOffer> comboOffers = new List<ComboOffer>();

    private RoundManager roundManager;
    private bool bound;
    private Coroutine bubbleTypingRoutine;
    private bool isBubbleTyping;

    class CardOffer
    {
        public CardData card;
        public int price;
        public bool purchased;
    }

    class RelicOffer
    {
        public RelicDef relic;
        public bool isSpriteOnly;
        public Sprite sprite;
        public string displayName;
        public string description;
        public int price;
        public bool purchased;
    }

    class ComboOffer
    {
        public ComboSkillDef combo;
        public int price;
        public bool purchased;
    }

    void Awake()
    {
        BindRuntime();
        CaptureBubbleFallbackText();
    }

    void OnEnable()
    {
        BindRuntime();
        EnterShopStage();
    }

    void OnValidate()
    {
        shopCardVisualScale = Mathf.Clamp(shopCardVisualScale, 0.5f, 1.2f);
        shopMoneyIconScale = Mathf.Clamp(shopMoneyIconScale, 1f, 2.5f);
        shopRelicIconScale = Mathf.Clamp(shopRelicIconScale, 1f, 2.5f);
        shopRelicPriceFontSize = Mathf.Clamp(shopRelicPriceFontSize, 12f, 72f);
        comboBookScale = Mathf.Clamp(comboBookScale, 0.3f, 1.5f);
    }

    public void BeginShop(RoundManager manager)
    {
        roundManager = manager;
        EnterShopStage();
    }

    void BindRuntime()
    {
        if (bound) return;

        if (shopOwnerButton != null)
        {
            shopOwnerButton.onClick.RemoveListener(OpenShopCanvas2);
            shopOwnerButton.onClick.AddListener(OpenShopCanvas2);
        }

        if (backToCanvas1Button != null)
        {
            backToCanvas1Button.onClick.RemoveListener(ReturnToMapFromShop);
            backToCanvas1Button.onClick.AddListener(ReturnToMapFromShop);
        }

        if (leaveShopButton != null)
        {
            leaveShopButton.onClick.RemoveListener(ReturnToMapFromShop);
            leaveShopButton.onClick.AddListener(ReturnToMapFromShop);
        }

        if (removeCardButton != null)
        {
            removeCardButton.onClick.RemoveListener(OnRemoveCardButtonClicked);
            removeCardButton.onClick.AddListener(OnRemoveCardButtonClicked);
        }

        bound = true;
    }

    void EnterShopStage()
    {
        ShowCanvas1();
        UpdateRemoveCardButtonText();
    }

    void ShowCanvas1()
    {
        if (shopCanvas1 != null) shopCanvas1.SetActive(true);
        if (shopCanvas2 != null) shopCanvas2.SetActive(false);
        PlayShopBubbleTyping();
    }

    void OpenShopCanvas2()
    {
        if (completeTypingOnOwnerClick && isBubbleTyping)
        {
            CompleteShopBubbleTyping();
            return;
        }

        if (shopCanvas1 != null) shopCanvas1.SetActive(false);
        if (shopCanvas2 != null) shopCanvas2.SetActive(true);

        RegenerateOffers();
        RefreshOfferViews();
    }

    void OnDisable()
    {
        StopShopBubbleTyping();
    }

    void CaptureBubbleFallbackText()
    {
        if (!string.IsNullOrWhiteSpace(shopBubbleMessage)) return;
        if (shopBubbleText == null) return;
        if (string.IsNullOrWhiteSpace(shopBubbleText.text)) return;

        shopBubbleMessage = shopBubbleText.text;
    }

    void PlayShopBubbleTyping()
    {
        if (shopBubbleText == null) return;

        CaptureBubbleFallbackText();
        string message = string.IsNullOrWhiteSpace(shopBubbleMessage) ? shopBubbleText.text : shopBubbleMessage;

        StopShopBubbleTyping();

        shopBubbleText.text = message;
        shopBubbleText.maxVisibleCharacters = 0;
        bubbleTypingRoutine = StartCoroutine(TypeBubbleRoutine(message));
    }

    IEnumerator TypeBubbleRoutine(string message)
    {
        isBubbleTyping = true;

        shopBubbleText.ForceMeshUpdate();
        int total = shopBubbleText.textInfo.characterCount;
        if (total <= 0)
        {
            isBubbleTyping = false;
            yield break;
        }

        float cps = Mathf.Max(1f, shopBubbleCharsPerSecond);
        for (int i = 1; i <= total; i++)
        {
            shopBubbleText.maxVisibleCharacters = i;
            yield return new WaitForSeconds(1f / cps);
        }

        isBubbleTyping = false;
        bubbleTypingRoutine = null;
    }

    void CompleteShopBubbleTyping()
    {
        if (shopBubbleText == null) return;

        string message = string.IsNullOrWhiteSpace(shopBubbleMessage) ? shopBubbleText.text : shopBubbleMessage;
        StopShopBubbleTyping();
        shopBubbleText.text = message;
        shopBubbleText.maxVisibleCharacters = int.MaxValue;
    }

    void StopShopBubbleTyping()
    {
        isBubbleTyping = false;
        if (bubbleTypingRoutine == null) return;

        StopCoroutine(bubbleTypingRoutine);
        bubbleTypingRoutine = null;
    }

    void RegenerateOffers()
    {
        BuildCardOffers();
        BuildRelicOffers();
        BuildComboOffers();
        UpdateRemoveCardButtonText();
    }

    void BuildCardOffers()
    {
        cardOffers.Clear();

        var deckState = RunDeckState.EnsureExists();
        deckState.EnsureSeeded();
        HashSet<int> ownedIds = deckState.GetOwnedCardIds();

        var pool = new List<CardData>();
        var all = CardDatabase.All;
        for (int i = 0; i < all.Count; i++)
        {
            CardData card = all[i];
            if (card == null) continue;
            if (!IsElementCard(card)) continue;
            if (ownedIds.Contains(card.id)) continue;
            pool.Add(card);
        }

        List<CardData> picks = PickRandom(pool, cardOfferCount);
        for (int i = 0; i < picks.Count; i++)
        {
            cardOffers.Add(new CardOffer
            {
                card = picks[i],
                price = Random.Range(cardMinPrice, cardMaxPrice + 1),
            });
        }
    }

    void BuildRelicOffers()
    {
        relicOffers.Clear();

        var manager = RelicManager.Instance;
        if (manager == null) return;

        relicStageController = ResolveRelicStageController();

        if (relicStageController != null)
        {
            List<RelicStageController.ShopRelicCandidate> candidates = relicStageController.GetShopRelicCandidates(manager);
            candidates.RemoveAll(c => !c.isSpriteOnly || c.sprite == null);
            List<RelicStageController.ShopRelicCandidate> selectedCandidates = PickRandom(candidates, relicOfferCount);
            for (int i = 0; i < selectedCandidates.Count; i++)
            {
                RelicStageController.ShopRelicCandidate pick = selectedCandidates[i];
                relicOffers.Add(new RelicOffer
                {
                    relic = pick.relic,
                    isSpriteOnly = pick.isSpriteOnly,
                    sprite = pick.sprite,
                    displayName = pick.displayName,
                    description = pick.description,
                    price = Random.Range(relicMinPrice, relicMaxPrice + 1),
                });
            }

            if (relicOffers.Count > 0)
                return;
        }

        Debug.LogWarning("[ShopStageController] RelicStageController의 sprite-only 후보를 찾지 못해 렐릭 상점 목록이 비었습니다.");
    }

    void BuildComboOffers()
    {
        comboOffers.Clear();

        List<ComboSkillDef> all = ComboSkillDatabase.BuildOwnedCombos();
        if (all == null || all.Count == 0) return;

        var ownedRefIds = new HashSet<int>();
        var nb = NewBattleController.Instance;
        if (nb != null && nb.OwnedComboSkills != null)
        {
            for (int i = 0; i < nb.OwnedComboSkills.Count; i++)
            {
                ComboSkillDef owned = nb.OwnedComboSkills[i];
                if (owned == null) continue;
                ownedRefIds.Add(owned.refComboId);
            }
        }

        var pool = new List<ComboSkillDef>();
        for (int i = 0; i < all.Count; i++)
        {
            ComboSkillDef combo = all[i];
            if (combo == null) continue;
            if (ownedRefIds.Contains(combo.refComboId)) continue;
            pool.Add(combo);
        }

        List<ComboSkillDef> picks = PickRandom(pool, comboOfferCount);
        for (int i = 0; i < picks.Count; i++)
        {
            comboOffers.Add(new ComboOffer
            {
                combo = picks[i],
                price = Random.Range(comboMinPrice, comboMaxPrice + 1),
            });
        }
    }

    void RefreshOfferViews()
    {
        RefreshCardOfferViews();
        RefreshRelicOfferViews();
        RefreshComboOfferViews();
        UpdateRemoveCardButtonText();
    }

    void RefreshCardOfferViews()
    {
        Transform strictCardRoot = ResolveStrictCardRoot();
        if (strictCardRoot != null)
            cardOffersRoot = strictCardRoot;

        if (cardOffersRoot == null) return;

        if (useNamedSlots)
        {
            var namedSlots = CollectCardNamedSlots(cardOffersRoot, cardSlotPrefix, cardOfferCount);
            if (namedSlots.Count >= cardOfferCount)
            {
                BindCardOffersToNamedSlots(namedSlots);
                return;
            }

            Debug.LogWarning($"[ShopStageController] Top_Cards/{cardSlotPrefix}1~{cardOfferCount} 슬롯을 모두 찾지 못했습니다. ({namedSlots.Count}/{cardOfferCount})");
            return;
        }

        Debug.LogWarning("[ShopStageController] 카드 표시는 named slot 모드에서만 지원됩니다. useNamedSlots를 켜주세요.");
    }

    void RefreshRelicOfferViews()
    {
        Transform strictRelicRoot = ResolveStrictRelicRoot();
        if (strictRelicRoot != null)
            relicOffersRoot = strictRelicRoot;

        if (relicOffersRoot == null) return;

        if (useNamedSlots)
        {
            var namedSlots = CollectNamedSlots(relicOffersRoot, relicSlotPrefix, relicOfferCount, false);
            if (namedSlots.Count >= relicOfferCount)
            {
                BindRelicOffersToDirectSlots(namedSlots);
                return;
            }

            Debug.LogWarning($"[ShopStageController] Bottom_LeftRelics/{relicSlotPrefix}1~{relicOfferCount} 슬롯을 모두 찾지 못했습니다. ({namedSlots.Count}/{relicOfferCount})");
            // named slot 일부만 찾힌 경우 direct slot으로 폴백해 1~N 슬롯이 모두 바인딩되도록 한다.
        }

        var directSlots = CollectDirectSlots(relicOffersRoot, false);
        if (ShouldUseDirectSlots(directSlots.Count, relicOfferCount))
        {
            BindRelicOffersToDirectSlots(directSlots);
            return;
        }

        Debug.LogWarning("[ShopStageController] 렐릭 슬롯 바인딩 실패: Bottom_LeftRelics 하위 Relic 슬롯 수를 확인하세요.");
    }

    void RefreshComboOfferViews()
    {
        if (comboOffersRoot == null) return;

        var slots = new List<Transform>();

        if (useNamedSlots)
        {
            for (int i = 1; i <= comboOfferCount; i++)
            {
                string slotName = $"{comboSlotPrefix}{i}";
                Transform slotTf = FindChildRecursiveByName(comboOffersRoot, slotName);
                if (slotTf != null)
                    slots.Add(slotTf);
            }
        }

        if (slots.Count == 0)
        {
            for (int i = 0; i < comboOffersRoot.childCount; i++)
                slots.Add(comboOffersRoot.GetChild(i));
        }

        if (slots.Count > 0)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                Transform slot = slots[i];
                if (slot == null) continue;

                bool visible = i < comboOffers.Count;
                slot.gameObject.SetActive(visible);
                if (!visible) continue;

                BindComboOfferToObject(slot.gameObject, comboOffers[i]);
            }
            return;
        }

        TemplateBinding template = ResolveTemplate(comboOffersRoot, comboOfferTemplate, false);
        if (template.template == null) return;

        ClearChildren(comboOffersRoot, template.keepObject);
        for (int i = 0; i < comboOffers.Count; i++)
        {
            ComboOffer offer = comboOffers[i];
            if (offer == null || offer.combo == null) continue;

            GameObject go = Instantiate(template.template, comboOffersRoot);
            go.SetActive(true);
            BindComboOfferToObject(go, offer);
        }
    }

    void TryBuyCard(CardOffer offer)
    {
        if (offer == null || offer.card == null) return;
        if (offer.purchased) return;
        if (!TrySpendMoney(offer.price)) return;

        RunDeckState.EnsureExists().AddCard(offer.card.id);
        offer.purchased = true;
        RefreshOfferViews();
    }

    void TryBuyRelic(RelicOffer offer)
    {
        if (offer == null) return;
        if (offer.purchased) return;
        if (!TrySpendMoney(offer.price)) return;

        RelicManager manager = RelicManager.Instance;
        if (manager == null) return;

        if (offer.isSpriteOnly)
        {
            if (offer.sprite == null) return;
            if (!manager.TryAddSpriteOnlyRelic(offer.sprite, out _)) return;
        }
        else
        {
            if (offer.relic == null) return;
            manager.AddRelic(offer.relic);
        }

        offer.purchased = true;
        RefreshOfferViews();
    }

    void TryBuyCombo(ComboOffer offer)
    {
        if (offer == null || offer.combo == null) return;
        if (offer.purchased) return;

        NewBattleController nb = NewBattleController.Instance;
        if (nb == null)
        {
            Debug.LogWarning("[ShopStageController] NewBattleController.Instance가 없어 콤보 구매를 적용할 수 없습니다.");
            return;
        }

        if (!TrySpendMoney(offer.price)) return;

        nb.AddOwnedCombo(offer.combo.refComboId);
        offer.purchased = true;
        RefreshOfferViews();
    }

    void OnRemoveCardButtonClicked()
    {
        Debug.Log($"[ShopStageController] 카드 제거 기능은 아직 준비 중입니다. (예정 비용: {removeCardCost})");
    }

    bool TrySpendMoney(int amount)
    {
        if (MoneyManager.Instance == null)
        {
            Debug.LogWarning("[ShopStageController] MoneyManager.Instance가 없어 구매를 진행할 수 없습니다.");
            return false;
        }

        bool spent = MoneyManager.Instance.SpendMoney(amount);
        if (!spent)
        {
            Debug.Log($"[ShopStageController] 구매 실패 - 필요 골드:{amount}, 보유 골드:{MoneyManager.Instance.CurrentMoney}");
        }
        return spent;
    }

    void UpdateRemoveCardButtonText()
    {
        if (removeCardCostText != null)
            removeCardCostText.text = "카드 제거 (준비중)";
    }

    static bool IsElementCard(CardData card)
    {
        if (card == null) return false;
        return card.element == CardElement.Fire
            || card.element == CardElement.Water
            || card.element == CardElement.Wind
            || card.element == CardElement.Earth;
    }

    static Sprite ResolveCardSprite(CardData card)
    {
        if (card == null || string.IsNullOrWhiteSpace(card.skillImg)) return null;
        return Resources.Load<Sprite>($"CardIcons/{card.skillImg}");
    }

    static List<T> PickRandom<T>(List<T> source, int count)
    {
        var result = new List<T>();
        if (source == null || source.Count == 0 || count <= 0)
            return result;

        var pool = new List<T>(source);
        int pickCount = Mathf.Min(count, pool.Count);
        for (int i = 0; i < pickCount; i++)
        {
            int idx = Random.Range(0, pool.Count);
            result.Add(pool[idx]);
            pool.RemoveAt(idx);
        }

        return result;
    }

    struct TemplateBinding
    {
        public GameObject template;
        public GameObject keepObject;
    }

    struct CardSlotBinding
    {
        public Transform container;
        public ShopOfferItemUI ui;
        public TextMeshProUGUI priceText;
    }

    static List<ShopOfferItemUI> CollectDirectSlots(Transform root, bool allowNewCardViewAttach)
    {
        var slots = new List<ShopOfferItemUI>();
        if (root == null) return slots;

        for (int i = 0; i < root.childCount; i++)
        {
            var ui = GetOrAttachOfferItemUI(root.GetChild(i).gameObject, allowNewCardViewAttach);
            if (ui == null) continue;
            slots.Add(ui);
        }

        return slots;
    }

    static bool ShouldUseDirectSlots(int slotCount, int offerCount)
    {
        if (slotCount <= 0) return false;
        if (offerCount <= 1) return true;
        return slotCount >= offerCount;
    }

    static List<ShopOfferItemUI> CollectNamedSlots(Transform root, string prefix, int offerCount, bool allowNewCardViewAttach)
    {
        var slots = new List<ShopOfferItemUI>();
        if (root == null || string.IsNullOrWhiteSpace(prefix) || offerCount <= 0)
            return slots;

        for (int i = 1; i <= offerCount; i++)
        {
            string slotName = $"{prefix}{i}";
            Transform slotTf = FindChildRecursiveByName(root, slotName);
            if (slotTf == null) continue;

            var ui = GetOrAttachOfferItemUI(slotTf.gameObject, allowNewCardViewAttach);
            if (ui == null) continue;

            slots.Add(ui);
        }

        return slots;
    }

    List<CardSlotBinding> CollectCardNamedSlots(Transform root, string prefix, int offerCount)
    {
        var slots = new List<CardSlotBinding>();
        if (root == null || string.IsNullOrWhiteSpace(prefix) || offerCount <= 0)
            return slots;

        for (int i = 1; i <= offerCount; i++)
        {
            string slotName = $"{prefix}{i}";
            Transform container = FindChildRecursiveByName(root, slotName);
            if (container == null) continue;

            ShopOfferItemUI ui = ResolveOrCreateCardSlotContent(container);
            if (ui == null) continue;

            slots.Add(new CardSlotBinding
            {
                container = container,
                ui = ui,
                priceText = FindNamedText(container, $"{slotName}_Price") ?? FindNamedText(root, $"{slotName}_Price"),
            });
        }

        return slots;
    }

    static Transform FindChildRecursiveByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName)) return null;
        if (IsSameNodeName(root.name, targetName)) return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (IsSameNodeName(child.name, targetName)) return child;

            Transform found = FindChildRecursiveByName(child, targetName);
            if (found != null) return found;
        }

        return null;
    }

    static bool IsSameNodeName(string actual, string expected)
    {
        if (string.IsNullOrWhiteSpace(actual) || string.IsNullOrWhiteSpace(expected)) return false;

        string normalizedActual = actual.Trim().Replace(" ", string.Empty);
        string normalizedExpected = expected.Trim().Replace(" ", string.Empty);
        return string.Equals(normalizedActual, normalizedExpected, System.StringComparison.OrdinalIgnoreCase);
    }

    static ShopOfferItemUI GetOrAttachOfferItemUI(GameObject target, bool allowNewCardViewAttach = true)
    {
        if (target == null) return null;

        ShopOfferItemUI ui = target.GetComponent<ShopOfferItemUI>();
        if (ui != null) return ui;

        ui = target.GetComponentInChildren<ShopOfferItemUI>(true);
        if (ui != null) return ui;

        if (!allowNewCardViewAttach)
            return target.AddComponent<ShopOfferItemUI>();

        // NewSkillCard(NewCardView) 프리팹을 상점에서 재사용할 때 ShopOfferItemUI를 자동 부착.
        NewCardView cardView = target.GetComponent<NewCardView>();
        if (cardView != null)
            return target.AddComponent<ShopOfferItemUI>();

        cardView = target.GetComponentInChildren<NewCardView>(true);
        if (cardView != null)
            return cardView.gameObject.AddComponent<ShopOfferItemUI>();

        return null;
    }

    ShopOfferItemUI ResolveOrCreateCardSlotContent(Transform container)
    {
        if (container == null) return null;

        ShopOfferItemUI ui = GetOrAttachOfferItemUI(container.gameObject, true);
        if (ui != null) return ui;

        GameObject template = cardOfferTemplate != null ? cardOfferTemplate : shopOfferItemPrefab;
        if (template == null) return null;

        GameObject instance = Instantiate(template, container);
        instance.name = template.name;
        instance.SetActive(true);
        return GetOrAttachOfferItemUI(instance, true);
    }

    static TextMeshProUGUI FindNamedText(Transform root, string targetName)
    {
        Transform found = FindChildRecursiveByName(root, targetName);
        if (found == null) return null;

        TextMeshProUGUI text = found.GetComponent<TextMeshProUGUI>();
        if (text != null) return text;

        // 이름 오브젝트 아래에 실제 TMP가 중첩된 경우 대응
        return found.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    void BindCardOffersToNamedSlots(List<CardSlotBinding> slots)
    {
        if (slots == null) return;

        for (int i = 0; i < slots.Count; i++)
        {
            CardSlotBinding slot = slots[i];
            if (slot.container == null || slot.ui == null) continue;

            bool visible = i < cardOffers.Count
                && cardOffers[i] != null
                && cardOffers[i].card != null;
            slot.container.gameObject.SetActive(true);
            slot.ui.gameObject.SetActive(visible);
            slot.ui.SetExternalPriceText(slot.priceText);

            if (!visible)
            {
                slot.ui.ClearPriceText();
                continue;
            }

            CardOffer offer = cardOffers[i];
            string title = offer.card.displayName;
            string desc = string.IsNullOrWhiteSpace(offer.card.description) ? "카드 설명 없음" : offer.card.description;
            Sprite icon = ResolveCardSprite(offer.card);
            slot.ui.Setup(title, desc, icon, offer.price, () => TryBuyCard(offer));
            slot.ui.SetRelicIconOnlyMode(false);
            slot.ui.SetCardVisualScale(shopCardVisualScale);
            slot.ui.SetMoneyIconScale(shopMoneyIconScale);
            slot.ui.SetupCardVisual(offer.card, icon);
            slot.ui.SetPurchasedState(offer.purchased);
        }
    }

    void BindRelicOffersToDirectSlots(List<ShopOfferItemUI> slots)
    {
        if (slots == null) return;

        for (int i = 0; i < slots.Count; i++)
        {
            var ui = slots[i];
            if (ui == null) continue;

            bool visible = i < relicOffers.Count;
            ui.gameObject.SetActive(visible);
            if (!visible)
            {
                ui.ClearPriceText();
                continue;
            }

            RelicOffer offer = relicOffers[i];
            string title = ResolveRelicTitle(offer);
            string desc = ResolveRelicDescription(offer);
            ui.Setup(title, desc, ResolveRelicIcon(offer), offer.price, () => TryBuyRelic(offer));
            ui.SetPriceFontSize(shopRelicPriceFontSize);
            ui.SetRelicIconScale(shopRelicIconScale);
            ui.SetRelicIconOnlyMode(true);
            ui.SetPurchasedState(offer != null && offer.purchased);
        }
    }

    static string ResolveRelicTitle(RelicOffer offer)
    {
        if (offer == null) return "유물";
        if (!string.IsNullOrWhiteSpace(offer.displayName)) return offer.displayName;
        if (offer.relic != null && !string.IsNullOrWhiteSpace(offer.relic.displayName)) return offer.relic.displayName;
        return offer.sprite != null ? offer.sprite.name : "유물";
    }

    static string ResolveRelicDescription(RelicOffer offer)
    {
        if (offer == null) return "유물 설명 없음";
        if (!string.IsNullOrWhiteSpace(offer.description)) return offer.description;
        if (offer.relic != null && !string.IsNullOrWhiteSpace(offer.relic.description)) return offer.relic.description;
        return "유물 설명 없음";
    }

    static Sprite ResolveRelicIcon(RelicOffer offer)
    {
        if (offer == null) return null;
        if (offer.sprite != null) return offer.sprite;
        return offer.relic != null ? offer.relic.icon : null;
    }

    void BindComboOfferToObject(GameObject target, ComboOffer offer)
    {
        if (target == null || offer == null || offer.combo == null) return;

        // 1. 상점 기본 UI 스크립트 가져오기
        ShopOfferItemUI ui = GetOrAttachOfferItemUI(target, false);
        if (ui == null) return;

        // 2. 콤보 책 비주얼(BookRewardItemUI)을 먼저 생성하고 위치시키기
        Transform mount = target.transform;
        if (!string.IsNullOrWhiteSpace(comboBookMountName))
        {
            Transform namedMount = FindChildRecursiveByName(target.transform, comboBookMountName);
            if (namedMount != null)
                mount = namedMount;
        }

        BookRewardItemUI bookUI = mount.GetComponent<BookRewardItemUI>() ?? mount.GetComponentInChildren<BookRewardItemUI>(true);
        if (bookUI == null && comboBookItemPrefab != null)
        {
            BookRewardItemUI instance = Instantiate(comboBookItemPrefab);
            instance.transform.SetParent(mount, false);
            instance.gameObject.SetActive(true);
            bookUI = instance;
        }

        // 3. 책이 생성된 '이후'에 상점 Setup을 실행 (그래야 스크립트가 책 이미지를 인식하고 구매 시 투명하게 숨길 수 있음)
        string title = string.IsNullOrWhiteSpace(offer.combo.displayName) ? "콤보 스킬" : offer.combo.displayName;
        string desc = string.IsNullOrWhiteSpace(offer.combo.descriptionKR)
            ? $"콤보: {offer.combo.ComboString()}"
            : $"콤보: {offer.combo.ComboString()}\n{offer.combo.descriptionKR}";
        
        Sprite icon = (bookUI != null) ? null : offer.combo.skillIcon;

        ui.Setup(title, desc, icon, offer.price, () => TryBuyCombo(offer));
        ui.SetRelicIconOnlyMode(false);

        // 슬롯(ComboSkill 슬롯)에도 ButtonHoverScale이 있으면, 카드/렐릭 구매 후 RefreshOfferViews로
        // 다시 바인딩될 때 직전 호버 상태가 남아 "콤보 아이콘이 계속 확대된 채 호버됨"이 발생한다.
        // 매 바인딩마다 슬롯 호버 상태를 초기화한다.
        var slotHover = target.GetComponent<ButtonHoverScale>();
        if (slotHover != null)
            slotHover.ResetHoverState();

        // 4. 책의 비주얼 업데이트 및 구매 여부에 따른 '버튼 잠금'
        if (bookUI != null)
        {
            // 책 루트의 ButtonHoverScale이 매 프레임 localScale을 base로 되돌리므로 직접 대입하면 무시된다.
            // 호버 컴포넌트의 기준 스케일 자체를 comboBookScale로 갱신한다(없으면 직접 대입 폴백).
            var bookHover = bookUI.GetComponent<ButtonHoverScale>();
            if (bookHover != null)
                bookHover.SetBaseScale(comboBookScale);
            else
                bookUI.transform.localScale = Vector3.one * comboBookScale;

            bookUI.SetElementSprites(comboBookFireSprite, comboBookWaterSprite, comboBookWindSprite, comboBookEarthSprite);
            
            Button bookBtn = bookUI.GetComponent<Button>();

            if (!offer.purchased) {
                // 구매 전: 정상적으로 동작하게 바인딩
                bookUI.Bind(offer.combo, _ => TryBuyCombo(offer));
                if (bookBtn != null) bookBtn.interactable = true;
            } else {
                if (bookBtn != null) bookBtn.interactable = false;
            }
            bookUI.SetSelected(false);
        }

        // 5. 최종 구매 상태 반영 (SOLD 텍스트 표시 및 비주얼 반투명화)
        ui.SetPurchasedState(offer.purchased);
    }

    TemplateBinding ResolveTemplate(Transform root, GameObject explicitTemplate, bool allowSharedPrefabFallback)
    {
        if (explicitTemplate != null)
        {
            return new TemplateBinding
            {
                template = explicitTemplate,
                keepObject = explicitTemplate.transform.parent == root ? explicitTemplate : null,
            };
        }

        if (allowSharedPrefabFallback && shopOfferItemPrefab != null)
        {
            return new TemplateBinding
            {
                template = shopOfferItemPrefab,
                keepObject = null,
            };
        }

        if (root != null)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i).gameObject;
                if (child.GetComponent<ShopOfferItemUI>() == null) continue;

                child.SetActive(false);
                return new TemplateBinding
                {
                    template = child,
                    keepObject = child,
                };
            }
        }

        Debug.LogWarning($"[ShopStageController] 템플릿이 없어 상품 UI를 생성할 수 없습니다. Root={root?.name}");
        return default;
    }

    static void ClearChildren(Transform root, GameObject keepObject)
    {
        if (root == null) return;
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            GameObject child = root.GetChild(i).gameObject;
            if (keepObject != null && child == keepObject) continue;
            Destroy(child);
        }
    }

    RelicStageController ResolveRelicStageController()
    {
        if (relicStageController != null)
            return relicStageController;

        GameStateController state = GameStateController.Instance;
        if (state != null && state.relicStage != null)
        {
            relicStageController = state.relicStage.GetComponentInChildren<RelicStageController>(true);
            if (relicStageController != null)
                return relicStageController;
        }

        relicStageController = FindFirstObjectByType<RelicStageController>(FindObjectsInactive.Include);
        return relicStageController;
    }

    Transform ResolveStrictCardRoot()
    {
        if (!string.IsNullOrWhiteSpace(topCardsRootName))
        {
            Transform byCanvas = FindChildRecursiveByName(shopCanvas2 != null ? shopCanvas2.transform : transform, topCardsRootName);
            if (byCanvas != null) return byCanvas;
        }

        return cardOffersRoot;
    }

    Transform ResolveStrictRelicRoot()
    {
        if (!string.IsNullOrWhiteSpace(bottomLeftRelicsRootName))
        {
            Transform byCanvas = FindChildRecursiveByName(shopCanvas2 != null ? shopCanvas2.transform : transform, bottomLeftRelicsRootName);
            if (byCanvas != null) return byCanvas;
        }

        return relicOffersRoot;
    }

    public void ReturnToMapFromShop()
    {
        if (roundManager != null)
        {
            roundManager.ReturnToMap();
            return;
        }

        var stateController = GameStateController.Instance;
        if (stateController == null) return;

        stateController.MarkNodeCleared(stateController.lastVisitedNodeIndex);
        stateController.ShowMap();
    }

}
