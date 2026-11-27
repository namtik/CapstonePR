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
        // 더미 숫자 텍스트가 속한 더미 오브젝트(Deck/Grave) 전체를 버튼으로 만들고 호버 확대를 붙인다.
        // 자식(원·숫자)을 눌러도 클릭·호버가 부모 버튼으로 전달된다. 이름이 맞는 상위 오브젝트가 없으면 텍스트에 붙인다.
        void AttachPileButton(TMP_Text txt, string rootName, System.Action action)
        {
            if (txt == null) return;
            Transform root = FindAncestorNamed(txt.transform, rootName);
            if (root == null) root = txt.transform;

            txt.raycastTarget = true;
            var graphic = root.GetComponent<Graphic>();
            if (graphic != null) graphic.raycastTarget = true;

            var button = root.GetComponent<Button>();
            if (button == null) button = root.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None; // 색 변화 대신 호버 확대로 표시
            if (graphic != null) button.targetGraphic = graphic;
            var nav = button.navigation;
            nav.mode = Navigation.Mode.None; // 키보드/패드 포커스로 선택되지 않게
            button.navigation = nav;
            button.onClick.AddListener(() => action());

            if (root.GetComponent<ButtonHoverScale>() == null)
                root.gameObject.AddComponent<ButtonHoverScale>();
        }

        static Transform FindAncestorNamed(Transform from, string name)
        {
            for (Transform t = from; t != null; t = t.parent)
                if (t.name == name) return t;
            return null;
        }

        // 지정 더미 보기를 토글(0=뽑을, 1=버린)
        public void TogglePileViewer(int pile)
        {
            if (_deck == null) return;
            if (IsSelectionMode || IsPickerMode) return;
            if (_talismanHand) return; // 각성 중에는 뽑을/버린 더미 보기 금지(각성용 임시 더미)
            if (_viewerMode && _viewerPile == pile) { CloseViewer(); return; }

            var src = pile == 0 ? _deck.DrawPile : _deck.DiscardPile;
            var list = new List<CardInstance>(src);
            list.Sort((a, b) => a.Id.CompareTo(b.Id));
            string title = (pile == 0 ? "뽑을 더미" : "버린 더미") + $" ({list.Count})";
            EnterPileViewer(title, list, pile);
        }

        // 더미 보기 화면을 띄우고 카드들을 깐다
        void EnterPileViewer(string title, List<CardInstance> cards, int pile)
        {
            if (cardPrefab == null)
            {
                Debug.LogWarning("[CardHandHUD] cardPrefab 미설정 — 더미 보기 불가.");
                return;
            }
            CloseViewer();
            // 정보 창은 한 번에 하나 — Tab(적 정보/로그) 패널이 열려 있으면 닫고 더미 보기를 연다
            if (_tabPanel == null) _tabPanel = FindFirstObjectByType<BattleTabPanel>(FindObjectsInactive.Include);
            if (_tabPanel != null && _tabPanel.IsVisible) _tabPanel.Hide();
            _viewerMode = true;
            _viewerPile = pile;

            // 프리팹 패널이 지정돼 있으면 패널에 데이터만 넘긴다
            if (pileViewerPanel != null)
            {
                PileViewerKind kind = pile == 0 ? PileViewerKind.Draw
                    : pile == 1 ? PileViewerKind.Discard
                    : PileViewerKind.RunDeck;
                pileViewerPanel.Show(kind, title, cards, cardPrefab, this, CloseViewer);
                BringAwakenGaugeToFront();
                return;
            }

            EnsureDimOverlay();
            EnsurePickerRoot();

            if (handRoot != null) handRoot.SetAsFirstSibling();
            if (dimOverlay != null)
            {
                dimOverlay.gameObject.SetActive(true);
                dimOverlay.SetAsLastSibling();
            }
            if (selectionPromptText != null)
            {
                selectionPromptText.text = title;
                selectionPromptText.gameObject.SetActive(true);
                selectionPromptText.transform.SetAsLastSibling();
            }
            _pickerRoot.gameObject.SetActive(true);
            _pickerRoot.SetAsLastSibling();
            if (cards.Count > 0) BuildPickerCards(cards);
            else ClearPickerViews();
            BringAwakenGaugeToFront();
            SetViewerCloseButtonVisible(true);
        }

        // menubar 덱 버튼 — 현재 런 덱 전체를 그리드로 토글 표시
        public void ToggleRunDeckViewer()
        {
            if (IsSelectionMode || IsPickerMode) return;
            if (_viewerMode && _viewerPile == RunDeckViewerPile) { CloseViewer(); return; }

            var runDeck = RunDeckState.EnsureExists();
            var cards = runDeck.InstantiateForBattle();
            cards.Sort((a, b) =>
            {
                int idCompare = a.data.id.CompareTo(b.data.id);
                return idCompare != 0 ? idCompare : a.Id.CompareTo(b.Id);
            });
            string title = $"내 덱 ({cards.Count})";
            EnterPileViewer(title, cards, RunDeckViewerPile);
        }

        // 더미 보기 화면을 닫는다
        public void CloseViewer()
        {
            if (!_viewerMode) return;
            _viewerMode = false;
            _viewerPile = -1;
            if (pileViewerPanel != null)
            {
                pileViewerPanel.Hide();
                return;
            }
            ClearPickerViews();
            if (_pickerRoot != null) _pickerRoot.gameObject.SetActive(false);
            if (dimOverlay != null) dimOverlay.gameObject.SetActive(false);
            if (selectionPromptText != null) selectionPromptText.gameObject.SetActive(false);
            SetViewerCloseButtonVisible(false);
            if (handRoot != null) handRoot.SetAsLastSibling();
            BringAwakenGaugeToFront();
        }

        // 전체 화면 딤 오버레이가 없으면 캔버스 안에 생성
        void EnsureDimOverlay()
        {
            if (dimOverlay != null) return;

            Canvas canvas = ResolveTargetCanvas();
            Transform parent = canvas != null ? canvas.transform : transform;

            var go = new GameObject("SelectionDimOverlay", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.color = dimColor;
            img.raycastTarget = true; // 뒤쪽 UI 클릭 차단

            // 더미 보기 중 딤 클릭 시 닫기
            var trigger = go.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            entry.callback.AddListener(_ => { if (_viewerMode) CloseViewer(); });
            trigger.triggers.Add(entry);

            dimOverlay = rect;
            dimOverlay.gameObject.SetActive(false);
        }

        // 더미/덱 보기 화면 하단 닫기 버튼 준비
        void EnsureViewerCloseButton()
        {
            if (viewerCloseButton != null)
            {
                _viewerCloseButtonRuntime = viewerCloseButton;
                _viewerCloseButtonImage = null;
                _viewerCloseButtonLabelText = null;
            }
            else if (_viewerCloseButtonRuntime == null)
            {
                Canvas canvas = ResolveTargetCanvas();
                Transform parent = canvas != null ? canvas.transform : transform;

                var go = new GameObject("PileViewerCloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(parent, false);

                var textGo = new GameObject("Label", typeof(RectTransform));
                textGo.transform.SetParent(go.transform, false);
                var textRt = (RectTransform)textGo.transform;
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.offsetMin = Vector2.zero;
                textRt.offsetMax = Vector2.zero;
                textGo.AddComponent<TextMeshProUGUI>();

                _viewerCloseButtonRuntime = go.GetComponent<Button>();
                go.SetActive(false);
            }

            if (_viewerCloseButtonRuntime == null) return;

            CacheViewerCloseButtonParts();
            ApplyViewerCloseButtonStyle();

            _viewerCloseButtonRuntime.onClick.RemoveListener(CloseViewer);
            _viewerCloseButtonRuntime.onClick.AddListener(CloseViewer);
        }

        void CacheViewerCloseButtonParts()
        {
            if (_viewerCloseButtonRuntime == null) return;

            if (_viewerCloseButtonImage == null)
                _viewerCloseButtonImage = _viewerCloseButtonRuntime.GetComponent<Image>();

            if (_viewerCloseButtonLabelText == null)
                _viewerCloseButtonLabelText = _viewerCloseButtonRuntime.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        void ApplyViewerCloseButtonStyle()
        {
            if (_viewerCloseButtonRuntime == null) return;

            CacheViewerCloseButtonParts();

            RectTransform rect = _viewerCloseButtonRuntime.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = viewerCloseButtonAnchoredPos;
                rect.sizeDelta = viewerCloseButtonSize;
            }

            if (_viewerCloseButtonImage != null)
            {
                _viewerCloseButtonImage.sprite = viewerCloseButtonSprite;
                _viewerCloseButtonImage.type = viewerCloseButtonSprite != null
                    ? Image.Type.Sliced
                    : Image.Type.Simple;
                _viewerCloseButtonImage.color = viewerCloseButtonImageColor;
            }

            if (_viewerCloseButtonLabelText != null)
            {
                _viewerCloseButtonLabelText.text = string.IsNullOrWhiteSpace(viewerCloseButtonLabel)
                    ? "\uB2EB\uAE30"
                    : viewerCloseButtonLabel;
                _viewerCloseButtonLabelText.fontSize = viewerCloseButtonFontSize;
                _viewerCloseButtonLabelText.fontStyle = viewerCloseButtonFontStyle;
                _viewerCloseButtonLabelText.color = viewerCloseButtonFontColor;
                _viewerCloseButtonLabelText.alignment = TextAlignmentOptions.Center;
                _viewerCloseButtonLabelText.raycastTarget = false;

                TMP_FontAsset font = viewerCloseButtonFont;
                if (font == null && drawCountText != null)
                    font = drawCountText.font;
                if (font == null && discardCountText != null)
                    font = discardCountText.font;
                if (font != null)
                    _viewerCloseButtonLabelText.font = font;
            }
        }

        void SetViewerCloseButtonVisible(bool visible)
        {
            EnsureViewerCloseButton();
            if (_viewerCloseButtonRuntime == null) return;

            if (visible)
                ApplyViewerCloseButtonStyle();

            _viewerCloseButtonRuntime.gameObject.SetActive(visible);
            if (visible)
                _viewerCloseButtonRuntime.transform.SetAsLastSibling();
        }
    }
}
