using System;
using System.Collections.Generic;
using UnityEngine;
using Battle;
using Battle.Card;

namespace Battle.Deck
{
    // 카드 효과 실행 컨텍스트(카드 사용 시 주입되는 런타임 상태)
    public class CardEffectContext
    {
        public Player player;              // 플레이어
        public EnemyController enemy;      // 적 컨트롤러
        public EnemyStat enemyStat;        // 적 스탯
        public CardDeckSystem deck;        // 덱 시스템
        public CardEffectResolver resolver; // 효과 리졸버 참조

        public bool doubleBasicCardEffects; // 유물(태초의 서 10004): 기본 카드 효과 수치 2배

        public bool bonusDamagePerCardActive;     // 카드 사용 시 추가 피해 활성
        public int  bonusDamagePerCard;            // 카드 사용 시 추가 피해량
        public bool attackHitCountBonusActive;    // 공격 타격 횟수 보너스 활성
        public int  attackHitCountBonus;           // 공격 타격 횟수 보너스
        public bool burnOnHitActive;              // 피해 시 화상 부여 활성
        public int  burnOnHitAmount;               // 피해 시 화상 부여량
        public bool burnOnExileActive;            // 카드 소멸 시 화상 부여 활성
        public int  burnOnExileAmount;             // 카드 소멸 시 화상 부여량
        public bool fragmentOnEarthUseActive;     // 땅 카드 사용 시 파편 추가 활성
        public bool fragmentOnFragmentUseActive;  // 파편 카드 사용 시 파편 추가 활성
        public bool blockOnCardUseActive;         // 카드 사용 시 방어도 활성
        public int  blockOnCardUseAmount;          // 카드 사용 시 방어도량
        public bool burnOnByCardActive;           // 카드 효과로 화상 부여 시 피해 활성
        public int  burnOnByCardAmount;            // 카드 효과로 화상 부여 시 피해량
        public bool fragmentOnBlockConsumeActive; // 방어도 소모 시 파편 추가 활성
        public bool fragmentOnFragmentUse2Active; // 파편 사용 시 파편 추가(중복) 활성

        public bool healToDrawActive;             // 회복 시 드로우 활성
        public bool discardToDrawActive;          // 카드 버릴 때 드로우 활성
        public bool waterUseHealActive;           // 물 카드 사용 시 회복 활성
        public bool healOnCurseCleanseActive;     // 저주 해제 시 회복 활성
        public int  healOnCurseCleanseAmount;      // 저주 해제 시 회복량
        public bool healOnPlayerHitActive;        // 피격 시 회복 활성
        public bool chainGainOnHitActive;         // 피해 시 연쇄 획득 활성
        public int  chainCount;                    // 연쇄 누적량
        public int  chainBonusDamage;              // 연쇄 추가 피해량
        public int  attackPowerBonus;              // 공격력 보너스
        public int  damageMultiplierActive;        // 저체력 피해 배율(값=배율)
        public int  damageMultiplierThresholdHp;   // 배율 적용 임계 체력%

        public int lastHpLost;                    // 마지막으로 잃은 체력
        public int lastConsumedBurn;              // 마지막으로 소모한 화상량
        public int exhaustedCardCount;            // 이번 전투 소멸 카드 수
        public int consumedChainCount;            // 이번 전투 소모 연쇄 수
        public int lastHitEnemyCount;             // 직전 피해로 타격한 적 수
        public bool enemyKilledThisResolve;       // 이번 카드 처리 중 적 처치 여부
        public int lastDiscardedCount;            // 마지막으로 버린 카드 수
        public int usedAttackCardCount;           // 사용한 공격 카드 수
        public int usedFragmentCardCount;         // 사용한 파편 카드 수
        public CardType? previousCardType;        // 직전 사용 카드 유형
        public bool firstCardAfterEnemyAttack;    // 적 공격 직후 첫 카드 여부
        public bool nextCardIsFree;               // 다음 카드 게이지 무료 여부
        public bool currentCardJustDrawn;         // 현재 카드 드로우 직후 사용 여부
        public Dictionary<string, int> usedCardNameCounts = new Dictionary<string, int>(); // 카드 이름별 사용 횟수

        public int handLimitBonus;                // 손패 한도 보너스
        public bool firstCardAfterAttackFreeActive; // 적 공격 후 첫 카드 무료 등록 여부

        public bool chainGainOnCardUseActive;     // 카드 사용 시 연쇄 획득 활성
        public int  chainGainOnCardUseAmount;      // 카드 사용 시 연쇄 획득량
        public bool damageOnFragmentUseActive;    // 파편 사용 시 적 피해 활성
        public int  damageOnFragmentUseAmount;     // 파편 사용 시 적 피해량
        public bool frostOnWaterUseActive;        // 물 카드 사용 시 빙결 부여 활성
        public int  frostOnWaterUseAmount;         // 물 사용 시 빙결 부여량
        public int  frostOnWaterUseEveryN;         // 빙결 부여 주기(N회마다)
        public int  waterUseCounter;               // 물 카드 사용 카운터
        public bool attackPowerOnHpLossActive;    // 체력 잃을 때 공격력 증가 활성
        public int  attackPowerOnHpLossAmount;     // 체력 잃을 때 공격력 증가량
        public bool frostOnFrostByCardActive;     // 카드로 빙결 부여 시 추가 빙결 활성
        public int  frostOnFrostByCardAmount;      // 카드로 빙결 부여 시 추가 빙결량
        public bool blockOnEnemyAttackActive;     // 적 공격 종료 후 방어도 활성
        public int  blockOnEnemyAttackAmount;      // 적 공격 종료 후 방어도량
        public bool recastExhaustCardActive;      // 소멸 태그 카드 사용 시 효과 재발동 활성
        public bool burnSkillHitsAllActive;       // 화상 스킬 적 전체 대상화 활성
        public bool burnPersistsOnEnemyTurn;      // 적 행동 시 화상 유지 여부
        public int  awakenGaugeMaxDelta;           // 각성 게이지 최대치 가감
        bool _frostTriggerReentrancy;             // 빙결 트리거 재진입 가드
        public bool FrostTriggerReentrancy { get => _frostTriggerReentrancy; set => _frostTriggerReentrancy = value; } // 빙결 트리거 재진입 플래그
    }

