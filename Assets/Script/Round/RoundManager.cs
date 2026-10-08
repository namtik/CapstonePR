using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using Battle.Relic;

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
    [Tooltip("스테이지(바퀴-번호)별 마릿수와 몬스터 후보 풀. 정예/보스는 사용하지 않는다.")]
    [SerializeField] private EnemyEncounterConfig encounterConfig;
    [Header("디버그 — 보스 3마리 (임시)")]
    [Tooltip("ON이면 보스방에 같은 보스를 3마리 스폰한다. 3D 여러 마리 배치 확인용.")]
    [SerializeField] private bool debugSpawnThreeBosses = true;
    [Tooltip("2D 폴백에서 여러 마리를 가로로 벌릴 간격(px).")]
    [SerializeField] private float twoDMultiSpawnSpacing = 220f;

    [Header("상태이상 UI 패널")]
    [SerializeField] private StatusPanelUI playerStatusPanel; // 플레이어 상태이상 패널
    [SerializeField] private StatusPanelUI enemyStatusPanel; // 적 상태이상 패널

    [Header("보상 UI")]
    [SerializeField] private RewardHubUIController rewardHubUIController; // 보상 허브 UI 컨트롤러

    [Header("보상 상단 문구 - 카드")]
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

    private RoundData currentRoundData; // 현재 진행 중인 라운드 데이터
    private int currentEnemyIndex = 0; // 현재 처리 중인 적 인덱스(레거시 순차 스폰용)
    private EnemyStat currentEnemy; // 마지막으로 스폰한 적 스탯
    private readonly List<EnemyData> _injectedEnemies = new List<EnemyData>(); // 특이사항(복제) 등으로 끼어든 적 큐
    private bool _currentIsInjected = false; // 현재 적이 주입(복제)된 적인지 여부
    private int _spawnedCombatCount = 1; // 이번 전투에 스폰한 적 수(골드 계산용)
    private bool _waitingForRoundEnd = false; // 전멸 후 라운드 종료 예약 여부
    public event System.Action OnRoundClear; // 라운드 클리어 이벤트
    private IRoundHandler currentRoundHandler; // 현재 라운드 핸들러
    private int clearedCombatCount = 0; // 클리어한 전투 수


    // 런타임 플레이어가 없으면 생성하고 상태 패널을 연결한다.
    void EnsureRuntimePlayerExists()
    {
        Player existingPlayer = Player.Resolve(true);
        if (existingPlayer != null)
        {
            if (playerStatusPanel != null) playerStatusPanel.SetTarget(existingPlayer);
            return;
        }

        Player newPlayer = Player.GetOrCreateRuntime();

        Debug.Log("[RoundManager] Hidden runtime Player created");

        if (playerStatusPanel != null) playerStatusPanel.SetTarget(newPlayer);

        Debug.Log("[RoundManager] Hidden runtime Player created");
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

    // 레거시 원소 슬롯/HUD 전투 시스템을 보장 생성한다(새 전투 시스템이면 건너뜀).
    void EnsureElementCombatSystems()
    {
        EnsureRuntimePlayerExists();

        if (IsNewBattleSystemActive())
            return;

        var slotSystem = ElementSlotSystem.Instance ?? FindFirstObjectByType<ElementSlotSystem>();
        if (slotSystem == null)
        {
            var go = new GameObject("ElementSlotSystem");
            slotSystem = go.AddComponent<ElementSlotSystem>();
            DontDestroyOnLoad(go);
        }

        var hud = FindFirstObjectByType<ElementSlotHUD>();
        if (hud == null)
        {
            var hudGo = new GameObject("ElementSlotHUD");
            Canvas combatCanvas = ResolveCombatStageCanvas();
            if (combatCanvas != null)
                hudGo.transform.SetParent(combatCanvas.transform, false);
            hudGo.AddComponent<ElementSlotHUD>();
        }

        var legacySystems = FindObjectsByType<CardSystem>(FindObjectsSortMode.None);
        foreach (var legacy in legacySystems)
        {
            if (legacy != null)
            {
                legacy.ForceDisableForElementSystem();
                legacy.gameObject.SetActive(false);
            }
        }
    }

    // 전투 스테이지의 캔버스를 찾아 반환한다.
    Canvas ResolveCombatStageCanvas()
    {
        if (combatStageController != null)
        {
            Canvas fromController = combatStageController.GetComponentInChildren<Canvas>(true);
            if (fromController != null)
                return fromController;
        }

        GameObject combatStageObject = GameObject.Find("CombatStage");
        if (combatStageObject == null)
            return FindFirstObjectByType<Canvas>();

        return combatStageObject.GetComponentInChildren<Canvas>(true);
    }

    // 라운드를 시작한다(필요 시 초기 스킬 선택을 먼저 진행).
    public void StartRound(RoundData roundData)
    {
        EnsureElementCombatSystems();

        if (IsNewBattleSystemActive())
        {
            ContinueStartRound(roundData);
            return;
        }

        if (ComboSystem.Instance != null && ComboSystem.Instance.learnedSkills.Count == 0)
        {
            ComboSystem.Instance.LearnStarterSkill();

            SkillRewardUI rewardUI = SkillDataParser.Instance?.SkillRewardUI;
            if (rewardUI != null)
            {
                rewardUI.OnSkillSelected += OnStarterSkillSelected;
                pendingRoundData = roundData;
                return;
            }
        }

        ContinueStartRound(roundData);
    }

    private RoundData pendingRoundData; // 초기 스킬 선택 대기 중인 라운드 데이터

    // 초기 스킬 선택 완료 시 대기 중이던 라운드를 진행한다.
    void OnStarterSkillSelected(SkillDataParser.SkillData skill)
    {
        SkillRewardUI rewardUI = SkillDataParser.Instance?.SkillRewardUI;
        if (rewardUI != null)
            rewardUI.OnSkillSelected -= OnStarterSkillSelected;

        if (pendingRoundData != null)
        {
            ContinueStartRound(pendingRoundData);
            pendingRoundData = null;
        }
    }

    // 라운드 데이터를 설정하고 핸들러를 만들어 라운드를 진행한다.
    void ContinueStartRound(RoundData roundData)
    {
        currentRoundData = roundData;
        currentEnemyIndex = 0;

        if (combatStageController != null)
        {
            combatStageController.Initialize(roundData);
        }

        currentRoundHandler = roundData.CreateHandler();
        currentRoundHandler.OnEnterRound(this);
    }

    // 라운드를 종료하고 종료 핸들러와 클리어 이벤트를 호출한다.
    public void EndRound()
    {
        if (!IsNewBattleSystemActive())
            ElementSlotSystem.Instance?.EndBattle();
        else
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
        StageManager.Instance?.ShowStage(); // 3D 스테이지 표시(컨셉 미지정 → fallback)
        EnsureElementCombatSystems();
        if (!IsNewBattleSystemActive())
            ElementSlotSystem.Instance?.StartBattle();

        Player player = Player.Resolve(true);
        if (player != null) player.ResetStatusForNewBattle();

        SpawnParty(ResolveCombatRoster(data), data.columnIndex, data.roundType);

        if (IsNewBattleSystemActive()) BeginNewBattleForNode();
    }

    // 정예 전투를 시작한다.
    public void StartCombat(EliteRoundData data)
    {
        StageManager.Instance?.ShowStage(); // 3D 스테이지 표시(컨셉 미지정 → fallback)
        EnsureElementCombatSystems();
        if (!IsNewBattleSystemActive())
            ElementSlotSystem.Instance?.StartBattle();

        Player player = Player.Resolve(true);
        if (player != null) player.ResetStatusForNewBattle();

        SpawnParty(TakeFirstEnemy(data.enemies), data.columnIndex, data.roundType);

        if (IsNewBattleSystemActive()) BeginNewBattleForNode();
    }

    // 보스 전투를 시작한다.
    public void StartBoss(BossRoundData data)
    {
        StageManager.Instance?.ShowStage(); // 3D 스테이지 표시(컨셉 미지정 → fallback)
        EnsureElementCombatSystems();
        if (!IsNewBattleSystemActive())
            ElementSlotSystem.Instance?.StartBattle();

        Player player = Player.Resolve(true);
        if (player != null) player.ResetStatusForNewBattle();

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

        if (IsNewBattleSystemActive()) BeginNewBattleForNode();
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
        Debug.Log("[RoundManager] 상점 스테이지 시작");
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
        Debug.Log("[RoundManager] 이벤트 스테이지 시작");
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
        Debug.Log("[RoundManager] 휴식 스테이지 시작");
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
        Debug.Log("[RoundManager] 유물 스테이지 시작");
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

        if (IsNewBattleSystemActive())
        {
            ShowCardReward();
            return;
        }

        Debug.Log("스킬 보상 선택 UI 표시");

        if (rewardHubUIController == null)
            rewardHubUIController = FindFirstObjectByType<RewardHubUIController>(FindObjectsInactive.Include);

        Debug.Log($"[RoundManager] rewardHubUIController={(rewardHubUIController != null ? rewardHubUIController.name : "NULL")}");

        if (rewardHubUIController != null)
        {
            Debug.Log("[RoundManager] Opening RewardHubUIController.OpenHub()");
            rewardHubUIController.OpenHub();
            return;
        }

        if (SkillDataParser.Instance != null && SkillDataParser.Instance.SkillRewardUI != null)
        {
            Debug.Log("[RoundManager] Fallback -> SkillRewardUI.ShowRewardOptions()");
            SkillDataParser.Instance.SkillRewardUI.ShowRewardOptions();
        }
        else
            Debug.LogError("[RoundManager] RewardHubUIController와 SkillRewardUI가 모두 없습니다.");
    }


    // 신규 시스템 카드 획득 보상을 표시한다(제시 카드 중 1장을 런 덱에 추가).
    void ShowCardReward()
    {
        Debug.Log("[RoundManager] 카드 획득 보상 표시");

        var hud = FindFirstObjectByType<Battle.UI.CardHandHUD>(FindObjectsInactive.Include);
        Battle.UI.NewCardView cardPrefab = hud != null ? hud.CardPrefab : null;

        var rewardUI = Battle.UI.CardRewardUI.EnsureExists();
        rewardUI.SetTitleStyle(cardRewardTitleFont, cardRewardTitleFontSize, cardRewardTitleColor, cardRewardTitlePosition);
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

        ReturnToMap();
    });
}

    // 카드 보상 후 콤보 책 보상을 표시할 라운드인지 판정한다(정예/보스).
    bool ShouldShowComboBookRewardAfterCard()
    {
        return currentRoundData is EliteRoundData || currentRoundData is BossRoundData;
    }

    // 카드 보상 후 콤보 책 보상 UI를 표시하고 선택한 콤보를 지급한다.
    void ShowComboBookRewardAfterCard()
    {
        ShowComboBookReward(ReturnToMap);
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

        bool isBoss = currentRoundData is BossRoundData;
        bool isElite = currentRoundData is EliteRoundData;
        bool isNormalCombat = currentRoundData is CombatRoundData;
        int gold = 0;
        int rolls = Mathf.Max(1, _spawnedCombatCount);
        for (int i = 0; i < rolls; i++)
            gold += MoneyManager.Instance.RollCombatRewardGold(isBoss, isElite, isNormalCombat);
        if (gold <= 0) return;

        // 유물(복주머니 10005): 전투 보상 골드 가산
        int relicGoldBonus = Battle.Relic.RelicManager.Instance != null
            ? Battle.Relic.RelicManager.Instance.GetGoldRewardBonus() : 0;
        gold += relicGoldBonus;

        MoneyManager.Instance.AddMoney(gold);
        Debug.Log($"[보상] 전투 골드 +{gold}" + (relicGoldBonus > 0 ? $" (유물 +{relicGoldBonus})" : ""));
    }

    // 보상 처리 후 노드를 클리어 표시하고 맵으로 복귀한다(최종 보스 클리어 시 게임 클리어 화면).
    public void ReturnToMap()
    {
        StageManager.Instance?.HideStage(); // 전투 이탈 — 3D 스테이지 숨김
        if (!IsNewBattleSystemActive())
            ElementSlotSystem.Instance?.EndBattle();

        var stateController = GameStateController.Instance;
        if (stateController == null)
        {
            Debug.LogError("GameStateController.Instance가 null입니다!");
            return;
        }

        if (currentRoundData is BossRoundData)
        {
            stateController.RegisterBossDefeat();

            if (stateController.HasMoreLapsRemaining)
            {
                stateController.BeginNextLap();
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

    // 진행 중이던 전투를 정리하고 남은 적 GameObject를 제거한다(플레이어 사망/재시작 시).
    public void AbortActiveCombat()
    {
        StageManager.Instance?.HideStage(); // 전투 중단 — 3D 스테이지 숨김
        if (IsNewBattleSystemActive())
            Battle.NewBattleController.Instance?.EndBattle();
        else
            ElementSlotSystem.Instance?.EndBattle();

        EnemyCombatParty.DestroyAllMembers();
        currentEnemy = null;

        StopAllCoroutines();

        // 2D: enemySpawnPoint 아래 남은 적 정리
        if (enemySpawnPoint != null)
        {
            for (int i = enemySpawnPoint.childCount - 1; i >= 0; i--)
                Destroy(enemySpawnPoint.GetChild(i).gameObject);
        }

        // 3D: 스테이지 앵커 아래 남은 적 정리(사망 연출 중이던 잔여 포함)
        DestroyChildrenOf(BattleStageController.Current != null ? BattleStageController.Current.EnemyAnchor : null);

        currentEnemyIndex = 0;
        _spawnedCombatCount = 0;
        _waitingForRoundEnd = false;
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

        int n = enemies.Count;
        EnemyCombatParty.BeginBattle(n);
        _spawnedCombatCount = n;

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
        Debug.Log($"Initialize 호출: HP={data.maxHp}, col={columnIndex}, slot={slotIndex}/{partySize}, 3D={use3D}");

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

        // 게이지 최대치는 Initialize에서 확정되므로, 뷰 텍스트를 이 적의 실제 최대치로 즉시 갱신(0/max 표시)
        if (view != null)
            view.UpdateActionGauge(0f);

        // 여러 마리 전투이거나 주입된 사본이면 복제를 막는다.
        if (controller != null && (injected || partySize > 1))
            controller.AllowDuplication = false;

        stat.OnDied += HandleEnemyDied;
        currentEnemy = stat;
        if (controller != null) EnemyCombatParty.Register(controller);

        MonsterMidPattern midPattern = go.GetComponent<MonsterMidPattern>();
        if (midPattern != null)
        {
            midPattern.InitBattle();
        }
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
        bool followWorld = partySize > 1;

        if (use3D && view is EnemyView3D view3D)
        {
            EnemyOverlayHUD hud = EnemyOverlayHUD.Instance;
            if (slotIndex > 0 && EnemyOverlayHUD.Instance != null)
            {
                hud = EnemyOverlayHUD.Instance.CreateClone();
                EnemyCombatParty.TrackHudClone(hud);
            }
            if (hud != null)
                view3D.SetOverlayHud(hud, followWorld);
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
        {
            if (use3D) panel.SetFollowTarget(view.ShakeTarget, stageCam);
            else panel.SetFollowTarget(view.ShakeTarget);
        }
    }

    // 적 사망 시 재화를 지급하고, 남은 적이 없으면 라운드 종료를 예약한다.
    public void HandleEnemyDied()
    {
        Debug.Log("[RoundManager.HandleEnemyDied] 호출됨");

        if (!IsNewBattleSystemActive() && MoneyManager.Instance != null)
        {
            MoneyManager.Instance.OnEnemyKilled();
        }

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

