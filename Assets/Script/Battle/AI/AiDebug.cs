using UnityEngine;

namespace Battle.AI
{
    /// <summary>
    /// 적 AI 디버그 오버레이용 최신 결정/보상 스냅샷 허브 (폴링 기반, UI 비의존).
    /// - EnemyDisruptionAI.Decide() 가 결정을 게시(PublishDecision).
    /// - NewBattleController 의 ObserveDisruption/ResolveTrack 가 보상을 게시(PublishReward).
    /// 버전 카운터로 변경을 감지 → 오버레이는 Update에서 폴링해 바뀔 때만 다시 그린다.
    /// </summary>
    public static class AiDebug
    {
        // ── 최신 결정 ──
        public static AiDecision LastDecision;
        public static bool HasDecision;
        public static int DecisionVersion;   // 새 결정마다 +1

        // ── 최신 보상 (즉시형=결정과 동시, 지연형=발현 시점) ──
        public static int RewardAction = -1; // 0..5 (-1=아직 없음)
        public static float Reward;
        public static bool RewardImmediate;  // true=즉시, false=발현
        public static float Drift;           // 베이스 대비 가중치 드리프트
        public static int RewardVersion;     // 새 보상마다 +1

        public static void PublishDecision(AiDecision d)
        {
            LastDecision = d;
            HasDecision = true;
            DecisionVersion++;
        }

        public static void PublishReward(int action, float reward, bool immediate, float drift)
        {
            RewardAction = action;
            Reward = reward;
            RewardImmediate = immediate;
            Drift = drift;
            RewardVersion++;
        }

        /// <summary>매 Play 진입 시 초기화(에디터 도메인 리로드 off 대비).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            LastDecision = default;
            HasDecision = false;
            DecisionVersion = 0;
            RewardAction = -1;
            Reward = 0f;
            RewardImmediate = false;
            Drift = 0f;
            RewardVersion = 0;
        }
    }
}