    // 데이터 드리븐 카드 효과 엔진(JSON 정의를 해석/실행)
    public class CardEffectResolver
    {
        // 카드 효과 처리 결과(외부 위임 플래그 묶음)
        public struct ResolveResult
        {
            public bool exile;                        // 소멸 여부
            public bool keepOnField;                  // 파워 카드 필드 잔류 여부
            public int  totalDamage;                  // 총 피해량
            public bool requiresHandExileSelection;   // 패 소멸 선택 필요 여부
            public bool requiresHandDiscardSelection; // 패 버리기 선택 필요 여부
            public bool requiresHandCopySelection;    // 패 복사 선택 필요 여부
            public bool requiresDrawPileMoveSelection;// 뽑을 더미 이동 선택 필요 여부
            public bool requiresDiscardMoveSelection; // 버린 더미 이동 선택 필요 여부
            public bool requiresFragmentPoolSelection;// 파편 풀 선택 필요 여부
            public bool skipNextGaugeCost;            // 다음 카드 게이지 무료 여부
            public bool recastLastCard;               // 직전 카드 효과 재발동 여부
            public bool currentCardFreeThisUse;       // 이번 카드 게이지 무료 여부
            public bool poweredField;                 // 필드 잔류 동의어(호환용)
            public bool returnToHandInsteadOfDiscard; // 사용 후 패 복귀 여부

            public string handSelectionCardFilter;    // 패 선택 모드 카드 필터
            public string fragmentPickerTargetZone;    // 파편 선택 결과 도착 존
            public int fragmentPickerCopyCount;        // 파편 선택 복제 수량
        }

        public CardEffectContext Ctx { get; private set; } // 현재 효과 컨텍스트

        bool _triggeringOtherCards; // 다른 카드 효과 발동 재귀 가드
        bool _inLifecycleTrigger;   // 생애주기 트리거 재귀 가드

        // 효과 컨텍스트 바인딩
        public void Bind(CardEffectContext ctx)
        {
            Ctx = ctx;
            ctx.resolver = this;
        }

        // 효과 목록 조회 — 유물(태초의 서 10004) 보유 + 기본 카드면 수치(amount)를 2배로 복제해 반환
        IReadOnlyList<CardEffectData> GetResolveEffects(CardInstance card)
        {
            var effects = CardDatabase.GetEffects(card.Id);
            if (effects == null || effects.Count == 0) return effects;
            if (!Ctx.doubleBasicCardEffects || !CardDatabase.IsBasicCard(card.Id)) return effects;

            var doubled = new List<CardEffectData>(effects.Count);
            foreach (var e in effects)
            {
                doubled.Add(new CardEffectData
                {
                    cardId = e.cardId, index = e.index, when = e.when, ifCond = e.ifCond,
                    doAction = e.doAction, target = e.target,
                    amount = e.amount * 2,          // 기본 카드 효과 수치 2배
                    formula = e.formula, hits = e.hits, hitFormula = e.hitFormula,
                    status = e.status, cardFilter = e.cardFilter, fromZone = e.fromZone,
                    toZone = e.toZone, select = e.select, repeat = e.repeat,
                    extra = e.extra, runtimeKey = e.runtimeKey,
                });
            }
            return doubled;
        }

        // 카드 효과 해석/실행 메인 진입점
        public ResolveResult Resolve(CardInstance card)
        {
            var result = new ResolveResult();
            Ctx.enemyKilledThisResolve = false;

            if (card.cursed && Ctx.player != null)
            {
                Ctx.player.TakeDamage(6);
                Debug.Log($"[Curse] {card.data.displayName} 저주 발동 — 플레이어 6 피해");
                card.cursed = false;
            }

            bool isPower = card.Type == CardType.Power || card.data.HasTag("POWER");
            var effects = GetResolveEffects(card);
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

            if (card.data.HasTag("EXHAUST")) result.exile = true;

            if (isPower)
            {
                result.keepOnField = true;
                result.poweredField = true;
            }

            bool applyPowerEffects = !result.keepOnField;

            if (applyPowerEffects && Ctx.bonusDamagePerCardActive && Ctx.bonusDamagePerCard > 0)
                result.totalDamage += DealDamage(Ctx.bonusDamagePerCard, isAttackCard: false);

            if (applyPowerEffects && Ctx.blockOnCardUseActive && Ctx.blockOnCardUseAmount > 0)
                AddPlayerGuard(Ctx.blockOnCardUseAmount);

            if (applyPowerEffects && Ctx.fragmentOnEarthUseActive && card.Element == CardElement.Earth)
                Ctx.deck?.AddToDiscard(CardDatabase.CreateFragmentInstance());

            if (applyPowerEffects && Ctx.fragmentOnFragmentUseActive && card.Element == CardElement.Fragment)
                Ctx.deck?.AddToDiscard(CardDatabase.CreateFragmentInstance());

            if (applyPowerEffects && Ctx.waterUseHealActive && card.Element == CardElement.Water)
                HealPlayer(1);

            if (applyPowerEffects && Ctx.chainGainOnCardUseActive)
                Ctx.chainCount = Mathf.Min(999, Ctx.chainCount + Mathf.Max(1, Ctx.chainGainOnCardUseAmount));

            if (applyPowerEffects && Ctx.damageOnFragmentUseActive
                && card.Element == CardElement.Fragment && Ctx.damageOnFragmentUseAmount > 0)
                result.totalDamage += DealDamage(Ctx.damageOnFragmentUseAmount, isAttackCard: false);

            if (applyPowerEffects && Ctx.frostOnWaterUseActive && card.Element == CardElement.Water)
            {
                Ctx.waterUseCounter++;
                int everyN = Mathf.Max(1, Ctx.frostOnWaterUseEveryN);
                if (Ctx.waterUseCounter % everyN == 0)
                    AddEnemyStatus("frost", Mathf.Max(1, Ctx.frostOnWaterUseAmount), isCardEffect: true);
            }

            if (applyPowerEffects && Ctx.recastExhaustCardActive && card.data.HasTag("EXHAUST"))
            {
                var recast = new ResolveResult();
                var recastEffects = GetResolveEffects(card);
                if (recastEffects != null)
                {
                    foreach (var e2 in recastEffects)
                    {
                        if (!IsImmediateTrigger(e2.when)) continue;
                        if (!EvaluateCondition(e2.ifCond, card, ref recast)) continue;
                        ApplyAction(e2, card, ref recast);
                    }
                }
                result.totalDamage += recast.totalDamage;
            }

            return result;
        }

