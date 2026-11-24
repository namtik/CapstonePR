using System.Collections.Generic;
using UnityEngine;
using System.Linq;

// 맵 노드를 절차적으로 생성하는 컴포넌트 (일반 컬럼 + 보스 컬럼)
public class MapGenerator : MonoBehaviour
{
    private const int FixedRelicColumn = 5; // 화면에 표시되는 6라운드
    private const int FixedRelicNodeCount = 2;
    private const int MinEliteNodes = 3;
    private const int MaxEliteNodes = 5;

    // 상점/이벤트/유물/휴식 노드 배치 규칙 (인스펙터 형식 통일)
    [System.Serializable]
    public struct SpecialNodePlacementSettings
    {
        [Tooltip("이 타입 노드 최소 개수 (맵당)")]
        public int minNodes;
        [Tooltip("이 타입 노드 최대 개수 (맵당)")]
        public int maxNodes;
        [Tooltip("등장 가능한 최소 컬럼 인덱스")]
        public int columnMin;
        [Tooltip("등장 가능한 최대 컬럼 인덱스")]
        public int columnMax;
    }

    [Header("맵 생성 설정")]
    [Tooltip("일반 컬럼의 개수")]
    public int totalColumns = 10; // 일반 컬럼 수

    [Tooltip("정예 노드 등장 확률 (0~1)")]
    [Range(0f, 1f)]
    public float eliteChance = 0.2f; // 정예 노드 등장 확률
    [Tooltip("정예 노드가 등장 가능한 최소 컬럼")]
    public int eliteColumnMin = 2; // 화면에 표시되는 3라운드부터 정예 등장

    [Tooltip("각 컬럼의 최소 노드 수")]
    public int minNodesPerColumn = 3; // 컬럼당 최소 노드 수

    [Tooltip("각 컬럼의 최대 노드 수")]
    public int maxNodesPerColumn = 4; // 컬럼당 최대 노드 수

    [Header("배치 설정")]
    [Tooltip("컬럼 간 가로 간격")]
    public float columnSpacing = 250f; // 컬럼 가로 간격

    [Tooltip("노드 간 최소 세로 간격")]
    public float minNodeSpacing = 100f; // 노드 최소 세로 간격

    [Tooltip("노드 간 최대 세로 간격")]
    public float maxNodeSpacing = 200f; // 노드 최대 세로 간격

    [Tooltip("연결 가능한 최대 Y축 거리")]
    public float maxConnectionDistance = 300f; // 연결 허용 최대 Y거리

    [Header("상점 설정")]
    public SpecialNodePlacementSettings shopSettings = new SpecialNodePlacementSettings
    {
        minNodes = 1,
        maxNodes = 1,
        columnMin = 5,
        columnMax = 5,
    };

    [Header("이벤트 설정")]
    public SpecialNodePlacementSettings eventSettings = new SpecialNodePlacementSettings
    {
        minNodes = 1,
        maxNodes = 2,
        columnMin = 2,
        columnMax = 7,
    };

    [Header("유물 설정")]
    public SpecialNodePlacementSettings relicSettings = new SpecialNodePlacementSettings
    {
        minNodes = FixedRelicNodeCount,
        maxNodes = FixedRelicNodeCount,
        columnMin = 2,
        columnMax = 8,
    };

    [Header("휴식 설정")]
    public SpecialNodePlacementSettings restSettings = new SpecialNodePlacementSettings
    {
        minNodes = 1,
        maxNodes = 1,
        columnMin = 9,
        columnMax = 9,
    };

    [SerializeField] private RoundDataConfig roundDataConfig; // 노드 타입별 라운드 데이터 설정

    private MapData generatedMapData; // 생성된 맵 데이터

    private List<List<int>> columnNodes = new List<List<int>>(); // 컬럼별 노드 인덱스 목록 (연결 계산용)

    private List<int> actualShopColumns = new List<int>(); // 이번에 배정된 상점 컬럼들
    private List<int> actualRestColumns = new List<int>(); // 이번에 배정된 휴식 컬럼들
    private List<int> actualEventColumns = new List<int>(); // 이번에 배정된 이벤트 컬럼들
    private List<int> actualRelicColumns = new List<int>(); // 이번에 배정된 유물 컬럼들

