using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using Battle.Card;
using Battle.Deck;
using Battle.Relic;
using Battle.UI;
using Sashindo.VFX;

namespace Battle
{
    public partial class NewBattleController
    {
        // 현재 적의 허용 방해행동 마스크(길이 6). EnemyData.disruptionActions 기준, 비면 null(전체 허용).
        bool[] CurrentEnemyDisruptionMask()
        {
            var data = _enemyStat != null ? _enemyStat.Data : null;
            if (data == null || data.disruptionActions == null || data.disruptionActions.Length == 0)
                return null;
            return data.BuildDisruptionMask();
        }

        // 직전 방해행동을 제외한 허용 마스크(길이 6). 연속 발동을 막되, 제외 후 남는 행동이 없으면 제외하지 않는다.
        bool[] DisruptionAllowedExcludingLast()
        {
            bool[] baseMask = CurrentEnemyDisruptionMask(); // null = 6종 전체 허용
            bool[] mask = new bool[6];
            int allowedCount = 0;
            for (int i = 0; i < 6; i++)
            {
                mask[i] = baseMask == null || (i < baseMask.Length && baseMask[i]);
                if (mask[i]) allowedCount++;
            }
            // 직전 행동 제외 — 단, 남는 행동이 하나라도 있을 때만(전부 막히는 것 방지)
            if (_lastDisruptionAction >= 0 && _lastDisruptionAction < 6 &&
                mask[_lastDisruptionAction] && allowedCount > 1)
                mask[_lastDisruptionAction] = false;
            return mask;
        }

        // AI 미사용/폴백 — 가용 행동 중 무작위 1개 선택(몬스터 허용 방해행동으로 제한)
        int RandomFallbackPick(bool recoverReady, bool[] allowed)
        {
            bool Ok(int a) => allowed == null || (a < allowed.Length && allowed[a]);

            var pool = new List<int>();
            if (Ok(0)) pool.Add(0);
            if (Ok(1)) pool.Add(1);
            if (Ok(2)) pool.Add(2);
            if (Ok(3)) pool.Add(3);
            if (recoverReady && Ok(4)) pool.Add(4);
            if (_deck.HandCount >= 2 && Ok(5)) pool.Add(5);
            if (pool.Count == 0) pool.Add(1); // 전부 막히면 탈진 폴백
            return pool[Random.Range(0, pool.Count)];
        }

        void LogEnemyDisruption(int action)
        {
            Sprite portrait = _enemyStat != null && _enemyStat.Data != null ? _enemyStat.Data.enemySprite : null;
            string enemyName = _enemyStat != null && _enemyStat.Data != null ? _enemyStat.Data.enemyName : null;
            BattleLog.RecordDisruption(action, portrait, EnemyCombatParty.AliveIndexOf(_enemy), enemyName);
        }

        // 적 방해행동 발동 — AI(또는 랜덤)로 1개 선택 후 분기 실행, 보상 추적 시작
        public string TriggerDisruption() => TriggerDisruption(_enemy);

