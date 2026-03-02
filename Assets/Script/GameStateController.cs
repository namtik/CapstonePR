using UnityEngine;

// 게임 상태를 관리하고 캔버스 전환을 담당
//  씬 전환 대신 캔버스 활성화/비활성화로 상태 전환
public class GameStateController : MonoBehaviour
{
    public static GameStateController Instance { get; private set; }

    [Header("Canvas References")]
    public Canvas mapCanvas;
    
    [Header("Stage GameObjects")]
    public GameObject mapStage;          // MapStage GameObject
    public GameObject combatStage;       // CombatStage GameObject
    public GameObject eliteStage;        // EliteStage GameObject (있다면)
    public GameObject bossStage;         // BossStage GameObject (있다면)
    public GameObject shopStage;         // ShopStage GameObject (있다면)
    public GameObject restStage;         // RestStage GameObject (있다면)

    [Header("Managers")]
    public MapManager mapManager;
    public BattleManger battleManager;
    public Roundmanager roundManager;  // 라운드 관리자 추가

    [Header("Game State")]
    public int lastVisitedNodeIndex = -1;
    public System.Collections.Generic.List<int> clearedNodes = new System.Collections.Generic.List<int>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        // 게임 시작 시 초기화 및 맵 화면 표시
        InitializeGameState();
        ShowMap();
    }

    void InitializeGameState()
    {
        // GameManager와 동기화
        if (GameManager.Instance != null)
        {
            lastVisitedNodeIndex = GameManager.Instance.lastVisitedNodeIndex;
            clearedNodes = new System.Collections.Generic.List<int>(GameManager.Instance.clearedNodes);
        }
        
        // Panel raycastTarget 비활성화
        EnsureGraphicRaycaster();
    }
    
    void EnsureGraphicRaycaster()
    {
        if (mapCanvas != null)
        {
            var raycaster = mapCanvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            if (raycaster == null)
            {
                mapCanvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }
            
            // Panel이나 background 이미지가 클릭을 막지 않도록 설정
            DisablePanelRaycast();
        }
    }
    
    void DisablePanelRaycast()
    {
        if (mapCanvas == null) return;
        
        // Canvas 하위의 모든 Image 중 Panel, Background 등의 raycastTarget 비활성화
        var allImages = mapCanvas.GetComponentsInChildren<UnityEngine.UI.Image>(true);
        foreach (var img in allImages)
        {
            if (img.gameObject.name.ToLower().Contains("panel") || 
                img.gameObject.name.ToLower().Contains("background"))
            {
                img.raycastTarget = false;
            }
        }
    }

    // 맵 화면으로 전환
    // 전투 캔버스 숨기고 맵 캔버스 표시
    public void ShowMap()
    {
        //  모든 스테이지 비활성화
        HideAllStages();

        //  맵 스테이지만 활성화
        if (mapStage != null)
        {
            mapStage.SetActive(true);
        }
        else if (mapCanvas != null)
        {
            // 하위 호환: mapStage가 없으면 mapCanvas 사용
            mapCanvas.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogError("mapStage와 mapCanvas 둘 다 null입니다!");
        }

        //  맵을 새로고침 (약간의 지연으로 MapManager 초기화 완료 대기)
        if (mapManager != null)
        {
            StartCoroutine(RefreshMapDelayed());
        }
        else
        {
            Debug.LogError("mapManager가 null입니다!");
        }
    }

    System.Collections.IEnumerator RefreshMapDelayed()
    {
        // 한 프레임 대기하여 MapManager.Start() 완료 보장
        yield return null;
        mapManager.RefreshMap();
    }

    // 노드 타입에 따라 적절한 스테이지 표시
    // MapManager.OnNodeSelected()에서 호출됨
    public void ShowCanvasForNodeType(NodeType nodeType, bool isBossNode)
    {
        //  모든 스테이지 비활성화
        HideAllStages();

        //  노드 타입에 따라 스테이지 활성화
        GameObject targetStage = null;

        if (isBossNode && bossStage != null)
        {
            //  보스 노드는 bossStage 사용
            targetStage = bossStage;
        }
        else
        {
            //  일반 노드는 타입별 스테이지 사용
            switch (nodeType)
            {
                case NodeType.Combat:
                    targetStage = combatStage;
                    break;
                case NodeType.Elite:
                    targetStage = eliteStage;
                    break;
                case NodeType.Shop:
                    targetStage = shopStage;
                    break;
                case NodeType.Rest:
                    targetStage = restStage;
                    break;
            }
        }

        if (targetStage != null)
        {
            targetStage.SetActive(true);
        }
        else
        {
            Debug.LogWarning($"노드 타입 {nodeType}에 해당하는 스테이지가 없습니다!");
        }
    }

    // 모든 스테이지 비활성화
    void HideAllStages()
    {
        if (mapStage != null)
        {
            mapStage.SetActive(false);
        }
        if (combatStage != null)
        {
            combatStage.SetActive(false);
        }
        if (eliteStage != null) eliteStage.SetActive(false);
        if (bossStage != null) bossStage.SetActive(false);
        if (shopStage != null) shopStage.SetActive(false);
        if (restStage != null) restStage.SetActive(false);
    }

    // 전투 화면으로 전환
    [System.Obsolete("Use ShowCanvasForNodeType instead")]
    public void ShowBattle()
    {
        ShowCanvasForNodeType(NodeType.Combat, false);
    }

    // 노드 클리어 처리
    //GameManager 역할을 대신함
    public void MarkNodeCleared(int index)
    {
        if (index < 0) return;
        if (!clearedNodes.Contains(index))
        {
            clearedNodes.Add(index);
        }
    }

    // 노드가 클리어되었는지 확인
    public bool IsNodeCleared(int index)
    {
        return index >= 0 && clearedNodes.Contains(index);
    }

    // 전투 클리어 후 맵으로 복귀
    // BattleManger.OnBattleClear()에서 호출됨
    public void OnBattleClear()
    {
        // 현재 노드 클리어 처리
        if (lastVisitedNodeIndex >= 0)
        {
            MarkNodeCleared(lastVisitedNodeIndex);
        }

        // 맵으로 복귀
        ShowMap();
    }

}