using System.Collections.Generic;
using UnityEngine;
using static SkillDataParser;

/// <summary>
/// 특성 벡터 추출기
/// 
/// ComboSystem과 ElementSlotSystem의 현재 상태를 읽어
/// f1~f4 특성 벡터를 계산한다.
/// 자체 상태를 유지하지 않고, 호출 시점의 게임 상태에서 직접 계산.
/// </summary>
public static class FeatureExtractor
{
    // ─── 속성 매핑 ───
    // q=0(fire), w=1(water), e=2(wind), r=3(earth)
    private static readonly Dictionary<string, int> KeyToIndex = new Dictionary<string, int>
    {
        { "q", 0 }, { "w", 1 }, { "e", 2 }, { "r", 3 }
    };

    public const int ElementCount = 4;

    // ─── Top-4 경로 분석 결과 ───
    public struct RouteInfo
    {
        public int   Element;        // 이 경로의 속성 (0~3)
        public float BestProximity;  // 보유 콤보 중 최대 근접도
        public int   BestSkillIndex; // 해당 스킬의 learnedSkills 인덱스
        public int   CycleLength;    // 재발동 사이클 길이
    }

    // ═══════════════════════════════════════════
    // 특성 벡터 추출 (메인 진입점)
    // ═══════════════════════════════════════════

    /// <summary>
    /// 현재 게임 상태에서 4차원 특성 벡터를 추출한다.
    /// ComboSystem, ElementSlotSystem이 존재해야 한다.
    /// </summary>
    public static float[] ExtractFeatures()
    {
        RouteInfo[] routes = AnalyzeRoutes();

        float f1 = CalcUrgency(routes);
        float f2 = CalcConcentration(routes);
        float f3 = CalcRepeatRisk(routes);
        float f4 = CalcPollution(routes);

        return new float[] { f1, f2, f3, f4 };
    }

    /// <summary>
    /// Top-4 경로 분석도 함께 반환하는 버전 (타겟 슬롯 결정용)
    /// </summary>
    public static float[] ExtractFeatures(out RouteInfo[] routes)
    {
        routes = AnalyzeRoutes();

        float f1 = CalcUrgency(routes);
        float f2 = CalcConcentration(routes);
        float f3 = CalcRepeatRisk(routes);
        float f4 = CalcPollution(routes);

        return new float[] { f1, f2, f3, f4 };
    }

    // ═══════════════════════════════════════════
    // Top-4 경로 분석
    // ═══════════════════════════════════════════

    /// <summary>
    /// 4개 속성 각각에 대해 "다음에 이걸 쓰면 어떤 콤보에 가장 가까운지" 분석
    /// </summary>
    public static RouteInfo[] AnalyzeRoutes()
    {
        ComboSystem combo = ComboSystem.Instance;
        if (combo == null) return EmptyRoutes();

        int[] currentSlot = GetComboSlotAsIndices();
        List<SkillData> skills = combo.learnedSkills;

        RouteInfo[] routes = new RouteInfo[4];

        for (int elem = 0; elem < 4; elem++)
        {
            // 이 속성을 다음에 쓰면 콤보 슬롯이 이렇게 됨
            int[] nextCombo = SimulateNext(currentSlot, elem);

            float bestProx = 0f;
            int bestIdx = -1;

            for (int si = 0; si < skills.Count; si++)
            {
                int[] skillSeq = ComboStringToIndices(skills[si].combo);
                if (skillSeq == null) continue;

                float prox = CalcProximity(nextCombo, skillSeq);
                if (prox > bestProx)
                {
                    bestProx = prox;
                    bestIdx = si;
                }
            }

            int cycle = (bestIdx >= 0)
                ? CalcCycleLength(ComboStringToIndices(skills[bestIdx].combo))
                : 4;

            routes[elem] = new RouteInfo
            {
                Element = elem,
                BestProximity = bestProx,
                BestSkillIndex = bestIdx,
                CycleLength = cycle
            };
        }

        return routes;
    }

    // ═══════════════════════════════════════════
    // f1~f4 계산
    // ═══════════════════════════════════════════

    /// <summary>f1: 콤보 긴급도 = Top-4 중 최대 근접도</summary>
    static float CalcUrgency(RouteInfo[] routes)
    {
        float max = 0f;
        foreach (var r in routes)
            if (r.BestProximity > max) max = r.BestProximity;
        return max;
    }

    /// <summary>f2: 경로 집중도 = (1위 - 2위) / 1위</summary>
    static float CalcConcentration(RouteInfo[] routes)
    {
        float first = 0f, second = 0f;
        foreach (var r in routes)
        {
            if (r.BestProximity >= first)
            {
                second = first;
                first = r.BestProximity;
            }
            else if (r.BestProximity > second)
            {
                second = r.BestProximity;
            }
        }
        return first > 0.001f ? (first - second) / first : 0f;
    }

