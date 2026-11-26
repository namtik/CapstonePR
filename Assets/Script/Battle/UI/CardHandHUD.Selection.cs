using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using TMPro;
using Battle.Card;
using Battle.Deck;
using Battle;

namespace Battle.UI
{
    public partial class CardHandHUD
    {
        // 패에서 카드 1장 선택을 받는 모드로 진입(필터/제외 카드 적용)
        public void EnterSelectionMode(string promptMessage, System.Action<CardInstance> callback,
            System.Func<CardInstance, bool> filter = null, CardInstance excludeCard = null)
        {
            // 오버레이 프리팹이 있으면 빈 칸(1칸) + 확인 방식
            if (CanUseSlotSelection())
            {
                BeginSlotSelection(promptMessage, 1,
                    picks => callback?.Invoke(picks.Count > 0 ? picks[0] : null), filter, excludeCard);
                return;
            }

            _selectionCallback = callback;
            _selectionFilter = filter;
            _selectionExcludeCard = excludeCard;

            // 화면을 어둡게 하고 손패를 위로 올려 강조
            EnsureDimOverlay();
            if (dimOverlay != null)
            {
                dimOverlay.gameObject.SetActive(true);
                dimOverlay.SetAsLastSibling();
            }
            if (handRoot != null) handRoot.SetAsLastSibling();

            if (selectionPromptText != null)
            {
                selectionPromptText.text = promptMessage;
                selectionPromptText.gameObject.SetActive(true);
                selectionPromptText.transform.SetAsLastSibling();
            }
            BringAwakenGaugeToFront();
        }

        // 패에서 카드 count장 선택 — 빈 칸이 count개 생기고 모두 채운 뒤 확인으로 확정.
        // 고를 수 있는 카드가 count장보다 적으면 그 수만큼만 칸을 만든다.
        // 오버레이 프리팹이 없으면 기존 방식으로 1장씩 count번 고른다.
        public void EnterMultiSelectionMode(string promptMessage, int count,
            System.Action<IList<CardInstance>> callback,
            System.Func<CardInstance, bool> filter = null, CardInstance excludeCard = null)
        {
            count = Mathf.Max(1, count);
            if (CanUseSlotSelection())
            {
                BeginSlotSelection(promptMessage, count, callback, filter, excludeCard);
                return;
            }

            // 폴백: 1장씩 연속 선택 후 한꺼번에 전달
            var picked = new List<CardInstance>();
            System.Action next = null;
            next = () =>
            {
                if (picked.Count >= count || CountSelectableHandCards(filter, excludeCard, picked) == 0)
                {
                    callback?.Invoke(picked);
                    return;
                }
                EnterSelectionMode(promptMessage, c =>
                {
                    if (c == null) { callback?.Invoke(picked); return; }
                    picked.Add(c);
                    next();
                }, card => (filter == null || filter(card)) && !picked.Contains(card), excludeCard);
            };
            next();
        }

        bool CanUseSlotSelection() => EnsureSelectOverlay() && _selectOverlay.Slots.Count > 0;

        // 필터·제외 조건을 통과하는 손패 수
        int CountSelectableHandCards(System.Func<CardInstance, bool> filter, CardInstance exclude,
            ICollection<CardInstance> alreadyPicked = null)
        {
            if (_deck == null) return 0;
            int n = 0;
            foreach (var c in _deck.Hand)
            {
                if (c == null || c == exclude) continue;
                if (alreadyPicked != null && alreadyPicked.Contains(c)) continue;
                if (filter != null && !filter(c)) continue;
                n++;
            }
            return n;
        }

