using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using Battle.Relic;
using TMPro;

public class RoundManager : MonoBehaviour
{
    [SerializeField] private DifficultyConfig difficultyConfig; // 난이도 스케일 설정

    public DifficultyConfig DifficultyConfig => difficultyConfig;

    // 맵 컬럼을 현재 바퀴 기준 누적 난이도 컬럼으로 변환한다.
    public int ResolveDifficultyColumn(int mapColumn)
    {
        if (difficultyConfig == null)
            return mapColumn;

        int completedLaps = GameStateController.Instance != null
            ? GameStateController.Instance.bossDefeatCount
            : 0;

        return difficultyConfig.GetEffectiveColumn(mapColumn, completedLaps);
    }

    [SerializeField] private GameObject enemyPrefab; // 적 프리팹
    [SerializeField] private Transform enemySpawnPoint; // 적 스폰 위치
    [SerializeField] private CombatStageController combatStageController; // 전투 스테이지 배경 제어

    [Header("일반 전투 등장")]
    [Tooltip("일반 전투는 스테이지 프리셋, 정예 전투는 라운드 프리셋을 쓴다. 보스는 사용하지 않는다.")]
    [SerializeField] private EnemyEncounterConfig encounterConfig;
    [Header("디버그 — 보스 3마리 (임시)")]
    [Tooltip("ON이면 보스방에 같은 보스를 3마리 스폰한다. 3D 여러 마리 배치 확인용.")]
    [SerializeField] private bool debugSpawnThreeBosses = true;
    [Tooltip("2D 폴백에서 여러 마리를 가로로 벌릴 간격(px).")]
    [SerializeField] private float twoDMultiSpawnSpacing = 220f;

    [Header("상태이상 UI 패널")]
    [SerializeField] private StatusPanelUI playerStatusPanel; // 플레이어 상태이상 패널
    [SerializeField] private StatusPanelUI enemyStatusPanel; // 적 상태이상 패널

    [Header("적 HP/행동게이지 위치")]
    [Tooltip("머리 기준에서 좌우로 밀 거리. 화면 픽셀. 양수면 오른쪽, 음수면 왼쪽. 플레이 중에도 바로 반영된다.")]
    [SerializeField] private float enemyBarOffsetX = 0f;
    [Tooltip("머리 기준에서 위아래로 밀 거리. 화면 픽셀. 양수면 위, 음수면 아래. 플레이 중에도 바로 반영된다.")]
    [SerializeField] private float enemyBarOffsetY = -60f;

    public float EnemyBarOffsetX => enemyBarOffsetX;
    public float EnemyBarOffsetY => enemyBarOffsetY;

    [Header("적 다음 공격")]
    [InspectorName("아이콘")]
    [SerializeField] private Sprite enemyAttackIcon;
    [InspectorName("아이콘 크기")]
    [SerializeField] private float enemyAttackIconSize = 40f;
    [InspectorName("아이콘 위치")]
    [Tooltip("적 HP바 왼쪽 중앙 기준. X 음수 = 더 왼쪽, Y 양수 = 위. 플레이 중에도 바로 반영된다.")]
    [SerializeField] private Vector2 enemyAttackIconPosition = new Vector2(-9.76f, -6.03f);
    [InspectorName("폰트")]
    [SerializeField] private TMP_FontAsset enemyAttackFont;
    [InspectorName("숫자 크기")]
    [SerializeField] private float enemyAttackNumberSize = 38f;
    [InspectorName("색깔")]
    [SerializeField] private Color enemyAttackNumberColor = Color.red;
    [InspectorName("아웃라인 두께")]
    [Tooltip("0이면 외곽선 없음.")]
    [Range(0f, 1f)]
    [SerializeField] private float enemyAttackOutlineWidth = 0.3f;
    [InspectorName("아웃라인 색")]
    [SerializeField] private Color enemyAttackOutlineColor = Color.black;
    [InspectorName("숫자 위치")]
    [Tooltip("아이콘 기준. X 음수 = 아이콘 왼쪽, Y 양수 = 위. 플레이 중에도 바로 반영된다.")]
    [SerializeField] private Vector2 enemyAttackNumberPosition = new Vector2(56.9f, -11.72f);

    public Sprite EnemyAttackIcon => enemyAttackIcon;
    public float EnemyAttackIconSize => enemyAttackIconSize;
    public Vector2 EnemyAttackIconPosition => enemyAttackIconPosition;
    public TMP_FontAsset EnemyAttackFont => enemyAttackFont;
    public float EnemyAttackNumberSize => enemyAttackNumberSize;
    public Color EnemyAttackNumberColor => enemyAttackNumberColor;
    public float EnemyAttackOutlineWidth => enemyAttackOutlineWidth;
    public Color EnemyAttackOutlineColor => enemyAttackOutlineColor;
    public Vector2 EnemyAttackNumberPosition => enemyAttackNumberPosition;

    [Header("적 강화")]
    [InspectorName("아이콘")]
    [SerializeField] private Sprite enemyPowerUpIcon;
    public Sprite EnemyPowerUpIcon => enemyPowerUpIcon;


    [Header("보상 상단 문구 - 카드")]
    [Tooltip("부적 보상창 프리팹(배경·카드 슬롯·스킵 버튼). 비우면 런타임 생성 UI 사용.")]
    [SerializeField] private Battle.UI.CardRewardPanelView cardRewardPanelPrefab; // 부적 보상창 프리팹
    [SerializeField] private Font cardRewardTitleFont; // 카드 보상 제목 폰트
    [SerializeField, Range(12, 96)] private int cardRewardTitleFontSize = 40; // 카드 보상 제목 폰트 크기
    [SerializeField] private Color cardRewardTitleColor = Color.white; // 카드 보상 제목 색상
    [Tooltip("상단 문구 위치(화면 중앙 기준). 기본 (0, 320).")]
    [SerializeField] private Vector2 cardRewardTitlePosition = new Vector2(0f, 320f); // 카드 보상 제목 위치

    [Header("보상 - 카드 선택하지 않기 버튼")]
    [SerializeField] private Battle.UI.RewardSkipButtonStyle cardRewardSkipButtonStyle = new Battle.UI.RewardSkipButtonStyle(); // 카드 보상 건너뛰기 버튼 스타일

