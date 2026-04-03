using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 속성 슬롯 시스템 — 프로토타입 v29 이식
/// Q=불(fire) / W=물(water) / E=바람(wind) / R=땅(earth) 고정 슬롯
/// 각 속성별 독립 덱을 관리하며, 전투 간 런 덱이 유지됨
///
/// [교체 대상] CardSystem.cs (단일 덱 → 손패 방식)
/// </summary>
public class ElementSlotSystem : MonoBehaviour
{
    // ── 상수 ──────────────────────────────────────────────────────────
    public static readonly string[] SLOT_KEYS    = { "Q", "W", "E", "R" };
    public static readonly string[] ELEMENT_KEYS = { "fire", "water", "wind", "earth" };
    public const string NEUTRAL_CARD = "neutral";

    public const int CARDS_PER_ELEMENT = 3; // 런 덱 기본 속성당 카드 수
    public const int SLOT_CURSE_TURNS  = 2;
    public const int CURSED_SLOT_DAMAGE = 6;

    // ── 런 덱 카드 (전투 간 유지) ────────────────────────────────────
    [System.Serializable]
    public class RunDeckCard
    {
        public string elementKey;  // "fire" / "water" / "wind" / "earth"
        public int    upgradeLevel; // 카드 강화 횟수

        public RunDeckCard(string key, int upgrade = 0)
        {
            elementKey   = key;
            upgradeLevel = upgrade;
        }

        /// 카드 기본 피해 = 1 + 강화레벨
        public int BaseDamage => 1 + upgradeLevel;
        /// 부가효과 보너스 = 강화레벨과 동일
        public int EffectBonus => upgradeLevel;
    }

    // ── 슬롯 상태 ─────────────────────────────────────────────────────
    public class SlotState
    {
        public string       elementKey;   // 이 슬롯에 고정된 속성
        public string       slotKey;      // "Q" / "W" / "E" / "R"
        public RunDeckCard  currentCard;  // 현재 장전된 카드 (null = 빈 슬롯)
        public bool         hasNeutralCard; // 무속성 카드 혼입 여부
        public int          curseTurns;   // 저주 남은 횟수 (0 = 정상)

        public List<RunDeckCard> deck  = new List<RunDeckCard>();
        public List<RunDeckCard> grave = new List<RunDeckCard>();

        public bool IsEmpty   => currentCard == null && !hasNeutralCard && deck.Count == 0;
        public bool IsCursed  => curseTurns > 0;
        public bool HasCard   => currentCard != null || hasNeutralCard;
        public int  RemainingCount => (currentCard != null ? 1 : 0) + (hasNeutralCard ? 1 : 0) + deck.Count;
    }

    // ── 싱글턴 ───────────────────────────────────────────────────────
    public static ElementSlotSystem Instance;

    // ── 런 덱 (Inspector에서도 확인 가능) ────────────────────────────
    [Header("런 덱 상태 (디버그)")]
    [SerializeField] private int runDeckCount;

    public List<RunDeckCard> runDeck = new List<RunDeckCard>();

    // ── 속성별 전체 강화 레벨 ─────────────────────────────────────────
    public Dictionary<string, int> elementUpgradeLevels = new Dictionary<string, int>
    {
        { "fire", 0 }, { "water", 0 }, { "wind", 0 }, { "earth", 0 }
    };

    // ── 슬롯 배열 ─────────────────────────────────────────────────────
    public SlotState[] slots = new SlotState[4];

    [Header("슬롯 피해 계산")]
    [SerializeField] private float fallbackPlayerAttackDamage = 10f;
    [SerializeField] private float slotDamagePerAttackPoint = 0.1f;

    // ── 내부 참조 ─────────────────────────────────────────────────────
    private ComboSystem     comboSystem;
    private Player          player;
    private EnemyController enemyController;
    private bool            inBattle;
    public bool InBattle => inBattle;

    // ─────────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────────

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

    void Start()
    {
        comboSystem = FindFirstObjectByType<ComboSystem>();
        player      = FindFirstObjectByType<Player>();
        RefreshEnemyRef();
    }

    void Update()
    {
        runDeckCount = runDeck.Count; // 인스펙터 디버그용

        if (!inBattle) return;
        RefreshEnemyRef();
        HandleInput();
    }

    void RefreshEnemyRef()
    {
        if (enemyController == null || !enemyController.gameObject.activeInHierarchy)
            enemyController = FindFirstObjectByType<EnemyController>();

        if (player == null || !player.gameObject.activeInHierarchy)
            player = FindFirstObjectByType<Player>();
    }

    // ─────────────────────────────────────────────────────────────────
    // 초기화
    // ─────────────────────────────────────────────────────────────────

    /// <summary>런 처음 시작 시 기본 런 덱 생성</summary>
    public void InitRunDeck()
    {
        runDeck.Clear();
        foreach (string elem in ELEMENT_KEYS)
            for (int i = 0; i < CARDS_PER_ELEMENT; i++)
                runDeck.Add(new RunDeckCard(elem));

        Debug.Log($"[ElementSlotSystem] 런 덱 초기화: {runDeck.Count}장");
    }

