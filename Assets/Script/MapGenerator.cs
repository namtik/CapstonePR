using System.Collections.Generic;
using UnityEngine;
using System.Linq;

// 맵 노드 생성 로직
// 기본: 시작 컬럼부터 보스 컬럼까지 연결되는 10개 일반 컬럼 + 보스 컬럼
public class MapGenerator : MonoBehaviour
{
    [Header("맵 생성 설정")]
    [Tooltip("일반 컬럼의 개수")]
    public int totalColumns = 10;
    
    [Tooltip("정예 노드 등장 확률 (0~1)")]
    [Range(0f, 1f)]
    public float eliteChance = 0.2f;
    [Tooltip("정예 노드가 등장 가능한 최소 컬럼")]
    public int eliteColumnMin = 3;

    [Tooltip("각 컬럼의 최소 노드 수")]
    public int minNodesPerColumn = 3;
    
    [Tooltip("각 컬럼의 최대 노드 수")]
    public int maxNodesPerColumn = 4;
    
    [Header("배치 설정")]
    [Tooltip("컬럼 간 가로 간격")]
    public float columnSpacing = 250f;
    
    [Tooltip("노드 간 최소 세로 간격")]
    public float minNodeSpacing = 100f;
    
    [Tooltip("노드 간 최대 세로 간격")]
    public float maxNodeSpacing = 200f;
    
    [Tooltip("연결 가능한 최대 Y축 거리")]
    public float maxConnectionDistance = 300f;
    
    [Header("특수 노드 배치 (랜덤 범위)")]
    [Tooltip("상점 노드 최소 컬럼 인덱스")]
    public int shopColumnMin = 5;
    [Tooltip("상점 노드 최대 컬럼 인덱스")]
    public int shopColumnMax = 4;
    
    [Tooltip("휴식 노드 최소 컬럼 인덱스")]
    public int restColumnMin = 9;
    [Tooltip("휴식 노드 최대 컬럼 인덱스")]
    public int restColumnMax = 8;
    
    [Header("이벤트 노드 설정")]
    [Tooltip("이벤트 노드 최소 개수")]
    public int minEventNodes = 1;
    [Tooltip("이벤트 노드 최대 개수")]
    public int maxEventNodes = 2;
    [Tooltip("이벤트 노드가 등장 가능한 최소 컬럼 (상점/휴식 제외)")]
    public int eventColumnMin = 2;
    [Tooltip("이벤트 노드가 등장 가능한 최대 컬럼 (보스 직전)")]
    public int eventColumnMax = 7;

    [Header("유물 노드 설정")]
    [Tooltip("유물 노드 최소 개수")]
    public int minRelicNodes = 1;
    [Tooltip("유물 노드 최대 개수")]
    public int maxRelicNodes = 1;
    [Tooltip("유물 노드가 등장 가능한 최소 컬럼 (상점/휴식/이벤트 제외)")]
    public int relicColumnMin = 2;
    [Tooltip("유물 노드가 등장 가능한 최대 컬럼")]
    public int relicColumnMax = 8;

    [SerializeField] private RoundDataConfig roundDataConfig;

    // 생성된 맵 데이터
    private MapData generatedMapData;
    
    // 각 컬럼별 노드 인덱스 목록 (연결 계산용)
    private List<List<int>> columnNodes = new List<List<int>>();
    
    // 이번 생성에 실제로 배정된 특수 노드 컬럼 (랜덤 결과)
    private int actualShopColumn;
    private int actualRestColumn;
    private List<int> actualEventColumns = new List<int>();
    private List<int> actualRelicColumns = new List<int>();

