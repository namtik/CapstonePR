using UnityEngine;

public class BattleManger : MonoBehaviour
{
    public GameObject playerPrefab;
    public GameObject enemyPrefab;

    public Transform playerParent;
    public Transform enemyParent;

    private bool battleInitialized = false;

    void OnEnable()
    {
        // 캔버스가 활성화될 때마다 전투 초기화
        if (!battleInitialized)
        {
            InitializeBattle();
        }
    }

    void InitializeBattle()
    {
        // 기존 플레이어/적 제거
        ClearBattleObjects();

        // 새로 생성
        if (playerPrefab != null && playerParent != null)
        {
            Instantiate(playerPrefab, playerParent);
        }

        if (enemyPrefab != null && enemyParent != null)
        {
            Instantiate(enemyPrefab, enemyParent);
        }

        battleInitialized = true;
        Debug.Log("전투 초기화 완료");
    }

    void ClearBattleObjects()
    {
        // 기존 Player/Enemy 제거
        var existingPlayers = FindObjectsByType<Player>(FindObjectsSortMode.None);
        foreach (var p in existingPlayers)
        {
            Destroy(p.gameObject);
        }

        var existingEnemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        foreach (var e in existingEnemies)
        {
            Destroy(e.gameObject);
        }
    }

    void Update()
    {
        if (battleInitialized)
        {
            CheckEnemyRespawn();
        }
    }

    private EnemyController currentEnemy;

    public void CheckEnemyRespawn()
    {
        if (currentEnemy == null || !currentEnemy.gameObject.activeInHierarchy)
        {
            if (enemyPrefab != null && enemyParent != null)
            {
                GameObject enemyObj = Instantiate(enemyPrefab, enemyParent);
                currentEnemy = enemyObj.GetComponent<EnemyController>();
            }
        }
    }

    // 전투 클리어 후 호출: 맵 화면으로 복귀
    public void OnBattleClear()
    {
        Debug.Log("=== BattleManger.OnBattleClear 호출됨 ===");

        // GameStateController를 통해 상태 전환 처리
        GameStateController stateController = GameStateController.Instance;
        if (stateController != null)
        {
            Debug.Log("GameStateController.OnRoundClear 호출 중...");
            battleInitialized = false; // 다음 전투를 위해 리셋
            stateController.OnRoundClear();
            return;
        }

        Debug.LogWarning("BattleManger: GameStateController not found. Cannot return to map.");
    }
}