        // 카드 소멸 시 호출 — 소멸 트리거 효과 처리
        public void NotifyExile(CardInstance card)
        {
            if (Ctx.burnOnExileActive && Ctx.burnOnExileAmount > 0)
                AddEnemyStatus("burn", Ctx.burnOnExileAmount, isCardEffect: true);

            if (card == null) return;
            Ctx.exhaustedCardCount++;
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

        // 패에서 버려질 때 생애주기 트리거 처리
        public void NotifyCardDiscardedFromHand(CardInstance card)
        {
            RunWhenTriggers(card, "ON_SELF_DISCARDED");
            RunWhenTriggers(card, "ON_CARD_MOVED_TO_DISCARD");
        }

        // 사용 후 버린 더미로 이동할 때 생애주기 트리거 처리
        public void NotifyCardMovedToDiscard(CardInstance card)
        {
            RunWhenTriggers(card, "ON_CARD_MOVED_TO_DISCARD");
        }

        // 카드가 패에 들어올 때 생애주기 트리거 처리
        public void NotifyCardEnteredHand(CardInstance card)
        {
            RunWhenTriggers(card, "ON_CARD_RETURNED_TO_HAND");
        }

        // 지정 시점 키에 해당하는 효과들을 실행
        void RunWhenTriggers(CardInstance card, string whenKey)
        {
            if (card == null || _inLifecycleTrigger || Ctx == null) return;
            var effects = CardDatabase.GetEffects(card.Id);
            if (effects == null) return;
            _inLifecycleTrigger = true;
            try
            {
                var dummy = new ResolveResult();
                foreach (var e in effects)
                {
                    if ((e.when ?? "").Trim().ToUpperInvariant() != whenKey) continue;
                    if (!EvaluateCondition(e.ifCond, card, ref dummy)) continue;
                    ApplyAction(e, card, ref dummy);
                }
            }
            finally { _inLifecycleTrigger = false; }
        }

        // 주어진 카드들의 즉시 효과를 발동(카드는 이동시키지 않음)
        void TriggerOtherCards(List<CardInstance> cards, CardInstance source, string cardFilter)
        {
            if (_triggeringOtherCards || cards == null) return;
            _triggeringOtherCards = true;
            try
            {
                string filter = (cardFilter ?? "").Trim().ToUpperInvariant();
                foreach (var c in cards)
                {
                    if (c == null || c == source) continue;
                    if (!PassesTriggerFilter(c, filter)) continue;
                    RunImmediateEffects(c);
                }
            }
            finally { _triggeringOtherCards = false; }
        }

        // 뽑을 더미의 파편 카드를 모두 발동 후 버린 더미로 보냄
        void TriggerDrawPileFragments()
        {
            if (_triggeringOtherCards || Ctx.deck == null) return;
            _triggeringOtherCards = true;
            try
            {
                var frags = new List<CardInstance>();
                foreach (var c in Ctx.deck.DrawPile)
                    if (c != null && c.Element == CardElement.Fragment) frags.Add(c);
                foreach (var c in frags)
                {
                    RunImmediateEffects(c);
                    Ctx.deck.MoveFromDrawPileToDiscard(c);
                }
            }
            finally { _triggeringOtherCards = false; }
        }

        // 카드의 즉시 트리거 효과를 실행
        void RunImmediateEffects(CardInstance c)
        {
            var effects = CardDatabase.GetEffects(c.Id);
            if (effects == null) return;
            var dummy = new ResolveResult();
            foreach (var e in effects)
            {
                if (!IsImmediateTrigger(e.when)) continue;
                if (!EvaluateCondition(e.ifCond, c, ref dummy)) continue;
                ApplyAction(e, c, ref dummy);
            }
        }

        // 트리거 카드 필터 통과 여부 판정
        static bool PassesTriggerFilter(CardInstance c, string filter)
        {
            if (string.IsNullOrEmpty(filter)) return true;
            switch (filter)
            {
                case "FIRE_CARD":     return c.Element == CardElement.Fire;
                case "WATER_CARD":    return c.Element == CardElement.Water;
                case "WIND_CARD":     return c.Element == CardElement.Wind;
                case "EARTH_CARD":    return c.Element == CardElement.Earth;
                case "FRAGMENT_CARD": return c.Element == CardElement.Fragment;
                default: return true;
            }
        }

        // Extra 문자열에서 지정 키의 값 추출
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

        // Extra 문자열에서 지정 키의 정수 값 추출(없으면 기본값)
        static int ExtraIntOrDefault(string extra, string key, int def)
        {
            string s = ExtraSubstring(extra, key);
            if (string.IsNullOrEmpty(s)) return def;
            return int.TryParse(s, out int v) ? v : def;
        }

        // 즉시 발동 트리거 시점 여부 판정
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

        // 파워 카드의 지속 트리거를 컨텍스트에 등록
        void RegisterPowerTrigger(CardEffectData eff, CardInstance card)
        {
            string when = (eff.when ?? "").Trim().ToUpperInvariant();
            string verb = (eff.doAction ?? "").Trim().ToUpperInvariant();
            string status = (eff.status ?? "").Trim().ToUpperInvariant();
            int amount = eff.amount;

            switch (when)
            {
                case "ON_CARD_DAMAGE_HIT_DEALT":
                    if (verb == "APPLY_STATUS" && status == "BURN")
                    {
                        Ctx.burnOnHitActive = true;
                        Ctx.burnOnHitAmount += amount;
                    }
                    else if (verb == "GAIN_STATUS" && status == "CHAIN")
                    {
                        Ctx.chainGainOnHitActive = true;
                    }
                    break;

                case "ON_BURN_APPLIED_BY_CARD_EFFECT":
                    if (verb == "DAMAGE")
                    {
                        Ctx.burnOnByCardActive = true;
                        Ctx.burnOnByCardAmount += amount;
                    }
                    else if (verb == "MODIFY_STATUS_TARGET")
                    {
                        Ctx.burnSkillHitsAllActive = true;
                    }
                    break;

                case "ON_ATTACK_CARD_DAMAGE_CALC":
                    if (verb == "MODIFY_HIT_COUNT")
                    {
                        Ctx.attackHitCountBonusActive = true;
                        Ctx.attackHitCountBonus += amount;
                    }
                    else if (verb == "MODIFY_DAMAGE_MULTIPLIER")
                    {
                        Ctx.damageMultiplierActive = amount;
                        int hp = ParseRatioFromIfCond(eff.ifCond);
                        Ctx.damageMultiplierThresholdHp = hp > 0 ? hp : 25;
                    }
                    break;

                case "ON_CARD_USED":
                    if (verb == "DAMAGE")
                    {
                        Ctx.bonusDamagePerCardActive = true;
                        Ctx.bonusDamagePerCard += amount;
                    }
                    else if (verb == "GAIN_BLOCK")
                    {
                        Ctx.blockOnCardUseActive = true;
                        Ctx.blockOnCardUseAmount += amount;
                    }
                    else if (verb == "GAIN_STATUS" && status == "CHAIN")
                    {
                        Ctx.chainGainOnCardUseActive = true;
                        Ctx.chainGainOnCardUseAmount += Mathf.Max(1, amount);
                    }
                    else if (verb == "COPY_LAST_CARD_EFFECT")
                    {
                        Ctx.recastExhaustCardActive = true;
                    }
                    break;

                case "ON_ELEMENT_CARD_USED":
                    {
                        var elem = (ParseEnumValueFromCond(eff.ifCond, "CARD_ELEMENT_EQ") ?? "").ToUpperInvariant();
                        if (verb == "HEAL" && elem == "WATER")
                        {
                            Ctx.waterUseHealActive = true;
                        }
                        else if (verb == "ADD_CARD" && elem == "EARTH")
                        {
                            Ctx.fragmentOnEarthUseActive = true;
                        }
                        else if (verb == "APPLY_STATUS" && status == "FROST" && elem == "WATER")
                        {
                            Ctx.frostOnWaterUseActive = true;
                            Ctx.frostOnWaterUseAmount = Mathf.Max(1, amount);
                            Ctx.frostOnWaterUseEveryN = Mathf.Max(1, ExtraIntOrDefault(eff.extra, "EveryNthUse", 3));
                        }
                    }
                    break;

                case "ON_FRAGMENT_CARD_USED":
                    if (verb == "ADD_CARD")
                        Ctx.fragmentOnFragmentUseActive = true;
                    else if (verb == "DAMAGE")
                    {
                        Ctx.damageOnFragmentUseActive = true;
                        Ctx.damageOnFragmentUseAmount += amount;
                    }
                    break;

                case "ON_CARD_EXHAUSTED":
                    if (verb == "APPLY_STATUS" && status == "BURN")
                    {
                        Ctx.burnOnExileActive = true;
                        Ctx.burnOnExileAmount += amount;
                    }
                    break;

                case "ON_PLAYER_HEALED_ACTUAL":
                    if (verb == "DRAW_CARD") Ctx.healToDrawActive = true;
                    break;

                case "ON_PLAYER_HIT_INCLUDING_BLOCK":
                    if (verb == "HEAL") Ctx.healOnPlayerHitActive = true;
                    break;

                case "ON_CURSE_CLEANSED":
                    if (verb == "HEAL")
                    {
                        Ctx.healOnCurseCleanseActive = true;
                        Ctx.healOnCurseCleanseAmount += amount;
                    }
                    break;

                case "ON_CARD_DISCARDED_BY_CARD_EFFECT":
                    if (verb == "DRAW_CARD") Ctx.discardToDrawActive = true;
                    break;

                case "ON_BLOCK_CONSUMED_BY_ATTACK":
                    if (verb == "ADD_CARD") Ctx.fragmentOnBlockConsumeActive = true;
                    break;

                case "ON_CARD_USED_BEFORE_GAUGE":
                    if (verb == "SET_GAUGE_COST" && amount == 0)
                        Ctx.firstCardAfterAttackFreeActive = true;
                    break;

                case "ON_PLAYER_LOSE_HP_BY_CARD_EFFECT":
                    if (verb == "GAIN_STAT" || verb == "GAIN_STATUS")
                    {
                        Ctx.attackPowerOnHpLossActive = true;
                        Ctx.attackPowerOnHpLossAmount += Mathf.Max(1, amount);
                    }
                    break;

                case "ON_FROST_APPLIED_BY_CARD_EFFECT":
                    if (verb == "APPLY_STATUS")
                    {
                        Ctx.frostOnFrostByCardActive = true;
                        Ctx.frostOnFrostByCardAmount += Mathf.Max(1, amount);
                    }
                    break;

                case "ON_ENEMY_ATTACK_RESOLVED":
                    if (verb == "GAIN_BLOCK")
                    {
                        Ctx.blockOnEnemyAttackActive = true;
                        Ctx.blockOnEnemyAttackAmount += amount;
                    }
                    break;
            }

            if (verb == "GAIN_STAT")
            {
                string statKey = (eff.status ?? "").Trim().ToUpperInvariant();
                if (statKey == "ATTACK_POWER")
                {
                    Ctx.attackPowerBonus += amount;
                    if (IsPermanentExtra(eff.extra)) Battle.RunDeckState.Instance?.AddPermanentAttackPower(amount);
                }
                else if (statKey == "CHAIN_DAMAGE_BONUS") Ctx.chainBonusDamage += amount;
            }

            if (verb == "MODIFY_HAND_LIMIT")
            {
                Ctx.handLimitBonus += amount;
                Ctx.deck?.IncreaseHandLimit(amount);
            }
        }

        // ifCond 문자열에서 비율 수치를 파싱
        static int ParseRatioFromIfCond(string ifCond)
        {
            if (string.IsNullOrEmpty(ifCond)) return -1;
            int idx = ifCond.IndexOf(':');
            if (idx < 0) return -1;
            int.TryParse(ifCond.Substring(idx + 1), out int v);
            return v;
        }

        // ifCond 접두사 뒤의 enum 값 문자열을 파싱
        static string ParseEnumValueFromCond(string ifCond, string prefix)
        {
            if (string.IsNullOrEmpty(ifCond)) return null;
            if (!ifCond.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
            int idx = ifCond.IndexOf(':');
            return idx < 0 ? null : ifCond.Substring(idx + 1).Trim();
        }

        // 효과 발동 조건 평가
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
                return card != null && card.justDrawn;

            if (up == "LAST_USED_CARD_EXISTS_EXCLUDING_SELF")
                return Battle.NewBattleController.Instance != null
                    && Battle.NewBattleController.Instance.LastResolvedCard != null
                    && Battle.NewBattleController.Instance.LastResolvedCard != card;

            if (up.StartsWith("EXCLUDE_TRIGGER_SOURCE:")) return true;
            if (up == "DAMAGE_SOURCE_IS_CARD_EFFECT") return true;

            if (up == "ENEMY_KILLED_BY_THIS_CARD") return Ctx.enemyKilledThisResolve;

            if (up.StartsWith("USED_CARD_HAS_TAG:"))
            {
                string tag = c.Substring("USED_CARD_HAS_TAG:".Length).Trim();
                return card != null && card.data.HasTag(tag);
            }

            if (up.StartsWith("CARD_TYPE_EQ:"))
            {
                string t = up.Substring("CARD_TYPE_EQ:".Length).Trim();
                return card != null && string.Equals(card.Type.ToString(), t, StringComparison.OrdinalIgnoreCase);
            }

            Debug.LogWarning($"[CardEffect] 알 수 없는 조건 '{ifCond}'");
            return true;
        }

        // 카드 속성과 문자열 일치 여부 판정
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

        // 수치 공식 문자열을 평가
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
                int v = Ctx.lastHpLost;
                int starIdx = up.IndexOf('*');
                if (starIdx > 0)
                {
                    int.TryParse(up.Substring(starIdx + 1), out int mul);
                    if (mul > 0) v *= mul;
                }
                return v;
            }

