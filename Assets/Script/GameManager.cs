using UnityEngine;

// ���� ���� ���¸� ����ϴ� �̱���
// (�� ��ȯ ����� GameStateController�� �̰���)
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // ������/�湮�� ��� �ε��� �����
    public int lastVisitedNodeIndex = -1;
    // Cleared node indices (persisted in this singleton during play)
    public System.Collections.Generic.List<int> clearedNodes = new System.Collections.Generic.List<int>();

    // 스테이지 간 HP 유지를 위한 영구 저장값
    // -1이면 아직 초기화 전(처음 시작)을 의미
    public int persistedCurrentHp = -1;
    public int persistedMaxHp = 100;

    public void SavePlayerHp(int currentHp, int maxHp)
    {
        persistedCurrentHp = currentHp;
        persistedMaxHp = maxHp;
    }

    public void MarkNodeCleared(int index)
    {
        if (index < 0) return;
        if (!clearedNodes.Contains(index)) clearedNodes.Add(index);
        
        // GameStateController�� ����ȭ
        if (GameStateController.Instance != null)
        {
            GameStateController.Instance.MarkNodeCleared(index);
        }
        
        Debug.Log($"GameManager: ��� {index} Ŭ���� ��ŷ");
    }

    public bool IsNodeCleared(int index)
    {
        return index >= 0 && clearedNodes.Contains(index);
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}

