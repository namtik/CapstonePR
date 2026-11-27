using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Flags]
public enum AwakeningScreenEdges
{
    None = 0,
    Top = 1,
    Left = 2,
    Right = 4,
    Bottom = 8,
    Default = Top | Left | Right
}

[AddComponentMenu("")]
public sealed class AwakeningBrushGraphic : MaskableGraphic
{
    private const float LayoutWidth = 960f;
    private const float LayoutHeight = 540f;
    private const int RibbonSegments = 12;
    private static readonly AwakeningScreenEdges[] Edges =
    {
        AwakeningScreenEdges.Top, AwakeningScreenEdges.Left, AwakeningScreenEdges.Right, AwakeningScreenEdges.Bottom
    };

    private struct Stroke
    {
        public Vector2 origin;
        public Vector2 direction;
        public float length;
        public float halfWidth;
        public float travel;
        public float curve;
        public float strength;
        public float variation;
        public int bristles;
        public int flecks;
        public int seed;
    }

    private readonly List<Stroke> strokes = new List<Stroke>(32);
    private readonly List<Vector2> currentOrigins = new List<Vector2>(32);
    private readonly List<Vector2> previousOrigins = new List<Vector2>(32);
    private System.Random random;
    private float age;
    private float opacity;
    private Color tint;

    internal void RebuildLayout(AwakeningScreenEdges edges, int count, float length, float width, float travel,
        float variation, float spacing, float spawnSpread, bool mirrorTop, float detail)
    {
        previousOrigins.Clear();
        previousOrigins.AddRange(currentOrigins);
        currentOrigins.Clear();
        strokes.Clear();
        float perimeter = 0f;
        foreach (var edge in Edges)
            if ((edges & edge) != 0)
                perimeter += EdgeLength(edge);

        if (perimeter <= 0f || count <= 0)
        {
            SetVerticesDirty();
            return;
        }

        if (random == null)
            random = new System.Random(unchecked(Environment.TickCount * 397 ^ GetInstanceID()));
        float variationAmount = Mathf.Clamp01(variation);
        float targetGapSquared = spacing * spacing;
        int strokeCount = Mathf.Clamp(count, 0, 32);
        for (int i = 0; i < strokeCount; i++)
        {
            // Try fresh positions first. If the edge is crowded, use the best sampled gap.
            float bestScore = -1f;
            Vector2 origin = default;
            var chosen = AwakeningScreenEdges.Top;
            for (int attempt = 0; attempt < 28; attempt++)
            {
                var edge = PickEdge(edges, perimeter);
                Vector2 candidate = SampleOrigin(edge, spawnSpread);
                float score = Mathf.Min(NearestSquared(candidate, currentOrigins),
                    NearestSquared(candidate, previousOrigins) * 3.2f);
                if (score > bestScore)
                {
                    bestScore = score;
                    origin = candidate;
                    chosen = edge;
                }
                if (score >= targetGapSquared)
                    break;
            }

            Vector2 direction = GetDirection(chosen, origin.x, mirrorTop, variationAmount);
            float strokeLength = Mathf.Max(1f, length * (1f + RandomRange(-variationAmount, variationAmount)));
            float strokeTravel = Mathf.Max(0f, travel * RandomRange(.72f, 1.3f));
            if (chosen == AwakeningScreenEdges.Top && mirrorTop)
            {
                float room = Mathf.Abs(LayoutWidth * .5f - origin.x) - 5f;
                float reach = Mathf.Abs(direction.x) * (strokeLength + strokeTravel);
                float scale = reach > room ? Mathf.Clamp(room / Mathf.Max(.01f, reach), .15f, 1f) : 1f;
                strokeLength *= scale;
                strokeTravel *= scale;
            }

            strokes.Add(new Stroke
            {
                origin = origin,
                direction = direction,
                length = strokeLength,
                halfWidth = Mathf.Max(.5f, width * .5f * (1f + RandomRange(-variationAmount, variationAmount))),
                travel = strokeTravel,
                curve = strokeLength * (.19f + RandomRange(-.6f, .6f) * variationAmount),
                strength = RandomRange(.71f, 1f),
                variation = variationAmount,
                bristles = Mathf.RoundToInt(Mathf.Clamp01(detail) * RandomRange(0f, 4f)),
                flecks = Mathf.RoundToInt(Mathf.Clamp01(detail) * RandomRange(0f, 10f)),
                seed = random.Next()
            });
            currentOrigins.Add(origin);
        }

        SetVerticesDirty();
    }

