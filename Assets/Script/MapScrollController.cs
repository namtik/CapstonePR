using UnityEngine;

// UI 맵의 좌우 둘러보기 컨트롤러
// 마우스로 화면을 누른 채 좌우로 드래그하면 맵(nodesParent)이 따라 움직인다.
// 노드 선택 시에는 ScrollToNode/SnapToNode로 해당 노드를 화면 중앙으로 맞춘다.
public class MapScrollController : MonoBehaviour
{
    [Header("스크롤 대상")]
    [Tooltip("움직일 RectTransform (nodesParent)")]
    public RectTransform scrollTarget;

    [Tooltip("자동 스크롤 속도 (노드 이동 시 부드럽게)")]
    [Range(1f, 20f)]
    public float smoothSpeed = 8f;

    [Header("드래그 설정")]
    [Tooltip("마우스로 화면을 눌러 좌우로 둘러보기")]
    public bool enableDrag = true;

    [Tooltip("드래그 감도 (1 = 화면 픽셀과 1:1)")]
    [Range(0.1f, 3f)]
    public float dragSensitivity = 1f;

    [Header("스크롤 범위")]
    [Tooltip("X축 최소 위치 (왼쪽 끝)")]
    public float minX = -3000f;

    [Tooltip("X축 최대 위치 (오른쪽 끝)")]
    public float maxX = 500f;

    private Vector2 targetPosition;
    private bool isMoving = false;

    private Canvas _canvas;
    private bool _dragging = false;
    private Vector2 _lastPointerPos;

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
    void HandleDrag()
    {
        if (!enableDrag || scrollTarget == null) return;

        if (Input.GetMouseButtonDown(0))
        {
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

    // 특정 노드 위치로 스크롤 (부드럽게)
    // 노드의 anchoredPosition을 중심으로 이동
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

    // 특정 위치로 즉시 이동 (스냅)
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

    // anchoredPosition으로 부드럽게 스크롤
    public void ScrollToPosition(Vector2 anchoredPos)
    {
        if (scrollTarget == null) return;

        float targetX = -anchoredPos.x;
        targetX = Mathf.Clamp(targetX, minX, maxX);

        targetPosition = new Vector2(targetX, scrollTarget.anchoredPosition.y);
        isMoving = true;
    }

    // anchoredPosition으로 즉시 이동
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
