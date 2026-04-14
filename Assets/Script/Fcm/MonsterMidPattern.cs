using System.IO;
using UnityEngine;

/// <summary>
/// 몬스터 중간 패턴 컨트롤러
/// 
/// 파이프라인:
///   1. f1~f3 추출 (FCM용, 플레이어 행동 패턴)
///   2. f4 추출 (오염도, FCM 외부)
///   3. 프로필 블렌딩 (f1~f3만)
///   4. FCM 소속도 계산 (3차원)
///   5. 의사결정 트리 (소속도 + f1~f3 + f4)
///   6. 행동 실행
/// </summary>
public class MonsterMidPattern : MonoBehaviour
{
    [Header("의사결정 트리 임계값")]
    [SerializeField] private MonsterDecisionTree.Thresholds thresholds;

    [Header("스테이지 프로필 설정")]
    [SerializeField] private float profileBeta = 0.6f;
    [SerializeField] private float initialProfileWeight = 0.7f;
    [SerializeField] private float weightShiftPerTrigger = 0.15f;

    [Header("디버그 오버레이 (선택)")]
    [SerializeField] private FCMDebugOverlay debugOverlay;

    [Header("데이터 수집")]
    [SerializeField] private bool collectData = false;
    [SerializeField] private string logFolderName = "fcm_logs";

    private MonsterDecisionTree decisionTree;
    private int triggerCount = 0;
    private float[] stageProfile = null; // 3차원 (f1, f2, f3만)

    private string logFilePath;
    private bool headerWritten = false;

    private void Awake()
    {
        decisionTree = new MonsterDecisionTree(thresholds);
    }

    public void InitBattle()
    {
        triggerCount = 0;
        if (collectData) InitLogFile();
    }

    public string Execute()
    {
        triggerCount++;

        // 1. FCM용 특성 추출 (3차원: f1, f2, f3)
        float[] fcmFeatures = FeatureExtractor.ExtractFCMFeatures(out FeatureExtractor.RouteInfo[] routes);

        // 2. f4 별도 계산 (FCM 외부)
        float f4 = FeatureExtractor.CalcPollution(routes);

        // 3. 프로필 블렌딩 (f1~f3만)
        float[] blended = BlendWithProfile(fcmFeatures);

        // 4. FCM 소속도 (3차원)
        float[] membership = FCMAnalyzer.CalcMembership(blended);
        int dominant = FCMAnalyzer.GetDominantType(membership);

        // 5. 의사결정 트리 (소속도 + f1~f3 + f4)
        MonsterDecisionTree.Decision decision = decisionTree.Decide(membership, blended, f4, routes);

        // 6. 행동 실행
        string resultMessage = ExecuteAction(decision);

        // 디버그
        LogDecision(fcmFeatures, blended, f4, membership, dominant, decision);

        if (debugOverlay == null)
            debugOverlay = FindFirstObjectByType<FCMDebugOverlay>();
        debugOverlay?.ShowAnalysis(blended, f4, membership, dominant, decision, routes);

        if (collectData)
            WriteDataRow(fcmFeatures, blended, f4, membership, dominant, decision, routes);

        return resultMessage;
    }

    public void OnBattleEnd()
    {
        // 프로필 저장 (f1~f3만)
        float[] fcmFeatures = FeatureExtractor.ExtractFCMFeatures();
        UpdateProfile(fcmFeatures);
    }

    // --- 행동 실행 ---

    string ExecuteAction(MonsterDecisionTree.Decision decision)
    {
        switch (decision.ChosenAction)
        {
            case MonsterDecisionTree.Action.ComboShuffle:
                ComboSystem.Instance?.ShuffleComboInput();
                return "패턴 발동: 콤보 슬롯 셔플!";

            case MonsterDecisionTree.Action.SlotCurse:
                if (ElementSlotSystem.Instance != null && decision.TargetSlot >= 0)
                {
                    ElementSlotSystem.Instance.ApplyCurse(decision.TargetSlot);
                    return $"패턴 발동: {ElementSlotSystem.SLOT_KEYS[decision.TargetSlot]} 슬롯 저주";
                }
                return "패턴 발동: 저주";

            case MonsterDecisionTree.Action.NullInsert:
                if (ElementSlotSystem.Instance != null && decision.TargetSlot >= 0)
                {
                    ElementSlotSystem.Instance.InsertNullCard(decision.TargetSlot);
                    return $"패턴 발동: {ElementSlotSystem.SLOT_KEYS[decision.TargetSlot]}에 무속성 추가";
                }
                return "패턴 발동: 무속성 추가";

            default:
                return "패턴 발동";
        }
    }

    // --- 프로필 (3차원) ---

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

    // --- 데이터 수집 ---

    void InitLogFile()
    {
        string dir = Path.Combine(Application.persistentDataPath, logFolderName);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        logFilePath = Path.Combine(dir, $"fcm_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");
        headerWritten = false;
        Debug.Log($"[MidPattern] 데이터 수집: {logFilePath}");
    }

    void WriteDataRow(float[] raw, float[] blended, float f4,
                      float[] mem, int dominant,
                      MonsterDecisionTree.Decision dec,
                      FeatureExtractor.RouteInfo[] routes)
    {
        if (string.IsNullOrEmpty(logFilePath)) return;
        try
        {
            if (!headerWritten)
            {
                File.AppendAllText(logFilePath,
                    "trigger,f1,f2,f3,f4,f1_blend,f2_blend,f3_blend," +
                    "mem_rush,mem_path,mem_explorer,dominant,action,target," +
                    "route_q,route_w,route_e,route_r\n");
                headerWritten = true;
            }

            string action = MonsterDecisionTree.GetActionName(dec.ChosenAction);
            File.AppendAllText(logFilePath,
                $"{triggerCount},{raw[0]:F3},{raw[1]:F3},{raw[2]:F3},{f4:F3}," +
                $"{blended[0]:F3},{blended[1]:F3},{blended[2]:F3}," +
                $"{mem[0]:F3},{mem[1]:F3},{mem[2]:F3},{dominant},{action},{dec.TargetSlot}," +
                $"{routes[0].BestProximity:F2},{routes[1].BestProximity:F2}," +
                $"{routes[2].BestProximity:F2},{routes[3].BestProximity:F2}\n");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[MidPattern] 저장 실패: {e.Message}");
        }
    }

    // --- 디버그 ---

    void LogDecision(float[] raw, float[] blended, float f4,
                     float[] mem, int dominant,
                     MonsterDecisionTree.Decision dec)
    {
        Debug.Log($"[MidPattern] -- 트리거 #{triggerCount} --\n" +
                  $"  FCM특성: [{raw[0]:F2},{raw[1]:F2},{raw[2]:F2}] -> 블렌딩 [{blended[0]:F2},{blended[1]:F2},{blended[2]:F2}]\n" +
                  $"  f4(오염): {f4:F2} (FCM 외부)\n" +
                  $"  FCM:  러시={mem[0]:P0} 의존={mem[1]:P0} 탐색={mem[2]:P0} -> {FCMAnalyzer.GetTypeName(dominant)}\n" +
                  $"  행동: {MonsterDecisionTree.GetActionName(dec.ChosenAction)}" +
                  $" 슬롯={(dec.TargetSlot >= 0 ? ElementSlotSystem.SLOT_KEYS[dec.TargetSlot] : "-")} ({dec.Reason})");
    }
}