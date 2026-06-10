using System.Collections.Generic;
using UnityEngine;

// 속성 슬롯 시스템 — Q=불/W=물/E=바람/R=땅 고정 슬롯, 전투 간 런 덱 유지
public class ElementSlotSystem : MonoBehaviour
{
    public static readonly string[] SLOT_KEYS    = { "Q", "W", "E", "R" }; // 슬롯 키 목록
    public static readonly string[] ELEMENT_KEYS = { "fire", "water", "wind", "earth" }; // 속성 키 목록
    public const string NEUTRAL_CARD = "neutral"; // 무속성 카드 식별자

    public const int CARDS_PER_ELEMENT = 3; // 런 덱 속성당 기본 카드 수
    public const int SLOT_CURSE_TURNS  = 2; // 저주 지속 횟수
    public const int CURSED_SLOT_DAMAGE = 6; // 저주 반동 피해

    // 전투 간 유지되는 런 덱 카드
    [System.Serializable]
    public class RunDeckCard
    {
        public string elementKey;  // 속성 키
        public int    upgradeLevel; // 카드 강화 횟수

        // 카드 생성
        public RunDeckCard(string key, int upgrade = 0)
        {
            elementKey   = key;
            upgradeLevel = upgrade;
        }

        public int BaseDamage => 1 + upgradeLevel; // 기본 피해(1+강화레벨)
        public int EffectBonus => upgradeLevel; // 부가효과 보너스(강화레벨)
    }

    // 개별 슬롯의 런타임 상태
    public class SlotState
    {
        public string       elementKey;   // 이 슬롯에 고정된 속성
        public string       slotKey;      // 슬롯 키(Q/W/E/R)
        public RunDeckCard  currentCard;  // 현재 장전된 카드(null=빈 슬롯)
        public bool         hasNeutralCard; // 무속성 카드 혼입 여부
        public int          neutralDeckCount; // 뽑기 대기 중인 무속성 카드 수
        public int          neutralGraveCount; // 사용 후 대기 중인 무속성 카드 수
        public int          curseTurns;   // 저주 남은 횟수(0=정상)

        public List<RunDeckCard> deck  = new List<RunDeckCard>(); // 슬롯 덱
        public List<RunDeckCard> grave = new List<RunDeckCard>(); // 슬롯 묘지

        public bool IsEmpty   => currentCard == null && !hasNeutralCard && deck.Count == 0; // 완전히 빈 슬롯인지
        public bool IsCursed  => curseTurns > 0; // 저주 상태인지
        public bool HasCard   => currentCard != null || hasNeutralCard; // 장전된 카드 보유 여부
        public int  RemainingCount => (currentCard != null ? 1 : 0) + (hasNeutralCard ? 1 : 0) + deck.Count + neutralDeckCount; // 남은 카드 총수
    }

    public static ElementSlotSystem Instance; // 싱글턴 인스턴스

    [Header("런 덱 상태 (디버그)")]
    [SerializeField] private int runDeckCount; // 런 덱 카드 수(인스펙터 표시용)

    private List<RunDeckCard> _runDeck = new List<RunDeckCard>(); // 런 덱
    public System.Collections.ObjectModel.ReadOnlyCollection<RunDeckCard> RunDeck => _runDeck.AsReadOnly(); // 런 덱 읽기 전용 뷰

    // 속성별 전체 강화 레벨
    private Dictionary<string, int> _elementUpgradeLevels = new Dictionary<string, int>
    {
        { "fire", 0 }, { "water", 0 }, { "wind", 0 }, { "earth", 0 }
    };
    public System.Collections.Generic.IReadOnlyDictionary<string, int> ElementUpgradeLevels => _elementUpgradeLevels; // 속성 강화 레벨 읽기 전용 뷰

    private SlotState[] _slots = new SlotState[4]; // 슬롯 배열

