using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// MapGenerator를 사용하여 맵 생성 및 렌더링
public class MapManager : MonoBehaviour
{
    public GameObject nodePrefab;
    public Transform nodesParent;

    [Header("Path visuals")]
    public float lineThickness = 8f;
    public Color lineColor = new Color(0.8f, 0.8f, 0.8f, 1f);

    [Header("Player Position Highlight")]
    public Color currentPositionColor = new Color(0.3f, 0.6f, 1f, 1f);
    public float currentPositionScale = 1.3f;

    [Header("Map Scroll")]
    public MapScrollController scrollController;  // : 맵 스크롤 컨트롤러

    // : MapGenerator로 자동 생성된 맵 데이터 (런타임 전용)
    private MapData mapData;
    private MapGenerator mapGenerator;

    private List<MapNode> nodes = new List<MapNode>();
    private List<GameObject> pathLines = new List<GameObject>();
    private Sprite lineSprite;

    void Awake()
    {
        //  MapGenerator 컴포넌트 확보
        mapGenerator = GetComponent<MapGenerator>();
        if (mapGenerator == null)
        {
            mapGenerator = gameObject.AddComponent<MapGenerator>();
        }

        //  경로선용 스프라이트 준비
        if (lineSprite == null)
        {
            lineSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        }
    }

    void Start()
    {
        //  게임 시작 시 맵 생성
        GenerateMap();
    }

    // 맵 새로고침 (GameStateController에서 호출)
    //  맵 화면으로 돌아올 때마다 실행
    public void RefreshMap()
    {
        // 기존 맵이 없으면 생성
        if (nodes.Count == 0)
        {
            GenerateMap();
        }
        else
        {
            //  기존 맵이 있으면 상태만 업데이트
            UpdateNodeStates();
            UpdateNodeAvailability();
            HighlightCurrentPosition();
        }

        //  카메라를 현재 위치로 이동
        UpdateScrollPosition();
    }

    void UpdateNodeStates()
    {
        //  GameStateController에서 클리어 상태 복원
        var stateController = GameStateController.Instance;
        if (stateController == null) return;

        for (int i = 0; i < nodes.Count; i++)
        {
            nodes[i].SetCleared(stateController.IsNodeCleared(i));
        }
    }

    void UpdateNodeAvailability()
    {
        Debug.Log("=== UpdateNodeAvailability 시작 ===");
        
        //  모든 노드 비활성화
        for (int i = 0; i < nodes.Count; i++)
        {
            var btn = nodes[i].GetComponent<Button>();
            if (btn != null) btn.interactable = false;
        }

        //  GameStateController에서 현재 위치 확인
        var stateController = GameStateController.Instance;
        if (stateController == null)
        {
            Debug.LogError("UpdateNodeAvailability: GameStateController.Instance가 null입니다!");
            return;
        }

        int current = stateController.lastVisitedNodeIndex;
        Debug.Log($"현재 위치: {current}");

        //  MapData 없음 예외 처리
        if (mapData == null || mapData.nodes == null || mapData.nodes.Count == 0)
        {
            Debug.LogWarning("MapManager: No mapData available.");
            return;
        }

        //  게임 시작 전 - 시작 노드만 활성화
        if (current < 0)
        {
            Debug.Log($"게임 시작 전: 시작 노드 {mapData.startIndex} 활성화 시도");
            
            if (mapData.startIndex >= 0 && mapData.startIndex < nodes.Count)
            {
                var btn = nodes[mapData.startIndex].GetComponent<Button>();
                if (btn != null && !nodes[mapData.startIndex].isCleared)
                {
                    btn.interactable = true;
                    Debug.Log($"시작 노드 {mapData.startIndex} 활성화 완료!");
                }
                else
                {
                    Debug.LogWarning($"시작 노드 버튼이 null이거나 클리어됨: btn={btn != null}, cleared={nodes[mapData.startIndex].isCleared}");
                }
            }
            else
            {
                Debug.LogError($"시작 인덱스가 범위를 벗어남: {mapData.startIndex}, 노드 개수: {nodes.Count}");
            }
            return;
        }

        //  현재 노드의 연결된 다음 노드 활성화
        if (current >= 0 && current < mapData.nodes.Count)
        {
            var conns = mapData.nodes[current].connections;
            if (conns != null)
            {
                foreach (var targetIndex in conns)
                {
                    if (targetIndex >= 0 && targetIndex < nodes.Count && !nodes[targetIndex].isCleared)
                    {
                        var btn = nodes[targetIndex].GetComponent<Button>();
                        if (btn != null)
                        {
                            btn.interactable = true;
                            Debug.Log($"노드 {targetIndex} 활성화");
                        }
                    }
                }
            }
        }
    }

