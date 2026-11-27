using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RoundDataConfig", menuName = "Round/RoundDataConfig")]
public class RoundDataConfig : ScriptableObject
{
    [Header("���� (�Ϲ�)")]
    public List<CombatRoundData> combatPool; // 일반 전투 라운드 풀

    [Header("����")]
    public List<EliteRoundData> elitePool; // 정예 전투 라운드 풀

    [Header("����")]
    public List<BossRoundData> bossPool; // 보스 라운드 풀

    [Header("����/�޽�/���")]
    public ShopRoundData shopData; // 상점 라운드 데이터
    public RestRoundData restData; // 휴식 라운드 데이터
    public EventRoundData eventData; // 이벤트 라운드 데이터
    public RelicRoundData relicData; // 유물 라운드 데이터

    // NodeType에 맞는 RoundData를 골라 반환한다.
    public RoundData GetRoundData(NodeType type)
    {
        return type switch
        {
            NodeType.Combat => GetRandom(combatPool),
            NodeType.Elite => GetRandom(elitePool),
            NodeType.Boss => GetRandomBoss(),
            NodeType.Shop => shopData,
            NodeType.Rest => restData,
            NodeType.Event => eventData,
            NodeType.Relic => relicData,
            _ => null
        };
    }

    // 이번 런에서 이미 잡은 보스는 빼고 고른다(남은 보스가 없으면 전체 풀에서).
    BossRoundData GetRandomBoss()
    {
        var state = GameStateController.Instance;
        if (state != null && bossPool != null && state.defeatedBossRounds.Count > 0)
        {
            var remaining = new List<BossRoundData>();
            foreach (var b in bossPool)
                if (b != null && !state.defeatedBossRounds.Contains(b)) remaining.Add(b);
            if (remaining.Count > 0) return remaining[Random.Range(0, remaining.Count)];
        }
        return GetRandom(bossPool);
    }

    // 풀에서 무작위 항목을 하나 반환한다(비어 있으면 null).
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
