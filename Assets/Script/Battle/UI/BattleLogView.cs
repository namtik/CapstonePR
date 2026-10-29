using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Battle.UI
{
    // BattleLogFrame 안에서 기록을 오른쪽에서 왼쪽으로 밀어 넣는다.
    // 프레임의 RectTransform 은 건드리지 않고, 그 너비를 10칸으로 나눠 쓴다.
    public class BattleLogView : MonoBehaviour
    {
        [SerializeField] Sprite infoSprite;
        [SerializeField] Sprite wideSprite;
        [SerializeField] TMP_FontAsset font;
        [SerializeField] Color numberColor = new Color(0.957f, 0.808f, 0.549f, 1f);
        [Tooltip("숫자 정렬. Center 는 가로·세로 가운데.")]
        [SerializeField] TextAlignmentOptions numberAlignment = TextAlignmentOptions.Center;
        [Tooltip("숫자 글자 크기.")]
        [SerializeField] float fontSize = 40f;
        [Tooltip("숫자를 좌우로 미는 거리. 양수면 오른쪽, 음수면 왼쪽.")]
        [SerializeField] float numberOffsetX = 0f;
        [Tooltip("숫자 왼쪽 아이콘(공격, 방패, 화상, 드로우) 한 변 길이.")]
        [SerializeField] float badgeSize = 48f;
        [Tooltip("아이콘을 오른쪽으로 미는 거리. 양수면 오른쪽, 음수면 왼쪽.")]
        [SerializeField] float badgeOffsetX = 0f;
        [SerializeField] float gap = 10f;
        [SerializeField] float slideSeconds = 0.32f;

        bool _settingsReady;
        float _appliedFontSize;
        float _appliedNumberOffsetX;
        float _appliedBadgeSize;
        float _appliedBadgeOffsetX;
        Color _appliedNumberColor;
        TextAlignmentOptions _appliedAlignment;
        TMP_FontAsset _appliedFont;

        RectTransform _rect;
        readonly Dictionary<int, Widget> _widgets = new Dictionary<int, Widget>();
        readonly List<int> _scratch = new List<int>();
        float _slotWidth = 120f;
        float _slotHeight = 110f;
        float _gap = 10f;
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

                Place(slot, i * (_slotWidth + _gap), _slotWidth, _slotHeight);
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

            for (int i = 0; i < entries.Count; i++)
            {
                BattleLog.Entry entry = entries[i];
                keep.Add(entry.Id);
                Widget widget = GetOrCreate(entry);
                float target = cursor * (_slotWidth + _gap);
                widget.ToX = target;
                if (!animate) widget.FromX = target;
                else if (widget.Fresh) widget.FromX = _rect.rect.width;
                else widget.FromX = widget.Rect.anchoredPosition.x;
                widget.Fresh = false;
                widget.Dying = false;
                cursor += entry.Slots;
            }

            _scratch.Clear();
            foreach (KeyValuePair<int, Widget> pair in _widgets)
            {
                if (keep.Contains(pair.Key)) continue;
                Widget widget = pair.Value;
                widget.Dying = true;
                widget.FromX = widget.Rect.anchoredPosition.x;
                widget.ToX = -widget.Width - _gap;
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

            float width = entry.Slots == 1 ? _slotWidth : _slotWidth * 2f + _gap;
            GameObject root = new GameObject("LogEntry", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.layer = gameObject.layer;
            root.transform.SetParent(transform, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            Place(rect, _rect.rect.width, width, _slotHeight);

            Image plate = root.GetComponent<Image>();
            plate.raycastTarget = false;
            plate.color = Color.white;

            if (entry.Slots > 1)
            {
                plate.sprite = wideSprite;
                plate.preserveAspect = false;
                if (wideSprite == null) plate.color = new Color(0.16f, 0.09f, 0.04f, 1f);
                BuildWideContents(rect, entry);
            }
            else
            {
                plate.sprite = infoSprite;
                plate.preserveAspect = true;
                BuildIcon(rect, entry.Icon, InfoInnerSize(rect));
            }

            widget = new Widget
            {
                Rect = rect,
                Width = width,
                Fresh = true
            };
            _widgets[entry.Id] = widget;
            return widget;
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

            float right = pad + plate + 6f + badgeOffsetX;
            if (entry.Badge != null)
            {
                float badge = BadgeSize();
                if (!entry.HasNumber)
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

            if (!entry.HasNumber) return;

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
            label.text = entry.Number.ToString();
            label.fontSize = Mathf.Max(8f, fontSize);
            label.alignment = numberAlignment;
            label.color = numberColor;
            label.raycastTarget = false;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            if (font != null) label.font = font;
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
                || _appliedFont != font;
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
            Rect area = _rect != null ? _rect.rect : new Rect(0f, 0f, 0f, 0f);
            if (area.width < 10f || area.height < 10f)
            {
                _slotWidth = 120f;
                _slotHeight = 110f;
                return;
            }

            _slotHeight = area.height;
            float inner = area.width - _gap * (BattleLog.MaxSlots - 1);
            _slotWidth = inner / BattleLog.MaxSlots;
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
            if (font == null && TMP_Settings.defaultFontAsset != null)
                font = TMP_Settings.defaultFontAsset;
        }
    }
}
