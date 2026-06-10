using System.Collections.Generic;
using UnityEngine;

// FCM 유형·특성 기반 몬스터 방해행동 결정 트리
public class MonsterDecisionTree
{
    // 방해행동 종류
    public enum Action { ComboShuffle = 0, SlotCurse = 1, NullInsert = 2 }

    // 결정 결과(행동·사유·대상 슬롯)
    public struct Decision
    {
        public Action ChosenAction;   // 선택된 행동
        public string Reason;         // 결정 사유
        public int TargetSlot;        // 대상 슬롯
    }

    // 결정 트리 임계값 모음
    [System.Serializable]
    public class Thresholds
    {
        [Header("셔플 판정")]
        public float urgencyForShuffle = 0.70f;       // 셔플 긴급도 기준
        public float repeatForFastCycle = 0.50f;      // 빠른 사이클 판정 기준

        [Header("저주 판정")]
        public float densityForRush = 0.50f;          // 위협 집중 판정 기준
        public float urgencyOverrideForShuffle = 0.75f; // 집중 시 셔플 전환 긴급도

        [Header("무속성 전환점")]
        public float pollutionThreshold = 0.40f;      // 오염도 한계

        [Header("탐색형 셔플")]
        public float explorerUrgency = 0.50f;         // 탐색형 셔플 긴급도

        [Header("버스트 판정")]
        public float burstReplenishThreshold = 0.50f; // 버스트 보충 기준
        public float burstBalanceThreshold = 0.40f;   // 버스트 불균형 기준

        [Header("공통 가드")]
        [Tooltip("이 값 미만이면 위협 없음 → 무속성")]
        public float noThreatThreshold = 0.25f;       // 위협 없음 기준
        [Tooltip("이 값 이상이면 콤보 접근 → 무속성 금지")]
        public float comboApproachThreshold = 0.50f;  // 콤보 접근 기준
        [Tooltip("이 값 이상이면 카드 소진 → 무속성 금지")]
        public float depletionThreshold = 0.92f;      // 카드 소진 기준
    }

    private Thresholds t;   // 사용 임계값

    // 생성자: 임계값 주입(없으면 기본값)
    public MonsterDecisionTree(Thresholds thresholds = null)
    {
        t = thresholds ?? new Thresholds();
    }

    // 특성·소속도·위협으로 방해행동 결정
    public Decision Decide(float[] membership, float[] fcm,
                           float f4, FeatureExtractor.ThreatInfo[] threats)
    {
        float f1 = fcm[0], f3 = fcm[1], f5 = fcm[2], f7 = fcm[3], f8 = fcm[4];
        int dominant = FCMAnalyzer.GetDominantType(membership);

        bool hasChain = false;
        foreach (var th in threats)
            if (th.HasChain && th.Proximity >= FeatureExtractor.THREAT_THRESHOLD)
            { hasChain = true; break; }

        bool isFast = (f3 >= t.repeatForFastCycle) || hasChain;

        if (f1 < t.noThreatThreshold)
        {
            if (f5 >= t.depletionThreshold)
                return MakeCurse(threats, "공통+위협없음+카드소진->저주(견제)");
            if (f4 >= t.pollutionThreshold)
                return MakeCurse(threats, "공통+위협없음+오염->저주(견제)");
            return MakeNullInsert(threats, "공통+위협없음->무속성(미래투자)");
        }

        if (hasChain && f1 >= t.comboApproachThreshold)
            return MakeShuffle(threats, "공통+체인+콤보접근->셔플");

        switch (dominant)
        {
            case 0:
                if (f1 >= t.urgencyForShuffle && isFast)
                    return MakeShuffle(threats, "러시+긴급+빠름->셔플");
                if (f1 >= t.urgencyForShuffle)
                    return MakeCurse(threats, "러시+긴급+느림->저주");
                if (f4 >= t.pollutionThreshold)
                    return MakeCurse(threats, "러시+멀고+오염->저주");
                if (f5 >= t.depletionThreshold)
                    return MakeCurse(threats, "러시+멀고+카드소진->저주");
                return MakeNullInsert(threats, "러시+멀고+깨끗->무속성");

            case 1:
                if (f7 < t.densityForRush)
                {
                    if (f1 >= t.urgencyOverrideForShuffle && isFast)
                        return MakeShuffle(threats, "의존+집중+긴급+빠름->셔플");
                    return MakeCurse(threats, "의존+위협집중->저주");
                }
                if (f1 >= t.comboApproachThreshold)
                {
                    if (isFast)
                        return MakeShuffle(threats, "의존+분산+콤보접근+빠름->셔플");
                    return MakeCurse(threats, "의존+분산+콤보접근->저주");
                }
                if (f4 >= t.pollutionThreshold)
                    return MakeCurse(threats, "의존+분산+오염->저주");
                if (f5 >= t.depletionThreshold)
                    return MakeCurse(threats, "의존+분산+카드소진->저주");
                return MakeNullInsert(threats, "의존+분산+깨끗+안전->무속성");

            case 3:
                if (f1 >= t.urgencyForShuffle && isFast)
                    return MakeShuffle(threats, "버스트+콤보긴급+빠름->셔플");
                if (f1 >= t.urgencyForShuffle)
                    return MakeCurse(threats, "버스트+콤보긴급->저주");
                if (f5 >= t.burstReplenishThreshold && f8 < t.burstBalanceThreshold)
                {
                    if (f4 >= t.pollutionThreshold)
                        return MakeCurse(threats, "버스트+보충+불균형+오염->저주");
                    if (f5 >= t.depletionThreshold)
                        return MakeCurse(threats, "버스트+보충+카드소진->저주");
                    return MakeNullInsert(threats, "버스트+보충+불균형->무속성(보충전삽입)");
                }
                return MakeCurse(threats, "버스트+대기->저주");

            default:
                if (f1 >= t.comboApproachThreshold)
                {
                    if (isFast)
                        return MakeShuffle(threats, "탐색+콤보접근+빠름->셔플");
                    return MakeCurse(threats, "탐색+콤보접근->저주");
                }
                if (f4 >= t.pollutionThreshold)
                    return MakeCurse(threats, "탐색+오염->저주");
                if (f5 >= t.depletionThreshold)
                    return MakeCurse(threats, "탐색+카드소진->저주");
                return MakeNullInsert(threats, "탐색+깨끗+안전->무속성");
        }
    }

