using UnityEngine;
using UnityEngine.UI;

namespace Sashindo.VFX
{
    [DisallowMultipleComponent]
    public sealed class GuardShieldEffect : MonoBehaviour
    {
        private static readonly int ExpansionId = Shader.PropertyToID("_Expansion");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int TalismanColorId = Shader.PropertyToID("_TalismanTint");
        private static readonly int ShieldColorId = Shader.PropertyToID("_ShieldTint");
        private static readonly int SolidFillId = Shader.PropertyToID("_SolidFill");
        private static readonly int SequenceId = Shader.PropertyToID("_GuardSequence");
        private const float ReboundTime = 0.50f;
        private const float TalismanFadeEnd = ReboundTime + 0.41f;
        private const float MinimumTalismanSpeed = 0.01f;
        private const float MinimumDuration = 0.01f;
        private const float MinimumBaseSpeed = 0.01f;
        private const float MaximumFadePortion = 0.60f;
        private const float TalismanGlowOpacity = 0.04f;
        private const float ShieldGlowOpacity = 0.10f;

        [SerializeField, ColorUsage(false)] private Color talismanColor = new Color(.96f, .73f, .30f, 1f);
        [SerializeField, ColorUsage(false)] private Color shieldColor = new Color(.96f, .73f, .30f, 1f);
        // Until explicitly adjusted, preserve the glow and color alpha saved by earlier prefabs.
        [SerializeField] private float talismanOpacity = -1f;
        [SerializeField] private float shieldOpacity = -1f;
        [SerializeField, Min(MinimumTalismanSpeed)] private float talismanPlaybackSpeed = 1f;
        [SerializeField, Range(0f, 100f)] private float talismanBounceStrength = 40f;
        [SerializeField] private bool useSpeedSettings;
        [SerializeField, Min(MinimumBaseSpeed)] private float baseSpeed = 2.5f;
        [SerializeField, Min(0f)] private float accelerationRate = 18f;

        // Keep the original easing for prefabs saved before the speed controls were added.
        [SerializeField, HideInInspector] private float expansionDuration = 0.90f;
        [SerializeField, HideInInspector] private float acceleration = 2.1f;

        [SerializeField, Min(MinimumDuration)]
        [Tooltip("펼쳐지는 후반부에 노란빛이 사라지는 시간(초).")]
        private float fadeDuration = 0.66f;

        [SerializeField]
        private bool playOnEnable = true;

        [SerializeField, HideInInspector] private RawImage image;
        private Material sourceMaterial;
        private Material playbackMaterial;
        private float elapsed;
        private float activeTalismanSpeed;

        private float TalismanSpeed => Mathf.Max(MinimumTalismanSpeed, talismanPlaybackSpeed);
        public float ShieldStartDelay => ReboundTime / TalismanSpeed;
        private float BaseSpeed => Mathf.Max(MinimumBaseSpeed, baseSpeed);
        private float AccelerationRate => Mathf.Max(0f, accelerationRate);

        // Solve 1 = speed * time + acceleration * time² / 2 without subtracting close floats.
        public float ExpansionTime => useSpeedSettings
            ? 2f / (BaseSpeed + Mathf.Sqrt(BaseSpeed * BaseSpeed + 2f * AccelerationRate))
            : Mathf.Max(MinimumDuration, expansionDuration);

        public float EffectiveFadeTime => ResolveFadeTime(ExpansionTime);
        public float TalismanOpacity => talismanOpacity < 0f ? talismanColor.a * TalismanGlowOpacity : Mathf.Clamp01(talismanOpacity);
        public float ShieldOpacity => shieldOpacity < 0f ? shieldColor.a * ShieldGlowOpacity : Mathf.Clamp01(shieldOpacity);

        private float ResolveFadeTime(float travelTime)
        {
            return Mathf.Min(Mathf.Max(MinimumDuration, fadeDuration), travelTime * MaximumFadePortion);
        }

        private void Awake()
        {
            if (image == null)
                image = GetComponentInChildren<RawImage>(true);

            if (image == null)
            {
                Debug.LogWarning("Guard Shield Effect needs a Raw Image.", this);
                enabled = false;
                return;
            }

            sourceMaterial = image.material;
            image.raycastTarget = false;
            image.enabled = false;
        }

        private void OnEnable()
        {
            if (playOnEnable)
                Play();
        }

