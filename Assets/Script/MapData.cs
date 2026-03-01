using System;
using System.Collections.Generic;
using UnityEngine;

// MapData: 에디터에서 맵 레이아웃을 정의하는 SO
// MapManager가 이 SO를 읽어 노드를 생성하고 경로선을 그림
[CreateAssetMenu(fileName = "MapData", menuName = "Map/MapData")]
public class MapData : ScriptableObject
{
    [Serializable]
    public class NodeEntry
    {
        // UI 캔버스 상의 노드 위치(anchoredPosition)
        public Vector2 anchoredPosition = Vector2.zero;

        // 노드 타입 (Combat, Shop, Rest, Elite)
        public NodeType nodeType = NodeType.Combat;

        // 이어진 노드의 인덱스 목록 (MapData.nodes 리스트의 인덱스)
        public List<int> connections = new List<int>();
    }

    public List<NodeEntry> nodes = new List<NodeEntry>();

    [Header("Optional: start/boss indices")]
    public int startIndex = -1;
    public int bossIndex = -1;
}
