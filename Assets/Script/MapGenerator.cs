using System.Collections.Generic;
using UnityEngine;
using System.Linq;


// 왼쪽에서 오른쪽으로 진행하는 10개 컬럼 + 보스방 구조
public class MapGenerator : MonoBehaviour
{
    [Header("맵 구조 설정")]
    [Tooltip("일반 스테이지 컬럼 개수")]
    public int totalColumns = 10;
    
    [Tooltip("각 컬럼당 최소 노드 개수")]
    public int minNodesPerColumn = 3;
    
    [Tooltip("각 컬럼당 최대 노드 개수")]
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
    
    [Header("특수방 위치 (랜덤 범위)")]
    [Tooltip("상점방이 나올 최소 컬럼 인덱스")]
    public int shopColumnMin = 2;
    [Tooltip("상점방이 나올 최대 컬럼 인덱스")]
    public int shopColumnMax = 4;
    
    [Tooltip("휴식방이 나올 최소 컬럼 인덱스")]
    public int restColumnMin = 5;
    [Tooltip("휴식방이 나올 최대 컬럼 인덱스")]
    public int restColumnMax = 7;

    // 생성된 맵 데이터
    private MapData generatedMapData;
    
    // 각 컬럼별 노드 리스트 (연결 계산용)
    private List<List<int>> columnNodes = new List<List<int>>();
    
    // 현재 맵에서 실제로 사용되는 특수방 컬럼 (매번 랜덤)
    private int actualShopColumn;
    private int actualRestColumn;

    // 맵 생성 메인 함수
    public MapData GenerateMap()
    {
        // 1. MapData 초기화
        generatedMapData = ScriptableObject.CreateInstance<MapData>();
        generatedMapData.nodes = new List<MapData.NodeEntry>();
        columnNodes.Clear();

        // 특수방 위치를 매번 랜덤하게 결정
        actualShopColumn = Random.Range(shopColumnMin, shopColumnMax + 1);
        actualRestColumn = Random.Range(restColumnMin, restColumnMax + 1);
        Debug.Log($"이번 맵: 상점={actualShopColumn}번 컬럼, 휴식={actualRestColumn}번 컬럼");

        int nodeIndex = 0;

        // 2. 각 컬럼별로 노드 생성 (0~9번 컬럼)
        for (int col = 0; col < totalColumns; col++)
        {
            List<int> currentColumnIndices = new List<int>();
            
            // 0번 컬럼(시작)은 항상 1개만, 나머지는 랜덤 3~5개
            int nodeCount;
            if (col == 0)
            {
                nodeCount = 1; // 시작 노드 1개만
            }
            else
            {
                nodeCount = Random.Range(minNodesPerColumn, maxNodesPerColumn + 1);
            }
            
            // Y축 배치 위치 결정
            float[] yPositions = GenerateYPositions(nodeCount);
            
            for (int i = 0; i < nodeCount; i++)
            {
                MapData.NodeEntry node = new MapData.NodeEntry
                {
                    anchoredPosition = new Vector2(col * columnSpacing, yPositions[i]),
                    nodeType = DetermineNodeType(col, i, nodeCount),
                    connections = new List<int>()
                };
                
                generatedMapData.nodes.Add(node);
                currentColumnIndices.Add(nodeIndex);
                nodeIndex++;
            }
            
            columnNodes.Add(currentColumnIndices);
        }

        // 3. 보스 방 생성
        MapData.NodeEntry bossNode = new MapData.NodeEntry
        {
            anchoredPosition = new Vector2(totalColumns * columnSpacing, 0f),
            nodeType = NodeType.Elite, // 보스방
            connections = new List<int>()
        };
        generatedMapData.nodes.Add(bossNode);
        int bossIndex = nodeIndex;
        generatedMapData.bossIndex = bossIndex;
        
        // 4. 시작 노드 설정 (0번 컬럼의 유일한 노드)
        if (columnNodes.Count > 0 && columnNodes[0].Count > 0)
        {
            generatedMapData.startIndex = columnNodes[0][0]; // 각주: 0번 컬럼의 첫 번째(유일한) 노드
        }

        // 5. 노드 간 연결 생성
        ConnectNodes();

        Debug.Log($"맵 생성 완료: 총 {generatedMapData.nodes.Count}개 노드");
        return generatedMapData;
    }


    /// Y축 위치 생성 (균등 분포 + 랜덤 오프셋)

