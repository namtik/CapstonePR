using UnityEngine;

// UI Canvas 내 맵 노드 스크롤 컨트롤러
// RectTransform을 이동시켜 현재 노드를 화면 중앙에 표시
public class MapScrollController : MonoBehaviour
{
    [Header("스크롤 설정")]
    [Tooltip("스크롤할 RectTransform (nodesParent)")]
    public RectTransform scrollTarget;

    [Tooltip("스크롤 속도 (낮을수록 부드럽게)")]
    [Range(1f, 20f)]
    public float smoothSpeed = 8f;

    [Header("스크롤 제한")]
    [Tooltip("X축 최소 위치 (왼쪽 끝)")]
    public float minX = -1400f;

    [Tooltip("X축 최대 위치 (오른쪽 끝)")]
    public float maxX = 1400f;

    private Vector2 targetPosition;
    private bool isMoving = false;

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
        if (scrollTarget == null || !isMoving) return;

        scrollTarget.anchoredPosition = Vector2.Lerp(
            scrollTarget.anchoredPosition,
            targetPosition,
            smoothSpeed * Time.deltaTime
        );

        // 목표에 거의 도달하면 정확히 맞추고 정지
        if (Vector2.Distance(scrollTarget.anchoredPosition, targetPosition) < 1f)
        {
            scrollTarget.anchoredPosition = targetPosition;
            isMoving = false;
        }
    }

    // 특정 노드 위치로 스크롤 (부드럽게)
    // 노드의 anchoredPosition을 중심으로 이동
    public void ScrollToNode(RectTransform nodeTransform)
    {
        if (scrollTarget == null || nodeTransform == null) return;

        // 노드의 anchoredPosition을 반대로 이동
        // (노드가 오른쪽에 있으면 scrollTarget을 왼쪽으로 이동)
        float targetX = -nodeTransform.anchoredPosition.x;

        // 각주: 경계 제한
        targetX = Mathf.Clamp(targetX, minX, maxX);

        targetPosition = new Vector2(targetX, scrollTarget.anchoredPosition.y);
        isMoving = true;

        Debug.Log($"스크롤 목표: {targetPosition.x} (노드 위치: {nodeTransform.anchoredPosition.x})");
    }

    // 특정 위치로 즉시 이동
    //  맵 생성 직후 시작 노드로 이동 시 사용
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

    // anchoredPosition으로 직접 스크롤
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
