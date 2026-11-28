using UnityEngine;

// UI 맵을 마우스 드래그로 좌우 둘러보고 선택 노드를 중앙에 맞추는 컨트롤러
public class MapScrollController : MonoBehaviour
{
    [Header("스크롤 대상")]
    [Tooltip("움직일 RectTransform (nodesParent)")]
    public RectTransform scrollTarget; // 스크롤시킬 대상 트랜스폼

    [Tooltip("자동 스크롤 속도 (노드 이동 시 부드럽게)")]
    [Range(1f, 20f)]
    public float smoothSpeed = 8f; // 자동 스크롤 속도

    [Header("드래그 설정")]
    [Tooltip("마우스로 화면을 눌러 좌우로 둘러보기")]
    public bool enableDrag = true; // 드래그 둘러보기 사용 여부

    [Tooltip("드래그 감도 (1 = 화면 픽셀과 1:1)")]
    [Range(0.1f, 3f)]
    public float dragSensitivity = 1.2f; // 드래그 감도

    [Tooltip("이 거리(화면 픽셀) 이상 움직여야 드래그로 판정 — 노드를 누른 채 끌어도 스크롤되고, 살짝 누르면 클릭")]
    public float dragThreshold = 8f; // 드래그 판정 거리

    /// <summary>마지막 누름이 드래그로 판정됐는지 — 노드 클릭 처리에서 확인해 드래그 후 클릭을 무시한다.</summary>
    public static bool LastGestureWasDrag { get; private set; }

    [Header("스크롤 범위")]
    [Tooltip("X축 최소 위치 (왼쪽 끝)")]
    public float minX = -3000f; // 스크롤 최소 X

    [Tooltip("X축 최대 위치 (오른쪽 끝)")]
    public float maxX = 500f; // 스크롤 최대 X

    [Tooltip("맵 생성 시 노드 위치로 스크롤 범위를 자동 계산 (minX/maxX 무시)")]
    public bool autoRangeFromNodes = true; // 노드 기준 자동 범위 사용 여부

    [Tooltip("자동 범위일 때 양 끝 노드 바깥으로 더 끌 수 있는 여유 (캔버스 단위)")]
    public float rangePadding = 400f; // 자동 범위 여유

    private Vector2 targetPosition; // 자동 스크롤 목표 위치
    private bool isMoving = false; // 자동 스크롤 진행 중 여부

    private Canvas _canvas; // 캔버스 캐시
    private bool _dragging = false; // 드래그 중 여부
    private bool _pressing = false; // 맵 위에서 누르고 있는 중 여부
    private Vector2 _pressStartPos; // 누르기 시작한 위치
    private Vector2 _lastPointerPos; // 직전 포인터 위치

    // 시작 시 스크롤 목표를 현재 위치로 초기화한다
    void Start()
    {
        if (scrollTarget == null)
        {
            Debug.LogWarning("MapScrollController: scrollTarget이 설정되지 않았습니다.");
        }
        else
        {
            targetPosition = scrollTarget.anchoredPosition;
        }
    }

    // 매 프레임 드래그를 처리하고 자동 스크롤을 보간한다
    void Update()
    {
        HandleDrag();

        if (scrollTarget == null || !isMoving) return;

        scrollTarget.anchoredPosition = Vector2.Lerp(
            scrollTarget.anchoredPosition,
            targetPosition,
            smoothSpeed * Time.unscaledDeltaTime
        );

        // 목표에 거의 도달하면 정확히 맞추고 정지
        if (Vector2.Distance(scrollTarget.anchoredPosition, targetPosition) < 1f)
        {
            scrollTarget.anchoredPosition = targetPosition;
            isMoving = false;
        }
    }

    // 마우스 드래그 둘러보기

