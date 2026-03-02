using UnityEngine;

public enum NodeType { Combat, Shop, Rest, Elite, Boss, Event }

// 개별 노드의 상태와 동작을 관리하는 컴포넌트
public class MapNode : MonoBehaviour
{
    public int nodeIndex = -1;
    public MapManager mapManager;
    public bool isCleared = false;
    public RoundData roundData;
    
    [Header("노드 타입 (직접 설정 가능)")]
    [SerializeField] private NodeType _nodeType = NodeType.Combat;
    
    public NodeType nodeType
    {
        get
        {
            // roundData가 있으면 그것 사용, 없으면 _nodeType 사용
            if (roundData != null)
                return roundData.roundType;
            return _nodeType;
        }
        set
        {
            _nodeType = value;
        }
    }

    [Header("비주얼 설정")]
    public NodeVisualConfig visualConfig;

    private UnityEngine.UI.Image img;
    private bool isHighlighted = false;
    private Color highlightColor = Color.white;
    private bool isCurrentPosition = false; // 각주: 현재 플레이어 위치 표시

    void Awake()
    {
        img = GetComponent<UnityEngine.UI.Image>();
        
        // Image의 raycastTarget 활성화
        if (img != null && !img.raycastTarget)
        {
            img.raycastTarget = true;
        }
    }

    void Start()
    {
        UpdateVisual();
    }

    // 각주: 화면 표시 업데이트 (우선순위: 클리어 > 현재위치 > 하이라이트 > 타입)
    void UpdateVisual()
    {
        if (img == null) return;
        
        // 각주: 클리어된 노드는 회색
        if (isCleared)
        {
            img.color = visualConfig != null ? visualConfig.clearedColor : Color.gray;
            // 크기는 유지 (클리어 후에도 현재 위치면 크기 유지)
            if (!isCurrentPosition)
            {
                transform.localScale = Vector3.one;
            }
            return;
        }
        
        // 각주: 현재 위치는 크기와 Outline으로만 표시 (색상은 타입별 유지)
        // isCurrentPosition일 때도 아래 타입별 색상/이미지 적용
        
        // 각주: 강조 상태
        if (isHighlighted)
        {
            img.color = highlightColor;
            if (!isCurrentPosition)
            {
                transform.localScale = Vector3.one;
            }
            return;
        }

        // 각주: 기본 타입별 스프라이트 또는 색상
        // 크기는 현재 위치가 아니면 1.0으로 리셋
        if (!isCurrentPosition)
        {
            transform.localScale = Vector3.one;
        }
        
        if (visualConfig != null)
        {
            Sprite typeSprite = visualConfig.GetSpriteForType(nodeType);
            if (typeSprite != null)
            {
                img.sprite = typeSprite;
                img.color = Color.white;
            }
            else
            {
                img.color = visualConfig.GetColorForType(nodeType);
            }
        }
        else
        {
            // 폴백: 기본 색상
            switch (nodeType)
            {
                case NodeType.Combat: img.color = Color.red; break;
                case NodeType.Shop: img.color = Color.green; break;
                case NodeType.Rest: img.color = Color.cyan; break;
                case NodeType.Elite: img.color = Color.yellow; break;
                case NodeType.Boss: img.color = Color.magenta; break;
                case NodeType.Event: img.color = Color.blue; break;
            }
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

    /// <summary>
    /// 현재 플레이어 위치로 특별 하이라이트
    /// 각주: 크기 확대로만 표시 (가장 깔끔)
    /// </summary>
    public void HighlightAsCurrentPosition(float scale)
    {
        if (img == null) img = GetComponent<UnityEngine.UI.Image>();
        
        isCurrentPosition = true;
        
        // 각주: 크기 확대만 적용
        transform.localScale = Vector3.one * scale;
        
        UpdateVisual();
    }

    /// <summary>
    /// 현재 위치 하이라이트 해제
    /// </summary>
    public void ClearCurrentPositionHighlight()
    {
        if (!isCurrentPosition) return;
        
        isCurrentPosition = false;
        
        // 크기를 원래대로 복원
        transform.localScale = Vector3.one;
        
        UpdateVisual();
    }

    public void OnClicked()
    {
        if (mapManager != null)
        {
            mapManager.OnNodeSelected(this);
        }
    }
}