    [Header("보상 - 카드 크기/배치")]
    [Tooltip("제시 카드 배율(1 = 기본). 키우면 간격도 함께 올려 겹침 방지.")]
    [SerializeField, Range(0.3f, 2f)] private float cardRewardCardScale = 1f; // 카드 보상 카드 배율
    [Tooltip("카드 사이 가로 간격(px). 기본 360.")]
    [SerializeField] private float cardRewardCardSpacing = 360f; // 카드 보상 카드 간격(px)

    [Header("런 시작 콤보 선택")]
    [Tooltip("게임 시작 후 맵 진입 직후 정예/보스와 동일한 콤보 3택1을 표시한다.")]
    [SerializeField] private bool showStarterComboPickOnMapEntry = true;

    [Header("보상 후 이동 연출")]
    [Tooltip("ON이면 전투 보상을 마친 뒤 카드 HUD를 숨기고 다음 장소로 걸어가는 연출(BattleWalkTransition) 후 맵으로 복귀한다. 세부 수치를 조절하려면 씬 루트(전투 스테이지 밖)에 BattleWalkTransition을 배치. 없으면 자동 생성(기본값).")]
    [SerializeField] private bool walkTransitionAfterReward = true;

    private RoundData currentRoundData; // 현재 진행 중인 라운드 데이터
    private int currentEnemyIndex = 0; // 현재 처리 중인 적 인덱스(레거시 순차 스폰용)
    private EnemyStat currentEnemy; // 마지막으로 스폰한 적 스탯
    private readonly List<EnemyData> _injectedEnemies = new List<EnemyData>(); // 특이사항(복제) 등으로 끼어든 적 큐
    private bool _currentIsInjected = false; // 현재 적이 주입(복제)된 적인지 여부
    private int _spawnedCombatCount = 1; // 이번 전투에 스폰한 적 수(골드 계산용)
    private int _killGoldPaid; // 처치 연출로 이미 지급한 골드(전투 종료 시 중복 지급 방지)
    private bool _waitingForRoundEnd = false; // 전멸 후 라운드 종료 예약 여부
    public event System.Action OnRoundClear; // 라운드 클리어 이벤트
    private IRoundHandler currentRoundHandler; // 현재 라운드 핸들러


    // 런타임 플레이어가 없으면 생성하고 상태 패널을 연결한다.
    void EnsureRuntimePlayerExists()
    {
        Player existingPlayer = Player.Resolve(true);
        if (existingPlayer != null)
        {
            if (playerStatusPanel != null)
            {
                playerStatusPanel.SetTarget(existingPlayer);
                playerStatusPanel.DockUnderAwakenGauge();
            }
            return;
        }

        Player newPlayer = Player.GetOrCreateRuntime();


        if (playerStatusPanel != null)
        {
            playerStatusPanel.SetTarget(newPlayer);
            playerStatusPanel.DockUnderAwakenGauge();
        }

    }

    // 플레이어 UI 동기화를 보장한다.
    public void EnsurePlayerUiSync()
    {
        EnsureRuntimePlayerExists();
        Player.SyncAllInstancesFromCanonical();
    }

    // 새 전투 시스템(NewBattleController)이 활성 상태인지 반환한다.
    bool IsNewBattleSystemActive()
    {
        return Battle.NewBattleController.Instance != null;
    }

    // 라운드를 시작한다.
    public void StartRound(RoundData roundData)
    {
        EnsureRuntimePlayerExists();
        ContinueStartRound(roundData);
    }

    // ── 시연용: 현재 전투 스테이지 다시 시작(F3) ──
    [Header("시연용 단축키")]
    [Tooltip("ON이면 전투 중 지정 키로 현재 스테이지(같은 적·같은 스테이지)를 처음부터 다시 시작한다. 정식 빌드에서도 동작.")]
    [SerializeField] private bool enableRestartStageHotkey = true;
    [SerializeField] private KeyCode restartStageKey = KeyCode.F3;

    private int _roundStartHp = -1;        // 라운드 시작 시 플레이어 HP
    private int _roundStartMoney = -1;     // 라운드 시작 시 골드
    private int _roundStartKillCount = -1; // 라운드 시작 시 런 처치 수

    private List<EnemyData> _lastRoster;    // 이번 라운드에 나온 적 명단
    private List<EnemyData> _restartRoster; // 다시 시작 시 그대로 쓸 명단(일반 전투 명단은 매번 새로 뽑히므로 고정)

    void Update()
    {
        if (enableRestartStageHotkey && Input.GetKeyDown(restartStageKey))
            RestartCurrentStage();
    }

    // 다시 시작 중이면 직전 명단을 그대로 쓰고, 아니면 이번 명단을 기억한다
    List<EnemyData> PinRoster(List<EnemyData> resolved)
    {
        if (_restartRoster != null)
        {
            List<EnemyData> pinned = _restartRoster;
            _restartRoster = null;
            _lastRoster = new List<EnemyData>(pinned);
            return pinned;
        }
        _lastRoster = resolved != null ? new List<EnemyData>(resolved) : null;
        return resolved;
    }

    // 진행 중인 전투를 버리고, 라운드 시작 시점의 HP·골드·처치 기록으로 되돌린 뒤 같은 라운드를 다시 연다.
    public bool RestartCurrentStage()
    {
        bool isCombatRound = currentRoundData is CombatRoundData || currentRoundData is EliteRoundData
                             || currentRoundData is BossRoundData;
        var nb = Battle.NewBattleController.Instance;
        if (!isCombatRound || !IsNewBattleSystemActive() || nb == null || !nb.InBattle)
        {
            Debug.Log("[RoundManager] 다시 시작 무시 — 진행 중인 전투가 없습니다.");
            return false;
        }
        if (GameOverController.Instance != null && GameOverController.Instance.IsDead)
        {
            Debug.Log("[RoundManager] 다시 시작 무시 — 게임오버 화면입니다.");
            return false;
        }

        if (_restartRoutine != null) return false; // 이미 다시 시작 중

        RoundData data = currentRoundData;
        bool debugRound = DebugRoundActive; // AbortActiveCombat이 끄므로 보관(테스트 라운드는 테스트 라운드로 재시작)
        List<EnemyData> roster = _lastRoster != null && _lastRoster.Count > 0 ? new List<EnemyData>(_lastRoster) : null;

        Time.timeScale = 1f;
        GameStateController.ClearLingeringFx();
        AbortActiveCombat(); // 내부에서 StopAllCoroutines — 재시작 코루틴은 이 뒤에 시작
        _injectedEnemies.Clear();
        _restartRoutine = StartCoroutine(RestartAfterTeardown(data, debugRound, roster));
        return true;
    }