        // 빈 칸 방식 선택 시작(칸 수 = 고를 장수, 고를 수 있는 카드 수로 상한)
        void BeginSlotSelection(string promptMessage, int count, System.Action<IList<CardInstance>> callback,
            System.Func<CardInstance, bool> filter, CardInstance excludeCard)
        {
            if (_viewerMode) CloseViewer();

            int available = CountSelectableHandCards(filter, excludeCard);
            int n = Mathf.Clamp(count, 1, Mathf.Max(1, available));

            _slotConfirmCallback = callback;
            _selectionFilter = filter;
            _selectionExcludeCard = excludeCard;
            _usingSlotSelection = true;

            _selectOverlay.Show(CardSelectOverlay.Mode.Slots, promptMessage);
            _activeSlotButtons.Clear();
            _activeSlotButtons.AddRange(_selectOverlay.PrepareSlots(n));
            _slotPicks = new CardInstance[_activeSlotButtons.Count];
            _slotPreviews = new NewCardView[_activeSlotButtons.Count];
            for (int i = 0; i < _activeSlotButtons.Count; i++)
            {
                Button slot = _activeSlotButtons[i];
                if (slot == null) continue;
                int idx = i;
                slot.onClick.RemoveAllListeners();
                slot.onClick.AddListener(() => UnslotAt(idx));
            }
            if (_selectOverlay.ConfirmButton != null)
            {
                _selectOverlay.ConfirmButton.onClick.RemoveAllListeners();
                _selectOverlay.ConfirmButton.onClick.AddListener(ConfirmSlotSelection);
            }

            // 오버레이(딤·띠·칸) 위에 손패를 그려 패에서 고를 수 있게
            _selectOverlay.transform.SetAsLastSibling();
            if (handRoot != null) handRoot.SetAsLastSibling();
            RefreshSlotConfirmState();
            BringAwakenGaugeToFront();
        }

        // 카드 선택 모드를 종료하고 딤/안내를 끔
        public void ExitSelectionMode()
        {
            _selectionCallback = null;
            _slotConfirmCallback = null;
            _selectionFilter = null;
            _selectionExcludeCard = null;

            if (_usingSlotSelection)
            {
                _usingSlotSelection = false;
                ClearSlotSelection();
                if (_selectOverlay != null) _selectOverlay.Hide();
            }

            if (dimOverlay != null) dimOverlay.gameObject.SetActive(false);

            if (selectionPromptText != null)
                selectionPromptText.gameObject.SetActive(false);
        }

