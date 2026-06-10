using System.Collections;
using System.Collections.Generic;
using Battle;
using Battle.Card;
using Battle.Relic;
using Battle.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 상점 스테이지 진행 및 카드/렐릭/콤보 상품 구성·구매 처리
public class ShopStageController : MonoBehaviour
{
    [Header("Canvas 1 (상점 입구)")]
    [SerializeField] private GameObject shopCanvas1; // 상점 입구 캔버스
    [SerializeField] private Button shopOwnerButton; // 상점 주인 버튼
    [SerializeField] private Button leaveShopButton; // 상점 나가기 버튼

    [Header("Canvas 1 - 말풍선 타이핑")]
    [SerializeField] private TextMeshProUGUI shopBubbleText; // 말풍선 텍스트
    [SerializeField, TextArea(2, 4)] private string shopBubbleMessage = "무엇을 원하시오, 여행자여?"; // 말풍선 기본 문구
    [SerializeField, Min(1f)] private float shopBubbleCharsPerSecond = 28f; // 타이핑 속도(초당 글자)
    [SerializeField] private bool completeTypingOnOwnerClick = true; // 주인 클릭 시 타이핑 즉시 완료 여부

    [Header("Canvas 2 (상품 화면)")]
    [SerializeField] private GameObject shopCanvas2; // 상품 화면 캔버스
    [SerializeField] private Button backToCanvas1Button; // 뒤로가기 버튼

    [Header("Canvas 2 - 상품 루트")]
    [SerializeField] private Transform cardOffersRoot; // 카드 상품 루트
    [SerializeField] private Transform relicOffersRoot; // 렐릭 상품 루트
    [SerializeField] private Transform comboOffersRoot; // 콤보 상품 루트

    [Header("구조 강제 루트명")]
    [SerializeField] private string topCardsRootName = "Top_Cards"; // 카드 루트 강제 탐색 이름
    [SerializeField] private string bottomLeftRelicsRootName = "Bottom_LeftRelics"; // 렐릭 루트 강제 탐색 이름

    [Header("Canvas 2 - 이름 기반 슬롯 찾기")]
    [SerializeField] private bool useNamedSlots = true; // 이름 기반 슬롯 사용 여부
    [SerializeField] private string cardSlotPrefix = "Card"; // 카드 슬롯 이름 접두사
    [SerializeField] private string relicSlotPrefix = "Relic"; // 렐릭 슬롯 이름 접두사
    [SerializeField] private string comboSlotPrefix = "ComboSkill"; // 콤보 슬롯 이름 접두사

    [Header("Canvas 2 - 공용 프리팹")]
    [SerializeField] private GameObject shopOfferItemPrefab; // 공용 상품 아이템 프리팹

    [Header("Canvas 2 - 컨테이너별 템플릿 (Optional)")]
    [SerializeField] private GameObject cardOfferTemplate; // 카드 상품 템플릿
    [SerializeField] private GameObject relicOfferTemplate; // 렐릭 상품 템플릿
    [SerializeField] private GameObject comboOfferTemplate; // 콤보 상품 템플릿

    [Header("렐릭 소스")]
    [SerializeField] private RelicStageController relicStageController; // 렐릭 후보 제공 컨트롤러

    [Header("Canvas 2 - 카드 제거")]
    [SerializeField] private Button removeCardButton; // 카드 제거 버튼
    [SerializeField] private TextMeshProUGUI removeCardCostText; // 카드 제거 비용 텍스트
    [SerializeField] private int removeCardCost = 120; // 카드 제거 비용

    [Header("표시 개수")]
    [SerializeField] private int cardOfferCount = 5; // 카드 상품 개수
    [SerializeField] private int relicOfferCount = 3; // 렐릭 상품 개수
    [SerializeField] private int comboOfferCount = 1; // 콤보 상품 개수

    [Header("가격 범위")]
    [SerializeField] private int cardMinPrice = 60; // 카드 최소 가격
    [SerializeField] private int cardMaxPrice = 150; // 카드 최대 가격
    [SerializeField] private int relicMinPrice = 140; // 렐릭 최소 가격
    [SerializeField] private int relicMaxPrice = 260; // 렐릭 최대 가격
    [SerializeField] private int comboMinPrice = 180; // 콤보 최소 가격
    [SerializeField] private int comboMaxPrice = 320; // 콤보 최대 가격

