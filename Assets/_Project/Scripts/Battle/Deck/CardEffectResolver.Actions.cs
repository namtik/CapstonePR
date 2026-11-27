using System;
using System.Collections.Generic;
using UnityEngine;
using Battle;
using Battle.Card;

namespace Battle.Deck
{
    public partial class CardEffectResolver
    {
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
                else if (eff.amount > 0 && eff.formula.IndexOf('*') < 0)
                {
                    // Amount가 있고 공식에 배수가 없으면 Amount = 단위당 수치 (예: 수막 227·돌갑옷 428 "버린 수 만큼 방어도 3")
                    amount = formulaVal * eff.amount;
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

            // 카드 1장별 영구 피해 보너스(연마 410 처치 누적) — 같은 카드라도 처치한 그 장만
            if (verb == "DAMAGE" && amount > 0 && card != null)
                amount += card.permanentDamage;

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
                                    result.totalDamage += ApplyTargetedDamage(eff, amount, isAttackCard: true, actualHits);
                                    if (Ctx.chainCount > 0) Ctx.chainCount -= consumePer;
                                    safety--;
                                } while (Ctx.chainCount > 0 && safety > 0);
                                if (Ctx.chainCount < 0) Ctx.chainCount = 0;
                            }
                            else
                            {
                                result.totalDamage += ApplyTargetedDamage(eff, amount, isAttackCard: true, actualHits);
                            }
                        }
                        else
                        {
                            result.totalDamage += ApplyTargetedDamage(eff, amount, isAttackCard: true, actualHits);
                        }
                    }
                    break;

                case "GAIN_BLOCK":
                    if (amount > 0) AddPlayerGuard(amount);
                    break;

                case "LOSE_BLOCK":
                    // 플레이어 방어도를 amount만큼 줄인다(0 아래로는 안 내려감)
                    if (amount > 0 && Ctx.player != null) Ctx.player.AddGuard(-amount);
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
                        else if (sel == "ALL" && Ctx.deck != null)
                        {
                            // 패에서 필터에 맞는 카드 전부 버리기 (예: 돌갑옷 428 — 파편 카드 전부)
                            string filter = (eff.cardFilter ?? "").Trim().ToUpperInvariant();
                            var snapshot = new List<CardInstance>(Ctx.deck.Hand);
                            int d = 0;
                            foreach (var c in snapshot)
                            {
                                if (!PassesTriggerFilter(c, filter)) continue;
                                if (Ctx.deck.DiscardCardFromHand(c)) d++;
                            }
                            Ctx.lastDiscardedCount = d;
                            if (Ctx.discardToDrawActive && d > 0) Ctx.deck.Draw(d);
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
                        {
                            bool hitAll = Ctx.burnSkillHitsAllActive && st == "burn";
                            ForEachEnemyTarget(eff.target, hitAll, () => AddEnemyStatus(st, amount, isCardEffect: true));
                        }
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
                        bool selfCard = (eff.target ?? "").Trim().Equals("SELF_CARD", StringComparison.OrdinalIgnoreCase);
                        if (st == "ATTACK_POWER" && selfCard)
                        {
                            // 연마(410): 사용한 이 카드 1장의 피해만 영구 증가 — 같은 카드의 다른 장·플레이어 공격력은 그대로
                            if (card?.data != null)
                            {
                                card.permanentDamage += amount; // 이번 전투에서 바로 반영
                                if (card.runCopyIndex >= 0)     // 런 덱 카드면 다음 전투에도 유지
                                    Battle.RunDeckState.EnsureExists().AddPermanentCardDamage(card.data.id, card.runCopyIndex, amount);
                            }
                        }
                        else if (st == "ATTACK_POWER")
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

                case "MODIFY_CARD_GAUGE_COST":
                    HandleModifyCardGaugeCost(eff, card, amount);
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
            {
                result.requiresDiscardMoveSelection = true;
                result.discardMoveTargetZone = eff.toZone ?? "";
            }
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

        // MODIFY_CARD_GAUGE_COST: 패의 무작위 부적 1장의 게이지 코스트를 이번 전투 동안 amount만큼 증감(청류 202)
        // 감소일 때는 코스트가 남아 있는 부적을 우선 고르고, 없으면 패 전체에서 고른다. 사용 중인 카드 자신은 제외.
        void HandleModifyCardGaugeCost(CardEffectData eff, CardInstance card, int amount)
        {
            if (Ctx.deck == null || amount == 0) return;
            string from = (eff.fromZone ?? "").Trim().ToUpperInvariant();
            string sel = (eff.select ?? "").Trim().ToUpperInvariant();
            if ((from != "" && from != "HAND") || (sel != "" && sel != "RANDOM"))
            {
                Debug.LogWarning($"[CardEffect] MODIFY_CARD_GAUGE_COST 미지원 (from={from}, sel={sel}, cardId={eff.cardId})");
                return;
            }

            var pool = new List<CardInstance>();
            foreach (var h in Ctx.deck.Hand)
                if (h != null && h != card && (amount > 0 || h.Gauge > 0)) pool.Add(h);
            if (pool.Count == 0)
                foreach (var h in Ctx.deck.Hand)
                    if (h != null && h != card) pool.Add(h);
            if (pool.Count == 0) return;

            CardInstance target = pool[UnityEngine.Random.Range(0, pool.Count)];
            target.gaugeCostModifier += amount;
            Debug.Log($"[CardEffect] {target.data.displayName} 코스트 {amount:+0;-0} → {target.Gauge} (cardId={eff.cardId})");
            Ctx.deck.NotifyChanged(); // 패 UI 코스트 표시 갱신
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
    }
}