    /// <summary>전투 시작 시 호출 — 런 덱에서 속성별 덱 빌드 후 첫 카드 드로우</summary>
    public void StartBattle()
    {
        inBattle = true;

        player      = FindFirstObjectByType<Player>();
        comboSystem = FindFirstObjectByType<ComboSystem>();
        RefreshEnemyRef();

        // 4개 슬롯 초기화
        for (int i = 0; i < 4; i++)
        {
            slots[i] = new SlotState
            {
                slotKey       = SLOT_KEYS[i],
                elementKey    = ELEMENT_KEYS[i],
                curseTurns    = 0,
                hasNeutralCard = false
            };
        }

        BuildElementDecksFromRunDeck();
        SyncEmptySlots();

        Debug.Log("[ElementSlotSystem] 전투 시작 — 슬롯 준비 완료");
    }

    /// <summary>전투 종료 시 호출</summary>
    public void EndBattle()
    {
        inBattle = false;
        Debug.Log("[ElementSlotSystem] 전투 종료");
    }

    // ─────────────────────────────────────────────────────────────────
    // 덱 관리
    // ─────────────────────────────────────────────────────────────────

    void BuildElementDecksFromRunDeck()
    {
        foreach (var slot in slots)
        {
            slot.deck.Clear();
            slot.grave.Clear();
            slot.currentCard   = null;
            slot.hasNeutralCard = false;
        }

        // 런 덱을 셔플 후 속성별로 분류
        List<RunDeckCard> shuffled = new List<RunDeckCard>(runDeck);
        ShuffleList(shuffled);

        foreach (var card in shuffled)
        {
            int slotIndex = System.Array.IndexOf(ELEMENT_KEYS, card.elementKey);
            if (slotIndex >= 0)
                slots[slotIndex].deck.Add(card);
        }

        Debug.Log("[ElementSlotSystem] 속성별 덱 빌드 완료");
    }

    /// <summary>카드 없는 슬롯에 덱에서 드로우</summary>
    void SyncEmptySlots()
    {
        for (int i = 0; i < 4; i++)
        {
            if (!slots[i].HasCard && slots[i].deck.Count > 0)
                DrawCardForSlot(i);
        }

        if (AreAllElementResourcesSpent())
            RebuildSpentElementDecks();
    }

    void DrawCardForSlot(int index)
    {
        var slot = slots[index];
        if (slot.currentCard != null) return;
        if (slot.deck.Count == 0) return;

        slot.currentCard = slot.deck[0];
        slot.deck.RemoveAt(0);
    }

    bool AreAllElementResourcesSpent()
    {
        foreach (var slot in slots)
            if (slot.currentCard != null || slot.hasNeutralCard || slot.deck.Count > 0) return false;
        return true;
    }

    void RebuildSpentElementDecks()
    {
        bool anyGrave = false;
        foreach (var slot in slots)
            if (slot.grave.Count > 0) { anyGrave = true; break; }

        if (!anyGrave) return;

        foreach (var slot in slots)
        {
            slot.deck.AddRange(slot.grave);
            slot.grave.Clear();
            ShuffleList(slot.deck);
        }

        SyncEmptySlots();
        Debug.Log("[ElementSlotSystem] 모든 슬롯 소진 → 덱 재구성");
    }

    // ─────────────────────────────────────────────────────────────────
    // 입력 처리
    // ─────────────────────────────────────────────────────────────────