            if (up.StartsWith("LAST_CONSUMED_BURN"))
            {
                int v = Ctx.lastConsumedBurn;
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
                return 0;
            }

            if (up == "USED_ATTACK_CARD_COUNT") return Ctx.usedAttackCardCount;
            if (up == "USED_FRAGMENT_CARD_COUNT") return Ctx.usedFragmentCardCount;
            if (up == "LAST_DISCARDED_COUNT") return Ctx.lastDiscardedCount;
            if (up == "EXHAUSTED_CARD_COUNT") return Ctx.exhaustedCardCount;
            if (up == "CONSUMED_CHAIN_COUNT") return Ctx.consumedChainCount;
            if (up == "HIT_ENEMY_COUNT") return Ctx.lastHitEnemyCount;

            if (up.StartsWith("ENEMY_COUNT"))
            {
                int v = (Ctx.enemy != null && Ctx.enemyStat != null && Ctx.enemyStat.IsAlive) ? 1 : 0;
                int starIdx = up.IndexOf('*');
                if (starIdx > 0)
                {
                    int.TryParse(up.Substring(starIdx + 1), out int mul);
                    if (mul > 0) v *= mul;
                }
                return v;
            }

            if (up == "IF_ENEMY_HAS_FROST_MULTIPLY_2")
            {
                if (GetEnemyStatus("frost") > 0) multiplyBy2 = true;
                return -1;
            }

