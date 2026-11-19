using UnityEngine;
using UnityEngine.UI;

// 강화 중인 적 HP바 왼쪽에 PowerUp 아이콘을 붙인다. 강화가 풀리면 끈다.
public class EnemyPowerUpMark : MonoBehaviour
{
    const string RootName = "PowerUpIcon";
    const string SpritePath = "Assets/Sprite/UI/EnemyHpBar/PowerUp.png";

    static Sprite _sprite;

    RectTransform _root;
    Image _image;

    public static void Apply(RectTransform bar, bool on)
    {
        if (bar == null) return;
        var mark = bar.GetComponent<EnemyPowerUpMark>();
        if (mark == null) mark = bar.gameObject.AddComponent<EnemyPowerUpMark>();
        mark.SetShown(on);
    }

    public void SetShown(bool on)
    {
        if (!on)
        {
            if (_root != null) _root.gameObject.SetActive(false);
            return;
        }

        Ensure();
        if (_image != null)
        {
            Sprite sprite = LoadSprite();
            if (sprite != null) _image.sprite = sprite;
        }
        if (_root != null)
        {
            Place();
            _root.SetAsLastSibling();
            _root.gameObject.SetActive(_image == null || _image.sprite != null);
        }
    }

    void LateUpdate()
    {
        if (_root == null || !_root.gameObject.activeInHierarchy) return;
        Place();
    }

    void Ensure()
    {
        if (_root != null) return;
        Transform found = transform.Find(RootName);
        if (found != null)
        {
            _root = found as RectTransform;
            _image = found.GetComponent<Image>();
            if (_root != null && _image != null) return;
        }

        var go = new GameObject(RootName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(transform, false);
        _root = go.GetComponent<RectTransform>();
        _root.localRotation = Quaternion.identity;
        _root.localScale = Vector3.one;
        Place();

        _image = go.GetComponent<Image>();
        _image.preserveAspect = true;
        _image.raycastTarget = false;
        _image.maskable = false;
        _image.color = Color.white;
    }

    // 특이사항·상태이상 아이콘과 같이 HP바 왼쪽 위 모서리에 이어 붙인다.
    void Place()
    {
        if (_root == null) return;
        float x = 0f;
        float size = 45f;

        RectTransform note = transform.Find("EnemyNoteIcon") as RectTransform;
        if (note != null && note.gameObject.activeInHierarchy)
        {
            float noteSize = note.rect.width > 1f ? note.rect.width : note.sizeDelta.x;
            if (noteSize > 1f)
            {
                x += noteSize;
                size = noteSize;
            }
        }

        StatusPanelUI panel = GetComponentInChildren<StatusPanelUI>(true);
        if (panel != null)
        {
            var layout = panel.GetComponent<GridLayoutGroup>();
            if (layout != null && layout.cellSize.y > 1f) size = layout.cellSize.y;
            float step = layout != null ? layout.cellSize.x + layout.spacing.x : size;
            int shown = 0;
            StatusIconUI[] icons = panel.GetComponentsInChildren<StatusIconUI>(true);
            for (int i = 0; i < icons.Length; i++)
            {
                if (icons[i] != null && icons[i].gameObject.activeInHierarchy) shown++;
            }
            x += shown * step;
        }

        _root.anchorMin = _root.anchorMax = new Vector2(0f, 1f);
        _root.pivot = new Vector2(0f, 0f);
        _root.anchoredPosition = new Vector2(x, 0f);
        _root.sizeDelta = new Vector2(size, size);
        _root.localRotation = Quaternion.identity;
        _root.localScale = Vector3.one;
    }

    static Sprite LoadSprite()
    {
        if (_sprite != null) return _sprite;
        RoundManager style = FindFirstObjectByType<RoundManager>(FindObjectsInactive.Include);
        if (style != null && style.EnemyPowerUpIcon != null)
            _sprite = style.EnemyPowerUpIcon;
#if UNITY_EDITOR
        if (_sprite == null)
            _sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
#endif
        return _sprite;
    }
}