    // 마우스 드래그 입력을 받아 맵을 좌우로 이동시킨다
    void HandleDrag()
    {
        if (!enableDrag || scrollTarget == null) return;

        // 인벤토리·도감·설정 창이 열려 있으면 맵 드래그를 막는다
        if (Battle.UI.InventoryPanelController.IsOpen || Battle.UI.CodexPanelController.IsOpen || SettingPanel.IsOverlayOpen)
        {
            _dragging = false;
            _pressing = false;
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            LastGestureWasDrag = false;
            _dragging = false;
            // 예전엔 포인터 아래에 UI가 하나라도 있으면 막았다 -> 맵 배경·장식 이미지 위에서도 안 잡혀 잡히는 범위가 매우 좁았다.
            // 이제는 맵 캔버스의 '누를 수 없는' 그래픽(배경·길·장식) 위면 잡히고, 노드 버튼 등 상호작용 UI나 다른 창 위에서만 막는다.
            _pressing = CanStartDragAt(Input.mousePosition);
            _pressStartPos = Input.mousePosition;
            _lastPointerPos = Input.mousePosition;
            return;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            _dragging = false;
            _pressing = false;
        }

        if (!_pressing || !Input.GetMouseButton(0)) return;

        Vector2 current = Input.mousePosition;
        if (!_dragging)
        {
            // 임계 거리를 넘기 전까지는 클릭 후보로 둔다
            if (Mathf.Abs(current.x - _pressStartPos.x) < dragThreshold) return;
            _dragging = true;
            LastGestureWasDrag = true; // 이번 누름에서 노드 클릭은 무시
        }

        float deltaX = current.x - _lastPointerPos.x;
        _lastPointerPos = current;

        if (Mathf.Abs(deltaX) < 0.01f) return;

        // 화면 픽셀 -> 캔버스 좌표로 보정해 손가락과 1:1로 따라오게 한다.
        float scale = ResolveCanvasScale();
        float newX = scrollTarget.anchoredPosition.x + (deltaX * dragSensitivity / scale);
        newX = Mathf.Clamp(newX, minX, maxX);

        scrollTarget.anchoredPosition = new Vector2(newX, scrollTarget.anchoredPosition.y);

        // 드래그 중에는 자동 스크롤을 멈춘다.
        targetPosition = scrollTarget.anchoredPosition;
        isMoving = false;
    }

    readonly System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult> _hits =
        new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>(); // UI 레이캐스트 결과 버퍼

    // 이 위치에서 맵 드래그를 시작해도 되는지 — 맨 위 UI가 맵 캔버스의 비상호작용 그래픽이면 허용
    bool CanStartDragAt(Vector2 screenPos)
    {
        // 맵 캔버스 위에 뜨는 사방비급 선택창 등 일시정지 창이 열려 있으면 뒤의 맵을 움직이지 않는다
        if (Time.timeScale == 0f) return false;
        // 사방비급 선택창은 맵 캔버스에 붙고 시간을 멈추지 않을 수 있어 직접 확인(누를 때 한 번만 찾음)
        if (FindFirstObjectByType<Battle.UI.ComboBookRewardPanelUI>() != null) return false;
        if (FindFirstObjectByType<StageTwoWalkOverlay>() != null) return false; // 스테이지 전환 연출 중

        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es == null) return true;

        var ped = new UnityEngine.EventSystems.PointerEventData(es) { position = screenPos };
        _hits.Clear();
        es.RaycastAll(ped, _hits);
        if (_hits.Count == 0) return true; // UI 밖(빈 곳)

        GameObject top = _hits[0].gameObject;
        if (top == null) return true;

        // 다른 캔버스(상단 바·팝업·보상창 등) 위에서는 시작하지 않는다
        if (_canvas == null && scrollTarget != null)
            _canvas = scrollTarget.GetComponentInParent<Canvas>();
        Canvas topCanvas = top.GetComponentInParent<Canvas>();
        if (_canvas != null && topCanvas != null && topCanvas.rootCanvas != _canvas.rootCanvas)
            return false;

