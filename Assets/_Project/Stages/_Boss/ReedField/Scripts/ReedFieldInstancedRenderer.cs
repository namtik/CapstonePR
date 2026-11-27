using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Profiling;

namespace StageRendering
{
    /// <summary>Shares the authored reed meshes and caches their static placements for GPU instancing.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10000)]
    public sealed class ReedFieldInstancedRenderer : MonoBehaviour
    {
        // Nonuniform scales need both object-to-world and world-to-object matrices.
        private const int MaxInstancesPerBatch = 511;
        private static readonly ProfilerMarker SubmitMarker = new ProfilerMarker("BossReeds.SubmitInstances");

        private sealed class Batch
        {
            public Mesh Mesh;
            public RenderParams Parameters;
            public Matrix4x4[] Matrices;
            public Matrix4x4[] RelativeMatrices;
            public Bounds[] LocalBounds;
            public float WindPadding;
        }

        private readonly struct BatchKey : System.IEquatable<BatchKey>
        {
            public readonly Mesh Mesh;
            public readonly Material Material;
            public readonly int Layer;
            public readonly ShadowCastingMode Shadows;
            public readonly bool ReceiveShadows;
            public readonly LightProbeUsage Probes;
            public readonly ReflectionProbeUsage Reflections;
            public readonly MotionVectorGenerationMode Motion;
            public readonly uint RenderingLayers;

            public BatchKey(MeshRenderer renderer, Mesh mesh)
            {
                Mesh = mesh;
                Material = renderer.sharedMaterial;
                Layer = renderer.gameObject.layer;
                Shadows = renderer.shadowCastingMode;
                ReceiveShadows = renderer.receiveShadows;
                // With no probe data both paths use ambient SH; avoid splitting identical batches.
                LightProbes probes = LightmapSettings.lightProbes;
                Probes = probes && probes.count > 0 ? renderer.lightProbeUsage : LightProbeUsage.Off;
                // BossReedWind samples no reflection probes and has no motion-vector pass.
                Reflections = ReflectionProbeUsage.Off;
                Motion = MotionVectorGenerationMode.ForceNoMotion;
                RenderingLayers = renderer.renderingLayerMask;
            }

            public bool Equals(BatchKey other) => Mesh == other.Mesh && Material == other.Material
                && Layer == other.Layer && Shadows == other.Shadows && ReceiveShadows == other.ReceiveShadows
                && Probes == other.Probes && Reflections == other.Reflections && Motion == other.Motion
                && RenderingLayers == other.RenderingLayers;
            public override bool Equals(object other) => other is BatchKey key && Equals(key);
            public override int GetHashCode() => System.HashCode.Combine(Mesh, Material, Layer, Shadows);
        }

        private readonly List<Batch> batches = new List<Batch>(9);
        private readonly List<MeshRenderer> sources = new List<MeshRenderer>(180);
        private Matrix4x4 cachedRootMatrix;
        private bool initialized;

        public int BatchCount => batches.Count;
        public int InstanceCount => sources.Count;

        private void OnEnable()
        {
            if (Application.isPlaying)
                RebuildBatches();
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying)
                return;

            if (!initialized)
                RebuildBatches();

            Matrix4x4 rootMatrix = transform.localToWorldMatrix;
            if (rootMatrix != cachedRootMatrix)
                UpdateWorldMatrices(rootMatrix);

            // Arrays and render parameters are reused. No per-frame discovery or allocations.
            using (SubmitMarker.Auto())
            {
                for (int i = 0; i < batches.Count; i++)
                {
                    Batch batch = batches[i];
                    Graphics.RenderMeshInstanced(batch.Parameters, batch.Mesh, 0, batch.Matrices);
                }
            }
        }