    // 맵 데이터를 새로 생성해 반환한다
    public MapData GenerateMap()
    {
        // 1. MapData 초기화
        generatedMapData = ScriptableObject.CreateInstance<MapData>();
        generatedMapData.nodes = new List<MapData.NodeEntry>();
        columnNodes.Clear();

        int normalColumnCount = Mathf.Max(2, totalColumns);
        int preBossRestColumn = normalColumnCount - 1;
        int relicColumn = FixedRelicColumn < preBossRestColumn ? FixedRelicColumn : -1;

        // 6라운드와 보스 직전 열을 예약한 뒤 나머지 특수 노드의 열을 고른다.
        var reservedColumns = new HashSet<int> { preBossRestColumn };
        if (relicColumn >= 0)
            reservedColumns.Add(relicColumn);

        actualShopColumns = PickSpecialNodeColumns(shopSettings, reservedColumns);
        reservedColumns.UnionWith(actualShopColumns);
        actualEventColumns = PickSpecialNodeColumns(eventSettings, reservedColumns);
        reservedColumns.UnionWith(actualEventColumns);
        actualRelicColumns = PickSpecialNodeColumns(relicSettings, reservedColumns, relicColumn, FixedRelicNodeCount);
        reservedColumns.UnionWith(actualRelicColumns);
        actualRestColumns = PickSpecialNodeColumns(restSettings, reservedColumns, preBossRestColumn);
        reservedColumns.UnionWith(actualRestColumns);

        Debug.Log($"맵 생성: 상점={string.Join(",", actualShopColumns)}열, 이벤트={string.Join(",", actualEventColumns)}열, 유물={string.Join(",", actualRelicColumns)}열, 휴식={string.Join(",", actualRestColumns)}열");

        int nodeIndex = 0;

        // 2. 일반 컬럼 노드 생성
        for (int col = 0; col < normalColumnCount; col++)
        {
            List<int> currentColumnIndices = new List<int>();

            bool isFixedRelicColumn = col == relicColumn;
            bool isPreBossRestColumn = col == preBossRestColumn;
            int nodeCount;
            if (isFixedRelicColumn)
                nodeCount = FixedRelicNodeCount;
            else if (col == 0 || isPreBossRestColumn)
                nodeCount = 1;
            else
                nodeCount = Random.Range(minNodesPerColumn, maxNodesPerColumn + 1);

            // Y축 배치 계산
            float[] yPositions = GenerateYPositions(nodeCount, !isFixedRelicColumn && !isPreBossRestColumn);

            // 특수 노드가 배정된 열은 슬롯을 한 번만 무작위로 고른다.
            int specialNodeIndex = col > 0 && !isFixedRelicColumn && reservedColumns.Contains(col)
                ? Random.Range(0, nodeCount)
                : -1;

            for (int i = 0; i < nodeCount; i++)
            {
                NodeType nodeType = isFixedRelicColumn ? NodeType.Relic : DetermineNodeType(col, i, specialNodeIndex);

                MapData.NodeEntry node = new MapData.NodeEntry
                {
                    anchoredPosition = new Vector2(col * columnSpacing, yPositions[i]),
                    nodeType = nodeType,
                    column = col,
                    connections = new List<int>()
                };

                generatedMapData.nodes.Add(node);
                currentColumnIndices.Add(nodeIndex);
                nodeIndex++;
            }

            columnNodes.Add(currentColumnIndices);
        }

        // 3. 보스 노드 생성 (마지막 열)
        MapData.NodeEntry bossNode = new MapData.NodeEntry
        {
            anchoredPosition = new Vector2(normalColumnCount * columnSpacing, 0f),
            nodeType = NodeType.Boss,
            column = normalColumnCount,
            connections = new List<int>()
        };
        generatedMapData.nodes.Add(bossNode);
        int bossIndex = nodeIndex;
        generatedMapData.bossIndex = bossIndex;

        // 4. 시작 노드 인덱스 설정
        if (columnNodes.Count > 0 && columnNodes[0].Count > 0)
        {
            generatedMapData.startIndex = columnNodes[0][0]; // 0열 첫 노드
        }

        // 5. 노드 연결 생성
        ConnectNodes(relicColumn, preBossRestColumn);
        SeparateShopNodes();
        BalanceEliteNodes();

        if (roundDataConfig != null)
        {
            foreach (var node in generatedMapData.nodes)
                node.roundData = roundDataConfig.GetRoundData(node.nodeType);
        }

        Debug.Log($"맵 생성 완료: 총 {generatedMapData.nodes.Count}개 노드");
        return generatedMapData;
    }