    private Coroutine _restartRoutine;

    // 옛 적·HUD 복제본의 Destroy는 프레임 끝에 처리되고, 그때 OnDestroy가 파티/HUD/전투 상태를 건드린다.
    // 같은 프레임에 새 전투를 열면 새 적·덱이 그 정리에 휘말렸다(적 일부 누락, 패/더미 비어 있음, HP바 사라짐).
    // 정리가 끝난 뒤(2프레임) 맵에서 들어올 때와 같은 순서로 새로 시작한다.
    IEnumerator RestartAfterTeardown(RoundData data, bool debugRound, List<EnemyData> roster)
    {
        yield return null;
        yield return null;
        _restartRoutine = null;
        DebugRoundActive = debugRound;

        Player player = Player.Resolve(true);
        if (player != null && _roundStartHp > 0)
        {
            player.currentHp = Mathf.Clamp(_roundStartHp, 1, player.maxHp);
            Player.SyncAllInstancesFrom(player);
        }
        if (MoneyManager.Instance != null && _roundStartMoney >= 0)
            MoneyManager.Instance.SetMoney(_roundStartMoney);
        if (GameStateController.Instance != null && _roundStartKillCount >= 0)
            GameStateController.Instance.TrimRunKills(_roundStartKillCount);

        Debug.Log($"[RoundManager] 현재 스테이지 다시 시작 — 적 {(roster != null ? roster.Count : 0)}마리");
        _restartRoster = roster;
        StartRound(data);

        // 진단용: 시작 직후 상태
        yield return null;
        var deck = Battle.NewBattleController.Instance != null ? Battle.NewBattleController.Instance.Deck : null;
        Debug.Log($"[RoundManager] 다시 시작 후 — 살아 있는 적 {EnemyCombatParty.AliveCount}, " +
                  (deck != null ? $"패 {deck.HandCount}, 뽑을 {deck.DrawCount}, 버린 {deck.DiscardCount}" : "덱 없음"));
    }

    // 라운드 데이터를 설정하고 핸들러를 만들어 라운드를 진행한다.
    void ContinueStartRound(RoundData roundData)
    {
        // 다시 시작(F3)용 시작 상태 저장
        Player startPlayer = Player.Resolve(true);
        _roundStartHp = startPlayer != null ? startPlayer.currentHp : -1;
        _roundStartMoney = MoneyManager.Instance != null ? MoneyManager.Instance.GetMoney() : -1;
        _roundStartKillCount = GameStateController.Instance != null ? GameStateController.Instance.RunKillCount : -1;

        currentRoundData = roundData;
        currentEnemyIndex = 0;

        if (combatStageController != null)
        {
            combatStageController.Initialize(roundData);
        }

        if (roundData is BossRoundData)
            GameAudio.Instance?.SetCombatTrack(NodeType.Boss);
        else if (roundData is EliteRoundData)
            GameAudio.Instance?.SetCombatTrack(NodeType.Elite);
        else if (roundData is CombatRoundData)
            GameAudio.Instance?.SetCombatTrack(NodeType.Combat);

        currentRoundHandler = roundData.CreateHandler();
        currentRoundHandler.OnEnterRound(this);
    }

    // 라운드를 종료하고 종료 핸들러와 클리어 이벤트를 호출한다.
    public void EndRound()
    {
        Battle.NewBattleController.Instance?.EndBattle();

        currentRoundHandler.OnExitRound(this);
        EnemyCombatParty.EndBattle();
        OnRoundClear?.Invoke();
    }

    // 신규 전투 시스템: 런 덱을 주입하고 전투를 시작한다.
    void BeginNewBattleForNode()
    {
        var nb = Battle.NewBattleController.Instance;
        if (nb == null) return;

        var runDeck = Battle.RunDeckState.EnsureExists();
        runDeck.EnsureSeeded();
        nb.SetCustomStartingDeck(runDeck.InstantiateForBattle());
        nb.StartBattle();
    }

    // 일반 전투를 시작한다.
    public void StartCombat(CombatRoundData data)
    {
        List<EnemyData> roster = PinRoster(ResolveCombatRoster(data));
        ShowStageThen(roster, data.columnIndex, () => BeginCombat(data, roster));
    }

    void BeginCombat(CombatRoundData data, List<EnemyData> roster)
    {
        PrepareBattleStart();
        SpawnParty(roster, data.columnIndex, data.roundType);
        BeginNewBattleForNode();
    }

    // 전투 시작 전 공통 준비: 플레이어 보장 + 상태이상 초기화
    void PrepareBattleStart()
    {
        EnsureRuntimePlayerExists();
        Player player = Player.Resolve(true);
        if (player != null) player.ResetStatusForNewBattle();
    }

    // 정예 전투를 시작한다.
    public void StartCombat(EliteRoundData data)
    {
        // 스테이지 속성 결정용 명단 — 다시 시작 명단은 여기서 소비하지 않고(BeginElite의 PinRoster가 소비) 엿보기만 한다
        List<EnemyData> roster = _restartRoster != null ? _restartRoster : TakeFirstEnemy(data.enemies);
        ShowStageThen(roster, data.columnIndex, () => BeginElite(data, roster));
    }

    void BeginElite(EliteRoundData data, List<EnemyData> roster)
    {
        PrepareBattleStart();
        SpawnParty(PinRoster(ResolveEliteRoster(data)), data.columnIndex, data.roundType);
        BeginNewBattleForNode();
    }

    // 보스 전투를 시작한다.
    public void StartBoss(BossRoundData data)
    {
        var bossOnly = new List<EnemyData> { data.bossEnemy };
        ShowStageThen(bossOnly, data.columnIndex, () => BeginBoss(data), true);
    }

    void BeginBoss(BossRoundData data)
    {
        PrepareBattleStart();

        // [임시] 3D 여러 마리 배치 확인용. 같은 보스를 3마리 스폰한다.
        var bossRoster = new List<EnemyData>();
        if (data.bossEnemy != null)
        {
            bossRoster.Add(data.bossEnemy);
            if (debugSpawnThreeBosses)
            {
                bossRoster.Add(data.bossEnemy);
                bossRoster.Add(data.bossEnemy);
            }
        }
        SpawnParty(bossRoster, data.columnIndex, NodeType.Boss);
        BeginNewBattleForNode();
    }

