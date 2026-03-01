using UnityEditor.SceneManagement;
using UnityEngine;

// 게임 상태를 관리하고 캔버스 전환을 담당
//  씬 전환 대신 캔버스 활성화/비활성화로 상태 전환
public class GameStateController : MonoBehaviour
{
    public static GameStateController Instance { get; private set; }

    [Header("Canvas References")]
    public Canvas mapCanvas;
    public Canvas combatCanvas;  //  일반 전투 (NodeType.Combat)
    public Canvas eliteCanvas;   //  정예 전투 (NodeType.Elite)
    public Canvas bossCanvas;    //  보스 전투 (보스 노드)
    public Canvas shopCanvas;    //  상점 (NodeType.Shop)
    public Canvas restCanvas;    //  휴식 (NodeType.Rest)

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
        // 게임 시작 시 맵 화면 표시
        ShowMap();
    }

    // 맵 화면으로 전환
    // 전투 캔버스 숨기고 맵 캔버스 표시

    public void ShowMap()
    {
        Debug.Log("맵 화면으로 전환");

        //  모든 캔버스 비활성화
        HideAllCanvases();

        //  맵 캔버스만 활성화
        if (mapCanvas != null) mapCanvas.gameObject.SetActive(true);

        //  맵이 없으면 생성
        if (mapManager != null)
        {
            mapManager.RefreshMap();
        }
    }

    // 노드 타입에 따라 적절한 캔버스 표시
    // MapManager.OnNodeSelected()에서 호출됨
    public void ShowCanvasForNodeType(NodeType nodeType, bool isBossNode)
    {
        Debug.Log($"노드 타입 {nodeType}에 맞는 캔버스 표시 (보스: {isBossNode})");

        //  모든 캔버스 비활성화
        HideAllCanvases();

        //  노드 타입에 따라 캔버스 활성화
        Canvas targetCanvas = null;

        if (isBossNode && bossCanvas != null)
        {
            //  보스 노드는 bossCanvas 사용
            targetCanvas = bossCanvas;
        }
        else
        {
            //  일반 노드는 타입별 캔버스 사용
            switch (nodeType)
            {
                case NodeType.Combat:
                    targetCanvas = combatCanvas;
                    break;
                case NodeType.Elite:
                    targetCanvas = eliteCanvas;
                    break;
                case NodeType.Shop:
                    targetCanvas = shopCanvas;
                    break;
                case NodeType.Rest:
                    targetCanvas = restCanvas;
                    break;
            }
        }

        if (targetCanvas != null)
        {
            targetCanvas.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning($"노드 타입 {nodeType}에 해당하는 캔버스가 없습니다!");
        }
    }

    // 모든 캔버스 비활성화
    void HideAllCanvases()
    {
        if (mapCanvas != null) mapCanvas.gameObject.SetActive(false);
        if (combatCanvas != null) combatCanvas.gameObject.SetActive(false);
        if (eliteCanvas != null) eliteCanvas.gameObject.SetActive(false);
        if (bossCanvas != null) bossCanvas.gameObject.SetActive(false);
        if (shopCanvas != null) shopCanvas.gameObject.SetActive(false);
        if (restCanvas != null) restCanvas.gameObject.SetActive(false);
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
            Debug.Log($"노드 {index} 클리어됨");
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