    internal void Show(float progress, Color brushTint, float brushOpacity)
    {
        bool previouslyVisible = opacity > .002f;
        age = Mathf.Clamp01(progress);
        tint = brushTint;
        opacity = Mathf.Clamp01(brushOpacity * brushTint.a);
        if (strokes.Count > 0 && (previouslyVisible || opacity > .002f))
            SetVerticesDirty();
    }

    internal void ClearLayout()
    {
        strokes.Clear();
        currentOrigins.Clear();
        previousOrigins.Clear();
        opacity = 0f;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = rectTransform.rect;
        if (opacity <= .002f || rect.width <= 0f || rect.height <= 0f)
            return;

        float pixel = rect.height / LayoutHeight;
        float slide = Ease(age / .86f);
        float extend = Mathf.Lerp(.63f, 1f, Ease(age / .48f));
        Color warm = new Color(tint.r * .79f, tint.g * .55f, tint.b * .4f, 1f);
        Color pale = Color.Lerp(tint, Color.white, .41f);

        foreach (var stroke in strokes)
        {
            Vector2 start = new Vector2(rect.xMin + rect.width * stroke.origin.x / LayoutWidth,
                rect.yMax - rect.height * stroke.origin.y / LayoutHeight);
            Vector2 direction = new Vector2(stroke.direction.x, -stroke.direction.y);
            Vector2 normal = new Vector2(-direction.y, direction.x);
            start += direction * (slide * stroke.travel * pixel);
            Vector2 path = direction * (stroke.length * extend * pixel);
            float halfWidth = stroke.halfWidth * pixel;
            float bend = stroke.curve * pixel;
            float strength = opacity * stroke.strength;

            DrawRibbon(mesh, start, path, normal, halfWidth, bend, strength, warm, pale, stroke.seed, stroke.variation);
            for (int i = 0; i < stroke.bristles; i++)
            {
                int index = i * 6;
                float side = i % 2 == 0 ? 1f : -1f;
                float offset = side * halfWidth * Mathf.Lerp(.7f, 2f, Unit(stroke.seed, index));
                float drift = Mathf.Lerp(-7f, 10f, Unit(stroke.seed, index + 1)) * pixel;
                float size = Mathf.Lerp(.48f, .90f, Unit(stroke.seed, index + 2));
                float width = halfWidth * Mathf.Lerp(.16f, .38f, Unit(stroke.seed, index + 3));
                DrawRibbon(mesh, start + normal * offset + direction * drift, path * size,
                    normal, width, bend * .83f, strength * .82f, warm, pale, stroke.seed + i + 1, stroke.variation);
            }

            for (int i = 0; i < stroke.flecks; i++)
            {
                int index = 20 + i * 5;
                float along = Mathf.Lerp(.10f, 1.10f, Unit(stroke.seed, index));
                float away = Mathf.Lerp(-17f, 17f, Unit(stroke.seed, index + 1)) * pixel;
                float radius = Mathf.Lerp(.52f, 1.65f, Unit(stroke.seed, index + 2)) * pixel;
                float alpha = strength * Mathf.Lerp(.36f, .8f, Unit(stroke.seed, index + 3));
                DrawFleck(mesh, start + path * along + normal * away, radius, pale, alpha);
            }
        }
    }

    private void DrawRibbon(VertexHelper mesh, Vector2 start, Vector2 path, Vector2 normal, float width,
        float bend, float strength, Color warm, Color pale, int seed, float variation)
    {
        int first = mesh.currentVertCount;
        for (int i = 0; i <= RibbonSegments; i++)
        {
            float t = i / (float)RibbonSegments;
            Vector2 middle = start + path * t + normal * (bend * t * (1f - t));
            float taper = width * (.72f + .28f * Mathf.Sin(t * Mathf.PI)) * (1f - t);
            float jagged = 1f - variation * .72f * Unit(seed, 53 + i);
            Color shaded = t < .35f ? Color.Lerp(warm, tint, t / .35f) : Color.Lerp(tint, pale, (t - .35f) / .65f);
            float alpha = strength * Mathf.Lerp(.29f, 1f, Ease(t / .26f)) * (1f - Ease((t - .62f) / .38f));
            shaded.a = alpha;
            Color lower = shaded;
            lower.a *= .76f + .24f * Unit(seed, 68 + i);
            AddVertex(mesh, middle + normal * (taper * jagged), shaded);
            AddVertex(mesh, middle - normal * taper, lower);
            if (i == 0)
                continue;

            int p = first + i * 2;
            mesh.AddTriangle(p - 2, p, p - 1);
            mesh.AddTriangle(p, p + 1, p - 1);
        }
    }

