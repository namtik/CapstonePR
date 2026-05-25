using System;
using System.Collections.Generic;
using UnityEngine;
using Battle.Card;

namespace Battle.Deck
{
    /// <summary>
    /// 카드 효과 실행 컨텍스트.
    /// NewBattleController가 매 카드 사용 시 주입.
    /// </summary>
    public class CardEffectContext
    {
        public Player player;
        public EnemyController enemy;
        public EnemyStat enemyStat;
        public CardDeckSystem deck;
        public CardEffectResolver resolver;

        // ── 파워 효과(Ctx 캐시 — 데이터 드리븐 파워 등록 시 채워짐) ──
        public bool bonusDamagePerCardActive;     // 바람22(321): 카드 사용 시 +N
        public int  bonusDamagePerCard;
        public bool attackHitCountBonusActive;    // 바람21(320): 공격 카드 타격 +N
        public int  attackHitCountBonus;
        public bool burnOnHitActive;              // 불19(118): 피해 줄 때마다 화상 N
        public int  burnOnHitAmount;
        public bool burnOnExileActive;            // 불22(121): 카드 소멸 시 화상 N
        public int  burnOnExileAmount;
        public bool fragmentOnEarthUseActive;     // 땅21(420): 땅 카드 사용 시 파편 추가
        public bool fragmentOnFragmentUseActive;  // 땅22(421): 파편 카드 사용 시 파편 추가
        public bool blockOnCardUseActive;         // 땅20(419): 카드 사용 시 방어도 N
        public int  blockOnCardUseAmount;
        public bool burnOnByCardActive;           // 불20(119): 카드 효과로 화상 부여 시 N 피해
        public int  burnOnByCardAmount;
        public bool fragmentOnBlockConsumeActive; // 땅18(417): 방어도 소모 시 파편 추가
        public bool fragmentOnFragmentUse2Active; // 땅22(421) 중복 — 위와 통합 사용

        public bool healToDrawActive;             // 물19(218): 회복 시 드로우 1
        public bool discardToDrawActive;          // 물21(220): 카드 버릴 때마다 드로우 1
        public bool waterUseHealActive;           // 물22(221): 물 카드 사용 시 회복 1
        public bool healOnCurseCleanseActive;     // 물20(219): 저주 해제 시 회복 N
        public int  healOnCurseCleanseAmount;
        public bool healOnPlayerHitActive;        // 물18(217): 피격 시 회복 1
        public bool chainGainOnHitActive;         // 바람19(318): 피해 시 연쇄 +1
        public int  chainCount;                   // 연쇄 누적
        public int  chainBonusDamage;             // 바람18(317): 연쇄 피해 +N
        public int  attackPowerBonus;             // 불18(117): 공격력 +N (DAMAGE에 더해짐)
        public int  damageMultiplierActive;       // 불21(120): 체력 25% 이하 시 2배 (값=배율)
        public int  damageMultiplierThresholdHp;  // 임계 체력%

        // ── 런타임 통계 ──
        public int lastHpLost;
        public int lastDiscardedCount;
        public int usedAttackCardCount;
        public int usedFragmentCardCount;
        public CardType? previousCardType;
        public bool firstCardAfterEnemyAttack;    // 적 공격 직후 다음 카드 무료 판정용
        public bool nextCardIsFree;               // 바람14(313)
        public bool currentCardJustDrawn;         // 바람11(310) USED_IMMEDIATELY_AFTER_DRAW용
        public Dictionary<string, int> usedCardNameCounts = new Dictionary<string, int>();

        public int handLimitBonus;                // 땅19(418)
        public bool firstCardAfterAttackFreeActive; // 바람20(319) 등록 여부
    }

    /// <summary>
    /// 카드 효과 엔진. Resources/CardDB/CardEffects.json 기반으로 카드 효과를 해석/실행.
    /// 특수 동작(선택 모드, 효과 재사용 등)은 ResolveResult 플래그로 외부에 위임.
    /// </summary>
    public class CardEffectResolver
    {
        public struct ResolveResult
        {
            public bool exile;
            public bool keepOnField;                  // 파워 카드 — 필드 잔류
            public int  totalDamage;
            public bool requiresHandExileSelection;   // SELECT_ONE_FROM_HAND + EXHAUST
            public bool requiresHandDiscardSelection; // SELECT_ONE_FROM_HAND + DISCARD
            public bool requiresHandCopySelection;    // SELECT_ONE_FROM_HAND + COPY
            public bool requiresDrawPileMoveSelection;// SELECT_ONE_FROM_DRAW_PILE + MOVE
            public bool requiresDiscardMoveSelection; // SELECT_ONE_FROM_DISCARD + MOVE
            public bool requiresFragmentPoolSelection;// SELECT_ONE_FROM_FRAGMENT_POOL
            public bool skipNextGaugeCost;            // 바람14(313)
            public bool recastLastCard;               // 물12(211)
            public bool currentCardFreeThisUse;       // 바람11(310) — 이 카드 자체가 게이지 무료
            public bool poweredField;                 // 명시적 keepOnField와 동의어 (구버전 호환)
        }

