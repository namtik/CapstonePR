using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 간단한 노드 기반 맵 생성/렌더링/입력 처리
public class MapManager : MonoBehaviour
{
    public GameObject nodePrefab; // 단색 원/네모 UI 프리팹 (Image 필요)
    public Transform nodesParent; // 노드를 배치할 부모(예: Canvas)

    [Header("Path visuals")]
    public float lineThickness = 8f;
    public Color lineColor = new Color(0.8f, 0.8f, 0.8f, 1f);
    [Header("Map graph")]
    public int startNodeIndex = 3;
    public int bossNodeIndex = 6;

    [System.Serializable]
    public struct Edge { public int from; public int to; }
    public List<Edge> edges = new List<Edge>();
    public MapData mapData; // optional ScriptableObject data

    private List<MapNode> nodes = new List<MapNode>();
    private List<GameObject> pathLines = new List<GameObject>();
    private Sprite lineSprite;

    void Start()
    {
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
                rt.sizeDelta = new Vector2(800f, 600f);
                nodesParent = parentObj.transform;
            }
            else
            {
                Debug.LogWarning("MapManager: Canvas not found. Please add a Canvas to the scene.");
                return;
            }
            // create map now that parent is ready
            GenerateSimpleMap();
        }

        // create map now that parent is ready
        GenerateSimpleMap();
        // Prepare a simple white sprite for drawing lines
        if (lineSprite == null)
        {
            lineSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        }
    }

    void UpdateNodeAvailability()
    {
        // default: all not interactable
        for (int i = 0; i < nodes.Count; i++)
        {
            var btn = nodes[i].GetComponent<UnityEngine.UI.Button>();
            if (btn != null) btn.interactable = false;
        }

        var gm = GameManager.Instance ?? Object.FindFirstObjectByType<GameManager>();
        int current = gm != null ? gm.lastVisitedNodeIndex : -1;

        // If we have mapData, use its connections
        if (mapData != null && mapData.nodes != null && mapData.nodes.Count > 0)
        {
            if (current < 0)
            {
                if (mapData.startIndex >= 0 && mapData.startIndex < nodes.Count)
                {
                    var btn = nodes[mapData.startIndex].GetComponent<UnityEngine.UI.Button>();
                    if (btn != null && !nodes[mapData.startIndex].isCleared) btn.interactable = true;
                }
                return;
            }

            if (current >= 0 && current < mapData.nodes.Count)
            {
                var conns = mapData.nodes[current].connections;
                if (conns != null)
                {
                    foreach (var t in conns)
                    {
                        if (t >= 0 && t < nodes.Count && !nodes[t].isCleared)
                        {
                            var btn = nodes[t].GetComponent<UnityEngine.UI.Button>();
                            if (btn != null) btn.interactable = true;
                        }
                    }
                }
            }

            return;
        }

        // else use inspector edges if present
        if (edges != null && edges.Count > 0)
        {
            if (current < 0)
            {
                if (startNodeIndex >= 0 && startNodeIndex < nodes.Count)
                {
                    var btn = nodes[startNodeIndex].GetComponent<UnityEngine.UI.Button>();
                    if (btn != null && !nodes[startNodeIndex].isCleared) btn.interactable = true;
                }
                return;
            }

            foreach (var e in edges)
            {
                if (e.from == current && e.to >= 0 && e.to < nodes.Count && !nodes[e.to].isCleared)
                {
                    var btn = nodes[e.to].GetComponent<UnityEngine.UI.Button>();
                    if (btn != null) btn.interactable = true;
                }
            }
            return;
        }

        // fallback sequential
        if (current < 0)
        {
            if (startNodeIndex >= 0 && startNodeIndex < nodes.Count)
            {
                var btn = nodes[startNodeIndex].GetComponent<UnityEngine.UI.Button>();
                if (btn != null && !nodes[startNodeIndex].isCleared) btn.interactable = true;
            }
            return;
        }

        if (current >= 0 && current < nodes.Count - 1)
        {
            var btn = nodes[current + 1].GetComponent<UnityEngine.UI.Button>();
            if (btn != null && !nodes[current + 1].isCleared) btn.interactable = true;
        }
    }

    void GenerateSimpleMap()
    {
        ClearNodes();

        if (mapData != null && mapData.nodes != null && mapData.nodes.Count > 0)
        {
            // create nodes from mapData
            for (int i = 0; i < mapData.nodes.Count; i++)
            {
                var entry = mapData.nodes[i];
                CreateNode(entry.anchoredPosition, i, entry.nodeType);
            }

            // draw lines using mapData connections
            for (int i = 0; i < mapData.nodes.Count; i++)
            {
                var entry = mapData.nodes[i];
                if (entry.connections == null) continue;
                foreach (var tgt in entry.connections)
                {
                    if (tgt >= 0 && tgt < nodes.Count)
                        CreateLineBetween(nodes[i].GetComponent<RectTransform>(), nodes[tgt].GetComponent<RectTransform>());
                }
            }

            // highlight optional start/boss
            if (mapData.startIndex >= 0 && mapData.startIndex < nodes.Count)
            {
                nodes[mapData.startIndex].Highlight(Color.green);
            }
            if (mapData.bossIndex >= 0 && mapData.bossIndex < nodes.Count)
            {
                nodes[mapData.bossIndex].Highlight(Color.yellow);
            }
            // update availability after creating nodes
            UpdateNodeAvailability();
            return;
        }

        // 간단한 3행 노드 배치(각 행별 가로 정렬)
        int[] rowCounts = new int[] { 3, 4, 3 };
        float rowSpacing = 140f;
        float colSpacing = 200f;

        int index = 0;
        for (int r = 0; r < rowCounts.Length; r++)
        {
            int cols = rowCounts[r];
            float y = (rowCounts.Length - 1) * rowSpacing * 0.5f - r * rowSpacing;
            float startX = -(cols - 1) * colSpacing * 0.5f;
            for (int c = 0; c < cols; c++)
            {
                float x = startX + c * colSpacing;
                CreateNode(new Vector2(x, y), index, NodeType.Combat);
                index++;
            }
        }

        // draw lines after creating nodes (uses inspector edges if provided)
        DrawPathLines();
        // highlight start and boss nodes if present
        if (startNodeIndex >= 0 && startNodeIndex < nodes.Count)
            nodes[startNodeIndex].Highlight(Color.green);
        if (bossNodeIndex >= 0 && bossNodeIndex < nodes.Count)
            nodes[bossNodeIndex].Highlight(Color.yellow);

        // update which nodes are interactable based on adjacency/cleared state
        UpdateNodeAvailability();
    }

    void DrawPathLines()
    {
        if (nodes.Count <= 1) return;

        if (edges != null && edges.Count > 0)
        {
            foreach (var e in edges)
            {
                if (e.from >= 0 && e.from < nodes.Count && e.to >= 0 && e.to < nodes.Count)
                {
                    CreateLineBetween(nodes[e.from].GetComponent<RectTransform>(), nodes[e.to].GetComponent<RectTransform>());
                }
            }
            return;
        }

        // fallback to sequential
        for (int i = 0; i < nodes.Count - 1; i++)
        {
            CreateLineBetween(nodes[i].GetComponent<RectTransform>(), nodes[i + 1].GetComponent<RectTransform>());
        }
    }

    void BuildAdjacency()
    {
        // (old BuildAdjacency removed - use inspector-editable edges list)
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
        // Set cleared state from GameManager if available
        var gm = Object.FindFirstObjectByType<GameManager>();
        if (gm != null)
        {
            node.SetCleared(gm.IsNodeCleared(idx));
        }
        node.mapManager = this;

        // Add Button event
        var btn = go.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(() => node.OnClicked());
            // start disabled until availability update
            btn.interactable = false;
        }

        nodes.Add(node);

        // set mapManager reference on node so it can call back
        node.mapManager = this;
    }

    public string fallbackBattleSceneName = "SampleScene"; // 인스펙터에서 변경 가능

    public void OnNodeSelected(MapNode node)
    {
        Debug.Log($"Node selected: {node.nodeIndex} type:{node.nodeType}");
        // only allow selecting nodes that are interactable
        var btn = node.GetComponent<UnityEngine.UI.Button>();
        if (btn == null || !btn.interactable)
        {
            Debug.Log("Node not interactable");
            return;
        }

        // 우선 GameManager 싱글턴 사용
        var gm = GameManager.Instance ?? Object.FindFirstObjectByType<GameManager>();
        // 각주: 씬에 GameManager가 없으면 런타임에서 생성합니다.
        // 각주: 이렇게 하면 마지막 방문 노드(lastVisitedNodeIndex)를 기록하고
        // 각주: 씬 전환 시 상태를 유지할 수 있는 지속성 있는 매니저가 확보됩니다.
        if (gm == null)
        {
            GameObject gmObj = new GameObject("GameManager");
            gm = gmObj.AddComponent<GameManager>();
        }

        gm.lastVisitedNodeIndex = node.nodeIndex;
        gm.LoadBattleScene();
        return;

        // 최후의 수단: fallback 씬 이름으로 직접 로드
        Debug.LogWarning("GameManager not found. Falling back to direct scene load.");
        UnityEngine.SceneManagement.SceneManager.LoadScene(fallbackBattleSceneName);
    }
}