    float[] GenerateYPositions(int count)
    {
        float[] positions = new float[count];
        
        // 전체 높이 범위 계산
        float totalHeight = (count - 1) * ((minNodeSpacing + maxNodeSpacing) / 2f);
        float startY = -totalHeight / 2f;
        
        for (int i = 0; i < count; i++)
        {
            // 기본 균등 배치
            float baseY = startY + i * ((minNodeSpacing + maxNodeSpacing) / 2f);
            
            // 랜덤 오프셋 추가 (너무 일직선이 되지 않도록)
            float randomOffset = Random.Range(-30f, 30f);
            positions[i] = baseY + randomOffset;
        }
        
        return positions;
    }

    // 노드 타입 결정
    // 첫 번째 컬럼은 전투방, 랜덤 컬럼에 상점/휴식방 배치
    NodeType DetermineNodeType(int columnIndex, int nodeIndexInColumn, int totalNodesInColumn)
    {
        // 첫 번째 컬럼은 무조건 전투방
        if (columnIndex == 0)
            return NodeType.Combat;
        
        // 각주: 랜덤으로 결정된 상점 컬럼 (중간 노드만)
        if (columnIndex == actualShopColumn && nodeIndexInColumn == totalNodesInColumn / 2)
            return NodeType.Shop;
        
        // 각주: 랜덤으로 결정된 휴식 컬럼 (중간 노드만)
        if (columnIndex == actualRestColumn && nodeIndexInColumn == totalNodesInColumn / 2)
            return NodeType.Rest;
        
        // 나머지는 전투방
        return NodeType.Combat;
    }

    // 노드 간 연결 생성
    // 각 노드는 다음 컬럼의 가까운 노드들과 연결됨

    void ConnectNodes()
    {
        // 1. 일반 컬럼 간 연결 (0→1, 1→2, ..., 9→보스)
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
                // 마지막 컬럼 → 보스방
                nextColumn = new List<int> { generatedMapData.bossIndex };
            }

            // 현재 컬럼의 각 노드에서 다음 컬럼으로 연결
            foreach (int nodeIndex in currentColumn)
            {
                ConnectToNextColumn(nodeIndex, nextColumn);
            }
        }

        // 2. 고립된 노드 해결 (역방향 연결 추가)
        EnsureAllNodesConnected();
    }

    // 한 노드를 다음 컬럼 노드들과 연결
    // Y축 거리가 가까운 노드들만 연결 (1~3개)
    void ConnectToNextColumn(int fromIndex, List<int> nextColumnIndices)
    {
        Vector2 fromPos = generatedMapData.nodes[fromIndex].anchoredPosition;
        
        // 거리 순으로 정렬
        var sortedTargets = nextColumnIndices
            .Select(idx => new {
                Index = idx,
                Distance = Mathf.Abs(generatedMapData.nodes[idx].anchoredPosition.y - fromPos.y)
            })
            .Where(x => x.Distance <= maxConnectionDistance)
            .OrderBy(x => x.Distance)
            .ToList();

        // 최소 1개, 최대 3개 연결
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
    // 모든 노드가 최소 1개 이상의 연결을 가지도록 보장
    //  입구가 없는 노드에 역방향 연결 추가
    void EnsureAllNodesConnected()
    {
        // 각 노드가 입구를 가지는지 체크
        HashSet<int> nodesWithIncoming = new HashSet<int>();
        
        for (int i = 0; i < generatedMapData.nodes.Count; i++)
        {
            foreach (int target in generatedMapData.nodes[i].connections)
            {
                nodesWithIncoming.Add(target);
            }
        }

        // 입구가 없는 노드 찾기 (첫 컬럼 제외)
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
                        Debug.Log($"고립 노드 {nodeIndex} 해결: {closestIndex} → {nodeIndex} 연결 추가");
                    }
                }
            }
        }

        // 보스방 입구 보장
        int bossIndex = generatedMapData.bossIndex;
        if (!nodesWithIncoming.Contains(bossIndex))
        {
            // 마지막 컬럼의 모든 노드를 보스방에 연결
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

    // 디버그용: 생성된 맵 정보 출력
    public void PrintMapInfo()
    {
        Debug.Log("=== 맵 생성 정보 ===");
        Debug.Log($"총 노드 수: {generatedMapData.nodes.Count}");
        Debug.Log($"시작 노드: {generatedMapData.startIndex}");
        Debug.Log($"보스 노드: {generatedMapData.bossIndex}");
        
        for (int i = 0; i < generatedMapData.nodes.Count; i++)
        {
            var node = generatedMapData.nodes[i];
            Debug.Log($"노드 {i}: {node.nodeType}, 위치 ({node.anchoredPosition.x}, {node.anchoredPosition.y}), 연결 → [{string.Join(", ", node.connections)}]");
        }
    }
}
