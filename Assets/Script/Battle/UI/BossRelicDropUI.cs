using System.Collections;
using System.Collections.Generic;
using Battle.Relic;
using Battle.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 1스테이지 보스 처치 후, 콤보 보상이 끝난 뒤에 유물을 띄운다.
// 커서를 올리면 유물 스테이지와 같은 설명 팝업이 나오고, 클릭하면 인벤토리로 들어간다.
public class BossRelicDropUI : MonoBehaviour
{
    const float IconSize = 300f;
    const float RiseHeight = 250f;
    const float RiseTime = 0.35f;
    const float FlyTime = 0.58f;

    static BossRelicDropUI _instance;

    RectTransform _root;
    RectTransform _icon;
    Image _iconImage;
    RectTransform _tooltip;
    TMP_Text _nameText;
    TMP_Text _descText;

    RelicSO _relic;
    System.Action _onComplete;
    Vector2 _rest;
    float _hoverStart;
    float _hoverSpeed = 2.5f;
    float _hoverAmp = 10f;
    float _hoverScale = 1.08f;
    float _hoverLerp = 10f;
    bool _floating;
    bool _pointerOver;
    bool _absorbing;
    bool _granted;

    public static void Present(System.Action onComplete)
    {
        RelicSO relic = PickRelic();
        if (relic == null)
        {
            Debug.Log("[BossRelicDrop] 지급할 유물이 없어 유물 드랍을 건너뜁니다.");
            onComplete?.Invoke();
            return;
        }

        Instance.Begin(relic, onComplete);
    }

    static BossRelicDropUI Instance
    {
        get
        {
            if (_instance != null) return _instance;
            var go = new GameObject("BossRelicDropUI", typeof(RectTransform));
            _instance = go.AddComponent<BossRelicDropUI>();
            DontDestroyOnLoad(go);
            return _instance;
        }
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        BuildCanvas();
    }

    void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    void BuildCanvas()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 700;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();
        _root = transform as RectTransform;

