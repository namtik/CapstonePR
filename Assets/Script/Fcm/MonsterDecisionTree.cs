using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BT
/// 입력:
///   - FCM 소속도 3개 (플레이어 유형)
///   - f1~f3 (FCM에 사용된 특성)
///   - f4 (오염도)
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
        public float urgencyForShuffle = 0.70f;
        public float repeatForFastCycle = 0.50f;

        [Header("저주 판정")]
        public float densityForRush = 0.50f;
        public float urgencyOverrideForShuffle = 0.90f;

        [Header("무속성 전환점")]
        public float pollutionThreshold = 0.40f;

        [Header("탐색형 셔플")]
        public float explorerUrgency = 0.50f;

        [Header("버스트 판정")]
        public float burstReplenishThreshold = 0.50f;
        public float burstBalanceThreshold = 0.40f;
    }

    private Thresholds t;

    public MonsterDecisionTree(Thresholds thresholds = null)
    {
        t = thresholds ?? new Thresholds();
    }

    public Decision Decide(float[] membership, float[] fcm,
                           float f4, FeatureExtractor.ThreatInfo[] threats)
    {
        // fcm 인덱스: [0]=f1, [1]=f3, [2]=f5, [3]=f7, [4]=f8
        float f1 = fcm[0], f3 = fcm[1], f5 = fcm[2], f7 = fcm[3], f8 = fcm[4];
        int dominant = FCMAnalyzer.GetDominantType(membership);

        bool hasChain = false;
        foreach (var th in threats)
            if (th.HasChain && th.Proximity >= FeatureExtractor.THREAT_THRESHOLD)
            { hasChain = true; break; }

        bool isFast = (f3 >= t.repeatForFastCycle) || hasChain;

        switch (dominant)
        {
            case 0: // 콤보 러시형: f1 높고 f7 높고 f3 높음
                if (f1 >= t.urgencyForShuffle && isFast)
                    return MakeShuffle(threats, hasChain
                        ? "러시+긴급+체인->셔플" : "러시+긴급+빠른사이클->셔플");
                if (f1 >= t.urgencyForShuffle)
                    return MakeCurse(threats, "러시+긴급+느린사이클->저주");
                if (f4 >= t.pollutionThreshold)
                    return MakeCurse(threats, "러시+아직멀고+오염->저주");
                return MakeNullInsert(threats, "러시+아직멀고+깨끗->무속성");

            case 1: // 경로 의존형: f1 중간, f7 중간, f3 낮음
                if (f7 < t.densityForRush) // 위협이 적으면 집중
                {
                    if (f1 >= t.urgencyOverrideForShuffle && isFast)
                        return MakeShuffle(threats, "의존+긴급+빠름->셔플");
                    return MakeCurse(threats, "의존+위협집중->저주");
                }
                if (f4 >= t.pollutionThreshold)
                    return MakeCurse(threats, "의존+위협분산+오염->저주");
                return MakeNullInsert(threats, "의존+위협분산+깨끗->무속성");

            case 3: // 버스트형: f5 높고 f8 낮음
                if (f5 >= t.burstReplenishThreshold && f8 < t.burstBalanceThreshold)
                    return MakeNullInsert(threats, "버스트+보충임박+불균형->무속성(보충전삽입)");
                if (f1 >= t.urgencyForShuffle)
                    return MakeShuffle(threats, "버스트+콤보접근->셔플");
                return MakeCurse(threats, "버스트+대기->저주");

            default: // 탐색/분산형: 전부 낮음
                if (f4 >= t.pollutionThreshold)
                {
                    if (f1 >= t.explorerUrgency && isFast)
                        return MakeShuffle(threats, "탐색+오염+콤보접근->셔플");
                    return MakeCurse(threats, "탐색+오염->저주");
                }
                return MakeNullInsert(threats, "탐색+깨끗->무속성");
        }
    }

    Decision MakeShuffle(FeatureExtractor.ThreatInfo[] threats, string reason) => new Decision
    { ChosenAction = Action.ComboShuffle, Reason = reason, TargetSlot = -1 };
    Decision MakeCurse(FeatureExtractor.ThreatInfo[] threats, string reason) => new Decision
    { ChosenAction = Action.SlotCurse, Reason = reason, TargetSlot = FindCurseTarget(threats) };
    Decision MakeNullInsert(FeatureExtractor.ThreatInfo[] threats, string reason) => new Decision
    { ChosenAction = Action.NullInsert, Reason = reason, TargetSlot = FindNullInsertTarget(threats) };

    int FindCurseTarget(FeatureExtractor.ThreatInfo[] threats)
    {
        ComboSystem combo = ComboSystem.Instance;
        ElementSlotSystem slotSys = ElementSlotSystem.Instance;
        if (combo == null || slotSys == null) return Random.Range(0, 4);

        int[] elemCount = new int[4];
        foreach (var th in threats)
        {
            if (th.Proximity >= FeatureExtractor.THREAT_THRESHOLD && th.SkillIndex >= 0)
            {
                int[] seq = FeatureExtractor.ComboStringToIndices(
                    combo.learnedSkills[th.SkillIndex].combo);
                if (seq != null) foreach (int e in seq) elemCount[e]++;
                if (th.HasChain && th.ChainSkillIndex >= 0)
                {
                    int[] cs = FeatureExtractor.ComboStringToIndices(
                        combo.learnedSkills[th.ChainSkillIndex].combo);
                    if (cs != null) foreach (int e in cs) elemCount[e]++;
                }
            }
        }

        for (int i = 0; i < 4; i++)
            if (slotSys.slots[i].RemainingCount <= 0 || slotSys.slots[i].IsCursed) elemCount[i] = -1;

        int target = -1;
        for (int i = 0; i < 4; i++)
            if (elemCount[i] > 0 && (target < 0 || elemCount[i] > elemCount[target])) target = i;

        if (target < 0)
        {
            int mx = 0;
            for (int i = 0; i < 4; i++)
            {
                int r = slotSys.slots[i].RemainingCount;
                if (r > mx && !slotSys.slots[i].IsCursed) { mx = r; target = i; }
            }
            if (target < 0) target = Random.Range(0, 4);
        }
        return target;
    }

    int FindNullInsertTarget(FeatureExtractor.ThreatInfo[] threats)
    {
        ComboSystem combo = ComboSystem.Instance;
        ElementSlotSystem slotSys = ElementSlotSystem.Instance;
        if (combo == null || slotSys == null) return Random.Range(0, 4);

        HashSet<int> needed = new HashSet<int>();
        foreach (var th in threats)
            if (th.Proximity >= FeatureExtractor.THREAT_THRESHOLD && th.SkillIndex >= 0)
            {
                int[] seq = FeatureExtractor.ComboStringToIndices(
                    combo.learnedSkills[th.SkillIndex].combo);
                if (seq != null) foreach (int e in seq) needed.Add(e);
            }
        if (needed.Count == 0) needed = new HashSet<int> { 0, 1, 2, 3 };

        int best = -1, minN = int.MaxValue;
        foreach (int e in needed)
        {
            if (e < 0 || e >= 4) continue;
            var slot = slotSys.slots[e];
            if (slot.RemainingCount <= 0) continue;
            int nc = slot.neutralDeckCount + slot.neutralGraveCount + (slot.hasNeutralCard ? 1 : 0);
            if (nc < minN) { minN = nc; best = e; }
        }
        if (best < 0)
        {
            minN = int.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                var slot = slotSys.slots[i];
                int nc = slot.neutralDeckCount + slot.neutralGraveCount + (slot.hasNeutralCard ? 1 : 0);
                if (nc < minN) { minN = nc; best = i; }
            }
        }
        return best;
    }

    public static string GetActionName(Action a) => a switch
    {
        Action.ComboShuffle => "콤보 셔플",
        Action.SlotCurse => "슬롯 저주",
        Action.NullInsert => "무속성 삽입",
        _ => "?"
    };
    public static string GetActionCode(Action a) => a switch
    {
        Action.ComboShuffle => "shuffle",
        Action.SlotCurse => "curse",
        Action.NullInsert => "null_insert",
        _ => "unknown"
    };
}

