using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Battle.Card;

namespace Battle.UI
{
    // 카드 호버 시 카드 옆에 키워드(소멸/화상/빙결/파편) 설명 패널을 세로로 쌓아 보여주는 공용 툴팁(런타임 생성)
    public class CardKeywordTooltip : MonoBehaviour
    {
        // 키워드 정의 — 이름, 제목 색, 설명
        struct KeywordDef
        {
            public string name;
            public Color color;
            public string desc;
            public KeywordDef(string n, Color c, string d) { name = n; color = c; desc = d; }
        }

        static readonly KeywordDef Exhaust = new KeywordDef("소멸", new Color(0.80f, 0.70f, 1.00f),
            "사용하면 이번 전투 동안 다시 뽑을 수 없습니다.");
        static readonly KeywordDef Burn = new KeywordDef("화상", new Color(1.00f, 0.55f, 0.25f),
            "적의 행동게이지가 가득 차면 화상 수치만큼 피해를 입고, 화상이 절반으로 줄어듭니다.");
        static readonly KeywordDef Chain = new KeywordDef("연쇄", new Color(1.00f, 0.85f, 0.35f),
            "연쇄 스택 1개를 소모하여 공격 수 만큼 적에게 1 추가 피해를 입힙니다.");
        static readonly KeywordDef Frost = new KeywordDef("빙결", new Color(0.55f, 0.85f, 1.00f),
            "플레이어가 행동할 때 적의 행동 게이지가 오르는 대신 빙결이 1 줄어듭니다.");
        static readonly KeywordDef Fragment = new KeywordDef("파편", new Color(0.85f, 0.75f, 0.50f),
            "전투 중에 생성되는 임시 부적입니다. 사용하면 소멸합니다.");

        static readonly Color CurseColor = new Color(0.85f, 0.45f, 0.95f); // 저주 제목 색

        const float PanelWidth = 300f;   // 패널 폭(캔버스 단위)
        const float Pad = 12f;           // 패널 내부 여백
        const float Gap = 8f;            // 패널 사이 간격
        const float CardMargin = 14f;    // 카드와 패널 사이 간격
        const float TitleSize = 24f;     // 제목 글자 크기
        const float DescSize = 19f;      // 설명 글자 크기

        static CardKeywordTooltip _instance;

        Canvas _canvas;
        RectTransform _canvasRect;
        RectTransform _rect;
        RectTransform _target;           // 따라갈 카드
        float _contentHeight;            // 쌓인 패널 전체 높이
        readonly List<Panel> _panels = new List<Panel>();
        readonly List<KeywordDef> _buffer = new List<KeywordDef>(4);

        class Panel
        {
            public RectTransform rect;
            public TextMeshProUGUI title;
            public TextMeshProUGUI desc;
        }

        // 카드가 가진 키워드를 찾아 패널을 띄운다. 키워드가 없으면 숨김.
        public static void Show(NewCardView card, CardData data, string description, TMP_FontAsset font)
        {
            if (card == null || data == null) { Hide(); return; }
            Canvas canvas = card.GetComponentInParent<Canvas>();
            if (canvas == null) { Hide(); return; }
            canvas = canvas.rootCanvas;

            if (_instance == null || _instance._canvas != canvas) Create(canvas);
            _instance.ShowFor((RectTransform)card.transform, card.Card, data, description ?? string.Empty, font);
        }

        // 카드가 아닌 UI(휴식 선택지 등)에 같은 모양의 설명 패널 1개를 띄운다.
        public static void ShowText(RectTransform target, string title, Color titleColor, string desc, TMP_FontAsset font = null)
        {
            if (target == null) { Hide(); return; }
            Canvas canvas = target.GetComponentInParent<Canvas>();
            if (canvas == null) { Hide(); return; }
            canvas = canvas.rootCanvas;

            if (_instance == null || _instance._canvas != canvas) Create(canvas);
            _instance._buffer.Clear();
            _instance._buffer.Add(new KeywordDef(title ?? string.Empty, titleColor, desc ?? string.Empty));
            _instance.ShowBuffer(target, font);
        }

