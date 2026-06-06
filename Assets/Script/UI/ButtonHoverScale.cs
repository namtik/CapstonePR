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

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        baseScale = rectTransform.localScale;
        targetScale = baseScale;
    }

    void OnEnable()
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        baseScale = rectTransform.localScale;
        targetScale = baseScale;
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
}
