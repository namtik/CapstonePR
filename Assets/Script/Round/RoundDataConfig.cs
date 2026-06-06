using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RoundDataConfig", menuName = "Round/RoundDataConfig")]
public class RoundDataConfig : ScriptableObject
{
    [Header("���� (�Ϲ�)")]
    public List<CombatRoundData> combatPool;

    [Header("����")]
    public List<EliteRoundData> elitePool;

    [Header("����")]
    public List<BossRoundData> bossPool;

    [Header("����/�޽�/���")]
    public ShopRoundData shopData;
    public RestRoundData restData;
    public EventRoundData eventData;
    public RelicRoundData relicData;

    // NodeType�� �´� RoundData�� ���� ��ȯ
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
            NodeType.Relic => relicData,
            _ => null
        };
    }

    T GetRandom<T>(List<T> pool) where T : RoundData
    {
        if (pool == null || pool.Count == 0)
        {
            Debug.LogWarning($"RoundDataConfig: {typeof(T).Name} Ǯ�� ����ֽ��ϴ�!");
            return null;
        }
        return pool[Random.Range(0, pool.Count)];
    }
}
