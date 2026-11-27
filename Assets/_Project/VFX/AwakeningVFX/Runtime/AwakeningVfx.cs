using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Sashindo.VFX
{
    [AddComponentMenu("Sashindo/Awakening VFX")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    public sealed class AwakeningVfx : MonoBehaviour
    {
        private enum TitleTextOption
        {
            [InspectorName("한자 (覺醒)")] Hanja,
            [InspectorName("한글 (각성)")] Hangul,
            [InspectorName("Custom Texture")] CustomTexture
        }

        // The padded cells match the five TS7 textures in the prefab.
        private const int FrameWidth = 960;
        private const int FrameHeight = 540;
        private const int FrameCount = 28;
        private const int AtlasPadding = 4;
        private const int AtlasColumns = 2;
        private const int FramesPerPage = 6;
        private const float GroundUnits = 1024f;
        private const float MaxTitleBounceSeconds = .18f;
        private const float MaxTitleBounceShare = .24f;
        private const string ColorShaderResource = "SashindoAwakening/Colorize";
        private const string BrushTitleShaderResource = "SashindoAwakening/BrushTitle";
        private static readonly int TitleGradientEnabledId = Shader.PropertyToID("_TitleGradientEnabled");
        private static readonly int TitleGradientTopId = Shader.PropertyToID("_TitleGradientTop");
        private static readonly int TitleGradientBottomId = Shader.PropertyToID("_TitleGradientBottom");
        private static readonly int WriteProgressId = Shader.PropertyToID("_WriteProgress");
        private const float OutlineRadiusAt1080p = 15f;
        private static readonly int OutlineRadiusId = Shader.PropertyToID("_OutlineRadius");
        private const string HanjaTitleResource = "SashindoAwakening/HanjaTitle";
        private const string HangulTitleResource = "SashindoAwakening/HangulTitle";

        private static readonly Color BrushInk = new Color(.976f, .976f, .976f);
        private static readonly Color DefaultShade = new Color(.124f, .086f, .061f);

        [Header("Playback")]
        [Tooltip("Seconds for the incoming spiral, title writing, and title bounce. The spiral closes as the bounce finishes.")]
        [SerializeField, Min(0.1f)] private float entryDuration = .9f;
        [Tooltip("Seconds for the spiral to open and reveal the battle screen.")]
        [SerializeField, Min(0.1f)] private float returnDuration = .6f;
        [SerializeField] private bool playOnEnable;
        [SerializeField, Range(0f, 1.5f)] private float intensity = 1f;

        [Header("Full-screen overlay")]
        [Tooltip("Peak opacity of the spiral and shade combined.")]
        [SerializeField, Range(0f, 1f)] private float overlayOpacity = .65f;

        [Header("Title")]
        [SerializeField] private bool showTitle = true;
        [Tooltip("Choose one of the two brush titles or use the Awakening Title texture from the Textures section.")]
        [SerializeField] private TitleTextOption titleText = TitleTextOption.Hanja;
        [Tooltip("Reveal the selected brush title stroke by stroke. Turn off to show the full title at once.")]
        [SerializeField] private bool writeTitle = true;
        [Tooltip("Normalized viewport coordinates. The origin is the lower-left corner.")]
        [SerializeField] private Vector2 titlePosition = new Vector2(0.5f, 0.5f);
        [SerializeField, Min(0.05f)] private float titleScale = 1f;
        [Tooltip("How much larger the title gets at the bounce peak, as a percentage of Title Scale. 0 turns off the bounce.")]
        [SerializeField, Range(0f, 50f)] private float titleBounceStrength = 10f;

        [Header("Title Gradient")]
        [Tooltip("Apply the gradient to the title lettering. This takes priority over Title Color.")]
        [SerializeField] private bool useTitleGradient;
        [SerializeField, ColorUsage(false)] private Color titleGradientTop = new Color32(255, 245, 220, 255);
        [SerializeField, ColorUsage(false)] private Color titleGradientBottom = new Color32(212, 155, 80, 255);

        [Header("Optional ground afterglow")]
        [SerializeField] private Transform groundAnchor;
        [SerializeField, Min(0.01f)] private float worldDiameter = 5.4f;
        [SerializeField] private float groundOffset = 0.045f;
        [SerializeField] private Color groundTint = Color.white;

        [Header("Custom Colors")]
        [Tooltip("Recolor the existing textures while preserving their light and dark detail.")]
        [SerializeField] private bool useCustomColors;
        [SerializeField, ColorUsage(false)] private Color spiralColor = new Color(.92f, .64f, .30f);
        [SerializeField, ColorUsage(false)] private Color shadeColor = DefaultShade;
        [SerializeField, ColorUsage(false)] private Color titleColor = new Color(1f, .78f, .43f);
        [Tooltip("Used when the effect is played at a ground anchor or with PlayAt.")]
        [SerializeField, ColorUsage(false)] private Color groundColor = new Color(.97f, .70f, .35f);

        [Header("Textures")]
        [SerializeField] private Texture2D[] spiralAtlasPages;
        [SerializeField] private Texture2D awakeningTitle;
        [SerializeField] private Texture2D brushWave;
        [SerializeField] private Texture2D groundGlow;

        private Canvas overlayCanvas;
        private RectTransform overlayRect;
        private RawImage shadeImage;
        private RawImage spiralImage;
        private RawImage titleImage;
        private Canvas worldCanvas;
        private RawImage afterglowImage;
        private RawImage waveImage;
        private Material colorMaterial;
        private Material brushTitleMaterial;
        private Material textureTitleGradientMaterial;
        private Texture2D referenceTitle;
        private bool brushTitleActive;
        private bool customColorsActive;

        private bool playing;
        private bool playAfterglow;
        private float elapsed;
        private float playbackEntryDuration;
        private float playbackWriteDuration;
        private float playbackReturnDuration;

        public bool IsPlaying => playing;

        private void Awake()
        {
            InitializeCanvas();
        }

        private void OnEnable()
        {
            if (playOnEnable)
                Play();
        }

        private void OnDisable()
        {
            Stop();
        }

        private void OnDestroy()
        {
            if (worldCanvas != null)
                Destroy(worldCanvas.gameObject);
            if (colorMaterial != null)
                Destroy(colorMaterial);
            if (brushTitleMaterial != null)
                Destroy(brushTitleMaterial);
            if (textureTitleGradientMaterial != null)
                Destroy(textureTitleGradientMaterial);
        }

        private void Update()
        {
            if (!playing)
                return;

            // Loading an additive stage can stall a frame. Keep the writing visible after the stall.
            elapsed += Mathf.Min(Time.unscaledDeltaTime, .1f);
            if (elapsed >= playbackEntryDuration + playbackReturnDuration)
            {
                Stop();
                return;
            }

            DrawFrame(elapsed);
        }

        [ContextMenu("Play Awakening")]
        public void Play()
        {
            bool useGroundAnchor = groundAnchor != null;
            Begin(useGroundAnchor, useGroundAnchor ? groundAnchor.position : Vector3.zero);
        }

        public void PlayAt(Vector3 groundPosition)
        {
            Begin(true, groundPosition);
        }

        [ContextMenu("Stop Awakening")]
        public void Stop()
        {
            playing = false;
            if (overlayCanvas != null)
                overlayCanvas.enabled = false;
            if (worldCanvas != null)
                worldCanvas.enabled = false;
        }

        private void Begin(bool showGroundAfterglow, Vector3 groundPosition)
        {
            if (!Application.isPlaying || !isActiveAndEnabled)
                return;

            InitializeCanvas();
            BuildViews();
            Stop();

            playAfterglow = showGroundAfterglow;
            if (playAfterglow)
            {
                PrepareWorldCanvas(groundPosition);
                worldCanvas.enabled = true;
            }

            ApplyColorMode();

            overlayCanvas.enabled = true;
            elapsed = 0f;
            playbackEntryDuration = Mathf.Max(.1f, entryDuration);
            float bounceDuration = Mathf.Min(MaxTitleBounceSeconds, playbackEntryDuration * MaxTitleBounceShare);
            playbackWriteDuration = playbackEntryDuration - bounceDuration;
            playbackReturnDuration = Mathf.Max(.1f, returnDuration);
            DrawFrame(0f);
            playing = true;
        }

        private void InitializeCanvas()
        {
            if (overlayCanvas != null)
                return;

            overlayCanvas = GetComponent<Canvas>();
            overlayRect = (RectTransform)overlayCanvas.transform;
            overlayCanvas.enabled = false;
        }

        private void BuildViews()
        {
            if (spiralImage != null)
                return;

            shadeImage = CreateImage("Warm shade", overlayRect, Texture2D.whiteTexture);
            Stretch(shadeImage.rectTransform);
            spiralImage = CreateImage("Full-screen spiral", overlayRect, null);
            Stretch(spiralImage.rectTransform);
            titleImage = CreateImage("Awakening title", overlayRect, awakeningTitle);
        }

        private void PrepareWorldCanvas(Vector3 groundPosition)
        {
            if (worldCanvas == null)
            {
                var canvasObject = new GameObject(name + " (Ground VFX)", typeof(RectTransform), typeof(Canvas));
                canvasObject.layer = gameObject.layer;
                if (canvasObject.scene != gameObject.scene)
                    SceneManager.MoveGameObjectToScene(canvasObject, gameObject.scene);
                worldCanvas = canvasObject.GetComponent<Canvas>();
                worldCanvas.renderMode = RenderMode.WorldSpace;
                var rect = (RectTransform)canvasObject.transform;
                rect.sizeDelta = Vector2.one * GroundUnits;
                afterglowImage = CreateImage("Ground afterglow", rect, groundGlow);
                waveImage = CreateImage("Ground ripple", rect, brushWave);
            }

            Transform groundPlane = worldCanvas.transform;
            groundPlane.SetPositionAndRotation(
                groundPosition + Vector3.up * groundOffset,
                Quaternion.Euler(90f, 0f, 0f));
            groundPlane.localScale = Vector3.one * (Mathf.Max(0.01f, worldDiameter) / GroundUnits);
        }

        private void ApplyColorMode()
        {
            if (useCustomColors && colorMaterial == null)
                colorMaterial = CreateMaterial(ColorShaderResource);
            bool useBrushTitle = titleText != TitleTextOption.CustomTexture;
            if (useBrushTitle)
            {
                if (brushTitleMaterial == null)
                    brushTitleMaterial = CreateMaterial(BrushTitleShaderResource);
                string titleResource = titleText == TitleTextOption.Hangul ? HangulTitleResource : HanjaTitleResource;
                referenceTitle = Resources.Load<Texture2D>(titleResource);
            }

            customColorsActive = useCustomColors && colorMaterial != null;
            brushTitleActive = useBrushTitle && brushTitleMaterial != null && referenceTitle != null;
            Material material = customColorsActive ? colorMaterial : null;
            spiralImage.material = material;

            titleImage.texture = brushTitleActive ? referenceTitle : awakeningTitle;

            if (afterglowImage != null)
            {
                afterglowImage.material = material;
                waveImage.material = material;
            }
        }

        private static Material CreateMaterial(string resource)
        {
            Shader shader = Resources.Load<Shader>(resource);
            if (shader != null && shader.isSupported)
                return new Material(shader);

            Debug.LogWarning($"Awakening VFX could not load shader: {resource}");
            return null;
        }

        private void DrawFrame(float time)
        {
            Rect rect = overlayRect.rect;
            float width = rect.width > 0f ? rect.width : Screen.width;
            float height = rect.height > 0f ? rect.height : Screen.height;
            float aspectRatio = width / Mathf.Max(1f, height);
            float referenceScale = Mathf.Min(width / 1920f, height / 1080f);

            float entryProgress = Mathf.Clamp01(time / playbackEntryDuration);
            bool returning = time >= playbackEntryDuration;
            float returnProgress = Mathf.Clamp01((time - playbackEntryDuration) / playbackReturnDuration);
            float overlayStrength = Mathf.Clamp01(overlayOpacity * intensity);

            // The shade holds for the entire writing phase and fades during the return.
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            float shadeStrength = overlayStrength * (linear ? 1.23f : .98f);
            Color backdrop = customColorsActive ? Opaque(shadeColor)
                : linear ? DefaultShade : new Color(.156f, .082f, .059f);
            SetTint(shadeImage, backdrop, Mathf.Clamp01(shadeStrength) * (1f - Ease(returnProgress)));

            DrawSpiral(entryProgress, returnProgress, returning, aspectRatio, overlayStrength);
            DrawTitle(time, returnProgress, referenceScale);

            if (playAfterglow)
                DrawAfterglow(returnProgress);
        }

        private void DrawTitle(float time, float returnProgress, float referenceScale)
        {
            float titleAlpha = showTitle ? 1f - Ease(Progress(returnProgress, 0f, .78f)) : 0f;
            RectTransform title = titleImage.rectTransform;
            title.anchorMin = title.anchorMax = titlePosition;
            Vector2 size = brushTitleActive
                ? new Vector2(referenceTitle.width, referenceTitle.height)
                : new Vector2(613f, 204.333f);
            float bounceProgress = Progress(time, playbackWriteDuration, playbackEntryDuration);
            float bounce = TitleBounceScale(bounceProgress);
            title.sizeDelta = size * (referenceScale * titleScale * bounce);
            title.anchoredPosition = Vector2.zero;

            Material titleMaterial = customColorsActive ? colorMaterial : null;
            if (brushTitleActive)
            {
                titleMaterial = brushTitleMaterial;
                float writeProgress = writeTitle ? Mathf.Clamp01(time / playbackWriteDuration) : 1f;
                brushTitleMaterial.SetFloat(WriteProgressId, writeProgress);
                // Bounce changes the glyph's size. Its border keeps its screen thickness.
                brushTitleMaterial.SetFloat(OutlineRadiusId, OutlineRadiusAt1080p * referenceScale * titleScale);
                ApplyTitleGradient(brushTitleMaterial);
            }
            else if (useTitleGradient)
            {
                if (textureTitleGradientMaterial == null)
                    textureTitleGradientMaterial = CreateMaterial(ColorShaderResource);
                if (textureTitleGradientMaterial != null)
                {
                    titleMaterial = textureTitleGradientMaterial;
                    ApplyTitleGradient(titleMaterial);
                }
            }

            if (titleImage.material != (titleMaterial != null ? titleMaterial : titleImage.defaultMaterial))
                titleImage.material = titleMaterial;

            Color ink = brushTitleActive ? BrushInk : Color.white;
            SetTint(titleImage, customColorsActive ? Opaque(titleColor) : ink, intensity * titleAlpha);
        }

        private void ApplyTitleGradient(Material material)
        {
            material.SetFloat(TitleGradientEnabledId, useTitleGradient ? 1f : 0f);
            if (!useTitleGradient)
                return;

            material.SetColor(TitleGradientTopId, titleGradientTop);
            material.SetColor(TitleGradientBottomId, titleGradientBottom);
        }

        private float TitleBounceScale(float progress)
        {
            const float peakAt = .32f;
            const float settleAt = .72f;
            float amount = Mathf.Clamp(titleBounceStrength, 0f, 50f) / 100f;
            float peakScale = 1f + amount;
            float settleScale = 1f - amount * .15f;
            if (progress < peakAt)
                return Mathf.Lerp(1f, peakScale, Ease(Progress(progress, 0f, peakAt)));
            if (progress < settleAt)
                return Mathf.Lerp(peakScale, settleScale, Ease(Progress(progress, peakAt, settleAt)));
            return Mathf.Lerp(settleScale, 1f, Ease(Progress(progress, settleAt, 1f)));
        }

        private void DrawSpiral(float entryProgress, float returnProgress, bool returning,
            float aspectRatio, float overlayStrength)
        {
            int lastFrame = FrameCount - 1;
            int frame = returning
                ? Mathf.CeilToInt((1f - returnProgress) * lastFrame)
                : Mathf.FloorToInt(entryProgress * lastFrame);
            int pageIndex = frame / FramesPerPage;
            if (spiralAtlasPages == null || pageIndex >= spiralAtlasPages.Length || spiralAtlasPages[pageIndex] == null)
            {
                spiralImage.enabled = false;
                return;
            }

            Texture2D page = spiralAtlasPages[pageIndex];
            int cell = frame % FramesPerPage;
            int x = cell % AtlasColumns * (FrameWidth + AtlasPadding * 2) + AtlasPadding;
            int y = page.height - (cell / AtlasColumns * (FrameHeight + AtlasPadding * 2) + AtlasPadding + FrameHeight);
            float crop = aspectRatio / ((float)FrameWidth / FrameHeight);
            float cropWidth = Mathf.Min(1f, crop) * FrameWidth;
            float cropHeight = Mathf.Min(1f, 1f / Mathf.Max(.01f, crop)) * FrameHeight;
            spiralImage.texture = page;
            spiralImage.uvRect = new Rect(
                (x + (FrameWidth - cropWidth) * .5f) / page.width,
                (y + (FrameHeight - cropHeight) * .5f) / page.height,
                cropWidth / page.width,
                cropHeight / page.height);

            float fadeIn = Ease(Progress(entryProgress, 0f, .18f));
            float fadeOut = 1f - Ease(Progress(returnProgress, .62f, 1f));
            float spiralOpacity = overlayStrength * fadeIn * fadeOut;
            SetTint(spiralImage, customColorsActive ? Opaque(spiralColor) : Color.white, spiralOpacity);
        }

        private void DrawAfterglow(float returnProgress)
        {
            float remaining = 1f - Progress(returnProgress, .14f, .995f);
            float sweep = 1f - remaining * remaining * remaining;
            float alpha = Envelope(returnProgress, .20f, .46f, .59f, .995f);
            afterglowImage.rectTransform.sizeDelta = Vector2.one * (GroundUnits * Mathf.Lerp(.42f, 1.05f, sweep));
            waveImage.rectTransform.sizeDelta = Vector2.one * (GroundUnits * Mathf.Lerp(.30f, 1.1f, sweep));
            waveImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-8f, 12f, sweep));
            Color tint = customColorsActive ? Opaque(groundColor) : groundTint;
            SetTint(afterglowImage, tint, .64f * intensity * alpha);
            SetTint(waveImage, tint, .78f * intensity * alpha);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static RawImage CreateImage(string imageName, RectTransform parent, Texture2D art)
        {
            var imageObject = new GameObject(imageName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            imageObject.layer = parent.gameObject.layer;
            var image = imageObject.GetComponent<RawImage>();
            image.rectTransform.SetParent(parent, false);
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(.5f, .5f);
            image.raycastTarget = false;
            image.maskable = false;
            image.canvasRenderer.cullTransparentMesh = true;
            image.texture = art;
            image.enabled = false;
            return image;
        }

        private static Color Opaque(Color color)
        {
            color.a = 1f;
            return color;
        }

        private static void SetTint(RawImage image, Color tint, float opacity)
        {
            tint.a = Mathf.Clamp01(tint.a * opacity);
            image.enabled = tint.a > .002f && image.texture != null;
            image.color = tint;
        }

        private static float Progress(float time, float first, float last) => Mathf.InverseLerp(first, last, time);

        private static float Ease(float value) => value * value * (3f - 2f * value);


        private static float Envelope(float time, float start, float visible, float fade, float end)
        {
            return Ease(Progress(time, start, visible)) * (1f - Ease(Progress(time, fade, end)));
        }
    }
}
