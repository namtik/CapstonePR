using System;
using System.Collections.Generic;
using UnityEngine;
using Battle;
using Battle.Card;

namespace Battle.Deck
{
    public partial class CardEffectResolver
    {
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

            // PLAYER_BLOCK*배수 (소수 허용, 예: 쇄석 404 "PLAYER_BLOCK*0.5" = 방어도 절반, 내림)
            if (up.StartsWith("PLAYER_BLOCK*"))
            {
                float block = Ctx.player != null ? Ctx.player.guard : 0f;
                float.TryParse(up.Substring("PLAYER_BLOCK*".Length),
                    System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float mul);
                return Mathf.Max(0, Mathf.FloorToInt(block * mul));
            }

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

            // 매혹(505) — 적 게이지 가득 채우기는 NewBattleController.ResolveCardUse에서 처리, 카드 코스트는 0
            if (up == "ENEMY_GAUGE_REMAINING") return 0;

            if (up == "USED_ATTACK_CARD_COUNT") return Ctx.usedAttackCardCount;
            if (up == "USED_FRAGMENT_CARD_COUNT") return Ctx.usedFragmentCardCount;
            if (up.StartsWith("LAST_DISCARDED_COUNT"))
            {
                int v = Ctx.lastDiscardedCount;
                int starIdx = up.IndexOf('*');
                if (starIdx > 0)
                {
                    int.TryParse(up.Substring(starIdx + 1), out int mul);
                    if (mul > 0) v *= mul;
                }
                return v;
            }
            if (up == "EXHAUSTED_CARD_COUNT") return Ctx.exhaustedCardCount;
            if (up == "CONSUMED_CHAIN_COUNT") return Ctx.consumedChainCount;
            if (up == "HIT_ENEMY_COUNT") return Ctx.lastHitEnemyCount;

            if (up.StartsWith("ENEMY_COUNT"))
            {
                int v = EnemyCombatParty.AliveCount;
                if (v == 0 && Ctx.enemy != null && Ctx.enemyStat != null && Ctx.enemyStat.IsAlive)
                    v = 1;
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

            // 대상 적의 상태이상 수치만큼 타격(동사 208: 빙결 수치만큼 반복, 0이면 기본 1회)
            if (up.StartsWith("ENEMY_STATUS_VALUE:"))
                return GetEnemyStatus(f.Substring("ENEMY_STATUS_VALUE:".Length).Trim().ToLowerInvariant());

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
                    if (IsFragmentCard(c)) cnt++;
                return cnt;
            }

            Debug.LogWarning($"[CardEffect] 알 수 없는 HitFormula '{formula}'");
            return 0;
        }
    }
}
