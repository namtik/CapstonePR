using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class ButtonHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private float hoverScale = 1.06f;
    [SerializeField] private float pressedScale = 1.0f;
    [SerializeField] private float lerpSpeed = 14f;

    private RectTransform rectTransform;
    private Vector3 baseScale;
    private Vector3 targetScale;
    private bool isHovering;
    private bool isPressing;
    private bool baseScaleCaptured;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        CaptureBaseScaleIfNeeded();
    }

    void OnEnable()
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        CaptureBaseScaleIfNeeded();

        // 재활성화 시 호버/프레스 상태를 초기화해 "호버가 계속 걸려 있는" 문제를 방지한다.
        // (예전에는 OnEnable에서 현재 localScale을 base로 다시 캡처해, 확대된 상태가 base로 굳는 버그가 있었다.)
        ResetHoverState();
    }

    void CaptureBaseScaleIfNeeded()
    {
        if (baseScaleCaptured || rectTransform == null) return;

        baseScale = rectTransform.localScale;
        targetScale = baseScale;
        baseScaleCaptured = true;
    }

    void Update()
    {
        if (rectTransform == null) return;

        float t = 1f - Mathf.Exp(-Mathf.Max(1f, lerpSpeed) * Time.unscaledDeltaTime);
        rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, targetScale, t);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;
        UpdateTargetScale();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;
        isPressing = false;
        UpdateTargetScale();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressing = true;
        UpdateTargetScale();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressing = false;
        UpdateTargetScale();
    }

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

    public void Configure(float hoverScaleValue, float pressedScaleValue, float lerpSpeedValue)
    {
        hoverScale = hoverScaleValue;
        pressedScale = pressedScaleValue;
        lerpSpeed = lerpSpeedValue;
        UpdateTargetScale();
    }

    /// <summary>
    /// 호버/프레스 상태를 즉시 해제하고 기준 스케일로 되돌린다.
    /// UI를 다시 바인딩할 때 호출하면 "호버가 계속 걸려 있는" 현상을 막을 수 있다.
    /// </summary>
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

    public void SetBaseScale(float uniformScale)
    {
        SetBaseScale(Vector3.one * uniformScale);
    }

    /// <summary>
    /// 외부에서 기준(미호버) 스케일을 갱신한다. 이 컴포넌트는 매 프레임 localScale을 baseScale로
    /// 되돌리므로, 외부에서 직접 localScale을 바꾸면 무시된다. 그 대신 이 메서드로 base를 지정해야 한다.
    /// 호버 시에는 이 base 위에 hoverScale이 곱해진다.
    /// </summary>
    public void SetBaseScale(Vector3 newBaseScale)
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        baseScale = newBaseScale;
        baseScaleCaptured = true;
        ResetHoverState();
    }
}
