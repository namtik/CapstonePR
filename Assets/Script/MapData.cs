using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MapData", menuName = "Map/MapData")]
public class MapData : ScriptableObject
{
    [Serializable]
    public class NodeEntry
    {
        public Vector2 anchoredPosition = Vector2.zero;
        public NodeType nodeType = NodeType.Combat;
        public string sceneName = ""; // optional scene to load for this node
        public List<int> connections = new List<int>(); // indices of target nodes
    }

    public List<NodeEntry> nodes = new List<NodeEntry>();

    [Header("Optional: start/boss indices")]
    public int startIndex = -1;
    public int bossIndex = -1;
}
