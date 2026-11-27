using System;
using System.Collections.Generic;
using UnityEngine;
using Battle;
using Battle.Card;

namespace Battle.Deck
{
    public partial class CardEffectResolver
    {
        // 적에게 피해를 가함(타격 보너스/배율/연쇄 포함). 총 피해량 반환
        int DealDamage(int amount, bool isAttackCard, int baseHits = 1, bool burnDamage = false)
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
            for (int i = 0; i < hits; i++)
            {
                Ctx.enemy.TakeDamage(dmg, burnDamage ? "burn" : "Default");
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
            {
                SfxManager.Instance?.PlayBurn();
                Ctx.enemy.TakeDamage(Ctx.burnOnByCardAmount, "burn");
                Sprite burnPortrait = Ctx.enemyStat != null && Ctx.enemyStat.Data != null ? Ctx.enemyStat.Data.enemySprite : null;
                string burnName = Ctx.enemyStat != null && Ctx.enemyStat.Data != null ? Ctx.enemyStat.Data.enemyName : null;
                Battle.UI.BattleLog.RecordEnemyBurn(burnPortrait, Ctx.burnOnByCardAmount, burnName);
            }

            if (isCardEffect && key == "frost" && Ctx.frostOnFrostByCardActive
                && Ctx.frostOnFrostByCardAmount > 0 && !Ctx.FrostTriggerReentrancy)
            {
                Ctx.FrostTriggerReentrancy = true;
                Ctx.enemy.AddStatus("frost", Ctx.frostOnFrostByCardAmount);
                Ctx.FrostTriggerReentrancy = false;
            }

            if (isCardEffect && key == "frost" && amount > 0 && Ctx.damageOnFrostByCardActive
                && Ctx.damageOnFrostByCardAmount > 0 && !Ctx.FrostTriggerReentrancy)
            {
                Ctx.FrostTriggerReentrancy = true;
                try { DamageAllEnemies(Ctx.damageOnFrostByCardAmount); }
                finally { Ctx.FrostTriggerReentrancy = false; }
            }
        }

        // 파워 효과용: 살아 있는 모든 적에게 공격 카드 보너스 없이 피해
        int DamageAllEnemies(int amount)
        {
            if (amount <= 0) return 0;
            return ApplyTargetedDamage(AllEnemiesTarget, amount, isAttackCard: false, actualHits: 1);
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

        int ApplyTargetedDamage(CardEffectData eff, int amount, bool isAttackCard, int actualHits)
        {
            EnemyController savedEnemy = Ctx.enemy;
            EnemyStat savedStat = Ctx.enemyStat;
            CollectEnemyTargets(eff != null ? eff.target : null, false);
            bool burnDamage = eff != null
                && (eff.formula ?? "").IndexOf("BURN", StringComparison.OrdinalIgnoreCase) >= 0;
            int total = 0;
            int hit = 0;
            for (int i = 0; i < _targetBuf.Count; i++)
            {
                EnemyController t = _targetBuf[i];
                if (t == null || !t.IsAlive) continue;
                Ctx.enemy = t;
                Ctx.enemyStat = t.GetComponent<EnemyStat>();
                if (burnDamage && amount > 0)
                    SfxManager.Instance?.PlayBurn();
                RecordFxTarget(t);
                int dealt = DealDamage(amount, isAttackCard, actualHits, burnDamage);
                total += dealt;
                hit++;
            }
            Ctx.enemy = savedEnemy;
            Ctx.enemyStat = savedStat;
            if (hit > 0) Ctx.lastHitEnemyCount = hit;
            return total;
        }

        void ForEachEnemyTarget(string target, bool forceAll, System.Action body)
        {
            if (body == null) return;
            EnemyController savedEnemy = Ctx.enemy;
            EnemyStat savedStat = Ctx.enemyStat;
            CollectEnemyTargets(target, forceAll);
            for (int i = 0; i < _targetBuf.Count; i++)
            {
                EnemyController t = _targetBuf[i];
                if (t == null || !t.IsAlive) continue;
                Ctx.enemy = t;
                Ctx.enemyStat = t.GetComponent<EnemyStat>();
                RecordFxTarget(t);
                body();
            }
            Ctx.enemy = savedEnemy;
            Ctx.enemyStat = savedStat;
        }

        void CollectEnemyTargets(string target, bool forceAll)
        {
            _targetBuf.Clear();
            string t = (target ?? "").Trim().ToUpperInvariant();
            if (forceAll || t == "ALL_ENEMIES")
            {
                EnemyCombatParty.CollectAlive(_targetBuf);
                return;
            }
            if (t == "RANDOM_ENEMY")
            {
                EnemyController r = EnemyCombatParty.PickRandomAlive();
                if (r != null) _targetBuf.Add(r);
                return;
            }
            if (Ctx.enemy != null && Ctx.enemy.IsAlive) _targetBuf.Add(Ctx.enemy);
            else
            {
                EnemyController f = EnemyCombatParty.FirstAlive();
                if (f != null) _targetBuf.Add(f);
            }
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
