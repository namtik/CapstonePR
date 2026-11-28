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
        // 손패 1장 선택 요청을 대기열에 넣는다(직전 효과의 시각 갱신 후 다음 프레임 이후 진입)
        SelectionRequest EnqueueHandSelection(string prompt, System.Action<CardInstance> callback,
            System.Func<CardInstance, bool> filter)
        {
            var req = new SelectionRequest { picker = false, prompt = prompt, callback = callback, filter = filter };
            _selectionQueue.Enqueue(req);
            StartSelectionQueue();
            return req;
        }

        // 손패 count장 선택 요청(빈 칸 count개, 모두 채우고 확인)
        void EnqueueHandMultiSelection(string prompt, int count, System.Action<IList<CardInstance>> callback,
            System.Func<CardInstance, bool> filter)
        {
            _selectionQueue.Enqueue(new SelectionRequest
            {
                picker = false, prompt = prompt, filter = filter,
                count = Mathf.Max(1, count), multiCallback = callback,
            });
            StartSelectionQueue();
        }

        // 카드 픽커 요청을 대기열에 넣는다(후보 목록은 실제로 열 때 만든다)
        SelectionRequest EnqueuePicker(string prompt, System.Func<IList<CardInstance>> cards,
            System.Action<CardInstance> callback, Battle.UI.PileViewerKind? pileKind = null)
        {
            var req = new SelectionRequest { picker = true, prompt = prompt, callback = callback, pickerCards = cards, pileKind = pileKind };
            _selectionQueue.Enqueue(req);
            StartSelectionQueue();
            return req;
        }

        void StartSelectionQueue()
        {
            if (_selectionRoutine != null) return;
            if (!isActiveAndEnabled) { _selectionQueue.Clear(); return; }
            _selectionRoutine = StartCoroutine(ProcessSelectionQueue());
        }

        IEnumerator ProcessSelectionQueue()
        {
            while (_selectionQueue.Count > 0)
            {
                yield return null; // 직전 효과의 패 갱신이 화면에 반영된 뒤 진입
                // 대기열 밖에서 연 선택창이 있으면 닫힐 때까지 대기(콜백 덮어쓰기 방지)
                while (handHud != null && (handHud.IsSelectionMode || handHud.IsPickerMode)) yield return null;
                if (!_inBattle || handHud == null || _selectionQueue.Count == 0) break;

                SelectionRequest req = _selectionQueue.Dequeue();
                IList<CardInstance> cards = null;
                if (req.picker)
                {
                    cards = req.pickerCards != null ? req.pickerCards() : null;
                    if (cards == null || cards.Count == 0) { Log($"[선택] '{req.prompt}' — 후보 없음, 건너뜀"); FinishSelection(req); continue; }
                }
                else if (!HandHasMatch(req.filter))
                {
                    // 앞선 선택(예: 방해 버리기)으로 패가 바뀌어 고를 카드가 없어진 경우
                    Log($"[선택] '{req.prompt}' — 고를 수 있는 카드 없음, 건너뜀");
                    FinishSelection(req);
                    continue;
                }

                bool closed = false;
                _selectionActive = true;
                System.Action<CardInstance> wrapped = c =>
                {
                    // HUD가 SelectionClosedCallback(보류 각성)을 부르기 전에 활성 표시를 내린다
                    _selectionActive = false;
                    closed = true;
                    req.callback?.Invoke(c);
                    // 사용한 카드 배치(onFinished)도 HUD가 보류 각성을 발동하기 '전에' 끝낸다.
                    // (늦으면 각성 스냅샷에 이 카드가 빠져, 각성 종료 복원 때 패/묘지에서 사라졌음)
                    FinishSelection(req);
                };
                System.Action<IList<CardInstance>> wrappedMulti = list =>
                {
                    _selectionActive = false;
                    closed = true;
                    req.multiCallback?.Invoke(list);
                    FinishSelection(req);
                };
                if (req.picker) handHud.EnterCardPickerMode(req.prompt, cards, wrapped, req.pileKind);
                else if (req.multiCallback != null) handHud.EnterMultiSelectionMode(req.prompt, req.count, wrappedMulti, req.filter);
                else handHud.EnterSelectionMode(req.prompt, wrapped, filter: req.filter);

                while (!closed)
                {
                    // 전투 종료 등으로 외부에서 창이 닫힘 -> 이 요청은 취소로 간주
                    if (!_inBattle || handHud == null || (!handHud.IsSelectionMode && !handHud.IsPickerMode)) break;
                    yield return null;
                }
                _selectionActive = false;
                FinishSelection(req);
            }
            _selectionRoutine = null;
            BattleLog.CloseMoveTarget();
            // 선택이 모두 끝났으면 보류 중이던 각성을 발동
            TryActivatePendingAwaken();
        }

        // 선택 요청 종료 후처리(사용한 카드를 묘지로 보내는 등). 전투가 끝났으면 버린다.
        void FinishSelection(SelectionRequest req)
        {
            var done = req?.onFinished;
            if (req != null) req.onFinished = null;
            if (done != null && _inBattle) done();
        }

        // 선택 대기열을 비우고 처리 코루틴을 멈춘다(전투 종료/시작 시)
        void ClearSelectionQueue()
        {
            if (_selectionRoutine != null) { StopCoroutine(_selectionRoutine); _selectionRoutine = null; }
            _selectionQueue.Clear();
            _selectionActive = false;
        }

        // 필터에 매칭되는 카드가 패에 있는지 확인
        bool HandHasMatch(System.Func<CardInstance, bool> filter)
        {
            if (filter == null) return _deck.Hand.Count > 0;
            for (int i = 0; i < _deck.Hand.Count; i++)
                if (filter(_deck.Hand[i])) return true;
            return false;
        }

        // CardEffects의 cardFilter 문자열을 선택 모드용 delegate로 변환
        static System.Func<CardInstance, bool> BuildHandCardFilter(string filter)
        {
            if (string.IsNullOrEmpty(filter)) return null;
            switch (filter.Trim().ToUpperInvariant())
            {
                case "":
                case "ANY_CARD":
                    return null;
                case "NEUTRAL_CARD":
                    return c => c != null && c.Element == CardElement.Neutral;
                case "FRAGMENT_CARD":
                    return c => c != null && c.Element == CardElement.Fragment;
                case "ATTACK_CARD":
                    return c => c != null && c.Type == CardType.Attack;
                default:
                    return null;
            }
        }

        // 파편 인스턴스를 지정 존(HAND/DRAW/DISCARD)에 배치
        void PlaceFragmentInZone(CardInstance frag, string zone)
        {
            if (frag == null) return;
            switch ((zone ?? "").Trim().ToUpperInvariant())
            {
                case "HAND":
                    if (!_deck.AddToHand(frag)) _deck.AddToDiscard(frag);
                    break;
                case "DRAW_PILE":
                case "DRAW_PILE_SHUFFLE":
                    _deck.AddToDrawShuffled(frag);
                    break;
                case "DISCARD_PILE":
                case "DISCARD_PILE_SHUFFLE":
                default:
                    _deck.AddToDiscard(frag);
                    break;
            }
        }
    }
}
