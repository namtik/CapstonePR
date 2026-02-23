using UnityEngine;

public class BattleManger : MonoBehaviour
{
    public GameObject playerPrefab;
    public GameObject enemyPrefab;

    public Transform playerParent; // Player 위치
    public Transform enemyParent;  // Enemy 위치

    void Start()
    {
        Instantiate(playerPrefab, playerParent);
        Instantiate(enemyPrefab, enemyParent);
    }

    void Update()
    {
        Spawn();
    }

    public void Spawn()
    {
        if (Object.FindAnyObjectByType<Enemy>() == null)
        {
            Instantiate(enemyPrefab, enemyParent);
        }
    }

    // 전투 클리어 시 호출: 맵 씬으로 돌아감
    public void OnBattleClear()
    {
        // Mark last visited node as cleared (if set) and return to map
        GameManager gm = Object.FindFirstObjectByType<GameManager>();
        if (gm != null)
        {
            if (gm.lastVisitedNodeIndex >= 0)
            {
                gm.MarkNodeCleared(gm.lastVisitedNodeIndex);
            }
            gm.LoadMapScene();
            return;
        }

        Debug.LogWarning("BattleManger: GameManager not found. Cannot return to map.");
    }
}
