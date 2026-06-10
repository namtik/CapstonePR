using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 몬스터 중간 패턴: 게이지 5 빠른 판단, 게이지 10 온라인 학습
public class MonsterMidPattern : MonoBehaviour
{
    [Header("행동 결정 모드")]
    [SerializeField] private bool useRBFN = false;                  // RBFN 사용 여부
    [SerializeField] private bool stochastic = true;               // 확률적 선택 여부
    [SerializeField] private float deterministicThreshold = 0.80f; // 확정 선택 임계값

    [Header("RBFN")]
    [SerializeField] private RBFNetwork rbfn = new RBFNetwork();    // RBF 네트워크

    [Header("실시간 학습 (Online Learning)")]
    [Tooltip("RBFN 모드에서 게이지 10에 온라인 학습 실행")]
    [SerializeField] private bool onlineLearning = true;           // 온라인 학습 on/off
    [Tooltip("슬라이딩 윈도우 크기 (최근 N개 데이터만 기억)")]
    [SerializeField] private int maxMemorySize = 30;               // 기억 버퍼 크기

    [Header("의사결정 트리 임계값 (트리 모드 + 정답 생성용)")]
    [SerializeField] private MonsterDecisionTree.Thresholds thresholds; // 트리 임계값

    [Header("디버그")]
    [SerializeField] private FCMDebugOverlay debugOverlay;          // 디버그 오버레이

    [Header("데이터 수집")]
    [SerializeField] private bool collectData = false;             // CSV 수집 여부
    [SerializeField] private string logFolderName = "fcm_logs";    // 로그 폴더명
    [SerializeField] private string playerId = "";                 // 플레이어 식별자

    private MonsterDecisionTree decisionTree;   // 결정 트리 인스턴스
    private int triggerCount = 0;               // 발동 횟수
    private string logFilePath;                 // 로그 파일 경로
    private bool headerWritten = false;         // CSV 헤더 기록 여부

    private List<float[]> recentFeaturesBuf = new List<float[]>(); // 최근 특성 버퍼
    private List<int> recentActionsBuf = new List<int>();          // 최근 정답 행동 버퍼

    // 결정 트리 초기화
    void Awake()
    {
        decisionTree = new MonsterDecisionTree(thresholds);
    }

    // 전투 시작 시 발동 카운트 초기화 및 로그 준비
    public void InitBattle()
    {
        triggerCount = 0;
        if (collectData) InitLogFile();
    }

    // 새 플레이어 시작 시 기억 버퍼 초기화
    public void ResetMemory()
    {
        recentFeaturesBuf.Clear();
        recentActionsBuf.Clear();
    }

    // 게이지 5: 특성 추출→행동 결정→실행→정답 수집
    public string Execute()
    {
        triggerCount++;

        float[] features = FeatureExtractor.ExtractFCMFeatures(
            out FeatureExtractor.ThreatInfo[] threats);
        float f4 = FeatureExtractor.CalcPollution(threats);

        float[] membership = FCMAnalyzer.CalcMembership(features);
        int dominant = FCMAnalyzer.GetDominantType(membership);

        MonsterDecisionTree.Decision finalDecision;
        float[] actionProbs = null;

        if (useRBFN)
            finalDecision = DecideWithRBFN(features, f4, threats, out actionProbs);
        else
            finalDecision = decisionTree.Decide(membership, features, f4, threats);

        string result = ExecuteAction(finalDecision);

        if (onlineLearning)
        {
            var idealDecision = decisionTree.Decide(membership, features, f4, threats);

            recentFeaturesBuf.Add((float[])features.Clone());
            recentActionsBuf.Add((int)idealDecision.ChosenAction);

            while (recentFeaturesBuf.Count > maxMemorySize)
            {
                recentFeaturesBuf.RemoveAt(0);
                recentActionsBuf.RemoveAt(0);
            }
        }

        LogDecision(features, f4, membership, dominant, finalDecision, threats, actionProbs);

        if (debugOverlay == null)
            debugOverlay = FindFirstObjectByType<FCMDebugOverlay>();
        debugOverlay?.ShowAnalysis(features, f4, membership, dominant, finalDecision, threats, actionProbs);

        if (collectData)
            WriteDataRow(features, f4, membership, dominant, finalDecision, threats, actionProbs);

        return result;
    }

    // 게이지 10: 최근 데이터로 RBFN 가중치 재학습
    public void OnGauge10()
    {
        if (!useRBFN || !onlineLearning) return;
        if (recentFeaturesBuf.Count < 5) return;

        rbfn.RetrainWeightsOnline(recentFeaturesBuf, recentActionsBuf, maxMemorySize);

        Debug.Log($"[MidPattern] 온라인 학습 완료 (기억: {recentFeaturesBuf.Count}개)");
    }

    // 전투 종료 처리(버퍼는 유지)
    public void OnBattleEnd()
    {
    }

