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
}
