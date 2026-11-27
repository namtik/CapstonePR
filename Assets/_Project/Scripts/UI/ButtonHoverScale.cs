using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class ButtonHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private float hoverScale = 1.06f;   // 호버 시 확대 배율
    [SerializeField] private float pressedScale = 1.0f;   // 눌렀을 때 배율
    [SerializeField] private float lerpSpeed = 14f;   // 스케일 보간 속도

    private RectTransform rectTransform;   // 대상 RectTransform 캐시
    private Vector3 baseScale;   // 기준(미호버) 스케일
    private Vector3 targetScale;   // 보간 목표 스케일
    private bool isHovering;   // 호버 중 여부
    private bool isPressing;   // 누르는 중 여부
    private bool baseScaleCaptured;   // 기준 스케일 캡처 여부

    // RectTransform을 캐시하고 기준 스케일을 캡처한다
    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        CaptureBaseScaleIfNeeded();
    }

    // 재활성화 시 기준 스케일을 보장하고 호버 상태를 초기화한다
    void OnEnable()
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        CaptureBaseScaleIfNeeded();

        // 재활성화 시 호버/프레스 상태를 초기화해 호버가 계속 걸려 있는 문제를 방지
        ResetHoverState();
    }

    // 아직 캡처하지 않았다면 현재 스케일을 기준 스케일로 1회 저장한다
    void CaptureBaseScaleIfNeeded()
    {
        if (baseScaleCaptured || rectTransform == null) return;

        baseScale = rectTransform.localScale;
        targetScale = baseScale;
        baseScaleCaptured = true;
    }

    // 매 프레임 현재 스케일을 목표 스케일로 보간한다
    void Update()
    {
        if (rectTransform == null) return;

        float t = 1f - Mathf.Exp(-Mathf.Max(1f, lerpSpeed) * Time.unscaledDeltaTime);
        rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, targetScale, t);
    }

    // 포인터 진입 시 호버 상태로 전환한다
    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;
        UpdateTargetScale();
    }

    // 포인터 이탈 시 호버/프레스 상태를 해제한다
    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;
        isPressing = false;
        UpdateTargetScale();
    }

    // 포인터 누름 시 프레스 상태로 전환한다
    public void OnPointerDown(PointerEventData eventData)
    {
        isPressing = true;
        UpdateTargetScale();
    }

    // 포인터 뗌 시 프레스 상태를 해제한다
    public void OnPointerUp(PointerEventData eventData)
    {
        isPressing = false;
        UpdateTargetScale();
    }

    // 호버/프레스 상태에 따라 목표 스케일을 갱신한다
    void UpdateTargetScale()
    {
        if (!isHovering)
        {
            targetScale = baseScale;
            return;
        }

        float scale = isPressing ? pressedScale : hoverScale;
        targetScale = baseScale * Mathf.Max(0.1f, scale);
    }

    // 외부에서 호버/프레스/보간 값을 설정한다
    public void Configure(float hoverScaleValue, float pressedScaleValue, float lerpSpeedValue)
    {
        hoverScale = hoverScaleValue;
        pressedScale = pressedScaleValue;
        lerpSpeed = lerpSpeedValue;
        UpdateTargetScale();
    }

    // 호버/프레스 상태를 즉시 해제하고 기준 스케일로 되돌린다
    public void ResetHoverState()
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        isHovering = false;
        isPressing = false;
        targetScale = baseScale;
        if (rectTransform != null)
            rectTransform.localScale = baseScale;
    }

    // 균일 배율로 기준 스케일을 설정한다
    public void SetBaseScale(float uniformScale)
    {
        SetBaseScale(Vector3.one * uniformScale);
    }

    // 외부에서 기준(미호버) 스케일을 갱신하고 상태를 초기화한다
    public void SetBaseScale(Vector3 newBaseScale)
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        baseScale = newBaseScale;
        baseScaleCaptured = true;
        ResetHoverState();
    }
}