    /// <summary>f3: 반복 위험도 = 위협 콤보의 최소 사이클 기반</summary>
    static float CalcRepeatRisk(RouteInfo[] routes)
    {
        int bestCycle = 4;
        foreach (var r in routes)
        {
            if (r.BestProximity >= 0.33f && r.CycleLength > 0)
                bestCycle = Mathf.Min(bestCycle, r.CycleLength);
        }
        return Mathf.Clamp01(1f - (bestCycle - 1f) / 3f);
    }

    /// <summary>f4: 오염도 = 위협 콤보 관련 슬롯의 무속성 비율</summary>
    static float CalcPollution(RouteInfo[] routes)
    {
        ElementSlotSystem slotSys = ElementSlotSystem.Instance;
        ComboSystem combo = ComboSystem.Instance;
        if (slotSys == null || combo == null) return 0f;

        // 위협 경로의 콤보에 필요한 속성 수집
        HashSet<int> neededElements = new HashSet<int>();
        foreach (var r in routes)
        {
            if (r.BestProximity >= 0.33f && r.BestSkillIndex >= 0)
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

            // 해당 슬롯의 총 무속성 카드 수
            int nullCount = slot.neutralDeckCount + slot.neutralGraveCount
                          + (slot.hasNeutralCard ? 1 : 0);

            // 해당 슬롯의 총 카드 수 (속성카드 기본 3 + 무속성)
            int normalTotal = ElementSlotSystem.CARDS_PER_ELEMENT; // 기본 3장
            int total = normalTotal + nullCount;

            totalNull += nullCount;
            totalCards += total;
        }

        return totalCards > 0 ? totalNull / totalCards : 0f;
    }

    // ═══════════════════════════════════════════
    // 유틸리티
    // ═══════════════════════════════════════════

    /// <summary>ComboSystem의 comboInput을 int[3] 배열로 변환</summary>
    public static int[] GetComboSlotAsIndices()
    {
        ComboSystem combo = ComboSystem.Instance;
        if (combo == null) return new int[] { -1, -1, -1 };

        List<string> input = combo.GetComboInput();
        int[] result = new int[] { -1, -1, -1 };

        // comboInput은 최대 3개, 오래된 것이 앞(0)
        // 내부 표현: result[0]=가장 오래된, result[2]=가장 최근
        int offset = 3 - input.Count;
        for (int i = 0; i < input.Count; i++)
        {
            if (KeyToIndex.TryGetValue(input[i], out int idx))
                result[offset + i] = idx;
        }

        return result;
    }

    /// <summary>"qqw" 같은 콤보 문자열을 int[3]으로 변환</summary>
    public static int[] ComboStringToIndices(string combo)
    {
        if (string.IsNullOrEmpty(combo) || combo.Length < 3) return null;
        string lower = combo.Trim().ToLowerInvariant();

        int[] result = new int[3];
        for (int i = 0; i < 3; i++)
        {
            string key = lower[i].ToString();
            if (!KeyToIndex.TryGetValue(key, out int idx)) return null;
            result[i] = idx;
        }
        return result;
    }

    /// <summary>근접도 계산: 오른쪽부터 연속 일치 수 / 3</summary>
    static float CalcProximity(int[] combo, int[] skillSeq)
    {
        int match = 0;
        for (int i = 2; i >= 0; i--)
        {
            if (combo[i] < 0) break;
            if (combo[i] == skillSeq[i]) match++;
            else break;
        }
        return match / 3f;
    }

    /// <summary>다음 카드 사용 시뮬레이션: 왼쪽으로 밀고 오른쪽에 추가</summary>
    static int[] SimulateNext(int[] current, int nextElement)
    {
        return new int[] { current[1], current[2], nextElement };
    }

    /// <summary>
    /// 사이클 길이 계산: 발동 후 재발동까지 필요한 최소 입력 수
    /// 사이클 1: aaa (동일 속성 3개)
    /// 사이클 2: aba (첫째=셋째)
    /// 사이클 3: 나머지 전부
    /// </summary>
    static int CalcCycleLength(int[] seq)
    {
        if (seq == null) return 4;
        if (seq[0] == seq[1] && seq[1] == seq[2]) return 1;  // aaa
        if (seq[0] == seq[2]) return 2;                       // aba
        return 3;
    }

    static RouteInfo[] EmptyRoutes()
    {
        return new RouteInfo[4];
    }
}
