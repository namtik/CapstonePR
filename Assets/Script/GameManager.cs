using UnityEngine;

// 게임 전역 상태를 담당하는 싱글턴
// (씬 전환 기능은 GameStateController로 이관됨)
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // 선택한/방문한 노드 인덱스 저장용
    public int lastVisitedNodeIndex = -1;
    // Cleared node indices (persisted in this singleton during play)
    public System.Collections.Generic.List<int> clearedNodes = new System.Collections.Generic.List<int>();

    public void MarkNodeCleared(int index)
    {
        if (index < 0) return;
        if (!clearedNodes.Contains(index)) clearedNodes.Add(index);
        
        // GameStateController와 동기화
        if (GameStateController.Instance != null)
        {
            GameStateController.Instance.MarkNodeCleared(index);
        }
        
        Debug.Log($"GameManager: 노드 {index} 클리어 마킹");
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
