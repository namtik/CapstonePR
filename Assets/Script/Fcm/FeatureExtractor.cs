using System.Collections.Generic;
using UnityEngine;
using static SkillDataParser;

/// <summary>
/// 특성 벡터 추출기
/// 
/// FCM용: float[3] = [f1 긴급도, f2 집중도, f3 반복성]
/// 트리 전용:
///   f4 - 오염도 (무속성 카드 비율)
///   f5 - 콤보 체인 위험도 (다른 스킬 간 연쇄 발동 가능성)
///
/// 콤보 체인 예시:
///   QWE 발동 -> 슬롯 [Q,W,E] -> R 입력 -> [W,E,R] = WER 발동!
///   1장으로 2개 스킬 연속 발동. f3(같은 스킬 반복)와 별도의 위협.
/// </summary>
public static class FeatureExtractor
{
    private static readonly Dictionary<string, int> KeyToIndex = new Dictionary<string, int>
    {
        { "q", 0 }, { "w", 1 }, { "e", 2 }, { "r", 3 }
    };

    public const int ElementCount = 4;
    public const float THREAT_THRESHOLD = 0.34f;

    public struct RouteInfo
    {
        public int Element;
        public float BestProximity;
        public int BestSkillIndex;
        public int CycleLength;

        //// 체인: 이 경로에서 콤보 발동 후 1장으로 다른 콤보가 터지는가
        //public bool HasChain;
        //public int ChainSkillIndex;   // 체인 스킬의 learnedSkills 인덱스
        //public int ChainElement;      // 체인을 발동시키는 속성 (0~3)
    }

    // FCM용 특성 추출
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

    // f4 오염도
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


    //public static float CalcChainPotential(RouteInfo[] routes)
    //{
    //    int threatCount = 0;
    //    int chainCount = 0;

    //    foreach (var r in routes)
    //    {
    //        if (r.BestProximity >= THREAT_THRESHOLD)
    //        {
    //            threatCount++;
    //            if (r.HasChain) chainCount++;
    //        }
    //    }

    //    return threatCount > 0 ? (float)chainCount / threatCount : 0f;
    //}


  
    // Top-4 경로 분석
    public static RouteInfo[] AnalyzeRoutes()
    {
        ComboSystem combo = ComboSystem.Instance;
        ElementSlotSystem slotSys = ElementSlotSystem.Instance;
        if (combo == null) return new RouteInfo[4];

        int[] currentSlot = GetComboSlotAsIndices();
        List<SkillData> skills = combo.learnedSkills;
        RouteInfo[] routes = new RouteInfo[4];

        for (int elem = 0; elem < 4; elem++)
        {
            // 해당 슬롯에 카드가 없으면 이 경로는 선택 불가
            bool slotEmpty = (slotSys != null && slotSys.slots[elem].RemainingCount <= 0);

            if (slotEmpty)
            {
                routes[elem] = new RouteInfo
                {
                    Element = elem,
                    BestProximity = 0f,
                    BestSkillIndex = -1,
                    CycleLength = 4,
                    //HasChain = false,
                    //ChainSkillIndex = -1,
                    //ChainElement = -1,
                };
                continue;
            }

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

            int cycle = bestIdx >= 0
                ? CalcCycleLength(ComboStringToIndices(skills[bestIdx].combo))
                : 4;

    
            //bool hasChain = false;
            //int chainSkillIdx = -1;
            //int chainElem = -1;

            //if (bestProx >= 0.999f && bestIdx >= 0)
            //{
            //    int[] firedSeq = ComboStringToIndices(skills[bestIdx].combo);

            //    if (firedSeq != null)
            //    {
            //        for (int ne = 0; ne < 4 && !hasChain; ne++)
            //        {
            //            bool chainSlotEmpty = (slotSys != null && slotSys.slots[ne].RemainingCount <= 0);
            //            if (chainSlotEmpty) continue;
            //            int[] afterChain = SimulateNext(firedSeq, ne);

            //            for (int si = 0; si < skills.Count; si++)
            //            {
            //                if (si == bestIdx) continue; 

            //                int[] chainSeq = ComboStringToIndices(skills[si].combo);
            //                if (chainSeq == null) continue;

            //                if (CalcProximity(afterChain, chainSeq) >= 0.999f)
            //                {
            //                    hasChain = true;
            //                    chainSkillIdx = si;
            //                    chainElem = ne;
            //                    break;
            //                }
            //            }
            //        }
            //    }
            //}

            routes[elem] = new RouteInfo
            {
                Element = elem,
                BestProximity = bestProx,
                BestSkillIndex = bestIdx,
                CycleLength = cycle,
                //HasChain = hasChain,
                //ChainSkillIndex = chainSkillIdx,
                //ChainElement = chainElem,
            };
        }

        return routes;
    }

    // f1~f3 계산
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
            float prox = r.BestProximity >= THREAT_THRESHOLD ? r.BestProximity : 0f;

            if (prox >= first) { second = first; first = prox; }
            else if (prox > second) second = prox;
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