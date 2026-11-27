using System.Reflection;
using Battle;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("Sashindo/VFX/Awakening Duration Flicker")]
[DisallowMultipleComponent]
public sealed class AwakeningDurationFlicker : MonoBehaviour
{
    private const BindingFlags TimerFieldFlags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo RemainingTimeField =
        typeof(NewBattleController).GetField("_awakenTimeRemaining", TimerFieldFlags);
    private static readonly FieldInfo TotalTimeField =
        typeof(NewBattleController).GetField("_awakenMaxTime", TimerFieldFlags);

    [Header("Flicker Speed")]
    [Tooltip("Starting pulses per second.")]
    [SerializeField, Min(.1f)] private float startingFrequency = 1f;
    [Tooltip("Fastest pulses per second near the end of awakening.")]
    [SerializeField, Min(.1f)] private float maximumFrequency = 3f;
    [Tooltip("Percent of the current awakening duration used when maximum speed is reached. 80 means 20 percent remains.")]
    [SerializeField, Range(1f, 100f)] private float fullSpeedAtPercent = 80f;
    [Tooltip("Fraction of each beat used to brighten. Smaller values give a quicker rise and a longer fade.")]
    [SerializeField, Range(.08f, .5f)] private float pulseRiseFraction = 0.2f;
    [Tooltip("How quickly the effect eases back to a slower beat when a combo extends awakening.")]
    [SerializeField, Min(.1f)] private float extensionSmoothingRate = 10f;

    [Header("Playback")]
    [Tooltip("Optional wait before the sustained effect starts, for use with a separate entry effect.")]
    [SerializeField, Min(0f)] private float startDelay = 0f;
    [Tooltip("Seconds to fade this effect in after the start delay.")]
    [SerializeField, Min(0f)] private float startFadeSeconds = 0.18f;
    [Tooltip("Seconds remaining on the awakening timer when the effect starts fading out.")]
    [SerializeField, Min(0f)] private float endFadeSeconds = 0.16f;

    [Header("Screen Intensity")]
    [Tooltip("Overall intensity of the glow, strokes and any screen darkening.")]
    [SerializeField, Range(0f, 1f)] private float effectIntensity = 1f;
    [Tooltip("Screen darkening between pulses. Set both darkness values to 0 for glow and strokes alone.")]
    [SerializeField, Range(0f, 1f)] private float restingDarkness = 0f;
    [Tooltip("Screen darkening at the pulse peak.")]
    [SerializeField, Range(0f, 1f)] private float peakDarkness = 0f;
    [Tooltip("Canvas order relative to other canvases rendered by the same camera.")]
    [SerializeField] private int canvasSortingOrder = 30000;

    [Header("Edge Glow")]
    [Tooltip("Edges which emit light. Top, Left and Right are enabled by default.")]
    [SerializeField] private AwakeningScreenEdges glowEdges = ~AwakeningScreenEdges.None;
    [Tooltip("Peak edge opacity at the start, before Effect Intensity is applied.")]
    [SerializeField, Range(0f, 1f)] private float edgeGlowOpacity = 0.25f;
    [Tooltip("Multiplier for peak edge opacity toward the end of awakening.")]
    [SerializeField, Range(1f, 3f)] private float endGlowMultiplier = 3f;
    [SerializeField] private Color edgeGlowColor = new Color(1f, 0.58961684f, 0f, 1f);
    [Tooltip("Top and bottom glow width as a fraction of screen height.")]
    [SerializeField, Range(.01f, .45f)] private float horizontalGlowReach = 0.193f;
    [Tooltip("Left and right glow width as a fraction of screen width.")]
    [SerializeField, Range(.01f, .45f)] private float verticalGlowReach = 0.156f;
    [Tooltip("Fraction of peak glow that remains between beats.")]
    [SerializeField, Range(0f, 1f)] private float glowBetweenPulses = 0.45f;

