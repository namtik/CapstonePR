using System.Collections;
using System.Collections.Generic;
using Battle;
using Battle.Card;
using Battle.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 깨달음 — 보유 콤보 목록에서 하나를 골라 속성 순서를 바꾼다
public class RestEnlightenmentPanel : MonoBehaviour
{
    [Header("패널")]
    [SerializeField] private GameObject listPanel;           // 보유 콤보 목록 패널
    [SerializeField] private GameObject reorderPanel;        // 속성 재배열 패널

    [Header("목록")]
    [SerializeField] private Transform listRoot;             // 콤보 항목 부모
    [SerializeField] private BookRewardItemUI comboItemPrefab; // 콤보 책 프리팹
    [SerializeField] private ScrollRect listScroll;          // 가로 드래그 스크롤
    [SerializeField] private Button listBackButton;          // 목록에서 선택 화면으로 돌아가기

    [Header("재배열")]
    [SerializeField] private TMP_Text reorderNameText;       // 선택한 콤보 이름
    [SerializeField] private TMP_Text reorderHintText;       // 조작 안내
    [SerializeField] private Image[] slotImages = new Image[3]; // 슬롯 속성 아이콘
    [SerializeField] private Button[] slotButtons = new Button[3]; // 슬롯 루트(드래그 핸들)
    [SerializeField] private Button confirmButton;           // 순서 확정
    [SerializeField] private Button reorderBackButton;       // 재배열에서 목록으로 돌아가기
    [SerializeField] private Color slotBackgroundColor = new Color(0f, 0f, 0f, 0.55f); // 슬롯 반투명 검정
    [SerializeField] private Color slotDropTargetColor = new Color(0f, 0f, 0f, 0.78f); // 드롭 대상 슬롯 배경

    [Header("속성 아이콘")]
    [SerializeField] private Sprite fireElementSprite;
    [SerializeField] private Sprite waterElementSprite;
    [SerializeField] private Sprite windElementSprite;
    [SerializeField] private Sprite earthElementSprite;

    [Header("목록 배치")]
    [SerializeField] private Vector2 listCellSize = new Vector2(320f, 220f);
    [SerializeField] private float bookFitPadding = 12f;

    public event System.Action OnBackToChoice; // 목록에서 뒤로 — 명상/깨달음 선택으로
    public event System.Action OnConfirmed;    // 순서 확정 — 맵 복귀

    readonly List<GameObject> _spawned = new List<GameObject>();
    readonly CardElement[] _slots = new CardElement[3];
    readonly CardElement[] _originalSlots = new CardElement[3];

    ComboSkillDef _editing;
    int _dragSlot = -1;
    int _hoverSlot = -1;
    RectTransform _dragGhost;
    Image _dragGhostImage;
    Coroutine _listLayoutRoutine;

    void Awake()
    {
        if (listScroll == null && listRoot != null)
            listScroll = listRoot.GetComponentInParent<ScrollRect>(true);

        RegisterUiCallbacks();
    }

    void OnDestroy()
    {
        UnregisterUiCallbacks();
    }

    // 보유 콤보 목록을 열고 재배열 화면은 숨긴다
    public void Open()
    {
        gameObject.SetActive(true);
        ShowList();
    }

    // 패널 전체를 닫고 생성된 목록 항목을 정리한다
    public void Hide()
    {
        ClearSpawned();
        _editing = null;
        EndSlotDrag();
        if (listPanel != null) listPanel.SetActive(false);
        if (reorderPanel != null) reorderPanel.SetActive(false);
        gameObject.SetActive(false);
    }

    void ShowList()
    {
        if (listPanel != null) listPanel.SetActive(true);
        if (reorderPanel != null) reorderPanel.SetActive(false);
        _editing = null;
        EndSlotDrag();
        RebuildList();
    }

    void RebuildList()
    {
        ClearSpawned();
        if (comboItemPrefab == null || listRoot == null)
            return;

        List<ComboSkillDef> combos = CollectOwnedCombos();
        for (int i = 0; i < combos.Count; i++)
        {
            ComboSkillDef def = combos[i];
            if (def == null) continue;

            var cellGo = new GameObject("ComboCell", typeof(RectTransform), typeof(LayoutElement));
            var cellRt = (RectTransform)cellGo.transform;
            cellRt.SetParent(listRoot, false);
            cellRt.sizeDelta = listCellSize;
            var layout = cellGo.GetComponent<LayoutElement>();
            layout.preferredWidth = listCellSize.x;
            layout.preferredHeight = listCellSize.y;
            layout.minWidth = listCellSize.x;
            layout.minHeight = listCellSize.y;

            BookRewardItemUI item = Instantiate(comboItemPrefab, cellRt);
            item.SetElementSprites(fireElementSprite, waterElementSprite, windElementSprite, earthElementSprite);

            bool canReorder = def.CanReorderSlots();
            item.Bind(def, canReorder ? OnComboPicked : null);
            item.SetSelected(false);
            FitBookInCell(item.transform as RectTransform, cellRt);

            if (!canReorder)
            {
                var group = item.gameObject.GetComponent<CanvasGroup>();
                if (group == null) group = item.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0.45f;
                group.interactable = false;
                group.blocksRaycasts = false;
            }

            _spawned.Add(cellGo);
        }

        ScheduleListLayoutRefresh();
    }

