using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using Battle.Relic;

public class Roundmanager : MonoBehaviour
{
    [SerializeField] private DifficultyConfig difficultyConfig;
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private Transform enemySpawnPoint;
    [SerializeField] private CombatStageController combatStageController;

    [Header("상태이상 UI 패널")]
    [SerializeField] private StatusPanelUI playerStatusPanel;
    [SerializeField] private StatusPanelUI enemyStatusPanel;

    [Header("보상 UI")]
    [SerializeField] private RewardHubUIController rewardHubUIController;

    [Header("보상 상단 문구 - 카드")]
    [SerializeField] private Font cardRewardTitleFont;
    [SerializeField, Range(12, 96)] private int cardRewardTitleFontSize = 40;
    [SerializeField] private Color cardRewardTitleColor = Color.white;
    [Tooltip("상단 문구 위치(화면 중앙 기준). 기본 (0, 320).")]
    [SerializeField] private Vector2 cardRewardTitlePosition = new Vector2(0f, 320f);

    [Header("보상 - 카드 선택하지 않기 버튼")]
    [SerializeField] private Battle.UI.RewardSkipButtonStyle cardRewardSkipButtonStyle = new Battle.UI.RewardSkipButtonStyle();

    [Header("보상 - 카드 크기/배치")]
    [Tooltip("제시 카드 배율(1 = 기본). 키우면 간격도 함께 올려 겹침 방지.")]
    [SerializeField, Range(0.3f, 2f)] private float cardRewardCardScale = 1f;
    [Tooltip("카드 사이 가로 간격(px). 기본 360.")]
    [SerializeField] private float cardRewardCardSpacing = 360f;

    private RoundData currentRoundData;
    private int currentEnemyIndex = 0;
    private EnemyStat currentEnemy;
    public event System.Action OnRoundClear;
    private IRoundHandler currentRoundHandler;
    private int clearedCombatCount = 0;


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

    public void EnsurePlayerUiSync()
    {
        EnsureRuntimePlayerExists();
    }

    /// <summary>새 전투 시스템(NewBattleController) 사용 중이면 기존 ElementSlot/Combo 자동 셋업을 건너뛴다.</summary>
    bool IsNewBattleSystemActive()
    {
        return Battle.NewBattleController.Instance != null;
    }

