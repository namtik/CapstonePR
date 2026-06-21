using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// MapGenerator로 생성한 맵을 인스턴스화하고 노드/경로를 그리는 관리자
public class MapManager : MonoBehaviour
{
    public GameObject nodePrefab; // 노드 프리팹
    public Transform nodesParent; // 노드들의 부모 트랜스폼

    [Header("Path visuals")]
    public float lineThickness = 8f; // 경로 선 두께
    public Color lineColor = new Color(0.8f, 0.8f, 0.8f, 1f); // 경로 선 색상

    [Header("Player Position Highlight")]
    public float currentPositionScale = 1.3f; // 현재 위치 노드 강조 배율

    [Header("Map Scroll")]
    public MapScrollController scrollController;  // 맵 스크롤 컨트롤러

    [Header("Node Visual")]
    public NodeVisualConfig nodeVisualConfig;  // 노드 비주얼 설정

    private MapData mapData; // 생성된 맵 데이터 (런타임 보관)
    private MapGenerator mapGenerator; // 맵 생성기 참조

    private List<MapNode> nodes = new List<MapNode>(); // 생성된 노드 목록
    private List<GameObject> pathLines = new List<GameObject>(); // 생성된 경로 선 목록
    private Sprite lineSprite; // 경로 선용 스프라이트

    public RoundManager roundManager;  // 라운드 관리자 참조

    [Header("디버그 — 보스방 바로 진입 (테스트용)")]
    [Tooltip("ON이면 맵에서 아래 키를 눌러 보스방으로 즉시 진입한다.")]
    [SerializeField] private bool debugBossShortcut = true; // 보스방 단축키 사용 여부
    [Tooltip("보스방 바로 진입 단축키.")]
    [SerializeField] private KeyCode debugBossKey = KeyCode.B; // 보스방 진입 단축키

    private bool isMapGenerated = false;  // 맵 생성 완료 여부

    // 초기화: MapGenerator 확보, 경로 선 스프라이트 준비
    void Awake()
    {
        mapGenerator = GetComponent<MapGenerator>();
        if (mapGenerator == null)
        {
            mapGenerator = gameObject.AddComponent<MapGenerator>();
        }

        if (lineSprite == null)
        {
            lineSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        }
    }

    // 시작 시 맵을 한 번 생성한다
    void Start()
    {
        if (!isMapGenerated)
        {
            GenerateMap();
            isMapGenerated = true;
        }
    }

    // 맵을 새로고침한다 (GameStateController에서 호출)
    public void RefreshMap()
    {
        // 노드가 없거나 데이터가 없으면 새로 생성
        if (nodes.Count == 0 || mapData == null)
        {
            GenerateMap();
        }
        else
        {
            // 기존 노드 상태/활성화/강조 갱신
            UpdateNodeStates();
            UpdateNodeAvailability();
            HighlightCurrentPosition();

            // 카메라를 현재 위치로 이동
            UpdateScrollPosition();

            SyncRunStageDisplay();
        }
    }

    // 새 맵을 생성한다 (2바퀴 진입·런 재시작 시)
    public void RegenerateMap()
    {
        GenerateMap();
        isMapGenerated = true;
    }

    // GameStateController의 클리어 정보로 각 노드 상태를 갱신한다
    void UpdateNodeStates()
    {
        var stateController = GameStateController.Instance;
        if (stateController == null) return;

        for (int i = 0; i < nodes.Count; i++)
        {
            nodes[i].SetCleared(stateController.IsNodeCleared(i));
        }
    }

