using System;
using System.Collections.Generic;
using UnityEngine;
using Battle;
using Battle.Card;

namespace Battle.Deck
{
    public partial class CardEffectResolver
    {
        // 이 카드 자신의 소멸·버림 효과는 사용한 카드와 따로 기록한다.
        void RunLoggedSelfTrigger(CardInstance card, string whenKey)
        {
            if (card == null || card.data == null || Ctx == null) return;
            if (!HasSelfTrigger(card, whenKey)) return;
            if (!_selfLogCards.Add(card)) return;

            var scope = Battle.UI.BattleLog.SuspendCapture();
            bool logGuard = Battle.UI.BattleLog.GuardLoggingEnabled;
            Battle.UI.BattleLog.SetGuardLogging(false);
            int before = Ctx.player != null ? Mathf.RoundToInt(Ctx.player.guard) : 0;
            Battle.UI.BattleLog.PushGuardFrame();
            var dummy = new ResolveResult();
            try
            {
                var effects = CardDatabase.GetEffects(card.Id);
                if (effects != null)
                {
                    for (int i = 0; i < effects.Count; i++)
                    {
                        var eff = effects[i];
                        if ((eff.when ?? "").Trim().ToUpperInvariant() != whenKey) continue;
                        if (!EvaluateCondition(eff.ifCond, card, ref dummy)) continue;
                        ApplyAction(eff, card, ref dummy);
                    }
                }
            }
            finally
            {
                int after = Ctx.player != null ? Mathf.RoundToInt(Ctx.player.guard) : before;
                int raw = Mathf.Max(0, after - before);
                int nested = Battle.UI.BattleLog.PopGuardFrame();
                int mine = Mathf.Max(0, raw - nested);
                Battle.UI.BattleLog.AddGuardFrame(raw);
                Battle.UI.BattleLog.SetGuardLogging(logGuard);
                Battle.UI.BattleLog.CommitTriggered(card.data, dummy.totalDamage, mine, scope);
                _selfLogCards.Remove(card);
            }
        }

        bool HasSelfTrigger(CardInstance card, string whenKey)
        {
            var effects = CardDatabase.GetEffects(card.Id);
            if (effects == null) return false;
            for (int i = 0; i < effects.Count; i++)
            {
                if ((effects[i].when ?? "").Trim().ToUpperInvariant() == whenKey) return true;
            }
            return false;
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
                    if (IsFragmentCard(c)) frags.Add(c);
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
                case "FRAGMENT_CARD": return IsFragmentCard(c);
                default: return true;
            }
        }

        // 파편 카드 판정 — 파편은 element가 EARTH이고 FRAGMENT 태그로 식별된다(구 Fragment 속성도 폴백 허용)
        static bool IsFragmentCard(CardInstance c)
        {
            if (c == null) return false;
            if (c.Element == CardElement.Fragment) return true;
            return c.data != null && c.data.HasTag("FRAGMENT");
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
                            Ctx.frostOnWaterUseEveryN = Mathf.Max(1, ExtraIntOrDefault(eff.extra, "EveryNthUse", 1));
                            Ctx.frostOnWaterUseRandomTarget = (eff.target ?? "").Trim().ToUpperInvariant() == "RANDOM_ENEMY";
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

                case "ON_PLAYER_LOSE_HP":
                    if (verb == "APPLY_STATUS" && status == "BURN")
                        Ctx.burnOnHpLossActive = true; // 수라의 길(122): 잃은 체력만큼 모든 적에게 화상
                    break;

                case "ON_CARD_DRAWN":
                    if (verb == "DAMAGE")
                    {
                        Ctx.damageOnDrawActive = true; // 선회(325): 부적을 뽑을 때마다 모든 적 피해
                        Ctx.damageOnDrawAmount += amount;
                    }
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
                    else if (verb == "DAMAGE")
                    {
                        Ctx.damageOnFrostByCardActive = true; // 혹한(225): 카드로 빙결 부여 시 모든 적 피해
                        Ctx.damageOnFrostByCardAmount += Mathf.Max(1, amount);
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

        // 선회(325): 부적이 패에 들어올 때(뽑기) 모든 적 피해
        public void NotifyCardDrawnForPowers()
        {
            if (Ctx == null || !Ctx.damageOnDrawActive || Ctx.damageOnDrawAmount <= 0) return;
            DamageAllEnemies(Ctx.damageOnDrawAmount);
        }

        // 수라의 길(122): 플레이어가 체력을 잃으면 잃은 만큼 모든 적에게 화상
        public void NotifyPlayerHpLostForPowers(int amount)
        {
            if (Ctx == null || !Ctx.burnOnHpLossActive || amount <= 0) return;
            ForEachEnemyTarget("ALL_ENEMIES", true, () => AddEnemyStatus("burn", amount, isCardEffect: false));
        }
    }
}