    void EnsureElementCombatSystems()
    {
        EnsureRuntimePlayerExists();

        // 새 전투 시스템 모드: 레거시 ElementSlot/HUD 자동 생성 차단
        if (IsNewBattleSystemActive())
            return;

        // Ensure slot system exists even when scene setup is missing.
        var slotSystem = ElementSlotSystem.Instance ?? FindFirstObjectByType<ElementSlotSystem>();
        if (slotSystem == null)
        {
            var go = new GameObject("ElementSlotSystem");
            slotSystem = go.AddComponent<ElementSlotSystem>();
            DontDestroyOnLoad(go);
        }

        // Ensure HUD exists.
        var hud = FindFirstObjectByType<ElementSlotHUD>();
        if (hud == null)
        {
            var hudGo = new GameObject("ElementSlotHUD");
            Canvas combatCanvas = ResolveCombatStageCanvas();
            if (combatCanvas != null)
                hudGo.transform.SetParent(combatCanvas.transform, false);
            hudGo.AddComponent<ElementSlotHUD>();
        }

        // Force legacy hand UI system off so only 4-slot HUD remains.
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

    public void StartRound(RoundData roundData)
    {
        EnsureElementCombatSystems();

        // 새 전투 시스템 모드에서는 starter 스킬 선택 UI를 건너뜀
        if (IsNewBattleSystemActive())
        {
            ContinueStartRound(roundData);
            return;
        }

        // 첫 스테이지 진입 시 스킬이 없으면 초기 스킬 선택 후 라운드 시작
        if (ComboSystem.Instance != null && ComboSystem.Instance.learnedSkills.Count == 0)
        {
            ComboSystem.Instance.LearnStarterSkill();

            // 스킬 선택 완료 후 라운드 진행
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

    private RoundData pendingRoundData;

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

    void ContinueStartRound(RoundData roundData)
    {
        currentRoundData = roundData;
        currentEnemyIndex = 0;

        // 전투 스테이지 배경 전환 + 덱/손패/콤보 초기화
        if (combatStageController != null)
        {
            combatStageController.Initialize(roundData);
        }

        currentRoundHandler = roundData.CreateHandler();
        currentRoundHandler.OnEnterRound(this);
    }

    public void EndRound()
    {
        if (!IsNewBattleSystemActive())
            ElementSlotSystem.Instance?.EndBattle();
        else
            Battle.NewBattleController.Instance?.EndBattle();

        currentRoundHandler.OnExitRound(this);
        OnRoundClear?.Invoke();
    }

    /// <summary>신규 전투 시스템: 노드 진입 시 런 덱을 주입하고 전투를 시작한다.</summary>
    void BeginNewBattleForNode()
    {
        var nb = Battle.NewBattleController.Instance;
        if (nb == null) return;

        var runDeck = Battle.RunDeckState.EnsureExists();
        runDeck.EnsureSeeded();
        nb.SetCustomStartingDeck(runDeck.InstantiateForBattle());
        nb.StartBattle();
    }

    /// <summary>
    /// 일반/정예 전투 시작 (CombatRoundHandler, EliteRoundHandler)
    /// </summary>
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

    /// <summary>
    /// 정예 전투 시작 (EliteRoundHandler)
    /// </summary>
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

    /// <summary>
    /// 보스 전투 시작 (BossRoundHandler)
    /// </summary>
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

    /// <summary>
    /// 상점 열기 (ShopRoundHandler)
    /// </summary>
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
            Debug.LogError("[Roundmanager] ShopStageController를 찾지 못해 맵으로 복귀합니다.");
            ReturnToMap();
            return;
        }

        controller.BeginShop(this);
        Debug.Log("[Roundmanager] 상점 스테이지 시작");
    }

    public void CloseShop()
    {
        ReturnToMap();
    }

    /// <summary>
    /// 이벤트 스테이지 시작 (EventRoundHandler)
    /// </summary>
    public void OpenEvent(EventRoundData data)
    {
        EventStageController controller = FindFirstObjectByType<EventStageController>(FindObjectsInactive.Include);
        if (controller == null)
        {
            Debug.LogError("[Roundmanager] EventStageController를 찾지 못해 맵으로 복귀합니다.");
            ReturnToMap();
            return;
        }

        controller.BeginEvent(data, this);
        Debug.Log("[Roundmanager] 이벤트 스테이지 시작");
    }

    /// <summary>
    /// 휴식 스테이지 시작 (RestRoundHandler)
    /// </summary>
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
            Debug.LogError("[Roundmanager] RestStageController를 찾지 못해 맵으로 복귀합니다.");
            ReturnToMap();
            return;
        }