    [Header("카드 표시 (Shop)")]
    [Tooltip("상점 카드(NewSkillCard) 전체 스케일")]
    [SerializeField, Range(0.5f, 1.2f)] private float shopCardVisualScale = 1f; // 상점 카드 비주얼 스케일
    [Tooltip("상점 카드 가격 옆 재화 아이콘 스케일")]
    [SerializeField, Range(1f, 2.5f)] private float shopMoneyIconScale = 1.35f; // 상점 카드 재화 아이콘 스케일

    [Header("렐릭 표시 (Shop)")]
    [Tooltip("상점 렐릭 아이콘 스케일")]
    [SerializeField, Range(1f, 2.5f)] private float shopRelicIconScale = 1.45f; // 상점 렐릭 아이콘 스케일
    [Tooltip("상점 렐릭 가격 텍스트 폰트 크기")]
    [SerializeField, Range(12f, 72f)] private float shopRelicPriceFontSize = 36f; // 상점 렐릭 가격 폰트 크기

    [Header("콤보 책 표시 (Shop)")]
    [SerializeField] private BookRewardItemUI comboBookItemPrefab; // 콤보 책 아이템 프리팹
    [SerializeField] private string comboBookMountName = "Mid_Combo"; // 콤보 책 장착 마운트 이름
    [SerializeField, Range(0.3f, 1.5f)] private float comboBookScale = 1f; // 콤보 책 스케일
    [SerializeField] private Sprite comboBookFireSprite; // 콤보 책 불 속성 스프라이트
    [SerializeField] private Sprite comboBookWaterSprite; // 콤보 책 물 속성 스프라이트
    [SerializeField] private Sprite comboBookWindSprite; // 콤보 책 바람 속성 스프라이트
    [SerializeField] private Sprite comboBookEarthSprite; // 콤보 책 땅 속성 스프라이트

    private readonly List<CardOffer> cardOffers = new List<CardOffer>(); // 현재 카드 상품 목록
    private readonly List<RelicOffer> relicOffers = new List<RelicOffer>(); // 현재 렐릭 상품 목록
    private readonly List<ComboOffer> comboOffers = new List<ComboOffer>(); // 현재 콤보 상품 목록

    private RoundManager roundManager; // 라운드 매니저 참조
    private bool bound; // 런타임 바인딩 완료 여부
    private Coroutine bubbleTypingRoutine; // 말풍선 타이핑 코루틴
    private bool isBubbleTyping; // 말풍선 타이핑 진행 중 여부

    // 카드 상품 한 건의 데이터
    class CardOffer
    {
        public CardData card; // 카드 데이터
        public int price; // 가격
        public bool purchased; // 구매 완료 여부
    }

    // 렐릭 상품 한 건의 데이터
    class RelicOffer
    {
        public RelicDef relic; // 렐릭 정의
        public bool isSpriteOnly; // 스프라이트 전용(효과 미구현) 여부
        public Sprite sprite; // 스프라이트
        public string displayName; // 표시 이름
        public string description; // 설명
        public int price; // 가격
        public bool purchased; // 구매 완료 여부
    }

    // 콤보 상품 한 건의 데이터
    class ComboOffer
    {
        public ComboSkillDef combo; // 콤보 스킬 정의
        public int price; // 가격
        public bool purchased; // 구매 완료 여부
    }

    // 런타임 바인딩 및 말풍선 폴백 문구 캡처
    void Awake()
    {
        BindRuntime();
        CaptureBubbleFallbackText();
    }

    // 활성화 시 바인딩 후 상점 진입
    void OnEnable()
    {
        BindRuntime();
        EnterShopStage();
    }

    // 인스펙터 값 변경 시 범위 클램프
    void OnValidate()
    {
        shopCardVisualScale = Mathf.Clamp(shopCardVisualScale, 0.5f, 1.2f);
        shopMoneyIconScale = Mathf.Clamp(shopMoneyIconScale, 1f, 2.5f);
        shopRelicIconScale = Mathf.Clamp(shopRelicIconScale, 1f, 2.5f);
        shopRelicPriceFontSize = Mathf.Clamp(shopRelicPriceFontSize, 12f, 72f);
        comboBookScale = Mathf.Clamp(comboBookScale, 0.3f, 1.5f);
    }

