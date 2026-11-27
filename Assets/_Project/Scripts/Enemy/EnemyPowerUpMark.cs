using UnityEngine;
using UnityEngine.UI;

// 강화 중인 적 HP바 왼쪽에 PowerUp 아이콘을 붙인다. 강화가 풀리면 끈다.
public class EnemyPowerUpMark : MonoBehaviour
{
    const string RootName = "PowerUpIcon";
    const string SpritePath = "Assets/_Project/Sprites/UI/EnemyHpBar/PowerUp.png";

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
            _root.gameObject.SetActive(_image == null || _image.sprite != null);
        }
    }

    void LateUpdate()
    {
        if (_root == null || !_root.gameObject.activeInHierarchy) return;
        Place();
    }

    // 아이콘을 상태이상 패널 안에 넣었으면 HP바(이 컴포넌트)가 사라질 때 같이 지운다 — 패널은 다음 적에게 재사용된다
    void OnDestroy()
    {
        if (_root != null && _root.parent != transform) Destroy(_root.gameObject);
    }

    void Ensure()
    {
        if (_root != null) return;
        Transform found = transform.Find(RootName);
        if (found != null)
        {
            _root = found as RectTransform;
            _image = found.GetComponent<Image>();
            if (_root != null && _image != null)
            {
                EnemyNoteBadge.AttachToIcon(_root, StatusPanelUI.PowerUpTooltip);
                return;
            }
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
        EnemyNoteBadge.AttachToIcon(_root, StatusPanelUI.PowerUpTooltip); // 마우스를 올리면 해골 아이콘과 같은 말풍선
    }

    // 특이사항·상태이상 아이콘과 같이 HP바 왼쪽 위 모서리에 이어 붙인다.
    // 상태이상 패널(Grid 레이아웃)이 이 HP바에 붙어 있으면 그 줄의 맨 앞 칸으로 넣는다.
    // 따로 좌표를 계산하면 강화가 풀렸을 때 뒤의 화상 등 아이콘이 당겨지지 않고 빈칸이 남았다.
    void Place()
    {
        if (_root == null) return;

        StatusPanelUI row = GetComponentInChildren<StatusPanelUI>(true);
        if (row != null && row.GetComponent<LayoutGroup>() != null)
        {
            if (_root.parent != row.transform) _root.SetParent(row.transform, false);
            if (_root.GetSiblingIndex() != 0) _root.SetAsFirstSibling();
            _root.localRotation = Quaternion.identity;
            _root.localScale = Vector3.one;
            return;
        }
        if (_root.parent != transform) _root.SetParent(transform, false);

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
        if (_sprite == null && BuildAssetRefs.Instance != null)
            _sprite = BuildAssetRefs.Instance.enemyPowerUpIcon;
#if UNITY_EDITOR
        if (_sprite == null)
            _sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
#endif
        return _sprite;
    }
}
