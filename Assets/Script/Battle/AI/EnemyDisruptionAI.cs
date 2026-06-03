using System.Collections.Generic;
using UnityEngine;
using Battle.Card;
using Battle.Deck;

namespace Battle.AI
{
    /// <summary>플레이어 카드 분류(특성/컨텍스트 산출용).</summary>
    public enum CardCategory { Burn, Fragment, Chain, Defense, Neutral }

    /// <summary>한 번의 방해행동 결정 결과(통합/로깅용).</summary>
    public struct AiDecision
    {
        public int action;          // 0..5 (0 저주 ~ 5 버리기)
        public float[] features;    // [f1,f2,f3,f4,f5,f7]
        public float[] context;     // [enemy_hp, player_hp, hand_fire, hand_frag, hand_chain, hand_def, hand_null, hand_count/10]
        public float[] membership;  // μ [5]
        public float[] q;           // Q [6]
        public int handCount;       // 원값
        public bool recoverReady;
        public bool explored;       // epsilon 탐험으로 무작위 선택됐는지
    }

    /// <summary>
    /// FCM-RBFN 추론 — 사전학습 가중치(EnemyAiModel)로 현재 플레이어를 실시간 분석해 방해행동을 고른다.
    /// Python(rbfn_train_gk.py)의 fuzzy_membership / q_total / available_mask / argmax 를 그대로 이식.
    /// </summary>
    public static class EnemyDisruptionAI
    {
        // ── 카드 분류 (설계문서 §3.1 특성 정의 기준) ───────────────
        /// <summary>
        /// 카드를 화상/파편/연쇄/방어/무속성으로 분류.
        /// 설계문서: f1 화상=화상(burn)'카드' / f3 연쇄=다타격+연쇄 → **키워드(효과)** 로 판정(원소 색 폴백 X).
        /// 단 f2 파편=덱_(땅+파편)이라 **땅 원소는 파편으로 포함**. 키워드 없는 바닐라 단타 공격은 무속성.
        /// </summary>
        public static CardCategory Classify(CardData card)
        {
            if (card == null) return CardCategory.Neutral;
            if (card.IsFragment) return CardCategory.Fragment; // 파편 원소
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
                    // 화상 키워드와 직접 연관된 카드만(부여/소모/배가). 단순 불-색 공격은 제외.
                    if (st == "BURN" && (act == "APPLY_STATUS" || act == "GAIN_STATUS" ||
                                         act == "CONSUME_STATUS" || act == "DOUBLE_STATUS")) burn = true;
                    if (st == "CHAIN" && (act == "APPLY_STATUS" || act == "GAIN_STATUS")) chain = true;
                    if (act == "GAIN_BLOCK") block = true;
                    if (act == "HEAL") heal = true;
                    if (e.hits > maxHits) maxHits = e.hits;
                }
            }