    // 콤보 셔플 결정 생성
    Decision MakeShuffle(FeatureExtractor.ThreatInfo[] threats, string reason) => new Decision
    { ChosenAction = Action.ComboShuffle, Reason = reason, TargetSlot = -1 };
    // 슬롯 저주 결정 생성
    Decision MakeCurse(FeatureExtractor.ThreatInfo[] threats, string reason) => new Decision
    { ChosenAction = Action.SlotCurse, Reason = reason, TargetSlot = FindCurseTarget(threats) };
    // 무속성 삽입 결정 생성
    Decision MakeNullInsert(FeatureExtractor.ThreatInfo[] threats, string reason) => new Decision
    { ChosenAction = Action.NullInsert, Reason = reason, TargetSlot = FindNullInsertTarget(threats) };

    // 저주 대상 슬롯 선정(위협 원소 빈도 최대)
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
            if (slotSys.GetSlot(i).RemainingCount <= 0 || slotSys.GetSlot(i).IsCursed)
                elemCount[i] = -1;

        int target = -1;
        for (int i = 0; i < 4; i++)
            if (elemCount[i] > 0 && (target < 0 || elemCount[i] > elemCount[target]))
                target = i;

        if (target < 0)
        {
            int mx = 0;
            for (int i = 0; i < 4; i++)
            {
                int r = slotSys.GetSlot(i).RemainingCount;
                if (r > mx && !slotSys.GetSlot(i).IsCursed) { mx = r; target = i; }
            }
            if (target < 0) target = Random.Range(0, 4);
        }
        return target;
    }

    // 무속성 삽입 대상 슬롯 선정(무속성 최소 보유 슬롯)
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
            var slot = slotSys.GetSlot(e);
            if (slot.RemainingCount <= 0) continue;
            int nc = slot.neutralDeckCount + slot.neutralGraveCount + (slot.hasNeutralCard ? 1 : 0);
            if (nc < minN) { minN = nc; best = e; }
        }
        if (best < 0)
        {
            minN = int.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                var slot = slotSys.GetSlot(i);
                int nc = slot.neutralDeckCount + slot.neutralGraveCount + (slot.hasNeutralCard ? 1 : 0);
                if (nc < minN) { minN = nc; best = i; }
            }
        }
        return best;
    }

    // 행동 한글 이름 반환
    public static string GetActionName(Action a) => a switch
    {
        Action.ComboShuffle => "콤보 셔플",
        Action.SlotCurse => "슬롯 저주",
        Action.NullInsert => "무속성 삽입",
        _ => "?"
    };
    // 행동 코드 문자열 반환
    public static string GetActionCode(Action a) => a switch
    {
        Action.ComboShuffle => "shuffle",
        Action.SlotCurse => "curse",
        Action.NullInsert => "null_insert",
        _ => "unknown"
    };
}
