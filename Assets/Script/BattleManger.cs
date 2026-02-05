using UnityEngine;

public class BattleManger : MonoBehaviour
{
    public GameObject playerPrefab;
    public GameObject enemyPrefab;

    public Transform playerParent; // Canvas/PlayerUnit 위치
    public Transform enemyParent;  // Canvas/EnemyUnit 위치

    void Start()
    {
        Instantiate(playerPrefab, playerParent);
        Instantiate(enemyPrefab, enemyParent);
    }
}