        public CardEffectContext Ctx { get; private set; }

        public void Bind(CardEffectContext ctx)
        {
            Ctx = ctx;
            ctx.resolver = this;
        }

        // ─────────────────────────────────────────────────────────────
        // Resolve — 메인 진입점
        // ─────────────────────────────────────────────────────────────

        public ResolveResult Resolve(CardInstance card)
        {
            var result = new ResolveResult();

            // 1. 저주 처리
            if (card.cursed && Ctx.player != null)
            {
                Ctx.player.TakeDamage(5);
                Debug.Log($"[Curse] {card.data.displayName} 저주 발동 — 플레이어 5 피해");
                card.cursed = false;
            }

            // 2. 효과 실행
            bool isPower = card.Type == CardType.Power || card.data.HasTag("POWER");
            var effects = CardDatabase.GetEffects(card.Id);
            if (effects != null && effects.Count > 0)
            {
                foreach (var eff in effects)
                {
                    if (isPower && !IsImmediateTrigger(eff.when))
                    {
                        RegisterPowerTrigger(eff, card);
                        continue;
                    }
                    if (!IsImmediateTrigger(eff.when)) continue;
                    if (!EvaluateCondition(eff.ifCond, card, ref result)) continue;
                    ApplyAction(eff, card, ref result);
                }
            }
            else
            {
                Debug.LogWarning($"[CardEffect] 효과 정의 없음 id={card.Id} ({card.data.displayName})");
            }

            // 3. EXHAUST 태그 → 소멸
            if (card.data.HasTag("EXHAUST")) result.exile = true;

            // 4. POWER → 필드 잔류
            if (isPower)
            {
                result.keepOnField = true;
                result.poweredField = true;
            }

            // 5. 공통 파워 트리거 (자기 자신 제외)
            bool applyPowerEffects = !result.keepOnField;

            // 바람22(321) — 카드 사용 시마다 추가 피해
            if (applyPowerEffects && Ctx.bonusDamagePerCardActive && Ctx.bonusDamagePerCard > 0)
                result.totalDamage += DealDamage(Ctx.bonusDamagePerCard, isAttackCard: false);

            // 땅20(419) — 카드 사용 시마다 방어도
            if (applyPowerEffects && Ctx.blockOnCardUseActive && Ctx.blockOnCardUseAmount > 0)
                AddPlayerGuard(Ctx.blockOnCardUseAmount);

            // 땅21(420) — 땅 속성 카드 사용 시 파편을 버린 더미에 추가
            if (applyPowerEffects && Ctx.fragmentOnEarthUseActive && card.Element == CardElement.Earth)
                Ctx.deck?.AddToDiscard(CardDatabase.CreateFragmentInstance());

            // 땅22(421) — 파편 카드 사용 시 파편 1장 추가
            if (applyPowerEffects && Ctx.fragmentOnFragmentUseActive && card.Element == CardElement.Fragment)
                Ctx.deck?.AddToDiscard(CardDatabase.CreateFragmentInstance());

            // 물22(221) — 물 카드 사용 시 회복 1
            if (applyPowerEffects && Ctx.waterUseHealActive && card.Element == CardElement.Water)
                HealPlayer(1);

            return result;
        }

