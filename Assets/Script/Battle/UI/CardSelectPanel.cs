using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Battle.Card;

namespace Battle.UI
{
    // 유희왕 마스터 듀얼식 카드 선택창 — 버린 더미(묘지)/뽑을 더미(덱)/파편 풀 등에서 카드를 고른다.
    // 화면 하단 중앙의 서랍형 패널(전장은 가리지 않음):
    //   우상단 탭 = 목록/그리드 전환 · 접기, 상단 = 안내문(존·카드 키워드 강조), 출처 탭(더미 이름·장수),
    //   파란 띠 = 후보 카드 가로 스크롤, 스크롤바, 하단 = 좌우 페이지 화살표 · 선택(결정) · 취소.
    // 마우스를 올린 카드는 화면 왼쪽 상세 패널에 크게 표시된다. 카드 클릭으로 선택 토글(순번 배지).
    // UI는 전부 코드로 생성되므로 씬/프리팹 배선이 필요 없다(카드 외형만 손패 카드 프리팹을 재사용).
    public class CardSelectPanel : MonoBehaviour
    {
        // 선택창 요청 내용
        public struct Request
        {
            public string title;                              // 상단 안내문(존/카드 키워드 자동 강조)
            public string sourceLabel;                        // 출처 탭 문구(비우면 '후보 N장')
            public IList<CardInstance> cards;                 // 후보 카드
            public int minCount;                              // 최소 선택 수
            public int maxCount;                              // 최대 선택 수
            public bool allowCancel;                          // 취소 허용 여부
            public System.Func<CardInstance, bool> filter;    // 선택 가능 조건(null=전부)
            public System.Action<List<CardInstance>> onComplete; // 결정=선택 목록, 취소=null
        }

        // 후보 카드 한 칸
        class Slot
        {
            public CardInstance card;
            public RectTransform rect;      // 칸 루트(배치 위치)
            public RectTransform body;      // 들림/확대 연출 대상
            public CanvasGroup group;
            public Image frame;             // 선택/포커스 테두리
            public Image dim;               // 선택 불가 어둡게
            public RectTransform badge;     // 선택 순번 배지
            public TextMeshProUGUI badgeText;
            public bool selectable;
            public float introDelay;
        }

        // 칸 위 투명 히트 영역 — 카드 뷰 자체 포인터 처리와 겹치지 않도록 별도 컴포넌트로 받는다
        class SlotHit : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
        {
            public CardSelectPanel owner;
            public int index;
            public void OnPointerEnter(PointerEventData e) => owner.OnSlotHover(index);
            public void OnPointerClick(PointerEventData e)
            {
                if (e.button != PointerEventData.InputButton.Left) return;
                owner.OnSlotClick(index, e.clickCount);
            }
        }

        // 사용자가 목록을 직접 끌면 화살표 자동 스크롤을 멈춘다
        class DragWatch : MonoBehaviour, IBeginDragHandler
        {
            public CardSelectPanel owner;
            public void OnBeginDrag(PointerEventData e) => owner._arrowScrolling = false;
        }

        [Header("배치 (1920x1080 기준)")]
        [SerializeField] private float panelWidth = 980f;     // 패널 폭
        [SerializeField] private float listStripHeight = 210f; // 목록 모드 파란 띠 높이
        [SerializeField] private float gridMaxHeight = 560f;  // 그리드 모드 파란 띠 최대 높이
        [SerializeField] private float cardHeight = 172f;     // 후보 카드 표시 높이

        static readonly Color PanelColor = new Color(0.02f, 0.03f, 0.06f, 0.96f);
        static readonly Color StripColor = new Color(0.10f, 0.22f, 0.58f, 1f);
        static readonly Color StripEdgeColor = new Color(0.30f, 0.55f, 1f, 1f);
        static readonly Color AccentColor = new Color(0.45f, 0.85f, 1f);        // 선택 강조(하늘색)
        static readonly Color TabColor = new Color(0.04f, 0.05f, 0.09f, 0.96f);
        static readonly Color SelectOnText = new Color(0.55f, 1f, 0.45f);
        static readonly Color SelectOffText = new Color(0.25f, 0.42f, 0.22f);
        const string ZoneHex = "FF9A3C";   // 안내문 존 키워드(덱/패/묘지 등)
        const string CardHex = "FFD84A";   // 안내문 '카드' 키워드

        const float HeaderH = 100f;        // 안내문 + 출처 탭 영역
        const float ScrollbarH = 22f;      // 스크롤바 줄
        const float BottomH = 84f;         // 하단 버튼 줄
        const float CollapsedH = 54f;      // 접었을 때 보이는 높이(안내문 줄)
        const float IntroStagger = 0.03f;  // 칸 순차 등장 간격

        static readonly Regex KeywordRegex = new Regex("(뽑을 더미|버린 더미|파편|묘지|덱|패)|(카드)");

        NewCardView _cardPrefab;
        TMP_FontAsset _font;

