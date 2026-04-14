using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 의사결정 트리
/// FCM 소속도 + 원본 특성값 + Top-4 경로 분석을 받아
/// 몬스터 행동과 대상 슬롯을 결정한다.
/// </summary>
public class MonsterDecisionTree
{
    public enum Action { ComboShuffle = 0, SlotCurse = 1, NullInsert = 2 }

    public struct Decision
    {
        public Action  ChosenAction;
        public string  Reason;
        public int     TargetSlot; // 0~3, 또는 -1(슬롯 무관)
    }

    // ─── 밸런스 임계값 ───
    [System.Serializable]
    public class Thresholds
    {
        [Header("셔플 판정")]
        public float urgencyForShuffle = 0.6f;
        public float repeatForFastCycle = 0.5f;

        [Header("저주 판정")]
        public float concentrationForCurse = 0.5f;
        public float urgencyOverrideForShuffle = 0.7f;

        [Header("무속성 전환점")]
        public float pollutionThreshold = 0.4f;

        [Header("탐색형 셔플")]
        public float explorerUrgency = 0.5f;
    }

    private Thresholds t;

    public MonsterDecisionTree(Thresholds thresholds = null)
    {
        t = thresholds ?? new Thresholds();
    }

    /// <summary>
    /// 행동 결정 메인 메서드
    /// </summary>
    public Decision Decide(float[] membership, float[] features, FeatureExtractor.RouteInfo[] routes)
    {
        float f1 = features[0], f2 = features[1], f3 = features[2], f4 = features[3];
        int dominant = FCMAnalyzer.GetDominantType(membership);

        switch (dominant)
        {
            case 0: // 콤보 러시형
                if (f1 >= t.urgencyForShuffle)
                    return MakeShuffle(f3 >= t.repeatForFastCycle
                        ? "러시+긴급+빠른사이클→셔플" : "러시+긴급→셔플");
                if (f4 >= t.pollutionThreshold)
                    return MakeCurse(routes, "러시+아직멀고+오염→저주");
                return MakeNullInsert(routes, "러시+아직멀고+깨끗→무속성");

            case 1: // 경로 의존형
                if (f2 >= t.concentrationForCurse)
                {
                    if (f1 >= t.urgencyOverrideForShuffle)
                        return MakeShuffle("의존+집중+긴급→셔플");
                    return MakeCurse(routes, "의존+경로집중→저주");
                }
                if (f4 >= t.pollutionThreshold)
                    return MakeCurse(routes, "의존+약한의존+오염→저주");
                return MakeNullInsert(routes, "의존+약한의존+깨끗→무속성");

            default: // 탐색/분산형
                if (f4 >= t.pollutionThreshold)
                {
                    if (f1 >= t.explorerUrgency)
                        return MakeShuffle("탐색+오염+콤보접근→셔플");
                    return MakeCurse(routes, "탐색+오염→저주");
                }
                return MakeNullInsert(routes, "탐색+깨끗→무속성");
        }
    }

    // ─── 행동 생성 ───

    Decision MakeShuffle(string reason) => new Decision
    {
        ChosenAction = Action.ComboShuffle,
        Reason = reason,
        TargetSlot = -1
    };

    Decision MakeCurse(FeatureExtractor.RouteInfo[] routes, string reason) => new Decision
    {
        ChosenAction = Action.SlotCurse,
        Reason = reason,
        TargetSlot = FindCurseTarget(routes)
    };

    Decision MakeNullInsert(FeatureExtractor.RouteInfo[] routes, string reason) => new Decision
    {
        ChosenAction = Action.NullInsert,
        Reason = reason,
        TargetSlot = FindNullInsertTarget(routes)
    };

    // ─── 대상 슬롯 결정 ───

    /// <summary>저주: 위협 콤보에 가장 많이 등장하는 속성 슬롯</summary>
    int FindCurseTarget(FeatureExtractor.RouteInfo[] routes)
    {
        ComboSystem combo = ComboSystem.Instance;
        if (combo == null) return Random.Range(0, 4);

        int[] elemCount = new int[4];
        foreach (var r in routes)
        {
            if (r.BestProximity >= 0.33f && r.BestSkillIndex >= 0)
            {
                int[] seq = FeatureExtractor.ComboStringToIndices(
                    combo.learnedSkills[r.BestSkillIndex].combo);
                if (seq != null)
                    foreach (int e in seq) elemCount[e]++;
            }
        }

        int target = 0;
        for (int i = 1; i < 4; i++)
            if (elemCount[i] > elemCount[target]) target = i;
        return target;
    }

    /// <summary>무속성: 위협 콤보에 필요하면서 가장 덜 오염된 슬롯</summary>
    int FindNullInsertTarget(FeatureExtractor.RouteInfo[] routes)
    {
        ComboSystem combo = ComboSystem.Instance;
        ElementSlotSystem slotSys = ElementSlotSystem.Instance;
        if (combo == null || slotSys == null) return Random.Range(0, 4);

        HashSet<int> needed = new HashSet<int>();
        foreach (var r in routes)
        {
            if (r.BestProximity >= 0.33f && r.BestSkillIndex >= 0)
            {
                int[] seq = FeatureExtractor.ComboStringToIndices(
                    combo.learnedSkills[r.BestSkillIndex].combo);
                if (seq != null) foreach (int e in seq) needed.Add(e);
            }
        }

        if (needed.Count == 0)
            needed = new HashSet<int> { 0, 1, 2, 3 };

        int bestSlot = 0;
        int minNull = int.MaxValue;

        foreach (int e in needed)
        {
            if (e < 0 || e >= 4) continue;
            var slot = slotSys.slots[e];
            int nullCount = slot.neutralDeckCount + slot.neutralGraveCount
                          + (slot.hasNeutralCard ? 1 : 0);
            if (nullCount < minNull)
            {
                minNull = nullCount;
                bestSlot = e;
            }
        }

        return bestSlot;
    }

    public static string GetActionName(Action a) => a switch
    {
        Action.ComboShuffle => "콤보 셔플",
        Action.SlotCurse => "슬롯 저주",
        Action.NullInsert => "무속성 삽입",
        _ => "?"
    };
}
