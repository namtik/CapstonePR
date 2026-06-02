using System;
using UnityEngine;
using Battle.Card;

namespace Battle
{
    /// <summary>
    /// 콤보 효과 실행에 필요한 런타임 참조/카운터. NewBattleController가 Resolve 직전에 채운다.
    /// </summary>
    public class ComboResolveContext
    {
        public EnemyController enemy;
        public EnemyStat enemyStat;
        public Player player;
        public Deck.CardDeckSystem deck;

        // Formula/Repeat 평가용 카운터
        public int usedFireCardCount;
        public int usedFragmentCardCount;
        public int playerChain;             // 현재 연쇄 보유량
        public int awakenComboCount;        // 이번 각성에서 발동한 콤보 수
        public int awakenFragmentExhausted; // 각성 진입 시 격리된 파편 수
        public int selfRepeatCount;         // 이 콤보가 이번 각성에 반복된 횟수

        // GAIN_STATUS CHAIN 등 — 플레이어 연쇄 증가 위임 (CardEffectContext.chainCount 갱신)
        public Action<int> onGainChain;
    }

    /// <summary>
    /// 데이터 드리븐 콤보 효과 엔진 (간소화판).
    /// 단일 적 프로토타입에 맞춰 ALL_ENEMIES/RANDOM_ENEMY/ENEMY는 모두 현재 적 1체에 적용.
    /// 핵심 동사(DAMAGE/APPLY_STATUS/GAIN_STATUS/HEAL/GAIN_BLOCK/DRAW_CARD/DISCARD_HAND) 실동작,
    /// 미지원 동사(MOVE_CARD/MODIFY_ENEMY_STAT/BUFF_NEXT_COMBO/SKIP_COOLDOWN)는 경고 후 스킵.
    /// </summary>
    public class ComboEffectResolver
    {
        private int _lastDiscardedCount; // DISCARD_HAND → DRAW_CARD(LAST_DISCARDED_COUNT) 연계용

        public void Resolve(ComboSkillDef combo, ComboResolveContext ctx)
        {
            if (combo == null || combo.dbEffects == null || ctx == null) return;
            _lastDiscardedCount = 0;

            foreach (var eff in combo.dbEffects)
            {
                if (!PassesCondition(eff, ctx)) continue;

                int repeat = ResolveRepeatCount(eff, ctx);
                for (int r = 0; r < repeat; r++)
                    ApplyEffect(eff, ctx);
            }
        }

        // ── 조건 ──────────────────────────────────────────────────

        bool PassesCondition(ComboEffectData eff, ComboResolveContext ctx)
        {
            string cond = (eff.ifCond ?? "").Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(cond)) return true;

            switch (cond)
            {
                case "ENEMY_KILLED_BY_THIS_COMBO":
                    // 처치 여부 추적 미구현 — 보수적으로 false(스킵). SKIP_COOLDOWN 전용이라 진행 영향 X.
                    return false;
                default:
                    Debug.LogWarning($"[ComboEffect] 알 수 없는 조건 '{eff.ifCond}' (ref={eff.refComboId}) — 무시하고 실행");
                    return true;
            }
        }

        // ── 반복 횟수 ─────────────────────────────────────────────

        int ResolveRepeatCount(ComboEffectData eff, ComboResolveContext ctx)
        {
            string rep = (eff.repeat ?? "").Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(rep)) return 1;

