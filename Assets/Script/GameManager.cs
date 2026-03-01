using UnityEngine;
using UnityEngine.SceneManagement;

// 게임 전역 상태와 씬 전환을 담당하는 싱글턴
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // 씬 이름은 인스펙터에서 설정하세요.
    public string mapSceneName = "MapScene";
    public string battleSceneName = "SampleScene"; // 전투 씬 이름(현재 SampleScene 등으로 변경)

    // 선택한/방문한 노드 인덱스 저장용
    public int lastVisitedNodeIndex = -1;
    // Cleared node indices (persisted in this singleton during play)
    public System.Collections.Generic.List<int> clearedNodes = new System.Collections.Generic.List<int>();

    public void MarkNodeCleared(int index)
    {
        if (index < 0) return;
        if (!clearedNodes.Contains(index)) clearedNodes.Add(index);
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

    public void LoadMapScene()
    {
        if (string.IsNullOrEmpty(mapSceneName))
        {
            Debug.LogWarning("GameManager: mapSceneName is empty. Cannot load map scene.");
            return;
        }
        SceneManager.LoadScene(mapSceneName);
    }

    public void LoadStage()
    {
        
    }

    public void LoadBattleScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("GameManager: provided sceneName is empty.");
            return;
        }
        battleSceneName = sceneName;
        SceneManager.LoadScene(battleSceneName);
    }
}
