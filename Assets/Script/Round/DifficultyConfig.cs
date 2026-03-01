using UnityEngine;

[CreateAssetMenu(fileName = "DifficultyConfig", menuName = "Round/DifficultyConfig")]
public class DifficultyConfig : ScriptableObject
{
    [Header("총 컬럼 수")]
    public int totalColumns = 10;

    [Header("스탯 배율 커브 (x: 컬럼 진행도 0~1, y: 배율)")]
    public AnimationCurve hpCurve = AnimationCurve.EaseInOut(0, 1f, 1f, 3f);
    //public AnimationCurve speedCurve = AnimationCurve.EaseInOut(0, 1f, 1f, 1.5f); 게이지 스피드

    [Header("타입별 추가 배율")]
    public float eliteMultiplier = 1.8f;  // 정예 배율
    public float bossMultiplier = 3.0f;  // 보스 배율    

    // 컬럼 인덱스 → 배율 계산
    public float GetHpMultiplier(int column, NodeType type)
    {
        float t = (float)column / totalColumns;         // 0~1 정규화
        float baseMultiplier = hpCurve.Evaluate(t);     // 커브에서 배율 조회
        return baseMultiplier * GetTypeMultiplier(type);
    }

    //public float GetSpeedMultiplier(int column, NodeType type)
    //{
    //    float t = (float)column / totalColumns;
    //    return speedCurve.Evaluate(t);
    //}

    float GetTypeMultiplier(NodeType type) => type switch
    {
        NodeType.Elite => eliteMultiplier,
        NodeType.Boss => bossMultiplier,
        _ => 1f
    };
}