        public string TriggerDisruption(EnemyController source)
        {
            if (source != null) BindEnemy(source);
            // 방해행동도 적의 '행동' — 연출 동안 행동 순서 바 맨 위에 올리고 입력을 잠근다(공격 처리 중이면 공격자 유지)
            if (!_attackResolving)
            {
                _actingEnemy = source != null ? source : _enemy;
                _actingIsDisruption = true;
            }
            _enemyActionLockUntil = Mathf.Max(_enemyActionLockUntil, Time.time + disruptionLockSeconds);
            bool recoverReady = _recoverCooldown <= 0;
            // 직전 방해행동을 제외해 연속 발동을 막는다(AI·랜덤 폴백 공통 마스크)
            bool[] allowedMask = DisruptionAllowedExcludingLast();
            int pick;
            Battle.AI.AiDecision? rewardDecision = null;
            if (useAiDisruption && Battle.AI.EnemyAiModel.Loaded)
            {
                try
                {
                    // self-state(상태의존 마스크용): 이미 건 저주/강화, 현재 각성 게이지
                    bool curseActive = _curses.Count > 0;
                    bool enhanceActive = _enemy != null && _enemy.IsNextAttackBuffed;
                    var decision = Battle.AI.EnemyDisruptionAI.Decide(_deck, _enemyStat, _player, recoverReady, aiExplorationEpsilon, onlineLearning, curseActive, enhanceActive, _elementInputCount, allowedMask);
                    pick = decision.action;
                    if (logVerbose)
                        Log($"[적AI] μ=[{string.Join(",", System.Array.ConvertAll(decision.membership, v => v.ToString("F2")))}]" +
                            $" -> 선택={Battle.AI.EnemyAiModel.ActionNames[pick]}{(decision.explored ? " (탐험)" : "")}");
                    if (logAiDecisions) Battle.AI.DisruptionLogger.Log(decision);
                    rewardDecision = decision;
                    if (aiRewardGamma > 0f) TdOnDecision(decision);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[적AI] 추론 실패 -> 랜덤 폴백: {e.Message}");
                    pick = RandomFallbackPick(recoverReady, allowedMask);
                }
            }
            else
            {
                pick = RandomFallbackPick(recoverReady, allowedMask);
            }

            // [테스트] StageTestPanel이 지정한 방해행동으로 교체(AI 학습 보상에는 반영하지 않음)
            if (_debugForcedDisruption.HasValue)
            {
                pick = Mathf.Clamp(_debugForcedDisruption.Value, 0, 5);
                _debugForcedDisruption = null;
                rewardDecision = null;
                Log($"[테스트] 방해행동 강제 -> {pick}");
            }

            // 다음 방해행동이 이번 것과 연속되지 않도록 직전 행동으로 기록
            _lastDisruptionAction = pick;

            // 방해행동 종류에 맞는 몬스터 동작 애니메이션(3 강화·4 회복=버프 / 0 저주·1 탈진·2 흡수=디버프)
            _enemy?.PlayDisruptionMotion(pick == 3 || pick == 4);

            // 회복 쿨다운 진행 — 회복이 아닌 행동이 발동되면 1 감소
            if (pick != 4 && _recoverCooldown > 0) _recoverCooldown--;

            LogEnemyDisruption(pick);

            switch (pick)
            {
                case 0: // 저주 — 한 속성 카드 사용 시 플레이어 피해(2회 지속)
                {
                    CardElement[] elements = { CardElement.Fire, CardElement.Water, CardElement.Wind, CardElement.Earth };
                    CardElement target = elements[Random.Range(0, elements.Length)];
                    string curseSource = _enemyStat != null && _enemyStat.Data != null ? _enemyStat.Data.enemyName : null;
                    ApplyElementCurse(target, CURSE_DURATION_USES, curseSource);
                    SfxManager.Instance?.PlayCurse();
                    if (rewardDecision.HasValue) BeginTrack(rewardDecision.Value, 0);
                    Log($"[방해] {target} 속성 저주 — 카드 사용 {CURSE_DURATION_USES}회 동안 지속");
                    return $"방해: {ElementName(target)} 부적 저주!";
                }
                case 1: // 탈진 — 소멸 무속성 더미 카드(500)를 버린 카드 더미에 넣음
                {
                    var dummy = CardDatabase.CreateNeutralFillerInstance();
                    if (dummy != null) _deck.AddToDiscard(dummy);
                    handHud?.Refresh();
                    SfxManager.Instance?.PlayExhaust();
                    if (rewardDecision.HasValue) { BeginTrack(rewardDecision.Value, 1); if (_track != null) _track.exhaustCard = dummy; }
                    Log("[방해] 탈진 — 소멸 무속성 카드 1장 버린 더미에 삽입");
                    return "방해: 탈진 부적 삽입!";
                }
                case 2: // 흡수 — 각성 게이지를 ABSORB_AMOUNT만큼 감소  [즉시형]
                {
                    int absorb = ABSORB_AMOUNT;
                    int before = _elementInputCount;
                    _elementInputCount = Mathf.Max(0, _elementInputCount - absorb);
                    UpdateAwakenText();
                    SfxManager.Instance?.PlayAbsorb();
                    if (rewardDecision.HasValue) ObserveDisruption(rewardDecision.Value, 2, AbsorbReward(before));
                    Log($"[방해] 흡수 — 각성 게이지 -{absorb} ({before} -> {_elementInputCount})");
                    return "방해: 각성 게이지 흡수!";
                }
                case 3: // 강화 — 적 다음 공격 1회 증가(+50%)
                {
                    _enemy?.BuffNextAttack();
                    SfxManager.Instance?.PlayEnhance();
                    if (rewardDecision.HasValue) { BeginTrack(rewardDecision.Value, 3); if (_track != null) _track.playerHpAtFire = _player != null ? _player.currentHp : 0; }
                    Log("[방해] 강화 — 적 다음 공격 피해 증가(1회)");
                    return "방해: 강화!";
                }
                case 4: // 회복 — 적 HP 10% 회복, 재사용까지 다른 방해행동 2회 필요  [즉시형]
                {
                    _recoverCooldown = 2;
                    float enemyHpBefore = _enemyStat != null ? _enemyStat.currentHp : 0f;
                    if (_enemyStat != null)
                    {
                        float heal = _enemyStat.maxHp * RECOVER_RATIO;
                        _enemyStat.Heal(heal);
                        Log($"[방해] 회복 — 적 HP +{heal:F0} (최대 {RECOVER_RATIO * 100f:F0}%)");
                    }
                    SfxManager.Instance?.PlayHeal();
                    if (rewardDecision.HasValue) ObserveDisruption(rewardDecision.Value, 4, RecoverReward(enemyHpBefore));
                    return "방해: 회복!";
                }
                default: // 5 버리기 — 패에서 무작위 1장 버림(가라카사오바케는 2장)  [즉시형]
                {
                    int handBefore = _deck != null ? _deck.HandCount : 0;
                    int discardCount = DISCARD_DISRUPTION_COUNT;
                    // 특이사항(가라카사오바케): 버리기 방해 발동 시 무작위로 부적 2장을 버리게 한다
                    if (_enemy != null && _enemy.Ability == EnemySpecialAbility.DisruptionDiscardRandom)
                        discardCount = DISRUPTION_DISCARD_RANDOM_ABILITY_COUNT;
                    int n = Mathf.Min(discardCount, _deck != null ? _deck.HandCount : 0);
                    if (n > 0) DiscardRandomFromHand(n);
                    SfxManager.Instance?.PlayDiscard();
                    if (rewardDecision.HasValue) ObserveDisruption(rewardDecision.Value, 5, DiscardReward(handBefore));
                    Log($"[방해] 버리기 — 패에서 무작위 {n}장 버림");
                    return "방해: 부적 무작위 버리기!";
                }
            }
        }

