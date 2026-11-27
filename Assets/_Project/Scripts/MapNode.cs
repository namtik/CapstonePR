using UnityEngine;

// 맵 노드 종류 (전투/상점/휴식/정예/보스/이벤트/유물)
public enum NodeType { Combat, Shop, Rest, Elite, Boss, Event, Relic }

// 개별 맵 노드의 상태와 비주얼을 담당하는 컴포넌트
public class MapNode : MonoBehaviour
{
    public int nodeIndex = -1; // 맵 데이터상 노드 인덱스
    public MapManager mapManager; // 소속 맵 관리자
    public bool isCleared = false; // 클리어 여부
    public RoundData roundData; // 이 노드의 라운드 데이터

    [Header("��� Ÿ�� (���� ���� ����)")]
    [SerializeField] private NodeType _nodeType = NodeType.Combat; // 직렬화된 기본 노드 타입

    // roundData가 있으면 그 타입을, 없으면 _nodeType을 사용하는 노드 타입
    public NodeType nodeType
    {
        get
        {
            // roundData가 있으면 그것을, 없으면 _nodeType 사용
            if (roundData != null)
                return roundData.roundType;
            return _nodeType;
        }
        set
        {
            _nodeType = value;
        }
    }

    [Header("���־� ����")]
    public NodeVisualConfig visualConfig; // 노드 비주얼 설정

    private UnityEngine.UI.Image img; // 노드 이미지 컴포넌트
    private bool isHighlighted = false; // 강조 표시 여부
    private Color highlightColor = Color.white; // 강조 색상
    private bool isCurrentPosition = false; // 현재 플레이어 위치 여부

    // 초기화: 이미지 컴포넌트 확보 및 raycastTarget 활성화
    void Awake()
    {
        img = GetComponent<UnityEngine.UI.Image>();

        // Image의 raycastTarget 활성화
        if (img != null && !img.raycastTarget)
        {
            img.raycastTarget = true;
        }
    }

    // 시작 시 비주얼을 갱신한다
    void Start()
    {
        UpdateVisual();
    }

    // 상태 우선순위(클리어 > 현재위치 > 강조 > 타입)에 따라 노드 표시를 갱신한다
    void UpdateVisual()
    {
        if (img == null) return;

        // 클리어된 노드는 회색 처리
        if (isCleared)
        {
            img.color = visualConfig != null ? visualConfig.clearedColor : Color.gray;
            // 크기는 복원 (현재 위치는 크기 유지)
            if (!isCurrentPosition)
            {
                transform.localScale = Vector3.one;
            }
            return;
        }

        // 현재 위치는 크기 확대로만 표시 (색/이미지는 타입별 유지)

        // 강조 상태
        if (isHighlighted)
        {
            img.color = Color.black;
            if (!isCurrentPosition)
            {
                transform.localScale = Vector3.one;
            }
            return;
        }

        // 기본: 타입별 스프라이트 또는 색상, 현재 위치가 아니면 크기 1.0
        if (!isCurrentPosition)
        {
            img.color = Color.black;
            transform.localScale = Vector3.one;
        }

        if (visualConfig != null)
        {
            Sprite typeSprite = visualConfig.GetSpriteForType(nodeType);
            if (typeSprite != null)
            {
                img.sprite = typeSprite;
                img.color = Color.black;
            }
            else
            {
                img.color = visualConfig.GetColorForType(nodeType);
            }
        }
        else
        {
            // 폴백: 타입별 기본 색상
            switch (nodeType)
            {
                case NodeType.Combat: img.color = Color.red; break;
                case NodeType.Shop: img.color = Color.green; break;
                case NodeType.Rest: img.color = Color.cyan; break;
                case NodeType.Elite: img.color = Color.yellow; break;
                case NodeType.Boss: img.color = Color.magenta; break;
                case NodeType.Event: img.color = Color.blue; break;
                case NodeType.Relic: img.color = new Color(1f, 0.65f, 0.1f); break;
            }
        }
    }

    // 클리어 상태를 설정하고 비주얼/버튼을 갱신한다
    public void SetCleared(bool cleared)
    {
        isCleared = cleared;
        UpdateVisual();
        var btn = GetComponent<UnityEngine.UI.Button>();
        if (btn != null) btn.interactable = !cleared;
    }

    // 지정 색으로 노드를 강조 표시한다
    public void Highlight(Color color)
    {
        if (img == null) img = GetComponent<UnityEngine.UI.Image>();
        isHighlighted = true;
        highlightColor = color;
        if (img != null) img.color = highlightColor;
        UpdateVisual();
    }

    // 현재 플레이어 위치로 크기 확대 강조한다
    public void HighlightAsCurrentPosition(float scale)
    {
        if (img == null) img = GetComponent<UnityEngine.UI.Image>();

        isCurrentPosition = true;

        // 크기 확대만 적용
        transform.localScale = Vector3.one * scale;

        UpdateVisual();
    }

    // 현재 위치 강조를 해제하고 크기를 복원한다
    public void ClearCurrentPositionHighlight()
    {
        if (!isCurrentPosition) return;

        isCurrentPosition = false;

        // 크기를 원래대로 복원
        transform.localScale = Vector3.one;

        UpdateVisual();
    }

    // 클릭 시 맵 관리자에 노드 선택을 알린다
    public void OnClicked()
    {
        if (mapManager != null)
        {
            mapManager.OnNodeSelected(this);
        }
    }
}