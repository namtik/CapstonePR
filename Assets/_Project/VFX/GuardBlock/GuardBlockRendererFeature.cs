using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Sashindo.VFX
{
    public sealed class GuardBlockRendererFeature : ScriptableRendererFeature
    {
        private static readonly int Film = Shader.PropertyToID("_GuardFilm");
        private static readonly int WaveColor = Shader.PropertyToID("_GuardWaveColor");
        private static readonly int Timing = Shader.PropertyToID("_GuardTiming");
        private static readonly int Shape = Shader.PropertyToID("_GuardShape");
        private static readonly int Motion = Shader.PropertyToID("_GuardMotion");
        private static readonly int Echo = Shader.PropertyToID("_GuardEcho");

        [SerializeField] private Shader effectShader;
        private Material runtimeMaterial;
        private GuardWavePass wavePass;
        private static Camera activeCamera;
        private static GuardBlockSettings activeSettings;
        private static float startTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Stop()
        {
            activeCamera = null;
            activeSettings = null;
        }

        public static void Play(Camera camera, GuardBlockSettings settings)
        {
            activeCamera = camera;
            activeSettings = settings;
            startTime = Time.unscaledTime;
        }

        public override void Create()
        {
            if (runtimeMaterial == null && effectShader != null)
                runtimeMaterial = CoreUtils.CreateEngineMaterial(effectShader);
            if (runtimeMaterial != null)
                wavePass = new GuardWavePass(runtimeMaterial);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var camera = renderingData.cameraData.camera;
            if (wavePass == null || activeSettings == null || activeCamera == null || camera != activeCamera ||
                renderingData.cameraData.cameraType != CameraType.Game)
                return;

            GuardBlockSettings settings = activeSettings;
            float elapsed = Time.unscaledTime - startTime;
            if (elapsed >= settings.Duration)
            {
                Stop();
                return;
            }

            var target = renderingData.cameraData.cameraTargetDescriptor;
            float aspect = (float)target.width / Mathf.Max(1, target.height);
            Color film = settings.FilmColor;
            film.a *= Mathf.Clamp01(settings.FilmOpacity);
            runtimeMaterial.SetColor(Film, film);
            runtimeMaterial.SetColor(WaveColor, settings.WaveColor);
            runtimeMaterial.SetVector(Timing, new Vector4(elapsed, Mathf.Max(0.01f, settings.FadeIn),
                Mathf.Max(0f, settings.Hold), Mathf.Max(0.01f, settings.FadeOut)));
            runtimeMaterial.SetVector(Shape, new Vector4(Mathf.Clamp01(settings.Center.x), Mathf.Clamp01(settings.Center.y),
                aspect, Mathf.Max(0.25f, settings.VerticalRatio)));
            runtimeMaterial.SetVector(Motion, new Vector4(Mathf.Max(0.01f, settings.Radius), Mathf.Max(0.02f, settings.TravelTime),
                Mathf.Max(0.004f, settings.Width), Mathf.Clamp01(settings.WaveBrightness)));
            runtimeMaterial.SetVector(Echo, new Vector4(Mathf.Max(0f, settings.EchoDelay), Mathf.Clamp01(settings.EchoStrength),
                Mathf.Max(0f, settings.Refraction) * 0.01f, 0f));

            renderer.EnqueuePass(wavePass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(runtimeMaterial);
            runtimeMaterial = null;
            wavePass = null;
        }

        private sealed class GuardWavePass : ScriptableRenderPass
        {
            private readonly Material material;

            public GuardWavePass(Material material)
            {
                this.material = material;
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer || !resources.activeColorTexture.IsValid())
                    return;

                TextureHandle source = resources.activeColorTexture;
                var desc = graph.GetTextureDesc(source);
                desc.name = "Guard Block Camera Color";
                desc.clearBuffer = false;
                TextureHandle destination = graph.CreateTexture(desc);
                var parameters = new RenderGraphUtils.BlitMaterialParameters(source, destination, material, 0);
                graph.AddBlitPass(parameters, passName: "Guard Block Wave");
                resources.cameraColor = destination;
            }
        }
    }
}
