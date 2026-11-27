using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sashindo.VFX
{
    [AddComponentMenu("Sashindo/VFX/Guard Break Effect")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GuardBreakEffect : MaskableGraphic
    {
        [Header("색상")]
        [SerializeField, InspectorName("화면 색상")]
        private Color fillColor = new Color(0.87f, 0.65f, 0.17f, 1f);

        [SerializeField, Range(0f, 1f), InspectorName("화면 불투명도")]
        private float fillOpacity = 0.52f;

        [SerializeField, InspectorName("금과 파편 가장자리 색상")]
        private Color crackColor = new Color(1f, 0.90f, 0.60f, 0.9f);

        [Header("조각")]
        [SerializeField, Range(6, 48), InspectorName("조각 수")]
        private int fragmentCount = 28;

        [SerializeField, Range(0f, 1f), InspectorName("크기 편차")]
        private float sizeVariation = 0.56f;

        [SerializeField, Range(0f, 3f), InspectorName("퍼지는 거리")]
        private float spread = 1.36f;

        [SerializeField, Range(0f, 180f), InspectorName("최대 회전 (도)")]
        private float rotation = 68f;

        [SerializeField, Range(0f, 2f), InspectorName("아래로 떨어지는 정도")]
        private float fall = 0.28f;

        [SerializeField, InspectorName("조각 배치 시드")]
        private int seed = 39271;

        [SerializeField, InspectorName("재생할 때마다 금 모양 바꾸기"),
         Tooltip("끄면 조각 배치 시드에 따라 같은 금 모양을 재생합니다.")]
        private bool randomizeOnReplay = true;

        [SerializeField, InspectorName("파편이 퍼지는 기준점 (0~1)"),
         Tooltip("파편 이동 방향에만 영향을 줍니다. 금은 화면의 여러 지점에서 시작합니다.")]
        private Vector2 impactPoint = new Vector2(0.47f, 0.49f);

        [Header("재생")]
        [SerializeField, Range(0f, 1f), InspectorName("색이 차오르는 시간 (초)"),
         Tooltip("화면 색상이 투명한 상태에서 설정한 불투명도까지 부드럽게 차오르는 시간입니다.")]
        private float fadeInTime = 0.22f;

        [SerializeField, Range(0f, 1f), InspectorName("화면이 덮인 시간 (초)")]
        private float holdTime = 0.10f;

        [SerializeField, Range(0.02f, 1f), InspectorName("금이 퍼지는 시간 (초)")]
        private float crackTime = 0.24f;

        [SerializeField, Range(0.10f, 2f), InspectorName("조각이 흩어지는 시간 (초)")]
        private float scatterTime = 0.64f;

        [SerializeField, InspectorName("활성화 시 자동 재생"), Tooltip("시연용입니다. 스크립트에서는 Play()로 재생할 수 있습니다.")]
        private bool playOnEnable;

        private readonly List<Shard> shards = new List<Shard>();
        private Vector2 layoutSize;
        private Vector2 layoutImpact;
        private int layoutCount;
        private int layoutSeed;
        private float layoutVariation;
        private float time = -1f;
        private bool playing;
        private int? playbackSeed;

        private float FadeInDuration => Mathf.Max(0f, fadeInTime);
        private float CrackStart => FadeInDuration + Mathf.Max(0f, holdTime);
        private float CrackDuration => Mathf.Max(0.02f, crackTime);
        private float ScatterDuration => Mathf.Max(0.10f, scatterTime);

        public float Duration => CrackStart + CrackDuration + ScatterDuration;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (Application.isPlaying && playOnEnable)
                Play();
        }

        protected override void OnDisable()
        {
            playing = false;
            time = -1f;
            base.OnDisable();
        }

        [ContextMenu("Play Guard Break")]
        public void Play()
        {
            if (!Application.isPlaying || !isActiveAndEnabled)
                return;

            PreparePlayback();
            playing = true;
            Sample(0f);
        }

        public void PreparePlayback()
        {
            playbackSeed = randomizeOnReplay ? System.Guid.NewGuid().GetHashCode() ^ seed : (int?)null;
        }

        public void Stop()
        {
            playing = false;
            Sample(-1f);
        }

        // Samples the entire sequence so the Inspector can preview it in Edit mode.
        public void Sample(float seconds)
        {
            time = seconds;
            SetVerticesDirty();
        }

        private void Update()
        {
            if (!playing)
                return;

            Sample(time + Time.unscaledDeltaTime);
            if (time >= Duration)
                Stop();
        }

        public override bool Raycast(Vector2 screenPoint, Camera eventCamera) => false;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (time < 0f || time >= Duration || rect.width <= 0f || rect.height <= 0f)
                return;

            Color fill = WithAlpha(fillColor, fillColor.a * fillOpacity);
            Color edge = crackColor;
            float crack = (time - CrackStart) / CrackDuration;
            if (crack <= 0f)
            {
                float reveal = FadeInDuration > 0f ? Mathf.SmoothStep(0f, 1f, time / FadeInDuration) : 1f;
                AddFace(mesh, new[] { rect.min, new Vector2(rect.xMin, rect.yMax), rect.max, new Vector2(rect.xMax, rect.yMin) },
                    WithAlpha(fill, fill.a * reveal));
                return;
            }

            Vector2 impact = new Vector2(Mathf.Clamp(impactPoint.x, 0.05f, 0.95f), Mathf.Clamp(impactPoint.y, 0.05f, 0.95f));
            EnsureLayout(rect.size, impact);

            if (crack < 1f)
            {
                AddFace(mesh, new[] { rect.min, new Vector2(rect.xMin, rect.yMax), rect.max, new Vector2(rect.xMax, rect.yMin) }, fill);
                DrawCracks(mesh, rect, crack, edge);
                return;
            }

            float progress = Mathf.Clamp01((time - CrackStart - CrackDuration) / ScatterDuration);
            DrawShards(mesh, rect, progress, fill, edge);
        }

        private void DrawCracks(VertexHelper mesh, Rect rect, float progress, Color edge)
        {
            Vector2 offset = rect.min;
            float width = rect.height * 0.0028f;

            foreach (Shard shard in shards)
            {
                for (int j = 0; j < shard.Points.Length; j++)
                {
                    Vector2 from = shard.Points[j];
                    Vector2 to = shard.Points[(j + 1) % shard.Points.Length];
                    if (GuardFractureLayout.OuterEdge(from, to, rect.size) || !PointComesFirst(from, to))
                        continue;

                    float start = shard.Arrival[j];
                    float end = shard.Arrival[(j + 1) % shard.Points.Length];
                    if (end < start)
                    {
                        (from, to) = (to, from);
                        (start, end) = (end, start);
                    }

                    if (progress <= start)
                        continue;

                    float length = Mathf.Clamp01((progress - start) / Mathf.Max(0.0001f, end - start));
                    float tail = CrackOpacity(progress - start);
                    float head = CrackOpacity(progress - Mathf.Lerp(start, end, length));
                    AddSeam(mesh, offset + from, offset + Vector2.Lerp(from, to, length), width,
                        WithAlpha(edge, edge.a * tail), head / tail);
                }
            }
        }

        private static float CrackOpacity(float age) =>
            Mathf.SmoothStep(0.24f, 1f, Mathf.Clamp01(age / 0.075f));

        private void DrawShards(VertexHelper mesh, Rect rect, float progress, Color fill, Color edge)
        {
            float scale = rect.height;
            Vector2 offset = rect.min;
            foreach (Shard shard in shards)
            {
                float u = Mathf.InverseLerp(shard.Delay, 1f, progress);
                float departure = u * u * (2.5f - 1.5f * u);
                float distance = 1f - Mathf.Pow(1f - departure, 1.56f);
                Vector2 movement = shard.Direction * (spread * scale * shard.Distance * distance)
                    + Vector2.down * (scale * fall * u * u);
                float turn = shard.Spin * rotation * departure * Mathf.Deg2Rad;
                float cos = Mathf.Cos(turn);
                float sin = Mathf.Sin(turn);
                float tip = Mathf.Lerp(1f, shard.Tip, departure);
                float fade = ShardOpacity(u);

                for (int i = 0; i < shard.Points.Length; i++)
                {
                    Vector2 p = shard.Points[i] - shard.Center;
                    p.y *= tip;
                    shard.Transformed[i] = offset + shard.Center + movement +
                        new Vector2(cos * p.x - sin * p.y, sin * p.x + cos * p.y);
                }

                AddFace(mesh, shard.Transformed, WithAlpha(fill, fill.a * fade), shard.Triangles);
            }

            // All faces go down first so the original crack remains visible at the handoff.
            foreach (Shard shard in shards)
            {
                float u = Mathf.InverseLerp(shard.Delay, 1f, progress);
                Color glint = WithAlpha(edge, edge.a * ShardOpacity(u));
                for (int i = 0; i < shard.Points.Length; i++)
                {
                    int next = (i + 1) % shard.Points.Length;
                    Vector2 from = shard.Points[i];
                    Vector2 to = shard.Points[next];
                    if (GuardFractureLayout.OuterEdge(from, to, rect.size))
                        continue;

                    // Keep each shared crack once at the handoff. Its second edge appears as the shard turns.
                    float edgeOpacity = PointComesFirst(from, to) ? 1f : Mathf.SmoothStep(0f, 0.72f, u / 0.26f);
                    AddSeam(mesh, shard.Transformed[i], shard.Transformed[next], scale * 0.0028f,
                        WithAlpha(glint, glint.a * edgeOpacity));
                }
            }
        }

        private static float ShardOpacity(float progress) =>
            1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.36f, 1f, progress));

        private void EnsureLayout(Vector2 size, Vector2 impact)
        {
            int count = Mathf.Clamp(fragmentCount, 6, 48);
            float variation = Mathf.Clamp01(sizeVariation);
            int chosenSeed = randomizeOnReplay ? playbackSeed ?? seed : seed;
            if (shards.Count > 0 && layoutSize == size && layoutImpact == impact &&
                layoutSeed == chosenSeed && layoutCount == count && Mathf.Approximately(layoutVariation, variation))
                return;

            layoutSize = size;
            layoutImpact = impact;
            layoutSeed = chosenSeed;
            layoutCount = count;
            layoutVariation = variation;
            shards.Clear();

            var random = new System.Random(chosenSeed);
            Vector2 origin = Vector2.Scale(size, impact);
            float meanArea = size.x * size.y / count;
            foreach (GuardFractureLayout.Cell cell in GuardFractureLayout.Build(size, count, variation, chosenSeed))
                CreateShard(cell, origin, meanArea, random);
        }

        private void CreateShard(GuardFractureLayout.Cell cell, Vector2 origin, float meanArea, System.Random random)
        {
            Vector2[] polygon = cell.Points;
            Vector2 center = Vector2.zero;
            float twiceArea = 0f;
            for (int i = 0; i < polygon.Length; i++)
            {
                Vector2 p = polygon[i];
                Vector2 next = polygon[(i + 1) % polygon.Length];
                center += p;
                twiceArea += p.x * next.y - p.y * next.x;
            }
            center /= polygon.Length;
            float weight = Mathf.InverseLerp(0.28f, 1.85f, Mathf.Abs(twiceArea) * 0.5f / meanArea);
            Vector2 direction = ((center - origin).normalized * 0.59f + RandomDirection(random) * 0.71f).normalized;
            float distance = SampleRange(random, 0.37f, 1.08f) * Mathf.Lerp(1.13f, 0.88f, weight);
            float spin = SampleRange(random, -1f, 1f);
            float delay = weight * 0.15f + SampleRange(random, 0.015f, 0.20f);
            shards.Add(new Shard(polygon, cell.Arrival, Triangulate(polygon), center, direction, distance, spin, delay,
                SampleRange(random, 0.26f, 0.83f)));
        }

        private static int[] Triangulate(Vector2[] polygon)
        {
            var ring = new List<int>(polygon.Length);
            var triangles = new List<int>((polygon.Length - 2) * 3);
            for (int i = 0; i < polygon.Length; i++)
                ring.Add(i);

            while (ring.Count > 3)
            {
                bool found = false;
                for (int i = 0; i < ring.Count; i++)
                {
                    int a = ring[(i + ring.Count - 1) % ring.Count];
                    int b = ring[i];
                    int c = ring[(i + 1) % ring.Count];
                    float cross = Cross(polygon[a], polygon[b], polygon[c]);
                    if (cross > 0.0001f)
                        continue;
                    if (cross >= -0.0001f)
                    {
                        ring.RemoveAt(i);
                        found = true;
                        break;
                    }

                    bool empty = true;
                    foreach (int other in ring)
                    {
                        Vector2 p = polygon[other];
                        if (other != a && other != b && other != c && Cross(polygon[a], polygon[b], p) <= 0f &&
                            Cross(polygon[b], polygon[c], p) <= 0f && Cross(polygon[c], polygon[a], p) <= 0f)
                        {
                            empty = false;
                            break;
                        }
                    }
                    if (!empty)
                        continue;
                    triangles.AddRange(new[] { a, b, c });
                    ring.RemoveAt(i);
                    found = true;
                    break;
                }
                if (!found)
                    break;
            }
            if (ring.Count == 3)
                triangles.AddRange(ring);
            return triangles.ToArray();
        }

        private static float Cross(Vector2 a, Vector2 b, Vector2 c) =>
            (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);

        private static bool PointComesFirst(Vector2 a, Vector2 b)
        {
            return Mathf.Abs(a.x - b.x) < 0.005f ? a.y < b.y : a.x < b.x;
        }

        private static void AddFace(VertexHelper mesh, Vector2[] points, Color color, int[] triangles = null)
        {
            if (color.a <= 0f)
                return;
            int start = mesh.currentVertCount;
            foreach (Vector2 point in points)
                mesh.AddVert(point, color, Vector2.zero);
            if (triangles != null)
            {
                for (int i = 0; i < triangles.Length; i += 3)
                    mesh.AddTriangle(start + triangles[i], start + triangles[i + 1], start + triangles[i + 2]);
            }
            else
            {
                for (int i = 1; i < points.Length - 1; i++)
                    mesh.AddTriangle(start, start + i, start + i + 1);
            }
        }

        private static void AddSeam(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color color, float headScale = 1f)
        {
            if (color.a <= 0f)
                return;
            Vector2 axis = b - a;
            Vector2 normal = new Vector2(-axis.y, axis.x).normalized * width;
            int start = mesh.currentVertCount;
            // Feather both sides so thin cracks stay smooth at different canvas scales.
            for (int i = -2; i <= 2; i++)
            {
                Color stripe = WithAlpha(color, color.a * (1f - 0.5f * Mathf.Abs(i)));
                Vector2 offset = normal * (i * 0.52f);
                mesh.AddVert(a + offset, stripe, Vector2.zero);
                mesh.AddVert(b + offset, WithAlpha(stripe, stripe.a * headScale), Vector2.zero);
            }
            for (int i = 0; i < 8; i += 2)
            {
                mesh.AddTriangle(start + i, start + i + 1, start + i + 3);
                mesh.AddTriangle(start + i, start + i + 3, start + i + 2);
            }
        }

        private static float SampleRange(System.Random random, float min, float max) =>
            Mathf.Lerp(min, max, (float)random.NextDouble());

        private static Vector2 RandomDirection(System.Random random)
        {
            float angle = SampleRange(random, 0f, 2f * Mathf.PI);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private sealed class Shard
        {
            public readonly Vector2[] Points;
            public readonly Vector2[] Transformed;
            public readonly float[] Arrival;
            public readonly int[] Triangles;
            public readonly Vector2 Center;
            public readonly Vector2 Direction;
            public readonly float Distance;
            public readonly float Spin;
            public readonly float Delay;
            public readonly float Tip;

            public Shard(Vector2[] points, float[] arrival, int[] triangles, Vector2 center, Vector2 direction, float distance, float spin, float delay, float tip)
            {
                Points = points;
                Arrival = arrival;
                Triangles = triangles;
                Transformed = new Vector2[points.Length];
                Center = center;
                Direction = direction;
                Distance = distance;
                Spin = spin;
                Delay = delay;
                Tip = tip;
            }
        }
    }
}