    [Header("슬롯 피해 계산")]
    [SerializeField] private float fallbackPlayerAttackDamage = 10f; // 플레이어 공격력 대체값
    [SerializeField] private float slotDamagePerAttackPoint = 0.1f; // 공격력 1당 슬롯 피해 계수

    private ComboSystem     comboSystem; // 콤보 시스템 참조
    private Player          player; // 플레이어 참조
    private EnemyController enemyController; // 적 참조
    private bool            inBattle; // 전투 진행 여부
    public bool InBattle => inBattle; // 전투 진행 여부(읽기 전용)

    // 싱글턴 설정 및 런 덱 초기화
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        InitRunDeck();
    }

    // 참조 확보 및 HUD 보장
    void Start()
    {
        comboSystem = FindFirstObjectByType<ComboSystem>();
        player      = Player.Resolve(true);
        RefreshEnemyRef();
        EnsureHudComponent();
    }

    // 전투 중 입력 및 참조 갱신
    void Update()
    {
        runDeckCount = _runDeck.Count;

        if (!inBattle) return;
        RefreshEnemyRef();
        HandleInput();
    }

    // 적/플레이어 참조 재확보
    void RefreshEnemyRef()
    {
        if (enemyController == null || !enemyController.gameObject.activeInHierarchy)
            enemyController = FindFirstObjectByType<EnemyController>();

        if (player == null || !player.gameObject.activeInHierarchy)
            player = Player.Resolve(true);
    }

    // HUD 컴포넌트 보장(없으면 추가)
    void EnsureHudComponent()
    {
        ElementSlotHUD existingHud = FindFirstObjectByType<ElementSlotHUD>();
        if (existingHud != null)
            return;

        gameObject.AddComponent<ElementSlotHUD>();
        Debug.Log("[ElementSlotSystem] ElementSlotHUD가 없어 자동으로 추가했습니다.");
    }

    // 런 처음 시작 시 기본 런 덱 생성
    public void InitRunDeck()
    {
        _runDeck.Clear();
        foreach (string elem in ELEMENT_KEYS)
            for (int i = 0; i < CARDS_PER_ELEMENT; i++)
                _runDeck.Add(new RunDeckCard(elem));

        Debug.Log($"[ElementSlotSystem] 런 덱 초기화: {_runDeck.Count}장");
    }

    // 전투 시작 — 속성별 덱 빌드 후 첫 카드 드로우
    public void StartBattle()
    {
        inBattle = true;

        player      = Player.Resolve(true);
        comboSystem = FindFirstObjectByType<ComboSystem>();
        RefreshEnemyRef();

        for (int i = 0; i < 4; i++)
        {
            _slots[i] = new SlotState
            {
                slotKey       = SLOT_KEYS[i],
                elementKey    = ELEMENT_KEYS[i],
                curseTurns    = 0,
                hasNeutralCard = false,
                neutralDeckCount = 0,
                neutralGraveCount = 0
            };
        }

        BuildElementDecksFromRunDeck();
        SyncEmptySlots();

        Debug.Log("[ElementSlotSystem] 전투 시작 — 슬롯 준비 완료");
    }

    // 전투 종료 처리
    public void EndBattle()
    {
        inBattle = false;
        Debug.Log("[ElementSlotSystem] 전투 종료");
    }

    // 런 덱을 셔플해 속성별 슬롯 덱으로 분배
    void BuildElementDecksFromRunDeck()
    {
        foreach (var slot in _slots)
        {
            slot.deck.Clear();
            slot.grave.Clear();
            slot.currentCard   = null;
            slot.hasNeutralCard = false;
            slot.neutralDeckCount = 0;
            slot.neutralGraveCount = 0;
        }

        List<RunDeckCard> shuffled = new List<RunDeckCard>(_runDeck);
        ShuffleList(shuffled);

        foreach (var card in shuffled)
        {
            int slotIndex = System.Array.IndexOf(ELEMENT_KEYS, card.elementKey);
            if (slotIndex >= 0)
                _slots[slotIndex].deck.Add(card);
        }

        Debug.Log("[ElementSlotSystem] 속성별 덱 빌드 완료");
    }

    // 카드 없는 슬롯에 덱에서 드로우(전량 소진 시 재구성)
    void SyncEmptySlots()
    {
        for (int i = 0; i < 4; i++)
        {
            if (!_slots[i].HasCard && (_slots[i].deck.Count > 0 || _slots[i].neutralDeckCount > 0))
                DrawCardForSlot(i);
        }

        if (AreAllElementResourcesSpent())
            RebuildSpentElementDecks();
    }

    // 슬롯에 카드 1장 드로우(무속성/일반 가중 추첨)
    void DrawCardForSlot(int index)
    {
        var slot = _slots[index];
        if (slot.HasCard) return;

        int normalCount = slot.deck.Count;
        int neutralCount = slot.neutralDeckCount;
        int total = normalCount + neutralCount;
        if (total == 0) return;

        int pick = Random.Range(0, total);
        if (pick < neutralCount)
        {
            slot.hasNeutralCard = true;
            slot.neutralDeckCount--;
            return;
        }

        slot.currentCard = slot.deck[0];
        slot.deck.RemoveAt(0);
    }

    // 모든 슬롯 자원이 소진됐는지 확인
    bool AreAllElementResourcesSpent()
    {
        foreach (var slot in _slots)
            if (slot.currentCard != null || slot.hasNeutralCard || slot.deck.Count > 0 || slot.neutralDeckCount > 0) return false;
        return true;
    }

    // 묘지를 덱으로 되돌려 슬롯 덱 재구성
    void RebuildSpentElementDecks()
    {
        bool anyGrave = false;
        foreach (var slot in _slots)
            if (slot.grave.Count > 0 || slot.neutralGraveCount > 0) { anyGrave = true; break; }

        if (!anyGrave) return;

        foreach (var slot in _slots)
        {
            slot.deck.AddRange(slot.grave);
            slot.grave.Clear();
            slot.neutralDeckCount += slot.neutralGraveCount;
            slot.neutralGraveCount = 0;
            ShuffleList(slot.deck);
        }

        SyncEmptySlots();
        Debug.Log("[ElementSlotSystem] 모든 슬롯 소진 → 덱 재구성");
    }

    // 키 입력에 따라 해당 슬롯 사용
    void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Q)) UseSlot(0);
        else if (Input.GetKeyDown(KeyCode.W)) UseSlot(1);
        else if (Input.GetKeyDown(KeyCode.E)) UseSlot(2);
        else if (Input.GetKeyDown(KeyCode.R)) UseSlot(3);
    }

    // 슬롯 사용 — 카드 소비/피해/콤보/저주 처리 후 드로우
    public void UseSlot(int index)
    {
        if (!inBattle) return;

        var slot = _slots[index];
        if (!slot.HasCard) return;


        bool isNeutral        = slot.hasNeutralCard;
        RunDeckCard card      = slot.currentCard;
        bool triggeredCurse   = !isNeutral && slot.IsCursed;
        bool consumedRealCard = false;

        if (isNeutral)
        {
            slot.hasNeutralCard = false;
            slot.neutralGraveCount++;
            Debug.Log($"[{SLOT_KEYS[index]}] 무속성 카드 사용 — 콤보 미적용");
        }
        else
        {
            slot.grave.Add(card);
            slot.currentCard = null;
            consumedRealCard = true;
        }

        if (!isNeutral)
            comboSystem?.OnCardUsed(SLOT_KEYS[index]);

        if (!isNeutral && enemyController != null)
        {
            int dmg = CalculateSlotDamage(card, slot.elementKey);
            enemyController.TakeDamage(dmg, SLOT_KEYS[index]);
            Debug.Log($"[{SLOT_KEYS[index]}] {slot.elementKey} 피해 {dmg}");

            int effectAmount = CalculateEffectAmount(card, slot.elementKey);
            Debug.Log($"[부가효과] 속성={slot.elementKey}, cardLevel={card.upgradeLevel}, elementLevel={GetElementUpgradeLevel(slot.elementKey)}, effectAmount={effectAmount}");
            if (effectAmount > 0)
                ApplyElementEffect(slot.elementKey, effectAmount);
        }

        if (triggeredCurse)
        {
            player?.TakeDamage(CURSED_SLOT_DAMAGE);
            Debug.Log($"[{SLOT_KEYS[index]}] 저주 반동 — 플레이어 {CURSED_SLOT_DAMAGE} 피해");
        }

        ConsumeGlobalCurseUse();

        if (consumedRealCard)
            DrawCardForSlot(index);

        SyncEmptySlots();

        enemyController?.OnPlayerAction();
    }

    // 랜덤 슬롯에 저주 또는 무속성 카드 혼입(적 방해)
    public string TriggerDisruptionPattern()
    {
        int  slotIndex = Random.Range(0, 4);
        bool isCurse   = Random.value < 0.5f;

        string resultMessage;

        if (isCurse)
        {
            _slots[slotIndex].curseTurns = SLOT_CURSE_TURNS;

            resultMessage = $"패턴 발동: {SLOT_KEYS[slotIndex]} 카드 저주 {SLOT_CURSE_TURNS}회";
            Debug.Log($"[방해] {SLOT_KEYS[slotIndex]} 슬롯 저주 {SLOT_CURSE_TURNS}회");
        }
        else
        {
            _slots[slotIndex].neutralDeckCount++;

            if (!_slots[slotIndex].hasNeutralCard)
            {
                _slots[slotIndex].hasNeutralCard = true;
                _slots[slotIndex].neutralDeckCount = Mathf.Max(0, _slots[slotIndex].neutralDeckCount - 1);
            }

            if (!_slots[slotIndex].HasCard)
                DrawCardForSlot(slotIndex);

            resultMessage = $"패턴 발동: {SLOT_KEYS[slotIndex]}에 무속성 카드 추가";
            Debug.Log($"[방해] {SLOT_KEYS[slotIndex]} 슬롯 무속성 카드 혼입");
        }

        return resultMessage;
    }

    // 특정 카드 인덱스 강화 +1 (전투 보상)
    public void UpgradeRunDeckCard(int cardIndex)
    {
        if (cardIndex < 0 || cardIndex >= _runDeck.Count) return;
        _runDeck[cardIndex].upgradeLevel++;
        Debug.Log($"[카드 강화] {_runDeck[cardIndex].elementKey} #{cardIndex} → +{_runDeck[cardIndex].upgradeLevel}");
    }

    // 속성 전체 강화 +1 (명상 보상)
    public void UpgradeElementGroup(string elementKey)
    {
        if (!_elementUpgradeLevels.ContainsKey(elementKey)) return;
        _elementUpgradeLevels[elementKey]++;
        Debug.Log($"[속성 강화] {elementKey} Lv.{_elementUpgradeLevels[elementKey]}");
    }

    // 런 덱에 속성 카드 추가 (상점 구매)
    public void AddRunDeckCard(string elementKey)
    {
        _runDeck.Add(new RunDeckCard(elementKey));
        Debug.Log($"[덱 추가] {elementKey} 추가 — 런 덱 총 {_runDeck.Count}장");
    }

    // 런 덱에서 속성 카드 1장 제거 (상점 정제)
    public bool RemoveRunDeckCard(string elementKey)
    {
        int idx = _runDeck.FindIndex(c => c.elementKey == elementKey);
        if (idx < 0) return false;
        _runDeck.RemoveAt(idx);
        Debug.Log($"[덱 제거] {elementKey} 제거 — 런 덱 총 {_runDeck.Count}장");
        return true;
    }

    // 저주를 전역 카운트로 1회 소모
    void ConsumeGlobalCurseUse()
    {
        bool hadCurse = false;

        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] != null && _slots[i].curseTurns > 0)
            {
                hadCurse = true;
                break;
            }
        }

        if (!hadCurse)
            return;

        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] != null && _slots[i].curseTurns > 0)
                _slots[i].curseTurns = Mathf.Max(0, _slots[i].curseTurns - 1);
        }
    }

    // 속성 전체 강화 레벨 반환
    public int       GetElementUpgradeLevel(string key)
        => _elementUpgradeLevels.TryGetValue(key, out int lv) ? lv : 0;

    // 인덱스로 슬롯 상태 반환
    public SlotState GetSlot(int index) => _slots[index];

    // 슬롯 키로 속성 키 반환
    public string GetElementKeyBySlotKey(string slotKey)
    {
        int i = System.Array.IndexOf(SLOT_KEYS, slotKey);
        return i >= 0 ? ELEMENT_KEYS[i] : "";
    }

    // 부가 효과량 = 카드 보너스 + 속성 강화 레벨
    int CalculateEffectAmount(RunDeckCard card, string elementKey)
    {
        return card.EffectBonus + GetElementUpgradeLevel(elementKey);
    }

    // 속성별 부가 효과 적용(fire=화상/water=습기/wind=런처/earth=강화)
    void ApplyElementEffect(string elementKey, int amount)
    {
        switch (elementKey)
        {
            case "fire":
                if (enemyController != null)
                {
                    enemyController.AddStatus("burn", amount);
                    Debug.Log($"[속성 효과] 적에게 화상 {amount} 부여");
                }
                break;

            case "water":
                if (enemyController != null)
                {
                    enemyController.AddStatus("wet", amount);
                    Debug.Log($"[속성 효과] 적에게 습기 {amount} 부여");
                }
                break;

            case "wind":
                if (player != null)
                {
                    player.AddStatus("launcher", amount);
                    Debug.Log($"[속성 효과] 플레이어에게 런처 {amount} 부여");
                }
                break;

            case "earth":
                if (player != null)
                {
                    player.AddStatus("fortify", amount);
                    Debug.Log($"[속성 효과] 플레이어에게 강화 {amount} 부여");
                }
                break;
        }
    }

    // 슬롯 피해 계산(플레이어 공격력×총 레벨×계수)
    int CalculateSlotDamage(RunDeckCard card, string elementKey)
    {
        float playerAttack = player != null ? player.attackDamage : fallbackPlayerAttackDamage;
        int totalLevel = card.BaseDamage + GetElementUpgradeLevel(elementKey);

        float scaledDamage = playerAttack * totalLevel * slotDamagePerAttackPoint;
        return Mathf.Max(1, Mathf.RoundToInt(scaledDamage));
    }

    // 리스트를 무작위로 섞음
    void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // 지정 슬롯에 저주 부여
    public void ApplyCurse(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= 4) return;
        _slots[slotIndex].curseTurns = SLOT_CURSE_TURNS;
        Debug.Log($"[ElementSlotSystem] {SLOT_KEYS[slotIndex]} 슬롯 저주 {SLOT_CURSE_TURNS}회");
    }

    // 지정 슬롯에 무속성 카드 삽입
    public void InsertNullCard(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= 4) return;

        _slots[slotIndex].neutralDeckCount++;

        if (!_slots[slotIndex].hasNeutralCard && _slots[slotIndex].currentCard == null)
        {
            _slots[slotIndex].hasNeutralCard = true;
            _slots[slotIndex].neutralDeckCount = Mathf.Max(0, _slots[slotIndex].neutralDeckCount - 1);
        }

        if (!_slots[slotIndex].HasCard)
            DrawCardForSlot(slotIndex);

        Debug.Log($"[ElementSlotSystem] {SLOT_KEYS[slotIndex]} 슬롯 무속성 카드 삽입");
    }

}
