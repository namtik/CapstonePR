using System.Collections.Generic;
using UnityEngine;

// 일반 전투에서 스테이지마다 마릿수와 몬스터 풀을 직접 정한다.
// 정예/보스는 RoundManager가 이 에셋을 쓰지 않고 기존처럼 1마리다.
[CreateAssetMenu(fileName = "EnemyEncounterConfig", menuName = "Round/EnemyEncounterConfig")]
public class EnemyEncounterConfig : ScriptableObject
{
    [System.Serializable]
    public class StageEncounter
    {
        [Tooltip("이 스테이지 일반 전투에 동시에 등장할 몬스터 수.")]
        [Min(1)]
        public int enemyCount = 1;

        [Tooltip("이 스테이지에서 랜덤으로 뽑을 몬스터 후보. 비우면 폴백 풀을 쓴다.")]
        public List<EnemyData> monsterPool = new List<EnemyData>();

        [Tooltip("ON이면 같은 몬스터가 한 전투에 중복될 수 있다. OFF면 가능한 한 중복을 피한다.")]
        public bool allowDuplicates = true;
    }

    [System.Serializable]
    public class LapEncounter
    {
        [Tooltip("바퀴 번호. 1=1바퀴, 2=2바퀴.")]
        [Min(1)]
        public int lap = 1;

        [Tooltip("이 바퀴의 스테이지 목록. 인덱스 0 = n-1, 인덱스 8 = n-9.")]
        public List<StageEncounter> stages = new List<StageEncounter>();
    }

    [Tooltip("바퀴당 일반 전투 스테이지 수(보스 제외). 기본 9.")]
    [Min(1)]
    public int stagesPerLap = 9;

    [Tooltip("바퀴별 스테이지 설정. 각 스테이지마다 마릿수와 몬스터 풀을 따로 정한다.")]
    public List<LapEncounter> laps = new List<LapEncounter>();

    [Tooltip("해당 스테이지의 몬스터 풀이 비었을 때 쓰는 폴백 후보.")]
    public List<EnemyData> fallbackPool = new List<EnemyData>();

    // 일반 전투 등장 명단을 구성한다. 후보가 없으면 빈 리스트.
    public List<EnemyData> ComposeCombatEnemies(int lap, int displayStage)
    {
        StageEncounter stage = FindStage(lap, displayStage);
        int count = stage != null ? Mathf.Max(1, stage.enemyCount) : 1;
        bool allowDup = stage == null || stage.allowDuplicates;
        List<EnemyData> pool = ResolvePool(stage);
        return PickFromPool(pool, count, allowDup);
    }

    StageEncounter FindStage(int lap, int displayStage)
    {
        if (laps == null || displayStage < 1) return null;
        for (int i = 0; i < laps.Count; i++)
        {
            LapEncounter lapData = laps[i];
            if (lapData == null || lapData.lap != lap || lapData.stages == null) continue;
            int idx = displayStage - 1;
            if (idx >= 0 && idx < lapData.stages.Count)
                return lapData.stages[idx];
        }
        return null;
    }

    List<EnemyData> ResolvePool(StageEncounter stage)
    {
        if (stage != null && HasAny(stage.monsterPool))
            return stage.monsterPool;
        return fallbackPool;
    }

    // 인스펙터에서 빠진 바퀴/스테이지 칸을 채운다.
    public void EnsureLayout()
    {
        int stageCount = Mathf.Max(1, stagesPerLap);
        if (laps == null) laps = new List<LapEncounter>();
        if (laps.Count == 0)
        {
            laps.Add(new LapEncounter { lap = 1, stages = new List<StageEncounter>() });
            laps.Add(new LapEncounter { lap = 2, stages = new List<StageEncounter>() });
        }

        for (int i = 0; i < laps.Count; i++)
        {
            LapEncounter lapData = laps[i];
            if (lapData == null)
            {
                lapData = new LapEncounter { lap = i + 1 };
                laps[i] = lapData;
            }
            if (lapData.lap < 1) lapData.lap = i + 1;
            if (lapData.stages == null) lapData.stages = new List<StageEncounter>();
            while (lapData.stages.Count < stageCount)
                lapData.stages.Add(new StageEncounter());
        }
    }

    void OnValidate()
    {
        EnsureLayout();
        for (int i = 0; i < laps.Count; i++)
        {
            LapEncounter lapData = laps[i];
            if (lapData == null || lapData.stages == null) continue;
            for (int s = 0; s < lapData.stages.Count; s++)
            {
                StageEncounter stage = lapData.stages[s];
                if (stage == null) continue;
                stage.enemyCount = Mathf.Max(1, stage.enemyCount);
            }
        }
    }

    static List<EnemyData> PickFromPool(List<EnemyData> pool, int count, bool allowDuplicates)
    {
        var result = new List<EnemyData>(count);
        if (pool == null || count <= 0) return result;

        var available = new List<EnemyData>();
        for (int i = 0; i < pool.Count; i++)
            if (pool[i] != null) available.Add(pool[i]);
        if (available.Count == 0) return result;

        if (allowDuplicates)
        {
            for (int i = 0; i < count; i++)
                result.Add(available[Random.Range(0, available.Count)]);
            return result;
        }

        Shuffle(available);
        int unique = Mathf.Min(count, available.Count);
        for (int i = 0; i < unique; i++)
            result.Add(available[i]);
        for (int i = unique; i < count; i++)
            result.Add(available[Random.Range(0, available.Count)]);
        return result;
    }

    static bool HasAny(List<EnemyData> list)
    {
        if (list == null) return false;
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null) return true;
        return false;
    }

    static void Shuffle(List<EnemyData> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
