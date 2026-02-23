using UnityEngine;

public enum NodeType { Combat, Shop, Rest, Elite }

// 맵의 각 노드 데이터 및 간단한 시각 요소 제어
// MapNode: 맵에서 각 노드를 나타내는 컴포넌트
// MapManager가 생성 시 이 컴포넌트를 추가하고 초기값을 설정
public class MapNode : MonoBehaviour
{
    // 노드의 인덱스 (MapManager 또는 MapData에서 할당)
    public int nodeIndex = -1;

    // 노드 유형
    public NodeType nodeType = NodeType.Combat;

    // 이 노드를 관리하는 MapManager 참조
    public MapManager mapManager; // 부모 매니저 참조

    // 노드가 이미 클리어되었는지 여부
    // 각주: 클리어된 노드는 회색으로 표시되고 상호작용 불가로 변경
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

    // 화면 표시 업데이트
    // 각주: isCleared > isHighlighted > nodeType 순으로 우선순위임
    void UpdateVisual()
    {
        if (img == null) return;
        // 클리어된 노드는 회색으로 고정
        if (isCleared)
        {
            img.color = Color.gray;
            return;
        }
        // 강조 상태면 강조 색상 사용
        if (isHighlighted)
        {
            img.color = highlightColor;
            return;
        }

        // 스테이지 유형에 따른 기본 색상
        switch (nodeType)
        {
            case NodeType.Combat: img.color = Color.red; break;
            case NodeType.Shop: img.color = Color.green; break;
            case NodeType.Rest: img.color = Color.cyan; break;
            case NodeType.Elite: img.color = Color.yellow; break;
        }
    }

    // 노드를 클리어 상태로 설정
    // GameManager에서 클리어 기록을 남긴 뒤 MapManager가 이 메서드를 호출하여
    // 각 노드의 시각/인터랙션을 갱신해야 하는데...
    public void SetCleared(bool cleared)
    {
        isCleared = cleared;
        UpdateVisual();
        var btn = GetComponent<UnityEngine.UI.Button>();
        if (btn != null) btn.interactable = !cleared;
    }

    // 특정 색상으로 노드를 강조합니다 (예: 시작 노드/보스 노드 표시)
    public void Highlight(Color color)
    {
        if (img == null) img = GetComponent<UnityEngine.UI.Image>();
        isHighlighted = true;
        highlightColor = color;
        if (img != null) img.color = highlightColor;
        UpdateVisual();
    }

    // 버튼 클릭 핸들러: MapManager에 선택된 노드를 전달
    public void OnClicked()
    {
        mapManager.OnNodeSelected(this);
    }
}
