using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 
/// 수정 사항 (17280개 조합 검증):
///   P1: f1 < 0.25 → 무속성 공통 가드 (위협 없으면 저주/셔플 의미 없음)
///   P2: 모든 유형에서 f1 >= 0.50이면 무속성 전에 저주/셔플 우선
///   P3: 체인 + f1 >= 0.50이면 셔플 우선
///   P4: 이미 오염 + 무속성 방지 (f4 체크 강화)
///   P5: f5 >= 0.92(카드 소진)이면 무속성 대신 저주
///   P6: urgencyOverrideForShuffle 0.90 → 0.75 (빠른 사이클 저주 방지)
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
        public float urgencyOverrideForShuffle = 0.75f;

        [Header("무속성 전환점")]
        public float pollutionThreshold = 0.40f;

        [Header("탐색형 셔플")]
        public float explorerUrgency = 0.50f;

        [Header("버스트 판정")]
        public float burstReplenishThreshold = 0.50f;
        public float burstBalanceThreshold = 0.40f;

        [Header("공통 가드")]
        [Tooltip("이 값 미만이면 위협 없음 → 무속성")]
        public float noThreatThreshold = 0.25f;      // P1
        [Tooltip("이 값 이상이면 콤보 접근 → 무속성 금지")]
        public float comboApproachThreshold = 0.50f;  // P2
        [Tooltip("이 값 이상이면 카드 소진 → 무속성 금지")]
        public float depletionThreshold = 0.92f;      // P5
    }

    private Thresholds t;

    public MonsterDecisionTree(Thresholds thresholds = null)
    {
        t = thresholds ?? new Thresholds();
    }

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

        // ══════════════════════════════════
        // 공통 가드 (유형 분기 전에 처리)
        // ══════════════════════════════════

        // P1: 위협 없으면 저주/셔플 의미 없음 → 무속성
        if (f1 < t.noThreatThreshold)
        {
            // P5: 카드 소진이면 무속성도 의미 없음 → 저주로 대체
            if (f5 >= t.depletionThreshold)
                return MakeCurse(threats, "공통+위협없음+카드소진->저주(견제)");
            // P4: 이미 오염이면 무속성 추가 대신 저주
            if (f4 >= t.pollutionThreshold)
                return MakeCurse(threats, "공통+위협없음+오염->저주(견제)");
            return MakeNullInsert(threats, "공통+위협없음->무속성(미래투자)");
        }

        // P3: 체인 + 콤보 접근이면 유형 무관하게 셔플 우선
        if (hasChain && f1 >= t.comboApproachThreshold)
            return MakeShuffle(threats, "공통+체인+콤보접근->셔플");

        // ══════════════════════════════════
        // 유형별 분기
        // ══════════════════════════════════

        switch (dominant)
        {
            case 0: // ── 콤보 러시형 ──
                if (f1 >= t.urgencyForShuffle && isFast)
                    return MakeShuffle(threats, "러시+긴급+빠름->셔플");
                if (f1 >= t.urgencyForShuffle)
                    return MakeCurse(threats, "러시+긴급+느림->저주");
                if (f4 >= t.pollutionThreshold)
                    return MakeCurse(threats, "러시+멀고+오염->저주");
                // P5: 카드 소진 체크
                if (f5 >= t.depletionThreshold)
                    return MakeCurse(threats, "러시+멀고+카드소진->저주");
                return MakeNullInsert(threats, "러시+멀고+깨끗->무속성");

            case 1: // ── 경로 의존형 ──
                if (f7 < t.densityForRush) // 위협 집중
                {
                    if (f1 >= t.urgencyOverrideForShuffle && isFast) // P6: 0.75로 완화
                        return MakeShuffle(threats, "의존+집중+긴급+빠름->셔플");
                    return MakeCurse(threats, "의존+위협집중->저주");
                }
                // 위협 분산 (f7 >= 0.50)
                // P2: 콤보 접근이면 무속성 금지
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

            case 3: // ── 버스트형 ──
                // P2: 콤보 접근이면 보충보다 즉각 대응 우선
                if (f1 >= t.urgencyForShuffle && isFast)
                    return MakeShuffle(threats, "버스트+콤보긴급+빠름->셔플");
                if (f1 >= t.urgencyForShuffle)
                    return MakeCurse(threats, "버스트+콤보긴급->저주");
                // 버스트 본래 로직
                if (f5 >= t.burstReplenishThreshold && f8 < t.burstBalanceThreshold)
                {
                    // P4: 이미 오염이 심하면 저주로 대체
                    if (f4 >= t.pollutionThreshold)
                        return MakeCurse(threats, "버스트+보충+불균형+오염->저주");
                    // P5: 카드 소진이면 무속성 불가
                    if (f5 >= t.depletionThreshold)
                        return MakeCurse(threats, "버스트+보충+카드소진->저주");
                    return MakeNullInsert(threats, "버스트+보충+불균형->무속성(보충전삽입)");
                }
                return MakeCurse(threats, "버스트+대기->저주");

            default: // ── 탐색/분산형 ──
                // P2: 콤보 접근이면 무속성 금지
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

    // ─── 행동 생성 ───

    Decision MakeShuffle(FeatureExtractor.ThreatInfo[] threats, string reason) => new Decision
    { ChosenAction = Action.ComboShuffle, Reason = reason, TargetSlot = -1 };
    Decision MakeCurse(FeatureExtractor.ThreatInfo[] threats, string reason) => new Decision
    { ChosenAction = Action.SlotCurse, Reason = reason, TargetSlot = FindCurseTarget(threats) };
    Decision MakeNullInsert(FeatureExtractor.ThreatInfo[] threats, string reason) => new Decision
    { ChosenAction = Action.NullInsert, Reason = reason, TargetSlot = FindNullInsertTarget(threats) };

    // ─── 타겟 슬롯 ───

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
            if (slotSys.slots[i].RemainingCount <= 0 || slotSys.slots[i].IsCursed)
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