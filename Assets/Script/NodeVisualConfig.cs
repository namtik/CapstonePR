using UnityEngine;

[CreateAssetMenu(fileName = "NodeVisualConfig", menuName = "Map/Node Visual Config")]
public class NodeVisualConfig : ScriptableObject
{
    [System.Serializable]
    public class NodeVisual
    {
        public NodeType nodeType;
        public Sprite sprite;
        public Color fallbackColor = Color.white;
    }

    [Header("��� Ÿ�Ժ� ���־� ����")]
    public NodeVisual[] nodeVisuals = new NodeVisual[]
    {
        new NodeVisual { nodeType = NodeType.Combat, fallbackColor = Color.red },
        new NodeVisual { nodeType = NodeType.Shop, fallbackColor = Color.green },
        new NodeVisual { nodeType = NodeType.Rest, fallbackColor = Color.cyan },
        new NodeVisual { nodeType = NodeType.Elite, fallbackColor = Color.yellow },
        new NodeVisual { nodeType = NodeType.Boss, fallbackColor = Color.magenta },
        new NodeVisual { nodeType = NodeType.Event, fallbackColor = Color.blue },
        new NodeVisual { nodeType = NodeType.Relic, fallbackColor = new Color(1f, 0.65f, 0.1f) }
    };

    [Header("���º� ����")]
    public Color clearedColor = Color.gray;
    
    public Sprite GetSpriteForType(NodeType type)
    {
        foreach (var visual in nodeVisuals)
        {
            if (visual.nodeType == type)
                return visual.sprite;
        }
        return null;
    }

    public Color GetColorForType(NodeType type)
    {
        foreach (var visual in nodeVisuals)
        {
            if (visual.nodeType == type)
                return visual.fallbackColor;
        }
        return Color.white;
    }
}