    // 현재 위치에서 진입 가능한 노드만 버튼을 활성화한다
    void UpdateNodeAvailability()
    {
        // 모든 노드 비활성화
        for (int i = 0; i < nodes.Count; i++)
        {
            var btn = nodes[i].GetComponent<Button>();
            if (btn != null) btn.interactable = false;
        }

        // 현재 위치 확인
        var stateController = GameStateController.Instance;
        if (stateController == null) return;

        int current = stateController.lastVisitedNodeIndex;

        // 맵 데이터 유효성 확인
        if (mapData == null || mapData.nodes == null || mapData.nodes.Count == 0)
        {
            return;
        }

        // 아직 시작 전이면 시작 노드만 활성화
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

        // 현재 노드와 연결된 다음 노드 활성화
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

    // 맵을 새로 생성하고 노드/경로를 만든다
    void GenerateMap()
    {
        ClearNodes();

        // MapGenerator로 맵 데이터 생성
        if (mapGenerator != null)
        {
            mapData = mapGenerator.GenerateMap();
        }

        // 맵 데이터 유효성 확인
        if (mapData == null || mapData.nodes == null || mapData.nodes.Count == 0)
        {
            return;
        }

        // 노드 생성
        for (int i = 0; i < mapData.nodes.Count; i++)
        {
            var entry = mapData.nodes[i];
            CreateNode(entry.anchoredPosition, i, entry.roundData, entry.nodeType);
        }

        // 경로 선 그리기
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

        // 현재 플레이어 위치 강조
        HighlightCurrentPosition();

        // 노드 활성화 상태 갱신
        UpdateNodeAvailability();

        // 스크롤을 시작 노드로 즉시 이동
        UpdateScrollPosition(true);

        SyncRunStageDisplay();
    }

    // 현재 맵 위치 기준으로 메뉴바 스테이지 표시(n-n)를 동기화한다.
    public void SyncRunStageDisplay()
    {
        var stateController = GameStateController.Instance;
        if (stateController == null)
            return;

        if (stateController.lastVisitedNodeIndex < 0 || mapData == null)
        {
            stateController.ResetMapStageForLapStart();
            return;
        }

        int nodeIndex = stateController.lastVisitedNodeIndex;
        if (nodeIndex < 0 || nodeIndex >= mapData.nodes.Count)
            return;

        stateController.SetCurrentMapStageFromColumn(mapData.nodes[nodeIndex].column);
    }

    // 스크롤을 현재(또는 시작) 노드 위치로 이동시킨다
    void UpdateScrollPosition(bool snapImmediately = false)
    {
        if (scrollController == null)
        {
            // MapScrollController 자동 탐색
            scrollController = FindFirstObjectByType<MapScrollController>();
            if (scrollController == null) return;
        }

        var stateController = GameStateController.Instance;
        if (stateController == null) return;

        int currentPos = stateController.lastVisitedNodeIndex;

        // 아직 시작 전이면 시작 노드로 이동
        if (currentPos < 0)
        {
            if (mapData != null && mapData.startIndex >= 0 && mapData.startIndex < nodes.Count)
            {
                RectTransform startNodeRect = nodes[mapData.startIndex].GetComponent<RectTransform>();

                if (snapImmediately)
                {
                    // 맵 생성 직후엔 즉시 이동
                    scrollController.SnapToNode(startNodeRect);
                }
                else
                {
                    scrollController.ScrollToNode(startNodeRect);
                }
            }
            return;
        }

        // 현재 위치로 이동
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

    // 현재 플레이어 위치 노드를 강조 표시한다
    void HighlightCurrentPosition()
    {
        var stateController = GameStateController.Instance;
        if (stateController == null) return;

        int currentPos = stateController.lastVisitedNodeIndex;

        // 모든 노드의 현재 위치 표시 해제
        for (int i = 0; i < nodes.Count; i++)
        {
            nodes[i].ClearCurrentPositionHighlight();
        }

        // 시작 전이거나 유효하지 않은 인덱스면 스킵
        if (currentPos < 0 || currentPos >= nodes.Count)
        {
            return;
        }

        MapNode currentNode = nodes[currentPos];

        // 현재 위치 노드를 강조 표시
        currentNode.HighlightAsCurrentPosition(currentPositionScale);
    }

    // 두 노드를 잇는 경로 선 오브젝트를 생성한다
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

    // 생성된 모든 노드와 경로 선을 제거한다
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

    // 프리팹으로 노드 하나를 생성하고 초기화/버튼 연결한다
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

        // 노드가 경로 선보다 앞에 오도록 (클릭 가능하게)
        go.transform.SetAsLastSibling();

        MapNode node = go.GetComponent<MapNode>() ?? go.AddComponent<MapNode>();
        node.nodeIndex = idx;
        node.roundData = roundData;
        node.nodeType = nodeType;  // NodeType 정보 전달
        node.mapManager = this;

        // 노드 타입별 크기 적용(NodeVisualConfig.size). (0,0)이면 프리팹 크기 유지. node.nodeType은 roundData가 반영된 실제 타입.
        if (rt != null && nodeVisualConfig != null)
        {
            Vector2 typeSize = nodeVisualConfig.GetSizeForType(node.nodeType);
            if (typeSize.x > 0f && typeSize.y > 0f) rt.sizeDelta = typeSize;
        }
        node.visualConfig = nodeVisualConfig;  // 비주얼 설정 전달

        // 클리어 상태 반영
        var stateController = GameStateController.Instance;
        if (stateController != null)
        {
            node.SetCleared(stateController.IsNodeCleared(idx));
        }

        // 버튼 이벤트 연결
        var btn = go.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();

            // 람다로 캡처해 올바른 노드 참조 보장
            MapNode capturedNode = node;
            btn.onClick.AddListener(() => capturedNode.OnClicked());

            btn.interactable = false;

            // Navigation을 None으로 설정
            var navigation = btn.navigation;
            navigation.mode = UnityEngine.UI.Navigation.Mode.None;
            btn.navigation = navigation;
        }

        // Image raycastTarget 설정
        var img = go.GetComponent<Image>();
        if (img != null)
        {
            img.raycastTarget = true;
        }

        nodes.Add(node);
    }