    [Header("Brush Strokes")]
    [SerializeField] private AwakeningScreenEdges brushEdges = AwakeningScreenEdges.Default;
    [Tooltip("Number of strokes per beat when awakening begins.")]
    [SerializeField, Range(0, 32)] private int startingBrushCount = 8;
    [Tooltip("Number of strokes per beat near the end of awakening.")]
    [SerializeField, Range(0, 32)] private int maximumBrushCount = 18;
    [Tooltip("Typical brush length on a 960 by 540 reference canvas.")]
    [SerializeField, Range(5f, 180f)] private float brushLength = 80f;
    [Tooltip("Typical full stroke width on a 960 by 540 reference canvas.")]
    [SerializeField, Range(1f, 42f)] private float brushWidth = 13f;
    [Tooltip("How far the strokes slide from their starting positions on a 960 by 540 reference canvas.")]
    [SerializeField, Range(0f, 100f)] private float brushTravel = 13f;
    [Tooltip("Variation in length, width, angle and curve from one stroke to the next.")]
    [SerializeField, Range(0f, 1f)] private float brushVariation = 0.48f;
    [Tooltip("Gap between the randomly selected stroke origins on a 960 by 540 reference canvas.")]
    [SerializeField, Range(0f, 110f)] private float brushSpacing = 50f;
    [Tooltip("How far the random stroke origins can vary inward or outward from the screen edge.")]
    [SerializeField, Range(0f, 65f)] private float brushSpawnSpread = 20f;
    [Tooltip("Top left strokes lean down and right. Top right strokes lean down and left.")]
    [SerializeField] private bool mirrorTopBrushDirection = true;
    [Tooltip("Additional uneven bristles and flecks on the strokes.")]
    [SerializeField, Range(0f, 1f)] private float brushTextureDetail = 0.7f;
    [Tooltip("Fraction of the beat for which the strokes remain visible.")]
    [SerializeField, Range(.2f, 1f)] private float brushVisibleFraction = 0.84f;
    [Tooltip("Peak opacity of the strokes, before Effect Intensity is applied.")]
    [SerializeField, Range(0f, 1f)] private float brushOpacity = 0.95f;
    [Tooltip("Stroke tint. The tips and flecks get a lighter version of this color.")]
    [SerializeField] private Color brushColor = new Color(1f, 0.82f, 0.5f, 1f);

    private Canvas effectCanvas;
    private Image darknessImage;
    private RawImage glowImage;
    private AwakeningBrushGraphic brushes;
    private Texture2D glowTexture;
    private Vector2 cachedGlowReach;
    private AwakeningScreenEdges cachedGlowEdges;
    private float phase;
    private float displayedProgress;
    private float sinceAwakening;
    private float endVisibility = 1f;
    private bool playing;
    private bool hasBatch;

    private void OnEnable()
    {
        if (RemainingTimeField?.FieldType != typeof(float) || TotalTimeField?.FieldType != typeof(float))
        {
            Debug.LogWarning("AwakeningDurationFlicker could not read the awakening timer on NewBattleController.", this);
            return;
        }

        Play();
    }

    private void OnDisable() => Stop();

    private void OnDestroy()
    {
        if (effectCanvas != null)
            Destroy(effectCanvas.gameObject);
        if (glowTexture != null)
            Destroy(glowTexture);
    }

