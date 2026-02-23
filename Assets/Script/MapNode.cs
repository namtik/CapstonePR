using UnityEngine;

public enum NodeType { Combat, Shop, Rest, Elite }

// 맵의 각 노드 데이터 및 간단한 시각 요소 제어
public class MapNode : MonoBehaviour
{
    public int nodeIndex = -1;
    public NodeType nodeType = NodeType.Combat;
    public MapManager mapManager; // 부모 매니저 참조
    public bool isCleared = false;

    private UnityEngine.UI.Image img;
    private bool isHighlighted = false;
    private Color highlightColor = Color.white;

    void Awake()
    {
        img = GetComponent<UnityEngine.UI.Image>();
    }

    void Start()
    {
        UpdateVisual();
    }

    void UpdateVisual()
    {
        if (img == null) return;
        // If cleared, show gray
        if (isCleared)
        {
            img.color = Color.gray;
            return;
        }
        // Highlight overrides default type color
        if (isHighlighted)
        {
            img.color = highlightColor;
            return;
        }

        switch (nodeType)
        {
            case NodeType.Combat: img.color = Color.red; break;
            case NodeType.Shop: img.color = Color.green; break;
            case NodeType.Rest: img.color = Color.cyan; break;
            case NodeType.Elite: img.color = Color.yellow; break;
        }
    }

    public void SetCleared(bool cleared)
    {
        isCleared = cleared;
        UpdateVisual();
        // disable interaction when cleared
        var btn = GetComponent<UnityEngine.UI.Button>();
        if (btn != null) btn.interactable = !cleared;
    }

    // Highlight node with a specific color (e.g., start or boss)
    public void Highlight(Color color)
    {
        if (img == null) img = GetComponent<UnityEngine.UI.Image>();
        isHighlighted = true;
        highlightColor = color;
        if (img != null) img.color = highlightColor;
        UpdateVisual();
    }

    public void OnClicked()
    {
        mapManager.OnNodeSelected(this);
    }
}