    // 노드 클릭 시 호출: 해당 노드의 라운드를 시작한다
    public void OnNodeSelected(MapNode node)
    {
        // 상호작용 가능 여부 체크
        var btn = node.GetComponent<Button>();
        if (btn == null || !btn.interactable) return;

        // 현재 노드 기록
        var stateController = GameStateController.Instance;
        if (stateController == null) return;

        stateController.lastVisitedNodeIndex = node.nodeIndex;

        int mapColumn = mapData.nodes[node.nodeIndex].column;
        stateController.SetCurrentMapStageFromColumn(mapColumn);

        // 보스 노드인지 확인
        bool isBossNode = (mapData != null && node.nodeIndex == mapData.bossIndex);

        // 노드 타입에 맞는 스테이지 표시
        stateController.ShowCanvasForNodeType(node.nodeType, isBossNode);

        // RoundData가 있으면 라운드 시작
        RoundData roundData = mapData.nodes[node.nodeIndex].roundData;
        if (roundData != null)
        {
            ApplyDifficultyColumnToRoundData(roundData, mapData.nodes[node.nodeIndex].column);

            // 인스펙터 참조가 비어 있으면(클래스 rename 후유증 등) 씬에서 자동 탐색
            if (roundManager == null)
                roundManager = FindFirstObjectByType<RoundManager>(FindObjectsInactive.Include);
            if (roundManager == null)
            {
                Debug.LogError("[MapManager] RoundManager를 찾지 못해 라운드를 시작할 수 없습니다. (씬에 RoundManager가 있는지 확인)");
                return;
            }
            roundManager.StartRound(roundData);
        }
    }

    // 매 프레임 디버그 보스방 단축키 입력을 감지한다
    void Update()
    {
        // [디버그] 맵 화면에서 단축키로 보스방 즉시 진입 (MapManager가 활성일 때만 동작 = 맵 보기 중).
        if (debugBossShortcut && Input.GetKeyDown(debugBossKey))
            JumpToBoss();
    }

    // [디버그] 노드 게이팅을 우회해 보스방 전투로 즉시 진입한다
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
        stateController.SetCurrentMapStageFromColumn(bossEntry.column);
        stateController.ShowCanvasForNodeType(NodeType.Boss, true);

        RoundData roundData = bossEntry.roundData;
        if (roundData == null)
        {
            Debug.LogWarning("[MapManager] 보스 노드에 roundData가 없습니다 (RoundDataConfig 확인).");
            return;
        }

        // 난이도 스케일링 컬럼 인덱스 주입 (OnNodeSelected와 동일).
        ApplyDifficultyColumnToRoundData(roundData, bossEntry.column);

        if (roundManager == null)
        {
            Debug.LogWarning("[MapManager] RoundManager가 미연결입니다.");
            return;
        }

        roundManager.StartRound(roundData);
        Debug.Log($"[MapManager] 보스방 바로 진입 (nodeIndex={bossIndex}, difficultyCol={ResolveDifficultyColumn(bossEntry.column)}).");
    }

    // 맵 컬럼을 현재 바퀴 누적 난이도 컬럼으로 변환해 RoundData에 주입한다.
    void ApplyDifficultyColumnToRoundData(RoundData roundData, int mapColumn)
    {
        int difficultyColumn = ResolveDifficultyColumn(mapColumn);

        if (roundData is CombatRoundData combat)
            combat.columnIndex = difficultyColumn;
        else if (roundData is EliteRoundData elite)
            elite.columnIndex = difficultyColumn;
        else if (roundData is BossRoundData boss)
            boss.columnIndex = difficultyColumn;
    }

    int ResolveDifficultyColumn(int mapColumn)
    {
        if (roundManager == null)
            roundManager = FindFirstObjectByType<RoundManager>(FindObjectsInactive.Include);

        if (roundManager != null)
            return roundManager.ResolveDifficultyColumn(mapColumn);

        var state = GameStateController.Instance;
        int completedLaps = state != null ? state.bossDefeatCount : 0;
        return mapColumn + completedLaps * 11;
    }
}
