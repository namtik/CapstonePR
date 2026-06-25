using UnityEngine;

[CreateAssetMenu(fileName = "DifficultyConfig", menuName = "Round/DifficultyConfig")]
public class DifficultyConfig : ScriptableObject
{
    [Header("Total Columns")]
    public int totalColumns = 10;

    [Header("Stage Scale Curves (x: column progress, y: multiplier)")]
    public AnimationCurve hpCurve = AnimationCurve.EaseInOut(0, 1f, 1f, 3f);
    public AnimationCurve attackCountCurve = AnimationCurve.EaseInOut(0, 1f, 1f, 3f);

    [Header("Type Multipliers")]
    public float eliteMultiplier = 1.8f;
    public float bossMultiplier = 2.0f;

    // 1??? ? ?? ? (0? ?? ~ ??? ??)
    public int ColumnsPerLap => totalColumns + 1;

    // ? ?? + ??? ?? ?? ?? ??? ???? ?? (2?? 0? = 11????).
    public int GetEffectiveColumn(int mapColumn, int completedLaps)
    {
        return mapColumn + completedLaps * ColumnsPerLap;
    }

    public float GetHpMultiplier(int column, NodeType type)
    {
        float t = (float)column / totalColumns;
        float baseMultiplier = EvaluateCurveWithLinearExtrapolation(hpCurve, t);
        return baseMultiplier * GetTypeMultiplier(type);
    }

    public int GetAttackCount(int baseCount, int column, NodeType type)
    {
        float t = (float)column / totalColumns;
        float scaled = baseCount * EvaluateCurveWithLinearExtrapolation(attackCountCurve, t) * GetTypeMultiplier(type);
        return Mathf.Max(1, Mathf.RoundToInt(scaled));
    }

    // t>1(2바퀴 이후 등)에서도 Clamp로 배율이 멈추지 않도록 마지막 키 기울기로 선형 외삽
    static float EvaluateCurveWithLinearExtrapolation(AnimationCurve curve, float t)
    {
        if (curve == null || curve.length == 0)
            return 1f;

        if (t <= 1f)
            return curve.Evaluate(t);

        Keyframe last = curve.keys[curve.length - 1];
        float slope = last.outTangent;
        if (float.IsInfinity(slope) || float.IsNaN(slope))
            slope = 0f;

        return last.value + slope * (t - last.time);
    }

    float GetTypeMultiplier(NodeType type) => type switch
    {
        NodeType.Elite => eliteMultiplier,
        NodeType.Boss => bossMultiplier,
        _ => 1f
    };
}