    // 적 속성에 맞는 3D 스테이지를 표시하고, 준비되면(이미 로드돼 있으면 즉시) 전투를 시작한다.
    // StageManager가 없으면 바로 시작(2D).
    // progressColumn: 누적 진행 컬럼 — 스테이지 시간대(아침→…→새벽 순환)를 정하는 데 쓰인다.
    // isBoss: 보스 전투면 보스 전용 스테이지를 쓴다.
    void ShowStageThen(List<EnemyData> roster, int progressColumn, System.Action begin, bool isBoss = false)
    {
        if (StageManager.Instance == null)
        {
            begin();
            return;
        }
        StageManager.Instance.ShowStage(StageManager.ResolveElement(roster), progressColumn, begin, isBoss, roster);
    }

    // 상점 스테이지를 연다.
    public void OpenShop()
    {
        ShopStageController controller = FindFirstObjectByType<ShopStageController>(FindObjectsInactive.Include);

        if (controller == null)
        {
            var stateController = GameStateController.Instance;
            if (stateController != null && stateController.shopStage != null)
            {
                controller = stateController.shopStage.GetComponentInChildren<ShopStageController>(true);
                if (controller == null)
                    controller = stateController.shopStage.AddComponent<ShopStageController>();
            }
        }

        if (controller == null)
        {
            Debug.LogError("[RoundManager] ShopStageController를 찾지 못해 맵으로 복귀합니다.");
            ReturnToMap();
            return;
        }

        controller.BeginShop(this);
    }

    // 상점을 닫고 맵으로 복귀한다.
    public void CloseShop()
    {
        ReturnToMap();
    }

    // 이벤트 스테이지를 연다.
    public void OpenEvent(EventRoundData data)
    {
        EventStageController controller = FindFirstObjectByType<EventStageController>(FindObjectsInactive.Include);
        if (controller == null)
        {
            Debug.LogError("[RoundManager] EventStageController를 찾지 못해 맵으로 복귀합니다.");
            ReturnToMap();
            return;
        }

        controller.BeginEvent(data, this);
    }

    // 휴식 스테이지를 연다.
    public void OpenRest(RestRoundData data)
    {
        RestStageController controller = FindFirstObjectByType<RestStageController>(FindObjectsInactive.Include);

        if (controller == null)
        {
            var stateController = GameStateController.Instance;
            if (stateController != null && stateController.restStage != null)
            {
                controller = stateController.restStage.GetComponentInChildren<RestStageController>(true);
                if (controller == null)
                    controller = stateController.restStage.AddComponent<RestStageController>();
            }
        }

        if (controller == null)
        {
            Debug.LogError("[RoundManager] RestStageController를 찾지 못해 맵으로 복귀합니다.");
            ReturnToMap();
            return;
        }

        controller.BeginRest(data, this);
    }

    // 유물 스테이지를 연다.
    public void OpenRelic(RelicRoundData data)
    {
        RelicStageController controller = FindFirstObjectByType<RelicStageController>(FindObjectsInactive.Include);

        if (controller == null)
        {
            var stateController = GameStateController.Instance;
            if (stateController != null && stateController.relicStage != null)
            {
                controller = stateController.relicStage.GetComponentInChildren<RelicStageController>(true);
                if (controller == null)
                    controller = stateController.relicStage.AddComponent<RelicStageController>();
            }
        }

        if (controller == null)
        {
            Debug.LogWarning("[RoundManager] RelicStageController를 찾지 못해 유물 스테이지를 진행할 수 없습니다. 맵으로 복귀합니다.");
            ReturnToMap();
            return;
        }

        controller.BeginRelic(data, this);
    }

    // 플레이어 HP를 비율만큼 회복하고 맵으로 복귀한다.
    public void HealPlayer(float healPercent)
    {
        var player = Player.Resolve(true);
        if (player == null) return;

        int healAmount = Mathf.Max(1, Mathf.RoundToInt(player.maxHp * healPercent));
        player.Heal(healAmount);

        Debug.Log($"플레이어 HP 회복: +{healAmount} ({healPercent * 100}%)");

        ReturnToMap();
    }

    // 전투 보상 UI를 표시한다(등급별 골드 지급 후 스킬 또는 카드 보상으로 분기).
    public void ShowSkillReward()
    {
        GrantTieredCombatGold();

        // 2스테이지 보스는 카드·콤보 보상 없이, 전투 스테이지 위에 클리어 화면만 띄운다.
        if (IsFinalClearBoss())
        {
            ShowFinalClearOnBattleStage();
            return;
        }

        ShowCardReward();
    }


    // 신규 시스템 카드 획득 보상을 표시한다(제시 카드 중 1장을 런 덱에 추가).
    void ShowCardReward()
    {
        Debug.Log("[RoundManager] 카드 획득 보상 표시");

        var hud = FindFirstObjectByType<Battle.UI.CardHandHUD>(FindObjectsInactive.Include);
        Battle.UI.NewCardView cardPrefab = hud != null ? hud.CardPrefab : null;

        var rewardUI = Battle.UI.CardRewardUI.EnsureExists();
        rewardUI.SetTitleStyle(cardRewardTitleFont, cardRewardTitleFontSize, cardRewardTitleColor, cardRewardTitlePosition);
        rewardUI.SetPanelPrefab(cardRewardPanelPrefab);
        rewardUI.SetSkipButtonStyle(cardRewardSkipButtonStyle);
        rewardUI.SetCardLayout(cardRewardCardScale, cardRewardCardSpacing);
        rewardUI.Present(cardPrefab, pickedCardId =>
    {
        if (pickedCardId > 0)
            Battle.RunDeckState.EnsureExists().AddCard(pickedCardId);

        if (ShouldShowComboBookRewardAfterCard())
        {
            ShowComboBookRewardAfterCard();
            return;
        }

        ReturnToMapWithWalk();
    });
}

    // 최종 보스 클리어: 맵으로 나가지 않고, 보스를 잡은 전투 스테이지 위에 클리어 패널을 띄운다.
    void ShowFinalClearOnBattleStage()
    {
        GameStateController state = GameStateController.Instance;
        if (state != null)
        {
            state.RegisterBossDefeat(currentRoundData);
            state.MarkNodeCleared(state.lastVisitedNodeIndex);
        }

        if (GameClearController.Instance != null && GameClearController.Instance.IsReady)
            GameClearController.Instance.ShowGameClear();
        else
            Debug.LogError("[RoundManager] GameClearController가 없어 전투 스테이지 위에 클리어 화면을 띄우지 못했습니다.");
    }

