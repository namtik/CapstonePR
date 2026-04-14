using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BT
/// 입력:
///   - FCM 소속도 3개 (플레이어 유형)
///   - f1~f3 (FCM에 사용된 특성)
///   - f4 (오염도, FCM 외부에서 별도 계산)
///   - Top-4 경로 (타겟 슬롯 결정용)
/// </summary>
public class MonsterDecisionTree
{
    public enum Action { ComboShuffle = 0, SlotCurse = 1, NullInsert = 2 }

    public struct Decision
    {
        public Action ChosenAction;
        public string Reason;
        public int TargetSlot;
    }

    [System.Serializable]
    public class Thresholds
    {
        [Header("셔플 판정")]
        [Tooltip("실제 데이터: 러시형 f1=0.95, 의존형 f1=0.92 → 0.85 이상만 셔플 고려")]
        public float urgencyForShuffle = 0.85f;
        [Tooltip("실제 데이터: 러시형 f3=0.86, 의존형 f3=0.34 → 0.5로 분리")]
        public float repeatForFastCycle = 0.5f;

        [Header("저주 판정")]
        [Tooltip("실제 데이터: 의존형 f2=0.58 → 0.4 이상이면 경로 집중")]
        public float concentrationForCurse = 0.4f;
        [Tooltip("의존형에서 셔플로 전환하려면 f1이 매우 높아야 함")]
        public float urgencyOverrideForShuffle = 0.95f;

        [Header("무속성 전환점")]
        public float pollutionThreshold = 0.4f;

        [Header("탐색형 셔플")]
        [Tooltip("실제 데이터: 탐색형 f1=0.43 → 0.5 이상이면 셔플")]
        public float explorerUrgency = 0.5f;
    }

    private Thresholds t;

    public MonsterDecisionTree(Thresholds thresholds = null)
    {
        t = thresholds ?? new Thresholds();
    }

    /// <summary>
    /// 행동 결정
    /// </summary>
    /// <param name="membership">FCM 소속도 [러시, 의존, 탐색]</param>
    /// <param name="fcmFeatures">FCM 특성 [f1, f2, f3]</param>
    /// <param name="f4">오염도 (FCM 외부)</param>
    /// <param name="routes">Top-4 경로 분석</param>
    public Decision Decide(float[] membership, float[] fcmFeatures, float f4,
                           FeatureExtractor.RouteInfo[] routes)
    {
        float f1 = fcmFeatures[0], f2 = fcmFeatures[1], f3 = fcmFeatures[2];
        int dominant = FCMAnalyzer.GetDominantType(membership);

        switch (dominant)
        {
            case 0: // 콤보 러시형
                if (f1 >= t.urgencyForShuffle)
                {
                    if (f3 >= t.repeatForFastCycle)
                        return MakeShuffle("러시+긴급+빠른사이클->셔플");
                    return MakeCurse(routes, "러시+긴급+느린사이클->저주");
                }
                if (f4 >= t.pollutionThreshold)
                    return MakeCurse(routes, "러시+아직멀고+오염->저주");
                return MakeNullInsert(routes, "러시+아직멀고+깨끗->무속성");

            case 1: // 경로 의존형
                if (f2 >= t.concentrationForCurse)
                {
                    if (f1 >= t.urgencyOverrideForShuffle && f3 >= t.repeatForFastCycle)
                        return MakeShuffle("의존+집중+긴급+빠른사이클->셔플");
                    return MakeCurse(routes, "의존+경로집중->저주");
                }
                if (f4 >= t.pollutionThreshold)
                    return MakeCurse(routes, "의존+약한의존+오염->저주");
                return MakeNullInsert(routes, "의존+약한의존+깨끗->무속성");

            default: // 탐색/분산형
                if (f4 >= t.pollutionThreshold)
                {
                    if (f1 >= t.explorerUrgency && f3 >= t.repeatForFastCycle)
                        return MakeShuffle("탐색+오염+콤보접근+빠른사이클->셔플");
                    return MakeCurse(routes, "탐색+오염->저주");
                }
                return MakeNullInsert(routes, "탐색+깨끗->무속성");
        }
    }

