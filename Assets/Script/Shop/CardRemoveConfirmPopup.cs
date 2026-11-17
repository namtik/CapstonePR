using Battle.Card;
using Battle.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 상점 카드 제거 확인 팝업 — 선택한 카드 미리보기 + "제거 하시겠습니까?" + [제거]/[취소]
public class CardRemoveConfirmPopup : MonoBehaviour
{
    [Header("구성 요소")]
    [Tooltip("팝업 뒤 딤(그리드 클릭 차단). 비워도 됨.")]
    [SerializeField] private Image dimBackground;
    [Tooltip("선택 카드 미리보기가 들어갈 영역. 카드가 이 영역에 맞춰 비율 유지 축소된다.")]
    [SerializeField] private RectTransform cardMount;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button confirmButton; // 제거
    [SerializeField] private Button cancelButton;  // 취소
    [Tooltip("제거 연출 때 숨길 팝업 본체(배경·문구·버튼). 카드 미리보기는 자동으로 밖으로 빼서 남긴다. 비우면 문구·버튼만 숨김.")]
    [SerializeField] private GameObject popupBody;

    [Header("옵션")]
    [SerializeField] private string message = "제거 하시겠습니까?";
    [Tooltip("ON이면 딤 클릭 시 취소로 처리.")]
    [SerializeField] private bool cancelOnDimClick = false;
    [Tooltip("그리드 호버 카드(정렬 200)보다 위에 그려지도록 팝업 캔버스 정렬 순서.")]
    [SerializeField] private int sortingOrder = 300;

    [Header("제거 연출 (카드가 불타 사라짐)")]
    [Tooltip("ON이면 [제거] 시 팝업 본체를 숨기고 카드만 남긴 뒤 각성 부적 전환과 같은 불타기 연출로 사라진다.")]
    [SerializeField] private bool burnOnConfirm = true;
    [Tooltip("팝업 본체가 사라진 뒤 불붙기 전 대기(초).")]
    [SerializeField, Min(0f)] private float burnStartDelay = 0.15f;
    [Tooltip("카드가 다 타는 시간(초).")]
    [SerializeField, Min(0.05f)] private float burnDuration = 0.8f;
    [Tooltip("다 탄 뒤 팝업·목록이 닫히기 전 대기(초).")]
    [SerializeField, Min(0f)] private float burnEndDelay = 0.2f;

    NewCardView _preview;
    System.Action _onConfirm;
    System.Action _onCancel;
    bool _wired;
    bool _burning; // 제거 연출 진행 중(입력 무시)
    Coroutine _burnRoutine;

    public bool IsOpen => gameObject.activeSelf;

    void Awake()
    {
        Wire();
    }

    void Wire()
    {
        if (_wired) return;
        _wired = true;

        if (confirmButton != null) confirmButton.onClick.AddListener(HandleConfirm);
        if (cancelButton != null) cancelButton.onClick.AddListener(HandleCancel);

        if (dimBackground != null)
        {
            dimBackground.raycastTarget = true;
            if (cancelOnDimClick)
            {
                var b = dimBackground.GetComponent<Button>();
                if (b == null) b = dimBackground.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(HandleCancel);
            }
        }
    }

    // 팝업 열기 — 카드 미리보기 생성 후 결과 콜백 대기
    public void Show(CardData card, NewCardView cardPrefab, System.Action onConfirm, System.Action onCancel)
    {
        Wire();
        _onConfirm = onConfirm;
        _onCancel = onCancel;

        StopBurn();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        EnsureOnTop();
        SetBodyVisible(true);

        if (messageText != null) messageText.text = message;
        BuildPreview(card, cardPrefab);
    }

    // 팝업 닫기(콜백 호출 없음)
    public void Hide()
    {
        StopBurn();
        SetBodyVisible(true); // 다음에 열 때를 위해 본체 복구
        _onConfirm = null;
        _onCancel = null;
        ClearPreview();
        gameObject.SetActive(false);
    }

    void HandleConfirm()
    {
        if (_burning) return;
        var cb = _onConfirm;

        if (burnOnConfirm && _preview != null && isActiveAndEnabled)
        {
            _burnRoutine = StartCoroutine(BurnThenConfirm(cb));
            return;
        }

        Hide();
        cb?.Invoke();
    }

    // 팝업 본체를 숨기고 카드만 남겨 불태운 뒤 확정 콜백
    System.Collections.IEnumerator BurnThenConfirm(System.Action cb)
    {
        _burning = true;

        // 카드가 본체(배경) 자식이어도 남도록 팝업 루트로 옮김(화면 위치·크기 유지)
        _preview.transform.SetParent(transform, true);
        _preview.transform.SetAsLastSibling();
        SetBodyVisible(false);

        if (burnStartDelay > 0f) yield return new WaitForSecondsRealtime(burnStartDelay);

        bool done = false;
        _preview.PlayBurnAway(burnDuration, () => done = true);
        while (!done) yield return null;

        if (burnEndDelay > 0f) yield return new WaitForSecondsRealtime(burnEndDelay);

        _burning = false;
        _burnRoutine = null;
        Hide();
        cb?.Invoke();
    }

    void StopBurn()
    {
        if (_burnRoutine != null) StopCoroutine(_burnRoutine);
        _burnRoutine = null;
        _burning = false;
    }

    // 팝업 본체(배경·문구·버튼) 표시 전환
    void SetBodyVisible(bool visible)
    {
        if (popupBody != null)
        {
            popupBody.SetActive(visible);
            return;
        }
        if (messageText != null) messageText.gameObject.SetActive(visible);
        if (confirmButton != null) confirmButton.gameObject.SetActive(visible);
        if (cancelButton != null) cancelButton.gameObject.SetActive(visible);
    }

    void HandleCancel()
    {
        if (_burning) return;
        var cb = _onCancel;
        Hide();
        cb?.Invoke();
    }

    // 그리드의 호버 카드(임시 캔버스 overrideSorting)보다 위에 그리기
    void EnsureOnTop()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
        }
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;
    }

    // 카드 미리보기를 cardMount 크기에 맞춰 비율 유지로 배치
    void BuildPreview(CardData card, NewCardView cardPrefab)
    {
        ClearPreview();
        if (card == null || cardPrefab == null || cardMount == null) return;

        _preview = Instantiate(cardPrefab, cardMount);
        _preview.ApplyShopPreview(card);

        var cg = _preview.GetComponent<CanvasGroup>();
        if (cg == null) cg = _preview.gameObject.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false; // interactable은 건드리지 않음 — 카드 안 Button이 비활성 색(어둡게)으로 바뀜

        var rt = (RectTransform)_preview.transform;
        Vector2 native = rt.sizeDelta;
        if (native.x < 1f || native.y < 1f) native = new Vector2(300f, 400f);
        Vector2 area = cardMount.rect.size;
        float fit = (area.x > 1f && area.y > 1f) ? Mathf.Min(area.x / native.x, area.y / native.y) : 1f;

        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one * fit;
    }

    void ClearPreview()
    {
        if (_preview != null) Destroy(_preview.gameObject);
        _preview = null;
    }
}
