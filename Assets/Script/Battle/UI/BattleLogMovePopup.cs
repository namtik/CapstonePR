using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Battle.Card;

namespace Battle.UI
{
    // 카드를 움직인 기록에 커서를 올리면 화면을 어둡게 하고, 실제로 움직인 카드만 보여 준다.
    public class BattleLogMovePopup : MonoBehaviour
    {
        const float CardWidth = 198f;
        const float CardHeight = 253f;
        const int MaxShown = 5;

        public struct Tune
        {
            public Vector2 DeckCards;
            public Vector2 DrawCards;
            public Vector2 HandCards;
            public Vector2 DiscardCards;
            public Vector2 GraveCards;
            public Vector2 ExileCards;
            public float VerticalStep;
            public float HorizontalStep;
            public Vector2 DeckArrow;
            public Vector2 GraveArrow;
            public Vector2 DrawArrow;
            public Vector2 DiscardArrow;
            public Vector2 HandArrow;
            public Vector2 ExileArrow;
            public float DeckArrowLength;
            public float GraveArrowLength;
            public float DrawArrowLength;
            public float DiscardArrowLength;
            public float HandArrowLength;
            public float ExileArrowLength;
        }

        static BattleLogMovePopup _instance;

        readonly List<CardData> _deck = new List<CardData>();
        readonly List<CardData> _draw = new List<CardData>();
        readonly List<CardData> _hand = new List<CardData>();
        readonly List<CardData> _discard = new List<CardData>();
        readonly List<CardData> _grave = new List<CardData>();
        readonly List<CardData> _exile = new List<CardData>();

        RectTransform _panel;
        TMP_FontAsset _font;
        int _openId = -1;
        Sprite _arrowUp;
        Sprite _arrowDown;
        Sprite _arrowRight;
        Sprite _arrowShortDown;
        NewCardView _cardPrefab;
        Tune _tune;

        const int PreviewId = -2;

        public static void Show(int entryId, TMP_FontAsset font, Component anchor, Tune tune)
        {
            BattleLog.Entry entry = BattleLog.Find(entryId);
            if (entry == null || !entry.HasMoves) return;
            Ensure(anchor).Open(entry, font, tune);
        }

        public static void HidePreview()
        {
            Hide(PreviewId);
        }

        public static void ShowPreview(Component anchor, Tune tune)
        {
            Ensure(anchor).OpenPreview(tune);
        }

        public static void Hide(int entryId)
        {
            if (_instance == null || _instance._openId != entryId) return;
            _instance._openId = -1;
            if (_instance._panel != null) _instance._panel.gameObject.SetActive(false);
        }

        static BattleLogMovePopup Ensure(Component anchor)
        {
            if (_instance != null) return _instance;
            Canvas canvas = anchor != null ? anchor.GetComponentInParent<Canvas>() : null;
            if (canvas != null) canvas = canvas.rootCanvas;
            if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>();
            var go = new GameObject("LogMovePopup");
            if (canvas != null) go.transform.SetParent(canvas.transform, false);
            _instance = go.AddComponent<BattleLogMovePopup>();
            return _instance;
        }

        void Awake()
        {
            _panel = gameObject.GetComponent<RectTransform>();
            if (_panel == null) _panel = gameObject.AddComponent<RectTransform>();
            _panel.anchorMin = Vector2.zero;
            _panel.anchorMax = Vector2.one;
            _panel.offsetMin = Vector2.zero;
            _panel.offsetMax = Vector2.zero;
            _panel.pivot = new Vector2(0.5f, 0.5f);

            var group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = false;
            group.interactable = true;
            gameObject.SetActive(false);
        }

        void KeepCardsSolid()
        {
            CanvasGroup group = GetComponent<CanvasGroup>();
            if (group == null) return;
            group.alpha = 1f;
            group.blocksRaycasts = false;
            group.interactable = true;
        }

