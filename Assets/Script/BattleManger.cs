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
        // 각주: 캔버스가 활성화될 때마다 전투 초기화
        if (!battleInitialized)
        {
            InitializeBattle();
        }
    }

    void InitializeBattle()
    {
        // 각주: 기존 플레이어/적 제거
        ClearBattleObjects();

        // 각주: 새로 생성
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
        // 각주: 기존 Player/Enemy 제거
        var existingPlayers = Object.FindObjectsOfType<Player>();
        foreach (var p in existingPlayers)
        {
            Destroy(p.gameObject);
        }

        var existingEnemies = Object.FindObjectsOfType<Enemy>();
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

    private Enemy currentEnemy;

    public void CheckEnemyRespawn()
    {
        if (currentEnemy == null || !currentEnemy.gameObject.activeInHierarchy)
        {
            if (enemyPrefab != null && enemyParent != null)
            {
                GameObject enemyObj = Instantiate(enemyPrefab, enemyParent);
                currentEnemy = enemyObj.GetComponent<Enemy>();
            }
        }
    }

    // 전투 클리어 후 호출: 맵 화면으로 복귀
    public void OnBattleClear()
    {
        //GameStateController를 통해 맵으로 복귀
        GameStateController stateController = Object.FindAnyObjectByType<GameStateController>();
        if (stateController != null)
        {
            battleInitialized = false; //다음 전투를 위해 리셋
            stateController.OnBattleClear();
            return;
        }

        Debug.LogWarning("BattleManger: GameStateController not found. Cannot return to map.");
    }
}