    void SeparateShopNodes()
    {
        var mapNodes = generatedMapData.nodes;
        var originalShopSlots = new int[columnNodes.Count];
        var allowedShopSlots = new List<int>[columnNodes.Count];

        // 뒤에서부터 계산해 다음 상점과 연결되지 않는 슬롯만 남긴다.
        for (int col = columnNodes.Count - 1; col >= 0; col--)
        {
            int shopSlot = columnNodes[col].FindIndex(index => mapNodes[index].nodeType == NodeType.Shop);
            if (shopSlot < 0) continue;

            int currentShop = columnNodes[col][shopSlot];
            originalShopSlots[col] = currentShop;
            var nextSlots = col + 1 < columnNodes.Count ? allowedShopSlots[col + 1] : null;
            var allowed = columnNodes[col].Where(index => nextSlots == null
                || nextSlots.Any(next => !mapNodes[index].connections.Contains(next))).ToList();
            if (allowed.Count > 0)
                allowedShopSlots[col] = allowed;
            else
                mapNodes[currentShop].nodeType = NodeType.Combat;
        }

        int previousShop = -1;
        for (int col = 0; col < allowedShopSlots.Length; col++)
        {
            var allowed = allowedShopSlots[col];
            if (allowed == null)
            {
                previousShop = -1;
                continue;
            }

            if (previousShop >= 0)
                allowed.RemoveAll(mapNodes[previousShop].connections.Contains);

            int original = originalShopSlots[col];
            int selected = allowed.Contains(original) ? original : allowed[Random.Range(0, allowed.Count)];
            mapNodes[original].nodeType = mapNodes[selected].nodeType;
            mapNodes[selected].nodeType = NodeType.Shop;
            previousShop = selected;
        }
    }

    void BalanceEliteNodes()
    {
        var mapNodes = generatedMapData.nodes;
        var neighbors = new List<int>[mapNodes.Count];
        for (int i = 0; i < neighbors.Length; i++)
            neighbors[i] = new List<int>();

        for (int i = 0; i < mapNodes.Count; i++)
        {
            foreach (int target in mapNodes[i].connections)
            {
                neighbors[i].Add(target);
                neighbors[target].Add(i);
            }
        }

        var candidates = Enumerable.Range(0, mapNodes.Count)
            .Where(index => mapNodes[index].column > 0 && mapNodes[index].column >= eliteColumnMin
                && (mapNodes[index].nodeType == NodeType.Combat || mapNodes[index].nodeType == NodeType.Elite))
            .OrderByDescending(index => mapNodes[index].nodeType == NodeType.Elite)
            .ThenBy(index => Random.value)
            .ToList();
        int rolledCount = candidates.Count(index => mapNodes[index].nodeType == NodeType.Elite);
        int targetCount = Mathf.Clamp(rolledCount, MinEliteNodes, MaxEliteNodes);
        var selected = new List<int>();

        // 최초 확률 판정에서 뽑힌 슬롯을 우선 사용한다.
        foreach (int index in candidates)
        {
            mapNodes[index].nodeType = NodeType.Combat;
            if (selected.Count < targetCount && !selected.Any(neighbors[index].Contains))
                selected.Add(index);
        }

        // 선택 순서 때문에 최소 수에 못 미쳤으면 가능한 조합을 한 번 더 찾는다.
        if (selected.Count < MinEliteNodes)
        {
            var minimumSet = new List<int>();
            if (TrySelectEliteMinimum(candidates, neighbors, 0, minimumSet))
                selected = minimumSet;
        }

        foreach (int index in selected)
            mapNodes[index].nodeType = NodeType.Elite;
    }

    bool TrySelectEliteMinimum(List<int> candidates, List<int>[] neighbors, int start, List<int> selected)
    {
        int needed = MinEliteNodes - selected.Count;
        if (needed == 0) return true;

        for (int i = start; i <= candidates.Count - needed; i++)
        {
            int index = candidates[i];
            if (selected.Any(neighbors[index].Contains)) continue;

            selected.Add(index);
            if (TrySelectEliteMinimum(candidates, neighbors, i + 1, selected)) return true;
            selected.RemoveAt(selected.Count - 1);
        }

        return false;
    }

    // 노드 수만큼 Y축 위치를 균등 배치 + 랜덤 오프셋으로 계산한다
    float[] GenerateYPositions(int count, bool randomize)
    {
        float[] positions = new float[count];

        // 전체 높이 기준 계산
        float totalHeight = (count - 1) * ((minNodeSpacing + maxNodeSpacing) / 2f);
        float startY = -totalHeight / 2f;

        for (int i = 0; i < count; i++)
        {
            // 기본 균등 배치
            float baseY = startY + i * ((minNodeSpacing + maxNodeSpacing) / 2f);

            // 랜덤 오프셋 추가 (과도한 겹침 방지)
            float randomOffset = randomize ? Random.Range(-30f, 30f) : 0f;
            positions[i] = baseY + randomOffset;
        }

        return positions;
    }