        void Open(BattleLog.Entry entry, TMP_FontAsset font, Tune tune)
        {
            KeepCardsSolid();
            _font = font;
            _tune = tune;
            if (_tune.VerticalStep < 8f) _tune.VerticalStep = 48f;
            if (_tune.HorizontalStep < 8f) _tune.HorizontalStep = 72f;
            _openId = entry.Id;
            Collect(entry);
            if (_deck.Count + _draw.Count + _hand.Count + _discard.Count + _grave.Count + _exile.Count == 0)
            {
                Hide(entry.Id);
                return;
            }

            LoadArt();
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(transform.GetChild(i).gameObject);
            Build();
        }

        void OpenPreview(Tune tune)
        {
            KeepCardsSolid();
            _font = null;
            _tune = tune;
            if (_tune.VerticalStep < 8f) _tune.VerticalStep = 48f;
            if (_tune.HorizontalStep < 8f) _tune.HorizontalStep = 72f;
            _openId = PreviewId;
            FillPreviewCards();
            LoadArt();
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(transform.GetChild(i).gameObject);
            Build();
        }

        void FillPreviewCards()
        {
            CardData sample = CardDatabase.GetById(100);
            if (sample == null && CardDatabase.All != null && CardDatabase.All.Count > 0)
                sample = CardDatabase.All[0];
            _deck.Clear();
            _draw.Clear();
            _hand.Clear();
            _discard.Clear();
            _grave.Clear();
            _exile.Clear();
            for (int i = 0; i < MaxShown; i++)
            {
                _deck.Add(sample);
                _draw.Add(sample);
                _hand.Add(sample);
                _discard.Add(sample);
                _grave.Add(sample);
                _exile.Add(sample);
            }
        }

        void LoadArt()
        {
            if (_arrowUp == null) _arrowUp = Resources.Load<Sprite>("UI/Tab/UI_Arrow_1");
            if (_arrowDown == null) _arrowDown = Resources.Load<Sprite>("UI/Tab/UI_Arrow_2");
            if (_arrowRight == null) _arrowRight = Resources.Load<Sprite>("UI/Tab/UI_Arrow_3");
            if (_arrowShortDown == null) _arrowShortDown = Resources.Load<Sprite>("UI/Tab/UI_Arrow_4");
            if (_cardPrefab == null)
            {
                CardHandHUD hud = Object.FindFirstObjectByType<CardHandHUD>(FindObjectsInactive.Include);
                if (hud != null) _cardPrefab = hud.CardPrefab;
            }
        }

        void Collect(BattleLog.Entry entry)
        {
            _deck.Clear();
            _draw.Clear();
            _hand.Clear();
            _discard.Clear();
            _grave.Clear();
            _exile.Clear();
            for (int i = 0; i < entry.Moves.Count; i++)
            {
                BattleLog.CardMove move = entry.Moves[i];
                if (move == null || move.Data == null) continue;
                switch (move.Zone)
                {
                    case BattleLog.MoveZone.Deck: _deck.Add(move.Data); break;
                    case BattleLog.MoveZone.Draw: _draw.Add(move.Data); break;
                    case BattleLog.MoveZone.Hand: _hand.Add(move.Data); break;
                    case BattleLog.MoveZone.Discard: _discard.Add(move.Data); break;
                    case BattleLog.MoveZone.Graveyard: _grave.Add(move.Data); break;
                    case BattleLog.MoveZone.Exile: _exile.Add(move.Data); break;
                }
            }
        }

