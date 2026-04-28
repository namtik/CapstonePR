using System.IO;
using UnityEngine;

/// <summary>
/// 몬스터 중간 패턴 컨트롤러 (v5 - RBFN 통합)
/// 
/// 두 가지 모드:
///   useRBFN = false: 기존 FCM + 의사결정 트리 (규칙 기반)
///   useRBFN = true:  FCM + RBFN (학습 기반, 확률적 행동)
/// 
/// RBFN 모드에서는 FCM 소속도 대신 RBF 뉴런 활성도를 사용하고,
/// 의사결정 트리 대신 가중치 행렬로 행동 확률을 직접 계산한다.
/// </summary>
public class MonsterMidPattern : MonoBehaviour
{
    [Header("행동 결정 모드")]
    [SerializeField] private bool useRBFN = false;
    [Tooltip("RBFN에서 확률적 선택 사용 (false면 최대 확률 선택)")]
    [SerializeField] private bool stochastic = true;
    [Tooltip("지배적 소속도가 이 값 이상이면 확정 (확률적 선택 안 함)")]
    [SerializeField] private float deterministicThreshold = 0.80f;

    [Header("RBFN 파라미터")]
    [SerializeField] private RBFNetwork rbfn = new RBFNetwork();

    [Header("의사결정 트리 임계값 (useRBFN=false일 때)")]
    [SerializeField] private MonsterDecisionTree.Thresholds thresholds;

    [Header("프로필")]
    [SerializeField] private float profileBeta = 0.6f;
    [SerializeField] private float initialProfileWeight = 0.7f;
    [SerializeField] private float weightShiftPerTrigger = 0.15f;

    [Header("디버그")]
    [SerializeField] private FCMDebugOverlay debugOverlay;

    [Header("데이터 수집")]
    [SerializeField] private bool collectData = false;
    [SerializeField] private string logFolderName = "fcm_logs";
    [SerializeField] private string playerId = "";

    private MonsterDecisionTree decisionTree;
    private int triggerCount = 0;
    private float[] stageProfile = null;
    private string logFilePath;
    private bool headerWritten = false;

    void Awake() { decisionTree = new MonsterDecisionTree(thresholds); }

    public void InitBattle()
    {
        triggerCount = 0;
        if (collectData) InitLogFile();
    }

    public string Execute()
    {
        triggerCount++;

        // 1. 5차원 특성 추출
        float[] fcmFeatures = FeatureExtractor.ExtractFCMFeatures(
            out FeatureExtractor.ThreatInfo[] threats);

        // 2. f4 오염도
        float f4 = FeatureExtractor.CalcPollution(threats);

        // 3. 프로필 블렌딩
        float[] blended = BlendWithProfile(fcmFeatures);

        // 4. FCM 소속도 (디버그/로그용, RBFN 모드에서도 계산)
        float[] membership = FCMAnalyzer.CalcMembership(blended);
        int dominant = FCMAnalyzer.GetDominantType(membership);

        // 5. 행동 결정
        MonsterDecisionTree.Decision decision;
        float[] actionProbs = null;

        if (useRBFN)
        {
            decision = DecideWithRBFN(blended, f4, threats, out actionProbs);
        }
        else
        {
            decision = decisionTree.Decide(membership, blended, f4, threats);
        }

        // 6. 행동 실행
        string result = ExecuteAction(decision);

        // 디버그
        LogDecision(fcmFeatures, blended, f4, membership, dominant, decision, threats, actionProbs);

        if (debugOverlay == null)
            debugOverlay = FindFirstObjectByType<FCMDebugOverlay>();
        debugOverlay?.ShowAnalysis(blended, f4, membership, dominant, decision, threats, actionProbs);

        if (collectData)
            WriteDataRow(fcmFeatures, blended, f4, membership, dominant, decision, threats, actionProbs);

        return result;
    }

    // ─── RBFN 행동 결정 ───

    MonsterDecisionTree.Decision DecideWithRBFN(float[] features, float f4,
        FeatureExtractor.ThreatInfo[] threats, out float[] probs)
    {
        // RBFN 순전파
        probs = rbfn.Forward(features);

        // 지배적 확률이 높으면 확정, 아니면 확률적
        float maxProb = Mathf.Max(probs[0], Mathf.Max(probs[1], probs[2]));
        bool useDeterministic = !stochastic || (maxProb >= deterministicThreshold);

        int actionIdx = rbfn.SelectAction(probs, useDeterministic);

        // 타겟 슬롯 결정 (의사결정 트리의 로직 재사용)
        var action = (MonsterDecisionTree.Action)actionIdx;
        int targetSlot = -1;

        if (action == MonsterDecisionTree.Action.SlotCurse ||
            action == MonsterDecisionTree.Action.NullInsert)
        {
            // 타겟 결정은 트리의 로직을 그대로 사용
            var tempDecision = decisionTree.Decide(
                FCMAnalyzer.CalcMembership(features), features, f4, threats);

            // RBFN이 고른 행동이 저주/무속성이면 트리의 타겟을 사용
            if (tempDecision.ChosenAction == action)
                targetSlot = tempDecision.TargetSlot;
            else
            {
                // 행동이 다르면 범용 타겟 계산
                targetSlot = FindBestTarget(action, threats);
            }
        }

        string reason = $"RBFN: 셔플{probs[0]:P0} 저주{probs[1]:P0} 무속성{probs[2]:P0}" +
                         (useDeterministic ? " (확정)" : " (확률)");

        return new MonsterDecisionTree.Decision
        {
            ChosenAction = action,
            Reason = reason,
            TargetSlot = targetSlot,
        };
    }