        controller.BeginRest(data, this);
        Debug.Log("[Roundmanager] 휴식 스테이지 시작");
    }

    /// <summary>
    /// 유물 스테이지 시작 (RelicRoundHandler)
    /// </summary>
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
            Debug.LogWarning("[Roundmanager] RelicStageController를 찾지 못해 유물 스테이지를 진행할 수 없습니다. 맵으로 복귀합니다.");
            ReturnToMap();
            return;
        }

        controller.BeginRelic(data, this);
        Debug.Log("[Roundmanager] 유물 스테이지 시작");
    }

    /// <summary>
    /// Relic 스테이지 보상 처리: 후보가 있으면 후보에서, 없으면 전체 효과에서 랜덤 지급.
    /// </summary>
    public void GrantRelicFromStage(IReadOnlyList<RelicEffectType> candidates)
    {
        if (RelicManager.Instance == null)
        {
            Debug.LogWarning("[Roundmanager] RelicManager가 없어 유물 지급을 건너뜁니다.");
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
            Debug.LogWarning("[Roundmanager] 지급 가능한 유물 효과가 없습니다.");
            return;
        }

        var picked = pool[Random.Range(0, pool.Count)];
        RelicManager.Instance.GiveRelicByEffect(picked);
        Debug.Log($"[Roundmanager] RelicStage 보상 지급: {picked}");
    }

    /// <summary>
    /// 플레이어 HP 회복 (RestRoundHandler)
    /// </summary>
    public void HealPlayer(float healPercent)
    {
        var player = Player.Resolve(true);
        if (player == null) return;

        int healAmount = Mathf.Max(1, Mathf.RoundToInt(player.maxHp * healPercent));
        player.Heal(healAmount);

        Debug.Log($"플레이어 HP 회복: +{healAmount} ({healPercent * 100}%)");

        ReturnToMap();
    }

    /// <summary>
    /// 스킬 보상 UI 표시 (CombatRoundHandler, EliteRoundHandler)
    /// </summary>
    public void ShowSkillReward()
    {
        // 기획서 0.6v: 전투 등급별 골드 일괄 지급 (일반 30~50 / 정예 100~150 / 보스 200~250)
        GrantTieredCombatGold();

        // 신규 전투 시스템: 스킬 보상 대신 카드 획득 보상으로 분기
        if (IsNewBattleSystemActive())
        {
            ShowCardReward();
            return;
        }

        Debug.Log("스킬 보상 선택 UI 표시");

        if (rewardHubUIController == null)
            rewardHubUIController = FindFirstObjectByType<RewardHubUIController>(FindObjectsInactive.Include);

        Debug.Log($"[Roundmanager] rewardHubUIController={(rewardHubUIController != null ? rewardHubUIController.name : "NULL")}");

        if (rewardHubUIController != null)
        {
            Debug.Log("[Roundmanager] Opening RewardHubUIController.OpenHub()");
            rewardHubUIController.OpenHub();
            return;
        }

        if (SkillDataParser.Instance != null && SkillDataParser.Instance.SkillRewardUI != null)
        {
            Debug.Log("[Roundmanager] Fallback -> SkillRewardUI.ShowRewardOptions()");
            SkillDataParser.Instance.SkillRewardUI.ShowRewardOptions();
        }
        else
            Debug.LogError("[Roundmanager] RewardHubUIController와 SkillRewardUI가 모두 없습니다.");
    }


    /// <summary>
    /// 신규 시스템 카드 획득 보상 — 4장 중 1장을 런 덱에 추가.
    /// </summary>
    void ShowCardReward()
    {
        Debug.Log("[Roundmanager] 카드 획득 보상 표시");

        var hud = FindFirstObjectByType<Battle.UI.CardHandHUD>(FindObjectsInactive.Include);
        Battle.UI.NewCardView cardPrefab = hud != null ? hud.CardPrefab : null;

        var rewardUI = Battle.UI.CardRewardUI.EnsureExists();
        rewardUI.SetTitleStyle(cardRewardTitleFont, cardRewardTitleFontSize, cardRewardTitleColor, cardRewardTitlePosition);
        rewardUI.SetSkipButtonStyle(cardRewardSkipButtonStyle);
        rewardUI.SetCardLayout(cardRewardCardScale, cardRewardCardSpacing);
        rewardUI.Present(cardPrefab, pickedCardId =>
    {
        // pickedCardId가 0보다 클 때만 추가하므로, -1을 전달받으면 추가되지 않음
        if (pickedCardId > 0)
            Battle.RunDeckState.EnsureExists().AddCard(pickedCardId);

        // 이후 로직(콤보 보상 체크 등)은 동일하게 진행
        if (ShouldShowComboBookRewardAfterCard())
        {
            ShowComboBookRewardAfterCard();
            return;
        }

        ReturnToMap();
    });
}

    bool ShouldShowComboBookRewardAfterCard()
    {
        return currentRoundData is EliteRoundData || currentRoundData is BossRoundData;
    }

    void ShowComboBookRewardAfterCard()
    {
        var panel = FindFirstObjectByType<Battle.UI.ComboBookRewardPanelUI>(FindObjectsInactive.Include);
        if (panel == null)
        {
            Debug.LogWarning("[Roundmanager] ComboBookRewardPanelUI를 찾지 못해 콤보 보상을 생략하고 맵으로 복귀합니다.");
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
                    Debug.Log($"[Roundmanager] 콤보 보상 획득: refComboId={selected.refComboId}, name={selected.displayName}");
                }
                else
                {
                    Debug.LogWarning("[Roundmanager] NewBattleController.Instance가 없어 콤보 보상 지급을 건너뜁니다.");
                }
            }

            ReturnToMap();
        });

        Debug.Log("[Roundmanager] 카드 보상 후 책 UI 콤보 보상 표시");
    }

    /// <summary>기획서 0.6v: 전투 등급별 골드 일괄 지급. 비전투 라운드는 지급하지 않는다.</summary>
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

    /// <summary>
    /// 맵으로 복귀 (RestRoundHandler, 보상 선택 완료 후)
    /// </summary>
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
        
        // 현재 노드 클리어 처리
        stateController.MarkNodeCleared(stateController.lastVisitedNodeIndex);
        
        // 맵으로 복귀
        stateController.ShowMap();
    }

    /// <summary>
    /// 플레이어 사망 후 런 재시작(또는 강제 재시작) 시 호출 — 진행 중이던 전투를 정리한다.
    /// 적은 자기가 죽을 때만 스스로 파괴되므로, 플레이어 사망으로 전투가 중단되면
    /// 살아있는 적 GameObject(+HP/행동게이지/스프라이트/데미지 표시)가 그대로 남는다.
    /// 다음 전투 진입 시 새 적과 겹쳐 보이는 것을 막기 위해 여기서 명시적으로 제거한다.
    /// </summary>
    public void AbortActiveCombat()
    {
        // 진행 중이던 전투 종료(덱/손패/이펙트 잔여물 정리)
        if (IsNewBattleSystemActive())
            Battle.NewBattleController.Instance?.EndBattle();
        else
            ElementSlotSystem.Instance?.EndBattle();

        // 적 사망 이벤트 구독 해지
        if (currentEnemy != null)
        {
            currentEnemy.OnDied -= HandleEnemyDied;
            currentEnemy = null;
        }

        // 진행 중이던 스폰 대기 코루틴(SpawnNextAfterDelay 등) 취소
        StopAllCoroutines();

        // 스폰 지점에 남아있는 모든 적 제거(자가 파괴되지 않은 적)
        if (enemySpawnPoint != null)
        {
            for (int i = enemySpawnPoint.childCount - 1; i >= 0; i--)
                Destroy(enemySpawnPoint.GetChild(i).gameObject);
        }

        currentEnemyIndex = 0;
    }

    // ── 내부 몬스터 스폰 ───

    void SpawnNextEnemy(List<EnemyData> enemies, int columnIndex, NodeType nodeType)
    {
        if (currentEnemyIndex >= enemies.Count)
        {
            // 모든 몬스터 처치 → 라운드 종료
            EndRound();
            return;
        }

        SpawnEnemy(enemies[currentEnemyIndex], columnIndex, nodeType);
    }

    void SpawnEnemy(EnemyData data, int columnIndex, NodeType nodeType)
    {
        if (enemyPrefab == null)
        {
            Debug.LogError("RoundManager: enemyPrefab이 없습니다.");
            return;
        }

        GameObject go = Instantiate(enemyPrefab, enemySpawnPoint);

        // 적을 중앙에 배치
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
            // 상태 패널이 적 피격 흔들림을 함께 따라가도록 흔들림 대상 연결
            if (view != null) enemyStatusPanel.SetFollowTarget(view.ShakeTarget);
        }
        Debug.Log($"Initialize 호출: HP={data.maxHp}, col={columnIndex}");
        
        if (view != null && data.enemySprite != null)
            view.SetSprite(data.enemySprite);

        // 피격 당한 이미지 주입(EnemyData.hitSprite). 비어 있으면 교체 없이 기존 연출만 적용.
        if (view != null)
            view.SetHitSprite(data.hitSprite);

        // 몬스터 종류에 맞는 전투 배경 적용(적별 배경 풀에서 랜덤). 풀이 비면 라운드 타입 기본 배경 유지.
        if (combatStageController != null)
            combatStageController.ApplyEnemyBackground(data);

        stat.Initialize(data, columnIndex, nodeType, difficultyConfig);

        // 이전 적 구독 해지 (중복 구독 방지)
        if (currentEnemy != null)
        {
            currentEnemy.OnDied -= HandleEnemyDied;
        }

        // 사망 이벤트 구독
        stat.OnDied += HandleEnemyDied;
        currentEnemy = stat;
        MonsterMidPattern midPattern = go.GetComponent<MonsterMidPattern>();
        if (midPattern != null)
        {
            midPattern.InitBattle();
        }
    }

    public void HandleEnemyDied()
    {
        Debug.Log("[RoundManager.HandleEnemyDied] 호출됨");
        
        // 구독 해지
        if (currentEnemy != null)
        {
            currentEnemy.OnDied -= HandleEnemyDied;
        }

        // 재화 지급 — 신규 시스템은 전투 종료 시 등급별 골드로 일괄 지급(기획서 0.6v)하므로 처치당 지급 X
        if (!IsNewBattleSystemActive() && MoneyManager.Instance != null)
        {
            MoneyManager.Instance.OnEnemyKilled();
        }

        currentEnemyIndex++;

        // 다음 몬스터 스폰 (딜레이)
        StartCoroutine(SpawnNextAfterDelay());
    }

    IEnumerator SpawnNextAfterDelay(float delay = 1.5f)
    {
        yield return new WaitForSeconds(delay);

        if (currentRoundData is CombatRoundData combatData)
            SpawnNextEnemy(combatData.enemies, combatData.columnIndex, combatData.roundType);
        else if (currentRoundData is EliteRoundData eliteData)
            SpawnNextEnemy(eliteData.enemies, eliteData.columnIndex, eliteData.roundType);
    }
}

