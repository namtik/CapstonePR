using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Battle.UI
{
    // 상점 유물 아이콘 호버 시 이름/설명을 보여주는 공용 툴팁(런타임 생성, 모든 슬롯이 공유)
    public class ShopRelicTooltip : MonoBehaviour
    {
        const float WIDTH = 340f;     // 툴팁 가로 폭
        const float PAD = 14f;        // 내부 여백
        const float NAME_SIZE = 20f;  // 이름 글자 크기
        const float DESC_SIZE = 16f;  // 설명 글자 크기

        static ShopRelicTooltip _instance;
        Canvas _canvas;
        RectTransform _canvasRect;
        RectTransform _rect;
        TextMeshProUGUI _nameText;
        TextMeshProUGUI _descText;
        bool _shown;

        // 최초 호버 시 상위 캔버스 아래 1회 생성
        public static ShopRelicTooltip Instance
        {
            get { if (_instance == null) Create(); return _instance; }
        }

        static void Create()
        {
            Canvas canvas = ResolveTopCanvas();
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

        // sortingOrder가 가장 높은 루트 캔버스를 선택
        static Canvas ResolveTopCanvas()
        {
            Canvas best = null;
            int top = int.MinValue;
            var all = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var c in all)
            {
                if (c == null || !c.isRootCanvas) continue;
                if (best == null || c.sortingOrder >= top) { top = c.sortingOrder; best = c; }
            }
            return best;
        }

        void BuildUI()
        {
            _rect = (RectTransform)transform;
            _rect.anchorMin = _rect.anchorMax = new Vector2(0.5f, 0.5f);
            _rect.pivot = new Vector2(0f, 1f); // 커서 기준 오른쪽-아래로 펼쳐짐
            _rect.sizeDelta = new Vector2(WIDTH, 80f);

            var bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.07f, 0.07f, 0.11f, 0.96f);
            bg.raycastTarget = false; // 툴팁이 마우스를 가로채지 않도록

            _nameText = MakeText("TT_Name", NAME_SIZE, FontStyles.Bold, Color.white);
            _descText = MakeText("TT_Desc", DESC_SIZE, FontStyles.Normal, new Color(0.85f, 0.85f, 0.85f, 1f));
        }

        TextMeshProUGUI MakeText(string name, float size, FontStyles style, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.raycastTarget = false;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        // 이름/설명을 채우고 크기 맞춘 뒤 커서 옆에 표시
        public void Show(string title, string description, TMP_FontAsset font)
        {
            if (_rect == null) return;

            if (font != null) { _nameText.font = font; _descText.font = font; }

            float textW = WIDTH - PAD * 2f;
            _nameText.text = string.IsNullOrWhiteSpace(title) ? "유물" : title;
            _descText.text = string.IsNullOrWhiteSpace(description) ? string.Empty : description;

            float nameH = Mathf.Ceil(NAME_SIZE * 1.35f);
            _nameText.rectTransform.sizeDelta = new Vector2(textW, nameH);
            _nameText.rectTransform.anchoredPosition = new Vector2(PAD, -PAD);

            float descH = _descText.GetPreferredValues(_descText.text, textW, 0f).y;
            _descText.rectTransform.sizeDelta = new Vector2(textW, descH);
            _descText.rectTransform.anchoredPosition = new Vector2(PAD, -PAD - nameH - 4f);

            _rect.sizeDelta = new Vector2(WIDTH, PAD + nameH + 4f + descH + PAD);

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
            if (x + size.x > halfW) x = lp.x - 18f - size.x; // 오른쪽 넘침 → 왼쪽으로
            if (y - size.y < -halfH) y = lp.y + 18f + size.y; // 아래 넘침 → 위로

            _rect.localPosition = new Vector3(x, y, 0f);
        }
    }
}