    void GenerateMap()
    {
        ClearNodes();

        //  MapGenerator로 랜덤 맵 생성
        if (mapGenerator != null)
        {
            Debug.Log("MapGenerator로 맵 자동 생성 중...");
            mapData = mapGenerator.GenerateMap();
            mapGenerator.PrintMapInfo();
        }

        //  MapData 확인
        if (mapData == null || mapData.nodes == null || mapData.nodes.Count == 0)
        {
            Debug.LogError("MapManager: No valid MapData. Cannot generate map.");
            return;
        }

        //  노드 생성
        for (int i = 0; i < mapData.nodes.Count; i++)
        {
            var entry = mapData.nodes[i];
            CreateNode(entry.anchoredPosition, i, entry.nodeType);
        }

        // 경로선 그리기
        for (int i = 0; i < mapData.nodes.Count; i++)
        {
            var entry = mapData.nodes[i];
            if (entry.connections == null) continue;
            foreach (var tgt in entry.connections)
            {
                if (tgt >= 0 && tgt < nodes.Count)
                {
                    CreateLineBetween(nodes[i].GetComponent<RectTransform>(), nodes[tgt].GetComponent<RectTransform>());
                }
            }
        }

        //  시작/보스 노드 강조
        if (mapData.startIndex >= 0 && mapData.startIndex < nodes.Count)
        {
            nodes[mapData.startIndex].Highlight(Color.green);
        }
        if (mapData.bossIndex >= 0 && mapData.bossIndex < nodes.Count)
        {
            nodes[mapData.bossIndex].Highlight(Color.yellow);
        }

        //  현재 플레이어 위치 하이라이트
        HighlightCurrentPosition();

        //  노드 활성화 상태 업데이트
        UpdateNodeAvailability();

        //  스크롤을 시작 노드로 즉시 이동
        UpdateScrollPosition(true);
    }

    // 스크롤을 현재 위치 노드로 이동
    //맵 복귀 시 현재 위치 추적
    void UpdateScrollPosition(bool snapImmediately = false)
    {
        if (scrollController == null)
        {
            //  MapScrollController 자동 찾기
            scrollController = Object.FindAnyObjectByType<MapScrollController>();
            if (scrollController == null)
            {
                Debug.LogWarning("MapManager: MapScrollController를 찾을 수 없습니다.");
                return;
            }
        }

        var stateController = GameStateController.Instance;
        if (stateController == null) return;

        int currentPos = stateController.lastVisitedNodeIndex;

        // 게임 시작 전이면 시작 노드로 이동
        if (currentPos < 0)
        {
            if (mapData != null && mapData.startIndex >= 0 && mapData.startIndex < nodes.Count)
            {
                RectTransform startNodeRect = nodes[mapData.startIndex].GetComponent<RectTransform>();
                
                if (snapImmediately)
                {
                    //  맵 생성 직후는 즉시 이동
                    scrollController.SnapToNode(startNodeRect);
                }
                else
                {
                    scrollController.ScrollToNode(startNodeRect);
                }
            }
            return;
        }

        //  현재 위치 노드로 이동
        if (currentPos >= 0 && currentPos < nodes.Count)
        {
            RectTransform currentNodeRect = nodes[currentPos].GetComponent<RectTransform>();
            
            if (snapImmediately)
            {
                scrollController.SnapToNode(currentNodeRect);
            }
            else
            {
                scrollController.ScrollToNode(currentNodeRect);
            }
        }
    }

    void HighlightCurrentPosition()
    {
        var stateController = GameStateController.Instance;
        if (stateController == null) return;

        int currentPos = stateController.lastVisitedNodeIndex;

        //  게임 시작 전이거나 유효하지 않은 인덱스면 스킵
        if (currentPos < 0 || currentPos >= nodes.Count)
        {
            return;
        }

        MapNode currentNode = nodes[currentPos];

        // 현재 위치 노드를 특별하게 표시
        currentNode.HighlightAsCurrentPosition(currentPositionColor, currentPositionScale);

        Debug.Log($"플레이어 현재 위치: 노드 {currentPos} ({currentNode.nodeType})");
    }

