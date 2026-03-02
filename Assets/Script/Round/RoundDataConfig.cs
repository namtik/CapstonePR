using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RoundDataConfig", menuName = "Round/RoundDataConfig")]
public class RoundDataConfig : ScriptableObject
{
    [Header("전투 (일반)")]
    public List<CombatRoundData> combatPool;

    [Header("정예")]
    public List<EliteRoundData> elitePool;

    [Header("보스")]
    public List<BossRoundData> bossPool;

    [Header("상점/휴식/사건")]
    public ShopRoundData shopData;
    public RestRoundData restData;
    public EventRoundData eventData;

    // NodeType에 맞는 RoundData를 랜덤 반환
    public RoundData GetRoundData(NodeType type)
    {
        return type switch
        {
            NodeType.Combat => GetRandom(combatPool),
            NodeType.Elite => GetRandom(elitePool),
            NodeType.Boss => GetRandom(bossPool),
            NodeType.Shop => shopData,
            NodeType.Rest => restData,
            NodeType.Event => eventData,
            _ => null
        };
    }

    T GetRandom<T>(List<T> pool) where T : RoundData
    {
        if (pool == null || pool.Count == 0)
        {
            Debug.LogWarning($"RoundDataConfig: {typeof(T).Name} 풀이 비어있습니다!");
            return null;
        }
        return pool[Random.Range(0, pool.Count)];
    }
}
