using System;
using UnityEngine;
using Battle.Card;

namespace Battle
{
    // 콤보 효과 실행에 필요한 런타임 참조/카운터 컨텍스트
    public class ComboResolveContext
    {
        public EnemyController enemy;          // 적 컨트롤러
        public EnemyStat enemyStat;            // 적 스탯
        public Player player;                  // 플레이어
        public Deck.CardDeckSystem deck;       // 덱 시스템

        public int usedFireCardCount;          // 사용한 불 카드 수
        public int usedFragmentCardCount;      // 사용한 파편 카드 수
        public int playerChain;                // 현재 연쇄 보유량
        public int awakenComboCount;           // 이번 각성에서 발동한 콤보 수
        public int awakenFragmentExhausted;    // 각성 진입 시 격리된 파편 수
        public int selfRepeatCount;            // 이 콤보가 이번 각성에 반복된 횟수

        public Action<int> onGainChain;        // 플레이어 연쇄 증가 위임 콜백
    }

    // 데이터 드리븐 콤보 효과 엔진(간소화판)
    public class ComboEffectResolver
    {
        private int _lastDiscardedCount; // 마지막으로 버린 카드 수(연계 공식용)

        // 콤보 효과 전체를 조건/반복 평가 후 실행
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

        // 효과 발동 조건 통과 여부 판정
        bool PassesCondition(ComboEffectData eff, ComboResolveContext ctx)
        {
            string cond = (eff.ifCond ?? "").Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(cond)) return true;

            switch (cond)
            {
                case "ENEMY_KILLED_BY_THIS_COMBO":
                    return false;
                default:
                    Debug.LogWarning($"[ComboEffect] 알 수 없는 조건 '{eff.ifCond}' (ref={eff.refComboId}) — 무시하고 실행");
                    return true;
            }
        }

        // 효과 반복 횟수 계산
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

        // 효과 동사 1건 실행
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

        // 미지원 동사 스킵 경고 출력
        void WarnSkip(string verb, ComboEffectData eff)
            => Debug.LogWarning($"[ComboEffect] '{verb}' 미지원(단일 적/프로토타입) — 스킵 (ref={eff.refComboId})");

        // 효과 수치 공식 평가
        int EvalAmount(ComboEffectData eff, ComboResolveContext ctx)
        {
            string f = (eff.formula ?? "").Trim();
            if (string.IsNullOrEmpty(f)) return eff.amount;

            if (f.IndexOf(':') >= 0 && f.ToUpperInvariant().StartsWith("ENEMY_STATUS_VALUE"))
            {
                string st = f.Substring(f.IndexOf(':') + 1).Trim().ToLowerInvariant();
                return EnemyStatus(ctx, st);
            }

            string upper = f.ToUpperInvariant();
            if (upper.StartsWith("MAX_HP*"))
            {
                string mul = upper.Substring("MAX_HP*".Length).Trim();
                if (float.TryParse(mul, out float m) && ctx.player != null)
                    return Mathf.FloorToInt(ctx.player.maxHp * m);
                return eff.amount;
            }

            int plus = f.IndexOf('+');
            if (plus > 0)
            {
                string left = f.Substring(0, plus).Trim();
                string right = f.Substring(plus + 1).Trim();
                int baseVal = int.TryParse(left, out int bv) ? bv : 0;
                return baseVal + TokenValue(right, ctx, eff.amount);
            }

            return TokenValue(f, ctx, eff.amount);
        }

        // 공식 토큰을 수치로 변환
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

        // 적 상태이상 수치 조회
        int EnemyStatus(ComboResolveContext ctx, string key)
        {
            if (ctx.enemyStat != null && ctx.enemyStat.statusEffects != null &&
                ctx.enemyStat.statusEffects.TryGetValue(key, out int v))
                return v;
            return 0;
        }
    }
}
