using UnityEngine;
using UnityEngine.UI;

namespace Battle.UI
{
    // 보상 화면 스킵/나가기 버튼을 인스펙터에서 꾸미는 스타일 묶음
    [System.Serializable]
    public class RewardSkipButtonStyle
    {
        [Header("라벨 (글자)")]
        public string label = "선택하지 않기"; // 버튼 라벨
        public Font font; // 라벨 폰트
        [Range(8, 96)] public int fontSize = 28; // 라벨 글자 크기
        public Color textColor = Color.white; // 라벨 색

        [Header("버튼 모양")]
        [Tooltip("비워두면 단색 배경으로 그려진다.")]
        public Sprite buttonImage; // 버튼 배경 스프라이트
        public Color buttonColor = new Color(0.15f, 0.15f, 0.18f, 0.95f); // 버튼 배경 색
        [Tooltip("buttonImage가 9-슬라이스(테두리 있는) 스프라이트일 때 켜면 늘려도 모서리가 보존된다.")]
        public bool sliced = true; // 9-슬라이스 사용 여부
        public Vector2 size = new Vector2(320f, 80f); // 버튼 크기

        [Header("위치 (부모 중앙 기준 anchored position)")]
        public Vector2 anchoredPosition = new Vector2(0f, -340f); // 버튼 위치

        static Font _builtinFont; // 내장 폰트 캐시

        // 이 스타일대로 클릭 버튼을 런타임 생성해 parent 아래에 부착
        public Button Build(Transform parent, System.Action onClick)
        {
            var go = new GameObject("SkipButton", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPosition;
            rt.localScale = Vector3.one;

            var img = go.AddComponent<Image>();
            img.sprite = buttonImage;
            img.color = buttonColor;
            if (buttonImage != null)
                img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            img.raycastTarget = true;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            if (onClick != null)
                btn.onClick.AddListener(() => onClick());

            var labelGo = new GameObject("Label", typeof(RectTransform));
            var lrt = (RectTransform)labelGo.transform;
            lrt.SetParent(rt, false);
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            lrt.localScale = Vector3.one;

            var text = labelGo.AddComponent<Text>();
            text.font = font != null ? font : BuiltinFont();
            text.fontSize = fontSize;
            text.color = textColor;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = label;
            text.raycastTarget = false;

            return btn;
        }

        // 내장 폰트를 로드(캐시)해 반환
        static Font BuiltinFont()
        {
            if (_builtinFont == null)
                _builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _builtinFont;
        }
    }
}
