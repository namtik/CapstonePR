using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Battle.Relic;

namespace Battle.UI
{
    // 도감 유물 셀 호버 시 옆에 뜨는 툴팁(아이콘/이름/분류/설명) — 비주얼은 런타임 생성
    [RequireComponent(typeof(RectTransform))]
    public class CodexRelicTooltip : MonoBehaviour
    {
        const float Pad = 14f;        // 내부 여백
        const float IconSize = 96f;   // 아이콘 크기
        const float Gap = 10f;        // 요소 간격

        [Header("글씨체")]
        [Tooltip("비워두면 TMP 기본 폰트 사용")]
        [SerializeField] private TMP_FontAsset font;           // 툴팁 폰트
        [SerializeField] private float nameFontSize = 38f;     // 이름 글자 크기
        [SerializeField] private float categoryFontSize = 26f; // 분류 글자 크기
        [SerializeField] private float descFontSize = 30f;     // 설명 글자 크기

        [Header("모양")]
        [SerializeField] private float width = 600f;           // 가로 폭(세로는 설명 길이에 맞춰 자동)
        [SerializeField] private Color backgroundColor = new Color(0.08f, 0.08f, 0.12f, 0.95f); // 배경색
        [SerializeField] private Color combatColor = new Color(0.95f, 0.45f, 0.35f, 1f);        // 전투형 표시색
        [SerializeField] private Color nonCombatColor = new Color(0.45f, 0.75f, 0.95f, 1f);     // 비전투형 표시색
        [Tooltip("호버 셀(InventoryCardSlot 정렬 순서)보다 위에 그려질 정렬 순서.")]
        [SerializeField] private int sortingOrder = 300;       // 정렬 순서

        [Header("미발견 표시")]
        [SerializeField] private string unknownName = "???";                  // 미발견 이름
        [SerializeField] private string unknownDescription = "아직 발견하지 못한 유물입니다."; // 미발견 설명

        RectTransform _rect;       // 툴팁 루트
        Canvas _canvas;            // 정렬용 캔버스
        Image _icon;               // 아이콘
        TextMeshProUGUI _name;     // 이름
        TextMeshProUGUI _category; // 분류
        TextMeshProUGUI _desc;     // 설명
        float _textX, _textW;      // 텍스트 시작 X/폭

        void Awake()
        {
            Build();
            gameObject.SetActive(false);
        }

        // 유물 정보를 채우고 anchor(셀) 옆에 표시 — discovered=false면 실루엣/??? 표시
        public void Show(RelicSO relic, bool discovered, RectTransform anchor)
        {
            if (relic == null) return;
            if (_rect == null) Build();

            _icon.sprite = relic.icon;
            _icon.color = relic.icon == null ? new Color(0.6f, 0.5f, 0.2f, 1f)
                        : discovered ? Color.white : Color.black;

            bool combat = relic.category == RelicCategory.Combat;
            _name.text = discovered ? relic.DisplayLabel : unknownName;
            _category.text = combat ? "전투형" : "비전투형";
            _category.color = combat ? combatColor : nonCombatColor;
            _desc.text = discovered ? relic.description : unknownDescription;

            // 설명 길이에 맞춰 세로 크기 조절
            float nameH = Mathf.Ceil(nameFontSize * 1.3f);
            float catH = Mathf.Ceil(categoryFontSize * 1.3f);
            float descH = _desc.GetPreferredValues(_desc.text ?? string.Empty, _textW, 0f).y;
            _name.rectTransform.sizeDelta = new Vector2(_textW, nameH);
            _category.rectTransform.anchoredPosition = new Vector2(_textX, -Pad - nameH);
            _category.rectTransform.sizeDelta = new Vector2(_textW, catH);
            _desc.rectTransform.anchoredPosition = new Vector2(_textX, -Pad - nameH - catH - 4f);
            _desc.rectTransform.sizeDelta = new Vector2(_textW, descH);
            float height = Mathf.Max(Pad + nameH + catH + 4f + descH + Pad, Pad * 2f + IconSize);
            _rect.sizeDelta = new Vector2(width, height);

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = sortingOrder;
            PlaceNextTo(anchor);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        // 셀 오른쪽에 붙이고, 부모 영역을 넘으면 왼쪽으로 뒤집고 세로는 영역 안으로 클램프
        void PlaceNextTo(RectTransform anchor)
        {
            var parent = _rect.parent as RectTransform;
            if (anchor == null || parent == null) return;

            var corners = new Vector3[4];
            anchor.GetWorldCorners(corners); // 0:좌하 1:좌상 2:우상 3:우하
            Vector2 topLeft = parent.InverseTransformPoint(corners[1]);
            Vector2 topRight = parent.InverseTransformPoint(corners[2]);

            Rect bounds = parent.rect;
            Vector2 size = _rect.sizeDelta;
            float x = topRight.x + Gap;
            if (x + size.x > bounds.xMax) x = topLeft.x - Gap - size.x;
            float y = Mathf.Clamp(topRight.y, bounds.yMin + size.y, bounds.yMax);

            _rect.localPosition = new Vector3(x + size.x * _rect.pivot.x, y - size.y * (1f - _rect.pivot.y), 0f);
        }

        // 배경/아이콘/텍스트를 런타임으로 생성
        void Build()
        {
            if (_rect != null) return;
            _rect = (RectTransform)transform;
            _rect.anchorMin = _rect.anchorMax = new Vector2(0.5f, 0.5f);
            _rect.pivot = new Vector2(0f, 1f);

            _canvas = GetComponent<Canvas>();
            if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();

            var bg = GetComponent<Image>();
            if (bg == null) bg = gameObject.AddComponent<Image>();
            bg.color = backgroundColor;
            bg.raycastTarget = false;

            _textX = Pad + IconSize + Gap;
            _textW = width - _textX - Pad;

            var iconRt = NewChild("Icon");
            iconRt.anchoredPosition = new Vector2(Pad, -Pad);
            iconRt.sizeDelta = new Vector2(IconSize, IconSize);
            _icon = iconRt.gameObject.AddComponent<Image>();
            _icon.preserveAspect = true;
            _icon.raycastTarget = false;

            _name = NewText("Name", nameFontSize, FontStyles.Bold, Color.white);
            _name.rectTransform.anchoredPosition = new Vector2(_textX, -Pad);
            _category = NewText("Category", categoryFontSize, FontStyles.Normal, Color.white);
            _desc = NewText("Desc", descFontSize, FontStyles.Normal, new Color(0.85f, 0.85f, 0.85f, 1f));
        }

        // 좌상단 앵커 자식 RectTransform 생성
        RectTransform NewChild(string childName)
        {
            var go = new GameObject(childName, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            return rt;
        }

        TextMeshProUGUI NewText(string childName, float size, FontStyles style, Color color)
        {
            var t = NewChild(childName).gameObject.AddComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.raycastTarget = false;
            t.overflowMode = TextOverflowModes.Overflow;
            if (font != null) t.font = font;
            return t;
        }
    }
}