    int FindBestTarget(MonsterDecisionTree.Action action, FeatureExtractor.ThreatInfo[] threats)
    {
        ComboSystem combo = ComboSystem.Instance;
        ElementSlotSystem slotSys = ElementSlotSystem.Instance;
        if (combo == null || slotSys == null) return Random.Range(0, 4);

        int[] score = new int[4];
        foreach (var th in threats)
        {
            if (th.Proximity >= FeatureExtractor.THREAT_THRESHOLD && th.SkillIndex >= 0)
            {
                int[] seq = FeatureExtractor.ComboStringToIndices(
                    combo.learnedSkills[th.SkillIndex].combo);
                if (seq != null) foreach (int e in seq) score[e]++;
            }
        }

        for (int i = 0; i < 4; i++)
            if (slotSys.slots[i].RemainingCount <= 0 || slotSys.slots[i].IsCursed)
                score[i] = -1;

        int best = -1;
        if (action == MonsterDecisionTree.Action.SlotCurse)
        {
            // 가장 많이 등장하는 속성
            for (int i = 0; i < 4; i++)
                if (score[i] > 0 && (best < 0 || score[i] > score[best])) best = i;
        }
        else
        {
            // 가장 깨끗한 슬롯
            int minNull = int.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                if (score[i] < 0) continue;
                var slot = slotSys.slots[i];
                int nc = slot.neutralDeckCount + slot.neutralGraveCount + (slot.hasNeutralCard ? 1 : 0);
                if (nc < minNull) { minNull = nc; best = i; }
            }
        }