    private static void DrawFleck(VertexHelper mesh, Vector2 point, float radius, Color color, float alpha)
    {
        int first = mesh.currentVertCount;
        color.a = alpha;
        AddVertex(mesh, point, color);
        color.a = 0f;
        AddVertex(mesh, point + Vector2.right * radius, color);
        AddVertex(mesh, point + Vector2.up * radius, color);
        AddVertex(mesh, point - Vector2.right * radius, color);
        AddVertex(mesh, point - Vector2.up * radius, color);
        mesh.AddTriangle(first, first + 2, first + 1);
        mesh.AddTriangle(first, first + 3, first + 2);
        mesh.AddTriangle(first, first + 4, first + 3);
        mesh.AddTriangle(first, first + 1, first + 4);
    }

    private static void AddVertex(VertexHelper mesh, Vector2 point, Color color)
    {
        var vertex = UIVertex.simpleVert;
        vertex.position = point;
        vertex.color = color;
        mesh.AddVert(vertex);
    }

    private AwakeningScreenEdges PickEdge(AwakeningScreenEdges allowed, float perimeter)
    {
        float offset = RandomRange(0f, perimeter);
        var last = AwakeningScreenEdges.Top;
        foreach (var edge in Edges)
        {
            if ((allowed & edge) == 0)
                continue;
            last = edge;
            offset -= EdgeLength(edge);
            if (offset <= 0f)
                break;
        }

        return last;
    }

    private Vector2 SampleOrigin(AwakeningScreenEdges edge, float spread)
    {
        float inset = RandomRange(-spread, spread);
        switch (edge)
        {
            case AwakeningScreenEdges.Left: return new Vector2(inset, RandomRange(22f, LayoutHeight - 24f));
            case AwakeningScreenEdges.Right: return new Vector2(LayoutWidth - inset, RandomRange(22f, LayoutHeight - 24f));
            case AwakeningScreenEdges.Bottom: return new Vector2(RandomRange(35f, LayoutWidth - 35f), LayoutHeight - inset);
            default:
                float x = RandomRange(38f, LayoutWidth - 38f);
                if (Mathf.Abs(x - LayoutWidth * .5f) < 24f)
                    x += x < LayoutWidth * .5f ? -24f : 24f;
                return new Vector2(x, inset);
        }
    }

    private Vector2 GetDirection(AwakeningScreenEdges edge, float x, bool mirrorTop, float variation)
    {
        float lean = RandomRange(-variation, variation);
        float inward = x < LayoutWidth * .5f ? 1f : -1f;
        switch (edge)
        {
            case AwakeningScreenEdges.Left: return new Vector2(1f, lean).normalized;
            case AwakeningScreenEdges.Right: return new Vector2(-1f, lean).normalized;
            case AwakeningScreenEdges.Bottom: return new Vector2(inward * (.7f + lean * .45f), -1f).normalized;
            default:
                return mirrorTop ? new Vector2(inward * (1.15f + lean * .83f), 1f).normalized : Vector2.up;
        }
    }

    private static float NearestSquared(Vector2 point, List<Vector2> samples)
    {
        float distance = float.PositiveInfinity;
        foreach (Vector2 sample in samples)
            distance = Mathf.Min(distance, (sample - point).sqrMagnitude);
        return distance;
    }

    private static float EdgeLength(AwakeningScreenEdges edge)
    {
        return edge == AwakeningScreenEdges.Top || edge == AwakeningScreenEdges.Bottom ? 884f : 494f;
    }

    private float RandomRange(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());

    private static float Unit(int seed, int index)
    {
        unchecked
        {
            uint value = (uint)seed ^ ((uint)index * 2654435761u);
            value ^= value >> 16;
            value *= 1540483477u;
            value ^= value >> 13;
            return (value & 65535u) / 65535f;
        }
    }

    private static float Ease(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }
}
