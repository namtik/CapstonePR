using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Battle.UI
{
    // BattleLogFrame 안에서 기록을 오른쪽에서 왼쪽으로 밀어 넣는다.
    // 한 기록은 152x105 패널이다. 그림은 135x100, 그 뒤는 152x102 회색이다.
    public class BattleLogView : MonoBehaviour
    {
        public const float PanelWidth = 152f;
        public const float PanelHeight = 105f;
        public const float BackWidth = 152f;
        public const float BackHeight = 102f;
        public const float IconWidth = 135f;
        public const float IconHeight = 100f;

        [SerializeField] Sprite infoSprite;
        [SerializeField] Sprite wideSprite;
        [SerializeField] Sprite cardPanel;
        [SerializeField] Sprite drawPanel;
        [SerializeField] Sprite enemyPanel;
        [SerializeField] Sprite relicPanel;
        [SerializeField] Sprite awakenPanel;
        [SerializeField] TMP_FontAsset font;
        [SerializeField] Color numberColor = new Color(0.957f, 0.808f, 0.549f, 1f);
        [Tooltip("숫자 정렬. Center 는 가로·세로 가운데.")]
        [SerializeField] TextAlignmentOptions numberAlignment = TextAlignmentOptions.Center;
        [Tooltip("숫자 글자 크기.")]
        [SerializeField] float fontSize = 40f;
        [Header("기록 이름")]
        [Tooltip("패널 아래 이름. 비우면 위 글꼴을 사용합니다.")]
        [SerializeField] TMP_FontAsset labelFont;
        [Tooltip("아래 칸 글자 크기.")]
        [SerializeField] float labelFontSize = 16f;
        [Tooltip("아래 칸 글자 색.")]
        [SerializeField] Color labelColor = new Color(0.96f, 0.93f, 0.86f, 1f);
        [Tooltip("아래 칸 정렬.")]
        [SerializeField] TextAlignmentOptions labelAlignment = TextAlignmentOptions.Center;
        [Tooltip("아래 칸 외곽선 두께. 0이면 외곽선이 없습니다.")]
        [Range(0f, 1f)]
        [SerializeField] float labelOutlineWidth = 0.15f;
        [Tooltip("아래 칸 외곽선 색.")]
        [SerializeField] Color labelOutlineColor = Color.black;
        [Tooltip("숫자를 좌우로 미는 거리. 양수면 오른쪽, 음수면 왼쪽.")]
        [SerializeField] float numberOffsetX = 0f;
        [Tooltip("숫자 왼쪽 아이콘(공격, 방패, 화상, 드로우) 한 변 길이.")]
        [SerializeField] float badgeSize = 48f;
        [Tooltip("아이콘을 오른쪽으로 미는 거리. 양수면 오른쪽, 음수면 왼쪽.")]
        [SerializeField] float badgeOffsetX = 0f;
        [SerializeField] float gap = 10f;
        [SerializeField] float slideSeconds = 0.32f;

        [Header("팝업 배치 미리보기")]
        [Tooltip("켜 두면 플레이 중에 화살표 6개와 구역마다 카드 5장이 계속 떠 있습니다. 위치 맞춘 뒤 끄면 됩니다.")]
        [SerializeField] bool popupLayoutPreview = true;

        [Header("콤보 20개 초과")]
        [Tooltip("초과 개수 외곽선 두께. 0이면 외곽선이 없습니다.")]
        [Range(0f, 1f)]
        [SerializeField] float comboOverflowOutlineWidth = 0.2f;
        [Tooltip("초과 개수 외곽선 색.")]
        [SerializeField] Color comboOverflowOutlineColor = Color.black;

        [Header("팝업 카드 위치")]
        [Tooltip("기본 위치에서 더하는 값입니다. X 양수는 오른쪽, Y 양수는 위입니다.")]
        [SerializeField] Vector2 deckCardOffset;
        [Tooltip("드로우로 뽑힌 카드. X 양수는 오른쪽, Y 양수는 위입니다.")]
        [SerializeField] Vector2 drawCardOffset;
        [Tooltip("패에 추가된 카드. X 양수는 오른쪽, Y 양수는 위입니다.")]
        [SerializeField] Vector2 handCardOffset;
        [Tooltip("패에서 버린 카드. 가로로 겹칩니다. X 양수는 오른쪽, Y 양수는 위입니다.")]
        [SerializeField] Vector2 discardCardOffset;
        [Tooltip("묘지로 보낸 카드. X 양수는 오른쪽, Y 양수는 위입니다.")]
        [SerializeField] Vector2 graveCardOffset;
        [Tooltip("소멸한 카드. X 양수는 오른쪽, Y 양수는 위입니다.")]
        [SerializeField] Vector2 exileCardOffset;

        [Header("팝업 카드 겹침")]
        [Tooltip("왼쪽(덱)과 오른쪽(묘지) 카드가 세로로 겹치는 간격. 작을수록 더 겹칩니다.")]
        [SerializeField] float verticalCardStep = 48f;
        [Tooltip("드로우, 패, 버리기, 소멸 카드가 가로로 겹치는 간격. 작을수록 더 겹칩니다.")]
        [SerializeField] float horizontalCardStep = 72f;

        [Header("팝업 화살표 위치")]
        [Tooltip("덱 카드 아래 긴 화살표. X 양수는 오른쪽, Y 양수는 위입니다.")]
        [SerializeField] Vector2 deckArrowOffset;
        [Tooltip("묘지 카드 아래 긴 화살표. X 양수는 오른쪽, Y 양수는 위입니다.")]
        [SerializeField] Vector2 graveArrowOffset;
        [Tooltip("드로우 가로 화살표. X 양수는 오른쪽, Y 양수는 위입니다.")]
        [SerializeField] Vector2 drawArrowOffset;
        [Tooltip("버리기 가로 화살표. X 양수는 오른쪽, Y 양수는 위입니다.")]
        [SerializeField] Vector2 discardArrowOffset;
        [Tooltip("패로 가져온 카드의 짧은 아래 화살표. X 양수는 오른쪽, Y 양수는 위입니다.")]
        [SerializeField] Vector2 handArrowOffset;
        [Tooltip("소멸 위 화살표. X 양수는 오른쪽, Y 양수는 위입니다.")]
        [SerializeField] Vector2 exileArrowOffset;

        [Header("팝업 화살표 길이")]
        [Tooltip("덱 아래 긴 화살표. 기본 길이에 더합니다. 양수면 길어지고 음수면 짧아집니다.")]
        [SerializeField] float deckArrowLength;
        [Tooltip("묘지 아래 긴 화살표. 기본 길이에 더합니다. 양수면 길어지고 음수면 짧아집니다.")]
        [SerializeField] float graveArrowLength;
        [Tooltip("드로우 가로 화살표. 기본 길이에 더합니다. 양수면 길어지고 음수면 짧아집니다.")]
        [SerializeField] float drawArrowLength;
        [Tooltip("버리기 가로 화살표. 기본 길이에 더합니다. 양수면 길어지고 음수면 짧아집니다.")]
        [SerializeField] float discardArrowLength;
        [Tooltip("패로 가져온 카드의 짧은 아래 화살표. 기본 길이에 더합니다. 양수면 길어지고 음수면 짧아집니다.")]
        [SerializeField] float handArrowLength;
        [Tooltip("소멸 위 화살표. 기본 길이에 더합니다. 양수면 길어지고 음수면 짧아집니다.")]
        [SerializeField] float exileArrowLength;

        bool _settingsReady;
        float _appliedFontSize;
        float _appliedNumberOffsetX;
        float _appliedBadgeSize;
        float _appliedBadgeOffsetX;
        Color _appliedNumberColor;
        TextAlignmentOptions _appliedAlignment;
        TMP_FontAsset _appliedFont;
        TMP_FontAsset _appliedLabelFont;
        float _appliedLabelFontSize;
        Color _appliedLabelColor;
        TextAlignmentOptions _appliedLabelAlignment;
        float _appliedLabelOutlineWidth;
        Color _appliedLabelOutlineColor;
        float _appliedComboOverflowOutlineWidth = -1f;
        Color _appliedComboOverflowOutlineColor;

        RectTransform _rect;
        readonly Dictionary<int, Widget> _widgets = new Dictionary<int, Widget>();
        readonly List<int> _scratch = new List<int>();
        float _slotWidth = 120f;
        float _slotHeight = 110f;
        float _gap = 10f;
        float _contentInset;
        float _appliedWidth = -1f;
        float _appliedHeight = -1f;
        float _appliedInset = -1f;
        float _anim = 1f;

        class Widget
        {
            public RectTransform Rect;
            public float Width;
            public float FromX;
            public float ToX;
            public bool Fresh;
            public bool Dying;
        }

        void Awake()
        {
            _rect = (RectTransform)transform;
            ResolveAssets();
        }

        void OnEnable()
        {
            BattleLog.Changed += HandleChanged;
            RebuildTrack();
            Sync(false);
        }

        void OnDisable()
        {
            BattleLog.Changed -= HandleChanged;
        }

        void Update()
        {
            ApplyComboOverflowOutline();
            if (SettingsDrifted())
            {
                DestroyAllWidgets();
                Sync(false);
            }

            if (_anim >= 1f) return;
            float duration = Mathf.Max(0.05f, slideSeconds);
            _anim = Mathf.Min(1f, _anim + Time.unscaledDeltaTime / duration);
            float t = 1f - (1f - _anim) * (1f - _anim);
            Apply(t);
            if (_anim >= 1f) DestroyDying();
        }

        void ApplyComboOverflowOutline()
        {
            if (Mathf.Approximately(_appliedComboOverflowOutlineWidth, comboOverflowOutlineWidth)
                && _appliedComboOverflowOutlineColor == comboOverflowOutlineColor)
                return;
            PushComboOverflowOutline();
        }

        void PushComboOverflowOutline()
        {
            _appliedComboOverflowOutlineWidth = comboOverflowOutlineWidth;
            _appliedComboOverflowOutlineColor = comboOverflowOutlineColor;
            BattleLogCombatPopup.SetOverflowOutline(comboOverflowOutlineWidth, comboOverflowOutlineColor);
        }

        void HandleChanged()
        {
            Sync(true);
        }

        // 칸 너비 계산용 자리만 잡아 둔다. 기록이 없으면 빈 칸은 보이지 않는다.
        public void RebuildTrack()
        {
            if (_rect == null) _rect = (RectTransform)transform;
            ResolveAssets();
            Measure();

            for (int i = 0; i < BattleLog.MaxSlots; i++)
            {
                string slotName = "LogSlot_" + i;
                Transform found = transform.Find(slotName);
                RectTransform slot;
                Image image;
                if (found == null)
                {
                    GameObject go = new GameObject(slotName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    go.transform.SetParent(transform, false);
                    slot = go.GetComponent<RectTransform>();
                    image = go.GetComponent<Image>();
                }
                else
                {
                    slot = (RectTransform)found;
                    image = found.GetComponent<Image>();
                    if (image == null) image = found.gameObject.AddComponent<Image>();
                }

                PlaceEntry(slot, _contentInset + i * (_slotWidth + _gap), _slotWidth, _slotHeight);
                slot.gameObject.layer = gameObject.layer;
                image.sprite = infoSprite;
                image.color = Color.white;
                image.preserveAspect = true;
                image.raycastTarget = false;
                slot.gameObject.SetActive(false);
                slot.SetSiblingIndex(i);
            }
        }

        void Sync(bool animate)
        {
            Measure();
            IReadOnlyList<BattleLog.Entry> entries = BattleLog.Entries;
            var keep = new HashSet<int>();
            int cursor = 0;

            // 가장 최근 기록이 맨 왼쪽. 새 기록은 왼쪽에서 들어오고 이전 기록은 오른쪽으로 밀린다.
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                BattleLog.Entry entry = entries[i];
                keep.Add(entry.Id);
                Widget widget = GetOrCreate(entry);
                float target = _contentInset + cursor * (_slotWidth + _gap);
                widget.ToX = target;
                if (!animate) widget.FromX = target;
                else if (widget.Fresh) widget.FromX = -widget.Width - _gap;
                else widget.FromX = widget.Rect.anchoredPosition.x;
                widget.Fresh = false;
                widget.Dying = false;
                cursor += entry.Slots;
            }

            float exitRight = _rect != null ? _rect.rect.width : _contentInset + cursor * (_slotWidth + _gap);
            _scratch.Clear();
            foreach (KeyValuePair<int, Widget> pair in _widgets)
            {
                if (keep.Contains(pair.Key)) continue;
                Widget widget = pair.Value;
                widget.Dying = true;
                widget.FromX = widget.Rect.anchoredPosition.x;
                widget.ToX = exitRight;
                if (!animate) _scratch.Add(pair.Key);
            }

            if (!animate)
            {
                for (int i = 0; i < _scratch.Count; i++) DestroyWidget(_scratch[i]);
                Apply(1f);
                _anim = 1f;
            }
            else
            {
                _anim = 0f;
                Apply(0f);
            }

            BringEntriesToFront();
            RememberSettings();
        }

        Widget GetOrCreate(BattleLog.Entry entry)
        {
            Widget widget;
            if (_widgets.TryGetValue(entry.Id, out widget)) return widget;

            float width = _slotWidth;
            GameObject root = new GameObject("LogEntry", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.layer = gameObject.layer;
            root.transform.SetParent(transform, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            PlaceEntry(rect, _rect.rect.width, width, _slotHeight);

            Image plate = root.GetComponent<Image>();
            plate.sprite = null;
            plate.color = new Color(1f, 1f, 1f, 0f);
            plate.raycastTarget = true;
            BuildPanel(rect, entry);
            LogEntryHover hover = root.AddComponent<LogEntryHover>();
            hover.EntryId = entry.Id;
            hover.View = this;

            widget = new Widget
            {
                Rect = rect,
                Width = width,
                Fresh = true
            };
            _widgets[entry.Id] = widget;
            return widget;
        }

        void BuildPanel(RectTransform root, BattleLog.Entry entry)
        {
            RectTransform back = CreateChild(root, "Back");
            Place(back, 0f, BackWidth, BackHeight);
            Image backImage = back.GetComponent<Image>();
            backImage.color = new Color(0.45f, 0.45f, 0.45f, 1f);
            backImage.raycastTarget = false;

            RectTransform icon = CreateChild(root, "Icon");
            Place(icon, (PanelWidth - IconWidth) * 0.5f, IconWidth, IconHeight);
            Image iconImage = icon.GetComponent<Image>();
            iconImage.sprite = entry.Icon;
            iconImage.color = Color.white;
            iconImage.preserveAspect = false;
            iconImage.enabled = entry.Icon != null;
            iconImage.raycastTarget = false;

            RectTransform frame = CreateChild(root, "Frame");
            Place(frame, 0f, PanelWidth, PanelHeight);
            Image frameImage = frame.GetComponent<Image>();
            frameImage.sprite = FrameFor(entry.Category);
            frameImage.color = Color.white;
            frameImage.preserveAspect = false;
            frameImage.enabled = frameImage.sprite != null;
            frameImage.raycastTarget = false;

            BuildLabel(root, entry.Caption);
        }

        void BuildLabel(RectTransform root, string value)
        {
            GameObject textGo = new GameObject("Label", typeof(RectTransform));
            textGo.layer = gameObject.layer;
            textGo.transform.SetParent(root, false);
            RectTransform rect = textGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(136f, 20f);
            rect.anchoredPosition = new Vector2(8f, -82f);

            TextMeshProUGUI label = textGo.AddComponent<TextMeshProUGUI>();
            label.text = value ?? string.Empty;
            TMP_FontAsset usedFont = labelFont != null ? labelFont : font;
            if (usedFont != null) label.font = usedFont;
            label.fontSize = Mathf.Max(8f, labelFontSize);
            label.color = labelColor;
            label.alignment = labelAlignment;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.enableAutoSizing = false;
            label.raycastTarget = false;
            float outline = Mathf.Clamp01(labelOutlineWidth);
            if (!Mathf.Approximately(label.outlineWidth, outline)) label.outlineWidth = outline;
            label.outlineColor = labelOutlineColor;
        }

        RectTransform CreateChild(RectTransform parent, string childName)
        {
            GameObject go = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = gameObject.layer;
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        bool _previewShown;
        BattleLogMovePopup.Tune _shownTune;

        void LateUpdate()
        {
            if (!popupLayoutPreview)
            {
                if (_previewShown)
                {
                    BattleLogMovePopup.HidePreview();
                    _previewShown = false;
                }
                return;
            }

            BattleLogMovePopup.Tune tune = CurrentTune();
            if (_previewShown && TuneSame(_shownTune, tune)) return;
            _shownTune = tune;
            _previewShown = true;
            BattleLogMovePopup.ShowPreview(this, tune);
        }

        public void ToggleMaxComboPreview()
        {
            if (BattleLogCombatPopup.IsMaxComboPreviewOpen)
            {
                BattleLogCombatPopup.HideMaxComboPreview();
                return;
            }

            BattleTabPanel panel = GetComponentInParent<BattleTabPanel>();
            if (panel != null) panel.Show();
            BattleLogCombatPopup.ShowMaxComboPreview(this);
        }

        public void ToggleOverflowComboPreview()
        {
            if (BattleLogCombatPopup.IsOverflowComboPreviewOpen)
            {
                BattleLogCombatPopup.HideOverflowComboPreview();
                return;
            }

            BattleTabPanel panel = GetComponentInParent<BattleTabPanel>();
            if (panel != null) panel.Show();
            PushComboOverflowOutline();
            BattleLogCombatPopup.ShowOverflowComboPreview(this);
        }

        void ShowMoves(int entryId)
        {
            if (popupLayoutPreview) return;
            TMP_FontAsset used = labelFont != null ? labelFont : font;
            BattleLogMovePopup.Show(entryId, used, this, CurrentTune());
            BattleLogCombatPopup.Show(entryId, this);
            ShowAttackHighlight(entryId);
        }

        void ShowAttackHighlight(int entryId)
        {
            BattleTabPanel panel = GetComponentInParent<BattleTabPanel>();
            if (panel == null) return;
            BattleLog.Entry entry = BattleLog.Find(entryId);
            if (entry != null && entry.AttackIndex >= 0)
                panel.HighlightEnemyAttack(entry.EnemyAliveIndex, entry.AttackIndex);
            else
                panel.ClearAttackHighlight();
            if (entry != null && entry.DisruptionIndex >= 0)
                panel.ShowDisruptionLabel(entry.EnemyAliveIndex, entry.DisruptionIndex);
            else
                panel.ClearDisruptionLabel();
        }

        void ClearAttackHighlight()
        {
            BattleTabPanel panel = GetComponentInParent<BattleTabPanel>();
            if (panel != null)
            {
                panel.ClearAttackHighlight();
                panel.ClearDisruptionLabel();
            }
        }

        BattleLogMovePopup.Tune CurrentTune()
        {
            return new BattleLogMovePopup.Tune
            {
                DeckCards = deckCardOffset,
                DrawCards = drawCardOffset,
                HandCards = handCardOffset,
                DiscardCards = discardCardOffset,
                GraveCards = graveCardOffset,
                ExileCards = exileCardOffset,
                VerticalStep = Mathf.Max(8f, verticalCardStep),
                HorizontalStep = Mathf.Max(8f, horizontalCardStep),
                DeckArrow = deckArrowOffset,
                GraveArrow = graveArrowOffset,
                DrawArrow = drawArrowOffset,
                DiscardArrow = discardArrowOffset,
                HandArrow = handArrowOffset,
                ExileArrow = exileArrowOffset,
                DeckArrowLength = deckArrowLength,
                GraveArrowLength = graveArrowLength,
                DrawArrowLength = drawArrowLength,
                DiscardArrowLength = discardArrowLength,
                HandArrowLength = handArrowLength,
                ExileArrowLength = exileArrowLength
            };
        }

        static bool TuneSame(BattleLogMovePopup.Tune a, BattleLogMovePopup.Tune b)
        {
            return a.DeckCards == b.DeckCards && a.DrawCards == b.DrawCards
                && a.HandCards == b.HandCards && a.DiscardCards == b.DiscardCards
                && a.GraveCards == b.GraveCards
                && a.ExileCards == b.ExileCards
                && Mathf.Approximately(a.VerticalStep, b.VerticalStep)
                && Mathf.Approximately(a.HorizontalStep, b.HorizontalStep)
                && a.DeckArrow == b.DeckArrow && a.GraveArrow == b.GraveArrow
                && a.DrawArrow == b.DrawArrow && a.DiscardArrow == b.DiscardArrow
                && a.HandArrow == b.HandArrow && a.ExileArrow == b.ExileArrow
                && Mathf.Approximately(a.DeckArrowLength, b.DeckArrowLength)
                && Mathf.Approximately(a.GraveArrowLength, b.GraveArrowLength)
                && Mathf.Approximately(a.DrawArrowLength, b.DrawArrowLength)
                && Mathf.Approximately(a.DiscardArrowLength, b.DiscardArrowLength)
                && Mathf.Approximately(a.HandArrowLength, b.HandArrowLength)
                && Mathf.Approximately(a.ExileArrowLength, b.ExileArrowLength);
        }

        sealed class LogEntryHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public int EntryId;
            public BattleLogView View;

            public void OnPointerEnter(PointerEventData eventData)
            {
                if (View != null) View.ShowMoves(EntryId);
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                BattleLogMovePopup.Hide(EntryId);
                BattleLogCombatPopup.Hide(EntryId);
                if (View != null) View.ClearAttackHighlight();
            }

            void OnDisable()
            {
                BattleLogMovePopup.Hide(EntryId);
                BattleLogCombatPopup.Hide(EntryId);
                if (View != null) View.ClearAttackHighlight();
            }
        }

        Sprite FrameFor(BattleLog.Kind category)
        {
            switch (category)
            {
                case BattleLog.Kind.Card: return cardPanel;
                case BattleLog.Kind.Draw: return drawPanel;
                case BattleLog.Kind.Enemy: return enemyPanel;
                case BattleLog.Kind.Relic: return relicPanel;
                case BattleLog.Kind.Awaken: return awakenPanel;
                default: return cardPanel;
            }
        }

        void BuildWideContents(RectTransform root, BattleLog.Entry entry)
        {
            float pad = Mathf.Clamp(_slotHeight * 0.08f, 6f, 14f);
            float plate = Mathf.Max(8f, _slotHeight - pad * 2f);

            GameObject infoGo = new GameObject("Info", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            infoGo.layer = gameObject.layer;
            infoGo.transform.SetParent(root, false);
            RectTransform info = infoGo.GetComponent<RectTransform>();
            Place(info, pad, plate, plate);
            info.anchoredPosition = new Vector2(pad, -pad);
            Image infoImage = infoGo.GetComponent<Image>();
            infoImage.sprite = infoSprite;
            infoImage.preserveAspect = true;
            infoImage.raycastTarget = false;
            infoImage.color = Color.white;
            BuildIcon(info, entry.Icon, InfoInnerSize(info));
            BuildMonsterNumber(info, entry.MonsterNumber);

            float right = pad + plate + 6f + badgeOffsetX;
            if (entry.Badge != null)
            {
                float badge = BadgeSize();
                if (!entry.HasCaption)
                {
                    float areaLeft = pad + plate;
                    float areaRight = root.sizeDelta.x;
                    right = (areaLeft + areaRight - badge) * 0.5f + badgeOffsetX;
                }
                GameObject badgeGo = new GameObject("Badge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                badgeGo.layer = gameObject.layer;
                badgeGo.transform.SetParent(root, false);
                RectTransform badgeRect = badgeGo.GetComponent<RectTransform>();
                Place(badgeRect, right, badge, badge);
                badgeRect.anchoredPosition = new Vector2(right, -(_slotHeight - badge) * 0.5f);
                Image badgeImage = badgeGo.GetComponent<Image>();
                badgeImage.sprite = entry.Badge;
                badgeImage.preserveAspect = true;
                badgeImage.raycastTarget = false;
                badgeImage.color = Color.white;
                right += badge + 4f;
            }

            if (!entry.HasCaption) return;

            GameObject textGo = new GameObject("Number", typeof(RectTransform));
            textGo.layer = gameObject.layer;
            textGo.transform.SetParent(root, false);
            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.offsetMin = new Vector2(right + numberOffsetX, 4f);
            textRect.offsetMax = new Vector2(-8f + numberOffsetX, -4f);

            TextMeshProUGUI label = textGo.AddComponent<TextMeshProUGUI>();
            bool caption = !string.IsNullOrEmpty(entry.Caption);
            label.text = caption ? entry.Caption : entry.Number.ToString();
            float captionSize = Mathf.Max(8f, fontSize * 0.78f);
            label.fontSize = caption ? captionSize : Mathf.Max(8f, fontSize);
            if (caption)
            {
                label.enableAutoSizing = true;
                label.fontSizeMin = 14f;
                label.fontSizeMax = captionSize;
            }
            label.alignment = numberAlignment;
            label.color = numberColor;
            label.raycastTarget = false;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            if (font != null) label.font = font;
        }

        void BuildMonsterNumber(RectTransform parent, Sprite mark)
        {
            if (mark == null) return;
            float height = Mathf.Clamp(parent.sizeDelta.y * 0.34f, 16f, 40f);
            float aspect = mark.rect.height > 0f ? mark.rect.width / mark.rect.height : 1f;
            GameObject go = new GameObject("MonsterNumber", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(height * aspect, height);
            rect.anchoredPosition = Vector2.zero;
            Image image = go.GetComponent<Image>();
            image.sprite = mark;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = Color.white;
        }

        void BuildIcon(RectTransform parent, Sprite sprite, float size)
        {
            if (sprite == null) return;
            GameObject go = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = Vector2.zero;
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = Color.white;
        }

        // UI_Info 테두리 안쪽에 그림이 꽉 차게 맞춘다.
        float InfoInnerSize(RectTransform frame)
        {
            float w = frame.sizeDelta.x;
            float h = frame.sizeDelta.y;
            float aspect = 1f;
            if (infoSprite != null && infoSprite.rect.height > 0f)
                aspect = infoSprite.rect.width / infoSprite.rect.height;

            float fittedW = w;
            float fittedH = w / Mathf.Max(0.01f, aspect);
            if (fittedH > h)
            {
                fittedH = h;
                fittedW = h * aspect;
            }

            float border = 3f / 140f;
            if (infoSprite != null && infoSprite.rect.width > 0f)
                border = 3f / infoSprite.rect.width;
            return Mathf.Max(8f, Mathf.Min(fittedW, fittedH) * (1f - border * 2f));
        }

        float BadgeSize()
        {
            return Mathf.Clamp(badgeSize, 8f, Mathf.Max(8f, _slotHeight));
        }

        bool SettingsDrifted()
        {
            if (!_settingsReady) return false;
            return !Mathf.Approximately(_appliedFontSize, fontSize)
                || !Mathf.Approximately(_appliedNumberOffsetX, numberOffsetX)
                || !Mathf.Approximately(_appliedBadgeSize, badgeSize)
                || !Mathf.Approximately(_appliedBadgeOffsetX, badgeOffsetX)
                || _appliedNumberColor != numberColor
                || _appliedAlignment != numberAlignment
                || _appliedFont != font
                || _appliedLabelFont != labelFont
                || !Mathf.Approximately(_appliedLabelFontSize, labelFontSize)
                || _appliedLabelColor != labelColor
                || _appliedLabelAlignment != labelAlignment
                || !Mathf.Approximately(_appliedLabelOutlineWidth, labelOutlineWidth)
                || _appliedLabelOutlineColor != labelOutlineColor;
        }

        void RememberSettings()
        {
            _settingsReady = true;
            _appliedFontSize = fontSize;
            _appliedNumberOffsetX = numberOffsetX;
            _appliedBadgeSize = badgeSize;
            _appliedBadgeOffsetX = badgeOffsetX;
            _appliedNumberColor = numberColor;
            _appliedAlignment = numberAlignment;
            _appliedFont = font;
            _appliedLabelFont = labelFont;
            _appliedLabelFontSize = labelFontSize;
            _appliedLabelColor = labelColor;
            _appliedLabelAlignment = labelAlignment;
            _appliedLabelOutlineWidth = labelOutlineWidth;
            _appliedLabelOutlineColor = labelOutlineColor;
        }

        void DestroyAllWidgets()
        {
            foreach (KeyValuePair<int, Widget> pair in _widgets)
            {
                if (pair.Value.Rect != null) Destroy(pair.Value.Rect.gameObject);
            }
            _widgets.Clear();
        }

        void Apply(float t)
        {
            foreach (KeyValuePair<int, Widget> pair in _widgets)
            {
                Widget widget = pair.Value;
                if (widget.Rect == null) continue;
                Vector2 pos = widget.Rect.anchoredPosition;
                pos.x = Mathf.Lerp(widget.FromX, widget.ToX, t);
                pos.y = EntryTop();
                widget.Rect.anchoredPosition = pos;
            }
        }

        void DestroyDying()
        {
            _scratch.Clear();
            foreach (KeyValuePair<int, Widget> pair in _widgets)
            {
                if (pair.Value.Dying) _scratch.Add(pair.Key);
            }
            for (int i = 0; i < _scratch.Count; i++) DestroyWidget(_scratch[i]);
        }

        void DestroyWidget(int id)
        {
            Widget widget;
            if (!_widgets.TryGetValue(id, out widget)) return;
            _widgets.Remove(id);
            if (widget.Rect != null) Destroy(widget.Rect.gameObject);
        }

        void BringEntriesToFront()
        {
            var moving = new List<Transform>();
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.name == "LogEntry") moving.Add(child);
            }
            for (int i = 0; i < moving.Count; i++) moving[i].SetAsLastSibling();
        }

        void Measure()
        {
            _gap = Mathf.Max(0f, gap);
            _slotWidth = PanelWidth;
            _slotHeight = PanelHeight;
            Rect area = _rect != null ? _rect.rect : new Rect(0f, 0f, 0f, 0f);
            _appliedWidth = area.width;
            _appliedHeight = area.height;
            _appliedInset = _contentInset;
        }

        // 기록 글자 오른쪽부터 로그가 시작하도록 왼쪽 여백을 둔다.
        public void ApplyContentInset(float left)
        {
            if (_rect == null) _rect = (RectTransform)transform;
            _contentInset = Mathf.Max(0f, left);
            Rect area = _rect.rect;
            bool changed = Mathf.Abs(area.width - _appliedWidth) > 0.5f
                || Mathf.Abs(area.height - _appliedHeight) > 0.5f
                || Mathf.Abs(_contentInset - _appliedInset) > 0.5f;
            if (!changed) return;
            DestroyAllWidgets();
            Measure();
            Sync(false);
        }

        void PlaceEntry(RectTransform rect, float x, float width, float height)
        {
            Place(rect, x, width, height);
            Vector2 pos = rect.anchoredPosition;
            pos.y = EntryTop();
            rect.anchoredPosition = pos;
        }

        // 기록 띠가 카드보다 높으면 띠 한가운데에 둔다. 짧으면 위쪽에 붙여 둔다.
        float EntryTop()
        {
            float height = _rect != null ? _rect.rect.height : 0f;
            if (height <= _slotHeight) return 0f;
            return -(height - _slotHeight) * 0.5f;
        }

        static void Place(RectTransform rect, float x, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, 0f);
        }

        void ResolveAssets()
        {
            if (infoSprite == null) infoSprite = Resources.Load<Sprite>("UI/Tab/UI_Info");
            if (wideSprite == null) wideSprite = Resources.Load<Sprite>("UI/Tab/UI_BattleLog_Frame");
            if (cardPanel == null) cardPanel = Resources.Load<Sprite>("UI/Tab/카드 기록 패널");
            if (drawPanel == null) drawPanel = Resources.Load<Sprite>("UI/Tab/드로우 기록 패널");
            if (enemyPanel == null) enemyPanel = Resources.Load<Sprite>("UI/Tab/적 기록 패널");
            if (relicPanel == null) relicPanel = Resources.Load<Sprite>("UI/Tab/유물 기록 패널");
            if (awakenPanel == null) awakenPanel = Resources.Load<Sprite>("UI/Tab/각성 기록 패널");
            if (font == null && TMP_Settings.defaultFontAsset != null)
                font = TMP_Settings.defaultFontAsset;
        }
    }
}