        /// <summary>Call after changing individual reed placements or visibility during play.</summary>
        [ContextMenu("Rebuild Instance Batches")]
        public void RebuildBatches()
        {
            RestoreSources();
            initialized = true;
            if (!Application.isPlaying || !isActiveAndEnabled || !SystemInfo.supportsInstancing)
                return;

            var grouped = new Dictionary<BatchKey, List<MeshRenderer>>();
            foreach (MeshRenderer renderer in GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.forceRenderingOff)
                    continue;
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                Mesh mesh = filter ? filter.sharedMesh : null;
                Material material = renderer.sharedMaterial;
                // Keep unsupported objects on their normal renderer path.
                if (!mesh || mesh.subMeshCount != 1 || !material || !material.enableInstancing
                    || material.shader.name != "Stage/BossReedWind"
                    || renderer.sharedMaterials.Length != 1 || renderer.HasPropertyBlock())
                    continue;

                var key = new BatchKey(renderer, mesh);
                if (!grouped.TryGetValue(key, out List<MeshRenderer> list))
                {
                    list = new List<MeshRenderer>();
                    grouped.Add(key, list);
                }
                list.Add(renderer);
            }

            Matrix4x4 inverseRoot = transform.worldToLocalMatrix;
            foreach (KeyValuePair<BatchKey, List<MeshRenderer>> group in grouped)
            {
                BatchKey key = group.Key;
                List<MeshRenderer> list = group.Value;
                for (int start = 0; start < list.Count; start += MaxInstancesPerBatch)
                {
                    int count = Mathf.Min(MaxInstancesPerBatch, list.Count - start);
                    var batch = new Batch
                    {
                        Mesh = key.Mesh,
                        Parameters = new RenderParams(key.Material)
                        {
                            layer = key.Layer, shadowCastingMode = key.Shadows,
                            receiveShadows = key.ReceiveShadows, lightProbeUsage = key.Probes,
                            reflectionProbeUsage = key.Reflections, motionVectorMode = key.Motion,
                            renderingLayerMask = key.RenderingLayers
                        },
                        Matrices = new Matrix4x4[count],
                        RelativeMatrices = new Matrix4x4[count],
                        LocalBounds = new Bounds[count],
                        WindPadding = key.Material.HasProperty("_Amplitude")
                            ? Mathf.Max(0.5f, Mathf.Abs(key.Material.GetFloat("_Amplitude")) * 2f) : 0.5f
                    };
                    for (int i = 0; i < count; i++)
                    {
                        MeshRenderer renderer = list[start + i];
                        batch.RelativeMatrices[i] = inverseRoot * renderer.localToWorldMatrix;
                        batch.LocalBounds[i] = renderer.localBounds;
                        sources.Add(renderer);
                    }
                    batches.Add(batch);
                }
            }
            UpdateWorldMatrices(transform.localToWorldMatrix);
            for (int i = 0; i < sources.Count; i++)
                sources[i].forceRenderingOff = true;
        }

        private void UpdateWorldMatrices(Matrix4x4 rootMatrix)
        {
            cachedRootMatrix = rootMatrix;
            foreach (Batch batch in batches)
            {
                Bounds bounds = default;
                for (int i = 0; i < batch.Matrices.Length; i++)
                {
                    Matrix4x4 matrix = rootMatrix * batch.RelativeMatrices[i];
                    batch.Matrices[i] = matrix;
                    Bounds local = batch.LocalBounds[i];
                    Vector3 x = matrix.MultiplyVector(new Vector3(local.extents.x, 0, 0));
                    Vector3 y = matrix.MultiplyVector(new Vector3(0, local.extents.y, 0));
                    Vector3 z = matrix.MultiplyVector(new Vector3(0, 0, local.extents.z));
                    Vector3 extent = new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                        Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
                        Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
                    var world = new Bounds(matrix.MultiplyPoint3x4(local.center), extent * 2f);
                    if (i == 0) bounds = world; else bounds.Encapsulate(world);
                }
                bounds.Expand(batch.WindPadding * 2f);
                RenderParams parameters = batch.Parameters;
                parameters.worldBounds = bounds;
                batch.Parameters = parameters;
            }
        }

        private void OnDisable() => RestoreSources();
        private void OnDestroy() => RestoreSources();

        private void RestoreSources()
        {
            for (int i = 0; i < sources.Count; i++)
                if (sources[i]) sources[i].forceRenderingOff = false;
            sources.Clear();
            batches.Clear();
            initialized = false;
        }
    }
}
