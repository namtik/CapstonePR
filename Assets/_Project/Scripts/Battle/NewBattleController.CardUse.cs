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
        // 카드 사용 통계(공격/파편/불/이름별·런 프로파일) 갱신
        void UpdateUsageStats(CardInstance card)
        {
            if (card == null) return;
            if (card.Type == CardType.Attack) _ctx.usedAttackCardCount++;
            // 파편 카드는 element==Fragment(Card_DB v11~) 또는 FRAGMENT 태그로 식별
            if (card.data != null && (card.Element == CardElement.Fragment || card.data.HasTag("FRAGMENT")))
                _ctx.usedFragmentCardCount++;
            if (card.Element == CardElement.Fire) _usedFireCardCount++;

            string name = card.data.displayName ?? "";
            if (!string.IsNullOrEmpty(name))
            {
                _ctx.usedCardNameCounts.TryGetValue(name, out int cur);
                _ctx.usedCardNameCounts[name] = cur + 1;
            }

            RunDeckState.Instance?.RecordCardUse(Battle.AI.EnemyDisruptionAI.Classify(card.data), card.data.gauge);
        }

        // 키 입력 처리 (D 드로우)
        void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                if (tabPanel == null) tabPanel = ResolveTabPanel();
                tabPanel?.Toggle();
            }

            if (Input.GetKeyDown(KeyCode.D))
                RequestDDraw();
        }

        // D 드로우 공용 진입점 — D 키와 드로우 버튼(DrawButtonUI)이 함께 사용
        public bool RequestDDraw()
        {
            if (!CanDDraw) return false;
            TryDDraw();
            return true;
        }

        // D 드로우 시도 — 1장 뽑고 적 게이지 +1 (비급서 10011 보유 시 패 전부 버리고 5장)
        void TryDDraw()
        {
            BattleLog.BeginPlayerDraw();
            try
            {
                // 유물(비급서 10011): D 드로우가 "현재 패 모두 버리고 5장 뽑기"로 교체
                if (RelicManager.Instance != null && RelicManager.Instance.HasDDrawOverride())
                {
                    int discarded = _deck.DiscardAllFromHand();
                    int redrawn = _deck.Draw(5, true);
                    int gaugeCost = DDrawGaugeCost; // 비급서: 코스트 3
                    if (redrawn > 0) AccrueEnemyGauge(gaugeCost);
                    handHud?.Refresh();
                    Log($"[유물] 비급서 — 패 {discarded}장 버리고 {redrawn}장 드로우, 적 게이지 +{gaugeCost}");
                    return;
                }

                if (_deck.IsHandFull)
                {
                    Log("D 드로우 불가 — 손패 가득 참");
                    return;
                }
                int drawn = _deck.Draw(1, true);
                if (drawn > 0)
                {
                    AccrueEnemyGauge(1);
                    Log("D 드로우 — 적 게이지 +1");
                }
                else
                {
                    Log($"D 드로우 실패 — 뽑을 더미({_deck.DrawCount})·버린 더미({_deck.DiscardCount}) 카드 없음 " +
                        $"(소멸 더미={_deck.ExilePile.Count})");
                }
            }
            finally
            {
                BattleLog.EndPlayerDraw();
            }
        }

        // 카드 사용 요청 처리 — 각성 중 콤보 슬롯 등록 / 일반은 중앙 연출 후 효과 실행
        bool OnUseCardRequested(CardInstance card)
        {
            if (!_inBattle) return false;
            if (card == null) return false;
            // 선택창이 열려 있거나 열릴 예정(버리기 2장 사이 프레임 포함)이면 사용 금지 — 선택이 끝난 뒤 사용
            if (HasPendingSelection)
            {
                Log("선택 진행 중 — 카드 사용 잠금");
                return false;
            }
            if (InputLockedByEnemyTurn)
            {
                Log("적 턴 — 카드 사용 잠금");
                turnOrderTimeline?.FlashLocked();
                return false;
            }

            BindEnemyFromHudOrAlive();

            if (_awakenActive)
            {
                _deck.MoveAfterUse(card, exile: false, poweredField: false);
                _deck.MergeDiscardIntoDrawShuffled(); // 매 드로우마다 손패 밖 카드 전체에서 무작위로 뽑히게
                // 1장만 뽑으면 버리기 등으로 줄어든 패가 각성 내내 그대로 유지됐다 — 한도까지 채운다
                _deck.Draw(_deck.HandLimit);

                // 콤보 재사용 대기는 각성 중 카드 사용 횟수로 줄어든다(이번 카드로 막 걸리는 쿨타임은 줄이지 않도록 먼저 차감)
                TickComboCooldownByCardUse();

                bool comboTriggered = false;
                if (card.data.comboSlot)
                {
                    AddToComboSlot(card.Element);
                    comboTriggered = TryActivateCombo();
                }

                if (comboTriggered)
                {
                    float before = _awakenTimeRemaining;
                    _awakenTimeRemaining += COMBO_BONUS_SECONDS;
                    _awakenMaxTime += COMBO_BONUS_SECONDS;
                    // 유물(비급서 등)이 콤보 성공 시 각성 시간을 추가로 늘림 — AddAwakenTimeSeconds로 더해짐
                    RelicManager.Instance?.NotifyComboTriggered(this);
                    Log($"[각성] 콤보 매칭 -> 시간 ({before:F2}s -> {_awakenTimeRemaining:F2}s, max={_awakenMaxTime:F2}s)");

                    // 콤보 성공 -> 몬스터 주위에 표식 1개(종료 시 차례로 폭발) + 완성 효과음
                    SpawnAwakenComboMarker();
                    SfxManager.Instance?.PlayComboComplete();
                }

                Log($"[각성] {card.data.displayName} -> 콤보입력=[{string.Join(",", _comboInput)}] 콤보발동={comboTriggered} 남은시간={_awakenTimeRemaining:F2}s");

                if (handHud != null)
                {
                    handHud.UpdateComboSlot(_comboInput);
                    if (comboTriggered)
                        handHud.PlayComboSlotSurge();
                    // 속성이 슬롯에 들어왔을 때만 해당 칸 강조(가장 최근 = 마지막 칸)
                    if (card.data.comboSlot && _comboInput.Count > 0)
                    {
                        handHud.PlayComboSlotInputEffect(_comboInput.Count - 1);
                        SfxManager.Instance?.PlayAwakenSlotInput();
                    }
                    PushComboCooldownUi();
                    handHud.UpdateAwakenInputHistory(_awakenInputHistory);
                    handHud.PlayCardUsePresentation(card, null, interruptable: true);
                }

                return true;
            }

            if (_cardPresenting) return false;

            _deck.PullFromHand(card);
            _cardPresenting = true;

            if (handHud != null)
            {
                handHud.PlayCardUsePresentation(card, () =>
                {
                    try { ResolveCardUse(card); }
                    catch (System.Exception ex)
                    {
                        Debug.LogException(ex);
                        RecoverOrphanUsedCard(card);
                    }
                    finally { _cardPresenting = false; }
                });
            }
            else
            {
                try { ResolveCardUse(card); }
                catch (System.Exception ex) { Debug.LogException(ex); RecoverOrphanUsedCard(card); }
                finally { _cardPresenting = false; }
            }
            return true;
        }

        // 효과 처리 중 예외로 사용한 카드가 어떤 더미에도 놓이지 못했으면 버린 더미로 돌려놓는다(카드 증발 방지)
        void RecoverOrphanUsedCard(CardInstance card)
        {
            if (card == null || _deck == null || !_inBattle) return;
            if (HasPendingSelection) return; // 선택 종료 시 PlaceUsedCard가 배치한다
            if (IsCardInAnyPile(card)) return;
            _deck.AddToDiscard(card);
            Debug.LogWarning($"[NewBattle] 예외로 배치되지 못한 카드 '{card.data?.displayName}'를 버린 더미로 복구");
        }

        bool IsCardInAnyPile(CardInstance card)
        {
            return ContainsRef(_deck.Hand, card) || ContainsRef(_deck.DrawPile, card)
                || ContainsRef(_deck.DiscardPile, card) || ContainsRef(_deck.ExilePile, card);
        }

        static bool ContainsRef(IReadOnlyList<CardInstance> list, CardInstance card)
        {
            for (int i = 0; i < list.Count; i++) if (ReferenceEquals(list[i], card)) return true;
            return false;
        }

        // 카드 효과를 실행하고, 피해·방어도를 카드 이미지와 한 기록으로 남긴다.
        CardEffectResolver.ResolveResult ResolveLoggedCard(CardInstance card)
        {
            int before = _player != null ? Mathf.RoundToInt(_player.guard) : 0;
            BattleLog.SetGuardLogging(false);
            BattleLog.BeginDeferDraws();
            BattleLog.BeginMoveCapture();
            BattleLog.PushGuardFrame();
            CardEffectResolver.ResolveResult result;
            int triggeredGuard = 0;
            try
            {
                result = _resolver.Resolve(card);
            }
            finally
            {
                BattleLog.SetGuardLogging(true);
                BattleLog.EndDeferDraws();
                triggeredGuard = BattleLog.PopGuardFrame();
            }

            int gained = _player != null ? Mathf.Max(0, Mathf.RoundToInt(_player.guard) - before) : 0;
            // 소멸·버림으로 발동한 카드가 얻은 방어도는 그 카드 기록에만 남긴다.
            gained = Mathf.Max(0, gained - triggeredGuard);
            BattleLog.RecordCardPlay(card != null ? card.data : null, result.totalDamage, gained);
            BattleLog.FlushDeferredDraws();
            return result;
        }

        // 카드 사용 실제 효과 처리 — 중앙 연출 종료 시점 호출(카드는 이미 패에서 제거됨)
        void ResolveCardUse(CardInstance card)
        {
            if (card == null) return;
            if (!_inBattle) return;

            ApplyCurseOnCardUse(card);

            // 특이사항(매혹, 구미호): 사용 시 적 행동 게이지가 가득 참 -> 즉시 공격
            if (card.data != null && card.data.id == CardDatabase.CHARM)
            {
                Log("[특이사항] 매혹 사용 — 적 행동 게이지 가득 참!");
                _enemyStat?.FillGaugeToFull();
            }

            EnemyController fxTarget = _enemy; // 효과로 대상이 바뀌거나 죽기 전에 이펙트 대상 확보
            CapturePartyFrost();

            var result = ResolveLoggedCard(card);
            int effectLog = BattleLog.MoveTargetId;

            card.selfUseCount++;

            // 유물(복사경 10015): 이번 전투 첫 사용 카드면 효과를 한 번 더 발동
            if (_relicFirstCardRecastPending)
            {
                _relicFirstCardRecastPending = false;
                ResolveLoggedCard(card);
                BattleLog.SetMoveTarget(effectLog);
                Log("[유물] 복사경 — 첫 카드 효과 재발동");
            }

            // 사용한 카드를 묘지/소멸/필드/패로 보낸다. 이 카드가 선택(묘지·덱·패에서 고르기)을 요구하면
            // 선택이 끝나 효과가 발동한 뒤에 보낸다 — 그래야 묘지에서 고를 때 방금 쓴 카드 자신이 후보에 섞이지 않는다.
            void PlaceUsedCard()
            {
                // 바람314: 버린 더미 대신 패로 복귀 (소멸이 아닐 때만)
                if (result.returnToHandInsteadOfDiscard && !result.exile && !result.keepOnField)
                {
                    if (!_deck.AddToHand(card)) _deck.AddToDiscard(card);
                }
                else
                {
                    _deck.PlaceCardAfterUse(card, result.exile, result.keepOnField);

                    // 바람318: 사용 후 버린 더미로 이동하는 카드 트리거
                    if (!result.exile && !result.keepOnField)
                        _resolver.NotifyCardMovedToDiscard(card);
                }

                if (result.exile) BattleLog.NoteMove(BattleLog.MoveZone.Exile, card);
                if (result.exile) _resolver.NotifyExile(card);
            }
            SelectionRequest lastSelection = null; // 이 카드가 연 마지막 선택 요청

            // 실제로 맞은 적(전체 공격이면 전부, 무작위면 뽑힌 적) 자리마다 이펙트 재생
            if (effectOverlay != null) effectOverlay.Play(card.data, _resolver.LastCardFxTargets, fxTarget);

            // 물12(211): 마지막 사용 카드 효과 재사용
            if (result.recastLastCard && _lastResolvedCard != null)
            {
                Log($"[재사용] {_lastResolvedCard.data.displayName} 효과 한 번 더");
                ResolveLoggedCard(_lastResolvedCard);
                BattleLog.SetMoveTarget(effectLog);
            }

            // 패에서 카드 선택 -> 소멸 (불4/103, 불13/112, 물10/209)
            if (result.requiresHandExileSelection && handHud != null && _deck.Hand.Count > 0)
            {
                var filter = BuildHandCardFilter(result.handSelectionCardFilter);
                if (HandHasMatch(filter))
                {
                    lastSelection = EnqueueHandSelection("패에서 소멸할 카드를 선택하세요",
                        (selected) =>
                        {
                            if (selected == null) return;
                            _deck.ExileFromHand(selected);
                            _resolver.NotifyExile(selected);
                        }, filter);
                }
                else Log("[효과] 소멸 가능한 카드 없음 — 건너뜀");
            }

            // 패에서 카드 선택 -> 버리기 (물8/207)
            if (result.requiresHandDiscardSelection && handHud != null && _deck.Hand.Count > 0)
            {
                var filter = BuildHandCardFilter(result.handSelectionCardFilter);
                if (HandHasMatch(filter))
                {
                    lastSelection = EnqueueHandSelection("패에서 버릴 카드를 선택하세요",
                        (selected) =>
                        {
                            if (selected == null) return;
                            _deck.DiscardCardFromHand(selected);
                            if (_ctx.discardToDrawActive) _deck.Draw(1);
                        }, filter);
                }
                else Log("[효과] 버릴 카드 없음 — 건너뜀");
            }

            // 패에서 카드 선택 -> 복사하여 패에 추가 (물14/213)
            if (result.requiresHandCopySelection && handHud != null && _deck.Hand.Count > 0)
            {
                var filter = BuildHandCardFilter(result.handSelectionCardFilter);
                if (HandHasMatch(filter))
                {
                    lastSelection = EnqueueHandSelection("복사할 카드를 선택하세요",
                        (selected) =>
                        {
                            if (selected == null) return;
                            var copy = new CardInstance(selected.data, transient: true);
                            if (!_deck.AddToHand(copy)) _deck.AddToDiscard(copy);
                        }, filter);
                }
                else Log("[효과] 복사 가능한 카드 없음 — 건너뜀");
            }

            // 뽑을 더미에서 카드 선택 -> 패로 (물7/206)
            if (result.requiresDrawPileMoveSelection && handHud != null)
            {
                lastSelection = EnqueuePicker("뽑을 더미에서 카드를 선택하세요",
                    () => new List<CardInstance>(_deck.DrawPile),
                    (picked) => { if (picked != null) _deck.MoveFromDrawPileToHand(picked); },
                    Battle.UI.PileViewerKind.Draw);
            }

            // 버린 더미에서 카드 선택 -> toZone에 따라 패 또는 뽑을 더미 맨 위 (역류203/물213)
            if (result.requiresDiscardMoveSelection && handHud != null)
            {
                if (_deck.DiscardCount > 0)
                {
                    string zone = (result.discardMoveTargetZone ?? "").Trim().ToUpperInvariant();
                    lastSelection = EnqueuePicker("버린 더미에서 카드를 선택하세요",
                        () => new List<CardInstance>(_deck.DiscardPile),
                        (picked) =>
                        {
                            if (picked == null) return;
                            bool toHand = zone == "HAND";
                            bool ok = toHand
                                ? _deck.MoveFromDiscardToHand(picked)
                                : _deck.MoveFromDiscardToDrawPileTop(picked);
                            if (!ok && toHand)
                                Log("[효과] 패가 가득 차 버린 더미 카드를 패로 가져올 수 없습니다.");
                        },
                        Battle.UI.PileViewerKind.Discard);
                }
                else Log("[효과] 버린 더미에 카드 없음 — 건너뜀");
            }

            // 파편 풀에서 선택 (땅10/409, 땅15/414)
            if (result.requiresFragmentPoolSelection && handHud != null)
            {
                var pool = new List<CardInstance>();
                foreach (var fid in CardDatabase.FragmentIds)
                {
                    var preview = CardDatabase.CreateFragmentInstance(fid);
                    if (preview != null) pool.Add(preview);
                }
                string zone = result.fragmentPickerTargetZone;
                int copies = Mathf.Max(1, result.fragmentPickerCopyCount);
                lastSelection = EnqueuePicker("파편 카드를 선택하세요",
                    () => pool,
                    (picked) =>
                    {
                        if (picked == null) return;
                        for (int i = 0; i < copies; i++)
                        {
                            var inst = CardDatabase.CreateFragmentInstance(picked.Id);
                            if (inst == null) continue;
                            PlaceFragmentInZone(inst, zone);
                        }
                    });
            }

            // 선택이 있으면 마지막 선택이 끝난 뒤, 없으면 지금 카드를 보낸다
            if (lastSelection != null) lastSelection.onFinished = PlaceUsedCard;
            else PlaceUsedCard();

            // 게이지 누적
            int gaugeCost = card.Gauge;
            // 유물(독주 10013): 모든 카드 코스트 가감 (하한 0)
            int relicCostDelta = RelicManager.Instance != null ? RelicManager.Instance.GetGaugeCostDelta() : 0;
            if (relicCostDelta != 0) gaugeCost = Mathf.Max(0, gaugeCost + relicCostDelta);
            if (result.currentCardFreeThisUse) gaugeCost = 0;
            if (_ctx.firstCardAfterAttackFreeActive && _ctx.firstCardAfterEnemyAttack)
                gaugeCost = 0;

            // 이번 카드가 붙인 빙결은 이 카드의 게이지를 막지 않는다. 맞은 적이 여러 명이어도 같이 미룬다.
            HoldFrostAddedThisCard();
            AccrueEnemyGauge(gaugeCost);
            AccrueGaugeOnCardUseAbility();
            ReleaseHeldFrost();

            if (result.skipNextGaugeCost) _skipNextGaugeCount++;

            UpdateUsageStats(card);

            // 유물(웃는 탈 10006): 일반 카드 사용 누적 -> 3장마다 등 카드 사용 훅
            _cardsPlayedThisBattle++;
            RelicManager.Instance?.NotifyCardPlayed(this, card, _cardsPlayedThisBattle);

            if (card.data.comboSlot) AccumulateAwakenInput();

            // 각성 발동이 보류됐다면 — 선택이 없으면 곧, 선택 카드면 선택 완료 후 발동
            if (_awakenPending) StartCoroutine(DeferredPendingAwakenCheck());

            if (card.Id != 211) _lastResolvedCard = card;
            _ctx.previousCardType = card.Type;
            _ctx.firstCardAfterEnemyAttack = false;
            _ctx.currentCardJustDrawn = false;

            // "방금 뽑은" 표시는 한 번이라도 카드를 쓰면 만료
            for (int i = 0; i < _deck.Hand.Count; i++)
                _deck.Hand[i].justDrawn = false;
            if (card != null) card.justDrawn = false;

            Log($"{card.data.displayName} 사용 — 게이지+{gaugeCost}, 연쇄={_ctx.chainCount}");
            if (!HasPendingSelection) BattleLog.CloseMoveTarget();
        }

        // 카드 효과 전에 살아 있는 적들의 빙결 스택을 적어 둔다
        void CapturePartyFrost()
        {
            _frostAtCardStart.Clear();
            EnemyCombatParty.CollectAlive(_aliveBuf);
            for (int i = 0; i < _aliveBuf.Count; i++)
            {
                EnemyController enemy = _aliveBuf[i];
                if (enemy == null) continue;
                _frostAtCardStart[enemy] = enemy.GetStatus("frost");
            }
        }

        // 이번 카드로 늘어난 빙결만 게이지 계산에서 빼 둔다. 원래 있던 빙결은 그대로 소모된다.
        void HoldFrostAddedThisCard()
        {
            _frostHoldEnemies.Clear();
            _frostHoldAdded.Clear();
            foreach (var pair in _frostAtCardStart)
            {
                EnemyController enemy = pair.Key;
                if (enemy == null || !enemy.IsAlive) continue;
                int added = Mathf.Max(0, enemy.GetStatus("frost") - pair.Value);
                if (added <= 0) continue;
                EnemyStat stat = enemy.GetComponent<EnemyStat>();
                if (stat == null || !stat.statusEffects.ContainsKey("frost")) continue;
                stat.statusEffects["frost"] = pair.Value;
                _frostHoldEnemies.Add(enemy);
                _frostHoldAdded.Add(added);
            }
        }

        // 게이지를 처리한 뒤, 빼 두었던 이번 카드의 빙결을 도로 붙인다
        void ReleaseHeldFrost()
        {
            for (int i = 0; i < _frostHoldEnemies.Count; i++)
            {
                EnemyController enemy = _frostHoldEnemies[i];
                if (enemy == null || !enemy.IsAlive) continue;
                enemy.SetStatus("frost", enemy.GetStatus("frost") + _frostHoldAdded[i], false);
            }
        }

        // 적 게이지 누적 (각성 중·바람14 스킵 처리 포함)
        void AccrueEnemyGauge(int amount)
        {
            if (_awakenActive) return;

            if (_skipNextGaugeCount > 0)
            {
                _skipNextGaugeCount--;
                Log("[게이지] 다음 카드 게이지 소모 스킵 (바람14)");
                return;
            }

            EnemyCombatParty.CollectAlive(_aliveBuf);
            if (_aliveBuf.Count == 0) return;
            for (int i = 0; i < amount; i++)
            {
                for (int e = 0; e < _aliveBuf.Count; e++)
                {
                    EnemyStat st = _aliveBuf[e] != null ? _aliveBuf[e].GetComponent<EnemyStat>() : null;
                    st?.ConsumeGaugeStep();
                }
                // 패의 모든 카드 gaugeSinceDrawn 누적 (땅6/405, 땅16/415 공식용)
                for (int h = 0; h < _deck.Hand.Count; h++)
                    _deck.Hand[h].gaugeSinceDrawn++;
            }
        }

        // 특이사항(인차무샤): 부적을 사용할 때마다 이 적의 행동게이지 +1(코스트와 별도)
        void AccrueGaugeOnCardUseAbility()
        {
            if (_awakenActive) return;
            EnemyCombatParty.CollectAlive(_aliveBuf);
            for (int e = 0; e < _aliveBuf.Count; e++)
            {
                EnemyController en = _aliveBuf[e];
                if (en == null || en.Ability != EnemySpecialAbility.GaugeUpOnCardUse) continue;
                en.GetComponent<EnemyStat>()?.ConsumeGaugeStep();
            }
        }
    }
}