            switch (rep)
            {
                case "REPEAT_BY_AWAKEN_COMBO_COUNT":        return Mathf.Max(1, ctx.awakenComboCount);
                case "REPEAT_BY_RESOURCE_CONSUME_ALL":      return Mathf.Max(1, ctx.playerChain);
                case "REPEAT_BY_AWAKEN_FRAGMENT_EXHAUSTED": return Mathf.Max(1, ctx.awakenFragmentExhausted);
                case "REPEAT_BY_SELF_AWAKEN_USE_COUNT":     return Mathf.Max(1, ctx.selfRepeatCount);
                default:
                    Debug.LogWarning($"[ComboEffect] 미지원 Repeat '{eff.repeat}' (ref={eff.refComboId}) — 1회만 실행");
                    return 1;
            }
        }

        // ── 동사 실행 ─────────────────────────────────────────────

        void ApplyEffect(ComboEffectData eff, ComboResolveContext ctx)
        {
            string verb = (eff.doAction ?? "").Trim().ToUpperInvariant();
            int amount = EvalAmount(eff, ctx);
            int hits = Mathf.Max(1, eff.hits);

            switch (verb)
            {
                case "DAMAGE":
                    if (ctx.enemy != null)
                        for (int h = 0; h < hits; h++) ctx.enemy.TakeDamage(amount);
                    break;

                case "APPLY_STATUS":
                {
                    string st = (eff.status ?? "").Trim().ToUpperInvariant();
                    if (st == "ATTACK_POWER") { WarnSkip("APPLY_STATUS:ATTACK_POWER", eff); break; }
                    if (ctx.enemy != null && !string.IsNullOrEmpty(st))
                        ctx.enemy.AddStatus(st.ToLowerInvariant(), amount);
                    break;
                }

                case "GAIN_STATUS":
                {
                    string st = (eff.status ?? "").Trim().ToUpperInvariant();
                    if (st == "CHAIN")
                        ctx.onGainChain?.Invoke(amount);
                    else if (ctx.player != null && !string.IsNullOrEmpty(st))
                        ctx.player.AddStatus(st.ToLowerInvariant(), amount);
                    break;
                }

                case "HEAL":
                    if (ctx.player != null) ctx.player.Heal(amount);
                    break;

                case "GAIN_BLOCK":
                    if (ctx.player != null) ctx.player.AddGuard(amount);
                    break;

                case "DRAW_CARD":
                    if (ctx.deck != null) ctx.deck.Draw(Mathf.Max(0, amount));
                    break;

                case "DISCARD_HAND":
                    _lastDiscardedCount = ctx.deck != null ? ctx.deck.DiscardAllFromHand() : 0;
                    break;

                // ── 미지원(단일 적/프로토타입 범위 밖) — 경고 후 스킵 ──
                case "MOVE_CARD":
                case "MODIFY_ENEMY_STAT":
                case "BUFF_NEXT_COMBO":
                case "SKIP_COOLDOWN":
                    WarnSkip(verb, eff);
                    break;

                default:
                    Debug.LogWarning($"[ComboEffect] 미구현 동사 '{eff.doAction}' (ref={eff.refComboId})");
                    break;
            }
        }

        void WarnSkip(string verb, ComboEffectData eff)
            => Debug.LogWarning($"[ComboEffect] '{verb}' 미지원(단일 적/프로토타입) — 스킵 (ref={eff.refComboId})");

        // ── 수치 공식 ─────────────────────────────────────────────

        int EvalAmount(ComboEffectData eff, ComboResolveContext ctx)
        {
            string f = (eff.formula ?? "").Trim();
            if (string.IsNullOrEmpty(f)) return eff.amount;

            // ENEMY_STATUS_VALUE:BURN 형태
            if (f.IndexOf(':') >= 0 && f.ToUpperInvariant().StartsWith("ENEMY_STATUS_VALUE"))
            {
                string st = f.Substring(f.IndexOf(':') + 1).Trim().ToLowerInvariant();
                return EnemyStatus(ctx, st);
            }

            // MAX_HP*0.1 형태
            string upper = f.ToUpperInvariant();
            if (upper.StartsWith("MAX_HP*"))
            {
                string mul = upper.Substring("MAX_HP*".Length).Trim();
                if (float.TryParse(mul, out float m) && ctx.player != null)
                    return Mathf.FloorToInt(ctx.player.maxHp * m);
                return eff.amount;
            }

            // "기본값+TOKEN" 형태
            int plus = f.IndexOf('+');
            if (plus > 0)
            {
                string left = f.Substring(0, plus).Trim();
                string right = f.Substring(plus + 1).Trim();
                int baseVal = int.TryParse(left, out int bv) ? bv : 0;
                return baseVal + TokenValue(right, ctx, eff.amount);
            }

            // 단일 토큰
            return TokenValue(f, ctx, eff.amount);
        }

        int TokenValue(string token, ComboResolveContext ctx, int fallback)
        {
            switch ((token ?? "").Trim().ToUpperInvariant())
            {
                case "USED_FIRE_CARD_COUNT":     return ctx.usedFireCardCount;
                case "USED_FRAGMENT_CARD_COUNT": return ctx.usedFragmentCardCount;
                case "PLAYER_BLOCK":             return ctx.player != null ? Mathf.RoundToInt(ctx.player.guard) : 0;
                case "PLAYER_CHAIN":             return ctx.playerChain;
                case "LAST_DISCARDED_COUNT":     return _lastDiscardedCount;
                default:
                    if (int.TryParse(token, out int v)) return v;
                    Debug.LogWarning($"[ComboEffect] 알 수 없는 Formula 토큰 '{token}' — 기본값 {fallback} 사용");
                    return fallback;
            }
        }

        int EnemyStatus(ComboResolveContext ctx, string key)
        {
            if (ctx.enemyStat != null && ctx.enemyStat.statusEffects != null &&
                ctx.enemyStat.statusEffects.TryGetValue(key, out int v))
                return v;
            return 0;
        }
    }
}
