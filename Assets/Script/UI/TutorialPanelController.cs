using UnityEngine;
using UnityEngine.UI;

// 메인 메뉴 조작법(튜토리얼) 팝업 — 이미지 페이지 넘김
public class TutorialPanelController : MonoBehaviour
{
    [Header("루트")]
    [Tooltip("튜토리얼 전체 패널. 비우면 이 컴포넌트가 붙은 오브젝트를 사용한다.")]
    [SerializeField] private GameObject panelRoot; // 튜토리얼 패널 루트

    [Header("페이지")]
    [SerializeField] private Image pageImage; // 현재 페이지 이미지
    [SerializeField] private Sprite[] pages; // 조작법 이미지(텍스트 포함)

    [Header("버튼")]
    [SerializeField] private Button prevButton; // 이전 페이지
    [SerializeField] private Button nextButton; // 다음 페이지
    [SerializeField] private Button closeButton; // 닫기(X)

    private int currentPageIndex; // 현재 페이지 인덱스(0부터)
    private bool initialized; // 버튼 리스너 연결 완료 여부

    // 비활성 TutorialCanvas는 첫 Open() 때 Awake가 실행된다. 여기서 panelRoot를 끄면 첫 클릭이 무효가 된다.
    void Awake()
    {
        EnsureInitialized();
    }

    // 튜토리얼을 1페이지부터 연다
    public void Open()
    {
        EnsureInitialized();
        currentPageIndex = 0;
        panelRoot.SetActive(true);
        RefreshPage();
    }

    // 버튼 리스너를 1회만 연결한다 (TutorialCanvas가 비활성으로 시작해도 Open()에서 보장)
    void EnsureInitialized()
    {
        if (initialized)
            return;

        initialized = true;

        if (panelRoot == null)
            panelRoot = gameObject;

        BindButton(prevButton, ShowPreviousPage);
        BindButton(nextButton, ShowNextPage);
        BindButton(closeButton, Close);
    }

    // 튜토리얼 패널을 닫는다
    public void Close()
    {
        panelRoot.SetActive(false);
    }

    // 이전 페이지로 이동
    void ShowPreviousPage()
    {
        if (pages == null || pages.Length == 0)
            return;

        if (currentPageIndex <= 0)
            return;

        currentPageIndex--;
        RefreshPage();
    }

    // 다음 페이지로 이동
    void ShowNextPage()
    {
        if (pages == null || pages.Length == 0)
            return;

        if (currentPageIndex >= pages.Length - 1)
            return;

        currentPageIndex++;
        RefreshPage();
    }

    // 현재 페이지 이미지와 좌·우 버튼 상태 갱신
    void RefreshPage()
    {
        if (pageImage != null)
        {
            if (pages != null && pages.Length > 0 && currentPageIndex >= 0 && currentPageIndex < pages.Length)
                pageImage.sprite = pages[currentPageIndex];
            else
                pageImage.sprite = null;
        }

        int lastIndex = pages != null ? pages.Length - 1 : -1;
        SetButtonInteractable(prevButton, currentPageIndex > 0);
        SetButtonInteractable(nextButton, lastIndex >= 0 && currentPageIndex < lastIndex);
    }

    // 버튼의 기존 콜백을 제거 후 새로 등록한다(중복 방지)
    void BindButton(Button button, UnityEngine.Events.UnityAction callback)
    {
        if (button == null || callback == null)
            return;

        button.onClick.RemoveListener(callback);
        button.onClick.AddListener(callback);
    }

    // 버튼 interactable만 토글(null이면 무시)
    static void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null)
            button.interactable = interactable;
    }
}