        RectTransform _root;
        RectTransform _panel;
        TextMeshProUGUI _title;
        TextMeshProUGUI _sourceText;
        RectTransform _listViewport;
        RectTransform _listContent;
        ScrollRect _scroll;
        Scrollbar _scrollbar;
        Button _leftArrow, _rightArrow;
        Button _selectButton;
        Image _selectFrame;
        TextMeshProUGUI _selectText;
        Button _cancelButton;
        RectTransform _chevron;        // 접기 탭 화살표(접힘 상태에 따라 뒤집힘)
        Image _gridIconTint;

        RectTransform _detail;         // 화면 왼쪽 카드 상세
        CanvasGroup _detailGroup;
        NewCardView _preview;
        TextMeshProUGUI _detailName;
        TextMeshProUGUI _detailMeta;
        TextMeshProUGUI _detailDesc;

        readonly List<Slot> _slots = new List<Slot>();
        readonly List<CardInstance> _selected = new List<CardInstance>();
        Request _req;
        int _focus = -1;
        float _openTime;
        bool _collapsed;
        bool _gridMode;
        bool _arrowScrolling;
        float _scrollTarget;
        float _panelH;
        float _panelY;

        static Sprite _roundedSprite;
        static Sprite _circleSprite;

        public bool IsOpen => _root != null && _root.gameObject.activeSelf;

        // 캔버스 하위에 선택창을 생성(처음 1회)
        public static CardSelectPanel Create(Transform canvasRoot, NewCardView cardPrefab, TMP_FontAsset font)
        {
            var go = new GameObject("CardSelectPanel", typeof(RectTransform));
            go.transform.SetParent(canvasRoot, false);
            var panel = go.AddComponent<CardSelectPanel>();
            panel._cardPrefab = cardPrefab;
            panel._font = font;
            panel.Build();
            panel._root.gameObject.SetActive(false);
            return panel;
        }

        // ── 열기/닫기 ─────────────────────────────────────────────
        public void Open(Request req)
        {
            _req = req;
            _req.maxCount = Mathf.Max(1, req.maxCount);
            _req.minCount = Mathf.Clamp(req.minCount, 0, _req.maxCount);
            _selected.Clear();
            _collapsed = false;
            _gridMode = false;
            _arrowScrolling = false;

            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            _openTime = Time.unscaledTime;

            _title.text = Highlight(req.title ?? "카드를 선택해 주세요");
            int count = _req.cards != null ? _req.cards.Count : 0;
            _sourceText.text = !string.IsNullOrEmpty(req.sourceLabel) ? req.sourceLabel : $"후보 {count}장";
            _cancelButton.gameObject.SetActive(req.allowCancel);

            ApplyMode();
            _panelY = -_panelH; // 아래에서 밀려 올라오며 등장
            _panel.anchoredPosition = new Vector2(0f, _panelY);
            _detailGroup.alpha = 0f;

            int first = 0;
            for (int i = 0; i < _slots.Count; i++) if (_slots[i].selectable) { first = i; break; }
            SetFocus(_slots.Count > 0 ? first : -1);
            RefreshState();
        }

        public void Close()
        {
            ClearSlots();
            _selected.Clear();
            _focus = -1;
            if (_root != null) _root.gameObject.SetActive(false);
        }

        void Confirm()
        {
            if (!CanConfirm()) return;
            var result = new List<CardInstance>(_selected);
            var cb = _req.onComplete;
            Close();
            cb?.Invoke(result);
        }

        void Cancel()
        {
            if (!_req.allowCancel) return;
            var cb = _req.onComplete;
            Close();
            cb?.Invoke(null);
        }

        bool CanConfirm() => _selected.Count >= _req.minCount && _selected.Count <= _req.maxCount;

        // ── 입력 ─────────────────────────────────────────────────
        void OnSlotHover(int index)
        {
            if (index != _focus) SetFocus(index);
        }

        void OnSlotClick(int index, int clickCount)
        {
            if (index < 0 || index >= _slots.Count) return;
            Slot s = _slots[index];
            SetFocus(index);
            if (!s.selectable) return;

            int at = _selected.IndexOf(s.card);
            if (at >= 0)
            {
                // 1장 선택에서 선택된 카드를 더블클릭하면 바로 결정
                if (_req.maxCount == 1 && clickCount >= 2) { Confirm(); return; }
                _selected.RemoveAt(at);
            }
            else
            {
                if (_req.maxCount == 1) _selected.Clear();          // 1장 선택은 교체
                else if (_selected.Count >= _req.maxCount) return;  // 다중 선택은 상한에서 막음
                _selected.Add(s.card);
                if (_req.maxCount == 1 && clickCount >= 2) { Confirm(); return; }
            }
            RefreshState();
        }

        void ToggleCollapse()
        {
            _collapsed = !_collapsed;
            _chevron.localRotation = Quaternion.Euler(0f, 0f, _collapsed ? 180f : 0f);
        }

        void ToggleGrid()
        {
            _gridMode = !_gridMode;
            if (_collapsed) ToggleCollapse();
            ApplyMode();
            RefreshState();
        }

