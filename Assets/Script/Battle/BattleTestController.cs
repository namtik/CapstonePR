using System.Collections.Generic;
using UnityEngine;
using static SkillDataParser;
using Battle;
using Battle.Card;
using Battle.Relic;
using Battle.UI;

/// <summary>
/// 전투 단일 라운드 테스트용 진입점.
/// GameStateController/MapManager 흐름을 우회하고 Inspector 값으로
/// Player/Enemy/Round를 직접 세팅한 뒤 RoundManager를 호출한다.
/// 새 카드슬롯/콤보 시스템 프로토타입을 격리된 환경에서 시험할 때 사용.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class BattleTestController : MonoBehaviour
{
    public enum BattleTestRoundType
    {
        Combat,
        Elite,
        Boss
    }

    [Header("플레이어 설정")]
    [SerializeField] private int playerMaxHp = 100;
    [SerializeField] private int playerStartHp = 100;
    [SerializeField] private float playerAttackDamage = 10f;

    [Header("라운드 설정")]
    [SerializeField] private BattleTestRoundType roundType = BattleTestRoundType.Combat;
    [SerializeField] private DifficultyConfig difficultyConfig;
    [SerializeField] private int columnIndex = 0;

    [Header("적 (Combat/Elite는 enemies 사용, Boss는 bossEnemy 사용)")]
    [SerializeField] private List<EnemyData> enemies = new List<EnemyData>();
    [SerializeField] private EnemyData bossEnemy;

    [Header("시작 보유 스킬 (CSV ID, 비워두면 기본 starter 선택 UI 표시)")]
    [SerializeField] private List<int> starterSkillIds = new List<int>();

    [Header("새 전투 시스템")]
    [Tooltip("ON이면 NewBattleController를 사용 — 기존 ElementSlotSystem/ComboSystem은 비활성.")]
    [SerializeField] private bool useNewBattleSystem = true;
    [SerializeField] private NewBattleController newBattleController;

    [Header("유물 (테스트)")]
    [Tooltip("전투 시작 시 자동 지급할 유물. 이무기의 여의주=AwakenGaugeRecoverPerCombo, 비급서=ComboBonusSecondsBoost")]
    [SerializeField] private List<RelicEffectType> testRelics = new List<RelicEffectType>();

    [Header("시작 덱 (Inspector 편집 가능 / 비우면 PDF 프로토타입 덱 사용)")]
    [Tooltip("카드 ID + 보유 수량 리스트. 비어있으면 CardDatabase.DefaultPrototypeDeckEntries()를 사용한다.")]
    [SerializeField] private List<CardDatabase.DeckEntry> startingDeck = new List<CardDatabase.DeckEntry>();
    [Tooltip("OnValidate에서 startingDeck가 비어있으면 PDF 기본 덱으로 자동 채운다.")]
    [SerializeField] private bool autoFillDefaultDeckInEditor = true;

    [Header("씬 참조")]
    [Tooltip("전투 UI 루트. 자동 활성화. 없으면 씬에서 'CombatStage' 이름으로 자동 탐색.")]
    [SerializeField] private GameObject combatStage;

    [Header("진행 옵션")]
    [SerializeField] private bool autoStartOnPlay = true;
    [Tooltip("씬의 다른 매니저(Player/ComboSystem/HUD 등) 초기화 후 라운드 시작까지 대기 시간(초).")]
    [SerializeField] private float startDelay = 0.1f;
    [Tooltip("Play 시작 시 씬의 GameStateController를 비활성화하여 맵 자동 표시를 막는다.")]
    [SerializeField] private bool disableGameStateController = true;

    private RoundManager cachedRoundManager;

    void Awake()
    {
        if (disableGameStateController)
        {
            var stateCtrl = FindFirstObjectByType<GameStateController>();
            if (stateCtrl != null) stateCtrl.enabled = false;
        }

        // 새 전투 시스템 사용 시 — 다른 Awake 실행 전에 레거시 시스템을 차단
        if (useNewBattleSystem)
            PreDisableLegacyCombatObjects();

        if (combatStage == null)
        {
            GameObject found = GameObject.Find("CombatStage");
            if (found != null) combatStage = found;
        }
    }

    void PreDisableLegacyCombatObjects()
    {
        // 비활성 오브젝트까지 포함해서 찾는다(아직 Awake 전이라도 SetActive(false) 처리)
        var slotSystems = FindObjectsByType<ElementSlotSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var s in slotSystems) if (s != null) s.gameObject.SetActive(false);

        var comboSystems = FindObjectsByType<ComboSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in comboSystems) if (c != null) c.gameObject.SetActive(false);

        var huds = FindObjectsByType<ElementSlotHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var h in huds) if (h != null) h.gameObject.SetActive(false);

        var cardSystems = FindObjectsByType<CardSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var cs in cardSystems) if (cs != null) cs.gameObject.SetActive(false);
    }

    void Start()
    {
        HideNonCombatStages();
        if (combatStage != null) combatStage.SetActive(true);

        if (autoStartOnPlay)
            Invoke(nameof(StartTestBattle), startDelay);
    }

    [ContextMenu("Start Test Battle")]
    public void StartTestBattle()
    {
        ApplyPlayerOverrides();

        if (!useNewBattleSystem)
            ApplyStarterSkills();

        RoundData data = BuildRoundData();
        if (data == null) return;

        RoundManager rm = ResolveRoundManager();
        if (rm == null)
        {
            Debug.LogError("[BattleTest] RoundManager가 씬에 없습니다.");
            return;
        }

        rm.StartRound(data);
        Debug.Log($"[BattleTest] 라운드 시작 — type={roundType}, column={columnIndex}, newSystem={useNewBattleSystem}");

        if (useNewBattleSystem)
            StartCoroutine(StartNewBattleAfterFrame());
    }

    System.Collections.IEnumerator StartNewBattleAfterFrame()
    {
        // RoundManager가 적을 스폰할 시간을 한 프레임 줌
        yield return null;
        yield return null;

        if (newBattleController == null)
            newBattleController = FindFirstObjectByType<NewBattleController>();

        if (newBattleController == null)
        {
            var go = new GameObject("NewBattleController");
            newBattleController = go.AddComponent<NewBattleController>();
            Debug.Log("[BattleTest] NewBattleController가 자동 생성되었습니다.");
        }

        // RelicManager 자동 생성
        if (RelicManager.Instance == null)
        {
            var rmGo = new GameObject("RelicManager");
            rmGo.AddComponent<RelicManager>();
        }

        // 테스트 유물 지급
        if (testRelics != null)
        {
            foreach (var effectType in testRelics)
                RelicManager.Instance?.GiveRelicByEffect(effectType);
        }

        // RelicHUD 자동 생성
        if (RelicHUD.Instance == null)
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                var hudGo = new GameObject("RelicHUD", typeof(RectTransform));
                hudGo.transform.SetParent(canvas.transform, false);
                hudGo.AddComponent<RelicHUD>();
            }
        }

        // 인스펙터 덱 → CardInstance 리스트로 변환 후 주입
        var deck = ResolveStartingDeck();
        newBattleController.SetCustomStartingDeck(deck);
        newBattleController.StartBattle();
    }

    List<CardInstance> ResolveStartingDeck()
    {
        IList<CardDatabase.DeckEntry> entries =
            (startingDeck != null && startingDeck.Count > 0)
            ? (IList<CardDatabase.DeckEntry>)startingDeck
            : CardDatabase.DefaultPrototypeDeckEntries();

        return CardDatabase.InstantiateDeck(entries);
    }

    void OnValidate()
    {
        if (!autoFillDefaultDeckInEditor) return;
        if (startingDeck == null) startingDeck = new List<CardDatabase.DeckEntry>();
        if (startingDeck.Count == 0)
            startingDeck.AddRange(CardDatabase.DefaultPrototypeDeckEntries());
    }

    void ApplyPlayerOverrides()
    {
        Player player = Player.Resolve(true);
        if (player == null)
        {
            GameObject go = new GameObject("PlayerLogic");
            player = go.AddComponent<Player>();
        }

        player.maxHp = playerMaxHp;
        player.currentHp = Mathf.Clamp(playerStartHp, 1, playerMaxHp);
        player.attackDamage = playerAttackDamage;
        player.UpdateUIForExternalSync();
    }

    void ApplyStarterSkills()
    {
        if (starterSkillIds == null || starterSkillIds.Count == 0)
            return;

        if (SkillDataParser.Instance == null)
        {
            Debug.LogWarning("[BattleTest] SkillDataParser.Instance가 없어 starter 스킬 적용 생략.");
            return;
        }

        if (ComboSystem.Instance == null)
        {
            Debug.LogWarning("[BattleTest] ComboSystem.Instance가 없어 starter 스킬 적용 생략.");
            return;
        }

        ComboSystem.Instance.learnedSkills.Clear();

        foreach (int id in starterSkillIds)
        {
            SkillData skill = SkillDataParser.Instance.GetSkill(id);
            if (skill == null)
            {
                Debug.LogWarning($"[BattleTest] 스킬 ID {id}을(를) 찾을 수 없음.");
                continue;
            }
            ComboSystem.Instance.LearnSkill(skill);
        }
    }

    RoundData BuildRoundData()
    {
        switch (roundType)
        {
            case BattleTestRoundType.Combat:
            {
                if (enemies == null || enemies.Count == 0)
                {
                    Debug.LogError("[BattleTest] Combat: enemies 리스트가 비어있습니다.");
                    return null;
                }
                var data = ScriptableObject.CreateInstance<CombatRoundData>();
                data.roundName = "TestCombat";
                data.roundType = NodeType.Combat;
                data.enemies = new List<EnemyData>(enemies);
                data.columnIndex = columnIndex;
                return data;
            }
            case BattleTestRoundType.Elite:
            {
                if (enemies == null || enemies.Count == 0)
                {
                    Debug.LogError("[BattleTest] Elite: enemies 리스트가 비어있습니다.");
                    return null;
                }
                var data = ScriptableObject.CreateInstance<EliteRoundData>();
                data.roundName = "TestElite";
                data.roundType = NodeType.Elite;
                data.enemies = new List<EnemyData>(enemies);
                data.columnIndex = columnIndex;
                return data;
            }
            case BattleTestRoundType.Boss:
            {
                if (bossEnemy == null)
                {
                    Debug.LogError("[BattleTest] Boss: bossEnemy가 설정되지 않았습니다.");
                    return null;
                }
                var data = ScriptableObject.CreateInstance<BossRoundData>();
                data.roundName = "TestBoss";
                data.roundType = NodeType.Boss;
                data.bossEnemy = bossEnemy;
                data.columnIndex = columnIndex;
                return data;
            }
        }
        return null;
    }

    RoundManager ResolveRoundManager()
    {
        if (cachedRoundManager != null) return cachedRoundManager;
        cachedRoundManager = FindFirstObjectByType<RoundManager>();
        return cachedRoundManager;
    }

    void HideNonCombatStages()
    {
        var stateCtrl = FindFirstObjectByType<GameStateController>();
        if (stateCtrl == null) return;

        if (stateCtrl.mapStage != null) stateCtrl.mapStage.SetActive(false);
        if (stateCtrl.shopStage != null) stateCtrl.shopStage.SetActive(false);
        if (stateCtrl.restStage != null) stateCtrl.restStage.SetActive(false);
        if (stateCtrl.eventStage != null) stateCtrl.eventStage.SetActive(false);
        if (stateCtrl.eliteStage != null) stateCtrl.eliteStage.SetActive(false);
        if (stateCtrl.bossStage != null) stateCtrl.bossStage.SetActive(false);
    }
}