            if (up == "IF_PLAYER_HP_RATE_LT_50_MULTIPLY_2")
            {
                if (Ctx.player != null)
                {
                    float rate = (float)Ctx.player.currentHp / Mathf.Max(1, Ctx.player.maxHp) * 100f;
                    if (rate < 50f) multiplyBy2 = true;
                }
                return -1;
            }

            Debug.LogWarning($"[CardEffect] 알 수 없는 Formula '{formula}'");
            return 0;
        }

        // 타격 횟수 공식 문자열을 평가
        int EvalHitFormula(string formula, CardInstance card)
        {
            if (string.IsNullOrEmpty(formula)) return 0;
            string f = formula.Trim();
            string up = f.ToUpperInvariant();

            if (f.StartsWith("USED_CARD_NAME_COUNT:", StringComparison.OrdinalIgnoreCase))
            {
                string name = f.Substring("USED_CARD_NAME_COUNT:".Length).Trim();
                Ctx.usedCardNameCounts.TryGetValue(name, out int cnt);
                if (card != null && card.data.displayName == name) cnt += 1;
                return cnt;
            }

            if (up == "USED_ATTACK_CARD_COUNT")
                return Ctx.usedAttackCardCount + (card != null && card.Type == CardType.Attack ? 1 : 0);

            if (up == "USED_SAME_NAME_CARD_COUNT")
            {
                int cnt = 0;
                if (card != null) Ctx.usedCardNameCounts.TryGetValue(card.data.displayName ?? "", out cnt);
                return cnt + 1;
            }

            if (up == "DRAW_PILE_FRAGMENT_COUNT")
            {
                if (Ctx.deck == null) return 0;
                int cnt = 0;
                foreach (var c in Ctx.deck.DrawPile)
                    if (c != null && c.Element == CardElement.Fragment) cnt++;
                return cnt;
            }

            Debug.LogWarning($"[CardEffect] 알 수 없는 HitFormula '{formula}'");
            return 0;
        }

