using System;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class StageTimeOfDayEffects : MonoBehaviour
{
    [Serializable]
    public struct EffectMix
    {
        [Range(0f, 1f)] public float daylightDust;
        [Range(0f, 1f)] public float mist;
        [Range(0, 30)] public int fireflies;
        [Range(0f, 1f)] public float wind;

        public EffectMix(float daylightDust, float mist, int fireflies, float wind = 0f)
        {
            this.daylightDust = daylightDust;
            this.mist = mist;
            this.fireflies = fireflies;
            this.wind = wind;
        }
    }

    [Header("Scene References")]
    [SerializeField] private StageLightingManager lightingManager;
    [SerializeField] private GameObject leavesRoot;
    [SerializeField] private GameObject effectsRoot;
    [SerializeField] private ParticleSystem daylightDust;
    [SerializeField] private ParticleSystem windParticles;
    [SerializeField] private ParticleSystem fireflyParticles;
    [SerializeField] private Renderer mistRenderer;
    [SerializeField] private ParticleSystem mistParticles;

    [Header("Base Settings")]
    [Min(0f)]
    [SerializeField] private float fullDustRate = 17f;
    [Min(0f)]
    [SerializeField] private float fullWindRate = 2f;

    [Header("Time Presets")]
    [SerializeField] private EffectMix dawn = new EffectMix(0f, 1f, 0);
    [SerializeField] private EffectMix morning = new EffectMix(0.25f, 0.35f, 0);
    [SerializeField] private EffectMix afternoon = new EffectMix(0.7f, 0f, 0);
    [SerializeField] private EffectMix sunset = new EffectMix(0f, 0f, 0);
    [SerializeField] private EffectMix evening = new EffectMix(0f, 0f, 18);
    [SerializeField] private EffectMix overcast = new EffectMix(0f, 0.45f, 0);
    [SerializeField] private EffectMix lateAfternoon = new EffectMix(0.45f, 0f, 0);
    [SerializeField] private EffectMix blueHour = new EffectMix(0f, 0.15f, 6);

    private StageLightingManager.StageTime appliedPreset;
    private bool hasApplied;

    private void ResolveManager()
    {
        if (lightingManager != null) return;
        lightingManager = GetComponent<StageLightingManager>();
        // prefab-edit / preview scenes are valid but not loaded; GetRootGameObjects throws there
        if (lightingManager != null || !gameObject.scene.IsValid() || !gameObject.scene.isLoaded) return;
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            lightingManager = root.GetComponentInChildren<StageLightingManager>(true);
            if (lightingManager != null) return;
        }
    }

    private void OnEnable()
    {
        ResolveManager();
        hasApplied = false;
    }

    private void OnValidate()
    {
        ResolveManager();
        hasApplied = false;
    }

    private void Update()
    {
        if (lightingManager != null &&
            (!hasApplied || appliedPreset != lightingManager.CurrentTime))
        {
            RefreshNow();
        }
    }

    public void RefreshNow()
    {
        if (lightingManager == null)
            return;

        StageLightingManager.StageTime preset = lightingManager.CurrentTime;
        EffectMix mix = GetMix(preset);

        if (leavesRoot != null && !leavesRoot.activeSelf)
            leavesRoot.SetActive(true);

        if (mistParticles == null && mistRenderer != null)
            mistParticles = mistRenderer.GetComponent<ParticleSystem>();
        if (mistRenderer == null && mistParticles != null)
            mistRenderer = mistParticles.GetComponent<ParticleSystemRenderer>();

        bool showDust = daylightDust != null && mix.daylightDust > 0.001f;
        bool showMist = mistRenderer != null && mix.mist > 0.001f;
        bool showFireflies = fireflyParticles != null && mix.fireflies > 0;
        bool showWind = windParticles != null && mix.wind > 0.001f;

        if (effectsRoot != null)
            effectsRoot.SetActive(showDust || showMist || showFireflies || showWind);

        if (daylightDust != null)
        {
            var emission = daylightDust.emission;
            emission.rateOverTime = Mathf.Max(0f, fullDustRate * mix.daylightDust);
            daylightDust.gameObject.SetActive(showDust);
            if (showDust && !daylightDust.isPlaying)
                daylightDust.Play();
        }

        if (windParticles != null)
        {
            var emission = windParticles.emission;
            emission.rateOverTime = Mathf.Max(0f, fullWindRate * mix.wind);
            if (!showWind)
                windParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            windParticles.gameObject.SetActive(showWind);
            if (showWind && !windParticles.isPlaying)
                windParticles.Play(true);
        }

        if (mistRenderer != null)
        {
            var tint = mistRenderer.sharedMaterial != null &&
                       mistRenderer.sharedMaterial.HasProperty("_BaseColor")
                ? mistRenderer.sharedMaterial.GetColor("_BaseColor")
                : Color.white;
            tint.a *= Mathf.Clamp01(mix.mist);
            var block = new MaterialPropertyBlock();
            mistRenderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", tint);
            mistRenderer.SetPropertyBlock(block);
            if (!showMist && mistParticles != null)
                mistParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            mistRenderer.gameObject.SetActive(showMist);
            if (showMist && mistParticles != null && !mistParticles.isPlaying)
                mistParticles.Play(true);
        }

        if (fireflyParticles != null)
        {
            fireflyParticles.gameObject.SetActive(false);
            if (showFireflies)
            {
                var emission = fireflyParticles.emission;
                emission.SetBursts(new[]
                {
                    new ParticleSystem.Burst(0f, (short)Mathf.Clamp(mix.fireflies, 1, 30))
                });
                fireflyParticles.gameObject.SetActive(true);
                fireflyParticles.Simulate(0.1f, true, true, true);
                fireflyParticles.Play();
            }
        }

        appliedPreset = preset;
        hasApplied = true;
    }

    private EffectMix GetMix(StageLightingManager.StageTime preset)
    {
        switch (preset)
        {
            case StageLightingManager.StageTime.Dawn: return dawn;
            case StageLightingManager.StageTime.Morning: return morning;
            case StageLightingManager.StageTime.Sunset: return sunset;
            case StageLightingManager.StageTime.Evening: return evening;
            case StageLightingManager.StageTime.Overcast: return overcast;
            case StageLightingManager.StageTime.LateAfternoon: return lateAfternoon;
            case StageLightingManager.StageTime.BlueHour: return blueHour;
            default: return afternoon;
        }
    }
}
