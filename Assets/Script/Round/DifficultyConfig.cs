using UnityEngine;

[CreateAssetMenu(fileName = "DifficultyConfig", menuName = "Round/DifficultyConfig")]
public class DifficultyConfig : ScriptableObject
{
    [Header("�� �÷� ��")]
    public int totalColumns = 10; // 전체 열(컬럼) 수

    [Header("���� ���� Ŀ�� (x: �÷� ���൵ 0~1, y: ����)")]
    public AnimationCurve hpCurve = AnimationCurve.EaseInOut(0, 1f, 1f, 3f); // 진행도별 체력 배율 곡선
    public AnimationCurve attackCountCurve = AnimationCurve.EaseInOut(0, 1f, 1f, 3f); // 진행도별 공격 횟수 배율 곡선

    [Header("Ÿ�Ժ� �߰� ����")]
    public float eliteMultiplier = 1.8f; // 정예 추가 배율
    public float bossMultiplier = 2.0f; // 보스 추가 배율

    // 열 인덱스와 노드 타입으로 체력 배율을 계산한다.
    public float GetHpMultiplier(int column, NodeType type)
    {
        float t = (float)column / totalColumns;
        float baseMultiplier = hpCurve.Evaluate(t);
        return baseMultiplier * GetTypeMultiplier(type);
    }

    // 기본 공격 횟수에 진행도/타입 배율을 적용해 최종 공격 횟수를 계산한다.
    public int GetAttackCount(int baseCount, int column, NodeType type)
    {
        float t = (float)column / totalColumns;
        float scaled = baseCount * attackCountCurve.Evaluate(t) * GetTypeMultiplier(type);
        return Mathf.Max(1, Mathf.RoundToInt(scaled));
    }


    // 노드 타입별 추가 배율을 반환한다.
    float GetTypeMultiplier(NodeType type) => type switch
    {
        NodeType.Elite => eliteMultiplier,
        NodeType.Boss => bossMultiplier,
        _ => 1f
    };

}