    // 한 권이면 가운데, 뷰포트를 넘으면 왼쪽부터 가로 드래그
    void ScheduleListLayoutRefresh()
    {
        if (!isActiveAndEnabled)
        {
            RefreshListLayout();
            return;
        }

        if (_listLayoutRoutine != null)
            StopCoroutine(_listLayoutRoutine);
        _listLayoutRoutine = StartCoroutine(RefreshListLayoutNextFrame());
    }

    IEnumerator RefreshListLayoutNextFrame()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        RefreshListLayout();
        _listLayoutRoutine = null;
    }

    void RefreshListLayout()
    {
        var content = listRoot as RectTransform;
        if (content == null)
            return;

        if (listScroll == null)
            listScroll = content.GetComponentInParent<ScrollRect>(true);

        var hlg = content.GetComponent<HorizontalLayoutGroup>();
        float spacing = hlg != null ? hlg.spacing : 0f;
        float pad = hlg != null ? hlg.padding.left + hlg.padding.right : 0f;
        float itemsWidth = pad + _spawned.Count * listCellSize.x + Mathf.Max(0, _spawned.Count - 1) * spacing;

        RectTransform viewport = listScroll != null && listScroll.viewport != null
            ? listScroll.viewport
            : content.parent as RectTransform;
        float viewportWidth = viewport != null ? viewport.rect.width : 0f;
        bool fits = viewportWidth <= 1f || itemsWidth <= viewportWidth + 0.5f;

        if (hlg != null)
            hlg.childAlignment = fits ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;

        if (fits)
        {
            content.anchorMin = new Vector2(0.5f, 0.5f);
            content.anchorMax = new Vector2(0.5f, 0.5f);
            content.pivot = new Vector2(0.5f, 0.5f);
            content.anchoredPosition = Vector2.zero;
            if (listScroll != null)
            {
                listScroll.horizontal = false;
                listScroll.vertical = false;
            }
        }
        else
        {
            content.anchorMin = new Vector2(0f, 0.5f);
            content.anchorMax = new Vector2(0f, 0.5f);
            content.pivot = new Vector2(0f, 0.5f);
            content.anchoredPosition = Vector2.zero;
            if (listScroll != null)
            {
                listScroll.horizontal = true;
                listScroll.vertical = false;
                listScroll.movementType = ScrollRect.MovementType.Clamped;
                listScroll.inertia = true;
                listScroll.horizontalNormalizedPosition = 0f;
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }

    static List<ComboSkillDef> CollectOwnedCombos()
    {
        var result = new List<ComboSkillDef>();
        var nb = NewBattleController.Instance;
        if (nb == null || nb.OwnedComboSkills == null)
            return result;

        for (int i = 0; i < nb.OwnedComboSkills.Count; i++)
        {
            ComboSkillDef def = nb.OwnedComboSkills[i];
            if (def != null) result.Add(def);
        }

        return result;
    }

    void FitBookInCell(RectTransform itemRt, RectTransform cellRt)
    {
        if (itemRt == null || cellRt == null) return;

        Vector2 native = itemRt.sizeDelta;
        if (native.x < 1f || native.y < 1f) native = new Vector2(600f, 400f);

        itemRt.anchorMin = itemRt.anchorMax = new Vector2(0.5f, 0.5f);
        itemRt.pivot = new Vector2(0.5f, 0.5f);
        itemRt.anchoredPosition = Vector2.zero;

        Vector2 cell = listCellSize;
        if (cell.x < 1f || cell.y < 1f)
            cell = cellRt.sizeDelta;
        float fit = Mathf.Min(
            (cell.x - bookFitPadding) / native.x,
            (cell.y - bookFitPadding) / native.y);
        itemRt.localScale = Vector3.one * Mathf.Max(0.1f, fit);
    }

    void OnComboPicked(ComboSkillDef picked)
    {
        if (picked == null || !picked.CanReorderSlots())
            return;

        _editing = picked;
        _slots[0] = picked.slot1;
        _slots[1] = picked.slot2;
        _slots[2] = picked.slot3;
        _originalSlots[0] = picked.slot1;
        _originalSlots[1] = picked.slot2;
        _originalSlots[2] = picked.slot3;
        EndSlotDrag();

        if (listPanel != null) listPanel.SetActive(false);
        if (reorderPanel != null) reorderPanel.SetActive(true);

        if (reorderNameText != null)
            reorderNameText.text = string.IsNullOrWhiteSpace(picked.displayName) ? "콤보 스킬" : picked.displayName;
        if (reorderHintText != null)
            reorderHintText.text = "아이콘을 드래그해서 순서를 바꾸세요";

        RefreshSlotVisuals();
        RefreshConfirmInteractable();
    }

    void OnSlotBeginDrag(int index, PointerEventData eventData)
    {
        if (_editing == null || index < 0 || index > 2)
            return;

        _dragSlot = index;
        _hoverSlot = index;
        EnsureDragGhost();
        if (_dragGhostImage != null)
        {
            _dragGhostImage.sprite = ResolveElementSprite(_slots[index]);
            _dragGhostImage.enabled = _dragGhostImage.sprite != null;
            _dragGhostImage.preserveAspect = true;
        }

        if (_dragGhost != null)
        {
            _dragGhost.gameObject.SetActive(true);
            _dragGhost.SetAsLastSibling();
            MoveDragGhost(eventData);
        }

        RefreshSlotVisuals();
    }

    void OnSlotDrag(PointerEventData eventData)
    {
        if (_dragSlot < 0)
            return;

        MoveDragGhost(eventData);
        int hit = HitSlotIndex(eventData);
        if (hit != _hoverSlot)
        {
            _hoverSlot = hit;
            RefreshSlotVisuals();
        }
    }

    void OnSlotEndDrag(PointerEventData eventData)
    {
        if (_dragSlot < 0)
            return;

        int drop = HitSlotIndex(eventData);
        if (drop >= 0 && drop != _dragSlot)
        {
            CardElement temp = _slots[_dragSlot];
            _slots[_dragSlot] = _slots[drop];
            _slots[drop] = temp;
        }

        EndSlotDrag();
        RefreshSlotVisuals();
        RefreshConfirmInteractable();
    }

    void EndSlotDrag()
    {
        _dragSlot = -1;
        _hoverSlot = -1;
        if (_dragGhost != null)
            _dragGhost.gameObject.SetActive(false);
    }

    void EnsureDragGhost()
    {
        if (_dragGhost != null)
            return;

        Transform parent = reorderPanel != null ? reorderPanel.transform : transform;
        var go = new GameObject("ElementDragGhost", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        go.layer = 5;
        _dragGhost = (RectTransform)go.transform;
        _dragGhost.SetParent(parent, false);
        _dragGhost.sizeDelta = new Vector2(120f, 120f);
        _dragGhost.anchorMin = _dragGhost.anchorMax = new Vector2(0.5f, 0.5f);
        _dragGhost.pivot = new Vector2(0.5f, 0.5f);

        _dragGhostImage = go.GetComponent<Image>();
        _dragGhostImage.raycastTarget = false;
        _dragGhostImage.color = Color.white;

        var group = go.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.alpha = 0.92f;
        go.SetActive(false);
    }

    void MoveDragGhost(PointerEventData eventData)
    {
        if (_dragGhost == null || eventData == null)
            return;

        var parent = _dragGhost.parent as RectTransform;
        if (parent == null)
            return;

        Vector2 local;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, eventData.pressEventCamera, out local))
            _dragGhost.anchoredPosition = local;
    }

    int HitSlotIndex(PointerEventData eventData)
    {
        if (eventData == null || slotButtons == null)
            return -1;

        for (int i = 0; i < slotButtons.Length && i < 3; i++)
        {
            if (slotButtons[i] == null)
                continue;

            var rt = slotButtons[i].transform as RectTransform;
            if (rt == null)
                continue;

            if (RectTransformUtility.RectangleContainsScreenPoint(rt, eventData.position, eventData.pressEventCamera))
                return i;
        }

        return -1;
    }

    void RefreshSlotVisuals()
    {
        for (int i = 0; i < 3; i++)
        {
            if (slotImages != null && i < slotImages.Length && slotImages[i] != null)
            {
                slotImages[i].sprite = ResolveElementSprite(_slots[i]);
                slotImages[i].enabled = slotImages[i].sprite != null;
                slotImages[i].color = i == _dragSlot ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
            }

            if (slotButtons != null && i < slotButtons.Length && slotButtons[i] != null)
            {
                var rt = slotButtons[i].transform as RectTransform;
                bool isDrop = _dragSlot >= 0 && i == _hoverSlot && i != _dragSlot;
                if (rt != null)
                    rt.localScale = Vector3.one * (isDrop ? 1.08f : 1f);

                var bg = slotButtons[i].targetGraphic as Image;
                if (bg == null)
                    bg = slotButtons[i].GetComponent<Image>();
                if (bg != null)
                    bg.color = isDrop ? slotDropTargetColor : slotBackgroundColor;
            }
        }
    }

    void RefreshConfirmInteractable()
    {
        bool changed = _editing != null
            && (_slots[0] != _originalSlots[0] || _slots[1] != _originalSlots[1] || _slots[2] != _originalSlots[2]);

        if (confirmButton != null)
            confirmButton.interactable = changed;
    }

    Sprite ResolveElementSprite(CardElement element)
    {
        return element switch
        {
            CardElement.Fire => fireElementSprite,
            CardElement.Water => waterElementSprite,
            CardElement.Wind => windElementSprite,
            CardElement.Earth => earthElementSprite,
            _ => null
        };
    }

    void OnConfirmClicked()
    {
        if (_editing == null || NewBattleController.Instance == null)
            return;
        if (confirmButton != null && !confirmButton.interactable)
            return;

        NewBattleController.Instance.SetOwnedComboSlots(_editing.refComboId, _slots[0], _slots[1], _slots[2]);
        Hide();
        OnConfirmed?.Invoke();
    }

    void OnListBackClicked()
    {
        Hide();
        OnBackToChoice?.Invoke();
    }

    void OnReorderBackClicked()
    {
        ShowList();
    }

    void RegisterUiCallbacks()
    {
        if (listBackButton != null)
        {
            listBackButton.onClick.RemoveListener(OnListBackClicked);
            listBackButton.onClick.AddListener(OnListBackClicked);
        }

        if (reorderBackButton != null)
        {
            reorderBackButton.onClick.RemoveListener(OnReorderBackClicked);
            reorderBackButton.onClick.AddListener(OnReorderBackClicked);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(OnConfirmClicked);
            confirmButton.onClick.AddListener(OnConfirmClicked);
        }

        BindSlotDrag(0);
        BindSlotDrag(1);
        BindSlotDrag(2);
    }

    void UnregisterUiCallbacks()
    {
        if (listBackButton != null)
            listBackButton.onClick.RemoveListener(OnListBackClicked);
        if (reorderBackButton != null)
            reorderBackButton.onClick.RemoveListener(OnReorderBackClicked);
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(OnConfirmClicked);

        UnbindSlotDrag(0);
        UnbindSlotDrag(1);
        UnbindSlotDrag(2);
    }

    void BindSlotDrag(int index)
    {
        if (slotButtons == null || index < 0 || index >= slotButtons.Length || slotButtons[index] == null)
            return;

        Button button = slotButtons[index];
        button.onClick.RemoveAllListeners();
        button.transition = Selectable.Transition.None;
        button.interactable = true;

        var trigger = button.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = button.gameObject.AddComponent<EventTrigger>();
        trigger.triggers.Clear();

        AddTrigger(trigger, EventTriggerType.BeginDrag, eventData => OnSlotBeginDrag(index, eventData));
        AddTrigger(trigger, EventTriggerType.Drag, eventData => OnSlotDrag(eventData));
        AddTrigger(trigger, EventTriggerType.EndDrag, eventData => OnSlotEndDrag(eventData));
    }

    void UnbindSlotDrag(int index)
    {
        if (slotButtons == null || index < 0 || index >= slotButtons.Length || slotButtons[index] == null)
            return;

        slotButtons[index].onClick.RemoveAllListeners();
        var trigger = slotButtons[index].GetComponent<EventTrigger>();
        if (trigger != null)
            trigger.triggers.Clear();
    }

    static void AddTrigger(EventTrigger trigger, EventTriggerType type, System.Action<PointerEventData> callback)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(data => callback(data as PointerEventData));
        trigger.triggers.Add(entry);
    }

    void ClearSpawned()
    {
        if (_listLayoutRoutine != null)
        {
            StopCoroutine(_listLayoutRoutine);
            _listLayoutRoutine = null;
        }

        for (int i = 0; i < _spawned.Count; i++)
        {
            if (_spawned[i] != null)
                Destroy(_spawned[i]);
        }
        _spawned.Clear();
    }
}