    // 설정에 따라 특수 노드가 배치될 컬럼을 무작위로 고른다
    List<int> PickSpecialNodeColumns(SpecialNodePlacementSettings settings, HashSet<int> reservedColumns,
        int fixedColumn = -1, int fixedNodeCount = 1)
    {
        var picked = new List<int>();
        int minNodes = Mathf.Max(0, settings.minNodes);
        int maxNodes = Mathf.Max(minNodes, settings.maxNodes);
        int remainingCount = Random.Range(minNodes, maxNodes + 1);

        // 고정 열도 맵당 노드 수에 포함한다.
        if (fixedColumn >= 0)
        {
            picked.Add(fixedColumn);
            remainingCount -= fixedNodeCount;
        }

        if (remainingCount <= 0)
            return picked;

        int colMin = Mathf.Min(settings.columnMin, settings.columnMax);
        int colMax = Mathf.Max(settings.columnMin, settings.columnMax);

        var available = new List<int>();
        for (int i = colMin; i <= colMax; i++)
        {
            if (reservedColumns != null && reservedColumns.Contains(i))
                continue;
            available.Add(i);
        }

        for (int i = 0; i < remainingCount && available.Count > 0; i++)
        {
            int randomIndex = Random.Range(0, available.Count);
            picked.Add(available[randomIndex]);
            available.RemoveAt(randomIndex);
        }

        return picked;
    }

    // 컬럼/위치 조건에 따라 노드 타입(시작/특수/정예/일반)을 결정한다
    NodeType DetermineNodeType(int columnIndex, int nodeIndexInColumn, int specialNodeIndex)
    {
        // 시작 컬럼은 전투 고정
        if (columnIndex == 0)
            return NodeType.Combat;

        if (nodeIndexInColumn == specialNodeIndex)
        {
            if (actualShopColumns.Contains(columnIndex))
                return NodeType.Shop;
            if (actualRestColumns.Contains(columnIndex))
                return NodeType.Rest;
            if (actualEventColumns.Contains(columnIndex))
                return NodeType.Event;
            if (actualRelicColumns.Contains(columnIndex))
                return NodeType.Relic;
        }

        if (columnIndex >= eliteColumnMin && Random.value < eliteChance)
            return NodeType.Elite;

        // 기본은 일반 전투
        return NodeType.Combat;
    }

    // 각 컬럼을 다음 컬럼으로 연결하고 단절 노드를 보정한다
    void ConnectNodes(int relicColumn, int preBossRestColumn)
    {
        // 1. 일반 컬럼 간 연결 (0->1, 1->2, ..., 마지막->보스)
        for (int col = 0; col < columnNodes.Count; col++)
        {
            List<int> currentColumn = columnNodes[col];

            // 다음 컬럼 결정
            List<int> nextColumn = null;
            if (col < columnNodes.Count - 1)
            {
                // 다음 일반 컬럼
                nextColumn = columnNodes[col + 1];
            }
            else
            {
                // 마지막 컬럼은 보스로 연결
                nextColumn = new List<int> { generatedMapData.bossIndex };
            }

            if (col + 1 == relicColumn)
            {
                ConnectToRelicColumn(currentColumn, nextColumn);
                continue;
            }

            // 현재 컬럼 각 노드에서 다음 컬럼으로 연결
            foreach (int nodeIndex in currentColumn)
            {
                // 보스 직전에는 휴식으로 모인다.
                if (col >= preBossRestColumn - 1)
                    generatedMapData.nodes[nodeIndex].connections.AddRange(nextColumn);
                else
                    ConnectToNextColumn(nodeIndex, nextColumn, requireConnection: col == relicColumn);
            }
        }

        // 2. 단절 노드 보정
        EnsureAllNodesConnected();
    }

