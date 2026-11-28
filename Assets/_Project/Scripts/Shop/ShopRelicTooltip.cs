using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Battle.UI
{
    // 상점 유물 툴팁 Inspector 스타일
    [Serializable]
    public class ShopRelicTooltipStyle
    {
        [Tooltip("이름·설명 공통 폰트. descFont가 비어 있으면 설명에도 사용.")]
        public TMP_FontAsset font;
        [Tooltip("비워 두면 font를 설명에도 사용.")]
        public TMP_FontAsset descFont;
        public float nameFontSize = 20f;
        public float descFontSize = 16f;
        public FontStyles nameFontStyle = FontStyles.Bold;
        public FontStyles descFontStyle = FontStyles.Normal;
        public Color nameColor = Color.white;
        public Color descColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        public float width = 340f;
    }

    // 상점 유물 아이콘 호버 시 이름/설명을 보여주는 공용 툴팁(런타임 생성, 모든 슬롯이 공유)
    public class ShopRelicTooltip : MonoBehaviour
    {
        const float PAD = 14f; // 내부 여백

        static ShopRelicTooltip _instance;
        static ShopRelicTooltipStyle _style = new ShopRelicTooltipStyle();

        Canvas _canvas;
        RectTransform _canvasRect;
        RectTransform _rect;
        TextMeshProUGUI _nameText;
        TextMeshProUGUI _descText;
        bool _shown;

        // Inspector에서 넘긴 스타일을 저장하고, 이미 생성된 툴팁에도 반영
        public static void Configure(ShopRelicTooltipStyle style)
        {
            if (style == null)
                return;

            _style = style;
            if (_instance != null)
                _instance.ApplyConfiguredStyle();
        }

        // 떠 있는 툴팁만 닫는다. 없으면 새로 만들지 않는다.
        public static void HideActive()
        {
            if (_instance != null) _instance.Hide();
        }

        // 최초 호버 시 상위 캔버스 아래 1회 생성
        public static ShopRelicTooltip Instance
        {
            get { if (_instance == null) Create(null); return _instance; }
        }

        static void Create(Transform hoverTarget)
        {
            Canvas canvas = FindUsableCanvas(hoverTarget);
            if (canvas == null) return;

            var go = new GameObject("ShopRelicTooltip", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var tip = go.AddComponent<ShopRelicTooltip>();
            tip._canvas = canvas;
            tip._canvasRect = canvas.transform as RectTransform;
            tip.BuildUI();
            go.SetActive(false);
            _instance = tip;
        }

        // 스케일이 0인 메뉴 캔버스(SoundMenu 등)는 보이지 않으므로 건너뛴다.
        static bool Usable(Canvas canvas)
        {
            if (canvas == null || !canvas.isActiveAndEnabled) return false;
            if (!canvas.gameObject.activeInHierarchy) return false;
            return canvas.transform.lossyScale.sqrMagnitude > 0.0001f;
        }

        // 호버한 유물에서 올라가며, 레이아웃에 끼지 않는 바깥쪽 캔버스를 고른다.
        static Canvas FindUsableCanvas(Transform hoverTarget)
        {
            Canvas found = null;
            Transform t = hoverTarget;
            while (t != null)
            {
                Canvas canvas = t.GetComponent<Canvas>();
                if (Usable(canvas)) found = canvas;
                t = t.parent;
            }
            if (found != null) return found;

            Canvas best = null;
            int top = int.MinValue;
            var all = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var c in all)
            {
                if (!Usable(c) || !c.isRootCanvas) continue;
                if (best == null || c.sortingOrder >= top) { top = c.sortingOrder; best = c; }
            }
            return best;
        }

        void AttachTo(Canvas canvas)
        {
            if (canvas == null || _canvas == canvas) return;
            transform.SetParent(canvas.transform, false);
            _canvas = canvas;
            _canvasRect = canvas.transform as RectTransform;
        }

        void BuildUI()
        {
            _rect = (RectTransform)transform;
            _rect.anchorMin = _rect.anchorMax = new Vector2(0.5f, 0.5f);
            _rect.pivot = new Vector2(0f, 1f); // 커서 기준 오른쪽-아래로 펼쳐짐
            _rect.sizeDelta = new Vector2(_style.width, 80f);

            var overlay = gameObject.AddComponent<Canvas>();
            overlay.overrideSorting = true;
            overlay.sortingOrder = 80;

            var bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.07f, 0.07f, 0.11f, 0.96f);
            bg.raycastTarget = false; // 툴팁이 마우스를 가로채지 않도록

            _nameText = MakeText("TT_Name");
            _descText = MakeText("TT_Desc");
            ApplyConfiguredStyle();
        }

        TextMeshProUGUI MakeText(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.raycastTarget = false;
            t.overflowMode = TextOverflowModes.Overflow;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.alignment = TextAlignmentOptions.TopLeft;
            return t;
        }

        void ApplyConfiguredStyle()
        {
            if (_nameText == null || _descText == null)
                return;

            TMP_FontAsset nameFont = _style.font;
            TMP_FontAsset descFont = _style.descFont != null ? _style.descFont : _style.font;

            if (nameFont != null) _nameText.font = nameFont;
            if (descFont != null) _descText.font = descFont;

            _nameText.fontSize = Mathf.Max(8f, _style.nameFontSize);
            _descText.fontSize = Mathf.Max(8f, _style.descFontSize);
            _nameText.fontStyle = _style.nameFontStyle;
            _descText.fontStyle = _style.descFontStyle;
            _nameText.color = _style.nameColor;
            _descText.color = _style.descColor;
        }

        // 이름/설명을 채우고 크기 맞춘 뒤 커서 옆에 표시
        public void Show(string title, string description, Transform hoverTarget = null)
        {
            AttachTo(FindUsableCanvas(hoverTarget));
            if (_rect == null) return;

            ApplyConfiguredStyle();

            float width = Mathf.Max(120f, _style.width);
            float textW = width - PAD * 2f;
            _nameText.text = string.IsNullOrWhiteSpace(title) ? "유물" : title;
            _descText.text = string.IsNullOrWhiteSpace(description) ? string.Empty : description;

            float nameH = _nameText.GetPreferredValues(_nameText.text, textW, 0f).y;
            nameH = Mathf.Max(nameH, Mathf.Ceil(_nameText.fontSize * 1.2f));
            _nameText.rectTransform.sizeDelta = new Vector2(textW, nameH);
            _nameText.rectTransform.anchoredPosition = new Vector2(PAD, -PAD);

            float descH = _descText.GetPreferredValues(_descText.text, textW, 0f).y;
            _descText.rectTransform.sizeDelta = new Vector2(textW, descH);
            _descText.rectTransform.anchoredPosition = new Vector2(PAD, -PAD - nameH - 4f);

            _rect.sizeDelta = new Vector2(width, PAD + nameH + 4f + descH + PAD);

            gameObject.SetActive(true);
            _rect.SetAsLastSibling();
            _shown = true;
            UpdatePosition();
        }

        public void Hide()
        {
            _shown = false;
            if (gameObject != null) gameObject.SetActive(false);
        }

        void Update()
        {
            if (_shown) UpdatePosition();
        }

        // 커서 위치로 따라다니며, 화면 오른쪽/아래로 넘치면 반대편으로 뒤집음
        void UpdatePosition()
        {
            if (_canvasRect == null) return;
            Camera cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, Input.mousePosition, cam, out var lp))
                return;

            Vector2 size = _rect.sizeDelta;
            Vector2 canvasSize = _canvasRect.rect.size;
            float halfW = canvasSize.x * 0.5f;
            float halfH = canvasSize.y * 0.5f;

            float x = lp.x + 18f;
            float y = lp.y - 18f;
            if (x + size.x > halfW) x = lp.x - 18f - size.x; // 오른쪽 넘침 -> 왼쪽으로
            if (y - size.y < -halfH) y = lp.y + 18f + size.y; // 아래 넘침 -> 위로

            _rect.localPosition = new Vector3(x, y, 0f);
        }
    }
}
