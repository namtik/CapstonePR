using System.Collections.Generic;
using UnityEngine;

namespace Battle.AI
{
    /// <summary>
    /// 하이브리드 온라인 학습기 — 오프라인 베이스(EnemyAiModel)에서 warm-start 후,
    /// 게임 중 실제 결과 보상으로 RBFN Q-가중치만 미세조정한다. FCM 중심점/공분산은 고정(여기서 안 다룸).
    /// 안정화: 베이스로의 L2 정규화 + 작은 학습률 + 미니배치 리플레이.
    /// 지속: 세션 단위(인메모리 static). 런 리셋에는 영향 없음. 게임(Play) 재시작 시 자동 베이스 복귀.
    /// </summary>
    public static class OnlineQLearner
    {
        public const float LR = 0.02f;       // 학습률(작게)
        public const float REG = 0.01f;      // 베이스로의 L2 당김(드리프트 방지) — 하이브리드 핵심
        public const int BATCH = 4;          // 미니배치 크기
        public const int BUFFER_MAX = 256;   // 리플레이 버퍼 상한

        struct Experience { public float[] state; public int action; public float reward; }

        static float[,] _qType;   // [TYPES, ACTIONS] 온라인 가중치
        static float[,] _qCtx;    // [CTX, ACTIONS]
        static float[] _qBias;    // [ACTIONS]
        static readonly List<Experience> _buffer = new List<Experience>();
        static readonly System.Random _rng = new System.Random(12345);
        static bool _initialized;

        public static bool Initialized => _initialized;

        // 세션(Play) 시작 시 베이스로 초기화 — 에디터 도메인 리로드 OFF에서도 매 Play마다 baseline 보장.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void OnSessionStart() => ResetToBase();

        public static void EnsureInit() { if (!_initialized) ResetToBase(); }

        /// <summary>온라인 가중치를 오프라인 베이스로 재복사(세션 리셋/안전 복귀).</summary>
        public static void ResetToBase()
        {
            _qType = (float[,])EnemyAiModel.QTypeWeights.Clone();
            _qCtx = (float[,])EnemyAiModel.QContextWeights.Clone();
            _qBias = (float[])EnemyAiModel.QBias.Clone();
            _buffer.Clear();
            _initialized = true;
        }

        /// <summary>온라인 가중치로 Q[6] 계산 (온라인 모드일 때 EnemyDisruptionAI가 사용).</summary>
        public static float[] QTotal(float[] mu, float[] ctx)
        {
            EnsureInit();
            var q = new float[EnemyAiModel.ACTIONS];
            for (int a = 0; a < EnemyAiModel.ACTIONS; a++)
            {
                float v = _qBias[a];
                for (int i = 0; i < EnemyAiModel.TYPES; i++) v += mu[i] * _qType[i, a];
                for (int j = 0; j < EnemyAiModel.CTX; j++) v += ctx[j] * _qCtx[j, a];
                q[a] = v;
            }
            return q;
        }

        /// <summary>경험(상태=[μ;ctx], 행동, 보상) 추가 후 미니배치 1스텝 학습.</summary>
        public static void Observe(float[] mu, float[] ctx, int action, float reward)
        {
            EnsureInit();
            if (mu == null || ctx == null || action < 0 || action >= EnemyAiModel.ACTIONS) return;

            var state = new float[EnemyAiModel.TYPES + EnemyAiModel.CTX];
            for (int i = 0; i < EnemyAiModel.TYPES; i++) state[i] = mu[i];
            for (int j = 0; j < EnemyAiModel.CTX; j++) state[EnemyAiModel.TYPES + j] = ctx[j];

            _buffer.Add(new Experience { state = state, action = action, reward = Mathf.Clamp(reward, -1f, 1f) });
            if (_buffer.Count > BUFFER_MAX) _buffer.RemoveAt(0);
            TrainMinibatch();
        }

        // Q_total(s,a) = state·W[:,a] + b[a] — 선택된 행동의 Q를 보상으로 회귀(SGD) + 베이스로의 L2.
        static void TrainMinibatch()
        {
            int n = _buffer.Count;
            if (n == 0) return;
            int b = Mathf.Min(BATCH, n);
            int K = EnemyAiModel.TYPES, C = EnemyAiModel.CTX;
            for (int s = 0; s < b; s++)
            {
                var e = _buffer[_rng.Next(n)];
                int a = e.action;

                float pred = _qBias[a];
                for (int i = 0; i < K; i++) pred += e.state[i] * _qType[i, a];
                for (int j = 0; j < C; j++) pred += e.state[K + j] * _qCtx[j, a];
                float err = pred - e.reward;

                for (int i = 0; i < K; i++)
                {
                    float g = err * e.state[i] + REG * (_qType[i, a] - EnemyAiModel.QTypeWeights[i, a]);
                    _qType[i, a] -= LR * g;
                }
                for (int j = 0; j < C; j++)
                {
                    float g = err * e.state[K + j] + REG * (_qCtx[j, a] - EnemyAiModel.QContextWeights[j, a]);
                    _qCtx[j, a] -= LR * g;
                }
                _qBias[a] -= LR * err;
            }
        }

        /// <summary>베이스 대비 가중치 변화량(L2 norm) — 디버그/모니터링용.</summary>
        public static float WeightDriftFromBase()
        {
            EnsureInit();
            float sum = 0f;
            for (int i = 0; i < EnemyAiModel.TYPES; i++)
                for (int a = 0; a < EnemyAiModel.ACTIONS; a++)
                { float d = _qType[i, a] - EnemyAiModel.QTypeWeights[i, a]; sum += d * d; }
            for (int j = 0; j < EnemyAiModel.CTX; j++)
                for (int a = 0; a < EnemyAiModel.ACTIONS; a++)
                { float d = _qCtx[j, a] - EnemyAiModel.QContextWeights[j, a]; sum += d * d; }
            for (int a = 0; a < EnemyAiModel.ACTIONS; a++)
            { float d = _qBias[a] - EnemyAiModel.QBias[a]; sum += d * d; }
            return Mathf.Sqrt(sum);
        }
    }
}
