using UnityEngine;

public class BattleManger : MonoBehaviour
{
    public GameObject playerPrefab;
    public GameObject enemyPrefab;

    public Transform playerParent;
    public Transform enemyParent;

    private bool battleInitialized = false;
    private Enemy cachedEnemy;

    void OnEnable()
    {
        if (!battleInitialized)
        {
            InitializeBattle();
        }
    }

    void InitializeBattle()
    {
        ClearBattleObjects();

        if (playerPrefab != null && playerParent != null)
        {
            Instantiate(playerPrefab, playerParent);
        }

        if (enemyPrefab != null && enemyParent != null)
        {
            GameObject enemyObj = Instantiate(enemyPrefab, enemyParent);
            cachedEnemy = enemyObj.GetComponent<Enemy>();
        }

        battleInitialized = true;
        Debug.Log("전투 초기화 완료");
    }

    void ClearBattleObjects()
    {
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
        
        cachedEnemy = null;
    }

    void Update()
    {
        Spawn();
    }

    public void Spawn()
    {
        if (cachedEnemy == null || cachedEnemy.gameObject == null)
        {
            if (enemyPrefab != null && enemyParent != null)
            {
                GameObject enemyObj = Instantiate(enemyPrefab, enemyParent);
                cachedEnemy = enemyObj.GetComponent<Enemy>();
            }
        }
    }

    public void OnBattleClear()
    {
        GameStateController stateController = GameStateController.Instance;
        if (stateController != null)
        {
            battleInitialized = false;
            stateController.OnBattleClear();
            return;
        }

        Debug.LogWarning("BattleManger: GameStateController not found. Cannot return to map.");
    }
}

