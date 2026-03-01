using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework.Interfaces;

public class Roundmanager : MonoBehaviour
{
    [SerializeField] private DifficultyConfig difficultyConfig;
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private Transform enemySpawnPoint;
    
    private RoundData currentRoundData;
    private int currentEnemyIndex = 0;
    private EnemyStat currentEnemy;
    public event System.Action OnRoundClear;
    private IRoundHandler currentRoundHandler;

    public void StartRound(RoundData roundData)
    {
        currentRoundData = roundData;
        currentEnemyIndex = 0;
        currentRoundHandler = roundData.CreateHandler();
        currentRoundHandler.OnEnterRound(this);
    }

    public void EndRound()
    {

        currentRoundHandler.OnExitRound(this);
        OnRoundClear?.Invoke();
    }

    /// <summary>
    /// 일반/정예 전투 시작 (CombatRoundHandler, EliteRoundHandler)
    /// </summary>
    public void StartCombat(CombatRoundData data)
    {
        currentEnemyIndex = 0;
        SpawnNextEnemy(data.enemies, data.columnIndex, data.roundType);
    }

    /// <summary>
    /// 보스 전투 시작 (BossRoundHandler)
    /// </summary>
    public void StartBoss(BossRoundData data)
    {
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
        var player = Object.FindFirstObjectByType<Player>();
        if (player == null) return;

        float healAmount = player.maxHp * healPercent;
        player.currentHp = Mathf.Min(player.currentHp + healAmount, player.maxHp);

        Debug.Log($"플레이어 HP 회복: +{healAmount} ({healPercent * 100}%)");

        ReturnToMap();
    }

    /// <summary>
    /// 스킬 보상 UI 표시 (CombatRoundHandler, EliteRoundHandler)
    /// </summary>
    public void ShowSkillReward()
    {
        Debug.Log("스킬 보상 선택 UI 표시");
        SkillDataParser.Instance.SkillRewardUI.ShowRewardOptions();
    }

    /// <summary>
    /// 보장 보상 표시 (EliteRoundHandler)
    /// </summary>


    /// <summary>
    /// 맵으로 복귀 (RestRoundHandler, 보상 선택 완료 후)
    /// </summary>
    public void ReturnToMap()
    {
        GameManager.Instance.MarkNodeCleared(GameManager.Instance.lastVisitedNodeIndex);
        GameStateController.Instance.ShowMap();
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

        EnemyStat stat = go.GetComponent<EnemyStat>();
        if (stat == null)
        {
            Debug.LogError("RoundManager: enemyPrefab에 EnemyStat이 없습니다.");
            return;
        }

        stat.Initialize(data, columnIndex, nodeType, difficultyConfig);

        // 사망 이벤트 구독
        stat.OnDied += HandleEnemyDied;
        currentEnemy = stat;
    }

    public void HandleEnemyDied()
    {
        // 구독 해지
        if (currentEnemy != null)
            currentEnemy.OnDied -= HandleEnemyDied;

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