            // 키워드(효과) 우선 — 원소 색만으론 분류하지 않음.
            if (burn) return CardCategory.Burn;                   // f1: 화상 카드
            if (chain || maxHits >= 2) return CardCategory.Chain; // f3: 연쇄 + 다타격(2+)
            if (block || heal) return CardCategory.Defense;       // 방어/회복
            if (card.element == CardElement.Earth) return CardCategory.Fragment; // f2: 땅+파편(땅 원소 포함)
            return CardCategory.Neutral;                          // 바닐라 단타(불/물/바람 기본공격) = 키워드 없음
        }

        // ── 특성 x[6] = [f1,f2,f3,f4,f5,f7] ─────────────────────────
        static float[] BuildFeatures()
        {
            var run = RunDeckState.Instance;
            int total = 0, burn = 0, frag = 0, chain = 0;
            float gaugeSum = 0f;
            if (run != null)
            {
                var deck = run.RunDeck;
                for (int i = 0; i < deck.Count; i++)
                {
                    var data = CardDatabase.GetById(deck[i].cardId);
                    if (data == null) continue;
                    var cat = Classify(data);
                    int cnt = deck[i].count;
                    total += cnt;
                    gaugeSum += data.gauge * cnt;
                    if (cat == CardCategory.Burn) burn += cnt;
                    else if (cat == CardCategory.Fragment) frag += cnt;
                    else if (cat == CardCategory.Chain) chain += cnt;
                }
            }

            float f1 = total > 0 ? (float)burn / total : 0f;   // 화상의존도
            float f2 = total > 0 ? (float)frag / total : 0f;   // 파편의존도
            float f3 = total > 0 ? (float)chain / total : 0f;  // 연속/연쇄의존도
            float avgGauge = total > 0 ? gaugeSum / total : 1f;
            float f5 = Mathf.Clamp01((3f - avgGauge) / 2f);    // 덱평균코스트(저코스트↔고코스트)
            float f4 = run != null ? run.DefenseWindowRatio : 0f;  // 방어성향(최근 윈도우)
            float f7 = 0f;                                      // 피버사이클
            if (run != null && run.NonAwakenCardUses > 0)
                f7 = Mathf.Min(1f, run.AwakenActivations / (run.NonAwakenCardUses / 15f));

            return new float[] { f1, f2, f3, f4, f5, f7 };
        }

        // ── 컨텍스트 c[8] ──────────────────────────────────────────
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

        // ── 퍼지 소속도 μ[5] (마할라노비스) ─────────────────────────
        static float[] FuzzyMembership(float[] x)
        {
            int K = EnemyAiModel.TYPES, D = EnemyAiModel.FEAT;
            var inv = new float[K];
            float sum = 0f;
            float power = -1f / (EnemyAiModel.M - 1f); // m=2 → -1
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

        // ── Q_total[6] ────────────────────────────────────────────
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

        // ── 가용성 마스크 (설계 §5.5) ──────────────────────────────
        static bool[] AvailabilityMask(int handCount, bool recoverReady)
        {
            var m = new bool[EnemyAiModel.ACTIONS];
            for (int a = 0; a < m.Length; a++) m[a] = true;
            if (handCount < 2) m[5] = false;     // 버리기: 패 2장 미만 제외
            if (!recoverReady) m[4] = false;     // 회복: 쿨다운 중 제외
            return m;
        }

        // ── 결정 ──────────────────────────────────────────────────
        /// <summary>현재 상태로 방해행동을 고른다. epsilon&gt;0이면 탐험(데이터 수집용 다양성).</summary>
        public static AiDecision Decide(CardDeckSystem deck, EnemyStat enemyStat, Player player,
                                        bool recoverReady, float epsilon, bool onlineLearning = false)
        {
            float[] x = BuildFeatures();
            float[] c = BuildContext(deck, enemyStat, player, out int handCount);
            float[] mu = FuzzyMembership(x);
            // 온라인 학습 모드면 세션 학습 가중치로 Q 계산(베이스에서 warm-start), 아니면 고정 베이스.
            float[] q = onlineLearning ? OnlineQLearner.QTotal(mu, c) : QTotal(mu, c);
            bool[] mask = AvailabilityMask(handCount, recoverReady);

            int action;
            bool explored = false;
            if (epsilon > 0f && Random.value < epsilon)
            {
                var avail = new List<int>();
                for (int a = 0; a < EnemyAiModel.ACTIONS; a++) if (mask[a]) avail.Add(a);
                action = avail[Random.Range(0, avail.Count)];
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
                if (action < 0) action = 0; // 안전망 — 0~3은 항상 가용
            }

            return new AiDecision
            {
                action = action,
                features = x,
                context = c,
                membership = mu,
                q = q,
                handCount = handCount,
                recoverReady = recoverReady,
                explored = explored
            };
        }

        // ── 휴리스틱 보상 (온라인 학습용 — Python action_is_good 이식) ──
        /// <summary>유형(argmax μ)·상황에 알맞은 행동이면 +1, 아니면 -1. 온라인 보상의 휴리스틱 항.</summary>
        public static float HeuristicReward(AiDecision d)
        {
            if (d.membership == null || d.context == null) return 0f;
            int type = ArgMax(d.membership);          // 0..4
            return ActionIsGood(type, d.action, d.context[0], d.handCount, d.context) ? 1f : -1f;
        }

        // ctx: [0 enemy_hp,1 player_hp,2 hand_fire,3 hand_frag,4 hand_chain,5 hand_def,6 hand_null,7 hand_count/10]
        static bool ActionIsGood(int type, int a, float enemyHp, int handCount, float[] ctx)
        {
            // 유형 무관 컨텍스트 강수
            if (a == 5 && handCount >= 4) return true;   // 버리기: 패 과부하
            if (a == 4 && enemyHp <= 0.3f) return true;  // 회복: 빈사

            if (!IsTypeCorrect(type, a)) return false;   // 유형 적합 행동만
            if (a == 0)                                  // 저주: 압박 속성 손패 비율 ≥ 0.25
            {
                int attr = CurseAttrCtxIndex(type);
                return attr >= 0 && ctx[attr] >= 0.25f;
            }
            if (a == 5) return handCount >= 2;           // 버리기
            if (a == 4) return enemyHp <= 0.5f;          // 회복
            return true;                                 // 탈진/흡수/강화
        }

        // 유형별 알맞은 행동 (0저주 1탈진 2흡수 3강화 4회복 5버리기)
        static bool IsTypeCorrect(int type, int a)
        {
            switch (type)
            {
                case 0: return a == 0;                     // 화상형: 저주
                case 1: return a == 1;                     // 파편형: 탈진
                case 2: return a == 0 || a == 3 || a == 5; // 연속타격형: 저주/강화/버리기
                case 3: return a == 2;                     // 피버형: 흡수
                case 4: return a == 3 || a == 4;           // 안정형: 강화/회복
                default: return false;
            }
        }

        // 유형이 압박하는 속성의 context 인덱스(저주 유효성용). 피버형은 없음(-1).
        static int CurseAttrCtxIndex(int type)
        {
            switch (type)
            {
                case 0: return 2; // 화상형 → hand_fire
                case 1: return 3; // 파편형 → hand_frag
                case 2: return 4; // 연속타격형 → hand_chain
                case 4: return 5; // 안정형 → hand_def
                default: return -1;
            }
        }

        static int ArgMax(float[] v)
        {
            int idx = 0; float best = v[0];
            for (int i = 1; i < v.Length; i++) if (v[i] > best) { best = v[i]; idx = i; }
            return idx;
        }
    }
}