    // 보상 종료 후 걷기 연출을 거쳐 맵으로 복귀한다(연출 OFF면 즉시 복귀).
    void ReturnToMapWithWalk() => LeaveRound(walkTransitionAfterReward);

    // 이번 보스 처치로 런이 끝나는지. 보상 UI를 띄우기 전이라 처치 수는 아직 오르지 않았다.
    bool IsFinalClearBoss()
    {
        if (DebugRoundActive) return false;
        if (currentRoundData is not BossRoundData) return false;

        GameStateController state = GameStateController.Instance;
        return state != null && state.bossDefeatCount + 1 >= GameStateController.RequiredBossDefeats;
    }

    // 카드 보상 후 콤보 책 보상을 표시할 라운드인지 판정한다(정예/보스).
    bool ShouldShowComboBookRewardAfterCard()
    {
        return currentRoundData is EliteRoundData || currentRoundData is BossRoundData;
    }

    // 카드 보상 후 콤보 책 보상 UI를 표시하고 선택한 콤보를 지급한다.
    void ShowComboBookRewardAfterCard()
    {
        if (ShouldDropStageBossRelic())
        {
            ShowComboBookReward(() => BossRelicDropUI.Present(ReturnToMapWithWalk));
            return;
        }

        ShowComboBookReward(ReturnToMapWithWalk);
    }

    // 1스테이지 보스만 유물을 떨어뜨린다. 2스테이지 보스는 클리어 화면으로 끝난다.
    bool ShouldDropStageBossRelic()
    {
        if (currentRoundData is not BossRoundData)
            return false;
        if (IsFinalClearBoss())
            return false;

        GameStateController state = GameStateController.Instance;
        return state == null || state.bossDefeatCount == 0;
    }

    // 런 시작 직후 맵에서 콤보 3택1을 표시한다.
    public void ShowStarterComboBookReward()
    {
        if (!showStarterComboPickOnMapEntry)
            return;

        ShowComboBookReward(null);
        Debug.Log("[RoundManager] 런 시작 콤보 3택1 표시");
    }

    // 콤보 책 보상 UI를 표시하고 선택한 콤보를 지급한다(onComplete가 null이면 맵 복귀 없음).
    public void ShowComboBookReward(System.Action onComplete)
    {
        var panel = FindFirstObjectByType<Battle.UI.ComboBookRewardPanelUI>(FindObjectsInactive.Include);
        if (panel == null)
        {
            Debug.LogWarning("[RoundManager] ComboBookRewardPanelUI를 찾지 못해 콤보 보상을 생략합니다.");
            onComplete?.Invoke();
            return;
        }

        panel.Present(selected =>
        {
            if (selected != null)
            {
                var nb = Battle.NewBattleController.Instance;
                if (nb != null)
                {
                    nb.AddOwnedCombo(selected.refComboId);
                    Debug.Log($"[RoundManager] 콤보 보상 획득: refComboId={selected.refComboId}, name={selected.displayName}");
                }
                else
                {
                    Debug.LogWarning("[RoundManager] NewBattleController.Instance가 없어 콤보 보상 지급을 건너뜁니다.");
                }
            }

            onComplete?.Invoke();
        });

        Debug.Log("[RoundManager] 책 UI 콤보 보상 표시");
    }

    // 전투 등급별 골드를 일괄 지급한다(비전투 라운드는 지급하지 않음).
    void GrantTieredCombatGold()
    {
        if (MoneyManager.Instance == null) return;

        // 처치 순간에 적마다 이미 지급했다. 여기서는 유물 가산만 더한다.
        if (_killGoldPaid > 0)
        {
            int relicGoldBonus = Battle.Relic.RelicManager.Instance != null
                ? Battle.Relic.RelicManager.Instance.GetGoldRewardBonus() : 0;
            if (relicGoldBonus > 0)
                MoneyManager.Instance.AddMoney(relicGoldBonus);
            Debug.Log($"[보상] 처치 지급분 {_killGoldPaid}" + (relicGoldBonus > 0 ? $", 유물 +{relicGoldBonus}" : ""));
            _killGoldPaid = 0;
            return;
        }

        bool isBoss = currentRoundData is BossRoundData;
        bool isElite = currentRoundData is EliteRoundData;
        bool isNormalCombat = currentRoundData is CombatRoundData;
        int gold = 0;
        int rolls = Mathf.Max(1, _spawnedCombatCount);
        for (int i = 0; i < rolls; i++)
            gold += MoneyManager.Instance.RollCombatRewardGold(isBoss, isElite, isNormalCombat);
        if (gold <= 0) return;

        // 유물(복주머니 10005): 전투 보상 골드 가산
        int relicBonus = Battle.Relic.RelicManager.Instance != null
            ? Battle.Relic.RelicManager.Instance.GetGoldRewardBonus() : 0;
        gold += relicBonus;

        MoneyManager.Instance.AddMoney(gold);
        Debug.Log($"[보상] 전투 골드 +{gold}" + (relicBonus > 0 ? $" (유물 +{relicBonus})" : ""));
    }

    private bool _returnToMapPending; // 페이드 대기 중 중복 복귀 방지

    // 보상 처리 후 노드를 클리어 표시하고 맵으로 복귀한다(최종 보스 클리어 시 게임 클리어 화면).
    // 화면을 어둡게 한 뒤 전환한다. 걷기 연출처럼 이미 화면이 덮여 있으면 즉시 전환.
    public void ReturnToMap() => LeaveRound(false);

    void LeaveRound(bool withWalk)
    {
        if (_returnToMapPending) return;
        _returnToMapPending = true;
        void Finish()
        {
            _returnToMapPending = false;
            ReturnToMapNow();
        }

        bool enteringStageTwo = !DebugRoundActive && currentRoundData is BossRoundData &&
            GameStateController.Instance != null && GameStateController.Instance.CurrentLap == 1;
        if (withWalk)
        {
            var walk = FindFirstObjectByType<BattleWalkTransition>();
            if (walk == null) walk = new GameObject("BattleWalkTransition").AddComponent<BattleWalkTransition>();
            if (walk.IsPlaying)
            {
                _returnToMapPending = false;
                return;
            }
            if (enteringStageTwo) walk.PlayStageTwo(Finish);
            else walk.Play(Finish);
        }
        else if (enteringStageTwo)
        {
            var overlay = StageTwoWalkOverlay.Open(GameStateController.Instance.transform);
            if (overlay != null) overlay.StartCoroutine(overlay.CoverAndReveal(Finish));
            else ScreenFader.Transition(Finish);
        }
        else ScreenFader.Transition(Finish);
    }