    // 라운드 매니저를 받아 상점 시작
    public void BeginShop(RoundManager manager)
    {
        roundManager = manager;
        EnterShopStage();
    }

    // 버튼 클릭 리스너를 1회 바인딩
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

    // 상점 진입: 입구 화면 표시 및 버튼 텍스트 갱신
    void EnterShopStage()
    {
        ShowCanvas1();
        UpdateRemoveCardButtonText();
    }

    // 입구 캔버스를 표시하고 말풍선 타이핑 재생
    void ShowCanvas1()
    {
        if (shopCanvas1 != null) shopCanvas1.SetActive(true);
        if (shopCanvas2 != null) shopCanvas2.SetActive(false);
        PlayShopBubbleTyping();
    }

    // 상품 화면 열기(타이핑 중이면 먼저 완료)
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

    // 비활성화 시 말풍선 타이핑 정지
    void OnDisable()
    {
        StopShopBubbleTyping();
    }

    // 빈 문구일 때 현재 텍스트를 폴백 문구로 캡처
    void CaptureBubbleFallbackText()
    {
        if (!string.IsNullOrWhiteSpace(shopBubbleMessage)) return;
        if (shopBubbleText == null) return;
        if (string.IsNullOrWhiteSpace(shopBubbleText.text)) return;

        shopBubbleMessage = shopBubbleText.text;
    }

    // 말풍선 타이핑 연출 시작
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

    // 글자를 한 자씩 노출하는 타이핑 코루틴
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

    // 타이핑을 즉시 완료해 전체 문구 표시
    void CompleteShopBubbleTyping()
    {
        if (shopBubbleText == null) return;

        string message = string.IsNullOrWhiteSpace(shopBubbleMessage) ? shopBubbleText.text : shopBubbleMessage;
        StopShopBubbleTyping();
        shopBubbleText.text = message;
        shopBubbleText.maxVisibleCharacters = int.MaxValue;
    }

    // 진행 중인 타이핑 코루틴 정지
    void StopShopBubbleTyping()
    {
        isBubbleTyping = false;
        if (bubbleTypingRoutine == null) return;

        StopCoroutine(bubbleTypingRoutine);
        bubbleTypingRoutine = null;
    }

    // 카드/렐릭/콤보 상품 목록을 새로 생성
    void RegenerateOffers()
    {
        BuildCardOffers();
        BuildRelicOffers();
        BuildComboOffers();
        UpdateRemoveCardButtonText();
    }

    // 보유하지 않은 속성 카드 풀에서 카드 상품 구성
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

    // 렐릭 후보(스프라이트 전용)에서 렐릭 상품 구성
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

    // 미보유 콤보 풀에서 콤보 상품 구성
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

    // 모든 상품 뷰 갱신
    void RefreshOfferViews()
    {
        RefreshCardOfferViews();
        RefreshRelicOfferViews();
        RefreshComboOfferViews();
        UpdateRemoveCardButtonText();
    }

    // 카드 상품을 이름 기반 슬롯에 바인딩
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

