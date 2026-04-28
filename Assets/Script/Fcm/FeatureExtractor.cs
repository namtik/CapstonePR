using System.Collections.Generic;
using UnityEngine;
using static SkillDataParser;

/// <summary>
/// 특성 벡터 추출기 (v4 - 5차원, f2 제거)
/// 
/// FCM용 (5차원):
///   f1 긴급도      = 가장 가까운 콤보 근접도 (4-카드수)/4
///   f3 반복성      = 위협 콤보 중 최소 사이클
///   f5 보충 진행도  = 사용한 카드 / 12 (연속)
///   f7 위협 밀도    = 위협 콤보 수 / 전체 보유 수 (연속, f2 대체)
///   f8 슬롯 균형도  = 4슬롯 잔여 균등도 (연속)
/// 
/// 트리 전용:
///   f4 오염도      = 무속성 카드 비율
/// 
/// f2 제거 이유: 97%에서 0, 분리도 기여 0.1%. f7이 동일 역할을 연속값으로 수행.
/// </summary>
public static class FeatureExtractor
{
    private static readonly Dictionary<string, int> KeyToIndex = new Dictionary<string, int>
    {
        { "q", 0 }, { "w", 1 }, { "e", 2 }, { "r", 3 }
    };

    public const int ElementCount = 4;
    public const float THREAT_THRESHOLD = 0.50f;
    public const int TOTAL_CARDS = 12;

    public struct ThreatInfo
    {
        public int SkillIndex;
        public int MinCards;
        public float Proximity;
        public int CycleLength;
        public bool HasChain;
        public int ChainSkillIndex;
        public int ChainCards;
        public int TotalCards;
    }

    // ─── FCM용 특성 추출 (5차원) ───

    public static float[] ExtractFCMFeatures()
    {
        ThreatInfo[] threats = AnalyzeThreats();
        return CalcFCMFeatures(threats);
    }

    public static float[] ExtractFCMFeatures(out ThreatInfo[] threats)
    {
        threats = AnalyzeThreats();
        return CalcFCMFeatures(threats);
    }

    static float[] CalcFCMFeatures(ThreatInfo[] threats)
    {
        return new float[]
        {
            CalcUrgency(threats),          // f1
            CalcRepeatRisk(threats),       // f3
            CalcReplenishProgress(),       // f5
            CalcThreatDensity(threats),    // f7
            CalcSlotBalance(),             // f8
        };
    }

    // ─── f4 오염도 (트리 전용) ───

    public static float CalcPollution(ThreatInfo[] threats)
    {
        ElementSlotSystem slotSys = ElementSlotSystem.Instance;
        ComboSystem combo = ComboSystem.Instance;
        if (slotSys == null || combo == null) return 0f;

        HashSet<int> neededElements = new HashSet<int>();
        foreach (var t in threats)
        {
            if (t.Proximity >= THREAT_THRESHOLD && t.SkillIndex >= 0)
            {
                int[] seq = ComboStringToIndices(combo.learnedSkills[t.SkillIndex].combo);
                if (seq != null)
                    foreach (int e in seq) neededElements.Add(e);
            }
        }
        if (neededElements.Count == 0) return 0f;

        float totalNull = 0f, totalCards = 0f;
        foreach (int e in neededElements)
        {
            if (e < 0 || e >= 4) continue;
            var slot = slotSys.slots[e];
            int nullCount = slot.neutralDeckCount + slot.neutralGraveCount
                          + (slot.hasNeutralCard ? 1 : 0);
            totalNull += nullCount;
            totalCards += ElementSlotSystem.CARDS_PER_ELEMENT + nullCount;
        }
        return totalCards > 0 ? totalNull / totalCards : 0f;
    }

    // ─── 위협 분석 ───