        void Build()
        {
            Canvas.ForceUpdateCanvases();
            RectTransform parent = _panel.parent as RectTransform;
            float w = parent != null && parent.rect.width > 10f ? parent.rect.width : 1920f;
            float h = parent != null && parent.rect.height > 10f ? parent.rect.height : 1080f;

            Dim();

            float flowArrowY = -h * 0.36f;
            float drawY = flowArrowY + 34f + 24f + CardHeight * 0.5f;
            Vector2 deckPos = new Vector2(-w * 0.40f, h * 0.18f) + _tune.DeckCards;
            Vector2 gravePos = new Vector2(w * 0.40f, h * 0.18f) + _tune.GraveCards;
            Vector2 drawPos = new Vector2(0f, drawY) + _tune.DrawCards;
            Vector2 discardPos = new Vector2(w * 0.22f, drawY) + _tune.DiscardCards;
            Vector2 handPos = new Vector2(0f, -h * 0.22f) + _tune.HandCards;
            Vector2 exilePos = new Vector2(0f, h * 0.30f) + _tune.ExileCards;

            Span deck = Layout(_deck, deckPos, true);
            Span grave = Layout(_grave, gravePos, true);
            Span draw = Layout(_draw, drawPos, false);
            Span discard = Layout(_discard, discardPos, false);
            Span hand = Layout(_hand, handPos, false);
            Span exile = Layout(_exile, exilePos, false);

            if (deck.Visible) DownArrow(deck, -h * 0.5f + 120f, _tune.DeckArrow, _tune.DeckArrowLength);
            if (grave.Visible) DownArrow(grave, -h * 0.5f + 120f, _tune.GraveArrow, _tune.GraveArrowLength);
            if (draw.Visible) RightArrow(-w * 0.5f + 40f, -40f, flowArrowY, _tune.DrawArrow, _tune.DrawArrowLength);
            if (discard.Visible) RightArrow(40f, w * 0.5f - 40f, flowArrowY, _tune.DiscardArrow, _tune.DiscardArrowLength);
            if (hand.Visible) ShortDown(hand, _tune.HandArrow, _tune.HandArrowLength);
            if (exile.Visible) UpArrow(exile, _tune.ExileArrow, _tune.ExileArrowLength);

            if (_deck.Count >= 6) PlaceCount(_deck.Count, deckPos);
            if (_grave.Count >= 6) PlaceCount(_grave.Count, gravePos);
            if (_draw.Count >= 6) PlaceCount(_draw.Count, drawPos);
            if (_discard.Count >= 6) PlaceCount(_discard.Count, discardPos);
            if (_hand.Count >= 6) PlaceCount(_hand.Count, handPos);
            if (_exile.Count >= 6) PlaceCount(_exile.Count, exilePos);

            Transform dim = transform.Find("Dim");
            if (dim != null) dim.SetAsFirstSibling();
        }

