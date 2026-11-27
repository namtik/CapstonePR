using System;
using System.Collections.Generic;
using UnityEngine;

// 맵 레이아웃(노드 목록과 연결 정보)을 담는 ScriptableObject
[CreateAssetMenu(fileName = "MapData", menuName = "Map/MapData")]
public class MapData : ScriptableObject
{
    // 맵의 개별 노드 정보를 담는 직렬화 데이터
    [Serializable]
    public class NodeEntry
    {
        public Vector2 anchoredPosition = Vector2.zero; // UI 캔버스 기준 노드 위치

        public NodeType nodeType = NodeType.Combat; // 노드 종류(전투/상점/휴식 등)

        public string sceneName = ""; // 이 노드가 진입할 씬 이름

        public RoundData roundData; // 노드에서 진행할 라운드 데이터(전투/보상 등)

        public int column; // 이 노드가 속한 컬럼 인덱스 (난이도 스케일링용)

        public List<int> connections = new List<int>(); // 연결된 다음 노드 인덱스 목록
    }

    public List<NodeEntry> nodes = new List<NodeEntry>(); // 전체 노드 목록

    [Header("Optional: start/boss indices")]
    public int startIndex = -1; // 시작 노드 인덱스
    public int bossIndex = -1; // 보스 노드 인덱스
}
