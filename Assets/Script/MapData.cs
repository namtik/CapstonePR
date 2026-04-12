using System;
using System.Collections.Generic;
using UnityEngine;

// MapData: �����Ϳ��� �� ���̾ƿ� �����ϴ� SO��
// ����: MapManager�� �� SO�� �о� ��带 �����ϰ� ��μ��� �׸�
[CreateAssetMenu(fileName = "MapData", menuName = "Map/MapData")]
public class MapData : ScriptableObject
{
    [Serializable]
    public class NodeEntry
    {
        // ����� UI ĵ���� ���� ��� ��ġ(anchoredPosition).
        // ����: MapManager�� RectTransform.anchoredPosition�� �״�� ����մϴ�.
        public Vector2 anchoredPosition = Vector2.zero;

        // ��� ���� (Combat, Shop, Rest, Elite)
        // MapNode ���� �� ������ �����ϴ� �⺻ ����
        public NodeType nodeType = NodeType.Combat;

        // �� ��尡 �� �� �̸�
        public string sceneName = "";

        public RoundData roundData; // ��忡�� ������ ���� ���� (����, ���� ��)

        public int column; // 이 노드가 속한 컬럼 인덱스 (난이도 스케일링용)

        // ����� ������ �ε��� ���
        // �ε����� MapData.nodes ����Ʈ�� �ε����� ����
        public List<int> connections = new List<int>();
    }

    public List<NodeEntry> nodes = new List<NodeEntry>();

    [Header("Optional: start/boss indices")]
    public int startIndex = -1;
    public int bossIndex = -1;
}
