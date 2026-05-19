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
        public bool bonusDamagePerCardActive;     // 바람3: 카드 사용 시 +3
        public int  bonusDamagePerCard;
        public bool attackHitCountBonusActive;    // 바람2: 공격 카드 타격 횟수 +1
        public int  attackHitCountBonus;
        public bool burnOnHitActive;              // 불6: 적에게 피해 줄 때마다 화상 1
        public int  burnOnHitAmount;
        public bool burnOnExileActive;            // 불7: 카드 소멸 시 화상 5
        public int  burnOnExileAmount;
        public bool fragmentOnEarthUseActive;     // 땅4: 땅 카드 사용 시 파편 추가
    }

    /// <summary>
    /// 카드 ID별 효과 실행. 결과(소멸 여부, 파워 보존 여부)를 반환한다.
    /// </summary>
    public class CardEffectResolver
    {
        public struct ResolveResult
        {
            public bool exile;                       // 소멸 더미로
            public bool keepOnField;                 // 파워 — 필드 잔류
            public int  totalDamage;                 // 디버그/UI용
            public bool requiresHandExileSelection;  // 패에서 카드 1장 선택해 소멸 (불3)
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
                case 104: // 불1 — 소멸, 피해 10
                    result.totalDamage += DealDamage(10, isAttackCard: true);
                    result.exile = true;
                    break;

                case 107: // 불2 — 피해 = 화상 보유량
                    int burnAmt = GetEnemyBurn();
                    if (burnAmt > 0) result.totalDamage += DealDamage(burnAmt, isAttackCard: true);
                    break;

                case 112: // 불3 — 화상 15, 패에서 카드 1장 사용자가 직접 선택해 소멸
                    AddEnemyStatus("burn", 15);
                    result.requiresHandExileSelection = true;
                    break;

                case 115: // 불4 — 소멸, 화상 20
                    AddEnemyStatus("burn", 20);
                    result.exile = true;
                    break;

                case 116: // 불5 — 적 화상량만큼 화상 부여
                    {
                        int n = GetEnemyBurn();
                        if (n > 0) AddEnemyStatus("burn", n);
                    }
                    break;

                case 118: // 불6 (파워) — 적에게 피해 줄 때마다 화상 1
                    Ctx.burnOnHitActive = true;
                    Ctx.burnOnHitAmount += 1;
                    result.keepOnField = true;
                    break;

                case 121: // 불7 (파워) — 카드 소멸 시마다 화상 5
                    Ctx.burnOnExileActive = true;
                    Ctx.burnOnExileAmount += 5;
                    result.keepOnField = true;
                    break;

                // ── 물 ─────────────────────────────────────────────
                case 201: // 물1 — 방어도 3
                    AddPlayerGuard(3);
                    break;

                case 208: // 물2 — 체력 4 회복
                    Ctx.player?.Heal(4);
                    break;

                case 216: // 물3 — 패 전부 버리고 그만큼 드로우
                    {
                        int discarded = Ctx.deck.DiscardAllFromHand();
                        // 자기 자신은 이미 패에서 빠진 상태(NewBattleController가 사용 시점에 분리)
                        Ctx.deck.Draw(discarded);
                    }
                    break;

                // ── 바람 ───────────────────────────────────────────
                case 312: // 바람1 — 카드 2장 드로우
                    Ctx.deck.Draw(2);
                    break;

                case 320: // 바람2 (파워) — 모든 공격 카드 타격 횟수 +1
                    Ctx.attackHitCountBonusActive = true;
                    Ctx.attackHitCountBonus += 1;
                    result.keepOnField = true;
                    break;

                case 321: // 바람3 (파워) — 카드 사용 시마다 적에게 피해 3
                    Ctx.bonusDamagePerCardActive = true;
                    Ctx.bonusDamagePerCard += 3;
                    result.keepOnField = true;
                    break;

                // ── 땅 ─────────────────────────────────────────────
                case 400: // 땅1 — 방어도 3
                    AddPlayerGuard(3);
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

            // ── 파워 효과: 땅4 (땅 카드 사용 시 파편을 버린 더미에 추가) ──
            if (applyPowerEffects && Ctx.fragmentOnEarthUseActive && card.Element == CardElement.Earth)
            {
                Ctx.deck.AddToDiscard(CardDatabase.CreateFragmentInstance());
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────────
        // 효과 헬퍼
        // ─────────────────────────────────────────────────────────────

        int DealDamage(int amount, bool isAttackCard)
        {
            if (Ctx.enemy == null || amount <= 0) return 0;

            int hits = 1;
            if (isAttackCard && Ctx.attackHitCountBonusActive)
                hits += Ctx.attackHitCountBonus;

            int total = 0;
            for (int i = 0; i < hits; i++)
            {
                Ctx.enemy.TakeDamage(amount);
                total += amount;

                if (Ctx.burnOnHitActive && Ctx.burnOnHitAmount > 0)
                    AddEnemyStatus("burn", Ctx.burnOnHitAmount);
            }
            return total;
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
