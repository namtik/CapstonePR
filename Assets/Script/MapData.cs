using System;
using System.Collections.Generic;
using UnityEngine;

// MapData: 에디터에서 맵 레이아웃 정의하는 SO임
// 각주: MapManager는 이 SO를 읽어 노드를 생성하고 경로선을 그림
[CreateAssetMenu(fileName = "MapData", menuName = "Map/MapData")]
public class MapData : ScriptableObject
{
    [Serializable]
    public class NodeEntry
    {
        // 노드의 UI 캔버스 상의 상대 위치(anchoredPosition).
        // 각주: MapManager가 RectTransform.anchoredPosition에 그대로 사용합니다.
        public Vector2 anchoredPosition = Vector2.zero;

        // 노드 유형 (Combat, Shop, Rest, Elite)
        // MapNode 색상 및 동작을 결정하는 기본 정보
        public NodeType nodeType = NodeType.Combat;

        // 이 노드가 들어갈 씬 이름
        public string sceneName = "";

        public RoundData roundData; // 노드에서 시작할 라운드 정보 (전투, 상점 등)

        // 연결된 노드들의 인덱스 목록
        // 인덱스는 MapData.nodes 리스트의 인덱스를 참조
        public List<int> connections = new List<int>();
    }

    public List<NodeEntry> nodes = new List<NodeEntry>();

    [Header("Optional: start/boss indices")]
    public int startIndex = -1;
    public int bossIndex = -1;
}
