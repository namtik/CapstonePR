using UnityEditor;
using UnityEngine;

// DifficultyConfig 커스텀 인스펙터.
// 기본 AnimationCurve 프리뷰만으로는 스케일링이 잘 안 보여서(특히 평평한 커브),
// 컬럼별 실제 배율(HP/공격횟수)을 선 그래프로 직접 그려 확실히 보이게 한다.
[CustomEditor(typeof(DifficultyConfig))]
public class DifficultyConfigEditor : Editor
{
    static readonly Color HpColor  = new Color(0.30f, 0.80f, 0.95f); // HP 배율 선 색
    static readonly Color BgColor  = new Color(0.15f, 0.15f, 0.17f); // 그래프 배경
    static readonly Color GridColor = new Color(1f, 1f, 1f, 0.08f);  // 격자
    static readonly Color AxisColor = new Color(1f, 1f, 1f, 0.35f);  // 축

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var cfg = (DifficultyConfig)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("난이도 스케일링 그래프 (HP 배율)", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("x: 컬럼(0~totalColumns), y: 배율", EditorStyles.miniLabel);

        Rect area = GUILayoutUtility.GetRect(10, 220, GUILayout.ExpandWidth(true));
        DrawGraph(area, cfg);
    }

    static void DrawGraph(Rect area, DifficultyConfig cfg)
    {
        if (Event.current.type != EventType.Repaint) return;

        EditorGUI.DrawRect(area, BgColor);

        const float padL = 40f, padR = 12f, padT = 10f, padB = 20f;
        Rect inner = new Rect(area.x + padL, area.y + padT,
                              area.width - padL - padR, area.height - padT - padB);
        if (inner.width <= 1f || inner.height <= 1f) return;

        int cols = Mathf.Max(1, cfg.totalColumns);
        int samples = Mathf.Clamp(cols, 2, 200);

        // 샘플링 — 컬럼 0..cols 구간(커브 정의역 t=0..1)
        var hp = new float[samples + 1];
        float maxY = 1f, minY = 0f;
        for (int i = 0; i <= samples; i++)
        {
            float t = (float)i / samples;
            hp[i]  = cfg.hpCurve != null && cfg.hpCurve.length > 0 ? cfg.hpCurve.Evaluate(t) : 1f;
            maxY = Mathf.Max(maxY, hp[i]);
            minY = Mathf.Min(minY, hp[i]);
        }
        maxY = Mathf.Ceil(maxY * 1.05f);
        minY = Mathf.Floor(Mathf.Min(0f, minY));
        if (Mathf.Approximately(maxY, minY)) maxY = minY + 1f;

        // 격자 + y축 라벨
        var labelStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
        labelStyle.normal.textColor = new Color(1f, 1f, 1f, 0.6f);
        int yDiv = 4;
        for (int g = 0; g <= yDiv; g++)
        {
            float v = Mathf.Lerp(minY, maxY, (float)g / yDiv);
            float y = MapY(v, minY, maxY, inner);
            EditorGUI.DrawRect(new Rect(inner.x, y, inner.width, 1f), GridColor);
            GUI.Label(new Rect(area.x, y - 8f, padL - 4f, 16f), v.ToString("0.##"), labelStyle);
        }

        // x축 라벨(양끝 + 중앙)
        var xStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.UpperCenter };
        xStyle.normal.textColor = new Color(1f, 1f, 1f, 0.6f);
        for (int g = 0; g <= 2; g++)
        {
            float frac = g / 2f;
            float x = inner.x + frac * inner.width;
            EditorGUI.DrawRect(new Rect(x, inner.y, 1f, inner.height), GridColor);
            int colLabel = Mathf.RoundToInt(frac * cols);
            GUI.Label(new Rect(x - 20f, inner.yMax + 2f, 40f, 16f), colLabel.ToString(), xStyle);
        }

        // y=1 기준선(배율 1.0 강조)
        if (minY < 1f && maxY > 1f)
        {
            float y1 = MapY(1f, minY, maxY, inner);
            EditorGUI.DrawRect(new Rect(inner.x, y1, inner.width, 1f), AxisColor);
        }

        DrawLine(hp, minY, maxY, inner, HpColor);
    }

    static float MapY(float v, float minY, float maxY, Rect inner)
        => inner.yMax - (v - minY) / (maxY - minY) * inner.height;

    static void DrawLine(float[] values, float minY, float maxY, Rect inner, Color color)
    {
        int n = values.Length;
        if (n < 2) return;

        // 인스펙터(스크롤뷰)에서 Handles가 어긋나는 문제를 피하려고 DrawRect 선분으로 직접 그린다.
        const float thick = 2f;
        float prevX = inner.x;
        float prevY = MapY(values[0], minY, maxY, inner);
        for (int i = 1; i < n; i++)
        {
            float curX = inner.x + (float)i / (n - 1) * inner.width;
            float curY = MapY(values[i], minY, maxY, inner);

            // 두 점 사이를 세로로 채워 연결(샘플이 촘촘해 매끄러운 선처럼 보임)
            float segTop = Mathf.Min(prevY, curY);
            float segBot = Mathf.Max(prevY, curY);
            float x = prevX;
            float w = Mathf.Max(thick, curX - prevX + thick);
            EditorGUI.DrawRect(new Rect(x, segTop - thick * 0.5f, w, (segBot - segTop) + thick), color);

            prevX = curX;
            prevY = curY;
        }
    }
}