        // 맵 노드 위는 활성/비활성 상관없이 허용 — 짧게 누르면 클릭, 끌면 스크롤(임계 거리로 구분)
        if (top.GetComponentInParent<MapNode>() != null) return true;

        // 자체 드래그를 받는 UI(스크롤뷰 등) 위에서는 시작하지 않는다
        if (UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IBeginDragHandler>(top) != null) return false;

        // 그 외 버튼은 비활성(누를 수 없음)일 때만 허용
        var selectable = top.GetComponentInParent<UnityEngine.UI.Selectable>();
        if (selectable != null) return !selectable.IsInteractable();
        if (UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IPointerClickHandler>(top) != null) return false;
        return true;
    }

    // 맵 노드의 가로 범위(가장 왼쪽/오른쪽 노드 X)로 스크롤 범위를 다시 잡는다
    public void SetRangeFromNodes(float minNodeX, float maxNodeX)
    {
        if (!autoRangeFromNodes) return;
        // 노드를 화면 중앙에 두는 위치 = -node.x 이므로 오른쪽 끝 노드가 minX, 왼쪽 끝 노드가 maxX가 된다
        minX = -maxNodeX - rangePadding;
        maxX = -minNodeX + rangePadding;
    }

    // 캔버스 스케일 팩터를 안전하게 반환한다
    float ResolveCanvasScale()
    {
        if (_canvas == null && scrollTarget != null)
            _canvas = scrollTarget.GetComponentInParent<Canvas>();

        float s = _canvas != null ? _canvas.scaleFactor : 1f;
        return s <= 0f ? 1f : s;
    }

    // 노드 자동 중앙정렬 (MapManager에서 호출)

    // 특정 노드 위치로 부드럽게 스크롤한다
    public void ScrollToNode(RectTransform nodeTransform)
    {
        if (scrollTarget == null || nodeTransform == null) return;

        // 노드의 anchoredPosition의 반대로 이동
        // (노드가 오른쪽에 있으면 scrollTarget을 왼쪽으로 이동)
        float targetX = -nodeTransform.anchoredPosition.x;

        // 범위 제한
        targetX = Mathf.Clamp(targetX, minX, maxX);

        targetPosition = new Vector2(targetX, scrollTarget.anchoredPosition.y);
        isMoving = true;

        Debug.Log($"스크롤 목표: {targetPosition.x} (노드 위치: {nodeTransform.anchoredPosition.x})");
    }

    // 특정 노드 위치로 즉시 이동(스냅)한다
    public void SnapToNode(RectTransform nodeTransform)
    {
        if (scrollTarget == null || nodeTransform == null) return;

        float targetX = -nodeTransform.anchoredPosition.x;
        targetX = Mathf.Clamp(targetX, minX, maxX);

        targetPosition = new Vector2(targetX, scrollTarget.anchoredPosition.y);
        scrollTarget.anchoredPosition = targetPosition;
        isMoving = false;

        Debug.Log($"스크롤 즉시 이동: {targetPosition.x}");
    }

    // 지정 좌표로 부드럽게 스크롤한다
    public void ScrollToPosition(Vector2 anchoredPos)
    {
        if (scrollTarget == null) return;

        float targetX = -anchoredPos.x;
        targetX = Mathf.Clamp(targetX, minX, maxX);

        targetPosition = new Vector2(targetX, scrollTarget.anchoredPosition.y);
        isMoving = true;
    }

    // 지정 좌표로 즉시 이동(스냅)한다
    public void SnapToPosition(Vector2 anchoredPos)
    {
        if (scrollTarget == null) return;

        float targetX = -anchoredPos.x;
        targetX = Mathf.Clamp(targetX, minX, maxX);

        targetPosition = new Vector2(targetX, scrollTarget.anchoredPosition.y);
        scrollTarget.anchoredPosition = targetPosition;
        isMoving = false;
    }
}