    public static ThreatInfo[] AnalyzeThreats()
    {
        ComboSystem combo = ComboSystem.Instance;
        ElementSlotSystem slotSys = ElementSlotSystem.Instance;
        if (combo == null) return new ThreatInfo[0];

        int[] currentSlot = GetComboSlotAsIndices();
        List<SkillData> skills = combo.learnedSkills;
        List<ThreatInfo> threats = new List<ThreatInfo>();

        for (int si = 0; si < skills.Count; si++)
        {
            int[] target = ComboStringToIndices(skills[si].combo);
            if (target == null) continue;

            int minCards = CalcMinCards(currentSlot, target, slotSys);
            if (minCards < 0) continue;

            int cyc = CalcCycleLength(target);

            int[] consumed = GetConsumedCards(minCards, target);
            int[] remaining = CalcRemainingAfterConsume(slotSys, consumed);

            bool hasChain = false;
            int chainIdx = -1;
            int chainCards = 0;

            for (int ci = 0; ci < skills.Count; ci++)
            {
                if (ci == si) continue;
                int[] chainTarget = ComboStringToIndices(skills[ci].combo);
                if (chainTarget == null) continue;
                int cd = CalcMinCardsWithRemaining(target, chainTarget, remaining);
                if (cd >= 1 && cd <= 2)
                {
                    hasChain = true;
                    chainIdx = ci;
                    chainCards = cd;
                    break;
                }
            }

            float proximity = Mathf.Clamp01((4f - minCards) / 4f);

            threats.Add(new ThreatInfo
            {
                SkillIndex = si,
                MinCards = minCards,
                Proximity = proximity,
                CycleLength = cyc,
                HasChain = hasChain,
                ChainSkillIndex = chainIdx,
                ChainCards = chainCards,
                TotalCards = hasChain ? minCards + chainCards : minCards,
            });
        }

        threats.Sort((a, b) => b.Proximity.CompareTo(a.Proximity));
        return threats.ToArray();
    }

    // ─── f1: 긴급도 ───
    static float CalcUrgency(ThreatInfo[] threats)
    {
        float max = 0f;
        foreach (var t in threats)
            if (t.Proximity > max) max = t.Proximity;
        return max;
    }

    // ─── f3: 반복성 ───
    static float CalcRepeatRisk(ThreatInfo[] threats)
    {
        int bestCycle = 4;
        foreach (var t in threats)
            if (t.Proximity >= THREAT_THRESHOLD && t.CycleLength > 0)
                bestCycle = Mathf.Min(bestCycle, t.CycleLength);
        return Mathf.Clamp01(1f - (bestCycle - 1f) / 3f);
    }

    // ─── f5: 보충 진행도 ───
    static float CalcReplenishProgress()
    {
        ElementSlotSystem slotSys = ElementSlotSystem.Instance;
        if (slotSys == null) return 0f;
        int rem = 0;
        for (int i = 0; i < 4; i++) rem += slotSys.slots[i].RemainingCount;
        return Mathf.Clamp01((float)(TOTAL_CARDS - rem) / TOTAL_CARDS);
    }

    // ─── f7: 위협 밀도 (f2 대체) ───
    static float CalcThreatDensity(ThreatInfo[] threats)
    {
        if (threats.Length == 0) return 0f;
        int tc = 0;
        foreach (var t in threats)
            if (t.Proximity >= THREAT_THRESHOLD) tc++;
        return (float)tc / threats.Length;
    }

    // ─── f8: 슬롯 균형도 ───
    static float CalcSlotBalance()
    {
        ElementSlotSystem slotSys = ElementSlotSystem.Instance;
        if (slotSys == null) return 1f;

        int totalRem = 0;
        float[] rates = new float[4];
        for (int i = 0; i < 4; i++)
        {
            int r = slotSys.slots[i].RemainingCount;
            totalRem += r;
            rates[i] = (float)r / ElementSlotSystem.CARDS_PER_ELEMENT;
        }
        if (totalRem == 0) return 1f;

        float avg = 0f;
        for (int i = 0; i < 4; i++) avg += rates[i];
        avg /= 4f;

        float variance = 0f;
        for (int i = 0; i < 4; i++)
        {
            float diff = rates[i] - avg;
            variance += diff * diff;
        }
        return Mathf.Clamp01(1f - Mathf.Sqrt(variance / 4f) / 0.5f);
    }

