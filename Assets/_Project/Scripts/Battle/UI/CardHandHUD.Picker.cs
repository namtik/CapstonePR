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
        // 임의 카드 목록에서 1장을 선택받는 픽커 모드로 진입
        // pileKind 지정(뽑을/버린 더미) -> 덱/묘지 보기 패널 + 상단 "부적을 선택하세요"
        // pileKind 미지정(파편 등 생성 후보) -> 띠 패널 위 카드 가로 배치
        public void EnterCardPickerMode(string promptMessage, IList<CardInstance> cards,
            System.Action<CardInstance> callback, PileViewerKind? pileKind = null)
        {
            if (cards == null || cards.Count == 0)
            {
                callback?.Invoke(null);
                return;
            }
            if (cardPrefab == null)
            {
                Debug.LogWarning("[CardHandHUD] EnterCardPickerMode — cardPrefab 미설정. 무작위 자동 선택 폴백.");
                callback?.Invoke(cards[Random.Range(0, cards.Count)]);
                return;
            }

            _pickerCallback = callback;
            if (_viewerMode) CloseViewer();

            // 덱/묘지 선택 — 더미 보기 패널을 그대로 쓰고 X 버튼은 숨김
            if (pileKind.HasValue && pileViewerPanel != null)
            {
                _usingPilePicker = true;
                if (_tabPanel == null) _tabPanel = FindFirstObjectByType<BattleTabPanel>(FindObjectsInactive.Include);
                if (_tabPanel != null && _tabPanel.IsVisible) _tabPanel.Hide();
                string title = (pileKind.Value == PileViewerKind.Discard ? "버린 더미" : "뽑을 더미") + $" ({cards.Count})";
                pileViewerPanel.Show(pileKind.Value, title, cards, cardPrefab, this, null);
                pileViewerPanel.SetCloseButtonVisible(false);
                if (EnsureSelectOverlay())
                {
                    _selectOverlay.Show(CardSelectOverlay.Mode.TitleOnly, promptMessage);
                    _selectOverlay.transform.SetAsLastSibling();
                }
                BringAwakenGaugeToFront();
                return;
            }

            // 파편 등 — 띠 패널 위에 카드 가로 배치
            if (EnsureSelectOverlay() && _selectOverlay.CardRow != null)
            {
                _selectOverlay.Show(CardSelectOverlay.Mode.Row, promptMessage);
                _selectOverlay.transform.SetAsLastSibling();
                ClearPickerViews();
                for (int i = 0; i < cards.Count; i++)
                {
                    var view = Instantiate(cardPrefab, _selectOverlay.CardRow);
                    view.Bind(this, -1); // 그리드 카드(제자리 호버)
                    var vrect = (RectTransform)view.transform;
                    vrect.anchorMin = vrect.anchorMax = new Vector2(0.5f, 0.5f);
                    vrect.pivot = new Vector2(0.5f, 0.5f);
                    vrect.anchoredPosition = _selectOverlay.RowPosition(i, cards.Count);
                    vrect.localRotation = Quaternion.identity;
                    view.SetCard(cards[i]);
                    view.CaptureHome();
                    _pickerViews.Add(view);
                }
                BringAwakenGaugeToFront();
                return;
            }

            EnsureDimOverlay();
            EnsurePickerRoot();

            // handRoot(맨 뒤) -> dim -> 안내문 -> pickerRoot(최상단) 순으로 정렬
            if (handRoot != null) handRoot.SetAsFirstSibling();
            if (dimOverlay != null)
            {
                dimOverlay.gameObject.SetActive(true);
                dimOverlay.SetAsLastSibling();
            }
            if (selectionPromptText != null)
            {
                selectionPromptText.text = promptMessage;
                selectionPromptText.gameObject.SetActive(true);
                selectionPromptText.transform.SetAsLastSibling();
            }
            _pickerRoot.gameObject.SetActive(true);
            _pickerRoot.SetAsLastSibling();
            BuildPickerCards(cards);
            BringAwakenGaugeToFront();
        }

        // 픽커 모드를 종료하고 카드/딤/안내를 정리
        public void ExitPickerMode()
        {
            _pickerCallback = null;
            ClearPickerViews();
            if (_usingPilePicker)
            {
                _usingPilePicker = false;
                if (pileViewerPanel != null) pileViewerPanel.Hide();
            }
            if (_selectOverlay != null) _selectOverlay.Hide();
            if (_pickerRoot != null) _pickerRoot.gameObject.SetActive(false);
            if (dimOverlay != null) dimOverlay.gameObject.SetActive(false);
            if (selectionPromptText != null) selectionPromptText.gameObject.SetActive(false);
            // 뒤로 보냈던 손패를 다시 최상단으로 복원
            if (handRoot != null) handRoot.SetAsLastSibling();
            BringAwakenGaugeToFront();
        }

        // 픽커 루트(ScrollRect/뷰포트/콘텐츠)가 없으면 생성
        void EnsurePickerRoot()
        {
            if (_pickerRoot != null) return;
            Canvas canvas = ResolveTargetCanvas();
            Transform parent = canvas != null ? canvas.transform : transform;

            // 루트(ScrollRect) 생성
            var rootGo = new GameObject("CardPickerRoot", typeof(RectTransform), typeof(ScrollRect));
            rootGo.transform.SetParent(parent, false);
            _pickerRoot = (RectTransform)rootGo.transform;
            _pickerRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _pickerRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _pickerRoot.pivot = new Vector2(0.5f, 0.5f);
            _pickerRoot.anchoredPosition = Vector2.zero;
            _pickerRoot.sizeDelta = pickerViewportSize;

            // Viewport 생성(RectMask2D로 영역 클립, Image는 raycast 수신용)
            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewportGo.transform.SetParent(_pickerRoot, false);
            _pickerViewport = (RectTransform)viewportGo.transform;
            _pickerViewport.anchorMin = Vector2.zero;
            _pickerViewport.anchorMax = Vector2.one;
            _pickerViewport.offsetMin = Vector2.zero;
            _pickerViewport.offsetMax = Vector2.zero;
            var viewportImg = viewportGo.GetComponent<Image>();
            viewportImg.color = new Color(0f, 0f, 0f, 0f); // 완전 투명
            viewportImg.raycastTarget = true;

            // Content 생성(위에서 아래로 가변 높이)
            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(_pickerViewport, false);
            _pickerContent = (RectTransform)contentGo.transform;
            _pickerContent.anchorMin = new Vector2(0f, 1f);
            _pickerContent.anchorMax = new Vector2(1f, 1f);
            _pickerContent.pivot = new Vector2(0.5f, 1f);
            _pickerContent.anchoredPosition = Vector2.zero;
            _pickerContent.sizeDelta = new Vector2(0f, 0f); // 행 수에 따라 이후 갱신

            // ScrollRect 결선
            var scroll = rootGo.GetComponent<ScrollRect>();
            scroll.content = _pickerContent;
            scroll.viewport = _pickerViewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.1f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = pickerScrollSensitivity;
        }

        // 생성된 픽커 카드 뷰들을 모두 파괴
        void ClearPickerViews()
        {
            for (int i = 0; i < _pickerViews.Count; i++)
            {
                if (_pickerViews[i] != null) Destroy(_pickerViews[i].gameObject);
            }
            _pickerViews.Clear();
        }

        // 픽커 카드 1장의 실제 표시 폭(프리팹 size × scale)
        float GetPickerCardWidth()
        {
            if (cardPrefab == null) return 375f;

            RectTransform rt = cardPrefab.GetComponent<RectTransform>();
            if (rt == null) return 375f;

            return rt.sizeDelta.x * Mathf.Abs(rt.localScale.x);
        }

        // 열 수·카드 폭·간격에 맞춰 viewport 가로를 자동 확장(양쪽 열 잘림 방지)
        void ApplyPickerViewportSize(int columnsInRow)
        {
            if (_pickerRoot == null) return;

            int cols = Mathf.Max(1, columnsInRow);
            float cardWidth = GetPickerCardWidth();
            float horizontalPad = pickerContentPadding.x > 0f ? pickerContentPadding.x : 80f;
            float requiredWidth = pickerColumnSpacing * (cols - 1) + cardWidth + horizontalPad;
            float width = Mathf.Max(pickerViewportSize.x, requiredWidth);
            _pickerRoot.sizeDelta = new Vector2(width, pickerViewportSize.y);
        }

        // 카드 목록을 그리드로 배치해 픽커 카드 뷰를 생성
        void BuildPickerCards(IList<CardInstance> cards)
        {
            ClearPickerViews();
            int maxPerRow = Mathf.Max(1, pickerColumns);
            float colSpacing = pickerColumnSpacing;
            float rowSpacing = pickerRowSpacing;

            int total = cards.Count;
            int rows = Mathf.CeilToInt(total / (float)maxPerRow);

            ApplyPickerViewportSize(maxPerRow);

            // 컨텐츠 높이 = 행 수 × rowSpacing + 패딩
            float contentHeight = rows * rowSpacing + pickerContentPadding.y;
            _pickerContent.sizeDelta = new Vector2(0f, contentHeight);
            // 스크롤은 맨 위에서 시작
            _pickerContent.anchoredPosition = Vector2.zero;

            for (int i = 0; i < total; i++)
            {
                int row = i / maxPerRow;
                int col = i % maxPerRow;
                int cardsInRow = (row == rows - 1) ? (total - row * maxPerRow) : maxPerRow;
                float rowWidth = colSpacing * (cardsInRow - 1);
                float x = -rowWidth * 0.5f + col * colSpacing;
                // top-center 기준 각 카드의 중심 Y 계산
                float y = -(pickerContentPadding.y * 0.5f) - (row * rowSpacing) - (rowSpacing * 0.5f);

                var view = Instantiate(cardPrefab, _pickerContent);
                view.Bind(this, -1); // 슬롯 인덱스 -1은 픽커 뷰
                var vrect = (RectTransform)view.transform;
                vrect.anchorMin = new Vector2(0.5f, 1f);
                vrect.anchorMax = new Vector2(0.5f, 1f);
                vrect.pivot = new Vector2(0.5f, 0.5f);
                vrect.anchoredPosition = new Vector2(x, y);
                vrect.localRotation = Quaternion.identity;
                view.SetCard(cards[i]);
                view.CaptureHome();
                _pickerViews.Add(view);
            }
        }

        // 픽커 모드일 때 카드 클릭을 처리하고 콜백 발화
        bool TryHandlePickerClick(NewCardView view)
        {
            if (_pickerCallback == null) return false;
            if (view == null || view.Card == null) return true;
            bool isPickerView = _pickerViews.Contains(view)
                || (_usingPilePicker && pileViewerPanel != null && pileViewerPanel.ContainsView(view));
            if (!isPickerView) return true; // 픽커 뷰만 유효

            var cb = _pickerCallback;
            CardInstance picked = view.Card;
            ExitPickerMode();
            cb?.Invoke(picked);
            SelectionClosedCallback?.Invoke(); // 선택 후 보류 각성 발동 트리거
            return true;
        }
    }
}