    void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Q)) UseSlot(0);
        else if (Input.GetKeyDown(KeyCode.W)) UseSlot(1);
        else if (Input.GetKeyDown(KeyCode.E)) UseSlot(2);
        else if (Input.GetKeyDown(KeyCode.R)) UseSlot(3);
    }

    /// <summary>슬롯 사용 — 카드 소비 → 피해 → 콤보 전달</summary>
    public void UseSlot(int index)
    {
        if (!inBattle) return;

        var slot = slots[index];
        if (!slot.HasCard) return;


        bool isNeutral        = slot.hasNeutralCard;
        RunDeckCard card      = slot.currentCard;
        bool triggeredCurse   = !isNeutral && slot.IsCursed;
        bool consumedRealCard = false;

        // ── 카드 소비 ──────────────────────────────────────────────
        if (isNeutral)
        {
            slot.hasNeutralCard = false;
            Debug.Log($"[{SLOT_KEYS[index]}] 무속성 카드 사용 — 콤보 미적용");
        }
        else
        {
            slot.grave.Add(card);
            slot.currentCard = null;
            consumedRealCard = true;

            // 저주 턴 감소
            if (slot.curseTurns > 0)
                slot.curseTurns--;
        }

        // ── 콤보 히스토리 업데이트 (무속성 제외) ──────────────────
        if (!isNeutral)
            comboSystem?.OnCardUsed(SLOT_KEYS[index]);

        // ── 속성 피해 적용 (무속성 제외) ──────────────────────────
        if (!isNeutral && enemyController != null)
        {
            int dmg = CalculateSlotDamage(card, slot.elementKey);
            enemyController.TakeDamage(dmg, SLOT_KEYS[index]);
            Debug.Log($"[{SLOT_KEYS[index]}] {slot.elementKey} 피해 {dmg}");
        }

        // ── 저주 반동 ──────────────────────────────────────────────
        if (triggeredCurse)
        {
            player?.TakeDamage(CURSED_SLOT_DAMAGE);
            Debug.Log($"[{SLOT_KEYS[index]}] 저주 반동 — 플레이어 {CURSED_SLOT_DAMAGE} 피해");
        }

        // ── 다음 카드 드로우 ───────────────────────────────────────
        if (consumedRealCard)
            DrawCardForSlot(index);

        SyncEmptySlots();

        // ?? ?꾪닾 寃쎌슦 ?쒖뒱 ??? ?쇳빐 ?잛닔 ?뚯뿉 ?뱀젙)
        enemyController?.OnPlayerAction();
    }

    // ─────────────────────────────────────────────────────────────────
    // 적 방해 패턴 (EnemyStat 게이지 50% 도달 시 호출)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>랜덤 슬롯에 저주 or 무속성 카드 혼입</summary>
    public string TriggerDisruptionPattern()
    {
        int  slotIndex = Random.Range(0, 4);
        bool isCurse   = Random.value < 0.5f;

        string resultMessage;

        if (isCurse)
        {
            slots[slotIndex].curseTurns = SLOT_CURSE_TURNS;
            resultMessage = $"패턴 발동: {SLOT_KEYS[slotIndex]} 카드 저주받음 {SLOT_CURSE_TURNS}턴";
            Debug.Log($"[방해] {SLOT_KEYS[slotIndex]} 슬롯 저주 {SLOT_CURSE_TURNS}턴");
        }
        else
        {
            slots[slotIndex].hasNeutralCard = true;

            // Neutral is an extra temporary card, not a replacement for the slot's real card.
            // If the slot currently has no loaded real card, immediately load one underneath.
            if (slots[slotIndex].currentCard == null)
                DrawCardForSlot(slotIndex);

            resultMessage = $"패턴 발동: {SLOT_KEYS[slotIndex]}에 무속성 카드 추가";
            Debug.Log($"[방해] {SLOT_KEYS[slotIndex]} 슬롯 무속성 카드 혼입");
        }

        return resultMessage;
    }

    // ─────────────────────────────────────────────────────────────────
    // 런 덱 편집 (보상 / 상점 / 명상)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>특정 카드 인덱스 강화 +1 (전투 보상)</summary>
    public void UpgradeRunDeckCard(int cardIndex)
    {
        if (cardIndex < 0 || cardIndex >= runDeck.Count) return;
        runDeck[cardIndex].upgradeLevel++;
        Debug.Log($"[카드 강화] {runDeck[cardIndex].elementKey} #{cardIndex} → +{runDeck[cardIndex].upgradeLevel}");
    }

    /// <summary>속성 전체 강화 +1 (명상 보상)</summary>
    public void UpgradeElementGroup(string elementKey)
    {
        if (!elementUpgradeLevels.ContainsKey(elementKey)) return;
        elementUpgradeLevels[elementKey]++;
        Debug.Log($"[속성 강화] {elementKey} Lv.{elementUpgradeLevels[elementKey]}");
    }

    /// <summary>런 덱에 속성 카드 추가 (상점 구매)</summary>
    public void AddRunDeckCard(string elementKey)
    {
        runDeck.Add(new RunDeckCard(elementKey));
        Debug.Log($"[덱 추가] {elementKey} 추가 — 런 덱 총 {runDeck.Count}장");
    }

    /// <summary>런 덱에서 속성 카드 1장 제거 (상점 정제)</summary>
    public bool RemoveRunDeckCard(string elementKey)
    {
        int idx = runDeck.FindIndex(c => c.elementKey == elementKey);
        if (idx < 0) return false;
        runDeck.RemoveAt(idx);
        Debug.Log($"[덱 제거] {elementKey} 제거 — 런 덱 총 {runDeck.Count}장");
        return true;
    }

    // ─────────────────────────────────────────────────────────────────
    // 유틸
    // ─────────────────────────────────────────────────────────────────

    public int       GetElementUpgradeLevel(string key)
        => elementUpgradeLevels.TryGetValue(key, out int lv) ? lv : 0;

    public SlotState GetSlot(int index) => slots[index];

    public string GetElementKeyBySlotKey(string slotKey)
    {
        int i = System.Array.IndexOf(SLOT_KEYS, slotKey);
        return i >= 0 ? ELEMENT_KEYS[i] : "";
    }

    int CalculateSlotDamage(RunDeckCard card, string elementKey)
    {
        float playerAttack = player != null ? player.attackDamage : fallbackPlayerAttackDamage;
        int totalLevel = card.BaseDamage + GetElementUpgradeLevel(elementKey);

        // This keeps slot damage tied to player stats while preserving legacy balance when attackDamage=10.
        float scaledDamage = playerAttack * totalLevel * slotDamagePerAttackPoint;
        return Mathf.Max(1, Mathf.RoundToInt(scaledDamage));
    }

    void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
