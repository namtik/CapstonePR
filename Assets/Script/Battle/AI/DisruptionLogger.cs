using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Battle.AI
{
    /// <summary>
    /// 방해행동 결정을 CSV로 기록(실데이터 수집 → 오프라인 Python 재학습용).
    /// 경로: {Application.persistentDataPath}/ai_logs/run_yyyyMMdd_HHmmss.csv (세션당 1파일).
    /// 스키마(설계문서 §7 = Python이 읽는 컬럼):
    ///   f1,f2,f3,f4,f5,f7, enemy_hp,player_hp, hand_fire,hand_frag,hand_chain,hand_def,hand_null, hand_count, recover_ready, action
    /// ⚠️ 현재는 (상태·선택행동)만 기록. 의미있는 RBFN 보상엔 실제 결과(승패/피해)가 필요(향후). epsilon 탐험으로 행동 다양성 확보.
    /// </summary>
    public static class DisruptionLogger
    {
        const string HEADER =
            "f1,f2,f3,f4,f5,f7,enemy_hp,player_hp,hand_fire,hand_frag,hand_chain,hand_def,hand_null,hand_count,recover_ready,action";

        static string _path;
        static bool _failed;

        static void Ensure()
        {
            if (_path != null || _failed) return;
            try
            {
                string dir = Path.Combine(Application.persistentDataPath, "ai_logs");
                Directory.CreateDirectory(dir);
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                _path = Path.Combine(dir, $"run_{stamp}.csv");
                File.WriteAllText(_path, HEADER + "\n");
                Debug.Log($"[적AI] 방해행동 로그 시작: {_path}");
            }
            catch (Exception e)
            {
                _failed = true;
                Debug.LogWarning($"[적AI] 로그 초기화 실패 — 로깅 비활성: {e.Message}");
            }
        }

        public static void Log(AiDecision d)
        {
            Ensure();
            if (_path == null) return;
            try
            {
                float[] f = d.features;   // [f1,f2,f3,f4,f5,f7]
                float[] c = d.context;    // [enemy_hp, player_hp, hand_fire..hand_null, hand_count/10]
                string action = (d.action >= 0 && d.action < EnemyAiModel.ActionNames.Length)
                    ? EnemyAiModel.ActionNames[d.action] : "unknown";

                // hand_count는 원값(c[7]은 /10 정규화라 사용 안 함), recover_ready는 0/1
                string row = string.Format(CultureInfo.InvariantCulture,
                    "{0:F4},{1:F4},{2:F4},{3:F4},{4:F4},{5:F4},{6:F4},{7:F4},{8:F4},{9:F4},{10:F4},{11:F4},{12:F4},{13},{14},{15}",
                    f[0], f[1], f[2], f[3], f[4], f[5],
                    c[0], c[1], c[2], c[3], c[4], c[5], c[6],
                    d.handCount, d.recoverReady ? 1 : 0, action);

                File.AppendAllText(_path, row + "\n");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[적AI] 로그 기록 실패: {e.Message}");
            }
        }
    }
}
