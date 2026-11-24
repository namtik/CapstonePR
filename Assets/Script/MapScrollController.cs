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
    public float dragSensitivity = 1f; // 드래그 감도

    [Header("스크롤 범위")]
    [Tooltip("X축 최소 위치 (왼쪽 끝)")]
    public float minX = -3000f; // 스크롤 최소 X

    [Tooltip("X축 최대 위치 (오른쪽 끝)")]
    public float maxX = 500f; // 스크롤 최대 X

    private Vector2 targetPosition; // 자동 스크롤 목표 위치
    private bool isMoving = false; // 자동 스크롤 진행 중 여부

    private Canvas _canvas; // 캔버스 캐시
    private bool _dragging = false; // 드래그 중 여부
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

    // ─────────────────────────────────────────────────────────────
    // 마우스 드래그 둘러보기
    // ─────────────────────────────────────────────────────────────

    // 마우스 드래그 입력을 받아 맵을 좌우로 이동시킨다
    void HandleDrag()
    {
        if (!enableDrag || scrollTarget == null) return;

        // 인벤토리·도감·설정 창이 열려 있으면 맵 드래그를 막는다
        if (Battle.UI.InventoryPanelController.IsOpen || Battle.UI.CodexPanelController.IsOpen || SettingPanel.IsOverlayOpen)
        {
            _dragging = false;
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            // 예전엔 포인터 아래에 UI가 하나라도 있으면 막았다 → 맵 배경·장식 이미지 위에서도 안 잡혀 잡히는 범위가 매우 좁았다.
            // 이제는 맵 캔버스의 '누를 수 없는' 그래픽(배경·길·장식) 위면 잡히고, 노드 버튼 등 상호작용 UI나 다른 창 위에서만 막는다.
            if (!CanStartDragAt(Input.mousePosition))
            {
                _dragging = false;
                return;
            }

            _dragging = true;
            _lastPointerPos = Input.mousePosition;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            _dragging = false;
        }

        if (!_dragging || !Input.GetMouseButton(0)) return;

        Vector2 current = Input.mousePosition;
        float deltaX = current.x - _lastPointerPos.x;
        _lastPointerPos = current;

        if (Mathf.Abs(deltaX) < 0.01f) return;

        // 화면 픽셀 → 캔버스 좌표로 보정해 손가락과 1:1로 따라오게 한다.
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

        // 버튼(노드 등)·클릭/드래그를 받는 UI 위에서는 시작하지 않는다(클릭이 우선)
        if (top.GetComponentInParent<UnityEngine.UI.Selectable>() != null) return false;
        if (UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IPointerClickHandler>(top) != null) return false;
        if (UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IBeginDragHandler>(top) != null) return false;
        return true;
    }

    // 캔버스 스케일 팩터를 안전하게 반환한다
    float ResolveCanvasScale()
    {
        if (_canvas == null && scrollTarget != null)
            _canvas = scrollTarget.GetComponentInParent<Canvas>();

        float s = _canvas != null ? _canvas.scaleFactor : 1f;
        return s <= 0f ? 1f : s;
    }

    // ─────────────────────────────────────────────────────────────
    // 노드 자동 중앙정렬 (MapManager에서 호출)
    // ─────────────────────────────────────────────────────────────

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