        return best >= 0 ? best : Random.Range(0, 4);
    }

    public void OnBattleEnd()
    {
        float[] fcm = FeatureExtractor.ExtractFCMFeatures();
        UpdateProfile(fcm);
    }

    // ─── 행동 실행 ───

    string ExecuteAction(MonsterDecisionTree.Decision dec)
    {
        switch (dec.ChosenAction)
        {
            case MonsterDecisionTree.Action.ComboShuffle:
                ComboSystem.Instance?.ShuffleComboInput();
                return "패턴 발동: 콤보 슬롯 셔플!";
            case MonsterDecisionTree.Action.SlotCurse:
                if (ElementSlotSystem.Instance != null && dec.TargetSlot >= 0)
                {
                    ElementSlotSystem.Instance.ApplyCurse(dec.TargetSlot);
                    return $"패턴 발동: {ElementSlotSystem.SLOT_KEYS[dec.TargetSlot]} 슬롯 저주";
                }
                return "패턴 발동: 저주";
            case MonsterDecisionTree.Action.NullInsert:
                if (ElementSlotSystem.Instance != null && dec.TargetSlot >= 0)
                {
                    ElementSlotSystem.Instance.InsertNullCard(dec.TargetSlot);
                    return $"패턴 발동: {ElementSlotSystem.SLOT_KEYS[dec.TargetSlot]}에 무속성 추가";
                }
                return "패턴 발동: 무속성 추가";
            default: return "패턴 발동";
        }
    }

    // ─── 프로필 ───

    void UpdateProfile(float[] cur)
    {
        if (stageProfile == null) { stageProfile = (float[])cur.Clone(); return; }
        for (int i = 0; i < FCMAnalyzer.FeatureDim; i++)
            stageProfile[i] = profileBeta * stageProfile[i] + (1f - profileBeta) * cur[i];
    }

    float[] BlendWithProfile(float[] cur)
    {
        if (stageProfile == null) return cur;
        float cw = Mathf.Min(0.9f, (1f - initialProfileWeight) + triggerCount * weightShiftPerTrigger);
        float pw = 1f - cw;
        float[] b = new float[FCMAnalyzer.FeatureDim];
        for (int i = 0; i < FCMAnalyzer.FeatureDim; i++)
            b[i] = pw * stageProfile[i] + cw * cur[i];
        return b;
    }

    // ─── 데이터 수집 ───

    void InitLogFile()
    {
        string dir = Path.Combine(Application.persistentDataPath, logFolderName);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        string pid = string.IsNullOrEmpty(playerId) ? "p" + Random.Range(1000, 9999) : playerId;
        logFilePath = Path.Combine(dir,
            $"fcm_{pid}_{System.DateTime.Now:yyyyMMdd_HHmmss}_{Random.Range(1000, 9999)}.csv");
        headerWritten = false;
    }

    void WriteDataRow(float[] raw, float[] bl, float f4, float[] mem, int dom,
                      MonsterDecisionTree.Decision dec, FeatureExtractor.ThreatInfo[] threats,
                      float[] actionProbs)
    {
        if (string.IsNullOrEmpty(logFilePath)) return;
        try
        {
            if (!headerWritten)
            {
                File.AppendAllText(logFilePath,
                    "player,trigger,f1,f3,f5,f7,f8,f4," +
                    "f1b,f3b,f5b,f7b,f8b," +
                    "mem_rush,mem_path,mem_explorer,mem_burst,dominant," +
                    "mode,action,target," +
                    "prob_shuffle,prob_curse,prob_null," +
                    "top_threat,top_cards,top_chain\n");
                headerWritten = true;
            }
            string pid = string.IsNullOrEmpty(playerId) ? "unknown" : playerId;
            string act = MonsterDecisionTree.GetActionCode(dec.ChosenAction);
            string mode = useRBFN ? "rbfn" : "tree";

            string ps = "0", pc = "0", pn = "0";
            if (actionProbs != null)
            {
                ps = $"{actionProbs[0]:F3}";
                pc = $"{actionProbs[1]:F3}";
                pn = $"{actionProbs[2]:F3}";
            }

            string tt = "-", tc = "0", tch = "0";
            ComboSystem combo = ComboSystem.Instance;
            if (threats.Length > 0 && combo != null)
            {
                var t0 = threats[0];
                tt = t0.SkillIndex >= 0 ? combo.learnedSkills[t0.SkillIndex].combo : "-";
                tc = t0.MinCards.ToString();
                tch = t0.HasChain ? "1" : "0";
            }

            File.AppendAllText(logFilePath,
                $"{pid},{triggerCount}," +
                $"{raw[0]:F3},{raw[1]:F3},{raw[2]:F3},{raw[3]:F3},{raw[4]:F3},{f4:F3}," +
                $"{bl[0]:F3},{bl[1]:F3},{bl[2]:F3},{bl[3]:F3},{bl[4]:F3}," +
                $"{mem[0]:F3},{mem[1]:F3},{mem[2]:F3},{mem[3]:F3},{dom}," +
                $"{mode},{act},{dec.TargetSlot}," +
                $"{ps},{pc},{pn}," +
                $"{tt},{tc},{tch}\n");
        }
        catch (System.Exception e) { Debug.LogWarning($"[MidPattern] 저장 실패: {e.Message}"); }
    }

    // ─── 디버그 ───

    void LogDecision(float[] raw, float[] bl, float f4, float[] mem, int dom,
                     MonsterDecisionTree.Decision dec, FeatureExtractor.ThreatInfo[] threats,
                     float[] actionProbs)
    {
        string ts = "";
        ComboSystem combo = ComboSystem.Instance;
        int show = Mathf.Min(threats.Length, 3);
        for (int i = 0; i < show; i++)
        {
            var t = threats[i];
            string n = (combo != null && t.SkillIndex >= 0) ? combo.learnedSkills[t.SkillIndex].name : "?";
            string ch = t.HasChain && combo != null && t.ChainSkillIndex >= 0
                ? $" -> {combo.learnedSkills[t.ChainSkillIndex].name}" : "";
            ts += $"\n    [{t.MinCards}장] {n} ({t.Proximity:F2}){ch}";
        }

        string probStr = actionProbs != null
            ? $"\n  RBFN: 셔플={actionProbs[0]:P0} 저주={actionProbs[1]:P0} 무속성={actionProbs[2]:P0}"
            : "";

        Debug.Log($"[MidPattern] -- #{triggerCount} [{(useRBFN ? "RBFN" : "Tree")}] --\n" +
                  $"  f1={raw[0]:F2} f3={raw[1]:F2} f5={raw[2]:F2} f7={raw[3]:F2} f8={raw[4]:F2} | f4={f4:F2}\n" +
                  $"  위협:{threats.Length}개{ts}\n" +
                  $"  FCM: 러시={mem[0]:P0} 의존={mem[1]:P0} 탐색={mem[2]:P0} 버스트={mem[3]:P0} -> {FCMAnalyzer.GetTypeName(dom)}" +
                  probStr +
                  $"\n  행동: {MonsterDecisionTree.GetActionName(dec.ChosenAction)}" +
                  $" 슬롯={(dec.TargetSlot >= 0 ? ElementSlotSystem.SLOT_KEYS[dec.TargetSlot] : "-")} ({dec.Reason})");
    }
}
