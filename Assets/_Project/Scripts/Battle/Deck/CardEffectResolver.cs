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
        public bool frostOnWaterUseRandomTarget;   // 빙결 부여 대상 무작위 여부(낙숫물 226)
        public int  waterUseCounter;               // 물 카드 사용 카운터
        public bool attackPowerOnHpLossActive;    // 체력 잃을 때 공격력 증가 활성
        public int  attackPowerOnHpLossAmount;     // 체력 잃을 때 공격력 증가량
        public bool frostOnFrostByCardActive;     // 카드로 빙결 부여 시 추가 빙결 활성
        public int  frostOnFrostByCardAmount;      // 카드로 빙결 부여 시 추가 빙결량
        public bool damageOnFrostByCardActive;    // 카드로 빙결 부여 시 모든 적 피해 활성(혹한 225)
        public int  damageOnFrostByCardAmount;     // 카드로 빙결 부여 시 모든 적 피해량
        public bool burnOnHpLossActive;           // 체력 잃을 때 모든 적 화상 활성(수라의 길 122)
        public bool damageOnDrawActive;           // 부적 뽑을 때 모든 적 피해 활성(선회 325)
        public int  damageOnDrawAmount;            // 부적 뽑을 때 모든 적 피해량
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
    public partial class CardEffectResolver
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
            public string discardMoveTargetZone;      // 버린 더미 선택 후 이동 목적지(HAND/DRAW_PILE_TOP 등)
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
        readonly List<EnemyController> _targetBuf = new List<EnemyController>();

        bool _triggeringOtherCards; // 다른 카드 효과 발동 재귀 가드
        bool _inLifecycleTrigger;   // 생애주기 트리거 재귀 가드
        readonly HashSet<CardInstance> _selfLogCards = new HashSet<CardInstance>();

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

        // 마지막으로 해석한 카드가 실제로 피해·상태이상을 준 적 목록(이펙트 위치용, 중복 없음)
        private readonly List<EnemyController> _cardFxTargets = new List<EnemyController>();
        public IReadOnlyList<EnemyController> LastCardFxTargets => _cardFxTargets;
        private int _fxRecordDepth;   // 카드 자체 효과 실행 중일 때만 대상 기록

        void RecordFxTarget(EnemyController t)
        {
            if (_fxRecordDepth > 0 && t != null && !_cardFxTargets.Contains(t)) _cardFxTargets.Add(t);
        }

        // 카드 효과 해석/실행 메인 진입점
        public ResolveResult Resolve(CardInstance card)
        {
            var result = new ResolveResult();
            Ctx.enemyKilledThisResolve = false;
            _cardFxTargets.Clear();

            // 저주 피해는 NewBattleController.ApplyCurseOnCardUse에서 한 번만 처리한다(여기서 또 주면 이중 피해)

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
                    _fxRecordDepth++;
                    try { ApplyAction(eff, card, ref result); }
                    finally { _fxRecordDepth--; }
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
                {
                    int frostAmt = Mathf.Max(1, Ctx.frostOnWaterUseAmount);
                    if (Ctx.frostOnWaterUseRandomTarget)
                        ForEachEnemyTarget("RANDOM_ENEMY", false, () => AddEnemyStatus("frost", frostAmt, isCardEffect: true));
                    else
                        AddEnemyStatus("frost", frostAmt, isCardEffect: true);
                }
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
            RunLoggedSelfTrigger(card, "ON_SELF_EXHAUST");
        }

        // 패에서 버려질 때 생애주기 트리거 처리
        public void NotifyCardDiscardedFromHand(CardInstance card)
        {
            RunLoggedSelfTrigger(card, "ON_SELF_DISCARDED");
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

        // extra에 Permanent=true 포함 여부 판정
        static bool IsPermanentExtra(string extra) =>
            !string.IsNullOrEmpty(extra) &&
            extra.IndexOf("Permanent=true", System.StringComparison.OrdinalIgnoreCase) >= 0;

        static readonly CardEffectData AllEnemiesTarget = new CardEffectData { target = "ALL_ENEMIES" };

    }
}
