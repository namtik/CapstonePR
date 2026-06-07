using UnityEngine;
using UnityEngine.UI;

namespace Battle.UI
{
    /// <summary>
    /// 보상 화면의 "선택하지 않기 / 나가기" 버튼을 인스펙터에서 꾸미기 위한 스타일 묶음.
    /// 런타임에 버튼을 생성할 때 이 값을 그대로 적용한다.
    /// 버튼 이미지/색/크기/위치/폰트/폰트크기/글자색/라벨을 모두 노출한다.
    /// </summary>
    [System.Serializable]
    public class RewardSkipButtonStyle
    {
        [Header("라벨 (글자)")]
        public string label = "선택하지 않기";
        public Font font;
        [Range(8, 96)] public int fontSize = 28;
        public Color textColor = Color.white;

        [Header("버튼 모양")]
        [Tooltip("비워두면 단색 배경으로 그려진다.")]
        public Sprite buttonImage;
        public Color buttonColor = new Color(0.15f, 0.15f, 0.18f, 0.95f);
        [Tooltip("buttonImage가 9-슬라이스(테두리 있는) 스프라이트일 때 켜면 늘려도 모서리가 보존된다.")]
        public bool sliced = true;
        public Vector2 size = new Vector2(320f, 80f);

        [Header("위치 (부모 중앙 기준 anchored position)")]
        public Vector2 anchoredPosition = new Vector2(0f, -340f);

        static Font _builtinFont;

        /// <summary>이 스타일대로 클릭 버튼을 런타임 생성해 parent 아래에 붙이고 반환한다.</summary>
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

        static Font BuiltinFont()
        {
            if (_builtinFont == null)
                _builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _builtinFont;
        }
    }
}
