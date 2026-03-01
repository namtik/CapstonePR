using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 횡스크롤 로그라이크 맵 시스템 매니저
// 각주: MapGenerator와 연동하여 맵 생성 및 렌더링
public class MapManager : MonoBehaviour
{
    public GameObject nodePrefab;
    public Transform nodesParent;

    [Header("Path visuals")]
    public float lineThickness = 8f;
    public Color lineColor = new Color(0.8f, 0.8f, 0.8f, 1f);

    [Header("Player Position Highlight")]
    public Color currentPositionColor = new Color(0.3f, 0.6f, 1f, 1f); // 각주: 청색 - 현재 위치
    public float currentPositionScale = 1.3f; // 각주: 현재 위치 노드 크기 (1.3배)

    [Header("Map graph")]
    public MapData mapData;

    [Header("맵 생성기")]
    public bool useMapGenerator = true; // 각주: true면 자동 생성, false면 mapData 사용
    private MapGenerator mapGenerator;
    private GameManager cachedGameManager; // 각주: 성능 최적화용 캐시

    private List<MapNode> nodes = new List<MapNode>();
    private List<GameObject> pathLines = new List<GameObject>();
    private Sprite lineSprite;

    void Start()
    {
        // 각주: GameManager 캐싱
        cachedGameManager = GameManager.Instance ?? Object.FindFirstObjectByType<GameManager>();

        // 각주: MapGenerator 컴포넌트 확보
        if (useMapGenerator)
        {
            mapGenerator = GetComponent<MapGenerator>();
            if (mapGenerator == null)
            {
                mapGenerator = gameObject.AddComponent<MapGenerator>();
            }
        }

        // 각주: 경로선용 스프라이트 준비
        if (lineSprite == null)
        {
            lineSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        }

        // 각주: nodesParent 자동 생성
        if (nodesParent == null)
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                GameObject parentObj = new GameObject("MapNodes");
                parentObj.transform.SetParent(canvas.transform, false);
                RectTransform rt = parentObj.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(2800f, 1000f); // 각주: 횡스크롤용 넓은 크기
                nodesParent = parentObj.transform;
            }
            else
            {
                Debug.LogWarning("MapManager: Canvas not found. Please add a Canvas to the scene.");
                return;
            }
        }

        // 각주: 맵 생성 (1회만 호출)
        GenerateSimpleMap();
    }

    void UpdateNodeAvailability()
    {
        // default: all not interactable
        for (int i = 0; i < nodes.Count; i++)
        {
            var btn = nodes[i].GetComponent<UnityEngine.UI.Button>();
            if (btn != null) btn.interactable = false;
        }

        // 각주: 캐시된 GameManager 사용
        if (cachedGameManager == null)
        {
            cachedGameManager = GameManager.Instance ?? Object.FindFirstObjectByType<GameManager>();
        }
        int current = cachedGameManager != null ? cachedGameManager.lastVisitedNodeIndex : -1;

        // 각주: MapData 기반 연결만 사용 (구 시스템 fallback 제거)
        if (mapData == null || mapData.nodes == null || mapData.nodes.Count == 0)
        {
            Debug.LogWarning("MapManager: No mapData available. Cannot update node availability.");
            return;
        }

        // 각주: 게임 시작 시 - 시작 노드만 활성화
        if (current < 0)
        {
            if (mapData.startIndex >= 0 && mapData.startIndex < nodes.Count)
            {
                var btn = nodes[mapData.startIndex].GetComponent<Button>();
                if (btn != null && !nodes[mapData.startIndex].isCleared)
                {
                    btn.interactable = true;
                }
            }
            return;
        }

        // 각주: 현재 노드의 연결된 다음 노드들 활성화
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
                        }
                    }
                }
            }
        }
    }

    void GenerateSimpleMap()
    {
        ClearNodes();

        // 각주: 새 맵 생성 시스템 사용
        if (useMapGenerator && mapGenerator != null)
        {
            Debug.Log("MapGenerator로 새 맵 생성 중...");
            mapData = mapGenerator.GenerateMap();
            mapGenerator.PrintMapInfo();
        }

        // 각주: MapData 확인
        if (mapData == null || mapData.nodes == null || mapData.nodes.Count == 0)
        {
            Debug.LogError("MapManager: No valid MapData. Cannot generate map.");
            return;
        }

        // 각주: 노드 생성
        for (int i = 0; i < mapData.nodes.Count; i++)
        {
            var entry = mapData.nodes[i];
            CreateNode(entry.anchoredPosition, i, entry.nodeType);
        }

        // 각주: 경로선 그리기
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

        // 각주: 시작/보스 노드 강조
        if (mapData.startIndex >= 0 && mapData.startIndex < nodes.Count)
        {
            nodes[mapData.startIndex].Highlight(Color.green);
        }
        if (mapData.bossIndex >= 0 && mapData.bossIndex < nodes.Count)
        {
            nodes[mapData.bossIndex].Highlight(Color.yellow);
        }

        // 각주: 현재 플레이어 위치 하이라이트
        HighlightCurrentPosition();

        // 각주: 노드 활성화 상태 업데이트
        UpdateNodeAvailability();
    }

    /// <summary>
    /// 플레이어의 현재 위치를 하이라이트
    /// 각주: 시작 전(-1)이면 하이라이트 없음, 진행 중이면 현재 노드 강조
    /// </summary>
    void HighlightCurrentPosition()
    {
        if (cachedGameManager == null)
        {
            cachedGameManager = GameManager.Instance ?? Object.FindFirstObjectByType<GameManager>();
        }

        int currentPos = cachedGameManager != null ? cachedGameManager.lastVisitedNodeIndex : -1;

        // 각주: 게임 시작 전이거나 유효하지 않은 인덱스면 스킵
        if (currentPos < 0 || currentPos >= nodes.Count)
        {
            return;
        }

        MapNode currentNode = nodes[currentPos];

        // 각주: 현재 위치 노드를 특별하게 표시
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

        // put lines behind nodes
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

        // 각주: 캐시된 GameManager로 클리어 상태 복원
        if (cachedGameManager != null)
        {
            node.SetCleared(cachedGameManager.IsNodeCleared(idx));
        }

        // 각주: 버튼 이벤트 연결
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

        // 각주: 상호작용 가능 여부 체크
        var btn = node.GetComponent<Button>();
        if (btn == null || !btn.interactable)
        {
            Debug.Log("Node not interactable");
            return;
        }

        // 각주: 캐시된 GameManager 사용
        if (cachedGameManager == null)
        {
            cachedGameManager = GameManager.Instance ?? Object.FindFirstObjectByType<GameManager>();
        }

        // 각주: GameManager가 없으면 런타임 생성
        if (cachedGameManager == null)
        {
            GameObject gmObj = new GameObject("GameManager");
            cachedGameManager = gmObj.AddComponent<GameManager>();
        }

        // 각주: 현재 노드 기록 후 전투 씬 로드
        cachedGameManager.lastVisitedNodeIndex = node.nodeIndex;
        cachedGameManager.LoadStage();
    }
}
