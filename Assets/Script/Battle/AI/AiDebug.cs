using UnityEngine;

namespace Battle.AI
{
    // 적 AI 디버그 오버레이용 최신 결정/보상 스냅샷 허브
    public static class AiDebug
    {
        public static AiDecision LastDecision;   // 최신 결정 스냅샷
        public static bool HasDecision;          // 결정 존재 여부
        public static int DecisionVersion;       // 결정 변경 카운터

        public static int RewardAction = -1;     // 보상이 귀속된 행동 인덱스(-1=없음)
        public static float Reward;              // 최신 보상값
        public static bool RewardImmediate;      // 즉시형 보상 여부
        public static float Drift;               // 베이스 대비 가중치 드리프트
        public static int RewardVersion;         // 보상 변경 카운터

        // 최신 결정을 게시하고 버전 증가
        public static void PublishDecision(AiDecision d)
        {
            LastDecision = d;
            HasDecision = true;
            DecisionVersion++;
        }

        // 최신 보상을 게시하고 버전 증가
        public static void PublishReward(int action, float reward, bool immediate, float drift)
        {
            RewardAction = action;
            Reward = reward;
            RewardImmediate = immediate;
            Drift = drift;
            RewardVersion++;
        }

        // Play 진입 시 정적 상태 초기화
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
