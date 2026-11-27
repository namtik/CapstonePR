using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Battle.AI
{
    // 방해행동 결정을 CSV로 기록(오프라인 재학습용 실데이터 수집)
    public static class DisruptionLogger
    {
        const string HEADER =
            "f1,f2,f3,f4,f5,f7,enemy_hp,player_hp,hand_fire,hand_frag,hand_chain,hand_def,hand_null,hand_count,recover_ready,action"; // CSV 헤더 스키마

        static string _path;    // 세션 로그 파일 경로
        static bool _failed;    // 로그 초기화 실패 플래그

        // 로그 파일/디렉터리 준비(최초 1회)
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

        // 한 번의 결정을 CSV 한 줄로 기록
        public static void Log(AiDecision d)
        {
            Ensure();
            if (_path == null) return;
            try
            {
                float[] f = d.features;
                float[] c = d.context;
                string action = (d.action >= 0 && d.action < EnemyAiModel.ActionNames.Length)
                    ? EnemyAiModel.ActionNames[d.action] : "unknown";

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