    void CreateLineBetween(RectTransform a, RectTransform b)
    {
        if (a == null || b == null) return;
        GameObject lineObj = new GameObject("PathLine");
        lineObj.transform.SetParent(nodesParent, false);

        Image img = lineObj.AddComponent<Image>();
        img.color = lineColor;
        if (lineSprite != null) img.sprite = lineSprite;
        img.raycastTarget = false;

        RectTransform rt = lineObj.GetComponent<RectTransform>();
        Vector2 dir = b.anchoredPosition - a.anchoredPosition;
        float len = dir.magnitude;
        rt.sizeDelta = new Vector2(len, lineThickness);
        rt.anchoredPosition = a.anchoredPosition + dir * 0.5f;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        rt.localRotation = Quaternion.Euler(0, 0, angle);

        lineObj.transform.SetSiblingIndex(0);

        pathLines.Add(lineObj);
    }

    void ClearNodes()
    {
        foreach (var n in nodes)
        {
            if (n != null) Destroy(n.gameObject);
        }
        nodes.Clear();

        foreach (var l in pathLines)
        {
            if (l != null) Destroy(l);
        }
        pathLines.Clear();
    }

    void CreateNode(Vector2 anchoredPos, int idx, NodeType type)
    {
        if (nodePrefab == null)
        {
            Debug.LogWarning("MapManager: nodePrefab is not assigned.");
            return;
        }

        GameObject go = Instantiate(nodePrefab, nodesParent);
        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = anchoredPos;
        }

        MapNode node = go.AddComponent<MapNode>();
        node.nodeIndex = idx;
        node.nodeType = type;
        node.mapManager = this;

        //  GameStateController의 클리어 상태 복원
        var stateController = GameStateController.Instance;
        if (stateController != null)
        {
            node.SetCleared(stateController.IsNodeCleared(idx));
        }

        //  버튼 이벤트 연결
        var btn = go.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(() => node.OnClicked());
            btn.interactable = false;
        }

        nodes.Add(node);
    }

    public void OnNodeSelected(MapNode node)
    {
        Debug.Log($"Node selected: {node.nodeIndex} type:{node.nodeType}");

        //  상호작용 가능 여부 체크
        var btn = node.GetComponent<Button>();
        if (btn == null || !btn.interactable)
        {
            Debug.Log("Node not interactable");
            return;
        }

        //  GameStateController에 현재 노드 기록
        var stateController = GameStateController.Instance;
        if (stateController == null)
        {
            Debug.LogError("MapManager: GameStateController not found!");
            return;
        }

        stateController.lastVisitedNodeIndex = node.nodeIndex;

        // 노드가 보스 노드인지 확인
        bool isBossNode = (mapData != null && node.nodeIndex == mapData.bossIndex);

        //  노드 타입에 따라 적절한 캔버스 표시
        stateController.ShowCanvasForNodeType(node.nodeType, isBossNode);

        //  RoundData 생성 및 라운드 시작
        StartRoundForNode(node, isBossNode);
    }

    // 노드 타입에 맞는 RoundData로 라운드 시작
    void StartRoundForNode(MapNode node, bool isBossNode)
    {
        var roundManager = Object.FindAnyObjectByType<Roundmanager>();
        if (roundManager == null)
        {
            Debug.LogWarning("MapManager: Roundmanager를 찾을 수 없습니다.");
            return;
        }

        //  노드 타입에 따라 RoundData 생성
        RoundData roundData = null;

        if (isBossNode)
        {
            //  보스 노드
            var bossData = ScriptableObject.CreateInstance<BossRoundData>();
            bossData.roundName = "Boss Round";
            roundData = bossData;
        }
        else
        {
            //  일반 노드
            switch (node.nodeType)
            {
                case NodeType.Combat:
                    var combatData = ScriptableObject.CreateInstance<CombatRoundData>();
                    combatData.roundName = "Combat Round";
                    roundData = combatData;
                    break;

                case NodeType.Elite:
                    var eliteData = ScriptableObject.CreateInstance<EliteRoundData>();
                    eliteData.roundName = "Elite Round";
                    roundData = eliteData;
                    break;

                case NodeType.Shop:
                    var shopData = ScriptableObject.CreateInstance<ShopRoundData>();
                    shopData.roundName = "Shop Round";
                    roundData = shopData;
                    break;

                case NodeType.Rest:
                    var restData = ScriptableObject.CreateInstance<RestRoundData>();
                    restData.roundName = "Rest Round";
                    roundData = restData;
                    break;
            }
        }

        if (roundData != null)
        {
            roundManager.StartRound(roundData);
            Debug.Log($"라운드 시작: {roundData.GetType().Name}");
        }
        else
        {
            Debug.LogError($"MapManager: 노드 타입 {node.nodeType}에 해당하는 RoundData를 생성할 수 없습니다!");
        }
    }
}