        void Dim()
        {
            var go = new GameObject("Dim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = go.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.55f);
            image.raycastTarget = false;
        }

        struct Span
        {
            public float Left, Right, Top, Bottom;
            public bool Visible;
        }

        Span Layout(List<CardData> cards, Vector2 center, bool vertical)
        {
            Span span = new Span();
            if (cards.Count == 0) return span;
            int shown = Mathf.Min(MaxShown, cards.Count);
            bool overflow = cards.Count >= 6;
            float step = vertical ? _tune.VerticalStep : _tune.HorizontalStep;
            float spanSize = (shown - 1) * step;
            float start = -spanSize * 0.5f;

            for (int i = 0; i < shown; i++)
            {
                float along = start + i * step;
                Vector2 pos = vertical
                    ? new Vector2(center.x, center.y - along)
                    : new Vector2(center.x + along, center.y);
                PlaceCard(cards[i], pos, overflow);
            }

            if (vertical)
            {
                span.Left = center.x - CardWidth * 0.5f;
                span.Right = center.x + CardWidth * 0.5f;
                span.Top = center.y + spanSize * 0.5f + CardHeight * 0.5f;
                span.Bottom = center.y - spanSize * 0.5f - CardHeight * 0.5f;
            }
            else
            {
                span.Left = center.x - spanSize * 0.5f - CardWidth * 0.5f;
                span.Right = center.x + spanSize * 0.5f + CardWidth * 0.5f;
                span.Top = center.y + CardHeight * 0.5f;
                span.Bottom = center.y - CardHeight * 0.5f;
            }
            span.Visible = true;
            return span;
        }

        void PlaceCard(CardData data, Vector2 pos, bool darken)
        {
            if (_cardPrefab == null || data == null) return;
            NewCardView view = Instantiate(_cardPrefab, transform);
            view.ApplyShopPreview(data);
            RectTransform rect = view.GetComponent<RectTransform>();
            Vector2 native = rect.sizeDelta;
            float sx = native.x > 1f ? CardWidth / native.x : 1f;
            float sy = native.y > 1f ? CardHeight / native.y : 1f;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = new Vector3(sx, sy, 1f);
            rect.anchoredPosition = pos;

            CanvasGroup group = view.GetComponent<CanvasGroup>();
            if (group == null) group = view.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = false;
            group.interactable = true;
            Button button = view.GetComponent<Button>();
            if (button != null) button.enabled = false;
            Graphic[] graphics = view.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                graphics[i].raycastTarget = false;
                graphics[i].canvasRenderer.SetAlpha(1f);
            }
            view.enabled = false;
            rect.SetAsLastSibling();

            if (!darken) return;
            var shadeGo = new GameObject("Shade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            shadeGo.transform.SetParent(rect, false);
            RectTransform shade = shadeGo.GetComponent<RectTransform>();
            shade.anchorMin = Vector2.zero;
            shade.anchorMax = Vector2.one;
            shade.offsetMin = Vector2.zero;
            shade.offsetMax = Vector2.zero;
            Image shadeImage = shadeGo.GetComponent<Image>();
            shadeImage.color = new Color(0f, 0f, 0f, 0.55f);
            shadeImage.raycastTarget = false;
        }

        void PlaceCount(int count, Vector2 center)
        {
            var go = new GameObject("Count", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(240f, 100f);
            rect.anchoredPosition = center;

            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            label.text = "+" + count;
            if (_font != null) label.font = _font;
            label.fontSize = 72f;
            label.color = new Color(0.1f, 0.07f, 0.04f, 1f);
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            label.outlineWidth = 0.2f;
            label.outlineColor = new Color(0.96f, 0.91f, 0.78f, 1f);
        }

        void DownArrow(Span cards, float yEnd, Vector2 offset, float extra)
        {
            float height = cards.Bottom - yEnd + extra;
            if (height < 40f) height = 40f;
            Arrow(_arrowDown, new Vector2((cards.Left + cards.Right) * 0.5f, cards.Bottom) + offset, new Vector2(67f, height), new Vector2(0.5f, 1f));
        }

        void ShortDown(Span cards, Vector2 offset, float extra)
        {
            float height = 140f + extra;
            if (height < 40f) height = 40f;
            Arrow(_arrowShortDown, new Vector2((cards.Left + cards.Right) * 0.5f, cards.Bottom + 70f) + offset, new Vector2(67f, height), new Vector2(0.5f, 1f));
        }

        void UpArrow(Span cards, Vector2 offset, float extra)
        {
            float height = 218f + extra;
            if (height < 40f) height = 40f;
            Arrow(_arrowUp, new Vector2(cards.Right + 28f, (cards.Top + cards.Bottom) * 0.5f) + offset, new Vector2(67f, height), new Vector2(0.5f, 0f));
        }

        void RightArrow(float xStart, float xEnd, float y, Vector2 offset, float extra)
        {
            float width = xEnd - xStart + extra;
            if (width < 40f) width = 40f;
            Arrow(_arrowRight, new Vector2(xStart, y) + offset, new Vector2(width, 67f), new Vector2(0f, 0.5f));
        }

        void Arrow(Sprite sprite, Vector2 pos, Vector2 size, Vector2 pivot)
        {
            if (sprite == null) return;
            var go = new GameObject("Arrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = false;
            image.raycastTarget = false;
        }
    }
}
