using System.Collections.Generic;
using Battle;
using Battle.Card;
using Battle.UI;
using TMPro;
using UnityEngine;
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
    [SerializeField] private Button listBackButton;          // 목록에서 선택 화면으로 돌아가기

    [Header("재배열")]
    [SerializeField] private TMP_Text reorderNameText;       // 선택한 콤보 이름
    [SerializeField] private TMP_Text reorderHintText;       // 조작 안내
    [SerializeField] private Image[] slotImages = new Image[3]; // 슬롯 속성 아이콘
    [SerializeField] private Button[] slotButtons = new Button[3]; // 슬롯 클릭 버튼
    [SerializeField] private Button confirmButton;           // 순서 확정
    [SerializeField] private Button reorderBackButton;       // 재배열에서 목록으로 돌아가기

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
    int _selectedSlot = -1;

    void Awake()
    {
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
        _selectedSlot = -1;
        if (listPanel != null) listPanel.SetActive(false);
        if (reorderPanel != null) reorderPanel.SetActive(false);
        gameObject.SetActive(false);
    }

    void ShowList()
    {
        if (listPanel != null) listPanel.SetActive(true);
        if (reorderPanel != null) reorderPanel.SetActive(false);
        _editing = null;
        _selectedSlot = -1;
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

        Vector2 cell = cellRt.sizeDelta;
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
        _selectedSlot = -1;

        if (listPanel != null) listPanel.SetActive(false);
        if (reorderPanel != null) reorderPanel.SetActive(true);

        if (reorderNameText != null)
            reorderNameText.text = string.IsNullOrWhiteSpace(picked.displayName) ? "콤보 스킬" : picked.displayName;
        if (reorderHintText != null)
            reorderHintText.text = "아이콘 두 개를 눌러 순서를 바꾸세요";

        RefreshSlotVisuals();
        RefreshConfirmInteractable();
    }

    void OnSlotClicked(int index)
    {
        if (_editing == null || index < 0 || index > 2)
            return;

        if (_selectedSlot < 0)
        {
            _selectedSlot = index;
            RefreshSlotVisuals();
            return;
        }

        if (_selectedSlot != index)
        {
            CardElement temp = _slots[_selectedSlot];
            _slots[_selectedSlot] = _slots[index];
            _slots[index] = temp;
        }

        _selectedSlot = -1;
        RefreshSlotVisuals();
        RefreshConfirmInteractable();
    }

    void RefreshSlotVisuals()
    {
        for (int i = 0; i < 3; i++)
        {
            if (slotImages != null && i < slotImages.Length && slotImages[i] != null)
            {
                slotImages[i].sprite = ResolveElementSprite(_slots[i]);
                slotImages[i].enabled = slotImages[i].sprite != null;
            }

            if (slotButtons != null && i < slotButtons.Length && slotButtons[i] != null)
            {
                var rt = slotButtons[i].transform as RectTransform;
                if (rt != null)
                    rt.localScale = Vector3.one * (i == _selectedSlot ? 1.12f : 1f);

                var img = slotButtons[i].targetGraphic as Image;
                if (img != null)
                    img.color = i == _selectedSlot
                        ? new Color(1f, 0.92f, 0.55f, 1f)
                        : Color.white;
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

        BindSlotButton(0);
        BindSlotButton(1);
        BindSlotButton(2);
    }

    void UnregisterUiCallbacks()
    {
        if (listBackButton != null)
            listBackButton.onClick.RemoveListener(OnListBackClicked);
        if (reorderBackButton != null)
            reorderBackButton.onClick.RemoveListener(OnReorderBackClicked);
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(OnConfirmClicked);

        UnbindSlotButton(0);
        UnbindSlotButton(1);
        UnbindSlotButton(2);
    }

    void BindSlotButton(int index)
    {
        if (slotButtons == null || index < 0 || index >= slotButtons.Length || slotButtons[index] == null)
            return;

        Button button = slotButtons[index];
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => OnSlotClicked(index));
    }

    void UnbindSlotButton(int index)
    {
        if (slotButtons == null || index < 0 || index >= slotButtons.Length || slotButtons[index] == null)
            return;
        slotButtons[index].onClick.RemoveAllListeners();
    }

    void ClearSpawned()
    {
        for (int i = 0; i < _spawned.Count; i++)
        {
            if (_spawned[i] != null)
                Destroy(_spawned[i]);
        }
        _spawned.Clear();
    }
}
