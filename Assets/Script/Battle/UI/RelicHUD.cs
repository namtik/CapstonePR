using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Battle.Relic;

namespace Battle.UI
{
    /// <summary>
    /// 화면 좌상단에 보유 유물 아이콘을 획득 순서대로 표시한다.
    /// 아이콘에 마우스를 올리면 이름+설명 툴팁이 나타난다.
    /// </summary>
    public class RelicHUD : MonoBehaviour
    {
        public static RelicHUD Instance { get; private set; }

        private const float ICON_SIZE     = 60f;
        private const float ICON_SPACING  = 8f;
        private const float PANEL_PADDING = 10f;
        private const float TOOLTIP_W     = 220f;
        private const float TOOLTIP_H     = 100f;
        private const float TOOLTIP_PAD   = 10f;

        private RectTransform   _iconContainer;
        private GameObject      _tooltip;
        private Image           _tooltipIcon;
        private TextMeshProUGUI _tooltipName;
        private TextMeshProUGUI _tooltipDesc;
        private RectTransform   _tooltipRect;

        private readonly List<RelicDef>   _relics    = new List<RelicDef>();
        private readonly List<GameObject> _iconItems = new List<GameObject>();

        void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(gameObject); return; }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            BuildLayout();
            if (RelicManager.Instance != null)
                Refresh(RelicManager.Instance.OwnedRelics);
        }

        public void Refresh(IReadOnlyList<RelicDef> relics)
        {
            _relics.Clear();
            _relics.AddRange(relics);
            RebuildIcons();
        }

        // ── 레이아웃 구성 ──────────────────────────────────────────

        void BuildLayout()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            Transform canvasT = canvas.transform;

            // 아이콘 컨테이너: 캔버스 좌상단 고정
            var containerGo = new GameObject("RelicIconContainer", typeof(RectTransform));
            containerGo.transform.SetParent(canvasT, false);
            _iconContainer = (RectTransform)containerGo.transform;
            _iconContainer.anchorMin        = new Vector2(0f, 1f);
            _iconContainer.anchorMax        = new Vector2(0f, 1f);
            _iconContainer.pivot            = new Vector2(0f, 1f);
            _iconContainer.anchoredPosition = new Vector2(PANEL_PADDING, -PANEL_PADDING);

            var layout = containerGo.AddComponent<HorizontalLayoutGroup>();
            layout.spacing              = ICON_SPACING;
            layout.childForceExpandWidth  = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth      = false;
            layout.childControlHeight     = false;

            containerGo.AddComponent<ContentSizeFitter>().horizontalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            // 툴팁 패널: 캔버스 좌상단 앵커 (아이콘 컨테이너와 동일 기준)
            var tooltipGo = new GameObject("RelicTooltip", typeof(RectTransform));
            tooltipGo.transform.SetParent(canvasT, false);
            tooltipGo.transform.SetAsLastSibling();
            _tooltipRect = (RectTransform)tooltipGo.transform;
            _tooltipRect.anchorMin  = new Vector2(0f, 1f);
            _tooltipRect.anchorMax  = new Vector2(0f, 1f);
            _tooltipRect.pivot      = new Vector2(0f, 1f);
            _tooltipRect.sizeDelta  = new Vector2(TOOLTIP_W, TOOLTIP_H);
            _tooltip = tooltipGo;

            var bg = tooltipGo.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.12f, 0.93f);

            // 아이콘 (툴팁 좌측)
            var iconGo = new GameObject("TT_Icon", typeof(RectTransform));
            iconGo.transform.SetParent(tooltipGo.transform, false);
            var iconRect = (RectTransform)iconGo.transform;
            iconRect.anchorMin        = new Vector2(0f, 0.5f);
            iconRect.anchorMax        = new Vector2(0f, 0.5f);
            iconRect.pivot            = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(TOOLTIP_PAD, 0f);
            iconRect.sizeDelta        = new Vector2(40f, 40f);
            _tooltipIcon = iconGo.AddComponent<Image>();
            _tooltipIcon.color = new Color(0.6f, 0.5f, 0.2f, 1f); // 아이콘 없을 때 기본색

            float textX = TOOLTIP_PAD + 40f + 8f;
            float textW = TOOLTIP_W - textX - TOOLTIP_PAD;

            // 이름
            var nameGo = new GameObject("TT_Name", typeof(RectTransform));
            nameGo.transform.SetParent(tooltipGo.transform, false);
            var nameRect = (RectTransform)nameGo.transform;
            nameRect.anchorMin        = new Vector2(0f, 1f);
            nameRect.anchorMax        = new Vector2(0f, 1f);
            nameRect.pivot            = new Vector2(0f, 1f);
            nameRect.anchoredPosition = new Vector2(textX, -TOOLTIP_PAD);
            nameRect.sizeDelta        = new Vector2(textW, 22f);
            _tooltipName = nameGo.AddComponent<TextMeshProUGUI>();
            _tooltipName.fontSize     = 13f;
            _tooltipName.fontStyle    = FontStyles.Bold;
            _tooltipName.color        = Color.white;
            _tooltipName.overflowMode = TextOverflowModes.Overflow;

            // 설명
            var descGo = new GameObject("TT_Desc", typeof(RectTransform));
            descGo.transform.SetParent(tooltipGo.transform, false);
            var descRect = (RectTransform)descGo.transform;
            descRect.anchorMin        = new Vector2(0f, 1f);
            descRect.anchorMax        = new Vector2(0f, 1f);
            descRect.pivot            = new Vector2(0f, 1f);
            descRect.anchoredPosition = new Vector2(textX, -TOOLTIP_PAD - 26f);
            descRect.sizeDelta        = new Vector2(textW, 64f);
            _tooltipDesc = descGo.AddComponent<TextMeshProUGUI>();
            _tooltipDesc.fontSize     = 11f;
            _tooltipDesc.color        = new Color(0.85f, 0.85f, 0.85f, 1f);
            _tooltipDesc.overflowMode = TextOverflowModes.Overflow;

            _tooltip.SetActive(false);
        }

        void RebuildIcons()
        {
            if (_iconContainer == null) return;
            foreach (var go in _iconItems) if (go != null) Destroy(go);
            _iconItems.Clear();
            _tooltip?.SetActive(false);

            for (int i = 0; i < _relics.Count; i++)
                _iconItems.Add(CreateIconItem(_relics[i], i));
        }

        GameObject CreateIconItem(RelicDef relic, int index)
        {
            var go = new GameObject($"Relic_{relic.id}", typeof(RectTransform));
            go.transform.SetParent(_iconContainer, false);

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(ICON_SIZE, ICON_SIZE);

            var img = go.AddComponent<Image>();
            if (relic.icon != null)
            {
                img.sprite = relic.icon;
                img.color  = Color.white;
            }
            else
            {
                img.color = new Color(0.6f, 0.5f, 0.2f, 1f);
            }

            var outline = go.AddComponent<Outline>();
            outline.effectColor    = new Color(0.9f, 0.8f, 0.3f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);

            var trigger = go.AddComponent<EventTrigger>();
            int captured = index;
            var relicCaptured = relic;

            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => ShowTooltip(relicCaptured, captured));
            trigger.triggers.Add(enter);

            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => _tooltip?.SetActive(false));
            trigger.triggers.Add(exit);

            return go;
        }

        // ── 툴팁 ──────────────────────────────────────────────────

        void ShowTooltip(RelicDef relic, int iconIndex)
        {
            if (_tooltip == null || _tooltipRect == null) return;

            // 텍스트/아이콘 갱신
            if (_tooltipIcon != null)
            {
                _tooltipIcon.sprite = relic.icon;
                _tooltipIcon.color  = relic.icon != null ? Color.white : new Color(0.6f, 0.5f, 0.2f, 1f);
            }
            if (_tooltipName != null) _tooltipName.text = relic.displayName;
            if (_tooltipDesc != null) _tooltipDesc.text = relic.description;

            // 위치: _iconContainer와 _tooltipRect 모두 캔버스 좌상단(0,1) 앵커
            // → anchoredPosition이 동일 기준이므로 직접 오프셋 계산 가능
            float iconX = PANEL_PADDING + iconIndex * (ICON_SIZE + ICON_SPACING);
            float iconY = -PANEL_PADDING; // 아이콘 컨테이너 상단 Y

            _tooltipRect.anchoredPosition = new Vector2(iconX, iconY - ICON_SIZE - 4f);
            _tooltip.SetActive(true);
        }
    }
}
