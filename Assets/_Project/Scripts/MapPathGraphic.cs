using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 경로 곡선 모양 설정 (MapManager 인스펙터에서 조절)
[System.Serializable]
public class MapPathShapeSettings
{
    [Header("휨 (Bow)")]
    [Tooltip("노드 간 거리 대비 최대 휨 비율. 0이면 휘지 않음")]
    [Range(0f, 0.5f)] public float bowStrength = 0.1f;
    [Tooltip("경로별 휨 랜덤 배율 범위 (음수=반대 방향)")]
    public Vector2 bowRandomRange = new Vector2(-1f, 1f);

    [Header("잔물결 (Wobble)")]
    [Tooltip("노드 간 거리 대비 잔물결 진폭 비율. 0이면 잔물결 없음")]
    [Range(0f, 0.2f)] public float wobbleStrength = 0.03f;
    [Tooltip("경로별 잔물결 진폭 랜덤 배율 범위")]
    public Vector2 wobbleRandomRange = new Vector2(0.5f, 1.5f);
    [Tooltip("곡선 구간 안의 반 파장 개수 (클수록 물결이 촘촘)")]
    [Range(0f, 12f)] public float wobbleHalfWaves = 3f;

    [Header("곡선 형태")]
    [Tooltip("베지어 핸들 비율. 클수록 수평으로 오래 가다 꺾임")]
    [Range(0f, 1f)] public float handleRatio = 0.44f;
    [Tooltip("경로별 핸들 비율 랜덤 편차 (±)")]
    [Range(0f, 0.5f)] public float handleRatioVariance = 0.1f;
}

[RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
public sealed class MapPathGraphic : MaskableGraphic
{
    private Vector2 start;
    private Vector2 end;
    private float thickness;
    private bool dashed;
    private int priority; // 겹칠 때 누가 그릴지 (높을수록 우선: 갈 수 있는 길 > 지나간 길 > 앞으로 길 > 놓친 길)

    // 경로별 불규칙 변형 (from/to 인덱스로 시드 -> 다시 그려도 모양 유지)
    private int seed;
    private float handleRatio = 0.44f; // 베지어 핸들 비율
    private float bowAmount;           // 현 수직방향 휨 (px)
    private float wobbleAmount;        // 잔물결 진폭 (px)
    private float wobblePhase;
    private float wobbleHalfWaves = 3f; // 곡선 구간 안의 반 파장 개수
    private bool straight;              // true면 휨/잔물결 없이 두 노드를 직선으로 잇는다

    // 같은 노드에서 나가고/들어오는 경로들은 노드 쪽 끝이 같은 수평 직선으로 시작해 한동안 거의 포개진다.
    // 점선 위상을 공유 노드 기준(호길이)으로 맞추고, 다른 경로와 포개진 조각은 우선순위 높은 경로만 그려
    // 겹치는 구간이 하나의 점선으로 보이게 한다.
    private const float StubLength = 48f;   // 노드 양쪽 공용 직선 길이 (px)
    private const float DashPeriod = 24f;   // 점선 주기 (px)
    private const float DashLength = 11f;   // 점선 한 조각 길이 (px)
    private const int CurveSamples = 48;    // 곡선 구간 호길이 샘플 수

    private Vector2 curveStart, curveEnd;   // 곡선 구간 시작/끝 (공용 직선 끝)
    private float stub;                     // 실제 적용된 공용 직선 길이
    private readonly float[] curveArc = new float[CurveSamples + 1];
    private readonly Vector2[] curvePoints = new Vector2[CurveSamples + 1];
    private float totalLength;

    private IReadOnlyList<MapPathGraphic> siblings; // 같은 맵의 모든 경로 (겹침 판정용)

    public int FromIndex { get; private set; }
    public int ToIndex { get; private set; }

    public void Initialize(int fromIndex, int toIndex, RectTransform from, RectTransform to, float maxThickness,
        IReadOnlyList<MapPathGraphic> allPaths = null, MapPathShapeSettings shape = null, bool straight = false)
    {
        this.straight = straight;
        if (shape == null) shape = new MapPathShapeSettings();
        FromIndex = fromIndex;
        ToIndex = toIndex;
        siblings = allPaths;
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

        seed = unchecked(fromIndex * 73856093 ^ toIndex * 19349663 ^ 0x5bd1e995);
        var rng = new System.Random(seed);
        float chord = Vector2.Distance(start, end);
        handleRatio = shape.handleRatio + RandomRange(rng, -shape.handleRatioVariance, shape.handleRatioVariance);
        bowAmount = RandomRange(rng, shape.bowRandomRange.x, shape.bowRandomRange.y) * chord * shape.bowStrength;
        wobbleAmount = Mathf.Abs(RandomRange(rng, shape.wobbleRandomRange.x, shape.wobbleRandomRange.y) * chord * shape.wobbleStrength);
        wobblePhase = RandomRange(rng, 0f, Mathf.PI * 2f);
        wobbleHalfWaves = shape.wobbleHalfWaves;
        if (straight) bowAmount = wobbleAmount = 0f;

        // 휨만큼 렉트 여유 확보 (마스크 컬링 대비)
        rectTransform.sizeDelta += Vector2.one * 2f * (Mathf.Abs(bowAmount) + wobbleAmount);

        BuildPath();
        SetVerticesDirty();
    }

    public void SetStyle(Color tint, float width, bool dashed, int priority = 0)
    {
        color = tint;
        if (thickness == width && this.dashed == dashed && this.priority == priority) return;

        thickness = width;
        this.dashed = dashed;
        this.priority = priority;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (totalLength <= 0f || thickness <= 0f) return;

        if (!dashed)
        {
            AddStroke(mesh, 0f, totalLength, Mathf.Max(8, Mathf.CeilToInt(totalLength / 6f)));
            return;
        }

        // 점선 중심 위치: 앞쪽 절반은 출발 노드 기준 k·주기, 뒤쪽 절반은 도착 노드 기준 k·주기.
        // 같은 노드를 공유하는 경로끼리 위상이 같아서 포개진 구간의 조각이 정확히 겹친다.
        var centers = new List<float>();
        var fromStart = new List<bool>();
        float halfLength = totalLength * 0.5f;
        float lastA = 0f;
        for (float c = 0f; c <= halfLength; c += DashPeriod)
        {
            centers.Add(c); fromStart.Add(true); lastA = c;
        }
        var tail = new List<float>();
        float firstB = totalLength;
        for (float c = totalLength; c > halfLength; c -= DashPeriod)
        {
            tail.Add(c); firstB = c;
        }
        // 가운데 이음매 간격 보정: 너무 붙으면 하나 빼고, 너무 벌어지면 하나 넣는다
        float seam = firstB - lastA;
        if (tail.Count > 0 && seam < DashPeriod * 0.6f)
        {
            tail.RemoveAt(tail.Count - 1);
        }
        else if (seam > DashPeriod * 1.5f)
        {
            centers.Add((lastA + firstB) * 0.5f); fromStart.Add(true);
        }
        for (int i = tail.Count - 1; i >= 0; i--)
        {
            centers.Add(tail[i]); fromStart.Add(false);
        }

        var rng = new System.Random(seed);
        float half = DashLength * 0.5f;
        for (int i = 0; i < centers.Count; i++)
        {
            float c = centers[i];
            // 노드 근처(포개지는 곳)는 흔들지 않고, 가운데로 갈수록 손그림 느낌으로 흔든다
            float envelope = Mathf.Sin(Mathf.PI * c / totalLength);
            envelope *= envelope;
            float h = half * (1f + RandomRange(rng, -0.3f, 0.3f) * envelope);
            float shift = RandomRange(rng, -0.35f, 0.35f) * half * envelope;
            float widthScale = 1f + RandomRange(rng, -0.2f, 0.12f) * envelope;
            float sideOffset = RandomRange(rng, -0.3f, 0.3f) * thickness * envelope;

            if (IsCoveredBySibling(c, fromStart[i])) continue;
            AddStroke(mesh, c + shift - h, c + shift + h, 3, widthScale, sideOffset);
        }
    }

    // 같은 노드를 공유하는 더 우선순위 높은 경로가 같은 거리 지점에서 거의 같은 곳을 지나면 그 경로가 대신 그린다.
    private bool IsCoveredBySibling(float s, bool measuredFromStart)
    {
        if (siblings == null) return false;

        Vector2 mine = PointInParent(s);
        for (int i = 0; i < siblings.Count; i++)
        {
            var other = siblings[i];
            if (other == null || other == this || !other.isActiveAndEnabled || other.totalLength <= 0f) continue;

            bool shares = measuredFromStart ? other.FromIndex == FromIndex : other.ToIndex == ToIndex;
            if (!shares || !Outranks(other, measuredFromStart)) continue;

            float otherS = measuredFromStart ? s : other.totalLength - (totalLength - s);
            if (otherS < 0f || otherS > other.totalLength) continue;

            float tolerance = Mathf.Max(thickness, other.thickness);
            if ((other.PointInParent(otherS) - mine).sqrMagnitude < tolerance * tolerance) return true;
        }
        return false;
    }

    // other가 이 경로보다 그릴 우선권이 있는지 (같으면 인덱스가 작은 쪽)
    private bool Outranks(MapPathGraphic other, bool sharedStart)
    {
        if (other.priority != priority) return other.priority > priority;
        return sharedStart ? other.ToIndex < ToIndex : other.FromIndex < FromIndex;
    }

    private Vector2 PointInParent(float s)
    {
        Evaluate(s, out Vector2 point, out _);
        return rectTransform.anchoredPosition + point;
    }

    private static float RandomRange(System.Random rng, float min, float max)
    {
        return min + (float)rng.NextDouble() * (max - min);
    }

    private float DirectionX
    {
        get
        {
            float dirX = Mathf.Sign(end.x - start.x);
            return dirX == 0f ? 1f : dirX;
        }
    }

    // 노드쪽 직선 + 곡선 + 노드쪽 직선으로 이루어진 경로를 호길이 기준으로 준비한다.
    private void BuildPath()
    {
        totalLength = 0f;
        if (Vector2.Distance(start, end) <= 0f) return;

        float dirX = DirectionX;
        stub = straight ? 0f : Mathf.Min(StubLength, Mathf.Abs(end.x - start.x) * 0.2f);
        curveStart = start + new Vector2(dirX * stub, 0f);
        curveEnd = end - new Vector2(dirX * stub, 0f);

        float arc = 0f;
        for (int i = 0; i <= CurveSamples; i++)
        {
            curvePoints[i] = CurvePoint((float)i / CurveSamples);
            if (i > 0) arc += Vector2.Distance(curvePoints[i - 1], curvePoints[i]);
            curveArc[i] = arc;
        }
        totalLength = stub * 2f + arc;
    }

    // 곡선 구간 위의 점 (u: 0~1). 휨은 sin²(πu)로 감싸 양 끝 기울기가 0 -> 직선과 매끄럽게 이어진다.
    private Vector2 CurvePoint(float u)
    {
        Vector2 delta = curveEnd - curveStart;
        if (straight) return curveStart + delta * u;
        float handleX = delta.x * handleRatio;
        float inverse = 1f - u;
        float blend = 3f * inverse * u;
        Vector2 point = curveStart + new Vector2(
            blend * (inverse * handleX + u * (delta.x - handleX)) + u * u * u * delta.x,
            (blend * u + u * u * u) * delta.y);

        Vector2 chordNormal = delta.sqrMagnitude > 0f ? new Vector2(-delta.y, delta.x).normalized : Vector2.zero;
        float envelope = Mathf.Sin(Mathf.PI * u);
        envelope *= envelope;
        float lateral = envelope * (bowAmount + wobbleAmount * Mathf.Sin(u * Mathf.PI * wobbleHalfWaves + wobblePhase));
        return point + chordNormal * lateral;
    }

    // 호길이 s 위치의 점과 진행 방향 (이 그래픽의 로컬 좌표)
    private void Evaluate(float s, out Vector2 point, out Vector2 tangent)
    {
        s = Mathf.Clamp(s, 0f, totalLength);
        float dirX = DirectionX;

        if (s <= stub)
        {
            point = start + new Vector2(dirX * s, 0f);
            tangent = new Vector2(dirX, 0f);
            return;
        }
        if (s >= totalLength - stub)
        {
            point = end - new Vector2(dirX * (totalLength - s), 0f);
            tangent = new Vector2(dirX, 0f);
            return;
        }

        float cs = s - stub;
        int lo = 0, hi = CurveSamples;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) >> 1;
            if (curveArc[mid] < cs) lo = mid; else hi = mid;
        }
        float segment = curveArc[hi] - curveArc[lo];
        float f = segment > 0f ? (cs - curveArc[lo]) / segment : 0f;
        point = Vector2.Lerp(curvePoints[lo], curvePoints[hi], f);
        tangent = curvePoints[hi] - curvePoints[lo];
        if (tangent.sqrMagnitude <= 0f) tangent = new Vector2(dirX, 0f);
    }

    private void AddStroke(VertexHelper mesh, float from, float to, int steps,
        float widthScale = 1f, float sideOffset = 0f)
    {
        from = Mathf.Clamp(from, 0f, totalLength);
        to = Mathf.Clamp(to, 0f, totalLength);
        if (to <= from) return;

        int baseIndex = mesh.currentVertCount;
        for (int i = 0; i <= steps; i++)
        {
            Evaluate(Mathf.Lerp(from, to, (float)i / steps), out Vector2 point, out Vector2 tangent);
            Vector2 unitNormal = new Vector2(-tangent.y, tangent.x).normalized;
            point += unitNormal * sideOffset;
            Vector2 normal = unitNormal * (thickness * 0.5f * widthScale);
            mesh.AddVert(point + normal, color, Vector2.zero);
            mesh.AddVert(point - normal, color, Vector2.zero);

            if (i == 0) continue;
            int vertex = baseIndex + 2 * i;
            mesh.AddTriangle(vertex - 2, vertex, vertex - 1);
            mesh.AddTriangle(vertex - 1, vertex, vertex + 1);
        }
    }
}