        // 방해(버리기): 플레이어가 패에서 count장을 직접 선택해 버림.
        // 선택 대기열로 넣어, 카드 효과 선택창과 겹쳐도 콜백이 덮어써지지 않고 차례대로 열린다.
        void EnqueueEnemyForcedDiscard(int count)
        {
            if (handHud == null) return;
            // 빈 칸 count개를 한 번에 채워 확정(패가 count장보다 적으면 그 수만큼)
            EnqueueHandMultiSelection($"버릴 카드를 선택하세요 ({count}장)", count, cards =>
            {
                if (cards == null) return;
                foreach (var card in cards)
                    if (card != null) _deck.DiscardCardFromHand(card);
                RefillAwakenHand();
            }, null);

        }

        // 지연형(저주/탈진/강화) 추적 시작 (직전 미발현 추적은 먼저 마감)
        void BeginTrack(Battle.AI.AiDecision decision, int action)
        {
            ResolveTrackUnfired();
            _track = new DisruptionTrack { decision = decision, action = action };
        }

        // 즉시형(흡수/회복/버리기): 발동 시점 가치로 보상 확정 후 학습/게시
        void ObserveDisruption(Battle.AI.AiDecision decision, int action, float reward)
        {
            reward = Mathf.Clamp(reward, -1f, 1f);
            if (aiRewardGamma > 0f) { TdSetReward(decision, reward, immediate: true); return; }
            if (onlineLearning) Battle.AI.OnlineQLearner.Observe(decision.membership, decision.context, action, reward);
            float drift = Battle.AI.OnlineQLearner.WeightDriftFromBase();
            Battle.AI.AiDebug.PublishReward(action, reward, true, drift);
            if (logVerbose)
                Log($"[적AI{(onlineLearning ? "학습" : "관찰")}] {Battle.AI.EnemyAiModel.ActionNames[action]} r={reward:+0.00;-0.00} (즉시) drift={drift:F3}");
        }

