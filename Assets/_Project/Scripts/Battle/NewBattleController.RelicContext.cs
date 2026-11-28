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
        // IRelicBattleContext 구현 (유물이 전투에 영향을 주는 동작)
        // 적에게 빙결 부여 (양수 -> OnStatusApplied로 빙결 연출 트리거)
        public void ApplyFrostToEnemy(int amount)
        {
            if (_enemy == null || amount <= 0) return;
            _enemy.AddStatus("frost", amount);
            Log($"[유물] 적에게 빙결 {amount} 부여");
        }

        // 플레이어 방어도 획득
        public void AddPlayerGuard(int amount)
        {
            if (_player == null || amount <= 0) return;
            _player.AddGuard(amount);
            Log($"[유물] 방어도 +{amount}");
        }

        // 뽑을 더미에 무작위 파편 카드를 섞어 넣음
        public void ShuffleFragmentsIntoDrawPile(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var frag = CardDatabase.CreateFragmentInstance();
                if (frag != null) _deck.AddToDrawShuffled(frag);
            }
            handHud?.Refresh();
            if (count > 0) Log($"[유물] 파편 {count}장 뽑을 더미에 삽입");
        }

        // 카드 드로우
        public void DrawCards(int count)
        {
            if (count > 0) _deck.Draw(count);
        }

        // 특이사항: 패에서 count장을 무작위로 버림
        public void DiscardRandomFromHand(int count)
        {
            if (_deck == null || count <= 0) return;
            int discarded = _deck.DiscardRandomFromHand(count);
            RefillAwakenHand();
            handHud?.Refresh();
            Log($"[특이사항] 패에서 카드 {discarded}장 무작위 버리기");
        }

        // 특이사항: 플레이어가 패에서 count장을 직접 선택해 버림
        public void ForcePlayerDiscard(int count)
        {
            int n = Mathf.Min(count, _deck != null ? _deck.HandCount : 0);
            if (n > 0) EnqueueEnemyForcedDiscard(n);
            Log($"[특이사항] 카드 버리기 {n}장");
        }

        // 특이사항: 각성 게이지(누적 속성 입력) 초기화
        public void ResetAwakenGauge()
        {
            _elementInputCount = 0;
            UpdateAwakenText();
            Log("[특이사항] 각성 게이지 초기화");
        }

        // 특이사항(액괴): 버린 더미에서 무작위 카드 count장을 소멸
        public void ExileRandomDiscardCards(int count)
        {
            if (_deck == null || count <= 0) return;
            var exiled = _deck.ExileRandomFromDiscard(count);
            for (int i = 0; i < exiled.Count; i++)
                _resolver?.NotifyExile(exiled[i]);
            handHud?.Refresh();
            Log($"[특이사항] 버린 더미 카드 {exiled.Count}장 소멸");
        }

        // 적 특이사항으로 들어오는 카드(탈진·둔화·매혹)를 뽑을 더미에 넣는다.
        // 각성 중이면 각성 종료 후 복원될 뽑을 더미(스냅샷)에 넣어, 각성 중엔 뽑히지 않고 끝난 뒤에도 사라지지 않게 한다.
        void AddInsertedCardToDraw(CardInstance card)
        {
            if (card == null) return;
            if (_awakenActive && _awakenSnapshotDraw != null)
            {
                _awakenSnapshotDraw.Insert(Random.Range(0, _awakenSnapshotDraw.Count + 1), card);
                return;
            }
            _deck.AddToDrawShuffled(card);
        }

        // 특이사항: 뽑을 더미에 탈진(무속성 더미) 카드 count장 삽입
        public void InsertExhaustCards(int count)
        {
            if (_deck == null) return;
            int added = 0;
            for (int i = 0; i < count; i++)
            {
                var dummy = CardDatabase.CreateNeutralFillerInstance();
                if (dummy != null) { AddInsertedCardToDraw(dummy); added++; }
            }
            handHud?.Refresh();
            if (added > 0) Log($"[특이사항] 탈진 카드 {added}장 삽입");
        }

        // 특이사항(가마이타치): 뽑을 더미에 둔화(506) 카드 count장 삽입
        public void InsertSlowCards(int count)
        {
            if (_deck == null) return;
            int added = 0;
            for (int i = 0; i < count; i++)
            {
                var slow = CardDatabase.CreateTransientInstance(CardDatabase.SLOW);
                if (slow != null) { AddInsertedCardToDraw(slow); added++; }
            }
            handHud?.Refresh();
            if (added > 0) Log($"[특이사항] 둔화 카드 {added}장 삽입");
            else Debug.LogWarning("[특이사항] 둔화 카드(506)가 카드 DB에 없음");
        }

        // 특이사항(불개): 패에서 무작위 부적 count장 소멸
        public void ExileRandomHandCards(int count)
        {
            if (_deck == null || count <= 0) return;
            int exiled = 0;
            for (int i = 0; i < count && _deck.HandCount > 0; i++)
            {
                CardInstance victim = _deck.Hand[Random.Range(0, _deck.HandCount)];
                _deck.ExileFromHand(victim);
                _resolver?.NotifyExile(victim);
                exiled++;
            }
            handHud?.Refresh();
            if (exiled > 0) Log($"[특이사항] 패의 부적 {exiled}장 소멸");
        }

        // 특이사항(미코시뉴도): 패 전체 부적의 호흡(게이지 코스트)을 amount만큼 증가(이번 전투 동안)
        public void RaiseHandGaugeCost(int amount)
        {
            if (_deck == null || amount == 0 || _deck.HandCount == 0) return;
            for (int i = 0; i < _deck.HandCount; i++)
                if (_deck.Hand[i] != null) _deck.Hand[i].gaugeCostModifier += amount;
            _deck.NotifyChanged();
            Log($"[특이사항] 패 전체 호흡 {amount:+0;-0}");
        }

        // 특이사항: 뽑을 더미에 매혹 카드 count장 삽입(사용 시 적 게이지 가득 참)
        public void InsertCharmCards(int count)
        {
            if (_deck == null) return;
            int added = 0;
            for (int i = 0; i < count; i++)
            {
                var charm = CardDatabase.CreateCharmInstance();
                if (charm != null) { AddInsertedCardToDraw(charm); added++; }
            }
            handHud?.Refresh();
            if (added > 0) Log($"[특이사항] 매혹 카드 {added}장 삽입");
        }

        // 각성 남은시간/최대시간을 함께 증가
        public void AddAwakenTimeSeconds(float seconds)
        {
            if (seconds == 0f) return;
            _awakenTimeRemaining += seconds;
            _awakenMaxTime += seconds;
        }

        // 각성 종료 후 성공 콤보 수만큼 각성 게이지 회복 (즉시 재발동 방지 max-1 상한)
        public void RecoverAwakenGauge(int successCombos)
        {
            _elementInputCount = Mathf.Min(successCombos, EffectiveAwakenInput - 1);
            UpdateAwakenText();
            Log($"[유물] 각성 게이지 {_elementInputCount}/{EffectiveAwakenInput} 회복");
        }

        // 연쇄 스택 증가 (다음 Update의 SyncChainStatus가 UI 반영)
        public void AddChain(int amount)
        {
            if (amount == 0) return;
            _ctx.chainCount += amount;
            Log($"[유물] 연쇄 +{amount} (현재 {_ctx.chainCount})");
        }

        // 무작위 적에게 즉시 피해 (현재 단일 적)
        public void DealDamageToRandomEnemy(int amount)
        {
            if (amount <= 0) return;
            EnemyController target = EnemyCombatParty.PickRandomAlive() ?? _enemy;
            if (target == null) return;
            target.TakeDamage(amount);
            Log($"[유물] 적에게 {amount} 피해");
        }

        // 각성 진입 예약 — 전투 시작 시 OnBattleStart에서 호출됨. 시작 드로우 후 StartBattle이 발동.
        public void ForceActivateAwaken()
        {
            _relicForceAwakenPending = true;
        }

        // 이번 전투 첫 사용 카드의 효과를 한 번 더 발동하도록 예약
        public void EnableFirstCardRecast()
        {
            _relicFirstCardRecastPending = true;
        }

        // 좌상단 유물 아이콘 펄스 연출 후 onShown 호출 (HUD 없으면 즉시 실행)
        public void PulseRelic(Battle.Relic.RelicSO relic, string label, System.Action onShown)
        {
            if (turnOrderTimeline == null) turnOrderTimeline = ResolveOrCreateTurnOrderTimeline();
            turnOrderTimeline?.ShowRelicTrigger(relic); // 행동 순서 바 현재 칸에도 발동 표시
            if (relic != null) BattleLog.RecordRelic(relic.icon, relic.DisplayLabel);
            if (RelicHUD.Instance != null && relic != null)
                RelicHUD.Instance.PulseRelicIcon(relic, label, onShown);
            else
                onShown?.Invoke();
        }
    }
}