    private void LateUpdate()
    {
        if (!playing)
            return;

        var battle = NewBattleController.Instance;
        if (battle == null || !battle.isActiveAndEnabled || !battle.IsAwakenActive)
        {
            ResetPlayback();
            return;
        }

        float total = (float)TotalTimeField.GetValue(battle);
        float remaining = (float)RemainingTimeField.GetValue(battle);
        if (total <= 0f || remaining <= 0f)
        {
            ResetPlayback();
            return;
        }

        if (NewBattleController.IsAwakeningEntryPlaying)
        {
            if (effectCanvas != null) effectCanvas.enabled = false;
            return;
        }

        float deltaTime = Time.unscaledDeltaTime;
        sinceAwakening += deltaTime;
        float timeVisible = sinceAwakening - startDelay;
        if (timeVisible <= 0f)
        {
            if (effectCanvas != null && effectCanvas.enabled)
                effectCanvas.enabled = false;
            return;
        }

        if (effectCanvas == null)
            CreateOverlay();
        if (!effectCanvas.enabled)
            effectCanvas.enabled = true;
        effectCanvas.sortingOrder = canvasSortingOrder;

        float progress = 1f - Mathf.Clamp01(remaining / total);
        float blend = 1f - Mathf.Exp(-Mathf.Max(.1f, extensionSmoothingRate) * deltaTime);
        displayedProgress = Mathf.Max(progress, Mathf.Lerp(displayedProgress, progress, blend));

        float fullSpeed = Mathf.Clamp(fullSpeedAtPercent, 1f, 100f) * .01f;
        float low = Mathf.Max(.1f, startingFrequency);
        float high = Mathf.Max(low, maximumFrequency);
        float speed = Mathf.Lerp(low, high, displayedProgress / fullSpeed);
        float advance = phase + speed * Mathf.Min(deltaTime, timeVisible);
        phase = Mathf.Repeat(advance, 1f);
        if (!hasBatch || advance >= 1f)
        {
            int count = Mathf.RoundToInt(Mathf.Lerp(startingBrushCount, maximumBrushCount, displayedProgress));
            brushes.RebuildLayout(brushEdges, count, brushLength, brushWidth, brushTravel,
                brushVariation, brushSpacing, brushSpawnSpread, mirrorTopBrushDirection, brushTextureDetail);
            hasBatch = true;
        }

        float entryFade = startFadeSeconds > 0f ? Ease(timeVisible / startFadeSeconds) : 1f;
        float exitFade = endFadeSeconds > 0f ? Ease(remaining / endFadeSeconds) : 1f;
        endVisibility = Mathf.Min(exitFade, Mathf.Lerp(endVisibility, exitFade, blend));
        float visibility = Mathf.Clamp01(effectIntensity) * entryFade * endVisibility;
        float rise = Mathf.Clamp(pulseRiseFraction, .08f, .5f);
        float pulse = Pulse(phase, rise, 1f);
        UpdateScreen(pulse, visibility);

        float brushLife = Mathf.Clamp(brushVisibleFraction, .2f, 1f);
        float brushPulse = Pulse(phase, Mathf.Min(rise, brushLife * .75f), brushLife);
        float brushStrength = Mathf.Lerp(.64f, 1f, displayedProgress * displayedProgress);
        brushes.Show(phase / brushLife, brushColor, visibility * brushStrength * brushOpacity * brushPulse);
    }

    /// <summary>Restarts the sustained effect when awakening is active.</summary>
    public void Play()
    {
        ResetPlayback();
        playing = true;
    }

    public void Stop()
    {
        playing = false;
        ResetPlayback();
    }

    private void ResetPlayback()
    {
        if (effectCanvas != null && effectCanvas.enabled)
            effectCanvas.enabled = false;
        if (hasBatch && brushes != null)
            brushes.ClearLayout();
        hasBatch = false;
        phase = 0f;
        displayedProgress = 0f;
        sinceAwakening = 0f;
        endVisibility = 1f;
    }

    private void UpdateScreen(float pulse, float visibility)
    {
        darknessImage.color = new Color(0f, 0f, 0f, visibility * Mathf.Lerp(restingDarkness, peakDarkness, pulse));
        UpdateGlowTexture();
        float lastPeak = Mathf.Clamp01(edgeGlowOpacity * endGlowMultiplier);
        float peak = Mathf.Lerp(edgeGlowOpacity, lastPeak, displayedProgress * displayedProgress);
        Color glow = edgeGlowColor;
        glow.a *= visibility * peak * Mathf.Lerp(glowBetweenPulses, 1f, pulse);
        glowImage.color = glow;
    }

