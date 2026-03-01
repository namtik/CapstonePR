using UnityEngine;

public enum NodeType { Combat, Shop, Rest, Elite }

// 개별 노드의 상태와 동작을 관리하는 컴포넌트
public class MapNode : MonoBehaviour
{
    public int nodeIndex = -1;
    public NodeType nodeType = NodeType.Combat;
    public MapManager mapManager;
    public bool isCleared = false;

    private UnityEngine.UI.Image img;
    private bool isHighlighted = false;
    private Color highlightColor = Color.white;
    private bool isCurrentPosition = false; //  현재 플레이어 위치 표시

    void Awake()
    {
        img = GetComponent<UnityEngine.UI.Image>();
    }

    void Start()
    {
        UpdateVisual();
    }

    //  화면 표시 업데이트 (우선순위: 클리어 > 현재위치 > 하이라이트 > 타입)
    void UpdateVisual()
    {
        if (img == null) return;
        
        //  클리어된 노드는 회색
        if (isCleared)
        {
            img.color = Color.gray;
            transform.localScale = Vector3.one;
            return;
        }
        
        //  현재 위치는 특별 표시 (최우선)
        if (isCurrentPosition)
        {
            img.color = highlightColor;
            return;
        }
        
        //  강조 상태
        if (isHighlighted)
        {
            img.color = highlightColor;
            transform.localScale = Vector3.one;
            return;
        }

        //  기본 타입별 색상
        transform.localScale = Vector3.one;
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
        var btn = GetComponent<UnityEngine.UI.Button>();
        if (btn != null) btn.interactable = !cleared;
    }

    public void Highlight(Color color)
    {
        if (img == null) img = GetComponent<UnityEngine.UI.Image>();
        isHighlighted = true;
        highlightColor = color;
        if (img != null) img.color = highlightColor;
        UpdateVisual();
    }

    // 현재 플레이어 위치로 특별 하이라이트
    //  색상 + 크기 + 테두리 효과
    public void HighlightAsCurrentPosition(Color color, float scale)
    {
        if (img == null) img = GetComponent<UnityEngine.UI.Image>();
        
        isCurrentPosition = true;
        highlightColor = color;
        
        //  크기 확대
        transform.localScale = Vector3.one * scale;
        
        //  Outline 테두리 추가
        var outline = GetComponent<UnityEngine.UI.Outline>();
        if (outline == null)
        {
            outline = gameObject.AddComponent<UnityEngine.UI.Outline>();
        }
        outline.effectColor = Color.white;
        outline.effectDistance = new Vector2(5, -5);
        
        UpdateVisual();
        
        Debug.Log($"노드 {nodeIndex}가 현재 위치로 하이라이트됨");
    }

    public void OnClicked()
    {
        mapManager.OnNodeSelected(this);
    }
}
