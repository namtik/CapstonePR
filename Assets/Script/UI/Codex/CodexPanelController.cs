using UnityEngine;
using UnityEngine.UI;

namespace Battle.UI
{
    // 도감 패널 — menubar 도감 버튼으로 열고 탭(부적/사방비급/유물)을 전환(보기 전용)
    // 인벤토리와 동일하게 어떤 스테이지에도 속하지 않는 "항상 켜진" 오버레이 캔버스에 둔다.
    public class CodexPanelController : MonoBehaviour
    {
        // 탭 하나 — 탭 버튼 + 페이지 + 탭 컴포넌트
        [System.Serializable]
        public struct TabEntry
        {
            public Button button;   // 탭 버튼
            public GameObject page; // 탭 페이지
            public CodexTab tab;    // 탭 컴포넌트
        }

        // 전역 싱글톤 — 어느 스테이지의 버튼/스크립트든 Instance로 열 수 있다
        public static CodexPanelController Instance { get; private set; }

        // 도감이 현재 열려 있는지 — 배경 스테이지의 Input 폴링 조작 차단용
        public static bool IsOpen =>
            Instance != null && Instance.panelRoot != null && Instance.panelRoot.activeInHierarchy;

        [Header("패널")]
        [Tooltip("열고 닫을 패널 루트. 비우면 이 컴포넌트의 GameObject(권장하지 않음).")]
        [SerializeField] private GameObject panelRoot;        // 패널 루트

        [Header("탭 (0번이 열 때 기본 탭)")]
        [SerializeField] private TabEntry[] tabs = new TabEntry[0]; // 탭 목록

        [Header("버튼 (있으면 자동 연결)")]
        [SerializeField] private Button closeButton;          // 닫기(X) 버튼

        [Header("옵션")]
        [SerializeField] private bool startHidden = true;     // 시작 시 패널 숨김
        [Tooltip("아무 스테이지에서나 이 키로 도감 토글(None이면 비활성).")]
        [SerializeField] private KeyCode toggleKey = KeyCode.None; // 전역 토글 단축키
        [Tooltip("ON이면 열 때 패널 캔버스를 강제로 최상단(overlaySortingOrder)에 그려 어느 스테이지 위로도 뜨게 한다.")]
        [SerializeField] private bool bringCanvasToTopOnOpen = true; // 열 때 최상단 보장
        [SerializeField] private int overlaySortingOrder = 100;     // 오버레이 정렬 순서

        // 싱글톤 등록, 참조 자동 보정 및 버튼 자동 연결, 시작 시 숨김
        void Awake()
        {
            if (Instance == null) Instance = this;

            if (panelRoot == null) panelRoot = gameObject;

            if (closeButton != null) closeButton.onClick.AddListener(Close);
            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                if (tabs[i].button != null) tabs[i].button.onClick.AddListener(() => ShowTab(index));
            }

            if (startHidden) panelRoot.SetActive(false);
        }

        // 인스턴스 참조 해제
        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // 전역 단축키로 토글
        void Update()
        {
            if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
                Toggle();
        }

        // 패널이 속한 캔버스를 최상단에 그려 어느 스테이지 위로도 뜨게 한다
        void EnsureOnTop()
        {
            if (!bringCanvasToTopOnOpen || panelRoot == null) return;
            Canvas canvas = panelRoot.GetComponentInParent<Canvas>();
            if (canvas == null) return;
            if (!canvas.isRootCanvas) canvas.overrideSorting = true;
            canvas.sortingOrder = overlaySortingOrder;
        }

        // 열림/닫힘 토글
        public void Toggle()
        {
            if (panelRoot != null && panelRoot.activeSelf) Close();
            else Open();
        }

        // 패널을 열고 기본 탭부터 표시(인벤토리가 열려 있으면 닫음)
        public void Open()
        {
            if (InventoryPanelController.IsOpen) InventoryPanelController.Instance.Close();

            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
                panelRoot.transform.SetAsLastSibling();
            }
            EnsureOnTop();
            ShowTab(0);
        }

        // 패널을 닫음
        public void Close()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        // index번 탭으로 전환하고 갱신(활성 탭 버튼을 앞으로)
        public void ShowTab(int index)
        {
            if (index < 0 || index >= tabs.Length) return;
            for (int i = 0; i < tabs.Length; i++)
                if (tabs[i].page != null) tabs[i].page.SetActive(i == index);

            if (tabs[index].button != null) tabs[index].button.transform.SetAsLastSibling();
            if (tabs[index].tab != null) tabs[index].tab.Refresh();
        }
    }
}