        internal void PlayFromPrefab(GuardShieldEffect prefab)
        {
            talismanColor = prefab.talismanColor;
            shieldColor = prefab.shieldColor;
            talismanOpacity = prefab.talismanOpacity;
            shieldOpacity = prefab.shieldOpacity;
            talismanPlaybackSpeed = prefab.talismanPlaybackSpeed;
            talismanBounceStrength = prefab.talismanBounceStrength;
            useSpeedSettings = prefab.useSpeedSettings;
            baseSpeed = prefab.baseSpeed;
            accelerationRate = prefab.accelerationRate;
            expansionDuration = prefab.expansionDuration;
            acceleration = prefab.acceleration;
            fadeDuration = prefab.fadeDuration;
            Play();
        }

        [ContextMenu("Replay Guard Effect")]
        public void Play()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || image == null)
                return;

            if (playbackMaterial == null)
            {
                if (!sourceMaterial.HasProperty(ExpansionId) || !sourceMaterial.HasProperty(OpacityId))
                {
                    Debug.LogWarning("Guard Shield Effect needs its Guard Shield material.", this);
                    return;
                }

                playbackMaterial = new Material(sourceMaterial) { hideFlags = HideFlags.DontSave };
                image.material = playbackMaterial;
            }

            elapsed = 0f;
            activeTalismanSpeed = TalismanSpeed;
            ApplyFrame();
            image.enabled = true;
        }

        private void Update()
        {
            if (!image.enabled)
                return;

            elapsed += Time.unscaledDeltaTime;
            ApplyFrame();
        }

        private void ApplyFrame()
        {
            float travelTime = ExpansionTime;
            float fadeTime = ResolveFadeTime(travelTime);
            float fadeStart = travelTime - fadeTime;
            // The talisman uses its own clock. After the rebound, the shield advances in real seconds.
            float talismanTime = Mathf.Min(elapsed * activeTalismanSpeed, TalismanFadeEnd);
            float shieldTime = Mathf.Max(0f, elapsed - ReboundTime / activeTalismanSpeed);
            float travel = Mathf.Clamp01(shieldTime / travelTime);
            float fade = travel >= 1f ? 1f : Mathf.Clamp01((shieldTime - fadeStart) / fadeTime);

            float progress = useSpeedSettings
                ? Mathf.Clamp01(shieldTime * (BaseSpeed + 0.5f * AccelerationRate * shieldTime))
                : Mathf.Pow(travel, Mathf.Clamp(acceleration, 1f, 4f));
            Vector2 size = image.rectTransform.rect.size;
            float aspect = size.y > 0f && size.x > 0f ? size.x / size.y : 16f / 9f;
            float bounce = Mathf.Clamp01(talismanBounceStrength * 0.01f);
            playbackMaterial.SetVector(SequenceId, new Vector4(talismanTime, talismanTime - ReboundTime, aspect, bounce));
            // The shader derives the rim in sRGB, then converts both colors into the active color space.
            Color talisman = talismanColor;
            Color shield = shieldColor;
            float paperAlpha = TalismanOpacity;
            float guardAlpha = ShieldOpacity;
            // Below the original glow level, fade the whole layer. Above it, fill in the silhouette.
            talisman.a = Mathf.Clamp01(paperAlpha / TalismanGlowOpacity);
            shield.a = Mathf.Clamp01(guardAlpha / ShieldGlowOpacity);
            playbackMaterial.SetVector(TalismanColorId, talisman);
            playbackMaterial.SetVector(ShieldColorId, shield);
            playbackMaterial.SetVector(SolidFillId, new Vector4(
                Mathf.InverseLerp(TalismanGlowOpacity, 1f, paperAlpha),
                Mathf.InverseLerp(ShieldGlowOpacity, 1f, guardAlpha), 0f, 0f));
            playbackMaterial.SetFloat(ExpansionId, travel >= 1f ? 1f : progress);
            playbackMaterial.SetFloat(OpacityId, 1f - Mathf.SmoothStep(0f, 1f, fade));

            if (fade >= 1f && talismanTime >= TalismanFadeEnd)
                image.enabled = false;
        }

        private void OnDisable()
        {
            if (image != null)
                image.enabled = false;
        }

        private void OnDestroy()
        {
            if (playbackMaterial != null)
                Destroy(playbackMaterial);
        }
    }
}