        // 효과 동사를 해석해 실제 동작을 실행
        void ApplyAction(CardEffectData eff, CardInstance card, ref ResolveResult result)
        {
            string verb = (eff.doAction ?? "").Trim().ToUpperInvariant();

            int amount = eff.amount;
            bool multiplyBy2 = false;
            if (!string.IsNullOrEmpty(eff.formula))
            {
                int formulaVal = EvalFormula(eff.formula, ref multiplyBy2);
                if (formulaVal == -1)
                {
                    if (multiplyBy2) amount = eff.amount * 2;
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

            int hits = eff.hits > 0 ? eff.hits : 1;
            if (!string.IsNullOrEmpty(eff.hitFormula))
            {
                int hf = EvalHitFormula(eff.hitFormula, card);
                if (hf > 0) hits = hf;
            }

            amount = ScaleBasicCardEffectAmount(card, verb, amount);

            switch (verb)
            {
                case "DAMAGE":
                    if (amount > 0)
                    {
                        int actualHits = Mathf.Max(1, hits);
                        if (!string.IsNullOrEmpty(eff.repeat))
                        {
                            string repeat = eff.repeat.Trim().ToUpperInvariant();
                            if (repeat == "REPEAT_BY_RESOURCE_CONSUME_ALL"
                                && ExtraSubstring(eff.extra, "Resource").ToUpperInvariant() == "CHAIN")
                            {
                                int consumePer = ExtraIntOrDefault(eff.extra, "ConsumePerRepeat", 1);
                                int safety = 200;
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
                        if (Ctx.lastHpLost > 0 && Ctx.attackPowerOnHpLossActive)
                            Ctx.attackPowerBonus += Ctx.attackPowerOnHpLossAmount;
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
                        {
                            result.requiresHandDiscardSelection = true;
                            result.handSelectionCardFilter = eff.cardFilter ?? "";
                        }
                    }
                    break;

                case "EXHAUST_CARD":
                    {
                        string sel = (eff.select ?? "").Trim().ToUpperInvariant();
                        string tgt = (eff.target ?? "").Trim().ToUpperInvariant();
                        if (sel == "SELECT_ONE_FROM_HAND")
                        {
                            result.requiresHandExileSelection = true;
                            result.handSelectionCardFilter = eff.cardFilter ?? "";
                        }
                        else if (sel == "RANDOM" && Ctx.deck != null && Ctx.deck.Hand.Count > 0)
                        {
                            var hand = Ctx.deck.Hand;
                            var victim = hand[UnityEngine.Random.Range(0, hand.Count)];
                            Ctx.deck.ExileFromHand(victim);
                            NotifyExile(victim);
                        }
                        else if (sel == "ALL" && Ctx.deck != null)
                        {
                            if (tgt == "DRAW_PILE") Ctx.deck.ExileWholeDrawPile();
                            else if (tgt == "DISCARD_PILE") Ctx.deck.ExileWholeDiscardPile();
                        }
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
                            if (st == "CHAIN") Ctx.chainCount = Mathf.Min(999, Ctx.chainCount + amount);
                            else if (st == "ATTACK_POWER") Ctx.attackPowerBonus += amount;
                            else if (st == "CHAIN_DAMAGE_BONUS") Ctx.chainBonusDamage += amount;
                            else if (Ctx.player != null) Ctx.player.AddStatus(st.ToLowerInvariant(), amount);
                        }
                    }
                    break;

                case "GAIN_STAT":
                    {
                        string st = (eff.status ?? "").Trim().ToUpperInvariant();
                        if (st == "ATTACK_POWER")
                        {
                            Ctx.attackPowerBonus += amount;
                            if (IsPermanentExtra(eff.extra)) Battle.RunDeckState.Instance?.AddPermanentAttackPower(amount);
                        }
                        else if (st == "CHAIN_DAMAGE_BONUS") Ctx.chainBonusDamage += amount;
                    }
                    break;

                case "CLEANSE_STATUS":
                    {
                        string st = (eff.status ?? "").Trim().ToUpperInvariant();
                        if (st == "CURSE")
                        {
                            if (Battle.NewBattleController.Instance != null)
                                Battle.NewBattleController.Instance.ClearAllCurses();
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
                    Ctx.handLimitBonus += amount;
                    Ctx.deck?.IncreaseHandLimit(amount);
                    break;

                case "ADD_CARD":
                    HandleAddCard(eff, amount, ref result);
                    break;

                case "MOVE_CARD":
                    HandleMoveCard(eff, card, ref result);
                    break;

                case "COPY_CARD":
                    HandleCopyCard(eff, card, ref result);
                    break;

                case "COPY_LAST_CARD_EFFECT":
                    result.recastLastCard = true;
                    break;

                case "NO_EFFECT":
                    break;

                case "MODIFY_DAMAGE_MULTIPLIER":
                case "MODIFY_HIT_COUNT":
                    break;

                case "MODIFY_AWAKEN_GAUGE_MAX":
                    Ctx.awakenGaugeMaxDelta += amount;
                    break;

                case "MODIFY_STATUS_RULE":
                    if ((eff.status ?? "").Trim().ToUpperInvariant() == "BURN")
                        Ctx.burnPersistsOnEnemyTurn = true;
                    break;

                case "MODIFY_STATUS_TARGET":
                    if ((eff.status ?? "").Trim().ToUpperInvariant() == "BURN")
                        Ctx.burnSkillHitsAllActive = true;
                    break;

                case "CONSUME_STATUS":
                    {
                        string st = (eff.status ?? "").Trim().ToLowerInvariant();
                        int consumed = GetEnemyStatus(st);
                        if (consumed > 0 && Ctx.enemy != null) Ctx.enemy.SetStatus(st, 0);
                        if (st == "burn") Ctx.lastConsumedBurn = consumed;
                    }
                    break;

                case "DOUBLE_STATUS":
                    {
                        string st = (eff.status ?? "").Trim().ToLowerInvariant();
                        int cur = GetEnemyStatus(st);
                        if (cur > 0 && Ctx.enemy != null) Ctx.enemy.SetStatus(st, cur * 2);
                    }
                    break;

                case "INSTANT_KILL":
                    if (Ctx.enemy != null && Ctx.enemyStat != null)
                    {
                        bool exclude = ExtraSubstring(eff.extra, "ExcludeBossElite")
                                           .Equals("true", StringComparison.OrdinalIgnoreCase)
                                       && Ctx.enemyStat.IsBossOrElite;
                        if (!exclude)
                            Ctx.enemy.TakeDamage(Ctx.enemyStat.currentHp);
                    }
                    break;

                case "TRANSFORM_STATUS_TO_BLOCK":
                    {
                        string st = (eff.status ?? "").Trim().ToUpperInvariant();
                        if (st == "CHAIN" && Ctx.chainCount > 0)
                        {
                            AddPlayerGuard(Ctx.chainCount);
                            Ctx.chainCount = 0;
                        }
                    }
                    break;

                case "TRIGGER_HAND_CARDS":
                    if (Ctx.deck != null)
                        TriggerOtherCards(new List<CardInstance>(Ctx.deck.Hand), card, eff.cardFilter);
                    break;

                case "TRIGGER_DRAW_PILE_CARDS":
                    if (Ctx.deck != null)
                        TriggerDrawPileFragments();
                    break;

                default:
                    if (!string.IsNullOrEmpty(verb))
                        Debug.LogWarning($"[CardEffect] 미구현 동사 '{verb}' (cardId={eff.cardId})");
                    break;
            }
        }

        // ADD_CARD 동사 처리(필터/선택 모드에 따라 카드 추가)
        void HandleAddCard(CardEffectData eff, int computedAmount, ref ResolveResult result)
        {
            if (Ctx.deck == null) return;
            string filter = (eff.cardFilter ?? "").Trim().ToUpperInvariant();
            string toZone = (eff.toZone ?? "").Trim().ToUpperInvariant();
            string sel = (eff.select ?? "").Trim().ToUpperInvariant();
            int amount = Mathf.Max(1, computedAmount > 0 ? computedAmount : eff.amount);

            if (string.IsNullOrEmpty(filter) && string.IsNullOrEmpty(sel))
                filter = "RANDOM_FRAGMENT_CARD";

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
            else if (sel == "SELECT_ONE_FROM_FRAGMENT_POOL")
            {
                result.requiresFragmentPoolSelection = true;
                result.fragmentPickerTargetZone = string.IsNullOrEmpty(toZone) ? "DISCARD_PILE" : toZone;
                bool copy = ExtraSubstring(eff.extra, "CopySelectedSameName").ToLowerInvariant() == "true";
                result.fragmentPickerCopyCount = copy ? amount : 1;
            }
            else
            {
                Debug.LogWarning($"[CardEffect] ADD_CARD 미지원 필터/선택 (filter={filter}, sel={sel}, cardId={eff.cardId})");
            }
        }

        // 카드를 지정 존에 배치
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

        // MOVE_CARD 동사 처리(선택/자기 이동 모드 분기)
        void HandleMoveCard(CardEffectData eff, CardInstance card, ref ResolveResult result)
        {
            string sel = (eff.select ?? "").Trim().ToUpperInvariant();
            if (sel == "SELECT_ONE_FROM_DRAW_PILE")
                result.requiresDrawPileMoveSelection = true;
            else if (sel == "SELECT_ONE_FROM_DISCARD")
                result.requiresDiscardMoveSelection = true;
            else if (sel == "SELF")
            {
                bool returnToHand = ExtraSubstring(eff.extra, "ReturnToHandInsteadOfDiscard")
                                        .Equals("true", StringComparison.OrdinalIgnoreCase);
                int maxUses = ExtraIntOrDefault(eff.extra, "ExhaustAfterUses", 0);
                if (returnToHand)
                {
                    if (maxUses > 0 && card != null && card.selfUseCount + 1 >= maxUses)
                        result.exile = true;
                    else
                        result.returnToHandInsteadOfDiscard = true;
                }
            }
            else
                Debug.LogWarning($"[CardEffect] MOVE_CARD 미지원 (sel={sel}, cardId={eff.cardId})");
        }

        // COPY_CARD 동사 처리(자기/선택/패 전체 복사 모드 분기)
        void HandleCopyCard(CardEffectData eff, CardInstance card, ref ResolveResult result)
        {
            string sel = (eff.select ?? "").Trim().ToUpperInvariant();
            string from = (eff.fromZone ?? "").Trim().ToUpperInvariant();
            string to = (eff.toZone ?? "").Trim().ToUpperInvariant();

            if (sel == "SELF" && from == "SELF_CARD")
            {
                var copy = new CardInstance(card.data, transient: true);
                Ctx.deck?.AddToDiscard(copy);
                return;
            }
            if (sel == "SELECT_ONE_FROM_HAND")
            {
                result.requiresHandCopySelection = true;
                result.handSelectionCardFilter = eff.cardFilter ?? "";
                return;
            }
            if (sel == "ALL_IN_HAND" && from == "HAND")
            {
                if (Ctx.deck != null)
                {
                    var snapshot = new List<CardInstance>(Ctx.deck.Hand);
                    foreach (var h in snapshot)
                    {
                        if (h == null || h == card) continue;
                        var copy = new CardInstance(h.data, transient: true);
                        PlaceCardInZone(copy, string.IsNullOrEmpty(to) ? "DISCARD_PILE" : to);
                    }
                }
                return;
            }
            Debug.LogWarning($"[CardEffect] COPY_CARD 미지원 (sel={sel}, from={from}, cardId={eff.cardId})");
        }

        // 적에게 피해를 가함(타격 보너스/배율/연쇄 포함). 총 피해량 반환
        int DealDamage(int amount, bool isAttackCard, int baseHits = 1)
        {
            if (Ctx.enemy == null || amount <= 0) return 0;

            int hits = baseHits;
            if (isAttackCard && Ctx.attackHitCountBonusActive)
                hits += Ctx.attackHitCountBonus;

            int dmg = amount + (isAttackCard ? Ctx.attackPowerBonus : 0);

            if (isAttackCard && Ctx.damageMultiplierActive > 1 && Ctx.player != null)
            {
                float rate = (float)Ctx.player.currentHp / Mathf.Max(1, Ctx.player.maxHp) * 100f;
                if (rate <= Ctx.damageMultiplierThresholdHp)
                    dmg *= Ctx.damageMultiplierActive;
            }

            int total = 0;
            Ctx.lastHitEnemyCount = 1;
            for (int i = 0; i < hits; i++)
            {
                Ctx.enemy.TakeDamage(dmg);
                total += dmg;
                if (Ctx.enemyStat != null && !Ctx.enemyStat.IsAlive)
                    Ctx.enemyKilledThisResolve = true;

                if (Ctx.burnOnHitActive && Ctx.burnOnHitAmount > 0)
                    AddEnemyStatus("burn", Ctx.burnOnHitAmount, isCardEffect: true);

                if (isAttackCard) total += TriggerChain();

                if (Ctx.chainGainOnHitActive) Ctx.chainCount++;
            }
            return total;
        }

        // 연쇄 1회 소모해 추가 피해를 가함. 추가 피해량 반환
        int TriggerChain()
        {
            if (Ctx.chainCount <= 0) return 0;
            Ctx.chainCount--;
            Ctx.consumedChainCount++;
            int chainBase = Ctx.player != null ? Mathf.Max(1, Mathf.RoundToInt(Ctx.player.attackDamage * 0.1f)) : 1;
            int chainDmg = chainBase + Ctx.chainBonusDamage;
            Ctx.enemy.TakeDamage(chainDmg);
            Debug.Log($"[연쇄] 발동 — 추가 피해 {chainDmg}, 남은 연쇄 {Ctx.chainCount}");
            return chainDmg;
        }

        // extra에 Permanent=true 포함 여부 판정
        static bool IsPermanentExtra(string extra) =>
            !string.IsNullOrEmpty(extra) &&
            extra.IndexOf("Permanent=true", System.StringComparison.OrdinalIgnoreCase) >= 0;

        // 플레이어 회복 처리(회복 시 드로우 트리거 포함)
        void HealPlayer(int amount)
        {
            if (Ctx.player == null || amount <= 0) return;
            int before = Ctx.player.currentHp;
            Ctx.player.Heal(amount);
            int healed = Ctx.player.currentHp - before;
            if (healed > 0 && Ctx.healToDrawActive && Ctx.deck != null)
                Ctx.deck.Draw(1);
        }

        // 적에게 상태이상 부여(연계 트리거 포함)
        void AddEnemyStatus(string key, int amount, bool isCardEffect)
        {
            if (Ctx.enemy == null || amount == 0) return;
            Ctx.enemy.AddStatus(key, amount);

            if (isCardEffect && key == "burn" && Ctx.burnOnByCardActive && Ctx.burnOnByCardAmount > 0)
                Ctx.enemy.TakeDamage(Ctx.burnOnByCardAmount);

            if (isCardEffect && key == "frost" && Ctx.frostOnFrostByCardActive
                && Ctx.frostOnFrostByCardAmount > 0 && !Ctx.FrostTriggerReentrancy)
            {
                Ctx.FrostTriggerReentrancy = true;
                Ctx.enemy.AddStatus("frost", Ctx.frostOnFrostByCardAmount);
                Ctx.FrostTriggerReentrancy = false;
            }
        }

        // 적 상태이상 수치 조회
        int GetEnemyStatus(string key)
        {
            if (Ctx.enemyStat == null || string.IsNullOrEmpty(key)) return 0;
            return Ctx.enemyStat.statusEffects.TryGetValue(key, out int v) ? v : 0;
        }

        // 플레이어 방어도 증가
        void AddPlayerGuard(int amount)
        {
            if (Ctx.player == null) return;
            Ctx.player.AddGuard(amount);
        }

        // 리스트를 Fisher-Yates 방식으로 셔플
        static void ShuffleList<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        static int ScaleBasicCardEffectAmount(CardInstance card, string verb, int amount)
        {
            if (amount <= 0 || card?.data == null)
                return amount;

            if (!card.data.HasTag("BASIC"))
                return amount;

            string normalizedVerb = (verb ?? string.Empty).Trim().ToUpperInvariant();
            if (normalizedVerb != "DAMAGE" && normalizedVerb != "GAIN_BLOCK")
                return amount;

            int multiplier = RunDeckState.Instance?.BasicCardStatMultiplier ?? 1;
            return multiplier <= 1 ? amount : amount * multiplier;
        }
    }
}