    void ReturnToMapNow()
    {
        // 최종 보스 클리어는 전투 스테이지를 유지한다. 맵 복귀에서만 스테이지를 숨긴다.
        if (!IsFinalClearBoss())
            StageManager.Instance?.HideStage(); // 전투 이탈 — 3D 스테이지 숨김

        var stateController = GameStateController.Instance;
        if (stateController == null)
        {
            Debug.LogError("GameStateController.Instance가 null입니다!");
            return;
        }

        // [테스트] StageTestPanel로 들어온 라운드는 런 진행(보스 처치 수·바퀴·게임 클리어)을 바꾸지 않는다
        bool debugRound = DebugRoundActive;
        DebugRoundActive = false;
        if (debugRound)
        {
            stateController.ShowMap();
            return;
        }

        if (currentRoundData is BossRoundData)
        {
            stateController.RegisterBossDefeat(currentRoundData);

            if (stateController.HasMoreLapsRemaining)
            {
                stateController.BeginNextLap();
                HealOnStageAdvance();
                stateController.ShowMap();
                return;
            }

            stateController.MarkNodeCleared(stateController.lastVisitedNodeIndex);

            if (GameClearController.Instance != null && GameClearController.Instance.IsReady)
            {
                GameClearController.Instance.ShowGameClear();
                return;
            }

            stateController.ShowMap();
            return;
        }

        stateController.MarkNodeCleared(stateController.lastVisitedNodeIndex);
        stateController.ShowMap();
    }

    const int StageAdvanceHealAmount = 50; // 보스를 잡고 다음 스테이지로 넘어갈 때 회복량

    // 1스테이지 보스 처치 후 2스테이지로 넘어갈 때 체력 회복(최대 체력을 넘지 않음)
    void HealOnStageAdvance()
    {
        var player = Player.Resolve(true);
        if (player == null) return;
        player.Heal(StageAdvanceHealAmount);
        Debug.Log($"[스테이지 전환] 플레이어 HP +{StageAdvanceHealAmount} 회복");
    }

    // [테스트] StageTestPanel이 시작한 라운드인지 — ReturnToMap에서 런 진행 반영을 건너뛴다
    public static bool DebugRoundActive;
    // [테스트] 테스트 라운드에서 쓸 적 명단(비어 있으면 평소처럼 등장 구성 사용)
    public static List<EnemyData> DebugRosterOverride;

    static bool TryGetDebugRoster(out List<EnemyData> roster)
    {
        roster = null;
        if (!DebugRoundActive || DebugRosterOverride == null || DebugRosterOverride.Count == 0) return false;
        roster = new List<EnemyData>(DebugRosterOverride);
        return true;
    }

    // 진행 중이던 전투를 정리하고 남은 적 GameObject를 제거한다(플레이어 사망/재시작 시).
    public void AbortActiveCombat()
    {
        DebugRoundActive = false;
        StageManager.Instance?.HideStage(); // 전투 중단 — 3D 스테이지 숨김
        Battle.NewBattleController.Instance?.EndBattle();

        EnemyCombatParty.DestroyAllMembers();
        currentEnemy = null;

        StopAllCoroutines();
        _restartRoutine = null; // 다시 시작 대기 중이었다면 함께 취소됨

        // 2D 스폰 위치(EnemyPos) 아래 남은 적만 지운다.
        // 같은 트랜스폼에 3D 전투 HP바·예고 글자가 상주하므로, 적 컴포넌트가 없는 자식은 남긴다.
        if (enemySpawnPoint != null)
        {
            for (int i = enemySpawnPoint.childCount - 1; i >= 0; i--)
            {
                Transform child = enemySpawnPoint.GetChild(i);
                if (child == null) continue;
                if (child.GetComponent<EnemyStat>() == null && child.GetComponent<EnemyController>() == null)
                    continue;
                Destroy(child.gameObject);
            }
        }

        // 3D: 스테이지 앵커 아래 남은 적 정리(사망 연출 중이던 잔여 포함)
        if (BattleStageController.Current != null)
            foreach (Transform anchor in BattleStageController.Current.AllEnemyAnchors())
                DestroyChildrenOf(anchor);

        currentEnemyIndex = 0;
        _spawnedCombatCount = 0;
        _killGoldPaid = 0;
        _waitingForRoundEnd = false;
        _returnToMapPending = false; // 보상 후 맵 복귀 대기 중 런을 포기한 경우(다음 런의 복귀가 막히지 않게)
    }

    // 현재 3D 스테이지 앵커 아래 남은 적 오브젝트를 즉시 숨기고 제거한다(전투 시작 직전 — 살아 있는 적은 없어야 함)
    static void ClearLeftoverStageEnemies()
    {
        if (BattleStageController.Current == null) return;
        foreach (Transform anchor in BattleStageController.Current.AllEnemyAnchors())
        {
            if (anchor == null) continue;
            for (int i = anchor.childCount - 1; i >= 0; i--)
            {
                Transform child = anchor.GetChild(i);
                if (child == null) continue;
                if (child.GetComponent<EnemyController>() == null && child.GetComponent<EnemyStat>() == null) continue;
                Debug.LogWarning($"[RoundManager] 스테이지에 남아 있던 적 제거: {child.name}");
                child.gameObject.SetActive(false); // 같은 프레임에 새 적과 겹쳐 그려지지 않게
                Destroy(child.gameObject);
            }
        }
    }

