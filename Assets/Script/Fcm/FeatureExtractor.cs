using System.Collections.Generic;
using UnityEngine;
using static SkillDataParser;

// FCM/트리용 특성 벡터 추출기 (5차원 + 오염도)
public static class FeatureExtractor
{
    // 입력 키('q'~'r') → 원소 인덱스 매핑
    private static readonly Dictionary<string, int> KeyToIndex = new Dictionary<string, int>
    {
        { "q", 0 }, { "w", 1 }, { "e", 2 }, { "r", 3 }
    };

    public const int ElementCount = 4;            // 원소 수
    public const float THREAT_THRESHOLD = 0.50f;  // 위협 판정 근접도 기준
    public const int TOTAL_CARDS = 12;            // 전체 카드 수

    // 위협 콤보 분석 결과
    public struct ThreatInfo
    {
        public int SkillIndex;        // 스킬 인덱스
        public int MinCards;          // 완성까지 최소 카드 수
        public float Proximity;       // 근접도(0~1)
        public int CycleLength;       // 콤보 사이클 길이
        public bool HasChain;         // 연쇄 여부
        public int ChainSkillIndex;   // 연쇄 스킬 인덱스
        public int ChainCards;        // 연쇄 추가 카드 수
        public int TotalCards;        // 총 카드 수
    }

    // 위협 분석 후 FCM 특성 5차원 추출
    public static float[] ExtractFCMFeatures()
    {
        ThreatInfo[] threats = AnalyzeThreats();
        return CalcFCMFeatures(threats);
    }

    // 위협 정보를 함께 반환하며 FCM 특성 추출
    public static float[] ExtractFCMFeatures(out ThreatInfo[] threats)
    {
        threats = AnalyzeThreats();
        return CalcFCMFeatures(threats);
    }

    // 위협 배열로부터 특성 5차원 계산
    static float[] CalcFCMFeatures(ThreatInfo[] threats)
    {
        return new float[]
        {
            CalcUrgency(threats),
            CalcRepeatRisk(threats),
            CalcReplenishProgress(),
            CalcThreatDensity(threats),
            CalcSlotBalance(),
        };
    }

    // f4 오염도: 위협 원소 슬롯의 무속성 카드 비율
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
            var slot = slotSys.GetSlot(e);
            int nullCount = slot.neutralDeckCount + slot.neutralGraveCount
                          + (slot.hasNeutralCard ? 1 : 0);
            totalNull += nullCount;
            totalCards += ElementSlotSystem.CARDS_PER_ELEMENT + nullCount;
        }
        return totalCards > 0 ? totalNull / totalCards : 0f;
    }

    // 학습된 스킬들의 위협도(근접/사이클/연쇄) 분석
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

    // f1 긴급도: 최대 근접도
    static float CalcUrgency(ThreatInfo[] threats)
    {
        float max = 0f;
        foreach (var t in threats)
            if (t.Proximity > max) max = t.Proximity;
        return max;
    }

    // f3 반복성: 위협 콤보 중 최소 사이클 기반
    static float CalcRepeatRisk(ThreatInfo[] threats)
    {
        int bestCycle = 4;
        foreach (var t in threats)
            if (t.Proximity >= THREAT_THRESHOLD && t.CycleLength > 0)
                bestCycle = Mathf.Min(bestCycle, t.CycleLength);
        return Mathf.Clamp01(1f - (bestCycle - 1f) / 3f);
    }

    // f5 보충 진행도: 사용한 카드 비율
    static float CalcReplenishProgress()
    {
        ElementSlotSystem slotSys = ElementSlotSystem.Instance;
        if (slotSys == null) return 0f;
        int rem = 0;
        for (int i = 0; i < 4; i++) rem += slotSys.GetSlot(i).RemainingCount;
        return Mathf.Clamp01((float)(TOTAL_CARDS - rem) / TOTAL_CARDS);
    }

    // f7 위협 밀도: 위협 콤보 / 전체 비율
    static float CalcThreatDensity(ThreatInfo[] threats)
    {
        if (threats.Length == 0) return 0f;
        int tc = 0;
        foreach (var t in threats)
            if (t.Proximity >= THREAT_THRESHOLD) tc++;
        return (float)tc / threats.Length;
    }

    // f8 슬롯 균형도: 4슬롯 잔여 균등도
    static float CalcSlotBalance()
    {
        ElementSlotSystem slotSys = ElementSlotSystem.Instance;
        if (slotSys == null) return 1f;

        int totalRem = 0;
        float[] rates = new float[4];
        for (int i = 0; i < 4; i++)
        {
            int r = slotSys.GetSlot(i).RemainingCount;
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

    // 현재 슬롯에서 콤보 완성까지 필요한 최소 카드 수
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

    // 소비 후 잔여 카드 기준 최소 카드 수(연쇄 판정용)
    static int CalcMinCardsWithRemaining(int[] slot, int[] target, int[] remaining)
    {
        if (slot[0] == target[0] && slot[1] == target[1] && slot[2] == target[2]) return 0;
        if (slot[1] == target[0] && slot[2] == target[1])
            if (HasEnoughFromRemaining(remaining, new int[] { target[2] })) return 1;
        if (slot[2] == target[0])
            if (HasEnoughFromRemaining(remaining, new int[] { target[1], target[2] })) return 2;
        return -1;
    }

    // 최소 카드 수에 따라 소비될 카드 배열 반환
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

    // 소비 카드 차감 후 슬롯별 잔여 수 계산
    static int[] CalcRemainingAfterConsume(ElementSlotSystem slotSys, int[] consumed)
    {
        int[] rem = new int[4];
        if (slotSys != null)
            for (int i = 0; i < 4; i++) rem[i] = slotSys.GetSlot(i).RemainingCount;
        else
            for (int i = 0; i < 4; i++) rem[i] = 99;
        foreach (int e in consumed)
            if (e >= 0 && e < 4) rem[e] = Mathf.Max(0, rem[e] - 1);
        return rem;
    }

    // 잔여 카드로 필요한 원소를 충족하는지 검사
    static bool HasEnoughFromRemaining(int[] remaining, int[] elements)
    {
        int[] n = new int[4];
        foreach (int e in elements) { if (e < 0 || e >= 4) return false; n[e]++; }
        for (int i = 0; i < 4; i++) if (n[i] > 0 && remaining[i] < n[i]) return false;
        return true;
    }

    // 슬롯 보유분으로 필요한 원소를 충족하는지 검사
    static bool HasEnoughCards(ElementSlotSystem slotSys, int[] elements)
    {
        if (slotSys == null) return true;
        int[] n = new int[4];
        foreach (int e in elements) { if (e < 0 || e >= 4) return false; n[e]++; }
        for (int i = 0; i < 4; i++) if (n[i] > 0 && slotSys.GetSlot(i).RemainingCount < n[i]) return false;
        return true;
    }

    // 현재 콤보 입력을 원소 인덱스 배열로 변환
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

    // 콤보 문자열을 원소 인덱스 배열로 변환
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

    // 콤보 시퀀스의 사이클 길이(1~3) 계산
    static int CalcCycleLength(int[] seq)
    {
        if (seq == null) return 4;
        if (seq[0] == seq[1] && seq[1] == seq[2]) return 1;
        if (seq[0] == seq[2]) return 2;
        return 3;
    }
}