    // RBFN 순전파로 행동·확률 결정
    MonsterDecisionTree.Decision DecideWithRBFN(float[] features, float f4,
        FeatureExtractor.ThreatInfo[] threats, out float[] probs)
    {
        probs = rbfn.Forward(features);

        float maxProb = Mathf.Max(probs[0], Mathf.Max(probs[1], probs[2]));
        bool useDet = !stochastic || (maxProb >= deterministicThreshold);

        int actionIdx = rbfn.SelectAction(probs, useDet);
        var action = (MonsterDecisionTree.Action)actionIdx;

        int targetSlot = -1;
        if (action != MonsterDecisionTree.Action.ComboShuffle)
            targetSlot = FindBestTarget(action, threats);

        string reason = $"RBFN: 셔플{probs[0]:P0} 저주{probs[1]:P0} 무속성{probs[2]:P0}" +
                         (useDet ? " (확정)" : " (확률)") +
                         $" [기억:{recentFeaturesBuf.Count}개]";

        return new MonsterDecisionTree.Decision
        {
            ChosenAction = action,
            Reason = reason,
            TargetSlot = targetSlot,
        };
    }

    // 행동 종류에 맞는 대상 슬롯 선정
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
            if (slotSys.GetSlot(i).RemainingCount <= 0 || slotSys.GetSlot(i).IsCursed)
                score[i] = -1;

        int best = -1;
        if (action == MonsterDecisionTree.Action.SlotCurse)
        {
            for (int i = 0; i < 4; i++)
                if (score[i] > 0 && (best < 0 || score[i] > score[best])) best = i;
        }
        else
        {
            int minNull = int.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                if (score[i] < 0) continue;
                var slot = slotSys.GetSlot(i);
                int nc = slot.neutralDeckCount + slot.neutralGraveCount + (slot.hasNeutralCard ? 1 : 0);
                if (nc < minNull) { minNull = nc; best = i; }
            }
        }
        return best >= 0 ? best : Random.Range(0, 4);
    }

    // 결정된 방해행동을 실제 시스템에 적용
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

    // CSV 로그 파일 경로 초기화
    void InitLogFile()
    {
        string dir = Path.Combine(Application.persistentDataPath, logFolderName);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        string pid = string.IsNullOrEmpty(playerId) ? "p" + Random.Range(1000, 9999) : playerId;
        logFilePath = Path.Combine(dir,
            $"fcm_{pid}_{System.DateTime.Now:yyyyMMdd_HHmmss}_{Random.Range(1000, 9999)}.csv");
        headerWritten = false;
    }

    // 결정 1건을 CSV 한 행으로 기록
    void WriteDataRow(float[] features, float f4, float[] mem, int dom,
                      MonsterDecisionTree.Decision dec,
                      FeatureExtractor.ThreatInfo[] threats, float[] actionProbs)
    {
        if (string.IsNullOrEmpty(logFilePath)) return;
        try
        {
            if (!headerWritten)
            {
                File.AppendAllText(logFilePath,
                    "player,trigger,f1,f3,f5,f7,f8,f4," +
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
            { ps = $"{actionProbs[0]:F3}"; pc = $"{actionProbs[1]:F3}"; pn = $"{actionProbs[2]:F3}"; }

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
                $"{features[0]:F3},{features[1]:F3},{features[2]:F3},{features[3]:F3},{features[4]:F3},{f4:F3}," +
                $"{mem[0]:F3},{mem[1]:F3},{mem[2]:F3},{mem[3]:F3},{dom}," +
                $"{mode},{act},{dec.TargetSlot}," +
                $"{ps},{pc},{pn}," +
                $"{tt},{tc},{tch}\n");
        }
        catch (System.Exception e) { Debug.LogWarning($"[MidPattern] 저장 실패: {e.Message}"); }
    }

    // 결정 내용을 콘솔에 디버그 출력
    void LogDecision(float[] features, float f4, float[] mem, int dom,
                     MonsterDecisionTree.Decision dec,
                     FeatureExtractor.ThreatInfo[] threats, float[] actionProbs)
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
        string memStr = onlineLearning ? $" [기억:{recentFeaturesBuf.Count}개]" : "";

        Debug.Log($"[MidPattern] -- #{triggerCount} [{(useRBFN ? "RBFN" : "Tree")}]{memStr} --\n" +
                  $"  f1={features[0]:F2} f3={features[1]:F2} f5={features[2]:F2} f7={features[3]:F2} f8={features[4]:F2} | f4={f4:F2}\n" +
                  $"  위협:{threats.Length}개{ts}\n" +
                  $"  FCM: 러시={mem[0]:P0} 의존={mem[1]:P0} 탐색={mem[2]:P0} 버스트={mem[3]:P0} -> {FCMAnalyzer.GetTypeName(dom)}" +
                  probStr +
                  $"\n  행동: {MonsterDecisionTree.GetActionName(dec.ChosenAction)}" +
                  $" 슬롯={(dec.TargetSlot >= 0 ? ElementSlotSystem.SLOT_KEYS[dec.TargetSlot] : "-")} ({dec.Reason})");
    }
}
