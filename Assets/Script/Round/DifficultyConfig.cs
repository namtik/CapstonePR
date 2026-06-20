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
        float baseMultiplier = hpCurve.Evaluate(t);
        return baseMultiplier * GetTypeMultiplier(type);
    }

    public int GetAttackCount(int baseCount, int column, NodeType type)
    {
        float t = (float)column / totalColumns;
        float scaled = baseCount * attackCountCurve.Evaluate(t) * GetTypeMultiplier(type);
        return Mathf.Max(1, Mathf.RoundToInt(scaled));
    }

    float GetTypeMultiplier(NodeType type) => type switch
    {
        NodeType.Elite => eliteMultiplier,
        NodeType.Boss => bossMultiplier,
        _ => 1f
    };
}