    // 렐릭 상품을 이름/직접 슬롯에 바인딩
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
        }

        var directSlots = CollectDirectSlots(relicOffersRoot, false);
        if (ShouldUseDirectSlots(directSlots.Count, relicOfferCount))
        {
            BindRelicOffersToDirectSlots(directSlots);
            return;
        }

        Debug.LogWarning("[ShopStageController] 렐릭 슬롯 바인딩 실패: Bottom_LeftRelics 하위 Relic 슬롯 수를 확인하세요.");
    }

    // 콤보 상품을 슬롯 또는 템플릿으로 바인딩
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

    // 카드 구매 시도(골드 차감 후 덱에 추가)
    void TryBuyCard(CardOffer offer)
    {
        if (offer == null || offer.card == null) return;
        if (offer.purchased) return;
        if (!TrySpendMoney(offer.price)) return;

        RunDeckState.EnsureExists().AddCard(offer.card.id);
        offer.purchased = true;
        RefreshOfferViews();
    }

    // 렐릭 구매 시도(골드 차감 후 보유 추가)
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

    // 콤보 구매 시도(골드 차감 후 보유 콤보 추가)
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

    // 카드 제거 버튼 클릭(현재 준비 중)
    void OnRemoveCardButtonClicked()
    {
        Debug.Log($"[ShopStageController] 카드 제거 기능은 아직 준비 중입니다. (예정 비용: {removeCardCost})");
    }

    // 골드 차감 시도 후 성공 여부 반환
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

    // 카드 제거 버튼 텍스트 갱신
    void UpdateRemoveCardButtonText()
    {
        if (removeCardCostText != null)
            removeCardCostText.text = "카드 제거 (준비중)";
    }

    // 4속성(불/물/바람/땅) 카드인지 판정
    static bool IsElementCard(CardData card)
    {
        if (card == null) return false;
        return card.element == CardElement.Fire
            || card.element == CardElement.Water
            || card.element == CardElement.Wind
            || card.element == CardElement.Earth;
    }

    // 카드의 스킬 이미지 스프라이트를 리소스에서 로드
    static Sprite ResolveCardSprite(CardData card)
    {
        if (card == null || string.IsNullOrWhiteSpace(card.skillImg)) return null;
        return Resources.Load<Sprite>($"CardIcons/{card.skillImg}");
    }

    // 소스 목록에서 중복 없이 count개를 무작위 추출
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

    // 템플릿 생성 정보
    struct TemplateBinding
    {
        public GameObject template; // 인스턴스화할 템플릿
        public GameObject keepObject; // 삭제하지 않고 유지할 오브젝트
    }

    // 카드 슬롯 바인딩 정보
    struct CardSlotBinding
    {
        public Transform container; // 슬롯 컨테이너
        public ShopOfferItemUI ui; // 상품 UI
        public TextMeshProUGUI priceText; // 가격 텍스트
    }

    // 루트의 직접 자식들에서 상품 UI 슬롯 수집
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

    // 직접 슬롯 사용이 적합한지 판정
    static bool ShouldUseDirectSlots(int slotCount, int offerCount)
    {
        if (slotCount <= 0) return false;
        if (offerCount <= 1) return true;
        return slotCount >= offerCount;
    }

    // 접두사+번호 이름의 슬롯들에서 상품 UI 수집
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

    // 접두사+번호 카드 슬롯들의 컨테이너/UI/가격텍스트 바인딩 수집
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

    // 이름이 일치하는 노드를 재귀 탐색(루트 포함)
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

    // 공백 제거·대소문자 무시로 노드 이름 비교
    static bool IsSameNodeName(string actual, string expected)
    {
        if (string.IsNullOrWhiteSpace(actual) || string.IsNullOrWhiteSpace(expected)) return false;

        string normalizedActual = actual.Trim().Replace(" ", string.Empty);
        string normalizedExpected = expected.Trim().Replace(" ", string.Empty);
        return string.Equals(normalizedActual, normalizedExpected, System.StringComparison.OrdinalIgnoreCase);
    }

    // 대상에서 ShopOfferItemUI를 찾거나 없으면 부착해 반환
    static ShopOfferItemUI GetOrAttachOfferItemUI(GameObject target, bool allowNewCardViewAttach = true)
    {
        if (target == null) return null;

        ShopOfferItemUI ui = target.GetComponent<ShopOfferItemUI>();
        if (ui != null) return ui;

        ui = target.GetComponentInChildren<ShopOfferItemUI>(true);
        if (ui != null) return ui;

        if (!allowNewCardViewAttach)
            return target.AddComponent<ShopOfferItemUI>();

        NewCardView cardView = target.GetComponent<NewCardView>();
        if (cardView != null)
            return target.AddComponent<ShopOfferItemUI>();

        cardView = target.GetComponentInChildren<NewCardView>(true);
        if (cardView != null)
            return cardView.gameObject.AddComponent<ShopOfferItemUI>();

        return null;
    }

    // 카드 슬롯의 상품 UI를 찾거나 템플릿으로 생성
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

    // 이름으로 TMP 텍스트를 탐색(중첩 자식 포함)
    static TextMeshProUGUI FindNamedText(Transform root, string targetName)
    {
        Transform found = FindChildRecursiveByName(root, targetName);
        if (found == null) return null;

        TextMeshProUGUI text = found.GetComponent<TextMeshProUGUI>();
        if (text != null) return text;

        return found.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    // 카드 상품들을 카드 슬롯에 채워 표시
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

    // 렐릭 상품들을 직접 슬롯에 채워 표시
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

    // 렐릭 상품의 표시 이름 결정
    static string ResolveRelicTitle(RelicOffer offer)
    {
        if (offer == null) return "유물";
        if (!string.IsNullOrWhiteSpace(offer.displayName)) return offer.displayName;
        if (offer.relic != null && !string.IsNullOrWhiteSpace(offer.relic.displayName)) return offer.relic.displayName;
        return offer.sprite != null ? offer.sprite.name : "유물";
    }

    // 렐릭 상품의 설명 결정
    static string ResolveRelicDescription(RelicOffer offer)
    {
        if (offer == null) return "유물 설명 없음";
        if (!string.IsNullOrWhiteSpace(offer.description)) return offer.description;
        if (offer.relic != null && !string.IsNullOrWhiteSpace(offer.relic.description)) return offer.relic.description;
        return "유물 설명 없음";
    }

    // 렐릭 상품의 아이콘 결정
    static Sprite ResolveRelicIcon(RelicOffer offer)
    {
        if (offer == null) return null;
        if (offer.sprite != null) return offer.sprite;
        return offer.relic != null ? offer.relic.icon : null;
    }

    // 콤보 상품을 슬롯 오브젝트에 책 비주얼과 함께 바인딩
    void BindComboOfferToObject(GameObject target, ComboOffer offer)
    {
        if (target == null || offer == null || offer.combo == null) return;

        ShopOfferItemUI ui = GetOrAttachOfferItemUI(target, false);
        if (ui == null) return;

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

        string title = string.IsNullOrWhiteSpace(offer.combo.displayName) ? "콤보 스킬" : offer.combo.displayName;
        string desc = string.IsNullOrWhiteSpace(offer.combo.descriptionKR)
            ? $"콤보: {offer.combo.ComboString()}"
            : $"콤보: {offer.combo.ComboString()}\n{offer.combo.descriptionKR}";
        
        Sprite icon = (bookUI != null) ? null : offer.combo.skillIcon;

        ui.Setup(title, desc, icon, offer.price, () => TryBuyCombo(offer));
        ui.SetRelicIconOnlyMode(false);

        var slotHover = target.GetComponent<ButtonHoverScale>();
        if (slotHover != null)
            slotHover.ResetHoverState();

        if (bookUI != null)
        {
            var bookHover = bookUI.GetComponent<ButtonHoverScale>();
            if (bookHover != null)
                bookHover.SetBaseScale(comboBookScale);
            else
                bookUI.transform.localScale = Vector3.one * comboBookScale;

            bookUI.SetElementSprites(comboBookFireSprite, comboBookWaterSprite, comboBookWindSprite, comboBookEarthSprite);
            
            Button bookBtn = bookUI.GetComponent<Button>();

            if (!offer.purchased) {
                bookUI.Bind(offer.combo, _ => TryBuyCombo(offer));
                if (bookBtn != null) bookBtn.interactable = true;
            } else {
                if (bookBtn != null) bookBtn.interactable = false;
            }
            bookUI.SetSelected(false);
        }

        ui.SetPurchasedState(offer.purchased);
    }

    // 명시 템플릿/공용 프리팹/자식 중에서 사용할 템플릿 결정
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

    // 유지 대상을 제외한 모든 자식 제거
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

    // 렐릭 컨트롤러 참조를 확보(상태/씬 탐색 폴백)
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

    // 강제 이름으로 카드 루트를 탐색해 반환
    Transform ResolveStrictCardRoot()
    {
        if (!string.IsNullOrWhiteSpace(topCardsRootName))
        {
            Transform byCanvas = FindChildRecursiveByName(shopCanvas2 != null ? shopCanvas2.transform : transform, topCardsRootName);
            if (byCanvas != null) return byCanvas;
        }

        return cardOffersRoot;
    }

    // 강제 이름으로 렐릭 루트를 탐색해 반환
    Transform ResolveStrictRelicRoot()
    {
        if (!string.IsNullOrWhiteSpace(bottomLeftRelicsRootName))
        {
            Transform byCanvas = FindChildRecursiveByName(shopCanvas2 != null ? shopCanvas2.transform : transform, bottomLeftRelicsRootName);
            if (byCanvas != null) return byCanvas;
        }

        return relicOffersRoot;
    }

    // 상점에서 맵으로 복귀(노드 클리어 처리)
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
