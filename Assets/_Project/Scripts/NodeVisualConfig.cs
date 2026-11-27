using UnityEngine;

[CreateAssetMenu(fileName = "NodeVisualConfig", menuName = "Map/Node Visual Config")]
public class NodeVisualConfig : ScriptableObject
{
    // 노드 타입별 비주얼 정보(스프라이트/색/크기)
    [System.Serializable]
    public class NodeVisual
    {
        public NodeType nodeType; // 노드 타입
        public Sprite sprite; // 노드 스프라이트
        public Color fallbackColor = Color.white; // 스프라이트 없을 때 대체 색
        [Tooltip("이 노드 타입의 크기(px, 가로x세로). (0,0)이면 프리팹 기본 크기를 사용.")]
        public Vector2 size = Vector2.zero; // 노드 크기(px)
    }

    [Header("��� Ÿ�Ժ� ���־� ����")]
    public NodeVisual[] nodeVisuals = new NodeVisual[] // 노드 타입별 비주얼 설정 목록
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
    public Color clearedColor = Color.gray; // 클리어한 노드 색

    // 노드 타입에 해당하는 스프라이트를 반환한다
    public Sprite GetSpriteForType(NodeType type)
    {
        foreach (var visual in nodeVisuals)
        {
            if (visual.nodeType == type)
                return visual.sprite;
        }
        return null;
    }

    // 노드 타입에 해당하는 대체 색을 반환한다
    public Color GetColorForType(NodeType type)
    {
        foreach (var visual in nodeVisuals)
        {
            if (visual.nodeType == type)
                return visual.fallbackColor;
        }
        return Color.white;
    }

    // 노드 타입의 크기(px)를 반환한다((0,0)이면 미설정)
    public Vector2 GetSizeForType(NodeType type)
    {
        foreach (var visual in nodeVisuals)
        {
            if (visual.nodeType == type)
                return visual.size;
        }
        return Vector2.zero;
    }
}