        // 좌우 화살표 — 한 화면 폭만큼 부드럽게 이동
        void PageScroll(int dir)
        {
            float overflow = _listContent.rect.width - _listViewport.rect.width;
            if (overflow <= 1f) return;
            float step = _listViewport.rect.width * 0.85f / overflow;
            float from = _arrowScrolling ? _scrollTarget : _scroll.horizontalNormalizedPosition;
            _scrollTarget = Mathf.Clamp01(from + dir * step);
            _scroll.velocity = Vector2.zero;
            _arrowScrolling = true;
        }

        void Update()
        {
            if (!IsOpen) return;
            float dt = Time.unscaledDeltaTime;
            float t = 1f - Mathf.Exp(-14f * dt);

            // 키보드: Enter=결정, Esc=취소, ←/→=페이지 이동
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) { Confirm(); if (!IsOpen) return; }
            else if (Input.GetKeyDown(KeyCode.Escape)) { Cancel(); if (!IsOpen) return; }
            else if (Input.GetKeyDown(KeyCode.LeftArrow)) PageScroll(-1);
            else if (Input.GetKeyDown(KeyCode.RightArrow)) PageScroll(1);

            // 패널 등장/접기 슬라이드
            float targetY = _collapsed ? -(_panelH - CollapsedH) : 0f;
            _panelY = Mathf.Lerp(_panelY, targetY, 1f - Mathf.Exp(-12f * dt));
            _panel.anchoredPosition = new Vector2(0f, _panelY);

            // 화살표 자동 스크롤
            if (_arrowScrolling && !_gridMode)
            {
                float p = Mathf.Lerp(_scroll.horizontalNormalizedPosition, _scrollTarget, t);
                _scroll.horizontalNormalizedPosition = p;
                if (Mathf.Abs(p - _scrollTarget) < 0.001f) _arrowScrolling = false;
            }
            UpdateArrows();

            // 상세 패널은 접혀 있지 않고 포커스가 있을 때만
            bool showDetail = !_collapsed && _focus >= 0 && elapsedSinceOpen() > 0.12f;
            _detailGroup.alpha = Mathf.MoveTowards(_detailGroup.alpha, showDetail ? 1f : 0f, dt * 6f);

