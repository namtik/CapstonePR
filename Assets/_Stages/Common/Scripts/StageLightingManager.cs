using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class StageLightingManager : MonoBehaviour
{
    public enum StageTime
    {
        Dawn,
        Morning,
        Afternoon,
        Sunset,
        Evening,
        Overcast,
        LateAfternoon,
        BlueHour
    }

    [Serializable]
    public struct LightingProfile
    {
        public Color directionalColor;
        [Min(0f)] public float directionalIntensity;
        [Range(0f, 1f)] public float shadowStrength;
        [Tooltip("Equator color in the Trilight ambient gradient.")]
        public Color ambientColor;
        public Color fogColor;
        [Min(0f)] public float fogStartDistance;
        [Min(0f)] public float fogEndDistance;
        [Range(0f, 1f)] public float reflectionIntensity;

        public LightingProfile(
            Color directionalColor,
            float directionalIntensity,
            float shadowStrength,
            Color ambientColor,
            Color fogColor,
            float fogStartDistance,
            float fogEndDistance,
            float reflectionIntensity)
        {
            this.directionalColor = directionalColor;
            this.directionalIntensity = directionalIntensity;
            this.shadowStrength = shadowStrength;
            this.ambientColor = ambientColor;
            this.fogColor = fogColor;
            this.fogStartDistance = fogStartDistance;
            this.fogEndDistance = fogEndDistance;
            this.reflectionIntensity = reflectionIntensity;
        }
    }

    
[Serializable]
    public struct AmbientProfile
    {
        public Color skyColor;
        public Color groundColor;

        public AmbientProfile(Color skyColor, Color groundColor)
        {
            this.skyColor = skyColor;
            this.groundColor = groundColor;
        }
    }

    [Serializable]
    public struct SkyProfile
    {
        public Color skyTop;
        public Color skyMiddle;
        public Color skyHorizon;
        public Color cloudLit;
        public Color cloudShadow;
        public Color sunGlow;
        [Range(0f, 25f)] public float sunCoreBrightness;
        [Range(0f, 1f)] public float cloudCoverage;
        [Range(1f, 1.6f)] public float cloudBrightness;
        [Range(0f, 2f)] public float silverLining;

        public SkyProfile(
            Color skyTop, Color skyMiddle, Color skyHorizon,
            Color cloudLit, Color cloudShadow, Color sunGlow,
            float sunCoreBrightness, float cloudCoverage,
            float cloudBrightness, float silverLining)
        {
            this.skyTop = skyTop;
            this.skyMiddle = skyMiddle;
            this.skyHorizon = skyHorizon;
            this.cloudLit = cloudLit;
            this.cloudShadow = cloudShadow;
            this.sunGlow = sunGlow;
            this.sunCoreBrightness = sunCoreBrightness;
            this.cloudCoverage = cloudCoverage;
            this.cloudBrightness = cloudBrightness;
            this.silverLining = silverLining;
        }
    }

    
[Serializable]
    public struct VolumeLook
    {
        [Range(-1f, 1f)] public float exposure;
        [Range(-30f, 30f)] public float contrast;
        [Range(-100f, 100f)] public float temperature;
        [Range(0f, 1f)] public float bloomIntensity;
        [Range(0f, 0.3f)] public float vignetteIntensity;
        public Color shadowTint;
        public Color highlightTint;
        [Range(-100f, 100f)] public float toneBalance;

        public VolumeLook(
            float exposure, float contrast, float temperature,
            float bloomIntensity, float vignetteIntensity,
            Color shadowTint, Color highlightTint, float toneBalance)
        {
            this.exposure = exposure;
            this.contrast = contrast;
            this.temperature = temperature;
            this.bloomIntensity = bloomIntensity;
            this.vignetteIntensity = vignetteIntensity;
            this.shadowTint = shadowTint;
            this.highlightTint = highlightTint;
            this.toneBalance = toneBalance;
        }
    }

    [Serializable]
    public struct CloudLook
    {
        [Range(0f, 1f)] public float coverage;
        [Range(1f, 1.6f)] public float brightness;
        [Range(0f, 2f)] public float silverLining;

        public CloudLook(float coverage, float brightness, float silverLining)
        {
            this.coverage = coverage;
            this.brightness = brightness;
            this.silverLining = silverLining;
        }
    }

    [Header("Scene References")]
    [SerializeField] private Light directionalLight;
    [SerializeField] private Material skyMaterial;
    [SerializeField] private Camera stageCamera;
    [SerializeField] private Volume stageVolume;

    [Header("Active Time")]
    [SerializeField] private StageTime selectedTime = StageTime.Afternoon;

    public enum CloudLayout { Layout1, Layout2, Layout3, Layout4, Layout5 }

    [Header("Cloud Layout")]
    [Tooltip("Choose a layout once on Play/scene entry. Consecutive random entries avoid the previous layout.")]
    [SerializeField] private bool randomizeCloudLayoutOnPlay = true;
    [Tooltip("Preview this layout immediately in Edit Mode; also use it on Play when random selection is off.")]
    [SerializeField] private CloudLayout fixedCloudLayout = CloudLayout.Layout1;

    public const int CloudLayoutCount = 5;
    private static readonly float[] CloudLayoutSeeds = { 3f, 3.137f, 3.419f, 3.673f, 4.251f };
    // Scale, clump scale, clump strength and detail multipliers relative to the source.
    private static readonly Vector4[] CloudLayoutShapes = {
        new Vector4(0.70f, 0.65f, 1.10f, 0.60f),
        new Vector4(1.35f, 1.15f, 0.80f, 0.70f),
        new Vector4(0.95f, 0.55f, 1.40f, 0.45f),
        new Vector4(0.82f, 1.25f, 0.65f, 0.80f),
        new Vector4(1.60f, 0.75f, 1.10f, 0.50f)
    };
    private static readonly float[] CloudLayoutMix = { 0.20f, 0.80f, 0.35f, 0.65f, 0.45f };
    private static readonly float[] CloudLayoutCoverage = { 0.025f, -0.045f, 0.035f, -0.025f, 0.01f };
    private static readonly System.Random CloudLayoutRandom = new System.Random();
    private static int lastRandomCloudLayout = -1;
    [NonSerialized] private Material cloudSourceMaterial;
    [NonSerialized] private Material cloudRuntimeMaterial;
    [NonSerialized] private Material cloudPreviousSkybox;
    [NonSerialized] private bool cloudInstanceIsPlaying;
    [NonSerialized] private int activeCloudLayoutIndex = -1;

    public int CurrentCloudLayoutIndex => activeCloudLayoutIndex >= 0
        ? activeCloudLayoutIndex : (int)fixedCloudLayout;

    public static float GetCloudLayoutSeed(int index)
    {
        return CloudLayoutSeeds[Mathf.Clamp(index, 0, CloudLayoutCount - 1)];
    }

    public void SetCloudLayoutByIndex(int index)
    {
        if (index < 0 || index >= CloudLayoutCount) return;
        fixedCloudLayout = (CloudLayout)index;
        if (Application.isPlaying) activeCloudLayoutIndex = index;
        Apply();
    }

    [ContextMenu("Reroll Clouds / Preview Another Layout")]
    public void RerollCloudLayout()
    {
        int previous = CurrentCloudLayoutIndex;
        int next = (previous + 1 + CloudLayoutRandom.Next(CloudLayoutCount - 1)) % CloudLayoutCount;
        SetCloudLayoutByIndex(next);
    }

    private static int ChooseRandomCloudLayout()
    {
        int previous = lastRandomCloudLayout;
#if UNITY_EDITOR
        previous = UnityEditor.SessionState.GetInt("StageLightingManager.LastCloudLayout", previous);
#endif
        int chosen = previous >= 0 && previous < CloudLayoutCount
            ? (previous + 1 + CloudLayoutRandom.Next(CloudLayoutCount - 1)) % CloudLayoutCount
            : CloudLayoutRandom.Next(CloudLayoutCount);
        lastRandomCloudLayout = chosen;
#if UNITY_EDITOR
        UnityEditor.SessionState.SetInt("StageLightingManager.LastCloudLayout", chosen);
#endif
        return chosen;
    }

    private void PrepareRuntimeCloudLayout()
    {
        if (!isActiveAndEnabled || skyMaterial == null || !skyMaterial.HasProperty("_CloudSeed")) return;
        bool playing = Application.isPlaying;
        if (cloudRuntimeMaterial != null &&
            (cloudSourceMaterial != skyMaterial || cloudInstanceIsPlaying != playing))
            ReleaseRuntimeCloudLayout();
        if (cloudRuntimeMaterial == null)
        {
            cloudSourceMaterial = skyMaterial;
            cloudPreviousSkybox = RenderSettings.skybox;
            cloudInstanceIsPlaying = playing;
            cloudRuntimeMaterial = new Material(cloudSourceMaterial)
            {
                name = cloudSourceMaterial.name + (playing ? " (Stage Runtime Clouds)" : " (Stage Preview Clouds)"),
                hideFlags = HideFlags.HideAndDontSave
            };
            activeCloudLayoutIndex = playing && randomizeCloudLayoutOnPlay
                ? ChooseRandomCloudLayout() : Mathf.Clamp((int)fixedCloudLayout, 0, CloudLayoutCount - 1);
        }
        if (!playing) activeCloudLayoutIndex = Mathf.Clamp((int)fixedCloudLayout, 0, CloudLayoutCount - 1);
        ApplyCloudLayoutShape();
    }

    private void ApplyCloudLayoutShape()
    {
        if (cloudRuntimeMaterial == null || cloudSourceMaterial == null || activeCloudLayoutIndex < 0) return;
        int index = activeCloudLayoutIndex;
        Vector4 shape = CloudLayoutShapes[index];
        cloudRuntimeMaterial.SetFloat("_CloudSeed", GetCloudLayoutSeed(index));
        cloudRuntimeMaterial.SetFloat("_CloudScale", cloudSourceMaterial.GetFloat("_CloudScale") * shape.x);
        cloudRuntimeMaterial.SetFloat("_CloudClumpScale", Mathf.Clamp(cloudSourceMaterial.GetFloat("_CloudClumpScale") * shape.y, 0.1f, 1.5f));
        cloudRuntimeMaterial.SetFloat("_CloudClump", Mathf.Clamp(cloudSourceMaterial.GetFloat("_CloudClump") * shape.z, 0f, 1.5f));
        cloudRuntimeMaterial.SetFloat("_CloudDetail", Mathf.Clamp(cloudSourceMaterial.GetFloat("_CloudDetail") * shape.w, 0f, 0.8f));
        cloudRuntimeMaterial.SetFloat("_CloudMix", CloudLayoutMix[index]);
    }

    private void ApplyCloudLayoutCoverage()
    {
        if (cloudRuntimeMaterial == null || activeCloudLayoutIndex < 0) return;
        // The time preset writes its base coverage first; never accumulate the offset.
        float coverage = cloudRuntimeMaterial.GetFloat("_CloudCoverage");
        if (coverage > 0.001f)
            cloudRuntimeMaterial.SetFloat("_CloudCoverage", Mathf.Clamp01(coverage + CloudLayoutCoverage[activeCloudLayoutIndex]));
    }

    private void ReleaseRuntimeCloudLayout()
    {
        if (cloudRuntimeMaterial == null) return;
        if (RenderSettings.skybox == cloudRuntimeMaterial)
            RenderSettings.skybox = cloudPreviousSkybox != null ? cloudPreviousSkybox : cloudSourceMaterial;
        if (Application.isPlaying) Destroy(cloudRuntimeMaterial);
        else DestroyImmediate(cloudRuntimeMaterial);
        cloudRuntimeMaterial = null;
        cloudSourceMaterial = null;
        cloudPreviousSkybox = null;
        activeCloudLayoutIndex = -1;
    }

    private void OnDestroy()
    {
        ReleaseRuntimeCloudLayout();
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void InstallCloudPreviewSaveHooks()
    {
        UnityEditor.SceneManagement.EditorSceneManager.sceneSaving -= BeforeSavingCloudPreview;
        UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += BeforeSavingCloudPreview;
        UnityEditor.SceneManagement.EditorSceneManager.sceneSaved -= AfterSavingCloudPreview;
        UnityEditor.SceneManagement.EditorSceneManager.sceneSaved += AfterSavingCloudPreview;
    }

    private static void BeforeSavingCloudPreview(UnityEngine.SceneManagement.Scene scene, string path)
    {
        if (Application.isPlaying) return;
        foreach (var manager in FindObjectsByType<StageLightingManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (manager.gameObject.scene == scene && RenderSettings.skybox == manager.cloudRuntimeMaterial && manager.cloudSourceMaterial != null)
                RenderSettings.skybox = manager.cloudSourceMaterial;
    }

    private static void AfterSavingCloudPreview(UnityEngine.SceneManagement.Scene scene)
    {
        if (Application.isPlaying) return;
        foreach (var manager in FindObjectsByType<StageLightingManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (manager.gameObject.scene == scene && manager.isActiveAndEnabled) manager.Apply();
    }
#endif

    [Header("Lighting Profiles")]
    [SerializeField, HideInInspector] private LightingProfile dawn = new LightingProfile(
        new Color(1f, 0.692f, 0.497f), 0.582f, 0.55f,
        new Color(0.465f, 0.333f, 0.344f), new Color(0.337f, 0.357f, 0.383f),
        22f, 95f, 0.5f);

    [SerializeField, HideInInspector] private LightingProfile morning = new LightingProfile(
        new Color(1f, 0.797f, 0.655f), 0.795f, 0.68f,
        new Color(0.392f, 0.390f, 0.520f), new Color(0.497f, 0.588f, 0.561f),
        22f, 95f, 0.8f);

    [SerializeField, HideInInspector] private LightingProfile afternoon = new LightingProfile(
        new Color(1f, 0.873f, 0.750f), 1.2f, 0.78f,
        new Color(0.349f, 0.463f, 0.717f), new Color(0.559f, 0.656f, 0.646f),
        24f, 100f, 1f);

    [SerializeField, HideInInspector] private LightingProfile sunset = new LightingProfile(
        new Color(1f, 0.646f, 0.405f), 0.765f, 0.72f,
        new Color(0.611f, 0.355f, 0.306f), new Color(0.507f, 0.418f, 0.378f),
        22f, 90f, 0.7f);

    [SerializeField, HideInInspector] private LightingProfile evening = new LightingProfile(
        new Color(0.54f, 0.67f, 0.9f), 1.25f, 1f,
        new Color(0.29f, 0.32f, 0.38f), new Color(0.09f, 0.11f, 0.16f),
        20f, 85f, 0.35f);

    
[SerializeField, HideInInspector] private LightingProfile overcast = new LightingProfile(
        new Color(0.80f, 0.86f, 0.90f), 0.72f, 0.12f,
        new Color(0.50f, 0.54f, 0.55f), new Color(0.59f, 0.65f, 0.66f),
        20f, 80f, 0.70f);

    [SerializeField, HideInInspector] private LightingProfile lateAfternoon = new LightingProfile(
        new Color(1f, 0.88f, 0.68f), 1.00f, 0.80f,
        new Color(0.44f, 0.48f, 0.55f), new Color(0.58f, 0.66f, 0.60f),
        24f, 100f, 0.90f);

    [SerializeField, HideInInspector] private LightingProfile blueHour = new LightingProfile(
        new Color(0.58f, 0.70f, 0.88f), 0.70f, 0.30f,
        new Color(0.48f, 0.55f, 0.68f), new Color(0.27f, 0.35f, 0.46f),
        20f, 85f, 0.55f);

    [Header("Ambient Gradient - Equator Uses Lighting Profile Ambient Color")]
    [SerializeField, HideInInspector] private AmbientProfile dawnAmbient = new AmbientProfile(
        new Color(0.55f, 0.43f, 0.47f), new Color(0.23f, 0.20f, 0.22f));
    [SerializeField, HideInInspector] private AmbientProfile morningAmbient = new AmbientProfile(
        new Color(0.48f, 0.52f, 0.66f), new Color(0.22f, 0.24f, 0.29f));
    [SerializeField, HideInInspector] private AmbientProfile afternoonAmbient = new AmbientProfile(
        new Color(0.46f, 0.58f, 0.80f), new Color(0.20f, 0.23f, 0.28f));
    [SerializeField, HideInInspector] private AmbientProfile sunsetAmbient = new AmbientProfile(
        new Color(0.46f, 0.48f, 0.58f), new Color(0.22f, 0.18f, 0.17f));
    [SerializeField, HideInInspector] private AmbientProfile eveningAmbient = new AmbientProfile(
        new Color(0.32f, 0.39f, 0.50f), new Color(0.12f, 0.14f, 0.18f));
    [SerializeField, HideInInspector] private AmbientProfile overcastAmbient = new AmbientProfile(
        new Color(0.58f, 0.63f, 0.65f), new Color(0.32f, 0.34f, 0.34f));
    [SerializeField, HideInInspector] private AmbientProfile lateAfternoonAmbient = new AmbientProfile(
        new Color(0.50f, 0.56f, 0.65f), new Color(0.25f, 0.26f, 0.28f));
    [SerializeField, HideInInspector] private AmbientProfile blueHourAmbient = new AmbientProfile(
        new Color(0.52f, 0.61f, 0.75f), new Color(0.23f, 0.27f, 0.35f));

    [Header("Additional Sky Looks")]
    [SerializeField, HideInInspector] private SkyProfile overcastSky = new SkyProfile(
        new Color(0.38f, 0.47f, 0.54f),
        new Color(0.56f, 0.63f, 0.66f),
        new Color(0.74f, 0.78f, 0.78f),
        new Color(0.78f, 0.83f, 0.85f),
        new Color(0.39f, 0.49f, 0.56f),
        new Color(0.73f, 0.77f, 0.80f),
        0.5f, 0.74f, 1.02f, 0.15f);

    [SerializeField, HideInInspector] private SkyProfile lateAfternoonSky = new SkyProfile(
        new Color(0.09f, 0.25f, 0.58f),
        new Color(0.30f, 0.52f, 0.78f),
        new Color(0.82f, 0.85f, 0.76f),
        new Color(1.00f, 0.95f, 0.84f),
        new Color(0.45f, 0.54f, 0.65f),
        new Color(1.00f, 0.82f, 0.58f),
        8f, 0.44f, 1.25f, 0.8f);

    [SerializeField, HideInInspector] private SkyProfile blueHourSky = new SkyProfile(
        new Color(0.035f, 0.08f, 0.18f),
        new Color(0.10f, 0.19f, 0.32f),
        new Color(0.30f, 0.40f, 0.50f),
        new Color(0.48f, 0.56f, 0.67f),
        new Color(0.20f, 0.29f, 0.42f),
        new Color(0.20f, 0.30f, 0.43f),
        0f, 0.55f, 1.10f, 0.2f);

    
[Header("Camera-Facing Sunset")]
    [SerializeField, HideInInspector, Range(-45f, 45f)] private float lateAfternoonSunOffset = 10f;
    [SerializeField, HideInInspector, Range(-45f, 45f)] private float lateAfternoonSunTilt = 0f;
    [SerializeField, HideInInspector, Range(-45f, 45f)] private float sunsetSunOffset = 10f;
    [SerializeField, HideInInspector, Range(-45f, 45f)] private float sunsetSunTilt = -12f;

    [Header("Original Preset Clouds")]
    [SerializeField, HideInInspector] private CloudLook dawnClouds = new CloudLook(0.55f, 1.16f, 0.55f);
    [SerializeField, HideInInspector] private CloudLook morningClouds = new CloudLook(0.48f, 1.23f, 0.85f);
    [SerializeField, HideInInspector] private CloudLook afternoonClouds = new CloudLook(0.50f, 1.28f, 1f);
    [SerializeField, HideInInspector] private CloudLook sunsetClouds = new CloudLook(0.42f, 1.18f, 0.70f);
    [SerializeField, HideInInspector] private CloudLook eveningClouds = new CloudLook(0.34f, 1.08f, 0.15f);


    [SerializeField, HideInInspector] private VolumeLook dawnVolume = new VolumeLook(
        0.08f, 3f, 12f, 0f, 0.04f,
        new Color(0.40f, 0.46f, 0.55f), new Color(0.60f, 0.54f, 0.44f), 10f);
    [SerializeField, HideInInspector] private VolumeLook morningVolume = new VolumeLook(
        0.03f, 2f, 4f, 0f, 0.02f,
        new Color(0.45f, 0.49f, 0.54f), new Color(0.55f, 0.54f, 0.48f), 0f);
    [SerializeField, HideInInspector] private VolumeLook afternoonVolume = new VolumeLook(
        0f, 0f, 0f, 0f, 0f,
        new Color(0.48f, 0.51f, 0.54f), new Color(0.54f, 0.52f, 0.49f), 0f);
    [SerializeField, HideInInspector] private VolumeLook sunsetVolume = new VolumeLook(
        -0.05f, 6f, 16f, 0f, 0.06f,
        new Color(0.38f, 0.44f, 0.56f), new Color(0.66f, 0.55f, 0.42f), 15f);
    [SerializeField, HideInInspector] private VolumeLook eveningVolume = new VolumeLook(
        0.05f, 4f, -10f, 0f, 0.04f,
        new Color(0.40f, 0.47f, 0.60f), new Color(0.55f, 0.55f, 0.60f), -15f);
    [SerializeField, HideInInspector] private VolumeLook overcastVolume = new VolumeLook(
        0.06f, -4f, -4f, 0f, 0.02f,
        new Color(0.45f, 0.49f, 0.54f), new Color(0.53f, 0.54f, 0.52f), -5f);
    [SerializeField, HideInInspector] private VolumeLook lateAfternoonVolume = new VolumeLook(
        0f, 4f, 9f, 0f, 0.03f,
        new Color(0.43f, 0.47f, 0.55f), new Color(0.60f, 0.54f, 0.45f), 8f);
    [SerializeField, HideInInspector] private VolumeLook blueHourVolume = new VolumeLook(
        0.10f, 3f, -8f, 0f, 0.04f,
        new Color(0.40f, 0.48f, 0.60f), new Color(0.55f, 0.56f, 0.60f), -10f);

    [Header("Fixed Sky Palette")]
    [SerializeField, HideInInspector] private float orbitYaw = -30f;
    [SerializeField, HideInInspector, Range(-45f, 45f)] private float orbitTilt;
    [SerializeField, HideInInspector] private bool skyPaletteMigrated;
    [SerializeField, HideInInspector] private Gradient skyTop = Grad(
            (0.00f, C(0.02f,0.03f,0.10f)), (0.15f, C(0.03f,0.04f,0.13f)),
            (0.25f, C(0.14f,0.20f,0.42f)), (0.50f, C(0.06f,0.26f,0.72f)),
            (0.68f, C(0.20f,0.22f,0.50f)), (0.78f, C(0.28f,0.16f,0.42f)),
            (0.88f, C(0.012f,0.018f,0.045f)), (1.00f, C(0.008f,0.014f,0.035f)));
    [SerializeField, HideInInspector] private Gradient skyMid = Grad(
            (0.00f, C(0.04f,0.06f,0.16f)), (0.15f, C(0.05f,0.07f,0.18f)),
            (0.25f, C(0.72f,0.50f,0.50f)), (0.50f, C(0.20f,0.50f,0.90f)),
            (0.76f, C(0.90f,0.45f,0.30f)), (0.84f, C(0.55f,0.28f,0.34f)),
            (0.88f, C(0.022f,0.035f,0.075f)), (1.00f, C(0.018f,0.028f,0.060f)));
    [SerializeField, HideInInspector] private Gradient skyHorizon = Grad(
            (0.00f, C(0.06f,0.07f,0.16f)), (0.15f, C(0.07f,0.08f,0.18f)),
            (0.23f, C(0.98f,0.64f,0.50f)), (0.50f, C(0.86f,0.90f,0.90f)),
            (0.77f, C(1.00f,0.50f,0.24f)), (0.85f, C(0.85f,0.35f,0.28f)),
            (0.88f, C(0.052f,0.072f,0.100f)), (1.00f, C(0.035f,0.055f,0.080f)));
    [SerializeField, HideInInspector] private Gradient cloudLit = Grad(
            (0.00f, C(0.30f,0.34f,0.46f)), (0.15f, C(0.28f,0.32f,0.45f)),
            (0.25f, C(1.00f,0.82f,0.74f)), (0.50f, C(1.00f,0.99f,0.95f)),
            (0.68f, C(1.00f,0.82f,0.62f)), (0.78f, C(1.00f,0.68f,0.45f)),
            (0.88f, C(0.120f,0.160f,0.220f)), (1.00f, C(0.100f,0.150f,0.210f)));
    [SerializeField, HideInInspector] private Gradient cloudShadow = Grad(
            (0.00f, C(0.12f,0.15f,0.28f)), (0.15f, C(0.12f,0.15f,0.28f)),
            (0.25f, C(0.60f,0.52f,0.60f)), (0.50f, C(0.46f,0.56f,0.78f)),
            (0.70f, C(0.58f,0.42f,0.48f)), (0.80f, C(0.55f,0.32f,0.40f)),
            (0.88f, C(0.035f,0.055f,0.090f)), (1.00f, C(0.030f,0.050f,0.080f)));
    [SerializeField, HideInInspector] private Gradient sunGlow = Grad(
            (0.00f, C(0.025f,0.045f,0.080f)), (0.15f, C(0.035f,0.060f,0.100f)),
            (0.25f, C(1.00f,0.56f,0.30f)), (0.50f, C(1.00f,0.93f,0.80f)),
            (0.70f, C(1.00f,0.62f,0.30f)), (0.80f, C(1.00f,0.42f,0.20f)),
            (0.88f, C(0.035f,0.060f,0.100f)), (1.00f, C(0.025f,0.045f,0.080f)));
    [SerializeField, HideInInspector] private Gradient sunCore = Grad(
            (0.00f, C(1.00f,0.98f,0.92f)), (0.25f, C(1.00f,0.85f,0.70f)),
            (0.50f, C(1.00f,0.98f,0.92f)), (0.76f, C(1.00f,0.78f,0.58f)),
            (1.00f, C(1.00f,0.98f,0.92f)));
    [SerializeField, HideInInspector] private AnimationCurve sunCoreBrightness = Curve((0.00f,0f),(0.16f,0f),(0.24f,6f),(0.50f,10f),(0.70f,7f),(0.80f,3f),(0.86f,0f),(1.00f,0f));
    [SerializeField, HideInInspector] private AnimationCurve starVisibility = Curve((0.00f,1f),(0.15f,1f),(0.22f,0f),(0.80f,0f),(0.88f,1f),(1.00f,1f));
    [SerializeField, HideInInspector] private AnimationCurve moonVisibility = Curve((0.00f,1f),(0.15f,1f),(0.23f,0f),(0.80f,0f),(0.88f,1f),(1.00f,1f));

    [Header("Evening Stars")]
    [Tooltip("Fraction of the original stars kept in Evening. Other presets keep their original count.")]
    [Range(0f, 1f)] [SerializeField, HideInInspector] private float eveningStarAmount = 0.35f;
    [Tooltip("Occasional independent brightness pulses on a subset of Evening stars.")]
    [Range(0f, 1f)] [SerializeField, HideInInspector] private float eveningStarTwinkle = 1f;

    [Header("Character Light")]
    [Tooltip("Characters (Sasindo/Toon - Character) are lit by a separate key light: preset sun colour, but always from the camera's side so they never fall into back-light.")]
    [SerializeField] private bool characterLightEnabled = true;
    [Tooltip("How far the key light swings left/right from straight behind the camera (follows the sun's side).")]
    [SerializeField, Range(0f, 80f)] private float characterLightSideAngle = 35f;
    [Tooltip("Key light elevation above the horizon.")]
    [SerializeField, Range(5f, 85f)] private float characterLightElevation = 45f;

    private static readonly int CharLightDirId = Shader.PropertyToID("_SasindoCharLightDir");
    private static readonly int CharLightColorId = Shader.PropertyToID("_SasindoCharLightColor");

    public StageTime CurrentTime => selectedTime;

public void SetTime(StageTime time)
    {
        if ((int)time < 0 || (int)time > (int)StageTime.BlueHour) return;
        selectedTime = time;
        Apply();
    }

public void SetTimeByIndex(int index)
    {
        if (index < 0 || index > (int)StageTime.BlueHour) return;
        SetTime((StageTime)index);
    }

    public void Refresh()
    {
        Apply();
    }

    private void Reset()
    {
        ResolveReferences();
        Apply();
    }

    private void OnEnable()
    {
        Apply();
    }

private void OnDisable()
    {
        ReleaseRuntimeCloudLayout();
        Shader.SetGlobalVector(CharLightDirId, Vector4.zero);   // w = 0: characters fall back to the sun
    }


    private void OnValidate()
    {
        Apply();
    }

    private void Update()
    {
        if (isActiveAndEnabled && cloudRuntimeMaterial != null && cloudInstanceIsPlaying != Application.isPlaying)
            Apply();
    }

private void ResolveReferences()
    {
        if (directionalLight == null) directionalLight = GetComponent<Light>();
        if (directionalLight == null || stageCamera == null)
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                if (directionalLight == null)
                    foreach (var light in root.GetComponentsInChildren<Light>(true))
                        if (light.type == LightType.Directional) { directionalLight = light; break; }
                if (stageCamera == null)
                    foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                        if (camera.CompareTag("MainCamera")) { stageCamera = camera; break; }
            }
        }
        if (skyMaterial == null) skyMaterial = RenderSettings.skybox;
    }

private LightingProfile GetProfile()
    {
        switch (selectedTime)
        {
            case StageTime.Dawn: return dawn;
            case StageTime.Morning: return morning;
            case StageTime.Sunset: return sunset;
            case StageTime.Evening:
                // Apply the fixed preset to scenes saved with earlier profile values.
                LightingProfile eveningProfile = evening;
                eveningProfile.directionalIntensity = 1.25f;
                eveningProfile.shadowStrength = 1f;
                return eveningProfile;
            case StageTime.Overcast: return overcast;
            case StageTime.LateAfternoon: return lateAfternoon;
            case StageTime.BlueHour: return blueHour;
            default: return afternoon;
        }
    }

private AmbientProfile GetAmbientProfile()
    {
        switch (selectedTime)
        {
            case StageTime.Dawn: return dawnAmbient;
            case StageTime.Morning: return morningAmbient;
            case StageTime.Sunset: return sunsetAmbient;
            case StageTime.Evening: return eveningAmbient;
            case StageTime.Overcast: return overcastAmbient;
            case StageTime.LateAfternoon: return lateAfternoonAmbient;
            case StageTime.BlueHour: return blueHourAmbient;
            default: return afternoonAmbient;
        }
    }




private void Apply()
    {
        var presetFilter = GetComponent<IStageTimePresetFilter>();
        if (presetFilter is Behaviour filterBehaviour && filterBehaviour.isActiveAndEnabled)
            selectedTime = presetFilter.ResolveTime(selectedTime);
        if (Application.isPlaying && !isActiveAndEnabled) return;
        ResolveReferences();
        if (directionalLight == null || skyMaterial == null)
            return;

        PrepareRuntimeCloudLayout();
        ApplyBaseSky();
        ApplySkyProfile();
        ApplyCloudLayoutCoverage();

        LightingProfile profile = GetProfile();
        directionalLight.color = profile.directionalColor;
        directionalLight.intensity = Mathf.Max(0f, profile.directionalIntensity);
        directionalLight.shadowStrength = Mathf.Clamp01(profile.shadowStrength);

        RenderSettings.skybox = cloudRuntimeMaterial != null ? cloudRuntimeMaterial : skyMaterial;
        RenderSettings.sun = directionalLight;
        AmbientProfile ambient = GetAmbientProfile();
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = ambient.skyColor;
        RenderSettings.ambientEquatorColor = profile.ambientColor;
        RenderSettings.ambientGroundColor = ambient.groundColor;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = profile.fogColor;
        RenderSettings.fogStartDistance = Mathf.Max(0f, profile.fogStartDistance);
        RenderSettings.fogEndDistance = Mathf.Max(
            RenderSettings.fogStartDistance + 0.01f, profile.fogEndDistance);
        RenderSettings.reflectionIntensity =
            Mathf.Clamp01(profile.reflectionIntensity);

        ApplyVolume();
        ApplyCharacterLight();
    }

    // Character key light: the preset sun's colour and intensity, but its direction is
    // rebuilt on the camera's side (left/right follows the sun) so characters are
    // never back-lit. Read by Sasindo/Toon - Character as two globals.
    private void ApplyCharacterLight()
    {
        Camera cam = stageCamera != null && stageCamera.isActiveAndEnabled ? stageCamera : Camera.main;
        if (!characterLightEnabled || cam == null)
        {
            Shader.SetGlobalVector(CharLightDirId, Vector4.zero);
            return;
        }

        Vector3 camForward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        if (camForward.sqrMagnitude < 0.001f) camForward = Vector3.forward;
        Vector3 camRight = Vector3.Cross(Vector3.up, camForward);

        Vector3 toSun = -directionalLight.transform.forward;
        float side = Vector3.Dot(toSun, camRight) >= 0f ? 1f : -1f;

        float yaw = characterLightSideAngle * Mathf.Deg2Rad;
        float pitch = characterLightElevation * Mathf.Deg2Rad;
        Vector3 flat = (-camForward * Mathf.Cos(yaw) + camRight * (side * Mathf.Sin(yaw))).normalized;
        Vector3 toLight = (flat * Mathf.Cos(pitch) + Vector3.up * Mathf.Sin(pitch)).normalized;

        Color color = directionalLight.color.linear * directionalLight.intensity;
        Shader.SetGlobalVector(CharLightDirId, new Vector4(toLight.x, toLight.y, toLight.z, 1f));
        // as a vector: SetGlobalColor would convert gamma->linear a second time
        Shader.SetGlobalVector(CharLightColorId, new Vector4(color.r, color.g, color.b, 1f));
    }


private void ApplySkyProfile()
    {
        Material activeSkyMaterial = cloudRuntimeMaterial != null ? cloudRuntimeMaterial : skyMaterial;
        SkyProfile profile;
        switch (selectedTime)
        {
            case StageTime.Overcast: profile = overcastSky; break;
            case StageTime.LateAfternoon: profile = lateAfternoonSky; break;
            case StageTime.BlueHour: profile = blueHourSky; break;
            default:
                CloudLook clouds;
                switch (selectedTime)
                {
                    case StageTime.Dawn: clouds = dawnClouds; break;
                    case StageTime.Morning: clouds = morningClouds; break;
                    case StageTime.Sunset: clouds = sunsetClouds; break;
                    case StageTime.Evening: clouds = eveningClouds; break;
                    default: clouds = afternoonClouds; break;
                }
                activeSkyMaterial.SetFloat("_CloudCoverage", clouds.coverage);
                activeSkyMaterial.SetFloat("_CloudBrightness", clouds.brightness);
                activeSkyMaterial.SetFloat("_SilverLining", clouds.silverLining);
                return;
        }

        activeSkyMaterial.SetColor("_SkyTopColor", profile.skyTop);
        activeSkyMaterial.SetColor("_SkyMidColor", profile.skyMiddle);
        activeSkyMaterial.SetColor("_SkyHorizonColor", profile.skyHorizon);
        activeSkyMaterial.SetColor("_CloudColor", profile.cloudLit);
        activeSkyMaterial.SetColor("_CloudShadowColor", profile.cloudShadow);
        activeSkyMaterial.SetColor("_SunColor", profile.sunGlow);
        activeSkyMaterial.SetFloat("_SunCoreBright", profile.sunCoreBrightness);
        activeSkyMaterial.SetFloat("_CloudCoverage", profile.cloudCoverage);
        activeSkyMaterial.SetFloat("_CloudBrightness", profile.cloudBrightness);
        activeSkyMaterial.SetFloat("_SilverLining", profile.silverLining);
    }


private void ApplyVolume()
    {
        if (stageVolume == null || stageVolume.sharedProfile == null)
            return;

        VolumeProfile profile = Application.isPlaying ? stageVolume.profile : stageVolume.sharedProfile;
        VolumeLook look = GetVolumeLook();

        if (profile.TryGet(out Tonemapping tonemapping))
        {
            tonemapping.mode.overrideState = true;
            tonemapping.mode.value = TonemappingMode.Neutral;
        }

        if (profile.TryGet(out ColorAdjustments color))
        {
            color.postExposure.overrideState = true;
            color.postExposure.value = look.exposure;
            color.contrast.overrideState = true;
            color.contrast.value = look.contrast;
            color.saturation.overrideState = true;
            color.saturation.value = 25f;
        }

        if (profile.TryGet(out WhiteBalance balance))
        {
            balance.temperature.overrideState = true;
            balance.temperature.value = look.temperature;
        }

        if (profile.TryGet(out SplitToning toning))
        {
            toning.shadows.overrideState = true;
            toning.shadows.value = look.shadowTint;
            toning.highlights.overrideState = true;
            toning.highlights.value = look.highlightTint;
            toning.balance.overrideState = true;
            toning.balance.value = look.toneBalance;
        }

        if (profile.TryGet(out Bloom bloom))
        {
            bloom.intensity.overrideState = true;
            bloom.intensity.value = look.bloomIntensity;
        }

        if (profile.TryGet(out Vignette vignette))
        {
            vignette.intensity.overrideState = true;
            vignette.intensity.value = look.vignetteIntensity;
        }
    }


private VolumeLook GetVolumeLook()
    {
        switch (selectedTime)
        {
            case StageTime.Dawn: return dawnVolume;
            case StageTime.Morning: return morningVolume;
            case StageTime.Sunset: return sunsetVolume;
            case StageTime.Evening: return eveningVolume;
            case StageTime.Overcast: return overcastVolume;
            case StageTime.LateAfternoon: return lateAfternoonVolume;
            case StageTime.BlueHour: return blueHourVolume;
            default: return afternoonVolume;
        }
    }


private void ConfigureSunFraming(ref float yaw, ref float tilt)
    {
        if (stageCamera == null) return;
        Vector3 forward = Vector3.ProjectOnPlane(stageCamera.transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.001f) return;
        float cameraYaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        switch (selectedTime)
        {
            case StageTime.LateAfternoon:
                yaw = cameraYaw + lateAfternoonSunOffset; tilt = lateAfternoonSunTilt; break;
            case StageTime.Sunset:
                yaw = cameraYaw + sunsetSunOffset; tilt = sunsetSunTilt; break;
        }
    }

    public float CurrentHour => GetPresetHour(selectedTime);

    private static float GetPresetHour(StageTime time)
    {
        switch (time)
        {
            case StageTime.Dawn: return 6.25f;
            case StageTime.Morning: return 8.5f;
            case StageTime.Sunset: return 17.25f;
            case StageTime.Evening: return 21.5f;
            case StageTime.LateAfternoon: return 15.75f;
            case StageTime.BlueHour: return 18.75f;
            default: return 13.5f;
        }
    }

    private void ApplyBaseSky()
    {
        if (skyTop == null) return;
        float t = CurrentHour / 24f;
        float yaw = orbitYaw, tilt = orbitTilt;
        ConfigureSunFraming(ref yaw, ref tilt);
        Quaternion sunRotation = Quaternion.Euler(tilt, yaw, 0f) * Quaternion.Euler(t * 360f - 90f, 0f, 0f);
        Vector3 sunDirection = -(sunRotation * Vector3.forward);
        bool moonLight = selectedTime == StageTime.Evening || selectedTime == StageTime.BlueHour;
        directionalLight.transform.rotation = moonLight ? Quaternion.LookRotation(sunDirection) : sunRotation;
        Material mat = cloudRuntimeMaterial != null ? cloudRuntimeMaterial : skyMaterial;
        mat.SetColor("_SkyTopColor", skyTop.Evaluate(t));
        mat.SetColor("_SkyMidColor", skyMid.Evaluate(t));
        mat.SetColor("_SkyHorizonColor", skyHorizon.Evaluate(t));
        mat.SetColor("_CloudColor", cloudLit.Evaluate(t));
        mat.SetColor("_CloudShadowColor", cloudShadow.Evaluate(t));
        mat.SetColor("_SunColor", sunGlow.Evaluate(t));
        mat.SetColor("_SunCoreColor", sunCore.Evaluate(t));
        mat.SetFloat("_SunCoreBright", Mathf.Max(0f, sunCoreBrightness.Evaluate(t)));
        mat.SetFloat("_StarVisibility", Mathf.Clamp01(starVisibility.Evaluate(t)));
        bool eveningStars = selectedTime == StageTime.Evening;
        if (mat.HasProperty("_StarAmount"))
            mat.SetFloat("_StarAmount", eveningStars ? Mathf.Clamp01(eveningStarAmount) : 1f);
        if (mat.HasProperty("_StarTwinkleStrength"))
            mat.SetFloat("_StarTwinkleStrength", eveningStars ? Mathf.Clamp01(eveningStarTwinkle) : 0f);
        mat.SetFloat("_MoonVisibility", Mathf.Clamp01(moonVisibility.Evaluate(t)));
        mat.SetVector("_SunDir", sunDirection);
    }

    static Color C(float r, float g, float b) { return new Color(r, g, b, 1f); }

    static Gradient Grad(params (float t, Color c)[] keys)
    {
        var g = new Gradient();
        var ck = new GradientColorKey[keys.Length];
        for (int i = 0; i < keys.Length; i++) ck[i] = new GradientColorKey(keys[i].c, keys[i].t);
        g.SetKeys(ck, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }

    static AnimationCurve Curve(params (float t, float v)[] keys)
    {
        var c = new AnimationCurve();
        foreach (var k in keys) c.AddKey(k.t, k.v);
        return c;
    }
}

/// <summary>Optional scene-specific time restriction; stages without a filter keep all presets.</summary>
public interface IStageTimePresetFilter
{
    StageLightingManager.StageTime ResolveTime(StageLightingManager.StageTime requested);
}