        public void NotifyExile(CardInstance card)
        {
            // 파워(불22/121) — 카드 소멸 시마다 화상
            if (Ctx.burnOnExileActive && Ctx.burnOnExileAmount > 0)
                AddEnemyStatus("burn", Ctx.burnOnExileAmount, isCardEffect: true);

            // 카드 자신의 ON_SELF_EXHAUST 트리거 (불10/109)
            if (card == null) return;
            var effects = CardDatabase.GetEffects(card.Id);
            if (effects == null) return;
            for (int i = 0; i < effects.Count; i++)
            {
                var eff = effects[i];
                string when = (eff.when ?? "").Trim().ToUpperInvariant();
                if (when != "ON_SELF_EXHAUST") continue;
                var dummyResult = new ResolveResult();
                if (!EvaluateCondition(eff.ifCond, card, ref dummyResult)) continue;
                ApplyAction(eff, card, ref dummyResult);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Extra 키=값;키=값 파싱
        // ─────────────────────────────────────────────────────────────

        static string ExtraSubstring(string extra, string key)
        {
            if (string.IsNullOrEmpty(extra) || string.IsNullOrEmpty(key)) return "";
            var parts = extra.Split(';');
            foreach (var p in parts)
            {
                int eq = p.IndexOf('=');
                if (eq <= 0) continue;
                string k = p.Substring(0, eq).Trim();
                if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                    return p.Substring(eq + 1).Trim();
            }
            return "";
        }

        static int ExtraIntOrDefault(string extra, string key, int def)
        {
            string s = ExtraSubstring(extra, key);
            if (string.IsNullOrEmpty(s)) return def;
            return int.TryParse(s, out int v) ? v : def;
        }

        // ─────────────────────────────────────────────────────────────
        // Trigger 분류
        // ─────────────────────────────────────────────────────────────

        static bool IsImmediateTrigger(string when)
        {
            if (string.IsNullOrEmpty(when)) return true;
            switch (when.Trim().ToUpperInvariant())
            {
                case "ON_USE":
                case "ON_USE_BEFORE_GAUGE":
                    return true;
                default:
                    return false;
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 파워 카드 등록
        // ─────────────────────────────────────────────────────────────

        void RegisterPowerTrigger(CardEffectData eff, CardInstance card)
        {
            string when = (eff.when ?? "").Trim().ToUpperInvariant();
            string verb = (eff.doAction ?? "").Trim().ToUpperInvariant();
            string status = (eff.status ?? "").Trim().ToUpperInvariant();
            int amount = eff.amount;

            // 카드 ID별 매핑이 더 안정적이지만, 데이터의 When/Do로 추론.
            switch (when)
            {
                case "ON_CARD_DAMAGE_HIT_DEALT":
                    // 불19(118): 피해 줄 때 화상 N
                    if (verb == "APPLY_STATUS" && status == "BURN")
                    {
                        Ctx.burnOnHitActive = true;
                        Ctx.burnOnHitAmount += amount;
                    }
                    // 바람19(318): 피해 시 연쇄 1
                    else if (verb == "GAIN_STATUS" && status == "CHAIN")
                    {
                        Ctx.chainGainOnHitActive = true;
                    }
                    break;

                case "ON_BURN_APPLIED_BY_CARD_EFFECT":
                    // 불20(119): 화상 부여 시 적에게 피해
                    if (verb == "DAMAGE")
                    {
                        Ctx.burnOnByCardActive = true;
                        Ctx.burnOnByCardAmount += amount;
                    }
                    break;

                case "ON_ATTACK_CARD_DAMAGE_CALC":
                    // 바람21(320): 타격 횟수 +N
                    if (verb == "MODIFY_HIT_COUNT")
                    {
                        Ctx.attackHitCountBonusActive = true;
                        Ctx.attackHitCountBonus += amount;
                    }
                    // 불21(120): 체력 25% 이하 시 피해 배율 N
                    else if (verb == "MODIFY_DAMAGE_MULTIPLIER")
                    {
                        Ctx.damageMultiplierActive = amount;
                        // 조건 ENEMY/PLAYER_HP_RATE_LTE:25 등을 ifCond로 받음
                        int hp = ParseRatioFromIfCond(eff.ifCond);
                        Ctx.damageMultiplierThresholdHp = hp > 0 ? hp : 25;
                    }
                    break;

                case "ON_CARD_USED":
                    // 바람22(321): 피해
                    if (verb == "DAMAGE")
                    {
                        Ctx.bonusDamagePerCardActive = true;
                        Ctx.bonusDamagePerCard += amount;
                    }
                    // 땅20(419): 방어도
                    else if (verb == "GAIN_BLOCK")
                    {
                        Ctx.blockOnCardUseActive = true;
                        Ctx.blockOnCardUseAmount += amount;
                    }
                    break;

                case "ON_ELEMENT_CARD_USED":
                    {
                        var elem = (ParseEnumValueFromCond(eff.ifCond, "CARD_ELEMENT_EQ") ?? "").ToUpperInvariant();
                        if (verb == "HEAL" && elem == "WATER")
                        {
                            Ctx.waterUseHealActive = true; // 물22(221)
                        }
                        else if (verb == "ADD_CARD" && elem == "EARTH")
                        {
                            Ctx.fragmentOnEarthUseActive = true; // 땅21(420)
                        }
                    }
                    break;

                case "ON_FRAGMENT_CARD_USED":
                    if (verb == "ADD_CARD")
                        Ctx.fragmentOnFragmentUseActive = true; // 땅22(421)
                    break;

                case "ON_CARD_EXHAUSTED":
                    if (verb == "APPLY_STATUS" && status == "BURN")
                    {
                        Ctx.burnOnExileActive = true; // 불22(121)
                        Ctx.burnOnExileAmount += amount;
                    }
                    break;

                case "ON_PLAYER_HEALED_ACTUAL":
                    if (verb == "DRAW_CARD") Ctx.healToDrawActive = true; // 물19(218)
                    break;

                case "ON_PLAYER_HIT_INCLUDING_BLOCK":
                    if (verb == "HEAL") Ctx.healOnPlayerHitActive = true; // 물18(217)
                    break;

                case "ON_CURSE_CLEANSED":
                    if (verb == "HEAL")
                    {
                        Ctx.healOnCurseCleanseActive = true; // 물20(219)
                        Ctx.healOnCurseCleanseAmount += amount;
                    }
                    break;

                case "ON_CARD_DISCARDED_BY_CARD_EFFECT":
                    if (verb == "DRAW_CARD") Ctx.discardToDrawActive = true; // 물21(220)
                    break;

                case "ON_BLOCK_CONSUMED_BY_ATTACK":
                    if (verb == "ADD_CARD") Ctx.fragmentOnBlockConsumeActive = true; // 땅18(417)
                    break;

                case "ON_CARD_USED_BEFORE_GAUGE":
                    // 바람20(319): 적 공격 후 첫 카드 무료
                    if (verb == "SET_GAUGE_COST" && amount == 0)
                        Ctx.firstCardAfterAttackFreeActive = true;
                    break;
            }

            // GAIN_STAT (불18 117): 공격력 +N
            if (verb == "GAIN_STAT")
            {
                string statKey = (eff.status ?? "").Trim().ToUpperInvariant();
                if (statKey == "ATTACK_POWER") Ctx.attackPowerBonus += amount;
                else if (statKey == "CHAIN_DAMAGE_BONUS") Ctx.chainBonusDamage += amount;
            }

            // MODIFY_HAND_LIMIT (땅19 418)
            if (verb == "MODIFY_HAND_LIMIT")
            {
                Ctx.handLimitBonus += amount;
            }
        }

        static int ParseRatioFromIfCond(string ifCond)
        {
            if (string.IsNullOrEmpty(ifCond)) return -1;
            int idx = ifCond.IndexOf(':');
            if (idx < 0) return -1;
            int.TryParse(ifCond.Substring(idx + 1), out int v);
            return v;
        }

        static string ParseEnumValueFromCond(string ifCond, string prefix)
        {
            if (string.IsNullOrEmpty(ifCond)) return null;
            if (!ifCond.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
            int idx = ifCond.IndexOf(':');
            return idx < 0 ? null : ifCond.Substring(idx + 1).Trim();
        }

        // ─────────────────────────────────────────────────────────────
        // 조건 평가
        // ─────────────────────────────────────────────────────────────

        bool EvaluateCondition(string ifCond, CardInstance card, ref ResolveResult result)
        {
            if (string.IsNullOrEmpty(ifCond)) return true;
            string c = ifCond.Trim();
            string up = c.ToUpperInvariant();

            if (up == "PLAYER_HP_FULL")
                return Ctx.player != null && Ctx.player.currentHp >= Ctx.player.maxHp;

            if (up.StartsWith("PLAYER_HP_RATE_LTE:"))
            {
                int.TryParse(up.Substring("PLAYER_HP_RATE_LTE:".Length), out int pct);
                if (Ctx.player == null) return false;
                float rate = (float)Ctx.player.currentHp / Mathf.Max(1, Ctx.player.maxHp) * 100f;
                return rate <= pct;
            }

            if (up.StartsWith("ENEMY_STATUS_GTE:"))
            {
                // ENEMY_STATUS_GTE:BURN:10
                var rest = c.Substring("ENEMY_STATUS_GTE:".Length);
                int colon = rest.IndexOf(':');
                if (colon < 0) return false;
                string key = rest.Substring(0, colon).Trim().ToLowerInvariant();
                int.TryParse(rest.Substring(colon + 1), out int thresh);
                return GetEnemyStatus(key) >= thresh;
            }

            if (up.StartsWith("PLAYER_BLOCK_EQ:"))
            {
                int.TryParse(up.Substring("PLAYER_BLOCK_EQ:".Length), out int v);
                if (Ctx.player == null) return false;
                return Mathf.RoundToInt(Ctx.player.guard) == v;
            }

            if (up.StartsWith("CARD_ELEMENT_EQ:"))
            {
                string el = up.Substring("CARD_ELEMENT_EQ:".Length).Trim();
                return ElementMatches(card.Element, el);
            }

            if (up.StartsWith("PREVIOUS_CARD_TYPE_EQ:"))
            {
                string t = up.Substring("PREVIOUS_CARD_TYPE_EQ:".Length).Trim();
                if (!Ctx.previousCardType.HasValue) return false;
                return string.Equals(Ctx.previousCardType.Value.ToString(), t, StringComparison.OrdinalIgnoreCase);
            }

            if (up == "FIRST_CARD_AFTER_ENEMY_ATTACK")
                return Ctx.firstCardAfterEnemyAttack;

            if (up == "USED_IMMEDIATELY_AFTER_DRAW")
                return card != null && card.gaugeSinceDrawn == 0;

            if (up == "LAST_USED_CARD_EXISTS_EXCLUDING_SELF")
                return Battle.NewBattleController.Instance != null
                    && Battle.NewBattleController.Instance.LastResolvedCard != null
                    && Battle.NewBattleController.Instance.LastResolvedCard != card;

            if (up.StartsWith("EXCLUDE_TRIGGER_SOURCE:")) return true; // 무한 트리거 방지(데이터 흐름에서 처리)
            if (up == "DAMAGE_SOURCE_IS_CARD_EFFECT") return true;     // 현재 모든 카드 효과 피해는 카드 출처

            Debug.LogWarning($"[CardEffect] 알 수 없는 조건 '{ifCond}'");
            return true; // 모르는 조건은 통과(보수적)
        }

        static bool ElementMatches(CardElement el, string s)
        {
            switch (s)
            {
                case "FIRE":     return el == CardElement.Fire;
                case "WATER":    return el == CardElement.Water;
                case "WIND":     return el == CardElement.Wind;
                case "EARTH":    return el == CardElement.Earth;
                case "FRAGMENT": return el == CardElement.Fragment;
                case "NEUTRAL":  return el == CardElement.Neutral;
                default: return false;
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 공식 평가
        // ─────────────────────────────────────────────────────────────

        int EvalFormula(string formula, ref bool multiplyBy2)
        {
            multiplyBy2 = false;
            if (string.IsNullOrEmpty(formula)) return 0;
            string f = formula.Trim();
            string up = f.ToUpperInvariant();

            if (up.StartsWith("ENEMY_STATUS_VALUE:"))
            {
                string key = f.Substring("ENEMY_STATUS_VALUE:".Length).Trim().ToLowerInvariant();
                return GetEnemyStatus(key);
            }

            if (up == "PLAYER_BLOCK")
                return Ctx.player != null ? Mathf.RoundToInt(Ctx.player.guard) : 0;

            if (up == "FLOOR(PLAYER_CURRENT_HP/2)")
                return Ctx.player != null ? Ctx.player.currentHp / 2 : 0;

            if (up.StartsWith("LAST_HP_LOST"))
            {
                // LAST_HP_LOST 또는 LAST_HP_LOST*2
                int v = Ctx.lastHpLost;
                int starIdx = up.IndexOf('*');
                if (starIdx > 0)
                {
                    int.TryParse(up.Substring(starIdx + 1), out int mul);
                    if (mul > 0) v *= mul;
                }
                return v;
            }

            if (up == "GAUGE_SINCE_DRAWN")
            {
                // 현재 카드 기준 (Resolve 인자가 필요하므로 별도 처리 — 호출자가 amount로 직접 넣어주는 게 깔끔)
                // 여기서는 0 반환. 호출 측에서 card.gaugeSinceDrawn 사용.
                return 0;
            }

            if (up == "USED_ATTACK_CARD_COUNT") return Ctx.usedAttackCardCount;
            if (up == "USED_FRAGMENT_CARD_COUNT") return Ctx.usedFragmentCardCount;
            if (up == "LAST_DISCARDED_COUNT") return Ctx.lastDiscardedCount;

            if (up == "IF_PLAYER_HP_RATE_LT_50_MULTIPLY_2")
            {
                // 공식이라기보다 amount에 곱하는 modifier — 호출 측에서 다룬다.
                if (Ctx.player != null)
                {
                    float rate = (float)Ctx.player.currentHp / Mathf.Max(1, Ctx.player.maxHp) * 100f;
                    if (rate < 50f) multiplyBy2 = true;
                }
                return -1; // sentinel — amount × 2 적용 신호
            }

            Debug.LogWarning($"[CardEffect] 알 수 없는 Formula '{formula}'");
            return 0;
        }

        int EvalHitFormula(string formula, CardInstance card)
        {
            if (string.IsNullOrEmpty(formula)) return 0;
            string f = formula.Trim();
            if (f.StartsWith("USED_CARD_NAME_COUNT:", StringComparison.OrdinalIgnoreCase))
            {
                string name = f.Substring("USED_CARD_NAME_COUNT:".Length).Trim();
                Ctx.usedCardNameCounts.TryGetValue(name, out int cnt);
                // 이 카드 자체 사용 후 카운트가 증가하는데, 우리는 사용 직전에 평가하므로 +1
                if (card != null && card.data.displayName == name) cnt += 1;
                return cnt;
            }
            Debug.LogWarning($"[CardEffect] 알 수 없는 HitFormula '{formula}'");
            return 0;
        }

        // ─────────────────────────────────────────────────────────────
        // Do 동사 실행
        // ─────────────────────────────────────────────────────────────

        void ApplyAction(CardEffectData eff, CardInstance card, ref ResolveResult result)
        {
            string verb = (eff.doAction ?? "").Trim().ToUpperInvariant();

            // amount 계산 — Formula 우선
            int amount = eff.amount;
            bool multiplyBy2 = false;
            if (!string.IsNullOrEmpty(eff.formula))
            {
                int formulaVal = EvalFormula(eff.formula, ref multiplyBy2);
                if (formulaVal == -1 && multiplyBy2)
                {
                    amount = amount * 2;
                }
                else if (eff.formula.Trim().Equals("GAUGE_SINCE_DRAWN", StringComparison.OrdinalIgnoreCase))
                {
                    amount = card.gaugeSinceDrawn;
                }
                else
                {
                    amount = formulaVal;
                }
            }

            // hits 계산 — HitFormula 우선
            int hits = eff.hits > 0 ? eff.hits : 1;
            if (!string.IsNullOrEmpty(eff.hitFormula))
            {
                int hf = EvalHitFormula(eff.hitFormula, card);
                if (hf > 0) hits = hf;
            }

            switch (verb)
            {
                case "DAMAGE":
                    if (amount > 0)
                    {
                        int actualHits = Mathf.Max(1, hits);
                        // Repeat 처리(바람10/309): 자원을 모두 소모할 때까지 반복.
                        if (!string.IsNullOrEmpty(eff.repeat))
                        {
                            string repeat = eff.repeat.Trim().ToUpperInvariant();
                            if (repeat == "REPEAT_BY_RESOURCE_CONSUME_ALL"
                                && ExtraSubstring(eff.extra, "Resource").ToUpperInvariant() == "CHAIN")
                            {
                                int consumePer = ExtraIntOrDefault(eff.extra, "ConsumePerRepeat", 1);
                                int safety = 200;
                                // 최초 1회 + 연쇄가 0이 될 때까지 반복
                                do
                                {
                                    result.totalDamage += DealDamage(amount, isAttackCard: true, baseHits: actualHits);
                                    if (Ctx.chainCount > 0) Ctx.chainCount -= consumePer;
                                    safety--;
                                } while (Ctx.chainCount > 0 && safety > 0);
                                if (Ctx.chainCount < 0) Ctx.chainCount = 0;
                            }
                            else
                            {
                                result.totalDamage += DealDamage(amount, isAttackCard: true, baseHits: actualHits);
                            }
                        }
                        else
                        {
                            result.totalDamage += DealDamage(amount, isAttackCard: true, baseHits: actualHits);
                        }
                    }
                    break;

                case "GAIN_BLOCK":
                    if (amount > 0) AddPlayerGuard(amount);
                    break;

                case "HEAL":
                    if (amount > 0) HealPlayer(amount);
                    break;

                case "LOSE_HP":
                    if (amount > 0 && Ctx.player != null)
                    {
                        int before = Ctx.player.currentHp;
                        Ctx.player.TakeDamage(amount, "self_loss");
                        Ctx.lastHpLost = Mathf.Max(0, before - Ctx.player.currentHp);
                    }
                    break;

                case "DRAW_CARD":
                    if (amount > 0 && Ctx.deck != null) Ctx.deck.Draw(amount);
                    break;

                case "DISCARD_HAND":
                    if (Ctx.deck != null)
                    {
                        int d = Ctx.deck.DiscardAllFromHand();
                        Ctx.lastDiscardedCount = d;
                        if (Ctx.discardToDrawActive && d > 0) Ctx.deck.Draw(d);
                    }
                    break;

                case "EXHAUST_HAND":
                    if (Ctx.deck != null)
                    {
                        // 패 전체를 소멸 더미로 (자기 자신은 이미 PullFromHand로 빠져 있음)
                        var snapshot = new List<CardInstance>(Ctx.deck.Hand);
                        foreach (var c in snapshot)
                        {
                            Ctx.deck.ExileFromHand(c);
                            NotifyExile(c);
                        }
                    }
                    break;

                case "DISCARD_CARD":
                    {
                        string sel = (eff.select ?? "").Trim().ToUpperInvariant();
                        if (sel == "SELECT_ONE_FROM_HAND")
                            result.requiresHandDiscardSelection = true;
                        // 그 외 케이스(현재 없음)는 무시
                    }
                    break;

                case "EXHAUST_CARD":
                    {
                        string sel = (eff.select ?? "").Trim().ToUpperInvariant();
                        if (sel == "SELECT_ONE_FROM_HAND")
                            result.requiresHandExileSelection = true;
                    }
                    break;

                case "APPLY_STATUS":
                    {
                        string st = (eff.status ?? "").Trim().ToLowerInvariant();
                        if (amount > 0 && !string.IsNullOrEmpty(st))
                            AddEnemyStatus(st, amount, isCardEffect: true);
                    }
                    break;

                case "GAIN_STATUS":
                    {
                        string st = (eff.status ?? "").Trim().ToUpperInvariant();
                        if (amount > 0)
                        {
                            if (st == "CHAIN") Ctx.chainCount += amount;
                            else if (st == "ATTACK_POWER") Ctx.attackPowerBonus += amount;
                            else if (st == "CHAIN_DAMAGE_BONUS") Ctx.chainBonusDamage += amount;
                            // 그 외 GAIN_STATUS는 플레이어 상태이상으로
                            else if (Ctx.player != null) Ctx.player.AddStatus(st.ToLowerInvariant(), amount);
                        }
                    }
                    break;

                case "GAIN_STAT":
                    {
                        string st = (eff.status ?? "").Trim().ToUpperInvariant();
                        if (st == "ATTACK_POWER") Ctx.attackPowerBonus += amount;
                        else if (st == "CHAIN_DAMAGE_BONUS") Ctx.chainBonusDamage += amount;
                    }
                    break;

                case "CLEANSE_STATUS":
                    {
                        string st = (eff.status ?? "").Trim().ToUpperInvariant();
                        if (st == "CURSE")
                        {
                            // 저주 해제 — NewBattleController가 관리
                            if (Battle.NewBattleController.Instance != null)
                                Battle.NewBattleController.Instance.ClearAllCurses();
                            // 물20(219) 트리거
                            if (Ctx.healOnCurseCleanseActive && Ctx.healOnCurseCleanseAmount > 0)
                                HealPlayer(Ctx.healOnCurseCleanseAmount);
                        }
                    }
                    break;

                case "SET_NEXT_CARD_FREE":
                    result.skipNextGaugeCost = true;
                    Ctx.nextCardIsFree = true;
                    break;

                case "SET_GAUGE_COST":
                    if (amount == 0) result.currentCardFreeThisUse = true;
                    break;

                case "MODIFY_HAND_LIMIT":
                    Ctx.handLimitBonus += amount; // 즉시 적용형(현재 캐스트 시점)
                    break;

                case "ADD_CARD":
                    HandleAddCard(eff, ref result);
                    break;

                case "MOVE_CARD":
                    HandleMoveCard(eff, ref result);
                    break;

                case "COPY_CARD":
                    HandleCopyCard(eff, card, ref result);
                    break;

                case "COPY_LAST_CARD_EFFECT":
                    result.recastLastCard = true;
                    break;

                case "NO_EFFECT":
                    // 명시적 효과 없음 (500 카드)
                    break;

                case "MODIFY_DAMAGE_MULTIPLIER":
                case "MODIFY_HIT_COUNT":
                    // 파워 카드용 — RegisterPowerTrigger에서 처리. ON_USE로 와도 무시.
                    break;

                default:
                    if (!string.IsNullOrEmpty(verb))
                        Debug.LogWarning($"[CardEffect] 미구현 동사 '{verb}' (cardId={eff.cardId})");
                    break;
            }
        }

        // ─── ADD_CARD / MOVE_CARD / COPY_CARD ────────────────────

        void HandleAddCard(CardEffectData eff, ref ResolveResult result)
        {
            if (Ctx.deck == null) return;
            string filter = (eff.cardFilter ?? "").Trim().ToUpperInvariant();
            string toZone = (eff.toZone ?? "").Trim().ToUpperInvariant();
            string sel = (eff.select ?? "").Trim().ToUpperInvariant();
            int amount = Mathf.Max(1, eff.amount);

            if (filter == "RANDOM_FRAGMENT_CARD")
            {
                for (int i = 0; i < amount; i++)
                {
                    var c = CardDatabase.CreateFragmentInstance();
                    if (c == null) continue;
                    PlaceCardInZone(c, toZone);
                }
            }
            else if (filter == "DIFFERENT_FRAGMENT_CARDS" || sel == "ALL_DIFFERENT")
            {
                var ids = new List<int>(CardDatabase.FragmentIds);
                ShuffleList(ids);
                int n = Mathf.Min(amount, ids.Count);
                for (int i = 0; i < n; i++)
                {
                    var c = CardDatabase.CreateFragmentInstance(ids[i]);
                    if (c == null) continue;
                    PlaceCardInZone(c, toZone);
                }
            }
            else if (filter == "EACH_FRAGMENT_TYPE" || sel == "EACH_TYPE_ONCE")
            {
                foreach (var id in CardDatabase.FragmentIds)
                {
                    var c = CardDatabase.CreateFragmentInstance(id);
                    if (c == null) continue;
                    PlaceCardInZone(c, toZone);
                }
            }
            else if (filter == "FRAGMENT_CARD" && sel == "SELECT_ONE_FROM_FRAGMENT_POOL")
            {
                // 플레이어 선택 — 외부에 위임
                result.requiresFragmentPoolSelection = true;
            }
            else
            {
                Debug.LogWarning($"[CardEffect] ADD_CARD 미지원 필터/선택 (filter={filter}, sel={sel}, cardId={eff.cardId})");
            }
        }

        void PlaceCardInZone(CardInstance card, string zone)
        {
            if (card == null || Ctx.deck == null) return;
            switch (zone)
            {
                case "DISCARD_PILE":
                case "DISCARD_PILE_SHUFFLE":
                    Ctx.deck.AddToDiscard(card);
                    break;
                case "DRAW_PILE":
                case "DRAW_PILE_SHUFFLE":
                    Ctx.deck.AddToDrawShuffled(card);
                    break;
                case "HAND":
                    if (!Ctx.deck.AddToHand(card))
                        Ctx.deck.AddToDiscard(card);
                    break;
                default:
                    Ctx.deck.AddToDiscard(card);
                    break;
            }
        }

        void HandleMoveCard(CardEffectData eff, ref ResolveResult result)
        {
            string sel = (eff.select ?? "").Trim().ToUpperInvariant();
            if (sel == "SELECT_ONE_FROM_DRAW_PILE")
                result.requiresDrawPileMoveSelection = true;
            else if (sel == "SELECT_ONE_FROM_DISCARD")
                result.requiresDiscardMoveSelection = true;
            else
                Debug.LogWarning($"[CardEffect] MOVE_CARD 미지원 (sel={sel}, cardId={eff.cardId})");
        }

        void HandleCopyCard(CardEffectData eff, CardInstance card, ref ResolveResult result)
        {
            string sel = (eff.select ?? "").Trim().ToUpperInvariant();
            string from = (eff.fromZone ?? "").Trim().ToUpperInvariant();
            string to = (eff.toZone ?? "").Trim().ToUpperInvariant();

            if (sel == "SELF" && from == "SELF_CARD")
            {
                // 물5(204): 자기 자신 복사를 버린 더미로
                var copy = new CardInstance(card.data, transient: true);
                Ctx.deck?.AddToDiscard(copy);
                return;
            }
            if (sel == "SELECT_ONE_FROM_HAND")
            {
                result.requiresHandCopySelection = true;
                return;
            }
            Debug.LogWarning($"[CardEffect] COPY_CARD 미지원 (sel={sel}, from={from}, cardId={eff.cardId})");
        }

        // ─────────────────────────────────────────────────────────────
        // 효과 헬퍼
        // ─────────────────────────────────────────────────────────────

        int DealDamage(int amount, bool isAttackCard, int baseHits = 1)
        {
            if (Ctx.enemy == null || amount <= 0) return 0;

            int hits = baseHits;
            if (isAttackCard && Ctx.attackHitCountBonusActive)
                hits += Ctx.attackHitCountBonus;

            int dmg = amount + (isAttackCard ? Ctx.attackPowerBonus : 0);

            // 불21(120): 체력 25% 이하 피해 배율
            if (isAttackCard && Ctx.damageMultiplierActive > 1 && Ctx.player != null)
            {
                float rate = (float)Ctx.player.currentHp / Mathf.Max(1, Ctx.player.maxHp) * 100f;
                if (rate <= Ctx.damageMultiplierThresholdHp)
                    dmg *= Ctx.damageMultiplierActive;
            }

            int total = 0;
            for (int i = 0; i < hits; i++)
            {
                Ctx.enemy.TakeDamage(dmg);
                total += dmg;

                if (Ctx.burnOnHitActive && Ctx.burnOnHitAmount > 0)
                    AddEnemyStatus("burn", Ctx.burnOnHitAmount, isCardEffect: true);

                // 연쇄 발동 (이전 누적분만)
                if (isAttackCard) total += TriggerChain();

                // 연쇄 획득 (다음 공격부터)
                if (Ctx.chainGainOnHitActive) Ctx.chainCount++;
            }
            return total;
        }

        int TriggerChain()
        {
            if (Ctx.chainCount <= 0) return 0;
            Ctx.chainCount--;
            int chainDmg = 1 + Ctx.chainBonusDamage;
            Ctx.enemy.TakeDamage(chainDmg);
            Debug.Log($"[연쇄] 발동 — 추가 피해 {chainDmg}, 남은 연쇄 {Ctx.chainCount}");
            return chainDmg;
        }

        void HealPlayer(int amount)
        {
            if (Ctx.player == null || amount <= 0) return;
            int before = Ctx.player.currentHp;
            Ctx.player.Heal(amount);
            int healed = Ctx.player.currentHp - before;
            if (healed > 0 && Ctx.healToDrawActive && Ctx.deck != null)
                Ctx.deck.Draw(1);
        }

        void AddEnemyStatus(string key, int amount, bool isCardEffect)
        {
            if (Ctx.enemy == null || amount == 0) return;
            Ctx.enemy.AddStatus(key, amount);

            // 불20(119): 카드 효과로 화상 부여 시 추가 피해
            if (isCardEffect && key == "burn" && Ctx.burnOnByCardActive && Ctx.burnOnByCardAmount > 0)
                Ctx.enemy.TakeDamage(Ctx.burnOnByCardAmount);
        }

        int GetEnemyStatus(string key)
        {
            if (Ctx.enemyStat == null || string.IsNullOrEmpty(key)) return 0;
            return Ctx.enemyStat.statusEffects.TryGetValue(key, out int v) ? v : 0;
        }

        void AddPlayerGuard(int amount)
        {
            if (Ctx.player == null) return;
            Ctx.player.AddGuard(amount);
        }

        static void ShuffleList<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