            // 칸 연출: 순차 등장 + 선택 시 들림 + 포커스 확대
            float elapsed = elapsedSinceOpen();
            for (int i = 0; i < _slots.Count; i++)
            {
                Slot s = _slots[i];
                float k = Mathf.Clamp01((elapsed - s.introDelay) / 0.2f);
                s.group.alpha = 1f - (1f - k) * (1f - k);

                bool sel = _selected.Contains(s.card);
                bool foc = i == _focus;
                float targetScale = foc ? 1.07f : 1f;
                float targetLift = sel ? 10f : 0f;
                s.body.localScale = Vector3.Lerp(s.body.localScale, Vector3.one * targetScale, t);
                s.body.anchoredPosition = Vector2.Lerp(s.body.anchoredPosition, new Vector2(0f, targetLift), t);

                if (sel)
                {
                    float pulse = 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 5f);
                    s.frame.color = new Color(AccentColor.r, AccentColor.g, AccentColor.b, pulse);
                }
            }
        }

        float elapsedSinceOpen() => Time.unscaledTime - _openTime;

        void UpdateArrows()
        {
            bool scrollable = !_gridMode && _listContent.rect.width > _listViewport.rect.width + 1f;
            float p = _scroll.horizontalNormalizedPosition;
            SetArrow(_leftArrow, scrollable && p > 0.005f);
            SetArrow(_rightArrow, scrollable && p < 0.995f);
        }

        static void SetArrow(Button b, bool on)
        {
            if (b.interactable == on) return;
            b.interactable = on;
            var txt = b.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null) txt.color = on ? new Color(0.85f, 0.9f, 1f) : new Color(0.3f, 0.33f, 0.4f);
        }

        // ── 상태 반영 ────────────────────────────────────────────
        void SetFocus(int index)
        {
            _focus = index;
            Slot s = index >= 0 && index < _slots.Count ? _slots[index] : null;
            if (s == null)
            {
                RefreshState();
                return;
            }
            if (_preview != null) _preview.SetCard(s.card);
            CardData d = s.card.data;
            _detailName.text = d.displayName;
            _detailMeta.text = $"<color=#{ColorUtility.ToHtmlStringRGB(ElementColor(d.element))}>{ElementName(d.element)}</color>"
                               + $"  ·  {NewCardView.CardTypeName(d.type)}  ·  게이지 {d.gauge}";
            string desc = CardDatabase.GetDisplayDescription(d);
            if (!s.selectable) desc += "\n\n<color=#FF7A7A>이 카드는 선택할 수 없습니다.</color>";
            _detailDesc.text = desc;
            RefreshState();
        }

        void RefreshState()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                Slot s = _slots[i];
                int order = _selected.IndexOf(s.card);
                bool sel = order >= 0;
                bool foc = i == _focus;
                s.badge.gameObject.SetActive(sel);
                if (sel) s.badgeText.text = (order + 1).ToString();
                s.frame.enabled = sel || foc;
                if (!sel) s.frame.color = new Color(1f, 1f, 1f, foc ? 0.7f : 0f);
                s.dim.enabled = !s.selectable;
            }

            bool ok = CanConfirm();
            _selectButton.interactable = ok;
            _selectText.color = ok ? SelectOnText : SelectOffText;
            _selectFrame.color = ok ? new Color(SelectOnText.r, SelectOnText.g, SelectOnText.b, 0.85f) : new Color(1f, 1f, 1f, 0.12f);
            _selectText.text = _req.maxCount > 1 ? $"선택  {_selected.Count}/{_req.maxCount}" : "선택";
            _gridIconTint.color = _gridMode ? AccentColor : Color.white;
        }

        // ── 목록/그리드 배치 ─────────────────────────────────────
        void ApplyMode()
        {
            int count = _req.cards != null ? _req.cards.Count : 0;
            Vector2 cardSize = PrefabCardSize();
            float scale = cardHeight / Mathf.Max(1f, cardSize.y);
            float cardW = cardSize.x * scale;
            float slotW = cardW + 20f;
            float slotH = cardHeight + 30f;
            float viewW = panelWidth - 24f;

            float stripH;
            int cols;
            if (_gridMode)
            {
                cols = Mathf.Max(1, Mathf.FloorToInt((viewW - 20f) / slotW));
                int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)cols));
                stripH = Mathf.Min(rows * slotH + 20f, gridMaxHeight);
            }
            else
            {
                cols = Mathf.Max(1, count);
                stripH = listStripHeight;
            }

            _panelH = HeaderH + stripH + ScrollbarH + BottomH;
            _panel.sizeDelta = new Vector2(panelWidth, _panelH);
            Canvas.ForceUpdateCanvases();

            _scroll.horizontal = !_gridMode;
            _scroll.vertical = _gridMode;
            _scrollbar.gameObject.SetActive(!_gridMode);
            _scroll.horizontalScrollbar = _gridMode ? null : _scrollbar;

            ClearSlots();
            if (count == 0) return;

            if (_gridMode)
            {
                int rows = Mathf.CeilToInt(count / (float)cols);
                float contentH = Mathf.Max(stripH, rows * slotH + 20f);
                _listContent.anchorMin = new Vector2(0f, 1f);
                _listContent.anchorMax = new Vector2(1f, 1f);
                _listContent.pivot = new Vector2(0.5f, 1f);
                _listContent.sizeDelta = new Vector2(0f, contentH);
                _listContent.anchoredPosition = Vector2.zero;
                float gridW = cols * slotW;
                for (int i = 0; i < count; i++)
                {
                    int r = i / cols, c = i % cols;
                    var pos = new Vector2(-gridW * 0.5f + c * slotW + slotW * 0.5f, -10f - r * slotH - slotH * 0.5f);
                    _slots.Add(CreateSlot(i, _req.cards[i], new Vector2(0.5f, 1f), pos, new Vector2(slotW, slotH), cardW, scale));
                }
            }
            else
            {
                float contentW = count * slotW + 20f;
                float width = Mathf.Max(viewW, contentW);
                float startX = (width - contentW) * 0.5f + 10f; // 후보가 적으면 가운데 정렬
                _listContent.anchorMin = new Vector2(0f, 0f);
                _listContent.anchorMax = new Vector2(0f, 1f);
                _listContent.pivot = new Vector2(0f, 0.5f);
                _listContent.sizeDelta = new Vector2(width, 0f);
                _listContent.anchoredPosition = Vector2.zero;
                _scroll.horizontalNormalizedPosition = 0f;
                for (int i = 0; i < count; i++)
                {
                    var pos = new Vector2(startX + i * slotW + slotW * 0.5f, -4f);
                    _slots.Add(CreateSlot(i, _req.cards[i], new Vector2(0f, 0.5f), pos, new Vector2(slotW, slotH), cardW, scale));
                }
            }
            _openTime = Time.unscaledTime; // 모드 전환 시에도 순차 등장
        }

        Slot CreateSlot(int index, CardInstance card, Vector2 anchor, Vector2 pos, Vector2 size, float cardW, float scale)
        {
            var s = new Slot { card = card, introDelay = 0.08f + Mathf.Min(index, 14) * IntroStagger };
            s.selectable = _req.filter == null || _req.filter(card);

            var go = new GameObject("Slot", typeof(RectTransform), typeof(CanvasGroup));
            s.rect = (RectTransform)go.transform;
            s.rect.SetParent(_listContent, false);
            s.rect.anchorMin = s.rect.anchorMax = anchor;
            s.rect.pivot = new Vector2(0.5f, 0.5f);
            s.rect.sizeDelta = size;
            s.rect.anchoredPosition = pos;
            s.group = go.GetComponent<CanvasGroup>();
            s.group.alpha = 0f;

            var bodyGo = new GameObject("Body", typeof(RectTransform));
            s.body = (RectTransform)bodyGo.transform;
            s.body.SetParent(s.rect, false);
            s.body.anchorMin = s.body.anchorMax = new Vector2(0.5f, 0.5f);
            s.body.sizeDelta = new Vector2(cardW, cardHeight);

            s.frame = NewImage("Frame", s.body, RoundedSprite(), Color.clear);
            Stretch(s.frame.rectTransform, -5f);

            // 카드 외형(손패 프리팹 재사용 — Hud 미바인딩이라 자체 호버/드래그 동작 없음)
            var cardHolder = new GameObject("Card", typeof(RectTransform), typeof(CanvasGroup));
            var ch = (RectTransform)cardHolder.transform;
            ch.SetParent(s.body, false);
            Stretch(ch, 0f);
            cardHolder.GetComponent<CanvasGroup>().blocksRaycasts = false;
            if (_cardPrefab != null)
            {
                var view = Instantiate(_cardPrefab, ch);
                var vr = (RectTransform)view.transform;
                vr.anchorMin = vr.anchorMax = new Vector2(0.5f, 0.5f);
                vr.pivot = new Vector2(0.5f, 0.5f);
                vr.anchoredPosition = Vector2.zero;
                vr.localRotation = Quaternion.identity;
                vr.localScale = Vector3.one * scale;
                view.SetCard(card);
            }

            s.dim = NewImage("Dim", s.body, RoundedSprite(), new Color(0f, 0f, 0f, 0.6f));
            Stretch(s.dim.rectTransform, 0f);

            var badgeImg = NewImage("Badge", s.body, CircleSprite(), AccentColor);
            s.badge = badgeImg.rectTransform;
            s.badge.anchorMin = s.badge.anchorMax = new Vector2(1f, 1f);
            s.badge.anchoredPosition = new Vector2(-4f, -4f);
            s.badge.sizeDelta = new Vector2(30f, 30f);
            s.badgeText = NewText("Order", s.badge, 18f, TextAlignmentOptions.Center);
            Stretch(s.badgeText.rectTransform, 0f);
            s.badgeText.fontStyle = FontStyles.Bold;
            s.badgeText.color = new Color(0.03f, 0.10f, 0.18f);

            // 맨 위 투명 히트 영역(드래그는 부모 ScrollRect로 전달됨)
            var hit = NewImage("Hit", s.rect, null, Color.clear);
            hit.raycastTarget = true;
            Stretch(hit.rectTransform, 0f);
            var sh = hit.gameObject.AddComponent<SlotHit>();
            sh.owner = this;
            sh.index = index;
            return s;
        }

        void ClearSlots()
        {
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i].rect != null) Destroy(_slots[i].rect.gameObject);
            _slots.Clear();
        }

        Vector2 PrefabCardSize()
        {
            if (_cardPrefab == null) return new Vector2(300f, 400f);
            var rt = (RectTransform)_cardPrefab.transform;
            Vector2 sz = rt.sizeDelta;
            if (sz.x <= 1f || sz.y <= 1f) sz = new Vector2(300f, 400f);
            return sz;
        }

        static string Highlight(string s) => KeywordRegex.Replace(s, m =>
            m.Groups[1].Success ? $"<color=#{ZoneHex}>{m.Value}</color>" : $"<color=#{CardHex}>{m.Value}</color>");

        // ── UI 생성 ──────────────────────────────────────────────
        void Build()
        {
            _root = (RectTransform)transform;
            Stretch(_root, 0f);

            // 하단 서랍 패널
            var panelImg = NewImage("Panel", _root, null, PanelColor);
            panelImg.raycastTarget = true; // 패널 위 클릭이 뒤로 새지 않도록
            _panel = panelImg.rectTransform;
            _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0f);
            _panel.pivot = new Vector2(0.5f, 0f);
            var topEdge = NewImage("TopEdge", _panel, null, new Color(0.35f, 0.45f, 0.65f, 0.6f));
            TopBand(topEdge.rectTransform, 0f, 2f);

            // 우상단 탭: 목록/그리드 전환 · 접기
            var gridTab = NewTab("GridTab", new Vector2(-84f, 0f), ToggleGrid);
            for (int i = 0; i < 4; i++)
            {
                var sq = NewImage("Sq", gridTab, null, Color.white);
                Anchor(sq.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(i % 2 == 0 ? -8f : 8f, i < 2 ? 8f : -8f), new Vector2(12f, 12f));
                var hole = NewImage("Hole", sq.rectTransform, null, TabColor);
                Stretch(hole.rectTransform, 2.5f);
            }
            _gridIconTint = NewImage("Tint", gridTab, null, Color.white); // 그리드 모드 표시 밑줄
            Anchor(_gridIconTint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(28f, 3f));

            var foldTab = NewTab("FoldTab", new Vector2(-8f, 0f), ToggleCollapse);
            var chevGo = new GameObject("Chevron", typeof(RectTransform));
            _chevron = (RectTransform)chevGo.transform;
            _chevron.SetParent(foldTab, false);
            Anchor(_chevron, new Vector2(0.5f, 0.5f), new Vector2(0f, -2f), new Vector2(36f, 20f));
            for (int i = 0; i < 2; i++)
            {
                var bar = NewImage("Bar", _chevron, null, new Color(0.45f, 0.75f, 1f));
                Anchor(bar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(i == 0 ? -8f : 8f, 0f), new Vector2(22f, 5f));
                bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? -32f : 32f);
            }

            // 안내문
            _title = NewText("Title", _panel, 27f, TextAlignmentOptions.Center);
            _title.rectTransform.anchorMin = new Vector2(0f, 1f);
            _title.rectTransform.anchorMax = new Vector2(1f, 1f);
            _title.rectTransform.pivot = new Vector2(0.5f, 1f);
            _title.rectTransform.anchoredPosition = new Vector2(0f, -10f);
            _title.rectTransform.sizeDelta = new Vector2(-40f, 38f);

            var headLine = NewImage("HeadLine", _panel, null, new Color(1f, 1f, 1f, 0.06f));
            TopBand(headLine.rectTransform, -CollapsedH, 1f);

            // 출처 탭(파란 띠 왼쪽 위)
            var srcTab = NewImage("SourceTab", _panel, RoundedSprite(), StripColor);
            var sr = srcTab.rectTransform;
            sr.anchorMin = sr.anchorMax = new Vector2(0f, 1f);
            sr.pivot = new Vector2(0f, 0f);
            sr.anchoredPosition = new Vector2(12f, -HeaderH - 6f);
            sr.sizeDelta = new Vector2(220f, 44f);
            var icon = NewImage("Icon", sr, CircleSprite(), new Color(0.85f, 0.9f, 1f));
            Anchor(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(24f, 4f), new Vector2(24f, 24f));
            var iconIn = NewImage("In", icon.rectTransform, RoundedSprite(), StripColor);
            Stretch(iconIn.rectTransform, 5f);
            _sourceText = NewText("Label", sr, 18f, TextAlignmentOptions.MidlineLeft);
            _sourceText.rectTransform.anchorMin = Vector2.zero;
            _sourceText.rectTransform.anchorMax = Vector2.one;
            _sourceText.rectTransform.offsetMin = new Vector2(44f, 6f);
            _sourceText.rectTransform.offsetMax = new Vector2(-8f, 0f);
            _sourceText.color = new Color(0.9f, 0.94f, 1f);
            _sourceText.textWrappingMode = TextWrappingModes.NoWrap;

            // 파란 띠(후보 목록)
            var strip = NewImage("Strip", _panel, null, StripColor);
            var str = strip.rectTransform;
            str.anchorMin = new Vector2(0f, 0f);
            str.anchorMax = new Vector2(1f, 1f);
            str.offsetMin = new Vector2(12f, BottomH + ScrollbarH);
            str.offsetMax = new Vector2(-12f, -HeaderH);
            var stripTop = NewImage("Edge", str, null, StripEdgeColor);
            TopBand(stripTop.rectTransform, 0f, 3f);

            var listGo = new GameObject("List", typeof(RectTransform), typeof(ScrollRect));
            var listRect = (RectTransform)listGo.transform;
            listRect.SetParent(str, false);
            Stretch(listRect, 0f);
            listGo.AddComponent<DragWatch>().owner = this;

            var vpImg = NewImage("Viewport", listRect, null, new Color(0f, 0f, 0f, 0.001f));
            vpImg.raycastTarget = true;
            vpImg.gameObject.AddComponent<RectMask2D>();
            _listViewport = vpImg.rectTransform;
            Stretch(_listViewport, 0f);

            var contentGo = new GameObject("Content", typeof(RectTransform));
            _listContent = (RectTransform)contentGo.transform;
            _listContent.SetParent(_listViewport, false);

            _scroll = listGo.GetComponent<ScrollRect>();
            _scroll.viewport = _listViewport;
            _scroll.content = _listContent;
            _scroll.movementType = ScrollRect.MovementType.Elastic;
            _scroll.elasticity = 0.1f;
            _scroll.inertia = true;
            _scroll.decelerationRate = 0.12f;
            _scroll.scrollSensitivity = 50f;

            // 스크롤바(띠 바로 아래)
            var sbImg = NewImage("Scrollbar", _panel, RoundedSprite(), new Color(1f, 1f, 1f, 0.08f));
            var sbr = sbImg.rectTransform;
            sbr.anchorMin = new Vector2(0f, 0f);
            sbr.anchorMax = new Vector2(1f, 0f);
            sbr.pivot = new Vector2(0.5f, 0.5f);
            sbr.anchoredPosition = new Vector2(0f, BottomH + ScrollbarH * 0.5f);
            sbr.sizeDelta = new Vector2(-24f, 10f);
            sbImg.raycastTarget = true;
            var area = new GameObject("Sliding Area", typeof(RectTransform));
            var ar = (RectTransform)area.transform;
            ar.SetParent(sbr, false);
            Stretch(ar, 0f);
            var handle = NewImage("Handle", ar, RoundedSprite(), new Color(0.82f, 0.85f, 0.9f, 0.9f));
            handle.raycastTarget = true;
            Stretch(handle.rectTransform, 0f);
            _scrollbar = sbImg.gameObject.AddComponent<Scrollbar>();
            _scrollbar.handleRect = handle.rectTransform;
            _scrollbar.targetGraphic = handle;
            _scrollbar.direction = Scrollbar.Direction.LeftToRight;
            _scroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            // 하단 줄: ◀ [취소] [선택] ▶
            _leftArrow = NewArrow("Left", "<", new Vector2(0f, 0.5f), new Vector2(40f, 0f), () => PageScroll(-1));
            _rightArrow = NewArrow("Right", ">", new Vector2(1f, 0.5f), new Vector2(-40f, 0f), () => PageScroll(1));

            var selImg = NewImage("SelectButton", _panel, RoundedSprite(), new Color(0.03f, 0.04f, 0.06f, 1f));
            selImg.raycastTarget = true;
            Anchor(selImg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, BottomH * 0.5f), new Vector2(300f, 56f));
            _selectFrame = NewImage("Frame", selImg.rectTransform, RoundedSprite(), Color.white);
            Stretch(_selectFrame.rectTransform, -2f);
            _selectFrame.transform.SetAsFirstSibling();
            var selInner = NewImage("Inner", selImg.rectTransform, RoundedSprite(), new Color(0.03f, 0.04f, 0.06f, 1f));
            Stretch(selInner.rectTransform, 0f);
            selInner.transform.SetSiblingIndex(1);
            _selectText = NewText("Label", selImg.rectTransform, 26f, TextAlignmentOptions.Center);
            Stretch(_selectText.rectTransform, 0f);
            _selectText.fontStyle = FontStyles.Bold;
            _selectButton = selImg.gameObject.AddComponent<Button>();
            _selectButton.targetGraphic = selInner;
            _selectButton.transition = Selectable.Transition.ColorTint;
            var cb = _selectButton.colors;
            cb.highlightedColor = new Color(1.6f, 1.6f, 1.6f, 1f);
            cb.disabledColor = Color.white;
            _selectButton.colors = cb;
            _selectButton.onClick.AddListener(Confirm);

            var cancelImg = NewImage("CancelButton", _panel, RoundedSprite(), new Color(0.12f, 0.13f, 0.17f, 1f));
            cancelImg.raycastTarget = true;
            Anchor(cancelImg.rectTransform, new Vector2(0.5f, 0f), new Vector2(-240f, BottomH * 0.5f), new Vector2(140f, 48f));
            var cancelTxt = NewText("Label", cancelImg.rectTransform, 22f, TextAlignmentOptions.Center);
            Stretch(cancelTxt.rectTransform, 0f);
            cancelTxt.text = "취소";
            cancelTxt.color = new Color(0.8f, 0.82f, 0.88f);
            _cancelButton = cancelImg.gameObject.AddComponent<Button>();
            _cancelButton.onClick.AddListener(Cancel);

            BuildDetail();
        }

        // 화면 왼쪽 카드 상세(마우스 올린 카드 크게 보기)
        void BuildDetail()
        {
            var dImg = NewImage("Detail", _root, RoundedSprite(), new Color(0.02f, 0.03f, 0.06f, 0.9f));
            _detail = dImg.rectTransform;
            _detail.anchorMin = _detail.anchorMax = new Vector2(0f, 0.5f);
            _detail.pivot = new Vector2(0f, 0.5f);
            _detail.anchoredPosition = new Vector2(30f, 60f);
            _detail.sizeDelta = new Vector2(360f, 640f);
            _detailGroup = dImg.gameObject.AddComponent<CanvasGroup>();
            _detailGroup.blocksRaycasts = false;
            _detailGroup.interactable = false;
            var edge = NewImage("Edge", _detail, null, StripEdgeColor);
            TopBand(edge.rectTransform, 0f, 3f);

            var holder = new GameObject("Preview", typeof(RectTransform));
            var hr = (RectTransform)holder.transform;
            hr.SetParent(_detail, false);
            hr.anchorMin = hr.anchorMax = new Vector2(0.5f, 1f);
            hr.pivot = new Vector2(0.5f, 1f);
            hr.anchoredPosition = new Vector2(0f, -20f);
            hr.sizeDelta = new Vector2(255f, 340f);
            if (_cardPrefab != null)
            {
                _preview = Instantiate(_cardPrefab, hr);
                var pr = (RectTransform)_preview.transform;
                pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
                pr.pivot = new Vector2(0.5f, 0.5f);
                pr.anchoredPosition = Vector2.zero;
                pr.localRotation = Quaternion.identity;
                Vector2 cs = PrefabCardSize();
                pr.localScale = Vector3.one * Mathf.Min(255f / cs.x, 340f / cs.y);
            }

            _detailName = NewText("Name", _detail, 25f, TextAlignmentOptions.TopLeft);
            TopRow(_detailName.rectTransform, -372f, 34f);
            _detailName.fontStyle = FontStyles.Bold;
            _detailMeta = NewText("Meta", _detail, 17f, TextAlignmentOptions.TopLeft);
            TopRow(_detailMeta.rectTransform, -408f, 26f);
            _detailMeta.color = new Color(0.78f, 0.84f, 0.92f);
            var sep = NewImage("Sep", _detail, null, new Color(1f, 1f, 1f, 0.15f));
            TopRow(sep.rectTransform, -440f, 1f);
            _detailDesc = NewText("Desc", _detail, 18f, TextAlignmentOptions.TopLeft);
            var ddr = _detailDesc.rectTransform;
            ddr.anchorMin = Vector2.zero;
            ddr.anchorMax = Vector2.one;
            ddr.offsetMin = new Vector2(22f, 16f);
            ddr.offsetMax = new Vector2(-22f, -452f);
            _detailDesc.textWrappingMode = TextWrappingModes.Normal;
            _detailDesc.overflowMode = TextOverflowModes.Ellipsis;
            _detailDesc.lineSpacing = 4f;
        }

        RectTransform NewTab(string name, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            var img = NewImage(name, _panel, null, TabColor);
            img.raycastTarget = true;
            var r = img.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(1f, 0f);
            r.anchoredPosition = pos;
            r.sizeDelta = new Vector2(72f, 58f);
            var btn = img.gameObject.AddComponent<Button>();
            var cb = btn.colors;
            cb.highlightedColor = new Color(1.8f, 1.8f, 2.2f, 1f);
            btn.colors = cb;
            btn.onClick.AddListener(onClick);
            return r;
        }

        Button NewArrow(string name, string glyph, Vector2 anchor, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            var img = NewImage(name, _panel, null, new Color(0f, 0f, 0f, 0.001f));
            img.raycastTarget = true;
            Anchor(img.rectTransform, new Vector2(anchor.x, 0f), new Vector2(pos.x, BottomH * 0.5f), new Vector2(64f, 64f));
            var txt = NewText("Glyph", img.rectTransform, 46f, TextAlignmentOptions.Center);
            Stretch(txt.rectTransform, 0f);
            txt.fontStyle = FontStyles.Bold;
            txt.text = glyph;
            var btn = img.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(onClick);
            return btn;
        }

        static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        TextMeshProUGUI NewText(string name, Transform parent, float size, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (_font != null) t.font = _font;
            t.fontSize = size;
            t.alignment = align;
            t.color = Color.white;
            t.raycastTarget = false;
            t.richText = true;
            return t;
        }

        static void Stretch(RectTransform r, float inset)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(inset, inset);
            r.offsetMax = new Vector2(-inset, -inset);
        }

        static void Anchor(RectTransform r, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
        }

        // 부모 위쪽에 붙는 가로 띠(y=위에서의 거리, 음수=아래로)
        static void TopBand(RectTransform r, float y, float height)
        {
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, y);
            r.sizeDelta = new Vector2(0f, height);
        }

        // 상세 패널 안 좌우 여백을 둔 가로 줄
        static void TopRow(RectTransform r, float y, float height)
        {
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, y);
            r.sizeDelta = new Vector2(-44f, height);
        }

        static string ElementName(CardElement e) => e switch
        {
            CardElement.Fire => "불",
            CardElement.Water => "물",
            CardElement.Wind => "바람",
            CardElement.Earth => "땅",
            CardElement.Neutral => "무속성",
            CardElement.Fragment => "파편",
            _ => ""
        };

        static Color ElementColor(CardElement e) => e switch
        {
            CardElement.Fire => new Color(1f, 0.45f, 0.35f),
            CardElement.Water => new Color(0.40f, 0.70f, 1f),
            CardElement.Wind => new Color(0.50f, 0.92f, 0.60f),
            CardElement.Earth => new Color(0.85f, 0.68f, 0.40f),
            CardElement.Fragment => new Color(0.80f, 0.60f, 1f),
            _ => new Color(0.85f, 0.85f, 0.85f)
        };

        // 코드 생성 둥근 사각/원 스프라이트(별도 에셋 없이 동작)
        static Sprite RoundedSprite() => _roundedSprite != null ? _roundedSprite : (_roundedSprite = MakeRounded(64, 12f));
        static Sprite CircleSprite() => _circleSprite != null ? _circleSprite : (_circleSprite = MakeRounded(64, 32f));

        static Sprite MakeRounded(int size, float radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cx = x + 0.5f, cy = y + 0.5f;
                    float dx = Mathf.Max(radius - cx, 0f, cx - (size - radius));
                    float dy = Mathf.Max(radius - cy, 0f, cy - (size - radius));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    byte a = (byte)(Mathf.Clamp01(radius - d + 0.5f) * 255f);
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            float b = Mathf.Min(radius, size * 0.5f - 1f);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }
    }
}