        // 지정한 대상에 붙어 있는 툴팁일 때만 숨긴다(다른 UI가 띄운 툴팁은 건드리지 않음).
        public static void HideFor(RectTransform target)
        {
            if (_instance != null && _instance._target == target) Hide();
        }

        public static void Hide()
        {
            if (_instance == null) return;
            _instance._target = null;
            _instance.gameObject.SetActive(false);
        }

        static void Create(Canvas canvas)
        {
            if (_instance != null) Destroy(_instance.gameObject);
            var go = new GameObject("CardKeywordTooltip", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var tip = go.AddComponent<CardKeywordTooltip>();
            tip._canvas = canvas;
            tip._canvasRect = (RectTransform)canvas.transform;
            tip._rect = (RectTransform)go.transform;
            tip._rect.anchorMin = tip._rect.anchorMax = new Vector2(0.5f, 0.5f);
            tip._rect.pivot = new Vector2(0f, 1f);
            // 툴팁이 마우스를 가로채 카드 호버가 풀리지 않도록 레이캐스트 차단 해제
            var cg = go.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;
            go.SetActive(false);
            _instance = tip;
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        void ShowFor(RectTransform target, CardInstance card, CardData data, string description, TMP_FontAsset font)
        {
            CollectKeywords(data, description, _buffer);
            AddCurse(card, _buffer);
            ShowBuffer(target, font);
        }

        // _buffer에 담긴 패널들을 target 옆에 띄운다
        void ShowBuffer(RectTransform target, TMP_FontAsset font)
        {
            if (_buffer.Count == 0) { Hide(); return; }

            while (_panels.Count < _buffer.Count) _panels.Add(BuildPanel());

            for (int i = 0; i < _panels.Count; i++)
            {
                Panel p = _panels[i];
                bool on = i < _buffer.Count;
                p.rect.gameObject.SetActive(on);
                if (!on) continue;

                KeywordDef k = _buffer[i];
                if (font != null) { p.title.font = font; p.desc.font = font; }
                p.title.text = k.name;
                p.title.color = k.color;
                p.desc.text = k.desc;
            }

            _target = target;
            gameObject.SetActive(true);
            _rect.SetAsLastSibling();
            Relayout();
            // 처음 생성된 TMP는 활성화 직후 프레임에 폰트/메시가 확정되므로 몇 프레임 더 크기를 다시 잰다
            _relayoutFrames = 3;
            UpdatePosition();
        }

        int _relayoutFrames; // 표시 직후 크기 재측정 남은 프레임

        // 설명 텍스트 실제 높이에 맞춰 패널 크기/위치를 다시 계산
        void Relayout()
        {
            float y = 0f;
            float textW = PanelWidth - Pad * 2f;
            int count = Mathf.Min(_buffer.Count, _panels.Count);
            for (int i = 0; i < count; i++)
            {
                Panel p = _panels[i];
                float titleH = Mathf.Ceil(TitleSize * 1.25f);
                p.title.rectTransform.sizeDelta = new Vector2(textW, titleH);
                p.title.rectTransform.anchoredPosition = new Vector2(Pad, -Pad);

                // 폭을 먼저 확정하고 메시를 강제 갱신한 뒤 실제 줄바꿈 기준 높이를 잰다
                p.desc.rectTransform.sizeDelta = new Vector2(textW, p.desc.rectTransform.sizeDelta.y);
                p.desc.ForceMeshUpdate(true);
                float descH = Mathf.Max(p.desc.preferredHeight, p.desc.GetPreferredValues(p.desc.text, textW, 0f).y);
                p.desc.rectTransform.sizeDelta = new Vector2(textW, descH);
                p.desc.rectTransform.anchoredPosition = new Vector2(Pad, -Pad - titleH - 2f);

                float h = Pad + titleH + 2f + descH + Pad;
                p.rect.sizeDelta = new Vector2(PanelWidth, h);
                p.rect.anchoredPosition = new Vector2(0f, -y);
                y += h + Gap;
            }
            _contentHeight = Mathf.Max(0f, y - Gap);
            _rect.sizeDelta = new Vector2(PanelWidth, _contentHeight);
        }

        // 설명 문구/태그에서 키워드를 찾는다(표시 순서: 소멸 → 화상 → 연쇄 → 빙결 → 파편)
        static void CollectKeywords(CardData data, string description, List<KeywordDef> outList)
        {
            outList.Clear();
            if (description.Contains("소멸") || data.HasTag("EXHAUST")) outList.Add(Exhaust);
            if (description.Contains("화상")) outList.Add(Burn);
            if (description.Contains("연쇄")) outList.Add(Chain);
            if (description.Contains("빙결")) outList.Add(Frost);
            if (description.Contains("파편") || data.HasTag("FRAGMENT")) outList.Add(Fragment);
        }

        // 이 카드 속성에 저주가 걸려 있으면 저주 패널을 맨 위에 넣는다
        static void AddCurse(CardInstance card, List<KeywordDef> outList)
        {
            var nb = NewBattleController.Instance;
            if (card == null || card.data == null || nb == null) return;
            int remaining = nb.GetCurseRemainingUses(card.Element);
            if (remaining <= 0) return;

            string desc = $"{ElementName(card.Element)} 속성 부적을 사용할 때마다 피해를 {NewBattleController.CURSE_PLAYER_DAMAGE} 입습니다.\n"
                        + $"부적을 {remaining}장 더 사용하면 풀립니다.";
            outList.Insert(0, new KeywordDef("저주", CurseColor, desc));
        }

        static string ElementName(CardElement e) => e switch
        {
            CardElement.Fire => "불", CardElement.Water => "물",
            CardElement.Wind => "바람", CardElement.Earth => "땅", _ => "무"
        };

        Panel BuildPanel()
        {
            var go = new GameObject("KeywordPanel", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.10f, 0.14f, 0.17f, 0.95f);
            bg.raycastTarget = false;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.45f, 0.55f, 0.60f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);

            return new Panel
            {
                rect = rt,
                title = MakeText(rt, "Title", TitleSize, FontStyles.Bold),
                desc = MakeText(rt, "Desc", DescSize, FontStyles.Normal),
            };
        }

        static TextMeshProUGUI MakeText(RectTransform parent, string name, float size, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.raycastTarget = false;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = new Color(0.92f, 0.92f, 0.92f, 1f);
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            t.alignment = TextAlignmentOptions.TopLeft;
            return t;
        }

        void LateUpdate()
        {
            if (_target == null || !_target.gameObject.activeInHierarchy) { Hide(); return; }
            if (_relayoutFrames > 0) { _relayoutFrames--; Relayout(); }
            UpdatePosition();
        }

        // 카드 오른쪽 위에 붙이고, 화면 밖으로 나가면 왼쪽/위로 밀어 넣는다
        void UpdatePosition()
        {
            if (_target == null || _canvasRect == null) return;

            var corners = new Vector3[4];
            _target.GetWorldCorners(corners); // 0=좌하 1=좌상 2=우상 3=우하
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 4; i++)
            {
                Vector3 local = _canvasRect.InverseTransformPoint(corners[i]);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }

            Rect canvas = _canvasRect.rect;
            float x = max.x + CardMargin;
            if (x + PanelWidth > canvas.xMax) x = min.x - CardMargin - PanelWidth; // 오른쪽 넘침 → 카드 왼쪽
            x = Mathf.Clamp(x, canvas.xMin, canvas.xMax - PanelWidth);

            float y = max.y;
            if (y - _contentHeight < canvas.yMin) y = canvas.yMin + _contentHeight; // 아래 넘침 → 위로
            if (y > canvas.yMax) y = canvas.yMax;

            _rect.localPosition = new Vector3(x, y, 0f);
        }
    }
}
