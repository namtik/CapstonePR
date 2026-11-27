
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sashindo.VFX
{
    [DisallowMultipleComponent]
    public sealed class AwakeningComboParticle : MonoBehaviour
    {
        private const int OrbitCount = 42;
        private const int GatherCount = 26;
        private const int RisingCount = 8;
        private const string ShaderPath = "SashindoAwakening/ComboParticle";
        private static readonly Vector3[] FirstOffsets =
        {
            new Vector3(.96f, .68f, .48f), new Vector3(-.96f, .16f, -.46f), new Vector3(.69f, -.70f, -.21f)
        };

        private readonly ParticleSystem.Particle[] particles = new ParticleSystem.Particle[2 + OrbitCount + GatherCount + RisingCount];
        private readonly float[] offsets = new float[OrbitCount + GatherCount + RisingCount];
        private ParticleSystem system;
        private Material material;
        private AwakeningComboParticleGraphic projectedGraphic;
        private Transform trackedEnemy;
        private Vector3 initialEnemyPosition;
        private Vector3 restingPosition;
        private float worldUnit;
        private float elapsed;

        private static Camera BattleCamera()
        {
            var stage = BattleStageController.Current;
            var camera = stage != null ? stage.StageCamera : null;
            return camera != null && camera.isActiveAndEnabled ? camera : Camera.main;
        }

        public static GameObject Spawn(Transform owner, EnemyController enemy, int index, float radius,
            float verticalSquash, Vector2 centerOffset, float scale, RectTransform displayLayer = null)
        {
            var camera = BattleCamera();
            var shader = Resources.Load<Shader>(ShaderPath);
            if (camera == null || camera.cullingMask == 0 || shader == null || !shader.isSupported) return null;

            float reference = camera.pixelHeight / 1080f;
            if (reference <= 0f) return null;
            var target = enemy != null ? enemy.transform : null;
            var pixelRect = camera.pixelRect;
            Vector2 center = pixelRect.center;
            float depth = Mathf.Max(camera.nearClipPlane + 1f, 5f);
            if (target != null)
            {
                Vector3 projected = camera.WorldToScreenPoint(target.position);
                if (projected.z > camera.nearClipPlane)
                {
                    depth = projected.z;
                    center = projected;
                }

                if (enemy.TryGetAimScreen(out Vector2 aim)) center = aim;
            }

            Vector3 offset = Offset(index);
            center = PlaceOnScreen(pixelRect, center, offset, radius, verticalSquash, centerOffset * reference);
            var basePosition = camera.ScreenToWorldPoint(new Vector3(center.x, center.y, depth));
            var oneUnit = camera.ScreenToWorldPoint(new Vector3(center.x + 230f * reference, center.y, depth));
            float unit = Vector3.Distance(basePosition, oneUnit);
            float biasedDepth = Mathf.Clamp(depth + offset.z * unit,
                camera.nearClipPlane + .1f, camera.farClipPlane - .1f);
            var position = camera.ScreenToWorldPoint(new Vector3(center.x, center.y, biasedDepth));

            var go = new GameObject("Awakening combo particles", typeof(ParticleSystem));
            if (go.scene != owner.gameObject.scene) SceneManager.MoveGameObjectToScene(go, owner.gameObject.scene);
            go.layer = RenderLayer(camera, target != null ? target.gameObject.layer : 0);
            go.transform.position = position;
            var marker = go.AddComponent<AwakeningComboParticle>();
            marker.Initialize(shader, target, position, unit * Mathf.Max(.1f, scale), index, displayLayer);
            return go;
        }

        public bool TryGetBurstScreenPosition(out Vector2 screen)
        {
            screen = default;
            var camera = BattleCamera();
            if (camera == null) return false;
            Vector3 point = camera.WorldToScreenPoint(CurrentPosition());
            if (point.z <= camera.nearClipPlane) return false;
            screen = point;
            return true;
        }

        private static Vector2 PlaceOnScreen(Rect viewport, Vector2 aim, Vector3 direction, float radius,
            float verticalSquash, Vector2 centerOffset)
        {
            Vector2 origin = Vector2.Lerp(viewport.center, aim, .45f) + centerOffset;
            float spacing = Mathf.Max(1f, radius) / 230f;
            origin.x += direction.x * viewport.width * .34f * spacing;
            origin.y += direction.y * viewport.height * .32f * spacing * (verticalSquash / .72f);

            // Keep the markers inside the battle view, between the HUD and the hand.
            return new Vector2(
                Mathf.Clamp(origin.x, viewport.xMin + viewport.width * .12f, viewport.xMin + viewport.width * .88f),
                Mathf.Clamp(origin.y, viewport.yMin + viewport.height * .28f, viewport.yMin + viewport.height * .82f));
        }

        private static Vector3 Offset(int index)
        {
            if (index < FirstOffsets.Length) return FirstOffsets[index];
            float angle = index * 137.50776f * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(angle), .73f * Mathf.Sin(angle), .42f * Mathf.Cos(angle * .77f));
        }

        private static int RenderLayer(Camera camera, int preferred)
        {
            int stageLayer = LayerMask.NameToLayer("Stage3D");
            if (stageLayer >= 0 && (camera.cullingMask & (1 << stageLayer)) != 0) return stageLayer;
            if ((camera.cullingMask & (1 << preferred)) != 0) return preferred;
            for (int layer = 0; layer < 32; layer++)
                if ((camera.cullingMask & (1 << layer)) != 0) return layer;
            return 0;
        }

        private void Initialize(Shader shader, Transform enemy, Vector3 position, float scale, int index,
            RectTransform displayLayer)
        {
            trackedEnemy = enemy;
            initialEnemyPosition = enemy != null ? enemy.position : Vector3.zero;
            restingPosition = position;
            worldUnit = scale;
            var random = new System.Random(6713 + index * 2879);
            for (int i = 0; i < offsets.Length; i++) offsets[i] = (float)random.NextDouble();

            system = GetComponent<ParticleSystem>();
            system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = particles.Length;
            var emission = system.emission;
            emission.enabled = false;
            var renderer = GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            material = new Material(shader) { hideFlags = HideFlags.DontSave };
            renderer.sharedMaterial = material;
            if (displayLayer != null)
            {
                CreateProjectedGraphic(displayLayer);
                renderer.enabled = false;
            }
            system.Play(false);
            system.Pause(false);
            UpdateParticles();
        }

        private void CreateProjectedGraphic(RectTransform displayLayer)
        {
            var go = new GameObject("Awakening combo particle display", typeof(RectTransform),
                typeof(AwakeningComboParticleGraphic));
            var rect = (RectTransform)go.transform;
            rect.SetParent(displayLayer, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            projectedGraphic = go.GetComponent<AwakeningComboParticleGraphic>();
            projectedGraphic.raycastTarget = false;
            projectedGraphic.maskable = false;
            material.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
            projectedGraphic.material = material;
        }

        private Vector3 CurrentPosition()
        {
            if (trackedEnemy != null && trackedEnemy.gameObject.activeInHierarchy)
                return restingPosition + trackedEnemy.position - initialEnemyPosition;
            return transform.position;
        }

        private void LateUpdate()
        {
            if (system == null) return;
            transform.position = CurrentPosition();
            elapsed += Time.deltaTime;
            UpdateParticles();
        }

        private void UpdateParticles()
        {
            float settled = Mathf.SmoothStep(0f, 1f, elapsed / .24f);
            int count = 0;
            Set(ref count, Vector3.zero, .27f, new Color32(255, 141, 34, Alpha(settled * .21f)));
            Set(ref count, Vector3.zero, .065f, new Color32(255, 224, 146, Alpha(settled * (.84f + .16f * Mathf.Sin(elapsed * 21f)))));

            for (int i = 0; i < OrbitCount; i++)
            {
                float seed = offsets[i];
                float angle = seed * Mathf.PI * 2f + elapsed * (i % 2 == 0 ? 2.45f : -2.1f);
                float radius = .035f + .22f * seed * seed;
                float y = Mathf.Sin(angle) * radius;
                float tilt = .45f + (i % 3) * .64f;
                Vector3 point = new Vector3(Mathf.Cos(angle) * radius,
                    y * Mathf.Cos(tilt) + .024f * Mathf.Sin(angle * .73f + i), y * Mathf.Sin(tilt));
                float pulse = .58f + .42f * Mathf.Sin(elapsed * (4f + 4f * seed) + 28f * seed);
                Set(ref count, point, .018f + .020f * seed, Ember(i, settled * (.24f + .66f * pulse)));
            }

            for (int i = 0; i < GatherCount; i++)
            {
                float seed = offsets[OrbitCount + i];
                float travel = Mathf.SmoothStep(0f, 1f, (elapsed - .05f * seed) / (.17f + .24f * seed));
                if (travel >= 1f) continue;
                float angle = seed * Mathf.PI * 2f + travel * 1.35f;
                float distance = (.28f + .35f * seed) * (1f - travel);
                var point = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), Mathf.Sin(i * 2f) * .8f) * distance;
                Set(ref count, point, .025f + .022f * seed, Ember(i, Mathf.Clamp01(elapsed * 14f) * (1f - travel)));
            }

            for (int i = 0; i < RisingCount; i++)
            {
                float seed = offsets[OrbitCount + GatherCount + i];
                float age = Mathf.Repeat(elapsed * (.65f + .32f * seed) + seed, 1f);
                float angle = seed * Mathf.PI * 2f;
                var point = new Vector3(Mathf.Cos(angle) * age * .24f, .39f * age, Mathf.Sin(angle) * age * .20f);
                Set(ref count, point, .022f, Ember(i, settled * Mathf.Sin(age * Mathf.PI) * (1f - age) * .52f));
            }
            system.SetParticles(particles, count);
            if (projectedGraphic != null)
                projectedGraphic.Show(BattleCamera(), transform, particles, count);
        }

        private void Set(ref int index, Vector3 point, float size, Color32 color)
        {
            particles[index++] = new ParticleSystem.Particle
            {
                position = point * worldUnit,
                startSize = size * worldUnit,
                startColor = color,
                startLifetime = 1f,
                remainingLifetime = 1f
            };
        }

        private static Color32 Ember(int index, float alpha)
        {
            return index % 5 == 0
                ? new Color32(255, 244, 204, Alpha(alpha))
                : new Color32(255, 175, 69, Alpha(alpha));
        }

        private static byte Alpha(float alpha) => (byte)(Mathf.Clamp01(alpha) * 255f);

        private void OnDestroy()
        {
            if (projectedGraphic != null) Destroy(projectedGraphic.gameObject);
            if (material != null) Destroy(material);
        }
    }
}