        // 지연형 발현 -> 보상 확정 후 학습/게시
        void ResolveTrack(float reward)
        {
            if (_track == null) return;
            var t = _track; _track = null;
            reward = Mathf.Clamp(reward, -1f, 1f);
            if (aiRewardGamma > 0f) { TdSetReward(t.decision, reward, immediate: false); return; }
            if (onlineLearning) Battle.AI.OnlineQLearner.Observe(t.decision.membership, t.decision.context, t.action, reward);
            float drift = Battle.AI.OnlineQLearner.WeightDriftFromBase();
            Battle.AI.AiDebug.PublishReward(t.action, reward, false, drift);
            if (logVerbose)
                Log($"[적AI{(onlineLearning ? "학습" : "관찰")}] {Battle.AI.EnemyAiModel.ActionNames[t.action]} r={reward:+0.00;-0.00} (발현) drift={drift:F3}");
        }

        // 새 방해결정: 직전 전이들의 s'를 채우고 완성분 학습 후 새 전이 적재
        void TdOnDecision(Battle.AI.AiDecision decision)
        {
            for (int i = 0; i < _tdQueue.Count; i++)
                if (!_tdQueue[i].hasNext)
                {
                    _tdQueue[i].nextMu = decision.membership;
                    _tdQueue[i].nextCtx = decision.context;
                    _tdQueue[i].hasNext = true;
                }
            TdFlush();
            _tdQueue.Add(new TdTransition { mu = decision.membership, ctx = decision.context, action = decision.action });
        }

        // 같은 결정의 전이에 보상을 채움(완성되면 학습)
        void TdSetReward(Battle.AI.AiDecision decision, float reward, bool immediate)
        {
            for (int i = 0; i < _tdQueue.Count; i++)
                if (!_tdQueue[i].hasReward && ReferenceEquals(_tdQueue[i].mu, decision.membership))
                {
                    _tdQueue[i].reward = reward;
                    _tdQueue[i].hasReward = true;
                    _tdQueue[i].immediate = immediate;
                    break;
                }
            TdFlush();
        }

        // 보상과 s'가 모두 확정된 전이를 TD 학습 후 큐에서 제거
        void TdFlush()
        {
            for (int i = _tdQueue.Count - 1; i >= 0; i--)
            {
                var t = _tdQueue[i];
                if (!(t.hasReward && t.hasNext)) continue;
                if (onlineLearning) Battle.AI.OnlineQLearner.ObserveTD(t.mu, t.ctx, t.action, t.reward, t.nextMu, t.nextCtx, aiRewardGamma);
                float drift = Battle.AI.OnlineQLearner.WeightDriftFromBase();
                Battle.AI.AiDebug.PublishReward(t.action, t.reward, t.immediate, drift);
                if (logVerbose)
                    Log($"[적AI{(onlineLearning ? "학습" : "관찰")}] {Battle.AI.EnemyAiModel.ActionNames[t.action]} r={t.reward:+0.00;-0.00} " +
                        $"(TD γ={aiRewardGamma:F2}{(t.immediate ? ", 즉시" : ", 발현")}) drift={drift:F3}");
                _tdQueue.RemoveAt(i);
            }
        }

