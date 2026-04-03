using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework.Interfaces;

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

    private RoundData currentRoundData;
    private int currentEnemyIndex = 0;
    private EnemyStat currentEnemy;
    public event System.Action OnRoundClear;
    private IRoundHandler currentRoundHandler;
    private int clearedCombatCount = 0;


    void EnsureRuntimePlayerExists()
    {
        Player existingPlayer = FindFirstObjectByType<Player>();
        if (existingPlayer != null)
        {
            existingPlayer.UpdateUIForExternalSync();

            if (playerStatusPanel != null) playerStatusPanel.SetTarget(existingPlayer);
            return;
        }

        GameObject playerObject = new GameObject("PlayerLogic");
        Player newPlayer = playerObject.AddComponent<Player>();
        DontDestroyOnLoad(playerObject);

        Debug.Log("[RoundManager] Hidden runtime Player created");

        if (playerStatusPanel != null) playerStatusPanel.SetTarget(newPlayer);

        Debug.Log("[RoundManager] Hidden runtime Player created");
    }

    public void EnsurePlayerUiSync()
    {
        EnsureRuntimePlayerExists();
    }

    void EnsureElementCombatSystems()
    {
        EnsureRuntimePlayerExists();

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
        ElementSlotSystem.Instance?.EndBattle();

        currentRoundHandler.OnExitRound(this);
        OnRoundClear?.Invoke();
    }

    /// <summary>
    /// 일반/정예 전투 시작 (CombatRoundHandler, EliteRoundHandler)
    /// </summary>
    public void StartCombat(CombatRoundData data)
    {
        EnsureElementCombatSystems();
        ElementSlotSystem.Instance?.StartBattle();

        Player player = FindFirstObjectByType<Player>();
        if (player != null) player.ResetStatusForNewBattle();

        currentEnemyIndex = 0;
        SpawnNextEnemy(data.enemies, data.columnIndex, data.roundType);
    }

    /// <summary>
    /// 정예 전투 시작 (EliteRoundHandler)
    /// </summary>
    public void StartCombat(EliteRoundData data)
    {
        EnsureElementCombatSystems();
        ElementSlotSystem.Instance?.StartBattle();

        Player player = FindFirstObjectByType<Player>();
        if (player != null) player.ResetStatusForNewBattle();

        currentEnemyIndex = 0;
        SpawnNextEnemy(data.enemies, data.columnIndex, data.roundType);
    }

    /// <summary>
    /// 보스 전투 시작 (BossRoundHandler)
    /// </summary>
    public void StartBoss(BossRoundData data)
    {
        EnsureElementCombatSystems();
        ElementSlotSystem.Instance?.StartBattle();

        Player player = FindFirstObjectByType<Player>();
        if (player != null) player.ResetStatusForNewBattle();

        SpawnEnemy(data.bossEnemy, data.columnIndex, NodeType.Boss);
    }

    /// <summary>
    /// 상점 열기 (ShopRoundHandler)
    /// </summary>
    public void OpenShop()
    {
        Debug.Log($"상점 오픈: 아이템");
    }

    public void CloseShop()
    {
        ReturnToMap();
    }

    /// <summary>
    /// 플레이어 HP 회복 (RestRoundHandler)
    /// </summary>
    public void HealPlayer(float healPercent)
    {
        var player = FindFirstObjectByType<Player>();
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
    /// 맵으로 복귀 (RestRoundHandler, 보상 선택 완료 후)
    /// </summary>
    public void ReturnToMap()
    {
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
        }
        Debug.Log($"Initialize 호출: HP={data.maxHp}, col={columnIndex}");
        
        if (view != null && data.enemySprite != null)
            view.SetSprite(data.enemySprite);

        stat.Initialize(data, columnIndex, nodeType, difficultyConfig);

        // 이전 적 구독 해지 (중복 구독 방지)
        if (currentEnemy != null)
        {
            currentEnemy.OnDied -= HandleEnemyDied;
        }

        // 사망 이벤트 구독
        stat.OnDied += HandleEnemyDied;
        currentEnemy = stat;
    }

    public void HandleEnemyDied()
    {
        Debug.Log("[RoundManager.HandleEnemyDied] 호출됨");
        
        // 구독 해지
        if (currentEnemy != null)
        {
            currentEnemy.OnDied -= HandleEnemyDied;
        }

        // 재화 지급
        if (MoneyManager.Instance != null)
        {
            Debug.Log("[RoundManager] MoneyManager.Instance 있음 - OnEnemyKilled 호출");
            MoneyManager.Instance.OnEnemyKilled();
        }
        else
        {
            Debug.LogError("[RoundManager] MoneyManager.Instance가 NULL입니다!");
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

