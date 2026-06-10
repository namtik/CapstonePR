using System.Collections.Generic;
using UnityEngine;

namespace Battle.AI
{
    // 하이브리드 온라인 학습기 — 베이스 warm-start 후 RBFN Q-가중치만 미세조정
    public static class OnlineQLearner
    {
        public const float LR = 0.02f;       // 학습률
        public const float REG = 0.01f;      // 베이스로의 L2 정규화 강도
        public const int BATCH = 4;          // 미니배치 크기
        public const int BUFFER_MAX = 256;   // 리플레이 버퍼 상한

        struct Experience { public float[] state; public int action; public float target; } // 학습 경험 단위

        static float[,] _qType;   // 온라인 유형 가중치 [TYPES, ACTIONS]
        static float[,] _qCtx;    // 온라인 컨텍스트 가중치 [CTX, ACTIONS]
        static float[] _qBias;    // 온라인 바이어스 [ACTIONS]
        static readonly List<Experience> _buffer = new List<Experience>(); // 리플레이 버퍼
        static readonly System.Random _rng = new System.Random(12345);     // 미니배치 샘플링 RNG
        static bool _initialized; // 초기화 여부

        public static bool Initialized => _initialized; // 초기화 상태 노출

        // 세션(Play) 시작 시 베이스로 초기화
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void OnSessionStart() => ResetToBase();

        // 미초기화 시에만 베이스로 초기화
        public static void EnsureInit() { if (!_initialized) ResetToBase(); }

        // 온라인 가중치를 오프라인 베이스로 재복사
        public static void ResetToBase()
        {
            _qType = (float[,])EnemyAiModel.QTypeWeights.Clone();
            _qCtx = (float[,])EnemyAiModel.QContextWeights.Clone();
            _qBias = (float[])EnemyAiModel.QBias.Clone();
            _buffer.Clear();
            _initialized = true;
        }

        // 온라인 가중치로 Q[6] 계산
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

        // 즉시보상 경험 추가 후 1스텝 학습(밴딧)
        public static void Observe(float[] mu, float[] ctx, int action, float reward)
        {
            EnsureInit();
            if (mu == null || ctx == null || action < 0 || action >= EnemyAiModel.ACTIONS) return;
            AddExperience(mu, ctx, action, Mathf.Clamp(reward, -1f, 1f));
        }

        // TD(0) 경험 추가 — 타깃 r + γ·max_a' Q(s',a')
        public static void ObserveTD(float[] mu, float[] ctx, int action, float reward,
                                     float[] nextMu, float[] nextCtx, float gamma)
        {
            EnsureInit();
            if (mu == null || ctx == null || action < 0 || action >= EnemyAiModel.ACTIONS) return;

            float target = Mathf.Clamp(reward, -1f, 1f);
            if (gamma > 0f && nextMu != null && nextCtx != null)
                target += gamma * MaxQ(nextMu, nextCtx);
            AddExperience(mu, ctx, action, Mathf.Clamp(target, -2f, 2f));
        }

        // 상태 벡터 구성 + 버퍼 적재 + 1스텝 학습
        static void AddExperience(float[] mu, float[] ctx, int action, float target)
        {
            var state = new float[EnemyAiModel.TYPES + EnemyAiModel.CTX];
            for (int i = 0; i < EnemyAiModel.TYPES; i++) state[i] = mu[i];
            for (int j = 0; j < EnemyAiModel.CTX; j++) state[EnemyAiModel.TYPES + j] = ctx[j];

            _buffer.Add(new Experience { state = state, action = action, target = target });
            if (_buffer.Count > BUFFER_MAX) _buffer.RemoveAt(0);
            TrainMinibatch();
        }

        // 온라인 가중치 기준 max_a Q(s,a)
        static float MaxQ(float[] mu, float[] ctx)
        {
            var q = QTotal(mu, ctx);
            float best = q[0];
            for (int a = 1; a < q.Length; a++) if (q[a] > best) best = q[a];
            return best;
        }

        // 미니배치 SGD 1스텝 + 베이스로의 L2 당김
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
                float err = pred - e.target;

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

        // 베이스 대비 가중치 변화량(L2 norm)
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