        var iconGo = new GameObject("Relic", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RelicRewardInteractable));
        _icon = iconGo.GetComponent<RectTransform>();
        _icon.SetParent(_root, false);
        _icon.anchorMin = _icon.anchorMax = new Vector2(0.5f, 0.5f);
        _icon.pivot = new Vector2(0.5f, 0.5f);
        _icon.sizeDelta = new Vector2(IconSize, IconSize);
        _icon.localScale = Vector3.zero;

        _iconImage = iconGo.GetComponent<Image>();
        _iconImage.preserveAspect = true;
        _iconImage.raycastTarget = true;
        _iconImage.color = Color.white;

        var interact = iconGo.GetComponent<RelicRewardInteractable>();
        interact.onPointerEnter = OnPointerEnter;
        interact.onPointerExit = OnPointerExit;
        interact.onPointerClick = OnPointerClick;

        BuildTooltip();
    }

    void BuildTooltip()
    {
        var popupGo = new GameObject("RelicTooltipPopup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        _tooltip = popupGo.GetComponent<RectTransform>();
        _tooltip.SetParent(_root, false);
        _tooltip.anchorMin = new Vector2(0.5f, 1f);
        _tooltip.anchorMax = new Vector2(0.5f, 1f);
        _tooltip.pivot = new Vector2(0.5f, 1f);
        _tooltip.anchoredPosition = new Vector2(0f, -60f);
        _tooltip.sizeDelta = new Vector2(820f, 220f);

        var background = popupGo.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.78f);
        background.raycastTarget = false;

        var nameGo = new GameObject("Name", typeof(RectTransform));
        nameGo.transform.SetParent(_tooltip, false);
        _nameText = nameGo.AddComponent<TextMeshProUGUI>();
        _nameText.raycastTarget = false;

        var descGo = new GameObject("Description", typeof(RectTransform));
        descGo.transform.SetParent(_tooltip, false);
        _descText = descGo.AddComponent<TextMeshProUGUI>();
        _descText.raycastTarget = false;

        var nameRect = _nameText.rectTransform;
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.pivot = new Vector2(0.5f, 1f);
        nameRect.offsetMin = new Vector2(24f, -72f);
        nameRect.offsetMax = new Vector2(-24f, -18f);

        var descRect = _descText.rectTransform;
        descRect.anchorMin = Vector2.zero;
        descRect.anchorMax = Vector2.one;
        descRect.pivot = new Vector2(0.5f, 1f);
        descRect.offsetMin = new Vector2(24f, 18f);
        descRect.offsetMax = new Vector2(-24f, -78f);

        RelicStageController stage = FindFirstObjectByType<RelicStageController>(FindObjectsInactive.Include);
        if (stage != null)
        {
            _tooltip.anchoredPosition = stage.StageTooltipAnchoredPosition;
            _tooltip.sizeDelta = stage.StageTooltipSize;
            Vector2 pad = stage.StageTooltipPadding;
            nameRect.offsetMin = new Vector2(pad.x, -72f);
            nameRect.offsetMax = new Vector2(-pad.x, -pad.y);
            descRect.offsetMin = new Vector2(pad.x, pad.y);
            descRect.offsetMax = new Vector2(-pad.x, -78f);
            stage.ApplyStageTooltipStyle(background, _nameText, _descText);
            if (stage.StageItemHoverSpeed > 0f) _hoverSpeed = stage.StageItemHoverSpeed;
            if (stage.StageItemHoverAmplitude > 0f) _hoverAmp = stage.StageItemHoverAmplitude;
            if (stage.StageItemHoverScale > 1f) _hoverScale = stage.StageItemHoverScale;
            if (stage.StageItemHoverScaleLerp > 0f) _hoverLerp = stage.StageItemHoverScaleLerp;
        }

        _tooltip.gameObject.SetActive(false);
    }

    void Begin(RelicSO relic, System.Action onComplete)
    {
        if (_root == null) BuildCanvas();

        _relic = relic;
        _onComplete = onComplete;
        _granted = false;
        _absorbing = false;
        _floating = false;
        _pointerOver = false;
        _rest = new Vector2(0f, 160f);

        _iconImage.sprite = relic.icon;
        _iconImage.enabled = relic.icon != null;
        _iconImage.color = Color.white;
        _nameText.text = string.IsNullOrWhiteSpace(relic.displayName) ? "유물" : relic.displayName;
        _descText.text = string.IsNullOrWhiteSpace(relic.description) ? "개발중인 유물" : relic.description;
        _tooltip.gameObject.SetActive(false);
        _icon.gameObject.SetActive(true);

        StopAllCoroutines();
        StartCoroutine(Rise());
    }

    void Update()
    {
        if (!_floating || _absorbing || _icon == null)
            return;

        float y = _rest.y + Mathf.Sin((Time.unscaledTime - _hoverStart) * _hoverSpeed) * _hoverAmp;
        _icon.anchoredPosition = new Vector2(_rest.x, y);

        float mul = _pointerOver ? _hoverScale : 1f;
        _icon.localScale = Vector3.Lerp(_icon.localScale, Vector3.one * mul, Time.unscaledDeltaTime * _hoverLerp);
    }

    IEnumerator Rise()
    {
        Vector2 start = _rest + new Vector2(0f, -RiseHeight);
        _icon.anchoredPosition = start;
        _icon.localScale = Vector3.zero;

        float t = 0f;
        while (t < RiseTime)
        {
            t += Time.unscaledDeltaTime;
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / RiseTime));
            _icon.anchoredPosition = Vector2.Lerp(start, _rest, e);
            _icon.localScale = Vector3.one * e;
            yield return null;
        }

        _icon.anchoredPosition = _rest;
        _icon.localScale = Vector3.one;
        _hoverStart = Time.unscaledTime;
        _floating = true;
    }

    void OnPointerEnter()
    {
        if (!_floating || _absorbing)
            return;

        _pointerOver = true;
        _tooltip.gameObject.SetActive(true);
        _tooltip.SetAsLastSibling();
    }

    void OnPointerExit()
    {
        _pointerOver = false;
        if (_tooltip != null)
            _tooltip.gameObject.SetActive(false);
    }

    void OnPointerClick()
    {
        if (!_floating || _absorbing || _relic == null)
            return;

        _absorbing = true;
        _floating = false;
        _pointerOver = false;
        if (_tooltip != null)
            _tooltip.gameObject.SetActive(false);

        if (!_granted && RelicManager.Instance != null)
        {
            RelicManager.Instance.AddRelic(_relic);
            _granted = true;
        }

        StartCoroutine(FlyThenFinish());
    }

    IEnumerator FlyThenFinish()
    {
        InventoryOpenButton.TryGetTarget(null, out Vector2 target, out InventoryOpenButton button);
        if (_iconImage != null && _iconImage.sprite != null)
            yield return FlyIcon(target);

        if (button != null)
            button.PlayReceivePulse();

        if (_icon != null)
            _icon.gameObject.SetActive(false);

        yield return new WaitForSecondsRealtime(0.12f);

        System.Action done = _onComplete;
        _onComplete = null;
        _relic = null;
        done?.Invoke();
    }

    IEnumerator FlyIcon(Vector2 targetScreen)
    {
        Vector2 from = ToLocal(ScreenCenter(_icon));
        Vector2 to = ToLocal(targetScreen);
        Vector2 dir = to - from;
        Vector2 side = dir.sqrMagnitude > 1f ? new Vector2(-dir.y, dir.x).normalized : Vector2.up;
        if (side.y < 0f) side = -side;
        Vector2 control = Vector2.Lerp(from, to, 0.42f) + side * Mathf.Clamp(dir.magnitude * 0.16f, 50f, 180f);

        float t = 0f;
        while (t < FlyTime)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / FlyTime);
            float e = u * u * (3f - 2f * u);
            _icon.anchoredPosition = Quad(from, control, to, e);
            _icon.localScale = Vector3.one * Mathf.Lerp(1f, 0.22f, e);
            if (u > 0.88f)
            {
                Color c = _iconImage.color;
                c.a = Mathf.Lerp(1f, 0.15f, (u - 0.88f) / 0.12f);
                _iconImage.color = c;
            }
            yield return null;
        }
    }

    Vector2 ToLocal(Vector2 screen)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out Vector2 local);
        return local;
    }

    static Vector2 ScreenCenter(RectTransform rt)
    {
        return RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
    }

    static Vector2 Quad(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    static RelicSO PickRelic()
    {
        RelicManager manager = RelicManager.Instance;
        if (manager == null)
            return null;

        IReadOnlyList<RelicSO> defs = manager.RelicDefinitions;
        var pool = new List<RelicSO>();
        for (int i = 0; i < defs.Count; i++)
        {
            RelicSO def = defs[i];
            if (def == null || string.IsNullOrWhiteSpace(def.id) || def.icon == null)
                continue;
            if (manager.HasRelicId(def.id))
                continue;
            pool.Add(def);
        }

        if (pool.Count == 0)
            return null;

        return pool[Random.Range(0, pool.Count)];
    }
}
