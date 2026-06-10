using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// MapGenerator�� ����Ͽ� �� ���� �� ������
public class MapManager : MonoBehaviour
{
    public GameObject nodePrefab;
    public Transform nodesParent;

    [Header("Path visuals")]
    public float lineThickness = 8f;
    public Color lineColor = new Color(0.8f, 0.8f, 0.8f, 1f);

    [Header("Player Position Highlight")]
    public float currentPositionScale = 1.3f;

    [Header("Map Scroll")]
    public MapScrollController scrollController;  // : �� ��ũ�� ��Ʈ�ѷ�

    [Header("Node Visual")]
    public NodeVisualConfig nodeVisualConfig;  // ��� ���־� ����

    // : MapGenerator�� �ڵ� ������ �� ������ (��Ÿ�� ����)
    private MapData mapData;
    private MapGenerator mapGenerator;

    private List<MapNode> nodes = new List<MapNode>();
    private List<GameObject> pathLines = new List<GameObject>();
    private Sprite lineSprite;

    public Roundmanager roundManager;  // : ���� �Ŵ��� ����

    [Header("디버그 — 보스방 바로 진입 (테스트용)")]
    [Tooltip("ON이면 맵에서 아래 키를 눌러 보스방으로 즉시 진입한다.")]
    [SerializeField] private bool debugBossShortcut = true;
    [Tooltip("보스방 바로 진입 단축키.")]
    [SerializeField] private KeyCode debugBossKey = KeyCode.B;

    private bool isMapGenerated = false;  // ���� �̹� �����Ǿ����� ����

    void Awake()
    {
        //  MapGenerator ������Ʈ Ȯ��
        mapGenerator = GetComponent<MapGenerator>();
        if (mapGenerator == null)
        {
            mapGenerator = gameObject.AddComponent<MapGenerator>();
        }

        //  ��μ��� ��������Ʈ �غ�
        if (lineSprite == null)
        {
            lineSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        }
    }

    void Start()
    {
        //  ���� ���� �� �� ���� (�� ����)
        if (!isMapGenerated)
        {
            GenerateMap();
            isMapGenerated = true;
        }
    }
    
    // �� ���ΰ�ħ (GameStateController���� ȣ��)
    //  �� ȭ������ ���ƿ� ������ ����
    public void RefreshMap()
    {
        // ���� ���� ������ ����
        if (nodes.Count == 0 || mapData == null)
        {
            GenerateMap();
        }
        else
        {
            //  ���� ���� ������ ���¸� ������Ʈ
            UpdateNodeStates();
            UpdateNodeAvailability();
            HighlightCurrentPosition();
            
            //  ī�޶� ���� ��ġ�� �̵�
            UpdateScrollPosition();
        }
    }

    void UpdateNodeStates()
    {
        //  GameStateController���� Ŭ���� ���� ����
        var stateController = GameStateController.Instance;
        if (stateController == null) return;

        for (int i = 0; i < nodes.Count; i++)
        {
            nodes[i].SetCleared(stateController.IsNodeCleared(i));
        }
    }

