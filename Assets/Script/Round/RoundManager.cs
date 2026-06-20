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

    private RoundData currentRoundData; // 현재 진행 중인 라운드 데이터
    private int currentEnemyIndex = 0; // 현재 처리 중인 적 인덱스
    private EnemyStat currentEnemy; // 현재 적 스탯
    public event System.Action OnRoundClear; // 라운드 클리어 이벤트
    private IRoundHandler currentRoundHandler; // 현재 라운드 핸들러
    private int clearedCombatCount = 0; // 클리어한 전투 수


    // 런타임 플레이어가 없으면 생성하고 상태 패널을 연결한다.
    void EnsureRuntimePlayerExists()
    {
        Player existingPlayer = Player.Resolve(true);
        if (existingPlayer != null)
        {
            existingPlayer.UpdateUIForExternalSync();

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
        EnsureElementCombatSystems();
        if (!IsNewBattleSystemActive())
            ElementSlotSystem.Instance?.StartBattle();

        Player player = Player.Resolve(true);
        if (player != null) player.ResetStatusForNewBattle();

        currentEnemyIndex = 0;
        SpawnNextEnemy(data.enemies, data.columnIndex, data.roundType);

        if (IsNewBattleSystemActive()) BeginNewBattleForNode();
    }

    // 정예 전투를 시작한다.
    public void StartCombat(EliteRoundData data)
    {
        EnsureElementCombatSystems();
        if (!IsNewBattleSystemActive())
            ElementSlotSystem.Instance?.StartBattle();

        Player player = Player.Resolve(true);
        if (player != null) player.ResetStatusForNewBattle();

        currentEnemyIndex = 0;
        SpawnNextEnemy(data.enemies, data.columnIndex, data.roundType);

        if (IsNewBattleSystemActive()) BeginNewBattleForNode();
    }

    // 보스 전투를 시작한다.
    public void StartBoss(BossRoundData data)
    {
        EnsureElementCombatSystems();
        if (!IsNewBattleSystemActive())
            ElementSlotSystem.Instance?.StartBattle();

        Player player = Player.Resolve(true);
        if (player != null) player.ResetStatusForNewBattle();

        SpawnEnemy(data.bossEnemy, data.columnIndex, NodeType.Boss);

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

    // 유물 스테이지 보상을 처리한다(후보가 있으면 후보, 없으면 전체에서 랜덤 지급).
    public void GrantRelicFromStage(IReadOnlyList<RelicEffectType> candidates)
    {
        if (RelicManager.Instance == null)
        {
            Debug.LogWarning("[RoundManager] RelicManager가 없어 유물 지급을 건너뜁니다.");
            return;
        }

        List<RelicEffectType> pool = new List<RelicEffectType>();

        if (candidates != null)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                var effect = candidates[i];
                if (effect == RelicEffectType.None) continue;
                if (!pool.Contains(effect)) pool.Add(effect);
            }
        }

        if (pool.Count == 0)
        {
            foreach (RelicEffectType effect in System.Enum.GetValues(typeof(RelicEffectType)))
            {
                if (effect == RelicEffectType.None) continue;
                pool.Add(effect);
            }
        }

        if (pool.Count == 0)
        {
            Debug.LogWarning("[RoundManager] 지급 가능한 유물 효과가 없습니다.");
            return;
        }

        var picked = pool[Random.Range(0, pool.Count)];
        RelicManager.Instance.GiveRelicByEffect(picked);
        Debug.Log($"[RoundManager] RelicStage 보상 지급: {picked}");
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
        var panel = FindFirstObjectByType<Battle.UI.ComboBookRewardPanelUI>(FindObjectsInactive.Include);
        if (panel == null)
        {
            Debug.LogWarning("[RoundManager] ComboBookRewardPanelUI를 찾지 못해 콤보 보상을 생략하고 맵으로 복귀합니다.");
            ReturnToMap();
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

            ReturnToMap();
        });

        Debug.Log("[RoundManager] 카드 보상 후 책 UI 콤보 보상 표시");
    }

    // 전투 등급별 골드를 일괄 지급한다(비전투 라운드는 지급하지 않음).
    void GrantTieredCombatGold()
    {
        if (MoneyManager.Instance == null) return;

        bool isBoss = currentRoundData is BossRoundData;
        bool isElite = currentRoundData is EliteRoundData;
        bool isNormalCombat = currentRoundData is CombatRoundData;
        int gold = MoneyManager.Instance.RollCombatRewardGold(isBoss, isElite, isNormalCombat);
        if (gold <= 0) return;

        MoneyManager.Instance.AddMoney(gold);
        Debug.Log($"[보상] 전투 골드 +{gold}");
    }

    // 보상 처리 후 노드를 클리어 표시하고 맵으로 복귀한다(최종 보스 클리어 시 게임 클리어 화면).
    public void ReturnToMap()
    {
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
        if (IsNewBattleSystemActive())
            Battle.NewBattleController.Instance?.EndBattle();
        else
            ElementSlotSystem.Instance?.EndBattle();

        if (currentEnemy != null)
        {
            currentEnemy.OnDied -= HandleEnemyDied;
            currentEnemy = null;
        }

        StopAllCoroutines();

        if (enemySpawnPoint != null)
        {
            for (int i = enemySpawnPoint.childCount - 1; i >= 0; i--)
                Destroy(enemySpawnPoint.GetChild(i).gameObject);
        }

        currentEnemyIndex = 0;
    }

    // 다음 적을 스폰한다(목록을 모두 처치했으면 라운드 종료).
    void SpawnNextEnemy(List<EnemyData> enemies, int columnIndex, NodeType nodeType)
    {
        if (currentEnemyIndex >= enemies.Count)
        {
            EndRound();
            return;
        }

        SpawnEnemy(enemies[currentEnemyIndex], columnIndex, nodeType);
    }

    // 적 프리팹을 생성해 스탯/뷰/배경/사망 이벤트를 설정한다.
    void SpawnEnemy(EnemyData data, int columnIndex, NodeType nodeType)
    {
        if (enemyPrefab == null)
        {
            Debug.LogError("RoundManager: enemyPrefab이 없습니다.");
            return;
        }

        GameObject go = Instantiate(enemyPrefab, enemySpawnPoint);

        RectTransform rectTransform = go.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.localScale = Vector3.one;
        }

        EnemyStat stat = go.GetComponent<EnemyStat>();
        EnemyView view = go.GetComponent<EnemyView>();

        EnemyController controller = go.GetComponent<EnemyController>();

        if (stat == null)
        {
            Debug.LogError("RoundManager: enemyPrefab에 EnemyStat이 없습니다.");
            return;
        }
        if (enemyStatusPanel != null && controller != null)
        {
            enemyStatusPanel.SetTarget(controller);
            if (view != null) enemyStatusPanel.SetFollowTarget(view.ShakeTarget);
        }
        Debug.Log($"Initialize 호출: HP={data.maxHp}, col={columnIndex}");

        if (view != null && data.enemySprite != null)
            view.SetSprite(data.enemySprite);

        if (view != null)
            view.SetHitSprite(data.hitSprite);

        if (view != null)
            view.SetAttackSprites(data.attackSprites);

        if (combatStageController != null)
            combatStageController.ApplyEnemyBackground(data);

        stat.Initialize(data, columnIndex, nodeType, difficultyConfig);

        if (currentEnemy != null)
        {
            currentEnemy.OnDied -= HandleEnemyDied;
        }

        stat.OnDied += HandleEnemyDied;
        currentEnemy = stat;
        MonsterMidPattern midPattern = go.GetComponent<MonsterMidPattern>();
        if (midPattern != null)
        {
            midPattern.InitBattle();
        }
    }

    // 적 사망 시 재화를 지급하고 다음 적 스폰을 예약한다.
    public void HandleEnemyDied()
    {
        Debug.Log("[RoundManager.HandleEnemyDied] 호출됨");

        if (currentEnemy != null)
        {
            currentEnemy.OnDied -= HandleEnemyDied;
        }

        if (!IsNewBattleSystemActive() && MoneyManager.Instance != null)
        {
            MoneyManager.Instance.OnEnemyKilled();
        }

        currentEnemyIndex++;

        StartCoroutine(SpawnNextAfterDelay());
    }

    // 일정 지연 후 라운드 타입에 맞게 다음 적을 스폰하거나 라운드를 종료한다.
    IEnumerator SpawnNextAfterDelay(float delay = 1.5f)
    {
        yield return new WaitForSeconds(delay);

        if (currentRoundData is CombatRoundData combatData)
            SpawnNextEnemy(combatData.enemies, combatData.columnIndex, combatData.roundType);
        else if (currentRoundData is EliteRoundData eliteData)
            SpawnNextEnemy(eliteData.enemies, eliteData.columnIndex, eliteData.roundType);
        else if (currentRoundData is BossRoundData)
            EndRound();
    }
}

