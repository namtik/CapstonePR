using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 적 HP바 왼쪽 끝 위의 특이사항 아이콘. 마우스를 올리면 Note 말풍선을 띄운다.
public class EnemyNoteBadge : MonoBehaviour
{
    [Header("아이콘")]
    [SerializeField] private Sprite skullSprite;
    [SerializeField] private float iconSize = 45f;
    [Tooltip("HP바 왼쪽 위 기준. X는 오른쪽, Y는 위.")]
    [SerializeField] private Vector2 iconOffset;

    [Header("말풍선")]
    [SerializeField] private Sprite bubbleSprite;
    [SerializeField] private Vector2 bubbleSize = new Vector2(324f, 121f);
    [Tooltip("꼬리 끝 기준 추가 이동. X는 오른쪽, Y는 위.")]
    [SerializeField] private Vector2 bubbleOffset = new Vector2(0f, 2f);
    [Tooltip("글자와 말풍선 아래 테두리 사이 여백.")]
    [SerializeField] private float tailClearance = 16f;
    [SerializeField] private float textSidePadding = 22f;
    [Tooltip("위쪽 꼬리를 피해 비우는 여백.")]
    [SerializeField] private float textTopPadding = 36f;

    [Header("글자")]
    [SerializeField] private TMP_FontAsset font;
    [Tooltip("비우면 폰트 기본 머테리얼을 쓴다.")]
    [SerializeField] private Material fontMaterial;
    [SerializeField] private float fontSize = 22f;
    [Tooltip("문구가 길면 이 크기까지 글자를 줄여 말풍선 안에 맞춘다.")]
    [SerializeField] private float minFontSize = 14f;
    [SerializeField] private Color color = Color.white;
    [SerializeField] private FontStyles fontStyle = FontStyles.Normal;
    [SerializeField] private TextAlignmentOptions alignment = TextAlignmentOptions.Center;
    [Tooltip("0이면 외곽선 없음.")]
    [Range(0f, 1f)]
    [SerializeField] private float outlineWidth;
    [SerializeField] private Color outlineColor = Color.black;

    RectTransform _root;
    GameObject _bubble;
    TextMeshProUGUI _label;
    int _hoverCount;

    public void Bind(RectTransform bar, string note)
    {
        bool hasNote = !string.IsNullOrWhiteSpace(note);
        if (bar == null || !hasNote)
        {
            if (_root == null && bar != null)
                _root = bar.Find("EnemyNoteIcon") as RectTransform;
            if (_root != null) _root.gameObject.SetActive(false);
            if (_bubble != null) _bubble.SetActive(false);
            return;
        }

        Ensure(bar);
        _label.text = note.Trim();
        ApplyStyle();
        _hoverCount = 0;
        if (_bubble != null) _bubble.SetActive(false);
        _root.gameObject.SetActive(true);
    }

    public void HideBubble()
    {
        _hoverCount = 0;
        if (_bubble != null) _bubble.SetActive(false);
    }

    void LateUpdate()
    {
        if (_root == null || !_root.gameObject.activeInHierarchy) return;
        Place();
        if (_bubble != null && _bubble.activeSelf) ApplyStyle();
    }

    void Ensure(RectTransform bar)
    {
        ResolveSprites();
        Transform found = bar.Find("EnemyNoteIcon");
        if (found != null)
        {
            _root = found as RectTransform;
            _bubble = found.Find("Bubble")?.gameObject;
            _label = found.GetComponentInChildren<TextMeshProUGUI>(true);
            WireHover(_root.gameObject);
            if (_bubble != null) WireHover(_bubble);
            return;
        }

        var rootGo = new GameObject("EnemyNoteIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        rootGo.transform.SetParent(bar, false);
        _root = rootGo.GetComponent<RectTransform>();
        var icon = rootGo.GetComponent<Image>();
        icon.sprite = skullSprite;
        icon.preserveAspect = true;
        icon.raycastTarget = true;
        WireHover(rootGo);

        var bubbleGo = new GameObject("Bubble", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bubbleGo.transform.SetParent(_root, false);
        _bubble = bubbleGo;
        var bubbleImage = bubbleGo.GetComponent<Image>();
        bubbleImage.sprite = bubbleSprite;
        bubbleImage.type = Image.Type.Simple;
        bubbleImage.preserveAspect = true;
        bubbleImage.raycastTarget = true;
        WireHover(bubbleGo);

        var textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(bubbleGo.transform, false);
        _label = textGo.AddComponent<TextMeshProUGUI>();
        _label.raycastTarget = false;
        _label.enableWordWrapping = true;
        _label.overflowMode = TextOverflowModes.Overflow;

        bubbleGo.SetActive(false);
    }

    void ResolveSprites()
    {
        if (skullSprite != null && bubbleSprite != null && font != null && fontMaterial != null) return;
        var all = FindObjectsByType<EnemyNoteBadge>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null || all[i] == this) continue;
            if (skullSprite == null) skullSprite = all[i].skullSprite;
            if (bubbleSprite == null) bubbleSprite = all[i].bubbleSprite;
            if (font == null) font = all[i].font;
            if (fontMaterial == null) fontMaterial = all[i].fontMaterial;
        }
    }

