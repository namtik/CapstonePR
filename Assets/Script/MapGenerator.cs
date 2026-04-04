using System.Collections.Generic;
using UnityEngine;
using System.Linq;

// Ⱦ��ũ�� �α׶���ũ �� ������
// ����: ���ʿ��� ���������� �����ϴ� 10�� �÷� + ������ ����
public class MapGenerator : MonoBehaviour
{
    [Header("�� ���� ����")]
    [Tooltip("�Ϲ� �������� �÷� ����")]
    public int totalColumns = 10;
    
    [Tooltip("Elite�� ���� Ȯ�� (0~1)")]
    [Range(0f, 1f)]
    public float eliteChance = 0.2f;
    [Tooltip("Elite�� ���� �� �ִ� �ּ� �÷�")]
    public int eliteColumnMin = 3;

    [Tooltip("�� �÷��� �ּ� ��� ����")]
    public int minNodesPerColumn = 3;
    
    [Tooltip("�� �÷��� �ִ� ��� ����")]
    public int maxNodesPerColumn = 4;
    
    [Header("��ġ ����")]
    [Tooltip("�÷� �� ���� ����")]
    public float columnSpacing = 250f;
    
    [Tooltip("��� �� �ּ� ���� ����")]
    public float minNodeSpacing = 100f;
    
    [Tooltip("��� �� �ִ� ���� ����")]
    public float maxNodeSpacing = 200f;
    
    [Tooltip("���� ������ �ִ� Y�� �Ÿ�")]
    public float maxConnectionDistance = 300f;
    
    [Header("Ư���� ��ġ (���� ����)")]
    [Tooltip("�������� ���� �ּ� �÷� �ε���")]
    public int shopColumnMin = 5;
    [Tooltip("�������� ���� �ִ� �÷� �ε���")]
    public int shopColumnMax = 4;
    
    [Tooltip("�޽Ĺ��� ���� �ּ� �÷� �ε���")]
    public int restColumnMin = 9;
    [Tooltip("�޽Ĺ��� ���� �ִ� �÷� �ε���")]
    public int restColumnMax = 8;
    
    [Header("�̺�Ʈ ��� ����")]
    [Tooltip("�̺�Ʈ ��� ���� �ּ� ����")]
    public int minEventNodes = 1;
    [Tooltip("�̺�Ʈ ��� ���� �ִ� ����")]
    public int maxEventNodes = 2;
    [Tooltip("�̺�Ʈ ��尡 ������ �� �ִ� �ּ� �÷� (����/�޽� ����)")]
    public int eventColumnMin = 2;
    [Tooltip("�̺�Ʈ ��尡 ������ �� �ִ� �ִ� �÷� (���� ����)")]
    public int eventColumnMax = 7;

    [SerializeField] private RoundDataConfig roundDataConfig;

    // ������ �� ������
    private MapData generatedMapData;
    
    // �� �÷��� ��� ����Ʈ (���� ����)
    private List<List<int>> columnNodes = new List<List<int>>();
    
    // ����: ���� �ʿ��� ������ ���Ǵ� Ư���� �÷� (�Ź� ����)
    private int actualShopColumn;
    private int actualRestColumn;
    private List<int> actualEventColumns = new List<int>();

