using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EnemyAttackIntent : MonoBehaviour
{
    const string RootName = "AttackIntent";

    RectTransform _bar;
    RectTransform _root;
    Image _iconImage;
    TextMeshProUGUI _label;
    bool _hasDamage;
    RoundManager _style;

    public void SetDamage(RectTransform bar, int damage)
    {
        _hasDamage = true;
        if (bar == null)
        {
            if (_root != null) _root.gameObject.SetActive(false);
            return;
        }

        Ensure(bar);
        if (_label != null) _label.text = Mathf.Max(0, damage).ToString();
        ApplyStyle();
        Place();
        if (_root != null) _root.gameObject.SetActive(true);
    }

    public void Hide()
    {
        _hasDamage = false;
        if (_root != null) _root.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (!_hasDamage || _root == null || !_root.gameObject.activeInHierarchy) return;
        Place();
        ApplyStyle();
    }

    void Ensure(RectTransform bar)
    {
        if (_root != null && _bar == bar && _root.parent == bar && _label != null)
            return;

        _bar = bar;
        Transform found = bar.Find(RootName);
        if (found != null)
        {
            _root = found as RectTransform;
            _iconImage = found.GetComponent<Image>();
            _label = found.GetComponentInChildren<TextMeshProUGUI>(true);
            if (_iconImage != null && _label != null) return;
            Destroy(found.gameObject);
            _root = null;
            _iconImage = null;
            _label = null;
        }

        var rootGo = new GameObject(RootName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        rootGo.transform.SetParent(bar, false);
        _root = rootGo.GetComponent<RectTransform>();
        _iconImage = rootGo.GetComponent<Image>();
        _iconImage.preserveAspect = true;
        _iconImage.raycastTarget = false;
        _iconImage.maskable = false;

        var textGo = new GameObject("Damage", typeof(RectTransform));
        textGo.transform.SetParent(_root, false);
        _label = textGo.AddComponent<TextMeshProUGUI>();
        _label.raycastTarget = false;
        _label.maskable = false;
        _label.richText = false;
        _label.enableWordWrapping = false;
        _label.overflowMode = TextOverflowModes.Overflow;
        _label.alignment = TextAlignmentOptions.MidlineRight;
        _label.extraPadding = true;
    }

    RoundManager Style()
    {
        if (_style != null) return _style;
        _style = FindFirstObjectByType<RoundManager>(FindObjectsInactive.Include);
        return _style;
    }

    void Place()
    {
        if (_root == null) return;
        RoundManager style = Style();
        Vector2 iconPosition = style != null ? style.EnemyAttackIconPosition : new Vector2(-9.76f, -6.03f);
        float iconSize = style != null ? style.EnemyAttackIconSize : 40f;
        Vector2 numberPosition = style != null ? style.EnemyAttackNumberPosition : new Vector2(56.9f, -11.72f);
        float numberSize = style != null ? style.EnemyAttackNumberSize : 38f;

        _root.anchorMin = _root.anchorMax = new Vector2(0f, 0.5f);
        _root.pivot = new Vector2(1f, 0.5f);
        _root.anchoredPosition = iconPosition;
        float side = Mathf.Max(1f, iconSize);
        _root.sizeDelta = new Vector2(side, side);
        _root.localRotation = Quaternion.identity;
        _root.localScale = Vector3.one;

        if (_label == null) return;
        RectTransform textRt = _label.rectTransform;
        textRt.anchorMin = textRt.anchorMax = new Vector2(0f, 0.5f);
        textRt.pivot = new Vector2(1f, 0.5f);
        textRt.anchoredPosition = numberPosition;
        float size = Mathf.Max(1f, numberSize);
        textRt.sizeDelta = new Vector2(size * 4f, size * 1.6f);
        textRt.localRotation = Quaternion.identity;
        textRt.localScale = Vector3.one;
    }

    void ApplyStyle()
    {
        RoundManager style = Style();

        if (_iconImage != null)
        {
            Sprite icon = style != null ? style.EnemyAttackIcon : null;
            if (icon != null) _iconImage.sprite = icon;
            _iconImage.color = Color.white;
            _iconImage.preserveAspect = true;
        }

        if (_label == null) return;
        TMP_FontAsset font = style != null ? style.EnemyAttackFont : null;
        float numberSize = style != null ? style.EnemyAttackNumberSize : 38f;
        Color numberColor = style != null ? style.EnemyAttackNumberColor : Color.red;
        float outlineWidth = style != null ? style.EnemyAttackOutlineWidth : 0.3f;
        Color outlineColor = style != null ? style.EnemyAttackOutlineColor : Color.black;
        if (font != null) _label.font = font;
        _label.fontSize = Mathf.Max(1f, numberSize);
        _label.color = numberColor;
        _label.alignment = TextAlignmentOptions.MidlineRight;
        _label.outlineWidth = Mathf.Clamp01(outlineWidth);
        _ = _label.outlineColor;
        _label.outlineColor = outlineColor;
    }
}