    // --- 행동 생성 ---

    Decision MakeShuffle(string reason) => new Decision
    { ChosenAction = Action.ComboShuffle, Reason = reason, TargetSlot = -1 };

    Decision MakeCurse(FeatureExtractor.RouteInfo[] routes, string reason) => new Decision
    { ChosenAction = Action.SlotCurse, Reason = reason, TargetSlot = FindCurseTarget(routes) };

    Decision MakeNullInsert(FeatureExtractor.RouteInfo[] routes, string reason) => new Decision
    { ChosenAction = Action.NullInsert, Reason = reason, TargetSlot = FindNullInsertTarget(routes) };

    // --- 대상 슬롯 ---

    int FindCurseTarget(FeatureExtractor.RouteInfo[] routes)
    {
        ComboSystem combo = ComboSystem.Instance;
        ElementSlotSystem slotSys = ElementSlotSystem.Instance;
        if (combo == null || slotSys == null) return Random.Range(0, 4);

        int[] elemCount = new int[4];
        foreach (var r in routes)
        {
            if (r.BestProximity >= FeatureExtractor.THREAT_THRESHOLD && r.BestSkillIndex >= 0)
            {
                int[] seq = FeatureExtractor.ComboStringToIndices(
                    combo.learnedSkills[r.BestSkillIndex].combo);
                if (seq != null) foreach (int e in seq) elemCount[e]++;
            }
        }

        for (int i = 0; i < 4; i++)
        {
            var slot = slotSys.slots[i];
            if (slot.RemainingCount <= 0 || slot.IsCursed)
                elemCount[i] = -1;
        }

        int target = -1;
        for (int i = 0; i < 4; i++)
        {
            if (elemCount[i] <= 0) continue;
            if (target < 0 || elemCount[i] > elemCount[target]) target = i;
        }

        if (target < 0)
        {
            int maxCards = 0;
            for (int i = 0; i < 4; i++)
            {
                int remaining = slotSys.slots[i].RemainingCount;
                if (remaining > maxCards && !slotSys.slots[i].IsCursed)
                { maxCards = remaining; target = i; }
            }
            if (target < 0) target = Random.Range(0, 4);
        }

        return target;
    }

    int FindNullInsertTarget(FeatureExtractor.RouteInfo[] routes)
    {
        ComboSystem combo = ComboSystem.Instance;
        ElementSlotSystem slotSys = ElementSlotSystem.Instance;
        if (combo == null || slotSys == null) return Random.Range(0, 4);

        HashSet<int> needed = new HashSet<int>();
        foreach (var r in routes)
        {
            if (r.BestProximity >= FeatureExtractor.THREAT_THRESHOLD && r.BestSkillIndex >= 0)
            {
                int[] seq = FeatureExtractor.ComboStringToIndices(
                    combo.learnedSkills[r.BestSkillIndex].combo);
                if (seq != null) foreach (int e in seq) needed.Add(e);
            }
        }
        if (needed.Count == 0) needed = new HashSet<int> { 0, 1, 2, 3 };

        int bestSlot = -1;
        int minNull = int.MaxValue;
        foreach (int e in needed)
        {
            if (e < 0 || e >= 4) continue;
            var slot = slotSys.slots[e];
            if (slot.RemainingCount <= 0) continue;
            int nullCount = slot.neutralDeckCount + slot.neutralGraveCount
                          + (slot.hasNeutralCard ? 1 : 0);
            if (nullCount < minNull) { minNull = nullCount; bestSlot = e; }
        }

        if (bestSlot < 0)
        {
            minNull = int.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                var slot = slotSys.slots[i];
                int nc = slot.neutralDeckCount + slot.neutralGraveCount + (slot.hasNeutralCard ? 1 : 0);
                if (nc < minNull) { minNull = nc; bestSlot = i; }
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

    /// <summary>CSV 저장용 영문 코드 (인코딩 문제 방지)</summary>
    public static string GetActionCode(Action a) => a switch
    {
        Action.ComboShuffle => "shuffle",
        Action.SlotCurse => "curse",
        Action.NullInsert => "null_insert",
        _ => "unknown"
    };
}