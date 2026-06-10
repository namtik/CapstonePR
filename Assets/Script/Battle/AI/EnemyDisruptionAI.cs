using System.Collections.Generic;
using UnityEngine;
using Battle.Card;
using Battle.Deck;

namespace Battle.AI
{
    // 플레이어 카드 분류 범주
    public enum CardCategory { Burn, Fragment, Chain, Defense, Neutral }

    // 한 번의 방해행동 결정 결과(통합/로깅용)
    public struct AiDecision
    {
        public int action;          // 선택된 행동 인덱스 0..5
        public float[] features;    // 특성 [f1,f2,f3,f4,f5,f7]
        public float[] context;     // 컨텍스트 8차원
        public float[] membership;  // 퍼지 소속도 μ[5]
        public float[] q;           // 행동가치 Q[6]
        public bool[] mask;         // 가용성 마스크 [6]
        public int handCount;       // 손패 수(원값)
        public bool recoverReady;   // 회복 쿨다운 준비 여부
        public bool explored;       // epsilon 탐험 선택 여부
    }

    // FCM-RBFN 추론 — 현재 플레이어를 분석해 방해행동을 고른다
    public static class EnemyDisruptionAI
    {
        // 카드를 화상/파편/연쇄/방어/무속성으로 분류
        public static CardCategory Classify(CardData card)
        {
            if (card == null) return CardCategory.Neutral;
            if (card.IsFragment) return CardCategory.Fragment;
            if (card.IsNeutral) return CardCategory.Neutral;

            bool burn = false, chain = false, block = false, heal = false;
            int maxHits = 0;
            var effects = CardDatabase.GetEffects(card.id);
            if (effects != null)
            {
                for (int i = 0; i < effects.Count; i++)
                {
                    var e = effects[i];
                    string act = (e.doAction ?? "").Trim().ToUpperInvariant();
                    string st = (e.status ?? "").Trim().ToUpperInvariant();
                    if (st == "BURN" && (act == "APPLY_STATUS" || act == "GAIN_STATUS" ||
                                         act == "CONSUME_STATUS" || act == "DOUBLE_STATUS")) burn = true;
                    if (st == "CHAIN" && (act == "APPLY_STATUS" || act == "GAIN_STATUS")) chain = true;
                    if (act == "GAIN_BLOCK") block = true;
                    if (act == "HEAL") heal = true;
                    if (e.hits > maxHits) maxHits = e.hits;
                }
            }

            if (burn) return CardCategory.Burn;
            if (chain || maxHits >= 2) return CardCategory.Chain;
            if (block || heal) return CardCategory.Defense;
            if (card.element == CardElement.Earth) return CardCategory.Fragment;
            return CardCategory.Neutral;
        }

        // 특성 x[6] 산출 — 실제 플레이 패턴(사용 빈도/평균 코스트/각성 활용)
        static float[] BuildFeatures()
        {
            var run = RunDeckState.Instance;
            if (run == null) return new float[] { 0f, 0f, 0f, 0f, 0.5f, 0f };

            float f1 = run.BurnUseRatio;
            float f2 = run.FragmentUseRatio;
            float f3 = run.ChainUseRatio;
            float f4 = run.DefenseWindowRatio;
            float f5 = Mathf.Clamp01((3f - run.AvgUsedCost) / 2f);
            float f7 = run.NonAwakenCardUses > 0
                ? Mathf.Min(1f, run.AwakenActivations / (run.NonAwakenCardUses / 15f)) : 0f;

            return new float[] { f1, f2, f3, f4, f5, f7 };
        }

        // 컨텍스트 c[8] 산출(HP/손패 구성/손패 수)
        static float[] BuildContext(CardDeckSystem deck, EnemyStat enemyStat, Player player, out int handCount)
        {
            float enemyHp = (enemyStat != null && enemyStat.maxHp > 0f)
                ? Mathf.Clamp01(enemyStat.currentHp / enemyStat.maxHp) : 1f;
            float playerHp = (player != null && player.maxHp > 0)
                ? Mathf.Clamp01((float)player.currentHp / player.maxHp) : 1f;

            handCount = deck != null ? deck.HandCount : 0;
            int hf = 0, hfr = 0, hc = 0, hd = 0, hn = 0;
            if (deck != null)
            {
                var hand = deck.Hand;
                for (int i = 0; i < hand.Count; i++)
                {
                    switch (Classify(hand[i].data))
                    {
                        case CardCategory.Burn: hf++; break;
                        case CardCategory.Fragment: hfr++; break;
                        case CardCategory.Chain: hc++; break;
                        case CardCategory.Defense: hd++; break;
                        default: hn++; break;
                    }
                }
            }
            float inv = handCount > 0 ? 1f / handCount : 0f;
            return new float[]
            {
                enemyHp, playerHp,
                hf * inv, hfr * inv, hc * inv, hd * inv, hn * inv,
                Mathf.Min(handCount / 10f, 1f)
            };
        }

        // 퍼지 소속도 μ[5] 계산(마할라노비스 거리 기반)
        static float[] FuzzyMembership(float[] x)
        {
            int K = EnemyAiModel.TYPES, D = EnemyAiModel.FEAT;
            var inv = new float[K];
            float sum = 0f;
            float power = -1f / (EnemyAiModel.M - 1f);
            var diff = new float[D];
            for (int k = 0; k < K; k++)
            {
                for (int d = 0; d < D; d++) diff[d] = x[d] - EnemyAiModel.Centroids[k, d];
                float dist2 = MahalanobisSq(diff, EnemyAiModel.CovInvs[k]);
                dist2 = Mathf.Max(dist2, 1e-12f);
                inv[k] = Mathf.Pow(dist2, power);
                sum += inv[k];
            }
            var mu = new float[K];
            for (int k = 0; k < K; k++) mu[k] = sum > 0f ? inv[k] / sum : 1f / K;
            return mu;
        }