        // 전투 종료/리셋: 남은 전이를 터미널(s'=null)로 학습, 보상 미확정분은 버림
        void TdFlushTerminal()
        {
            for (int i = 0; i < _tdQueue.Count; i++)
            {
                var t = _tdQueue[i];
                if (!t.hasReward) continue;
                if (onlineLearning) Battle.AI.OnlineQLearner.ObserveTD(t.mu, t.ctx, t.action, t.reward, null, null, aiRewardGamma);
                float drift = Battle.AI.OnlineQLearner.WeightDriftFromBase();
                Battle.AI.AiDebug.PublishReward(t.action, t.reward, t.immediate, drift);
                if (logVerbose)
                    Log($"[적AI학습] {Battle.AI.EnemyAiModel.ActionNames[t.action]} r={t.reward:+0.00;-0.00} (TD 터미널) drift={drift:F3}");
            }
            _tdQueue.Clear();
        }

        // 발현 못 하고 끝난 지연형 추적 마감(저주는 자해/회피분, 그 외 미발현 음수)
        void ResolveTrackUnfired()
        {
            if (_track == null) return;
            if (_track.action == 0) ResolveTrack(CurseReward(_track.curseHits));
            else ResolveTrack(UNFIRED_REWARD);
        }

        // 지연형 추적 경과 시간 누적, 타임아웃 시 미발현 마감
        void TickDisruptionTrack()
        {
            if (_track == null) return;
            _track.elapsed += Time.deltaTime;
            if (_track.elapsed >= disruptionTrackTimeout) ResolveTrackUnfired();
        }

        // 흡수 보상: 발동 직전 각성 근접도(임박할수록 가치 증가)
        float AbsorbReward(int awakenBefore)
        {
            float prox = EffectiveAwakenInput > 0 ? Mathf.Clamp01(awakenBefore / (float)EffectiveAwakenInput) : 0f;
            return Mathf.Clamp(prox * 2f - 0.6f, -1f, 1f);
        }

        // 회복 보상: 발동 직전 적 빈사도(빈사일수록 가치 증가, 만피는 음수)
        float RecoverReward(float enemyHpBefore)
        {
            float ratio = (_enemyStat != null && _enemyStat.maxHp > 0f) ? Mathf.Clamp01(enemyHpBefore / _enemyStat.maxHp) : 1f;
            return Mathf.Clamp((1f - ratio) * 2f - 1f, -1f, 1f);
        }

        // 버리기 보상: 발동 직전 손패 압박(가득 찰수록 가치 증가, 한도 5 기준)
        float DiscardReward(int handBefore)
        {
            float pressure = Mathf.Clamp01(handBefore / 5f);
            return Mathf.Clamp(pressure * 2f - 0.8f, -1f, 1f);
        }

        // 저주 보상: 자해 유도 시 강한+, 회피(제약 성공)는 약한+ (HP 못 깎아도 실패 아님)
        float CurseReward(int hits)
        {
            if (hits <= 0) return 0.2f;
            return Mathf.Clamp(0.3f + hits * 0.45f, -1f, 1f);
        }

        // 강화 보상: 발현 자체 +0.4, 그 사이 받은 HP 피해만큼 가산
        float EnhanceReward(int playerHpAtFire)
        {
            float lost = (_player != null && _player.maxHp > 0)
                ? Mathf.Clamp01((playerHpAtFire - _player.currentHp) / (float)_player.maxHp) : 0f;
            return Mathf.Clamp(0.4f + lost * 3f, -1f, 1f);
        }
    }
}
