using System.Collections.Generic;
using UnityEngine;
using static SkillDataParser;

/// <summary>
/// 특성 벡터 추출기
/// 
/// FCM용 특성: float[3] = [f1 긴급도, f2 집중도, f3 반복성]
/// 오염도(f4): 별도 메서드로 제공 (의사결정 트리에서 직접 사용)
/// 
/// f4를 FCM에서 제외한 이유:
///   f4는 몬스터가 만든 게임 상태이지 플레이어 행동 패턴이 아님.
///   세 클러스터에서 f4 값이 거의 동일(0.10~0.15)해 분류에 기여하지 않음.
/// </summary>
public static class FeatureExtractor
{
    private static readonly Dictionary<string, int> KeyToIndex = new Dictionary<string, int>
    {
        { "q", 0 }, { "w", 1 }, { "e", 2 }, { "r", 3 }
    };

    public const int ElementCount = 4;

    // 근접도 0.34 이상 = 2칸 이상 일치(0.67+)만 위협으로 판정
    public const float THREAT_THRESHOLD = 0.34f;

    public struct RouteInfo
    {
        public int Element;
        public float BestProximity;
        public int BestSkillIndex;
        public int CycleLength;
    }

    // =======================================
    // FCM용 특성 추출 (3차원)
    // =======================================

    /// <summary>
    /// FCM 입력용 3차원 벡터 [f1, f2, f3]
    /// </summary>
    public static float[] ExtractFCMFeatures()
    {
        RouteInfo[] routes = AnalyzeRoutes();
        return new float[]
        {
            CalcUrgency(routes),
            CalcConcentration(routes),
            CalcRepeatRisk(routes)
        };
    }

    /// <summary>
    /// FCM 입력용 3차원 벡터 + Top-4 경로 (타겟 결정용)
    /// </summary>
    public static float[] ExtractFCMFeatures(out RouteInfo[] routes)
    {
        routes = AnalyzeRoutes();
        return new float[]
        {
            CalcUrgency(routes),
            CalcConcentration(routes),
            CalcRepeatRisk(routes)
        };
    }

    // =======================================
    // f4 오염도 (FCM 외부, 트리에서 직접 사용)
    // =======================================

    /// <summary>
    /// f4: 오염도. FCM에는 들어가지 않고 의사결정 트리에서 직접 참조.
    /// </summary>
    public static float CalcPollution(RouteInfo[] routes)
    {
        ElementSlotSystem slotSys = ElementSlotSystem.Instance;
        ComboSystem combo = ComboSystem.Instance;
        if (slotSys == null || combo == null) return 0f;

        HashSet<int> neededElements = new HashSet<int>();
        foreach (var r in routes)
        {
            if (r.BestProximity >= THREAT_THRESHOLD && r.BestSkillIndex >= 0)
            {
                int[] seq = ComboStringToIndices(combo.learnedSkills[r.BestSkillIndex].combo);
                if (seq != null)
                    foreach (int e in seq) neededElements.Add(e);
            }
        }

        if (neededElements.Count == 0) return 0f;

        float totalNull = 0f;
        float totalCards = 0f;

        foreach (int e in neededElements)
        {
            if (e < 0 || e >= 4) continue;
            var slot = slotSys.slots[e];
            int nullCount = slot.neutralDeckCount + slot.neutralGraveCount
                          + (slot.hasNeutralCard ? 1 : 0);
            int total = ElementSlotSystem.CARDS_PER_ELEMENT + nullCount;
            totalNull += nullCount;
            totalCards += total;
        }

        return totalCards > 0 ? totalNull / totalCards : 0f;
    }

    // =======================================
    // Top-4 경로 분석
    // =======================================

    public static RouteInfo[] AnalyzeRoutes()
    {
        ComboSystem combo = ComboSystem.Instance;
        if (combo == null) return new RouteInfo[4];

        int[] currentSlot = GetComboSlotAsIndices();
        List<SkillData> skills = combo.learnedSkills;
        RouteInfo[] routes = new RouteInfo[4];

        for (int elem = 0; elem < 4; elem++)
        {
            int[] nextCombo = SimulateNext(currentSlot, elem);
            float bestProx = 0f;
            int bestIdx = -1;

            for (int si = 0; si < skills.Count; si++)
            {
                int[] skillSeq = ComboStringToIndices(skills[si].combo);
                if (skillSeq == null) continue;
                float prox = CalcProximity(nextCombo, skillSeq);
                if (prox > bestProx) { bestProx = prox; bestIdx = si; }
            }

            routes[elem] = new RouteInfo
            {
                Element = elem,
                BestProximity = bestProx,
                BestSkillIndex = bestIdx,
                CycleLength = bestIdx >= 0
                    ? CalcCycleLength(ComboStringToIndices(skills[bestIdx].combo))
                    : 4
            };
        }

        return routes;
    }

    // =======================================
    // f1~f3 계산
    // =======================================

    static float CalcUrgency(RouteInfo[] routes)
    {
        float max = 0f;
        foreach (var r in routes)
            if (r.BestProximity > max) max = r.BestProximity;
        return max;
    }

    static float CalcConcentration(RouteInfo[] routes)
    {
        float first = 0f, second = 0f;
        foreach (var r in routes)
        {
            if (r.BestProximity >= first) { second = first; first = r.BestProximity; }
            else if (r.BestProximity > second) second = r.BestProximity;
        }
        return first > 0.001f ? (first - second) / first : 0f;
    }

    static float CalcRepeatRisk(RouteInfo[] routes)
    {
        int bestCycle = 4;
        foreach (var r in routes)
        {
            if (r.BestProximity >= THREAT_THRESHOLD && r.CycleLength > 0)
                bestCycle = Mathf.Min(bestCycle, r.CycleLength);
        }
        return Mathf.Clamp01(1f - (bestCycle - 1f) / 3f);
    }

    // =======================================
    // 유틸리티
    // =======================================

    public static int[] GetComboSlotAsIndices()
    {
        ComboSystem combo = ComboSystem.Instance;
        if (combo == null) return new int[] { -1, -1, -1 };

        List<string> input = combo.GetComboInput();
        int[] result = new int[] { -1, -1, -1 };
        int offset = 3 - input.Count;
        for (int i = 0; i < input.Count; i++)
        {
            if (KeyToIndex.TryGetValue(input[i], out int idx))
                result[offset + i] = idx;
        }
        return result;
    }

    public static int[] ComboStringToIndices(string combo)
    {
        if (string.IsNullOrEmpty(combo) || combo.Length < 3) return null;
        string lower = combo.Trim().ToLowerInvariant();
        int[] result = new int[3];
        for (int i = 0; i < 3; i++)
        {
            if (!KeyToIndex.TryGetValue(lower[i].ToString(), out int idx)) return null;
            result[i] = idx;
        }
        return result;
    }

    static float CalcProximity(int[] combo, int[] skillSeq)
    {
        int match = 0;
        for (int i = 2; i >= 0; i--)
        {
            if (combo[i] < 0) break;
            if (combo[i] == skillSeq[i]) match++; else break;
        }
        return match / 3f;
    }

    static int[] SimulateNext(int[] current, int nextElement)
        => new int[] { current[1], current[2], nextElement };

    static int CalcCycleLength(int[] seq)
    {
        if (seq == null) return 4;
        if (seq[0] == seq[1] && seq[1] == seq[2]) return 1;
        if (seq[0] == seq[2]) return 2;
        return 3;
    }
}