        // 카드 클릭 시 더미보기/픽커/선택 모드를 순서대로 처리
        // 선택 모드에서 손패 카드를 끌어다 놓은 위치가 '선택'으로 볼 자리인지 — 빈 칸 위이거나 손패 영역(사용 임계선) 위쪽
        public bool IsSelectionDropTarget(PointerEventData eventData)
        {
            if (!IsSelectionMode || eventData == null) return false;
            Vector2 pos = eventData.position;
            Camera cam = eventData.pressEventCamera;

            if (_usingSlotSelection)
            {
                for (int i = 0; i < _activeSlotButtons.Count; i++)
                {
                    Button slot = _activeSlotButtons[i];
                    if (slot == null || !slot.gameObject.activeInHierarchy) continue;
                    if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)slot.transform, pos, cam))
                        return true;
                }
            }
            return pos.y >= UseThresholdY;
        }

        // ───────── 카드 선택 오버레이 ─────────

        // 오버레이 프리팹 인스턴스 준비(없으면 false → 기존 방식 폴백)
        bool EnsureSelectOverlay()
        {
            if (_selectOverlay != null) return true;
            if (selectOverlayPrefab == null) return false;
            Canvas canvas = ResolveTargetCanvas();
            Transform parent = canvas != null ? canvas.transform : transform;
            _selectOverlay = Instantiate(selectOverlayPrefab, parent, false);
            var rt = (RectTransform)_selectOverlay.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            _selectOverlay.Hide();
            return true;
        }

        // 손패 카드를 빈 칸에 넣는다(칸이 다 찼으면 마지막 칸을 교체)
        void SlotHandCard(NewCardView handView)
        {
            if (_slotPicks == null || _slotPicks.Length == 0 || handView == null || handView.Card == null) return;
            for (int i = 0; i < _slotPicks.Length; i++)
                if (_slotPicks[i] == handView.Card) return; // 이미 넣은 카드

            int idx = System.Array.IndexOf(_slotPicks, null);
            if (idx < 0) { idx = _slotPicks.Length - 1; UnslotAt(idx); }

            Button slot = idx < _activeSlotButtons.Count ? _activeSlotButtons[idx] : null;
            if (slot == null) return;

            _slotPicks[idx] = handView.Card;

            // 칸 크기에 맞춘 미리보기(클릭은 칸 버튼이 받도록 레이캐스트 차단)
            var preview = Instantiate(cardPrefab, slot.transform);
            preview.SetCard(handView.Card);
            var cg = preview.GetComponent<CanvasGroup>();
            if (cg == null) cg = preview.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            // 카드를 지정 크기(기본 248×317)로 맞춰 칸 틀을 덮는다
            var prt = (RectTransform)preview.transform;
            var srt = (RectTransform)slot.transform;
            Vector2 native = prt.sizeDelta;
            if (native.x < 1f || native.y < 1f) native = new Vector2(300f, 400f);
            Vector2 target = _selectOverlay.SlotCardSize;
            if (target.x < 1f || target.y < 1f) target = srt.rect.size;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.anchoredPosition = Vector2.zero;
            prt.localRotation = Quaternion.identity;
            prt.localScale = new Vector3(target.x / native.x, target.y / native.y, 1f);
            prt.SetAsLastSibling(); // 칸 틀 위에 그리기
            _slotPreviews[idx] = preview;

            // 손패에서는 감춘다(해제 시 복구)
            SetHandViewHidden(handView, true);
            ClearHoverSpread();
            RefreshSlotConfirmState();
        }

        // i번째 칸을 비우고 손패 카드를 되돌린다
        void UnslotAt(int idx)
        {
            if (_slotPicks == null || idx < 0 || idx >= _slotPicks.Length) return;
            CardInstance card = _slotPicks[idx];
            if (card == null) return;
            _slotPicks[idx] = null;
            if (_slotPreviews[idx] != null) Destroy(_slotPreviews[idx].gameObject);
            _slotPreviews[idx] = null;

            for (int i = _slotHiddenHandViews.Count - 1; i >= 0; i--)
            {
                var v = _slotHiddenHandViews[i];
                if (v != null && v.Card == card) SetHandViewHidden(v, false);
            }
            RefreshSlotConfirmState();
        }

        // 확인 — 넣은 카드로 선택 확정(빈 칸이면 무시)
        void ConfirmSlotSelection()
        {
            if (!_usingSlotSelection || _slotPicks == null) return;
            var picks = new List<CardInstance>();
            for (int i = 0; i < _slotPicks.Length; i++)
                if (_slotPicks[i] != null) picks.Add(_slotPicks[i]);
            if (picks.Count < _slotPicks.Length) return; // 칸을 모두 채워야 확정

            var cb = _slotConfirmCallback;
            ExitSelectionMode();
            cb?.Invoke(picks);
            SelectionClosedCallback?.Invoke(); // 선택 후 보류 각성 발동 트리거
        }

        // 칸·미리보기·숨긴 손패를 모두 원상복구
        void ClearSlotSelection()
        {
            if (_slotPreviews != null)
                for (int i = 0; i < _slotPreviews.Length; i++)
                    if (_slotPreviews[i] != null) Destroy(_slotPreviews[i].gameObject);
            for (int i = _slotHiddenHandViews.Count - 1; i >= 0; i--)
                SetHandViewHidden(_slotHiddenHandViews[i], false);
            _slotHiddenHandViews.Clear();
            _slotPicks = null;
            _slotPreviews = null;
        }

        // 손패 카드 뷰 숨김/복구(레이아웃 유지 위해 알파·레이캐스트만 조절)
        void SetHandViewHidden(NewCardView view, bool hidden)
        {
            if (view == null) return;
            var cg = view.GetComponent<CanvasGroup>();
            if (cg == null) cg = view.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = hidden ? 0f : 1f;
            cg.blocksRaycasts = !hidden;
            if (hidden) { if (!_slotHiddenHandViews.Contains(view)) _slotHiddenHandViews.Add(view); }
            else _slotHiddenHandViews.Remove(view);
        }

        // 확인 버튼은 칸을 모두 채웠을 때만 활성
        void RefreshSlotConfirmState()
        {
            if (_selectOverlay == null || _selectOverlay.ConfirmButton == null) return;
            // 칸을 모두 채웠을 때만 확정 가능
            bool full = _slotPicks != null && _slotPicks.Length > 0;
            if (_slotPicks != null)
                for (int i = 0; i < _slotPicks.Length; i++) if (_slotPicks[i] == null) { full = false; break; }
            _selectOverlay.ConfirmButton.interactable = full;

        }
    }
}