    // 유물 직전 열을 위아래로 나눈다. 가운데 노드가 하나면 두 유물에 연결한다.
    void ConnectToRelicColumn(List<int> previousColumn, List<int> relicColumn)
    {
        var nodes = generatedMapData.nodes;
        var previousByHeight = previousColumn.OrderBy(index => nodes[index].anchoredPosition.y).ToArray();
        var relicsByHeight = relicColumn.OrderBy(index => nodes[index].anchoredPosition.y).ToArray();
        int lastLower = (previousByHeight.Length - 1) / 2;
        int firstUpper = previousByHeight.Length / 2;

        for (int i = 0; i < previousByHeight.Length; i++)
        {
            var connections = nodes[previousByHeight[i]].connections;
            if (i <= lastLower)
                connections.Add(relicsByHeight[0]);
            if (i >= firstUpper)
                connections.Add(relicsByHeight[1]);
        }
    }

    // 한 노드를 Y거리가 가까운 다음 컬럼 노드 1~2개와 연결한다
    void ConnectToNextColumn(int fromIndex, List<int> nextColumnIndices, bool requireConnection)
    {
        Vector2 fromPos = generatedMapData.nodes[fromIndex].anchoredPosition;

        // Y축 거리 기준 정렬
        var sortedTargets = nextColumnIndices
            .Select(idx => new {
                Index = idx,
                Distance = Mathf.Abs(generatedMapData.nodes[idx].anchoredPosition.y - fromPos.y)
            })
            .OrderBy(x => x.Distance)
            .ToList();

        // 최소 1개, 최대 2개 연결
        int candidates = sortedTargets.Count(x => x.Distance <= maxConnectionDistance);
        if (requireConnection && sortedTargets.Count > 0)
            candidates = Mathf.Max(1, candidates);
        int connectCount = Mathf.Min(Random.Range(1, 3), candidates);

        for (int i = 0; i < connectCount; i++)
        {
            int targetIndex = sortedTargets[i].Index;

            // 중복 연결 방지
            if (!generatedMapData.nodes[fromIndex].connections.Contains(targetIndex))
            {
                generatedMapData.nodes[fromIndex].connections.Add(targetIndex);
            }
        }
    }

    // 모든 노드가 최소 1개의 진입 경로를 갖도록 보정한다
    void EnsureAllNodesConnected()
    {
        // 각 노드의 진입 연결 여부 체크
        HashSet<int> nodesWithIncoming = new HashSet<int>();

        for (int i = 0; i < generatedMapData.nodes.Count; i++)
        {
            foreach (int target in generatedMapData.nodes[i].connections)
            {
                nodesWithIncoming.Add(target);
            }
        }

        // 진입 연결이 없는 노드 찾기 (시작 컬럼 제외)
        for (int col = 1; col < columnNodes.Count; col++)
        {
            foreach (int nodeIndex in columnNodes[col])
            {
                if (!nodesWithIncoming.Contains(nodeIndex))
                {
                    // 이전 컬럼에서 가장 가까운 노드 찾기
                    List<int> prevColumn = columnNodes[col - 1];
                    Vector2 targetPos = generatedMapData.nodes[nodeIndex].anchoredPosition;

                    int closestIndex = prevColumn
                        .OrderBy(idx => Mathf.Abs(generatedMapData.nodes[idx].anchoredPosition.y - targetPos.y))
                        .First();

                    // 연결 추가
                    if (!generatedMapData.nodes[closestIndex].connections.Contains(nodeIndex))
                    {
                        generatedMapData.nodes[closestIndex].connections.Add(nodeIndex);
                    }
                }
            }
        }

        // 보스 진입 보장
        int bossIndex = generatedMapData.bossIndex;
        if (!nodesWithIncoming.Contains(bossIndex))
        {
            // 마지막 컬럼의 모든 노드를 보스에 연결
            List<int> lastColumn = columnNodes[columnNodes.Count - 1];
            foreach (int nodeIndex in lastColumn)
            {
                if (!generatedMapData.nodes[nodeIndex].connections.Contains(bossIndex))
                {
                    generatedMapData.nodes[nodeIndex].connections.Add(bossIndex);
                }
            }
        }
    }

    // 디버그용: 생성된 맵 정보를 콘솔에 출력한다
    public void PrintMapInfo()
    {
        Debug.Log("=== 맵 생성 정보 ===");
        Debug.Log($"총 노드 수: {generatedMapData.nodes.Count}");
        Debug.Log($"시작 노드: {generatedMapData.startIndex}");
        Debug.Log($"보스 노드: {generatedMapData.bossIndex}");

        for (int i = 0; i < generatedMapData.nodes.Count; i++)
        {
            var node = generatedMapData.nodes[i];
            Debug.Log($"노드 {i}: {node.nodeType}, 위치 ({node.anchoredPosition.x}, {node.anchoredPosition.y}), 연결 [{string.Join(", ", node.connections)}]");
        }
    }
}