    void Place()
    {
        if (_root == null) return;
        _root.anchorMin = _root.anchorMax = new Vector2(0f, 1f);
        _root.pivot = new Vector2(0f, 0f);
        _root.anchoredPosition = iconOffset;
        _root.sizeDelta = new Vector2(iconSize, iconSize);
        _root.localScale = Vector3.one;
        _root.SetAsLastSibling();

        if (_bubble == null) return;
        var bubbleRt = _bubble.transform as RectTransform;
        // 꼬리 끝(그림 왼쪽 위)이 아이콘 아래에 닿고, 몸통은 오른쪽으로 펼쳐진다.
        bubbleRt.anchorMin = bubbleRt.anchorMax = new Vector2(0.2f, 0f);
        bubbleRt.pivot = new Vector2(2f / 270f, 1f);
        bubbleRt.anchoredPosition = bubbleOffset;
        bubbleRt.sizeDelta = bubbleSize;
        bubbleRt.localScale = Vector3.one;

        if (_label == null) return;
        var textRt = _label.rectTransform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(textSidePadding, tailClearance);
        textRt.offsetMax = new Vector2(-textSidePadding, -textTopPadding);
        textRt.localScale = Vector3.one;
    }

    void ApplyStyle()
    {
        if (_label == null) return;
        if (font != null) _label.font = font;
        // outlineWidth 설정 시 TMP가 머테리얼 인스턴스를 만들므로, 원본 지정은 바뀔 때만 한다(매 프레임 인스턴스 생성 방지).
        if (fontMaterial != null && _appliedMaterial != fontMaterial)
        {
            _label.fontSharedMaterial = fontMaterial;
            _appliedMaterial = fontMaterial;
        }
        _label.enableWordWrapping = true;
        _label.enableAutoSizing = false;
        // 최소 크기로도 안 들어가면 말풍선 밖으로 넘기지 않고 말줄임 처리.
        _label.overflowMode = TextOverflowModes.Ellipsis;
        _label.color = color;
        _label.fontStyle = fontStyle;
        _label.alignment = alignment;
        _label.outlineWidth = Mathf.Clamp01(outlineWidth);
        _ = _label.outlineColor;
        _label.outlineColor = outlineColor;
        FitText();

        var icon = _root != null ? _root.GetComponent<Image>() : null;
        if (icon != null && skullSprite != null) icon.sprite = skullSprite;
        var bubbleImage = _bubble != null ? _bubble.GetComponent<Image>() : null;
        if (bubbleImage != null && bubbleSprite != null)
        {
            bubbleImage.sprite = bubbleSprite;
            bubbleImage.type = Image.Type.Simple;
            bubbleImage.preserveAspect = true;
        }
    }

    Material _appliedMaterial;
    string _fitText;
    Vector2 _fitRect;
    float _fitMax, _fitMin;
    Object _fitFont, _fitMat;

    // TMP 자동 크기는 레이아웃 줄높이만 보고, 머테리얼의 Dilate/외곽선으로 두꺼워진 실제 글자 크기는 보지 않는다.
    // 그래서 실제로 그려지는 글자 범위(textBounds)가 글자 영역 안에 들어올 때까지 직접 줄인다.
    void FitText()
    {
        Rect r = _label.rectTransform.rect;
        if (r.width <= 1f || r.height <= 1f) return;

        float maxSize = Mathf.Max(1f, fontSize);
        float minSize = Mathf.Clamp(minFontSize, 1f, maxSize);
        if (_fitText == _label.text && _fitRect == r.size && _fitMax == maxSize && _fitMin == minSize
            && _fitFont == _label.font && _fitMat == _label.fontSharedMaterial)
            return;

        _fitText = _label.text;
        _fitRect = r.size;
        _fitMax = maxSize;
        _fitMin = minSize;
        _fitFont = _label.font;
        _fitMat = _label.fontSharedMaterial;

        const float tolerance = 0.5f;
        for (float size = maxSize; ; size -= 1f)
        {
            if (size <= minSize) size = minSize;
            _label.fontSize = size;
            _label.ForceMeshUpdate();
            Bounds b = _label.textBounds;
            bool fits = !_label.isTextTruncated
                && b.min.x >= r.xMin - tolerance && b.max.x <= r.xMax + tolerance
                && b.min.y >= r.yMin - tolerance && b.max.y <= r.yMax + tolerance;
            if (fits || size <= minSize) break;
        }
    }

    void WireHover(GameObject go)
    {
        var relay = go.GetComponent<HoverRelay>();
        if (relay == null) relay = go.AddComponent<HoverRelay>();
        relay.owner = this;
    }

    internal void NotifyEnter()
    {
        _hoverCount++;
        if (_bubble != null) _bubble.SetActive(true);
    }

    internal void NotifyExit()
    {
        _hoverCount = Mathf.Max(0, _hoverCount - 1);
        if (_hoverCount == 0 && _bubble != null) _bubble.SetActive(false);
    }

    sealed class HoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public EnemyNoteBadge owner;
        public void OnPointerEnter(PointerEventData eventData) { if (owner != null) owner.NotifyEnter(); }
        public void OnPointerExit(PointerEventData eventData) { if (owner != null) owner.NotifyExit(); }
    }
}
