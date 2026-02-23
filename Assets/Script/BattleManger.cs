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
        // 각주: 마지막 방문한 노드를 클리어로 표시하고 맵으로 돌아감
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
