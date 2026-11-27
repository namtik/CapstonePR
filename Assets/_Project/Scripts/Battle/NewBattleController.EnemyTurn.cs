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
        // 적에게 상태이상이 '부여'될 때 — 빙결이면 그 적 위치에서 연출 재생
        void OnEnemyStatusAppliedHandler(EnemyController enemy, string type, int amount)
        {
            if (amount <= 0 || type != "frost") return;
            if (effectOverlay == null || string.IsNullOrEmpty(frostAppliedEffectName)) return;

            Vector2 pos = effectOverlay.EnemyAnchoredPos;
            if (enemy != null && enemy.TryGetAimScreen(out Vector2 screen, out _)
                && effectOverlay.TryAnchoredFromScreen(screen, out Vector2 local))
                pos = local;

            bool playSfx = _frostFxSfxFrame != Time.frameCount;
            if (playSfx) _frostFxSfxFrame = Time.frameCount;
            effectOverlay.PlayByNameAtPosition(frostAppliedEffectName, pos, playSfx);
        }

        // EnemyController가 공격 종료 시 호출 — 다음 카드에 적 공격 후 플래그 적용
        public void NotifyEnemyAttackFinished()
        {
            _ctx.firstCardAfterEnemyAttack = true;
        }

        // 적 공격 발생 시 — 강화 발현 처리·각성 게이지 -(공격한 적의 흡수수치)·땅424 방어도 획득
        void OnEnemyAttackFiredHandler(EnemyStat attacker)
        {
            _ctx.firstCardAfterEnemyAttack = true;

            if (_track != null && _track.action == 3) ResolveTrack(EnhanceReward(_track.playerHpAtFire));

            if (!_awakenActive)
            {
                int drain = attacker != null && attacker.Data != null ? Mathf.Max(0, attacker.Data.흡수수치) : ATTACK_AWAKEN_DRAIN_DEFAULT;
                _elementInputCount = Mathf.Max(0, _elementInputCount - drain);
                UpdateAwakenText();
            }

            if (_ctx.blockOnEnemyAttackActive && _ctx.blockOnEnemyAttackAmount > 0 && _player != null)
                _player.AddGuard(_ctx.blockOnEnemyAttackAmount);
        }

        void BindEnemyFromHudOrAlive()
        {
            EnemyController aimed = handHud != null ? handHud.PendingEnemyTarget : null;
            // 특이사항(도로타보): 도발 적이 살아 있으면 단일 대상은 반드시 그 적
            EnemyController taunt = EnemyCombatParty.FirstAliveTaunt();
            if (taunt != null)
            {
                if (aimed != null && aimed != taunt) Log("[특이사항] 도발 — 우선 공격 대상으로 변경");
                BindEnemy(taunt);
                return;
            }
            if (aimed != null && aimed.IsAlive)
            {
                BindEnemy(aimed);
                return;
            }
            if (_enemy == null || !_enemy.IsAlive)
                BindEnemy(EnemyCombatParty.FirstAlive());
        }

        void BindEnemy(EnemyController enemy)
        {
            _enemy = enemy;
            _enemyStat = enemy != null ? enemy.GetComponent<EnemyStat>() : null;
            _ctx.enemy = _enemy;
            _ctx.enemyStat = _enemyStat;
            EnemyCombatParty.CurrentTarget = enemy;
        }

        void BindParty()
        {
            UnbindPartyEvents();
            EnemyCombatParty.CollectAlive(_aliveBuf);
            if (_aliveBuf.Count == 0)
            {
                EnemyController[] found = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
                for (int i = 0; i < found.Length; i++)
                {
                    if (found[i] != null && found[i].IsAlive)
                    {
                        EnemyCombatParty.Register(found[i]);
                        _aliveBuf.Add(found[i]);
                    }
                }
            }

            for (int i = 0; i < _aliveBuf.Count; i++)
            {
                EnemyController e = _aliveBuf[i];
                EnemyStat stat = e.GetComponent<EnemyStat>();
                if (stat != null)
                {
                    stat.OnGaugeFullBy -= OnEnemyAttackFiredHandler;
                    stat.OnGaugeFullBy += OnEnemyAttackFiredHandler;
                }
                e.OnStatusApplied -= OnEnemyStatusAppliedHandler;
                e.OnStatusApplied += OnEnemyStatusAppliedHandler;
                _boundEnemies.Add(e);
            }

            BindEnemy(_aliveBuf.Count > 0 ? _aliveBuf[0] : null);
        }

        void UnbindPartyEvents()
        {
            for (int i = 0; i < _boundEnemies.Count; i++)
            {
                EnemyController e = _boundEnemies[i];
                if (e == null) continue;
                EnemyStat stat = e.GetComponent<EnemyStat>();
                if (stat != null) stat.OnGaugeFullBy -= OnEnemyAttackFiredHandler;
                e.OnStatusApplied -= OnEnemyStatusAppliedHandler;
            }
            _boundEnemies.Clear();
        }

        // 게이지가 동시에 찬 적들의 공격을 한 마리씩 처리한다.
        public void EnqueueEnemyAttack(EnemyController source)
        {
            if (source == null) return;
            _attackQueue.Enqueue(new EnemyAction { enemy = source, disruption = false });
            TryProcessAttackQueue();
        }

        // 방해행동도 공격과 같은 대기열로 — 카드 연출/이전 행동이 끝난 뒤 차례대로 발동
        public void EnqueueEnemyDisruption(EnemyController source)
        {
            if (source == null) return;
            _attackQueue.Enqueue(new EnemyAction { enemy = source, disruption = true });
            TryProcessAttackQueue();
        }

        // 콤보(행동게이지 초기화): 대기열에서 이 적의 아직 시작 안 된 공격·방해 행동을 취소. 취소 수 반환
        public int CancelQueuedActions(EnemyController source)
        {
            if (source == null || _attackQueue.Count == 0) return 0;
            int before = _attackQueue.Count;
            var keep = new List<EnemyAction>(_attackQueue);
            keep.RemoveAll(a => a.enemy == source);
            _attackQueue.Clear();
            foreach (var a in keep) _attackQueue.Enqueue(a);
            int cancelled = before - _attackQueue.Count;
            if (cancelled > 0) Log($"[콤보] {source.name} 예약 행동 {cancelled}건 취소(게이지 초기화)");
            return cancelled;
        }

        void TryProcessAttackQueue()
        {
            if (_attackStartRoutine != null) return; // 이미 대기열 처리 중
            if (_attackQueue.Count == 0) return;
            if (!isActiveAndEnabled)
            {
                // 코루틴을 못 돌리는 상황(비활성) — 즉시 순차 실행 폴백
                while (_attackQueue.Count > 0)
                {
                    var a = _attackQueue.Dequeue();
                    if (a.enemy == null || !a.enemy.IsAlive) continue;
                    if (a.disruption) a.enemy.RunMidPatternNow();
                    else a.enemy.BeginQueuedAttack(null);
                }
                return;
            }
            _attackStartRoutine = StartCoroutine(ProcessEnemyActionQueue());
        }

        // 대기열을 하나씩 처리: 카드 연출·피격 반응 종료 대기, 행동자 표시 대기, 행동 실행, 완료까지 대기 순서
        IEnumerator ProcessEnemyActionQueue()
        {
            while (_attackQueue.Count > 0)
            {
                // 이전 동작이 완전히 끝날 때까지 대기(카드 중앙 연출 퇴장까지 + 적 피격 반응)
                yield return WaitPlayerPresentationsFinished(_attackQueue.Peek().enemy);
                if (!_inBattle) break;
                if (_attackQueue.Count == 0) break;

                EnemyAction a = _attackQueue.Dequeue();
                EnemyController e = a.enemy;
                if (e == null || !e.IsAlive) continue;

                _attackResolving = true;
                _actingEnemy = e;
                _actingIsDisruption = a.disruption;
                BindEnemy(e);
                // 행동하는 적 쪽으로 카메라를 당겨 누가 행동하는지 강조(행동 시작 대기 동안 들어감)
                EnemyActionFocusCamera.Focus(e.transform, enemyActionFocus);
                HideHudForEnemyAction();

                // 행동 순서 바에서 행동자가 '현재' 칸으로 올라오는 걸 보여준 뒤 행동 시작
                if (enemyTurnStartDelay > 0f) yield return new WaitForSeconds(enemyTurnStartDelay);
                if (!_inBattle) break;
                if (e == null || !e.IsAlive) { _attackResolving = false; continue; }
                BindEnemy(e);

                if (a.disruption)
                {
                    e.RunMidPatternNow();
                    // 방해행동 연출 잠금 시간만큼 다음 행동을 미룸
                    float wait = Mathf.Max(disruptionLockSeconds, _enemyActionLockUntil - Time.time);
                    if (wait > 0f) yield return new WaitForSeconds(wait);
                    // '버리기'처럼 선택을 요구하는 방해는 선택이 끝날 때까지 적 행동으로 유지(그동안 카드 사용 잠금)
                    while (_inBattle && HasPendingSelection) yield return null;
                }
                else
                {
                    bool done = false;
                    e.BeginQueuedAttack(() => done = true);
                    // 적이 파괴되면 OnDestroy에서 완료 콜백이 호출됨. 혹시 모를 정지 대비 상한 대기.
                    float deadline = Time.time + 10f;
                    while (!done && Time.time < deadline) yield return null;
                }
                _attackResolving = false;
                // 행동이 완전히 끝난 뒤 체력 손실 유물 발동(다음 행동은 그 연출이 끝날 때까지 대기)
                FlushDeferredHpLost();
            }
            _attackResolving = false;
            FlushDeferredHpLost();
            _attackStartRoutine = null;
            // 대기열이 모두 끝나면 원래 시점으로(연속 행동 사이에는 돌아가지 않고 다음 적으로 옮겨 감)
            EnemyActionFocusCamera.Release();
            EnemyActionHudFader.Show();
        }

        // 적 행동 동안 손패·덱·묘지·몬스터 HP 바를 숨긴다(카드 선택이 필요하면 그동안은 보임)
        void HideHudForEnemyAction()
        {
            if (!enemyActionHudHide.enabled) return;
            // 카메라 포커스가 실제로 걸리는 경우에만(2D 폴백·단일 적 생략 설정과 맞춤)
            if (!enemyActionFocus.enabled) return;
            if (enemyActionFocus.onlyWhenMultiple && !EnemyCombatParty.IsMulti) return;
            if (BattleStageController.Current == null || BattleStageController.Current.StageCamera == null) return;

            _hudHideTargets.Clear();
            if (enemyActionHudHide.hideHand && handHud != null && handHud.HandRoot != null)
                _hudHideTargets.Add(handHud.HandRoot.gameObject);
            if (enemyActionHudHide.hidePiles && handHud != null)
                handHud.CollectPileRoots(_hudHideTargets);
            if (enemyActionHudHide.hidePiles)
                DrawButtonUI.CollectHideTargets(_hudHideTargets); // 드로우 버튼·D 키 아이콘도 덱·묘지와 함께
            if (enemyActionHudHide.hideEnemyHp)
                EnemyOverlayHUD.CollectHpBars(_hudHideTargets);
            if (enemyActionHudHide.extraTargets != null)
                foreach (var go in enemyActionHudHide.extraTargets)
                    if (go != null && !_hudHideTargets.Contains(go)) _hudHideTargets.Add(go);

            EnemyActionHudFader.Hide(_hudHideTargets, enemyActionHudHide, () => HasPendingSelection);
        }

        // 플레이어 카드 사용 연출(등장부터 퇴장까지), 유물(아이템) 발동 연출, 대상 적의 피격 반응이 모두 끝날 때까지 대기
        IEnumerator WaitPlayerPresentationsFinished(EnemyController next)
        {
            // 카드 효과 선택창(소멸/버리기/복사/픽커)이 열려 있으면 고를 때까지 적 행동을 미룸 —
            // 선택 도중 방해 '버리기'가 끼어들어 선택 흐름이 꼬이지 않게(선택은 플레이어가 반드시 끝냄)
            while (_inBattle && HasPendingSelection) yield return null;

            float deadline = Time.time + 5f; // 안전 상한
            while (Time.time < deadline)
            {
                bool presenting = _cardPresenting || (handHud != null && handHud.IsCardUsePresenting);
                bool relicShowing = (RelicHUD.Instance != null && RelicHUD.Instance.IsPulsing)
                                    || (turnOrderTimeline != null && turnOrderTimeline.IsShowingRelicTrigger);
                bool hitReacting = next != null && next.HitReactionRemaining > 0f;
                if (!presenting && !relicShowing && !hitReacting) yield break;
                yield return null;
            }
        }

        // 적 턴 상태(공격 대기열·행동자·잠금) 초기화
        void ResetEnemyTurnState()
        {
            if (_attackStartRoutine != null)
            {
                StopCoroutine(_attackStartRoutine);
                _attackStartRoutine = null;
            }
            _attackResolving = false;
            _attackQueue.Clear();
            _deferredHpLost.Clear();
            EnemyActionFocusCamera.Release(immediate: true); // 전투 시작/종료 — 카메라 오프셋 즉시 원복
            EnemyActionHudFader.Show(immediate: true);
            CancelCurseNudges();
            _actingEnemy = null;
            _actingIsDisruption = false;
            _enemyActionLockUntil = 0f;
        }
    }
}