    private void CreateOverlay()
    {
        var stage = BattleStageController.Current;
        Camera camera = stage != null ? stage.StageCamera : null;
        if (camera == null || !camera.isActiveAndEnabled)
            camera = Camera.main;

        var canvasObject = new GameObject("Awakening flicker overlay", typeof(RectTransform), typeof(Canvas));
        canvasObject.layer = CameraLayer(camera, gameObject.layer);
        canvasObject.transform.SetParent(transform, false);
        effectCanvas = canvasObject.GetComponent<Canvas>();
        if (camera != null)
        {
            effectCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            effectCanvas.worldCamera = camera;
            effectCanvas.planeDistance = Mathf.Lerp(camera.nearClipPlane, camera.farClipPlane, .00005f);
        }
        else
        {
            effectCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }
        effectCanvas.overrideSorting = true;

        var parent = (RectTransform)canvasObject.transform;
        darknessImage = CreateGraphic<Image>("Screen darkening", parent);
        darknessImage.color = Color.clear;
        brushes = CreateGraphic<AwakeningBrushGraphic>("Random brush strokes", parent);
        glowImage = CreateGraphic<RawImage>("Edge glow", parent);
        glowImage.color = Color.clear;
    }

    private static int CameraLayer(Camera camera, int preferred)
    {
        if (camera == null || (camera.cullingMask & (1 << preferred)) != 0)
            return preferred;

        for (int layer = 0; layer < 32; layer++)
            if ((camera.cullingMask & (1 << layer)) != 0)
                return layer;
        return preferred;
    }

    private static T CreateGraphic<T>(string label, RectTransform parent) where T : MaskableGraphic
    {
        var child = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
        child.layer = parent.gameObject.layer;
        var graphic = child.GetComponent<T>();
        RectTransform rect = graphic.rectTransform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        graphic.raycastTarget = false;
        graphic.maskable = false;
        graphic.canvasRenderer.cullTransparentMesh = true;
        return graphic;
    }

    private void UpdateGlowTexture()
    {
        var reach = new Vector2(Mathf.Max(.01f, horizontalGlowReach), Mathf.Max(.01f, verticalGlowReach));
        if (glowTexture != null && reach == cachedGlowReach && glowEdges == cachedGlowEdges)
            return;

        const int width = 192, height = 108;
        var replacement = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "Awakening edge glow",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave
        };
        var pixels = new Color32[width * height];
        bool top = (glowEdges & AwakeningScreenEdges.Top) != 0;
        bool left = (glowEdges & AwakeningScreenEdges.Left) != 0;
        bool right = (glowEdges & AwakeningScreenEdges.Right) != 0;
        bool bottom = (glowEdges & AwakeningScreenEdges.Bottom) != 0;
        for (int y = 0; y < height; y++)
        {
            float v = y / (float)(height - 1);
            float horizontal = Mathf.Max(top ? 1f - Ease((1f - v) / reach.x) : 0f,
                bottom ? 1f - Ease(v / reach.x) : 0f);
            for (int x = 0; x < width; x++)
            {
                float u = x / (float)(width - 1);
                float vertical = Mathf.Max(left ? 1f - Ease(u / reach.y) : 0f,
                    right ? 1f - Ease((1f - u) / reach.y) : 0f);
                float edge = Mathf.Max(horizontal, vertical);
                pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(221f * edge * edge));
            }
        }

        replacement.SetPixels32(pixels);
        replacement.Apply(false, true);
        glowImage.texture = replacement;
        if (glowTexture != null)
            Destroy(glowTexture);
        glowTexture = replacement;
        cachedGlowReach = reach;
        cachedGlowEdges = glowEdges;
    }

    private static float Pulse(float t, float rise, float end)
    {
        return t < rise ? Ease(t / rise) : 1f - Ease((t - rise) / (end - rise));
    }

    private static float Ease(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}