    // ─── 최소 카드 수 ───

    static int CalcMinCards(int[] slot, int[] target, ElementSlotSystem slotSys)
    {
        if (slot[0] == target[0] && slot[1] == target[1] && slot[2] == target[2]) return 0;
        if (slot[1] == target[0] && slot[2] == target[1])
            if (HasEnoughCards(slotSys, new int[] { target[2] })) return 1;
        if (slot[2] == target[0])
            if (HasEnoughCards(slotSys, new int[] { target[1], target[2] })) return 2;
        if (HasEnoughCards(slotSys, new int[] { target[0], target[1], target[2] })) return 3;
        return -1;
    }

    static int CalcMinCardsWithRemaining(int[] slot, int[] target, int[] remaining)
    {
        if (slot[0] == target[0] && slot[1] == target[1] && slot[2] == target[2]) return 0;
        if (slot[1] == target[0] && slot[2] == target[1])
            if (HasEnoughFromRemaining(remaining, new int[] { target[2] })) return 1;
        if (slot[2] == target[0])
            if (HasEnoughFromRemaining(remaining, new int[] { target[1], target[2] })) return 2;
        return -1;
    }

    // ─── 카드 소비 ───

    static int[] GetConsumedCards(int minCards, int[] target)
    {
        switch (minCards)
        {
            case 0: return new int[0];
            case 1: return new int[] { target[2] };
            case 2: return new int[] { target[1], target[2] };
            case 3: return new int[] { target[0], target[1], target[2] };
            default: return new int[0];
        }
    }

    static int[] CalcRemainingAfterConsume(ElementSlotSystem slotSys, int[] consumed)
    {
        int[] rem = new int[4];
        if (slotSys != null)
            for (int i = 0; i < 4; i++) rem[i] = slotSys.slots[i].RemainingCount;
        else
            for (int i = 0; i < 4; i++) rem[i] = 99;
        foreach (int e in consumed)
            if (e >= 0 && e < 4) rem[e] = Mathf.Max(0, rem[e] - 1);
        return rem;
    }

    static bool HasEnoughFromRemaining(int[] remaining, int[] elements)
    {
        int[] n = new int[4];
        foreach (int e in elements) { if (e < 0 || e >= 4) return false; n[e]++; }
        for (int i = 0; i < 4; i++) if (n[i] > 0 && remaining[i] < n[i]) return false;
        return true;
    }

    static bool HasEnoughCards(ElementSlotSystem slotSys, int[] elements)
    {
        if (slotSys == null) return true;
        int[] n = new int[4];
        foreach (int e in elements) { if (e < 0 || e >= 4) return false; n[e]++; }
        for (int i = 0; i < 4; i++) if (n[i] > 0 && slotSys.slots[i].RemainingCount < n[i]) return false;
        return true;
    }

    // ─── 유틸리티 ───

    public static int[] GetComboSlotAsIndices()
    {
        ComboSystem combo = ComboSystem.Instance;
        if (combo == null) return new int[] { -1, -1, -1 };
        List<string> input = combo.GetComboInput();
        int[] result = new int[] { -1, -1, -1 };
        int offset = 3 - input.Count;
        for (int i = 0; i < input.Count; i++)
            if (KeyToIndex.TryGetValue(input[i], out int idx)) result[offset + i] = idx;
        return result;
    }

    public static int[] ComboStringToIndices(string combo)
    {
        if (string.IsNullOrEmpty(combo) || combo.Length < 3) return null;
        string lower = combo.Trim().ToLowerInvariant();
        int[] result = new int[3];
        for (int i = 0; i < 3; i++)
            if (!KeyToIndex.TryGetValue(lower[i].ToString(), out int idx)) return null;
            else result[i] = idx;
        return result;
    }

    static int CalcCycleLength(int[] seq)
    {
        if (seq == null) return 4;
        if (seq[0] == seq[1] && seq[1] == seq[2]) return 1;
        if (seq[0] == seq[2]) return 2;
        return 3;
    }
}
