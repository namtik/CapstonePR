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

            // 전투 노드면 새 전투 시스템도 시작
            bool isCombatNode = (node.nodeType == NodeType.Combat || node.nodeType == NodeType.Elite
                                 || node.nodeType == NodeType.Boss || isBossNode);
            if (isCombatNode)
            {
                // MapManager는 MapStage 하위 → HideAllStages 후 비활성화되므로,
                // 씬 루트의 NewBattleController에 딜레이 코루틴을 위임한다.
                var newBattle = Battle.NewBattleController.Instance
                    ?? FindFirstObjectByType<Battle.NewBattleController>();
                if (newBattle != null)
                    newBattle.StartBattleAfterDelay();
                else
                    Debug.LogWarning("[MapManager] NewBattleController를 찾을 수 없습니다.");
            }
        }
    }


    // ��� Ÿ�Կ� �´� RoundData�� ���� ����
    

}