    static void DestroyChildrenOf(Transform parent)
    {
        if (parent == null) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    // 일반 전투 명단: 인스펙터 구간 설정이 있으면 그걸 쓰고, 없으면 라운드 에셋 목록.
    List<EnemyData> ResolveCombatRoster(CombatRoundData data)
    {
        if (TryGetDebugRoster(out List<EnemyData> debugRoster))
        {
            data.enemies = debugRoster;
            return debugRoster;
        }

        int lap = 1;
        int stage = 1;
        GameStateController state = GameStateController.Instance;
        if (state != null)
        {
            lap = state.CurrentLap;
            stage = state.CurrentDisplayStage;
        }

        if (encounterConfig != null)
        {
            List<EnemyData> composed = encounterConfig.ComposeCombatEnemies(lap, stage);
            if (composed != null && composed.Count > 0)
            {
                data.enemies = composed;
                Debug.Log($"[등장] {lap}-{stage} 일반 전투 {composed.Count}마리");
                return composed;
            }
        }

        return data.enemies ?? new List<EnemyData>();
    }

    // 정예 전투 명단: 현재 라운드의 정예 프리셋이 있으면 그걸 쓰고, 없으면 라운드 에셋의 첫 적.
    List<EnemyData> ResolveEliteRoster(EliteRoundData data)
    {
        if (TryGetDebugRoster(out List<EnemyData> debugRoster)) return debugRoster;

        int lap = 1;
        GameStateController state = GameStateController.Instance;
        if (state != null)
            lap = state.CurrentLap;

        if (encounterConfig != null)
        {
            List<EnemyData> composed = encounterConfig.ComposeEliteEnemies(lap);
            if (composed != null && composed.Count > 0)
            {
                Debug.Log($"[등장] {lap}라운드 정예 전투 {composed.Count}마리");
                return composed;
            }
        }

        return TakeFirstEnemy(data != null ? data.enemies : null);
    }

    static List<EnemyData> TakeFirstEnemy(List<EnemyData> enemies)
    {
        var result = new List<EnemyData>(1);
        if (enemies == null) return result;
        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] == null) continue;
            result.Add(enemies[i]);
            break;
        }
        return result;
    }

    // 명단의 적을 전부 동시에 스폰한다.
    void SpawnParty(List<EnemyData> enemies, int columnIndex, NodeType nodeType)
    {
        _injectedEnemies.Clear();
        _waitingForRoundEnd = false;
        currentEnemyIndex = 0;

        if (enemies == null || enemies.Count == 0)
        {
            Debug.LogError("RoundManager: 스폰할 적이 없습니다.");
            EnemyCombatParty.BeginBattle(0);
            _spawnedCombatCount = 0;
            return;
        }

        // 이전 전투에서 남은 적(사망 연출 중 스테이지가 꺼져 지워지지 못한 시체 등)이 새 적과 겹치지 않게 먼저 치운다
        ClearLeftoverStageEnemies();

        int n = enemies.Count;
        EnemyCombatParty.BeginBattle(n);
        _spawnedCombatCount = n;
        _killGoldPaid = 0;

        for (int i = 0; i < n; i++)
            SpawnEnemy(enemies[i], columnIndex, nodeType, injected: false, slotIndex: i, partySize: n);
    }

    // 적 프리팹을 생성해 스탯/뷰/배경/사망 이벤트를 설정한다.
    void SpawnEnemy(EnemyData data, int columnIndex, NodeType nodeType, bool injected = false, int slotIndex = 0, int partySize = 1)
    {
        _currentIsInjected = injected; // 주입(복제)된 적은 사망 시 인덱스를 증가시키지 않음

        // 3D 전투: EnemyData에 3D 프리팹이 있고 현재 로드된 3D 스테이지가 있으면 3D 경로, 아니면 2D 폴백.
        bool use3D = data != null && data.battlePrefab3D != null && BattleStageController.Current != null;
        Camera stageCam = use3D ? BattleStageController.Current.StageCamera : null;

        if (!use3D && enemyPrefab == null)
        {
            Debug.LogError("RoundManager: enemyPrefab이 없습니다(2D 폴백 불가).");
            return;
        }

        GameObject go;
        if (use3D)
        {
            Transform anchor = BattleStageController.Current.ResolveEnemyAnchor(slotIndex, partySize);
            // instantiateInWorldSpace=false → 프리팹의 로컬 트랜스폼(몬스터별 스케일/회전) 보존
            go = Instantiate(data.battlePrefab3D, anchor, false);
            go.transform.position = BattleStageController.Current.ResolvePartyWorldPosition(slotIndex, partySize);
            float scaleMul = BattleStageController.Current.ResolvePartyScale(slotIndex, partySize);
            go.transform.localScale *= scaleMul;
        }
        else
        {
            go = Instantiate(enemyPrefab, enemySpawnPoint);
            RectTransform rectTransform = go.GetComponent<RectTransform>();
            if (rectTransform != null)
                PlaceTwoDEnemy(rectTransform, slotIndex, partySize);
        }

        EnemyStat stat = go.GetComponent<EnemyStat>();
        IEnemyView view = go.GetComponent<IEnemyView>();
        EnemyController controller = go.GetComponent<EnemyController>();

        // 3D 뷰에 스테이지 카메라 주입(HUD WorldToScreen 팔로우 기준)
        if (use3D && view is EnemyView3D view3D)
            view3D.SetRenderCamera(stageCam);

        if (stat == null)
        {
            Debug.LogError("RoundManager: enemyPrefab에 EnemyStat이 없습니다.");
            return;
        }

        AttachEnemyUi(controller, view, use3D, stageCam, slotIndex, partySize);

        if (view != null && data.enemySprite != null)
            view.SetSprite(data.enemySprite);

        if (view != null)
            view.SetHitSprite(data.hitSprite);

        if (view != null)
            view.SetAttackSprites(data.attackSprites);

        // 3D 스테이지에선 2D 배경을 건드리지 않는다(3D 포레스트가 배경 역할).
        if (!use3D && combatStageController != null && slotIndex == 0)
            combatStageController.ApplyEnemyBackground(data);

        stat.Initialize(data, columnIndex, nodeType, difficultyConfig);
        if (view != null)
            view.SetSpecialNote(data != null ? data.Note : null);

        // 게이지 최대치는 Initialize에서 확정되므로, 뷰 텍스트를 이 적의 실제 최대치로 즉시 갱신(0/max 표시)
        if (view != null)
            view.UpdateActionGauge(0f);

        // 여러 마리 전투이거나 주입된 사본이면 복제를 막는다.
        if (controller != null && (injected || partySize > 1))
            controller.AllowDuplication = false;

        EnemyStat dying = stat;
        IEnemyView dyingView = view;
        stat.OnDied += () => OnPartyMemberDied(dying, dyingView);
        currentEnemy = stat;
        if (controller != null) EnemyCombatParty.Register(controller);
    }

    void PlaceTwoDEnemy(RectTransform rectTransform, int slotIndex, int partySize)
    {
        int side = BattleStageController.PartySideSign(slotIndex, partySize);
        float scaleMul = side == 0 ? 1f : 0.6f;

        var stage = BattleStageController.Current;
        if (stage != null)
        {
            rectTransform.anchoredPosition = stage.ResolvePartyCanvasOffset(
                rectTransform.parent as RectTransform, slotIndex, partySize);
            scaleMul = stage.ResolvePartyScale(slotIndex, partySize);
        }
        else
        {
            rectTransform.anchoredPosition = new Vector2(side * twoDMultiSpawnSpacing, side == 0 ? 0f : -50f);
        }

        rectTransform.localScale = Vector3.one * scaleMul;
    }

    void AttachEnemyUi(EnemyController controller, IEnemyView view, bool use3D, Camera stageCam, int slotIndex, int partySize)
    {
        if (use3D && view is EnemyView3D view3D)
        {
            EnemyOverlayHUD hud = EnemyOverlayHUD.Instance;
            if (slotIndex > 0 && EnemyOverlayHUD.Instance != null)
            {
                hud = EnemyOverlayHUD.Instance.CreateClone();
                EnemyCombatParty.TrackHudClone(hud);
            }
            if (hud != null)
                view3D.SetOverlayHud(hud, true);
        }

        if (enemyStatusPanel == null || controller == null) return;

        StatusPanelUI panel = enemyStatusPanel;
        if (slotIndex > 0)
        {
            panel = Instantiate(enemyStatusPanel, enemyStatusPanel.transform.parent);
            panel.name = enemyStatusPanel.name + "_Clone";
            EnemyCombatParty.TrackStatusClone(panel);
        }

        panel.SetTarget(controller);
        if (view != null)
            panel.DockAboveHpBar(view);
    }

    // 적 사망 시 재화 연출을 띄우고, 남은 적이 없으면 라운드 종료를 예약한다.
    void OnPartyMemberDied(EnemyStat stat, IEnemyView view)
    {
        if (!DebugRoundActive && stat != null && stat.Data != null && GameStateController.Instance != null)
            GameStateController.Instance.RecordRunKill(stat.Data.enemySprite, stat.Data.enemyName, stat.Data.enemyClass);

        Vector2 screen = ResolveDeathScreen(stat, view);
        PlayKillReward(screen);
        if (IsLastRewardKill() && !IsFinalClearBoss())
            RewardDropFx.Play(screen, ShouldShowComboBookRewardAfterCard(), ShouldDropStageBossRelic());
        HandleEnemyDied();
    }

    // 이번 처치로 전투가 끝나고 카드(와 정예·보스면 콤보) 보상이 열리는지.
    bool IsLastRewardKill()
    {
        if (!IsNewBattleSystemActive()) return false;
        if (_waitingForRoundEnd) return false;
        if (EnemyCombatParty.AliveCount > 0) return false;
        if (!EnemyCombatParty.IsMulti && _injectedEnemies.Count > 0) return false;
        return currentRoundData is CombatRoundData
            || currentRoundData is EliteRoundData
            || currentRoundData is BossRoundData;
    }

    // 죽은 적의 화면 좌표. 조준점이 없으면 월드 위치를 투영한다.
    static Vector2 ResolveDeathScreen(EnemyStat stat, IEnemyView view)
    {
        if (view != null && view.TryGetAimScreen(out Vector2 aim, out _))
            return aim;

        if (stat != null)
        {
            Camera cam = BattleStageController.Current != null
                ? BattleStageController.Current.StageCamera
                : Camera.main;
            if (cam != null)
            {
                Vector3 sp = cam.WorldToScreenPoint(stat.transform.position);
                if (sp.z > 0f) return new Vector2(sp.x, sp.y);
            }
        }

        return new Vector2(Screen.width * 0.5f, Screen.height * 0.55f);
    }

    // 처치 위치에 +N을 띄우고, 영혼이 재화 UI에 닿으면 골드를 넣는다.
    void PlayKillReward(Vector2 screen)
    {
        if (MoneyManager.Instance == null) return;

        bool isBoss = currentRoundData is BossRoundData;
        bool isElite = currentRoundData is EliteRoundData;
        bool isNormalCombat = currentRoundData is CombatRoundData;
        int gold = MoneyManager.Instance.RollCombatRewardGold(isBoss, isElite, isNormalCombat);

        if (gold <= 0) return;
        _killGoldPaid += gold;

        int pay = gold;
        KillRewardFx.Play(screen, pay, () =>
        {
            if (MoneyManager.Instance != null)
                MoneyManager.Instance.AddMoney(pay);
        });
    }

    public void HandleEnemyDied()
    {
        if (EnemyCombatParty.AliveCount > 0)
            return;

        if (_waitingForRoundEnd) return;
        _waitingForRoundEnd = true;

        if (!_currentIsInjected)
            currentEnemyIndex++;

        StartCoroutine(SpawnNextAfterDelay());
    }

    // 일정 지연 후 복제 적을 스폰하거나 라운드를 종료한다.
    IEnumerator SpawnNextAfterDelay(float delay = 1.5f)
    {
        yield return new WaitForSeconds(delay);

        // 여러 마리 전투에서는 복제 스폰을 하지 않는다.
        if (!EnemyCombatParty.IsMulti && _injectedEnemies.Count > 0 && TryGetCurrentColumnAndType(out int col, out NodeType nt))
        {
            var dup = _injectedEnemies[0];
            _injectedEnemies.RemoveAt(0);
            EnemyCombatParty.BeginBattle(1);
            _spawnedCombatCount = 1;
            _waitingForRoundEnd = false;
            SpawnEnemy(dup, col, nt, injected: true);
            yield break;
        }

        EndRound();
    }

    // 현재 라운드의 열 인덱스/노드 타입 조회(복제 스폰용)
    bool TryGetCurrentColumnAndType(out int columnIndex, out NodeType nodeType)
    {
        if (currentRoundData is CombatRoundData c) { columnIndex = c.columnIndex; nodeType = c.roundType; return true; }
        if (currentRoundData is EliteRoundData e) { columnIndex = e.columnIndex; nodeType = e.roundType; return true; }
        if (currentRoundData is BossRoundData b)  { columnIndex = b.columnIndex; nodeType = NodeType.Boss; return true; }
        columnIndex = 0; nodeType = NodeType.Combat; return false;
    }

    // 특이사항(액괴 복제): 지정 적 데이터의 복제본을 다음 스폰 큐에 추가한다.
    public void RequeueEnemyCopy(EnemyData data)
    {
        if (data == null) return;
        if (EnemyCombatParty.IsMulti)
        {
            Debug.Log($"[특이사항] {data.enemyName} 복제 생략 — 여러 마리 전투");
            return;
        }
        _injectedEnemies.Add(data);
        Debug.Log($"[특이사항] {data.enemyName} 복제 예약 (대기 {_injectedEnemies.Count})");
    }
}

