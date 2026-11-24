using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
public sealed class MapPathGraphic : MaskableGraphic
{
    private Vector2 start;
    private Vector2 end;
    private float thickness;
    private bool dashed;

    public int FromIndex { get; private set; }
    public int ToIndex { get; private set; }

    public void Initialize(int fromIndex, int toIndex, RectTransform from, RectTransform to, float maxThickness)
    {
        FromIndex = fromIndex;
        ToIndex = toIndex;
        raycastTarget = false;

        Vector2 a = from.anchoredPosition;
        Vector2 b = to.anchoredPosition;
        float direction = Mathf.Sign(b.x - a.x);
        float maxInset = Mathf.Abs(b.x - a.x) * 0.3f;
        a.x += direction * Mathf.Min(from.rect.width * 0.18f, maxInset);
        b.x -= direction * Mathf.Min(to.rect.width * 0.18f, maxInset);

        Vector2 midpoint = (a + b) * 0.5f;
        rectTransform.anchorMin = rectTransform.anchorMax = from.anchorMin;
        rectTransform.anchoredPosition = midpoint;
        rectTransform.sizeDelta = new Vector2(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y))
            + Vector2.one * maxThickness;
        start = a - midpoint;
        end = b - midpoint;
        SetVerticesDirty();
    }

    public void SetStyle(Color tint, float width, bool dashed)
    {
        color = tint;
        if (thickness == width && this.dashed == dashed) return;

        thickness = width;
        this.dashed = dashed;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        float length = Vector2.Distance(start, end);
        if (length <= 0f || thickness <= 0f) return;

        if (!dashed)
        {
            AddStroke(mesh, 0f, 1f, 24);
            return;
        }

        // 첫 번째와 마지막 점선이 각각 노드 쪽 끝까지 닿도록 배치한다.
        int dashIntervals = Mathf.Max(1, Mathf.RoundToInt(length / 16f));
        for (int i = 0; i <= dashIntervals; i++)
        {
            float first = Mathf.Clamp01((i - 0.2f) / dashIntervals);
            float last = Mathf.Clamp01((i + 0.2f) / dashIntervals);
            AddStroke(mesh, first, last, 3);
        }
    }

    private void AddStroke(VertexHelper mesh, float first, float last, int steps)
    {
        int baseIndex = mesh.currentVertCount;
        Vector2 delta = end - start;
        float handleX = delta.x * 0.44f;

        for (int i = 0; i <= steps; i++)
        {
            float t = Mathf.Lerp(first, last, (float)i / steps);
            float inverse = 1f - t;
            float blend = 3f * inverse * t;
            Vector2 point = start + new Vector2(
                blend * (inverse * handleX + t * (delta.x - handleX)) + t * t * t * delta.x,
                (blend * t + t * t * t) * delta.y);
            Vector2 tangent = new Vector2(
                3f * (inverse * inverse + t * t) * handleX + 2f * blend * (delta.x - 2f * handleX),
                2f * blend * delta.y);
            Vector2 normal = new Vector2(-tangent.y, tangent.x).normalized * (thickness * 0.5f);
            mesh.AddVert(point + normal, color, Vector2.zero);
            mesh.AddVert(point - normal, color, Vector2.zero);

            if (i == 0) continue;
            int vertex = baseIndex + 2 * i;
            mesh.AddTriangle(vertex - 2, vertex, vertex - 1);
            mesh.AddTriangle(vertex - 1, vertex, vertex + 1);
        }
    }
}