    /// <summary>
    /// 맵 데이터를 새로 생성한다.
    /// 이 함수를 호출하면 새로운 맵 구조가 만들어진다.
    /// </summary>
    public MapData GenerateMap()
    {
        // 1. MapData 초기화
        generatedMapData = ScriptableObject.CreateInstance<MapData>();
        generatedMapData.nodes = new List<MapData.NodeEntry>();
        columnNodes.Clear();

        // 특수 노드 컬럼을 랜덤 선택
        actualShopColumn = Random.Range(shopColumnMin, shopColumnMax + 1);
        actualRestColumn = Random.Range(restColumnMin, restColumnMax + 1);
        
        // 이벤트 컬럼 개수 결정 (1~2개)
        actualEventColumns.Clear();
        int eventCount = Random.Range(minEventNodes, maxEventNodes + 1);
        
        // 상점/휴식 컬럼을 제외한 이벤트 가능 컬럼 수집
        List<int> availableColumns = new List<int>();
        for (int i = eventColumnMin; i <= eventColumnMax; i++)
        {
            if (i != actualShopColumn && i != actualRestColumn)
            {
                availableColumns.Add(i);
            }
        }
        
        // 이벤트 컬럼을 랜덤 추출
        for (int i = 0; i < eventCount && availableColumns.Count > 0; i++)
        {
            int randomIndex = Random.Range(0, availableColumns.Count);
            actualEventColumns.Add(availableColumns[randomIndex]);
            availableColumns.RemoveAt(randomIndex);
        }

        // 유물 컬럼 개수 결정
        actualRelicColumns.Clear();
        int relicCount = Random.Range(minRelicNodes, maxRelicNodes + 1);
        List<int> relicAvailableColumns = new List<int>();
        for (int i = relicColumnMin; i <= relicColumnMax; i++)
        {
            if (i != actualShopColumn && i != actualRestColumn && !actualEventColumns.Contains(i))
            {
                relicAvailableColumns.Add(i);
            }
        }

        for (int i = 0; i < relicCount && relicAvailableColumns.Count > 0; i++)
        {
            int randomIndex = Random.Range(0, relicAvailableColumns.Count);
            actualRelicColumns.Add(relicAvailableColumns[randomIndex]);
            relicAvailableColumns.RemoveAt(randomIndex);
        }
        
        Debug.Log($"맵 생성: 상점={actualShopColumn}열, 휴식={actualRestColumn}열, 이벤트={string.Join(",", actualEventColumns)}열, 유물={string.Join(",", actualRelicColumns)}열");

        int nodeIndex = 0;

        // 2. 일반 컬럼 노드 생성 (0~9열)
        for (int col = 0; col < totalColumns; col++)
        {
            List<int> currentColumnIndices = new List<int>();
            
            // 0열(시작)은 항상 1개, 나머지는 범위 내 랜덤
            int nodeCount;
            if (col == 0)
            {
                nodeCount = 1; // 시작 노드는 1개
            }
            else
            {
                nodeCount = Random.Range(minNodesPerColumn, maxNodesPerColumn + 1);
            }
            
            // Y축 배치 계산
            float[] yPositions = GenerateYPositions(nodeCount);
            
            for (int i = 0; i < nodeCount; i++)
            {
                NodeType nodeType = DetermineNodeType(col, i, nodeCount);

                MapData.NodeEntry node = new MapData.NodeEntry
                {
                    anchoredPosition = new Vector2(col * columnSpacing, yPositions[i]),
                    nodeType = nodeType,
                    roundData = roundDataConfig != null
                        ? roundDataConfig.GetRoundData(nodeType)
                        : null,
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
            anchoredPosition = new Vector2(totalColumns * columnSpacing, 0f),
            nodeType = NodeType.Boss,
            roundData = roundDataConfig != null
                ? roundDataConfig.GetRoundData(NodeType.Boss)
                : null,
            column = totalColumns,
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
        ConnectNodes();

        Debug.Log($"맵 생성 완료: 총 {generatedMapData.nodes.Count}개 노드");
        return generatedMapData;
    }

    /// <summary>
    /// Y축 위치 생성 (균등 배치 + 랜덤 오프셋)
    /// 노드가 겹치지 않도록 간격을 유지한다.
    /// </summary>
    float[] GenerateYPositions(int count)
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
            float randomOffset = Random.Range(-30f, 30f);
            positions[i] = baseY + randomOffset;
        }
        
        return positions;
    }

    /// <summary>
    /// 노드 타입 결정
    /// 시작/특수 노드 우선 배치 후, 나머지는 일반/정예로 결정.
    /// </summary>
    NodeType DetermineNodeType(int columnIndex, int nodeIndexInColumn, int totalNodesInColumn)
    {
        // 시작 컬럼은 전투 고정
        if (columnIndex == 0)
            return NodeType.Combat;
        
        // 상점 컬럼 중앙 노드
        if (columnIndex == actualShopColumn && nodeIndexInColumn == totalNodesInColumn / 2)
            return NodeType.Shop;
        
        // 휴식 컬럼 중앙 노드
        if (columnIndex == actualRestColumn && nodeIndexInColumn == totalNodesInColumn / 2)
            return NodeType.Rest;
        
        // 이벤트 컬럼 중앙 노드
        if (actualEventColumns.Contains(columnIndex) && nodeIndexInColumn == totalNodesInColumn / 2)
            return NodeType.Event;

        // 유물 컬럼 중앙 노드
        if (actualRelicColumns.Contains(columnIndex) && nodeIndexInColumn == totalNodesInColumn / 2)
            return NodeType.Relic;
        
        if (columnIndex >= eliteColumnMin && Random.value < eliteChance)
            return NodeType.Elite;

        // 기본은 일반 전투
        return NodeType.Combat;
    }

    /// <summary>
    /// 노드 간 연결 생성
    /// 각 컬럼에서 다음 컬럼으로 연결한다.
    /// </summary>
    void ConnectNodes()
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

            // 현재 컬럼 각 노드에서 다음 컬럼으로 연결
            foreach (int nodeIndex in currentColumn)
            {
                ConnectToNextColumn(nodeIndex, nextColumn);
            }
        }

        // 2. 단절 노드 보정
        EnsureAllNodesConnected();
    }

    /// <summary>
    /// 한 노드를 다음 컬럼 노드들과 연결
    /// Y축 거리가 가까운 노드 우선으로 1~2개 연결한다.
    /// </summary>
    void ConnectToNextColumn(int fromIndex, List<int> nextColumnIndices)
    {
        Vector2 fromPos = generatedMapData.nodes[fromIndex].anchoredPosition;
        
        // Y축 거리 기준 정렬
        var sortedTargets = nextColumnIndices
            .Select(idx => new {
                Index = idx,
                Distance = Mathf.Abs(generatedMapData.nodes[idx].anchoredPosition.y - fromPos.y)
            })
            .Where(x => x.Distance <= maxConnectionDistance)
            .OrderBy(x => x.Distance)
            .ToList();

        // 최소 1개, 최대 2개 연결
        int connectCount = Mathf.Min(Random.Range(1, 3), sortedTargets.Count);
        
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

    /// <summary>
    /// 모든 노드가 최소 1개의 진입 경로를 갖도록 보정
    /// 진입이 없는 노드는 이전 컬럼의 가장 가까운 노드와 연결한다.
    /// </summary>
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

    /// <summary>
    /// 디버그용: 생성된 맵 정보 출력
    /// </summary>
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
