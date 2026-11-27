using System.Collections.Generic;
using UnityEngine;

// 일반 전투는 스테이지마다, 정예 전투는 라운드(바퀴)마다 등장 조합 프리셋을 직접 정한다.
// 보스는 RoundManager가 이 에셋을 쓰지 않는다.
[CreateAssetMenu(fileName = "EnemyEncounterConfig", menuName = "Round/EnemyEncounterConfig")]
public class EnemyEncounterConfig : ScriptableObject
{
    [System.Serializable]
    public class EncounterPreset
    {
        [Tooltip("이 프리셋이 뽑히면 여기 넣은 몬스터가 모두 등장한다.")]
        public List<EnemyData> monsters = new List<EnemyData>();
    }

    [System.Serializable]
    public class StageEncounter
    {
        [Tooltip("이 스테이지 일반 전투에서 하나 랜덤으로 고르는 등장 조합. 개수는 자유롭게 늘린다.")]
        public List<EncounterPreset> presets = new List<EncounterPreset>();
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

    [System.Serializable]
    public class EliteLapEncounter
    {
        [Tooltip("라운드 번호. 1=1라운드(1바퀴), 2=2라운드(2바퀴).")]
        [Min(1)]
        public int lap = 1;

        [Tooltip("이 라운드의 정예 전투에서 하나 랜덤으로 고르는 등장 조합. 개수는 자유롭게 늘린다.")]
        public List<EncounterPreset> presets = new List<EncounterPreset>();
    }

    [Tooltip("바퀴당 일반 전투 스테이지 수(보스 제외). 기본 9.")]
    [Min(1)]
    public int stagesPerLap = 9;

    [Tooltip("바퀴별 스테이지 설정. 각 스테이지마다 등장 조합 프리셋을 따로 정한다.")]
    public List<LapEncounter> laps = new List<LapEncounter>();

    [Tooltip("라운드별 정예 등장 조합. 정예 노드는 스테이지가 랜덤이라 스테이지 칸 대신 여기 둔다.")]
    public List<EliteLapEncounter> eliteLaps = new List<EliteLapEncounter>();

    // 일반 전투 등장 명단을 구성한다. 유효한 프리셋이 없으면 빈 리스트.
    public List<EnemyData> ComposeCombatEnemies(int lap, int displayStage)
    {
        StageEncounter stage = FindStage(lap, displayStage);
        EncounterPreset preset = PickPreset(stage != null ? stage.presets : null);
        return CopyMonsters(preset);
    }

    // 정예 전투 등장 명단을 구성한다. 해당 라운드에 유효한 프리셋이 없으면 빈 리스트.
    public List<EnemyData> ComposeEliteEnemies(int lap)
    {
        EliteLapEncounter eliteLap = FindEliteLap(lap);
        EncounterPreset preset = PickPreset(eliteLap != null ? eliteLap.presets : null);
        return CopyMonsters(preset);
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

    EliteLapEncounter FindEliteLap(int lap)
    {
        if (eliteLaps == null) return null;
        for (int i = 0; i < eliteLaps.Count; i++)
        {
            EliteLapEncounter eliteLap = eliteLaps[i];
            if (eliteLap != null && eliteLap.lap == lap)
                return eliteLap;
        }
        return null;
    }

    // 인스펙터에서 빠진 바퀴/스테이지 칸과 1·2라운드 정예 칸을 채운다.
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

        if (eliteLaps == null) eliteLaps = new List<EliteLapEncounter>();
        EnsureEliteLap(1);
        EnsureEliteLap(2);
        for (int i = 0; i < eliteLaps.Count; i++)
        {
            EliteLapEncounter eliteLap = eliteLaps[i];
            if (eliteLap == null)
            {
                eliteLap = new EliteLapEncounter { lap = i + 1 };
                eliteLaps[i] = eliteLap;
            }
            if (eliteLap.lap < 1) eliteLap.lap = i + 1;
            if (eliteLap.presets == null) eliteLap.presets = new List<EncounterPreset>();
        }
    }

    void EnsureEliteLap(int lap)
    {
        for (int i = 0; i < eliteLaps.Count; i++)
            if (eliteLaps[i] != null && eliteLaps[i].lap == lap) return;
        eliteLaps.Add(new EliteLapEncounter { lap = lap, presets = new List<EncounterPreset>() });
    }

    void OnValidate()
    {
        EnsureLayout();
    }

    static EncounterPreset PickPreset(List<EncounterPreset> presets)
    {
        if (presets == null) return null;

        var valid = new List<EncounterPreset>();
        for (int i = 0; i < presets.Count; i++)
        {
            EncounterPreset preset = presets[i];
            if (HasAny(preset)) valid.Add(preset);
        }
        if (valid.Count == 0) return null;
        return valid[Random.Range(0, valid.Count)];
    }

    static List<EnemyData> CopyMonsters(EncounterPreset preset)
    {
        var result = new List<EnemyData>();
        if (preset == null || preset.monsters == null) return result;
        for (int i = 0; i < preset.monsters.Count; i++)
            if (preset.monsters[i] != null) result.Add(preset.monsters[i]);
        return result;
    }

    static bool HasAny(EncounterPreset preset)
    {
        if (preset == null || preset.monsters == null) return false;
        for (int i = 0; i < preset.monsters.Count; i++)
            if (preset.monsters[i] != null) return true;
        return false;
    }
}