    void UpdateNodeAvailability()
    {
        //  ��� ��� ��Ȱ��ȭ
        for (int i = 0; i < nodes.Count; i++)
        {
            var btn = nodes[i].GetComponent<Button>();
            if (btn != null) btn.interactable = false;
        }

        //  GameStateController���� ���� ��ġ Ȯ��
        var stateController = GameStateController.Instance;
        if (stateController == null) return;

        int current = stateController.lastVisitedNodeIndex;

        //  MapData ���� ���� ó��
        if (mapData == null || mapData.nodes == null || mapData.nodes.Count == 0)
        {
            return;
        }

        //  ���� ���� �� - ���� ��常 Ȱ��ȭ
        if (current < 0)
        {
            if (mapData.startIndex >= 0 && mapData.startIndex < nodes.Count)
            {
                MapNode startNode = nodes[mapData.startIndex];
                var btn = startNode.GetComponent<Button>();
                
                if (btn != null && !startNode.isCleared)
                {
                    btn.interactable = true;
                }
            }
            return;
        }

        //  ���� ����� ����� ���� ��� Ȱ��ȭ
        if (current >= 0 && current < mapData.nodes.Count)
        {
            var conns = mapData.nodes[current].connections;
            if (conns != null && conns.Count > 0)
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

    void GenerateMap()
    {
        ClearNodes();

        //  MapGenerator�� ���� �� ����
        if (mapGenerator != null)
        {
            mapData = mapGenerator.GenerateMap();
        }

        //  MapData Ȯ��
        if (mapData == null || mapData.nodes == null || mapData.nodes.Count == 0)
        {
            return;
        }

        //  ��� ����
        for (int i = 0; i < mapData.nodes.Count; i++)
        {
            var entry = mapData.nodes[i];
            CreateNode(entry.anchoredPosition, i, entry.roundData, entry.nodeType);
        }

        // ��μ� �׸���
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

        //  ���� �÷��̾� ��ġ ���̶���Ʈ
        HighlightCurrentPosition();

        //  ��� Ȱ��ȭ ���� ������Ʈ
        UpdateNodeAvailability();

        //  ��ũ���� ���� ���� ��� �̵�
        UpdateScrollPosition(true);
    }

    // ��ũ���� ���� ��ġ ���� �̵�
    //�� ���� �� ���� ��ġ ����
    void UpdateScrollPosition(bool snapImmediately = false)
    {
        if (scrollController == null)
        {
            //  MapScrollController �ڵ� ã��
            scrollController = FindFirstObjectByType<MapScrollController>();
            if (scrollController == null) return;
        }

        var stateController = GameStateController.Instance;
        if (stateController == null) return;

        int currentPos = stateController.lastVisitedNodeIndex;

        // ���� ���� ���̸� ���� ���� �̵�
        if (currentPos < 0)
        {
            if (mapData != null && mapData.startIndex >= 0 && mapData.startIndex < nodes.Count)
            {
                RectTransform startNodeRect = nodes[mapData.startIndex].GetComponent<RectTransform>();

                if (snapImmediately)
                {
                    //  �� ���� ���Ĵ� ��� �̵�
                    scrollController.SnapToNode(startNodeRect);
                }
                else
                {
                    scrollController.ScrollToNode(startNodeRect);
                }
            }
            return;
        }

        //  ���� ��ġ ���� �̵�
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

        //  ���� ��� ����� ���� ��ġ ǥ�� ����
        for (int i = 0; i < nodes.Count; i++)
        {
            nodes[i].ClearCurrentPositionHighlight();
        }

        //  ���� ���� ���̰ų� ��ȿ���� ���� �ε����� ��ŵ
        if (currentPos < 0 || currentPos >= nodes.Count)
        {
            return;
        }

        MapNode currentNode = nodes[currentPos];

        // ���� ��ġ ��带 Ư���ϰ� ǥ��
        currentNode.HighlightAsCurrentPosition(currentPositionScale);
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

    void CreateNode(Vector2 anchoredPos, int idx, RoundData roundData, NodeType nodeType)
    {
        if (nodePrefab == null) return;

        GameObject go = Instantiate(nodePrefab, nodesParent);
        go.name = $"Node_{idx}";
        
        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = anchoredPos;
        }
        
        // ��尡 ��μ����� �տ� ������ (Ŭ�� �����ϰ�)
        go.transform.SetAsLastSibling();

        MapNode node = go.GetComponent<MapNode>() ?? go.AddComponent<MapNode>();
        node.nodeIndex = idx;
        node.roundData = roundData;
        node.nodeType = nodeType;  // NodeType ���� ����
        node.mapManager = this;

        // 노드 타입별 크기 적용(NodeVisualConfig.size). (0,0)이면 프리팹 크기 유지. node.nodeType은 roundData가 반영된 실제 타입.
        if (rt != null && nodeVisualConfig != null)
        {
            Vector2 typeSize = nodeVisualConfig.GetSizeForType(node.nodeType);
            if (typeSize.x > 0f && typeSize.y > 0f) rt.sizeDelta = typeSize;
        }
        node.visualConfig = nodeVisualConfig;  // ���־� ���� ����

        //  GameStateController�� Ŭ���� ���� ����
        var stateController = GameStateController.Instance;
        if (stateController != null)
        {
            node.SetCleared(stateController.IsNodeCleared(idx));
        }

        //  ��ư �̺�Ʈ ����
        var btn = go.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            
            // ���ٷ� ĸó�ؼ� �ùٸ� ��� ���� ����
            MapNode capturedNode = node;
            btn.onClick.AddListener(() => capturedNode.OnClicked());
            
            btn.interactable = false;
            
            // Navigation�� None���� ����
            var navigation = btn.navigation;
            navigation.mode = UnityEngine.UI.Navigation.Mode.None;
            btn.navigation = navigation;
        }
        
        // Image raycastTarget ����
        var img = go.GetComponent<Image>();
        if (img != null)
        {
            img.raycastTarget = true;
        }

        nodes.Add(node);
    }

    public void OnNodeSelected(MapNode node)
    {
        //  ��ȣ�ۿ� ���� ���� üũ
        var btn = node.GetComponent<Button>();
        if (btn == null || !btn.interactable) return;

        //  GameStateController�� ���� ��� ���
        var stateController = GameStateController.Instance;
        if (stateController == null) return;

        stateController.lastVisitedNodeIndex = node.nodeIndex;

        // ��尡 ���� ������� Ȯ��
        bool isBossNode = (mapData != null && node.nodeIndex == mapData.bossIndex);

        //  ��� Ÿ�Կ� ���� ������ ĵ���� ǥ��
        stateController.ShowCanvasForNodeType(node.nodeType, isBossNode);

        //  RoundData ���� �� ���� ����
        RoundData roundData = mapData.nodes[node.nodeIndex].roundData;
        if (roundData != null)
        {
            // 노드의 컬럼 인덱스를 RoundData에 동적으로 설정 (난이도 스케일링)
            int col = mapData.nodes[node.nodeIndex].column;
            if (roundData is CombatRoundData combat)
                combat.columnIndex = col;
            else if (roundData is EliteRoundData elite)
                elite.columnIndex = col;
            else if (roundData is BossRoundData boss)
                boss.columnIndex = col;

            roundManager.StartRound(roundData);
        }
    }

    void Update()
    {
        // [디버그] 맵 화면에서 단축키로 보스방 즉시 진입 (MapManager가 활성일 때만 동작 = 맵 보기 중).
        if (debugBossShortcut && Input.GetKeyDown(debugBossKey))
            JumpToBoss();
    }

    /// <summary>[디버그] 맵을 거치지 않고 보스방 전투로 즉시 진입. OnNodeSelected의 보스 분기와 동일하게 동작하되 노드 클릭 게이팅을 우회한다.</summary>
    [ContextMenu("보스방 바로 진입")]
    public void JumpToBoss()
    {
        if (mapData == null || mapData.nodes == null)
        {
            Debug.LogWarning("[MapManager] mapData가 없어 보스방 진입 불가 (맵 생성 후 시도).");
            return;
        }

        int bossIndex = mapData.bossIndex;
        if (bossIndex < 0 || bossIndex >= mapData.nodes.Count)
        {
            Debug.LogWarning($"[MapManager] 보스 노드 인덱스가 유효하지 않음 (bossIndex={bossIndex}, nodes={mapData.nodes.Count}).");
            return;
        }

        var stateController = GameStateController.Instance;
        if (stateController == null)
        {
            Debug.LogWarning("[MapManager] GameStateController가 없습니다.");
            return;
        }

        var bossEntry = mapData.nodes[bossIndex];

        stateController.lastVisitedNodeIndex = bossIndex;
        stateController.ShowCanvasForNodeType(NodeType.Boss, true);

        RoundData roundData = bossEntry.roundData;
        if (roundData == null)
        {
            Debug.LogWarning("[MapManager] 보스 노드에 roundData가 없습니다 (RoundDataConfig 확인).");
            return;
        }

        // 난이도 스케일링 컬럼 인덱스 주입 (OnNodeSelected와 동일).
        int col = bossEntry.column;
        if (roundData is BossRoundData boss) boss.columnIndex = col;
        else if (roundData is EliteRoundData elite) elite.columnIndex = col;
        else if (roundData is CombatRoundData combat) combat.columnIndex = col;

        if (roundManager == null)
        {
            Debug.LogWarning("[MapManager] roundManager가 미연결입니다.");
            return;
        }

        roundManager.StartRound(roundData);
        Debug.Log($"[MapManager] 보스방 바로 진입 (nodeIndex={bossIndex}, col={col}).");
    }
}