        // 마할라노비스 제곱 거리 diff·A·diff
        static float MahalanobisSq(float[] diff, float[,] A)
        {
            int n = diff.Length;
            float s = 0f;
            for (int i = 0; i < n; i++)
            {
                float row = 0f;
                for (int j = 0; j < n; j++) row += A[i, j] * diff[j];
                s += diff[i] * row;
            }
            return s;
        }

        // 행동별 종합 가치 Q_total[6] 계산
        static float[] QTotal(float[] mu, float[] ctx)
        {
            var q = new float[EnemyAiModel.ACTIONS];
            for (int a = 0; a < EnemyAiModel.ACTIONS; a++)
            {
                float v = EnemyAiModel.QBias[a];
                for (int i = 0; i < EnemyAiModel.TYPES; i++) v += mu[i] * EnemyAiModel.QTypeWeights[i, a];
                for (int j = 0; j < EnemyAiModel.CTX; j++) v += ctx[j] * EnemyAiModel.QContextWeights[j, a];
                q[a] = v;
            }
            return q;
        }

        // 가용성 마스크 산출 — 상태의존(self-state) 반영
        static bool[] AvailabilityMask(int handCount, bool recoverReady,
                                       bool curseActive, bool enhanceActive, int awakenGauge)
        {
            var m = new bool[EnemyAiModel.ACTIONS];
            for (int a = 0; a < m.Length; a++) m[a] = true;
            if (curseActive) m[0] = false;
            if (awakenGauge <= 0) m[2] = false;
            if (enhanceActive) m[3] = false;
            if (!recoverReady) m[4] = false;
            if (handCount < 2) m[5] = false;
            return m;
        }

        // 현재 상태로 방해행동을 결정(epsilon 탐험 포함)
        public static AiDecision Decide(CardDeckSystem deck, EnemyStat enemyStat, Player player,
                                        bool recoverReady, float epsilon, bool onlineLearning,
                                        bool curseActive, bool enhanceActive, int awakenGauge)
        {
            float[] x = BuildFeatures();
            float[] c = BuildContext(deck, enemyStat, player, out int handCount);
            float[] mu = FuzzyMembership(x);
            float[] q = onlineLearning ? OnlineQLearner.QTotal(mu, c) : QTotal(mu, c);
            bool[] mask = AvailabilityMask(handCount, recoverReady, curseActive, enhanceActive, awakenGauge);

            int action;
            bool explored = false;
            if (epsilon > 0f && Random.value < epsilon)
            {
                var avail = new List<int>();
                for (int a = 0; a < EnemyAiModel.ACTIONS; a++) if (mask[a]) avail.Add(a);
                action = avail.Count > 0 ? avail[Random.Range(0, avail.Count)] : 1;
                explored = true;
            }
            else
            {
                action = -1;
                float best = float.NegativeInfinity;
                for (int a = 0; a < EnemyAiModel.ACTIONS; a++)
                {
                    if (!mask[a]) continue;
                    if (q[a] > best) { best = q[a]; action = a; }
                }
                if (action < 0)
                {
                    for (int a = 0; a < EnemyAiModel.ACTIONS; a++) if (mask[a]) { action = a; break; }
                    if (action < 0) action = 1;
                }
            }

            var decision = new AiDecision
            {
                action = action,
                features = x,
                context = c,
                membership = mu,
                q = q,
                mask = mask,
                handCount = handCount,
                recoverReady = recoverReady,
                explored = explored
            };
            AiDebug.PublishDecision(decision);
            return decision;
        }

        // 휴리스틱 보상 — 유형/상황에 적합하면 +1, 아니면 -1
        public static float HeuristicReward(AiDecision d)
        {
            if (d.membership == null || d.context == null) return 0f;
            int type = ArgMax(d.membership);
            return ActionIsGood(type, d.action, d.context[0], d.handCount, d.context) ? 1f : -1f;
        }

        // 유형/컨텍스트 대비 행동 적합성 판정
        static bool ActionIsGood(int type, int a, float enemyHp, int handCount, float[] ctx)
        {
            if (a == 5 && handCount >= 4) return true;
            if (a == 4 && enemyHp <= 0.3f) return true;

            if (!IsTypeCorrect(type, a)) return false;
            if (a == 0)
            {
                int attr = CurseAttrCtxIndex(type);
                return attr >= 0 && ctx[attr] >= 0.25f;
            }
            if (a == 5) return handCount >= 2;
            if (a == 4) return enemyHp <= 0.5f;
            return true;
        }

        // 유형별 알맞은 행동인지 판정
        static bool IsTypeCorrect(int type, int a)
        {
            switch (type)
            {
                case 0: return a == 0;
                case 1: return a == 1;
                case 2: return a == 0 || a == 3 || a == 5;
                case 3: return a == 2;
                case 4: return a == 3 || a == 4;
                default: return false;
            }
        }

        // 유형이 압박하는 속성의 컨텍스트 인덱스(저주 유효성용)
        static int CurseAttrCtxIndex(int type)
        {
            switch (type)
            {
                case 0: return 2;
                case 1: return 3;
                case 2: return 4;
                case 4: return 5;
                default: return -1;
            }
        }

        // 최댓값 인덱스 반환
        static int ArgMax(float[] v)
        {
            int idx = 0; float best = v[0];
            for (int i = 1; i < v.Length; i++) if (v[i] > best) { best = v[i]; idx = i; }
            return idx;
        }
    }
}
