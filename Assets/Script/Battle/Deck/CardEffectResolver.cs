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
        public CardEffectResolver resolver; // 자기 자신(파워 효과 트리거 전파용)

        // 파워 상태 (전투 동안 지속)
        public bool bonusDamagePerCardActive;     // 바람22(321): 카드 사용 시 +3
        public int  bonusDamagePerCard;
        public bool attackHitCountBonusActive;    // 바람21(320): 공격 카드 타격 횟수 +1
        public int  attackHitCountBonus;
        public bool burnOnHitActive;              // 불19(118): 적에게 피해 줄 때마다 화상 1
        public int  burnOnHitAmount;
        public bool burnOnExileActive;            // 불22(121): 카드 소멸 시 화상 5
        public int  burnOnExileAmount;
        public bool fragmentOnEarthUseActive;     // 땅21(420): 땅 카드 사용 시 파편 추가

        public bool healToDrawActive;             // 물19(218): 회복될 때마다 드로우 1
        public bool discardToDrawActive;          // 물21(220): 카드 버릴 때마다 드로우 1
        public bool waterUseHealActive;           // 물22(221): 물 카드 사용 시 체력 1 회복
        public bool chainGainOnHitActive;         // 바람19(318): 피해 시 연쇄 1 획득
        public int  chainCount;                   // 연쇄 누적치
        public int  chainBonusDamage;             // 바람18(317): 연쇄 1회 피해량 증가
    }

    /// <summary>
    /// 카드 ID별 효과 실행. 결과(소멸 여부, 파워 보존 여부)를 반환한다.
    /// </summary>
    public class CardEffectResolver
    {
        public struct ResolveResult
        {
            public bool exile;                        // 소멸 더미로
            public bool keepOnField;                  // 파워 — 필드 잔류
            public int  totalDamage;                  // 디버그/UI용
            public bool requiresHandExileSelection;   // 패에서 카드 1장 선택해 소멸 (불13/112)
            public bool requiresHandDiscardSelection; // 패에서 카드 1장 선택해 버리기 (물8/207)
            public bool skipNextGaugeCost;            // 다음 카드 게이지 소모 스킵 (바람14/313)
            public bool recastLastCard;               // 마지막 사용 카드 효과 재사용 (물12/211)
        }

        public CardEffectContext Ctx { get; private set; }

        public void Bind(CardEffectContext ctx)
        {
            Ctx = ctx;
            ctx.resolver = this;
        }

        public ResolveResult Resolve(CardInstance card)
        {
            var result = new ResolveResult();

            // ── 저주된 카드면 사용 시 플레이어가 피해를 본다 ──
            if (card.cursed && Ctx.player != null)
            {
                Ctx.player.TakeDamage(5);
                Debug.Log($"[Curse] {card.data.displayName} 저주 발동 — 플레이어 5 피해");
                card.cursed = false;
            }

            switch (card.Id)
            {
                // ── 불 ─────────────────────────────────────────────
                case 104: // 불5 — 소멸, 피해 10
                    result.totalDamage += DealDamage(10, isAttackCard: true);
                    result.exile = true;
                    break;

                case 106: // 불7 — 피해 10, 적 화상 10 이상이면 한 번 더 발동
                    result.totalDamage += DealDamage(10, isAttackCard: true);
                    if (GetEnemyBurn() >= 10)
                        result.totalDamage += DealDamage(10, isAttackCard: true);
                    break;

                case 107: // 불8 — 피해 = 화상 보유량
                    int burnAmt = GetEnemyBurn();
                    if (burnAmt > 0) result.totalDamage += DealDamage(burnAmt, isAttackCard: true);
                    break;

                case 112: // 불13 — 화상 15, 패에서 카드 1장 직접 선택해 소멸
                    AddEnemyStatus("burn", 15);
                    result.requiresHandExileSelection = true;
                    break;

                case 115: // 불16 — 소멸, 화상 20
                    AddEnemyStatus("burn", 20);
                    result.exile = true;
                    break;

                case 116: // 불17 — 적 화상량만큼 화상 부여
                    {
                        int n = GetEnemyBurn();
                        if (n > 0) AddEnemyStatus("burn", n);
                    }
                    break;

                case 118: // 불19 (파워) — 적에게 피해 줄 때마다 화상 1
                    Ctx.burnOnHitActive = true;
                    Ctx.burnOnHitAmount += 1;
                    result.keepOnField = true;
                    break;

                case 121: // 불22 (파워) — 카드 소멸 시마다 화상 5
                    Ctx.burnOnExileActive = true;
                    Ctx.burnOnExileAmount += 5;
                    result.keepOnField = true;
                    break;

                // ── 물 ─────────────────────────────────────────────
                case 201: // 물2 — 방어도 3
                    AddPlayerGuard(3);
                    break;

                case 203: // 물4 — 피해 5, 플레이어 체력이 가득이면 드로우 2
                    result.totalDamage += DealDamage(5, isAttackCard: true);
                    if (Ctx.player != null && Ctx.player.currentHp >= Ctx.player.maxHp)
                        Ctx.deck.Draw(2);
                    break;

                case 207: // 물8 — 드로우 1, 패에서 카드 1장 선택해 버리기
                    Ctx.deck.Draw(1);
                    result.requiresHandDiscardSelection = true;
                    break;

                case 208: // 물9 — 체력 4 회복
                    HealPlayer(4);
                    break;

                case 211: // 물12 — 소멸, 마지막 사용 카드 효과 재사용
                    result.recastLastCard = true;
                    result.exile = true;
                    break;

                case 216: // 물17 — 패 전부 버리고 그만큼 드로우
                    {
                        int discarded = Ctx.deck.DiscardAllFromHand();
                        if (Ctx.discardToDrawActive && discarded > 0) Ctx.deck.Draw(discarded); // 220 트리거분
                        Ctx.deck.Draw(discarded);
                    }
                    break;

                case 218: // 물19 (파워) — 플레이어 회복 시 드로우 1
                    Ctx.healToDrawActive = true;
                    result.keepOnField = true;
                    break;

                case 220: // 물21 (파워) — 카드 효과로 카드 버릴 때 드로우 1
                    Ctx.discardToDrawActive = true;
                    result.keepOnField = true;
                    break;

                case 221: // 물22 (파워) — 물 속성 카드 사용 시 체력 1 회복
                    Ctx.waterUseHealActive = true;
                    result.keepOnField = true;
                    break;

                // ── 바람 ───────────────────────────────────────────
                case 306: // 바람7 — 피해 2 x3, 연쇄 10 획득
                    result.totalDamage += DealDamage(2, isAttackCard: true, baseHits: 3);
                    Ctx.chainCount += 10;
                    break;

                case 308: // 바람9 — 피해 1 x3, 드로우 2
                    result.totalDamage += DealDamage(1, isAttackCard: true, baseHits: 3);
                    Ctx.deck.Draw(2);
                    break;

                case 309: // 바람10 — 소멸, 연쇄를 모두 소진할 때까지 피해 2 반복
                    {
                        int safety = 99; // 무한 루프 방지
                        // 최초 1회 + 연쇄가 남아있는 동안 반복
                        do
                        {
                            result.totalDamage += DealDamage(2, isAttackCard: true);
                            safety--;
                        } while (Ctx.chainCount > 0 && safety > 0);
                        result.exile = true;
                    }
                    break;

                case 312: // 바람13 — 카드 2장 드로우
                    Ctx.deck.Draw(2);
                    break;

                case 313: // 바람14 — 다음에 사용하는 카드는 행동 게이지 소모 X
                    result.skipNextGaugeCost = true;
                    break;

                case 314: // 바람15 — 연쇄 20 획득
                    Ctx.chainCount += 20;
                    break;

                case 317: // 바람18 (파워) — 연쇄 1회 피해량 +1
                    Ctx.chainBonusDamage += 1;
                    result.keepOnField = true;
                    break;

                case 318: // 바람19 (파워) — 피해를 줄 때마다 연쇄 1 획득
                    Ctx.chainGainOnHitActive = true;
                    result.keepOnField = true;
                    break;

                case 320: // 바람21 (파워) — 모든 공격 카드 타격 횟수 +1
                    Ctx.attackHitCountBonusActive = true;
                    Ctx.attackHitCountBonus += 1;
                    result.keepOnField = true;
                    break;

                case 321: // 바람22 (파워) — 카드 사용 시마다 적에게 피해 3
                    Ctx.bonusDamagePerCardActive = true;
                    Ctx.bonusDamagePerCard += 3;
                    result.keepOnField = true;
                    break;

                // ── 땅 ─────────────────────────────────────────────
                case 400: // 땅1 — 피해 5 (새 DB)
                    result.totalDamage += DealDamage(5, isAttackCard: true);
                    break;

                case 401: // 땅2 — 방어도 3
                    AddPlayerGuard(3);
                    break;

                case 407: // 땅8 — 방어도 5
                    AddPlayerGuard(5);
                    break;

                case 411: // 땅2 — 방어도 5, 파편 1장을 뽑을 더미에 섞음
                    AddPlayerGuard(5);
                    Ctx.deck.AddToDrawShuffled(CardDatabase.CreateFragmentInstance());
                    break;

                case 414: // 땅3 — 소멸, 파편 선택해 같은 이름 카드를 버린 더미에 10장 섞음
                    {
                        // 프로토타입: 무작위 파편 ID 선택
                        int pick = CardDatabase.FragmentIds[Random.Range(0, CardDatabase.FragmentIds.Length)];
                        for (int i = 0; i < 10; i++)
                            Ctx.deck.AddToDiscard(CardDatabase.CreateFragmentInstance(pick));
                        result.exile = true;
                    }
                    break;

                case 420: // 땅4 (파워) — 땅 속성 카드 사용 시마다 파편 버린 더미에 추가
                    Ctx.fragmentOnEarthUseActive = true;
                    result.keepOnField = true;
                    break;

                // ── 파편 ───────────────────────────────────────────
                case 422: // 파편1 — 소멸, 방어도 3, 드로우 1
                    AddPlayerGuard(3);
                    Ctx.deck.Draw(1);
                    result.exile = true;
                    break;

                case 423: // 파편2 — 소멸, 피해 5, 드로우 1
                    result.totalDamage += DealDamage(5, isAttackCard: true);
                    Ctx.deck.Draw(1);
                    result.exile = true;
                    break;

                case 424: // 파편3 — 소멸, 피해 8, 방어도 5
                    result.totalDamage += DealDamage(8, isAttackCard: true);
                    AddPlayerGuard(5);
                    result.exile = true;
                    break;

                default:
                    Debug.LogWarning($"[CardEffect] 알 수 없는 카드 id={card.Id}");
                    break;
            }

            // PDF: "모든 파워 카드의 효과는 카드 효과 처리 이후 발동"
            // → 파워 카드를 사용하는 그 시점에는 공통 파워 효과를 적용하지 않음(자기 자신 제외)
            bool applyPowerEffects = !result.keepOnField;

            // ── 파워 효과: 바람3 (카드 사용 시마다 +3) ──
            if (applyPowerEffects && Ctx.bonusDamagePerCardActive && Ctx.bonusDamagePerCard > 0)
            {
                result.totalDamage += DealDamage(Ctx.bonusDamagePerCard, isAttackCard: false);
            }

            // ── 파워 효과: 땅21(420) (땅 카드 사용 시 파편을 버린 더미에 추가) ──
            if (applyPowerEffects && Ctx.fragmentOnEarthUseActive && card.Element == CardElement.Earth)
            {
                Ctx.deck.AddToDiscard(CardDatabase.CreateFragmentInstance());
            }

            // ── 파워 효과: 물22(221) (물 속성 카드 사용 시 체력 1 회복) ──
            if (applyPowerEffects && Ctx.waterUseHealActive && card.Element == CardElement.Water)
            {
                HealPlayer(1);
            }

            return result;
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

            int total = 0;
            for (int i = 0; i < hits; i++)
            {
                // 본 피해
                Ctx.enemy.TakeDamage(amount);
                total += amount;

                if (Ctx.burnOnHitActive && Ctx.burnOnHitAmount > 0)
                    AddEnemyStatus("burn", Ctx.burnOnHitAmount);

                // (1) 연쇄 발동 먼저 — 이전에 쌓여 있던 연쇄만 소비/발동
                if (isAttackCard)
                    total += TriggerChain();

                // (2) 그 다음 연쇄 획득 — 318로 이번에 얻은 연쇄는 다음 공격부터 발동 가능
                if (Ctx.chainGainOnHitActive)  // 바람19(318): 피해 시 연쇄 1 획득
                    Ctx.chainCount++;
            }
            return total;
        }

        /// <summary>
        /// 연쇄 발동: 연쇄가 남아있으면 1 소비하고 추가 피해(1 + 317 보너스)를 준다.
        /// 연쇄 추가 피해는 318(피해 시 연쇄 획득)을 다시 트리거하지 않는다(무한 연쇄 방지).
        /// </summary>
        int TriggerChain()
        {
            if (Ctx.chainCount <= 0) return 0;
            Ctx.chainCount--;
            int chainDmg = 1 + Ctx.chainBonusDamage;
            Ctx.enemy.TakeDamage(chainDmg);
            Debug.Log($"[연쇄] 발동 — 추가 피해 {chainDmg}, 남은 연쇄 {Ctx.chainCount}");
            return chainDmg;
        }

        /// <summary>플레이어 회복. 물19(218) 활성 시 회복할 때마다 드로우 1.</summary>
        void HealPlayer(int amount)
        {
            if (Ctx.player == null || amount <= 0) return;
            Ctx.player.Heal(amount);
            if (Ctx.healToDrawActive)  // 물19(218)
                Ctx.deck.Draw(1);
        }

        void AddEnemyStatus(string key, int amount)
        {
            if (Ctx.enemy == null || amount == 0) return;
            Ctx.enemy.AddStatus(key, amount);
        }

        int GetEnemyBurn()
        {
            if (Ctx.enemyStat == null) return 0;
            return Ctx.enemyStat.statusEffects.TryGetValue("burn", out int v) ? v : 0;
        }

        void AddPlayerGuard(int amount)
        {
            if (Ctx.player == null) return;
            Ctx.player.AddGuard(amount);
        }

        void ExileOneFromHandExcept(CardInstance self)
        {
            // 프로토타입: 패에서 자기 자신 외 첫 번째 카드 소멸
            var hand = Ctx.deck.Hand;
            for (int i = 0; i < hand.Count; i++)
            {
                if (hand[i] != self)
                {
                    var target = hand[i];
                    Ctx.deck.ExileFromHand(target);
                    NotifyExile(target);
                    return;
                }
            }
        }

        /// <summary>외부에서 카드가 소멸될 때 파워 효과 트리거(불7)를 발동시키기 위한 알림.</summary>
        public void NotifyExile(CardInstance card)
        {
            if (Ctx.burnOnExileActive && Ctx.burnOnExileAmount > 0)
                AddEnemyStatus("burn", Ctx.burnOnExileAmount);
        }
    }
}