    /// <summary>
    /// �� ���� ���� �Լ�
    /// ����: �� �Լ��� ȣ���ϸ� ���ο� ���� ������
    /// </summary>
    public MapData GenerateMap()
    {
        // 1. MapData �ʱ�ȭ
        generatedMapData = ScriptableObject.CreateInstance<MapData>();
        generatedMapData.nodes = new List<MapData.NodeEntry>();
        columnNodes.Clear();

        // ����: Ư���� ��ġ�� �Ź� �����ϰ� ����
        actualShopColumn = Random.Range(shopColumnMin, shopColumnMax + 1);
        actualRestColumn = Random.Range(restColumnMin, restColumnMax + 1);
        
        // �̺�Ʈ �÷� ���� ���� (1~2��)
        actualEventColumns.Clear();
        int eventCount = Random.Range(minEventNodes, maxEventNodes + 1);
        
        // ������ �޽� �÷��� ������ ������ �÷� ��� ����
        List<int> availableColumns = new List<int>();
        for (int i = eventColumnMin; i <= eventColumnMax; i++)
        {
            if (i != actualShopColumn && i != actualRestColumn)
            {
                availableColumns.Add(i);
            }
        }
        
        // ������ �÷����� �����ϰ� ����
        for (int i = 0; i < eventCount && availableColumns.Count > 0; i++)
        {
            int randomIndex = Random.Range(0, availableColumns.Count);
            actualEventColumns.Add(availableColumns[randomIndex]);
            availableColumns.RemoveAt(randomIndex);
        }
        
        Debug.Log($"�̹� ��: ����={actualShopColumn}�� �÷�, �޽�={actualRestColumn}�� �÷�, �̺�Ʈ={string.Join(",", actualEventColumns)}�� �÷�");

        int nodeIndex = 0;

        // 2. �� �÷����� ��� ���� (0~9�� �÷�)
        for (int col = 0; col < totalColumns; col++)
        {
            List<int> currentColumnIndices = new List<int>();
            
            // ����: 0�� �÷�(����)�� �׻� 1����, �������� ���� 3~5��
            int nodeCount;
            if (col == 0)
            {
                nodeCount = 1; // ���� ��� 1����
            }
            else
            {
                nodeCount = Random.Range(minNodesPerColumn, maxNodesPerColumn + 1);
            }
            
            // Y�� ��ġ ��ġ ����
            float[] yPositions = GenerateYPositions(nodeCount);
            
            for (int i = 0; i < nodeCount; i++)
            {
                MapData.NodeEntry node = new MapData.NodeEntry
                {
                    anchoredPosition = new Vector2(col * columnSpacing, yPositions[i]),
                    nodeType = DetermineNodeType(col, i, nodeCount),
                    roundData = roundDataConfig != null
                        ? roundDataConfig.GetRoundData(DetermineNodeType(col, i, nodeCount))
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

        // 3. ���� �� ���� (11��° �÷�)
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
        
        // 4. ���� ��� ���� (0�� �÷��� ������ ���)
        if (columnNodes.Count > 0 && columnNodes[0].Count > 0)
        {
            generatedMapData.startIndex = columnNodes[0][0]; // ����: 0�� �÷��� ù ��°(������) ���
        }

        // 5. ��� �� ���� ����
        ConnectNodes();

        Debug.Log($"�� ���� �Ϸ�: �� {generatedMapData.nodes.Count}�� ���");
        return generatedMapData;
    }

    /// <summary>
    /// Y�� ��ġ ���� (�յ� ���� + ���� ������)
    /// ����: ������ ��ġ�� �ʰ� ����� ��ġ�ǵ��� ��
    /// </summary>
    float[] GenerateYPositions(int count)
    {
        float[] positions = new float[count];
        
        // ��ü ���� ���� ���
        float totalHeight = (count - 1) * ((minNodeSpacing + maxNodeSpacing) / 2f);
        float startY = -totalHeight / 2f;
        
        for (int i = 0; i < count; i++)
        {
            // �⺻ �յ� ��ġ
            float baseY = startY + i * ((minNodeSpacing + maxNodeSpacing) / 2f);
            
            // ���� ������ �߰� (�ʹ� �������� ���� �ʵ���)
            float randomOffset = Random.Range(-30f, 30f);
            positions[i] = baseY + randomOffset;
        }
        
        return positions;
    }

    /// <summary>
    /// ��� Ÿ�� ����
    /// ����: ù ��° �÷��� ������, ���� �÷��� ����/�޽�/�̺�Ʈ�� ��ġ
    /// </summary>
    NodeType DetermineNodeType(int columnIndex, int nodeIndexInColumn, int totalNodesInColumn)
    {
        // ù ��° �÷��� ������ ������
        if (columnIndex == 0)
            return NodeType.Combat;
        
        // ����: �������� ������ ���� �÷� (�߰� ��常)
        if (columnIndex == actualShopColumn && nodeIndexInColumn == totalNodesInColumn / 2)
            return NodeType.Shop;
        
        // ����: �������� ������ �޽� �÷� (�߰� ��常)
        if (columnIndex == actualRestColumn && nodeIndexInColumn == totalNodesInColumn / 2)
            return NodeType.Rest;
        
        // ����: �������� ������ �̺�Ʈ �÷� (�߰� ��常)
        if (actualEventColumns.Contains(columnIndex) && nodeIndexInColumn == totalNodesInColumn / 2)
            return NodeType.Event;
        
        if (columnIndex >= eliteColumnMin && Random.value < eliteChance)
            return NodeType.Elite;

        // �������� ������
        return NodeType.Combat;
    }

    /// <summary>
    /// ��� �� ���� ����
    /// ����: �� ���� ���� �÷��� ����� ����� �����
    /// </summary>
    void ConnectNodes()
    {
        // 1. �Ϲ� �÷� �� ���� (0��1, 1��2, ..., 9�溸��)
        for (int col = 0; col < columnNodes.Count; col++)
        {
            List<int> currentColumn = columnNodes[col];
            
            // ���� �÷� ����
            List<int> nextColumn = null;
            if (col < columnNodes.Count - 1)
            {
                // ���� �Ϲ� �÷�
                nextColumn = columnNodes[col + 1];
            }
            else
            {
                // ������ �÷� �� ������
                nextColumn = new List<int> { generatedMapData.bossIndex };
            }

            // ���� �÷��� �� ��忡�� ���� �÷����� ����
            foreach (int nodeIndex in currentColumn)
            {
                ConnectToNextColumn(nodeIndex, nextColumn);
            }
        }

        // 2. ������ ��� �ذ� (������ ���� �߰�)
        EnsureAllNodesConnected();
    }

    /// <summary>
    /// �� ��带 ���� �÷� ����� ����
    /// ����: Y�� �Ÿ��� ����� ���鸸 ���� (1~3��)
    /// </summary>
    void ConnectToNextColumn(int fromIndex, List<int> nextColumnIndices)
    {
        Vector2 fromPos = generatedMapData.nodes[fromIndex].anchoredPosition;
        
        // �Ÿ� ������ ����
        var sortedTargets = nextColumnIndices
            .Select(idx => new {
                Index = idx,
                Distance = Mathf.Abs(generatedMapData.nodes[idx].anchoredPosition.y - fromPos.y)
            })
            .Where(x => x.Distance <= maxConnectionDistance)
            .OrderBy(x => x.Distance)
            .ToList();

        // �ּ� 1��, �ִ� 3�� ����
        int connectCount = Mathf.Min(Random.Range(1, 3), sortedTargets.Count);
        
        for (int i = 0; i < connectCount; i++)
        {
            int targetIndex = sortedTargets[i].Index;
            
            // �ߺ� ���� ����
            if (!generatedMapData.nodes[fromIndex].connections.Contains(targetIndex))
            {
                generatedMapData.nodes[fromIndex].connections.Add(targetIndex);
            }
        }
    }

    /// <summary>
    /// ��� ��尡 �ּ� 1�� �̻��� ������ �������� ����
    /// ����: �Ա��� ���� ��忡 ������ ���� �߰�
    /// </summary>
    void EnsureAllNodesConnected()
    {
        // �� ��尡 �Ա��� �������� üũ
        HashSet<int> nodesWithIncoming = new HashSet<int>();
        
        for (int i = 0; i < generatedMapData.nodes.Count; i++)
        {
            foreach (int target in generatedMapData.nodes[i].connections)
            {
                nodesWithIncoming.Add(target);
            }
        }

        // �Ա��� ���� ��� ã�� (ù �÷� ����)
        for (int col = 1; col < columnNodes.Count; col++)
        {
            foreach (int nodeIndex in columnNodes[col])
            {
                if (!nodesWithIncoming.Contains(nodeIndex))
                {
                    // ���� �÷����� ���� ����� ��� ã��
                    List<int> prevColumn = columnNodes[col - 1];
                    Vector2 targetPos = generatedMapData.nodes[nodeIndex].anchoredPosition;
                    
                    int closestIndex = prevColumn
                        .OrderBy(idx => Mathf.Abs(generatedMapData.nodes[idx].anchoredPosition.y - targetPos.y))
                        .First();
                    
                    // ���� �߰�
                    if (!generatedMapData.nodes[closestIndex].connections.Contains(nodeIndex))
                    {
                        generatedMapData.nodes[closestIndex].connections.Add(nodeIndex);
                    }
                }
            }
        }

        // ������ �Ա� ����
        int bossIndex = generatedMapData.bossIndex;
        if (!nodesWithIncoming.Contains(bossIndex))
        {
            // ������ �÷��� ��� ��带 �����濡 ����
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
    /// ����׿�: ������ �� ���� ���
    /// </summary>
    public void PrintMapInfo()
    {
        Debug.Log("=== �� ���� ���� ===");
        Debug.Log($"�� ��� ��: {generatedMapData.nodes.Count}");
        Debug.Log($"���� ���: {generatedMapData.startIndex}");
        Debug.Log($"���� ���: {generatedMapData.bossIndex}");
        
        for (int i = 0; i < generatedMapData.nodes.Count; i++)
        {
            var node = generatedMapData.nodes[i];
            Debug.Log($"��� {i}: {node.nodeType}, ��ġ ({node.anchoredPosition.x}, {node.anchoredPosition.y}), ���� �� [{string.Join(", ", node.connections)}]");
        }
    }
}
