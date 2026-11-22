using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Battle.UI
{
    // 인벤토리 패널 — 상단 버튼으로 열고 부적/사방비급/유물 탭을 전환(보기 전용)
    // 주의: 이 컴포넌트(+panelRoot)는 어떤 스테이지에도 속하지 않는 "항상 켜진"
    //       상위 오버레이 캔버스에 둔다. 그래야 맵/전투/상점 등 모든 스테이지에서 뜬다.
    //       데이터(RunDeckState·NewBattleController)는 싱글톤이라 스테이지 토글과 무관하게 유지된다.
    public class InventoryPanelController : MonoBehaviour
    {
        // 전역 싱글톤 — 어느 스테이지의 버튼/스크립트든 Instance로 열 수 있다
        public static InventoryPanelController Instance { get; private set; }

        // 인벤토리가 현재 열려 있는지 — 배경 스테이지의 Input 폴링 조작 차단용
        public static bool IsOpen =>
            Instance != null && Instance.panelRoot != null && Instance.panelRoot.activeInHierarchy;

        [Header("패널")]
        [Tooltip("열고 닫을 패널 루트. 비우면 이 컴포넌트의 GameObject(권장하지 않음).")]
        [SerializeField] private GameObject panelRoot;        // 패널 루트

        [Header("탭 페이지")]
        [SerializeField] private GameObject cardTabPage;      // 부적 탭 페이지
        [SerializeField] private GameObject comboTabPage;     // 사방비급 탭 페이지
        [SerializeField] private GameObject relicTabPage;     // 유물 탭 페이지
        [SerializeField] private InventoryCardTab cardTab;    // 부적 탭 컴포넌트
        [SerializeField] private InventoryComboTab comboTab;  // 사방비급 탭 컴포넌트
        [SerializeField] private CodexRelicTab relicTab;      // 유물 탭 컴포넌트(보유 유물만)

        [Header("버튼 (있으면 자동 연결)")]
        [Tooltip("상단 인벤토리 열기 버튼. 패널 바깥(항상 활성)에 두기.")]
        [SerializeField] private Button openButton;           // 열기 버튼
        [SerializeField] private Button closeButton;          // 닫기(X) 버튼
        [SerializeField] private Button cardTabButton;        // 부적 탭 버튼
        [SerializeField] private Button comboTabButton;       // 사방비급 탭 버튼
        [SerializeField] private Button relicTabButton;       // 유물 탭 버튼

        [Header("옵션")]
        [SerializeField] private bool startHidden = true;     // 시작 시 패널 숨김
        [Tooltip("ON이면 패널을 열 때 Time.timeScale=0 으로 정지.")]
        [SerializeField] private bool pauseTimeWhenOpen = false; // 열 때 시간 정지
        [Tooltip("아무 스테이지에서나 이 키로 인벤토리 토글(None이면 비활성).")]
        [SerializeField] private KeyCode toggleKey = KeyCode.None; // 전역 토글 단축키
        [Tooltip("ON이면 열 때 패널 캔버스를 강제로 최상단(overlaySortingOrder)에 그려 어느 스테이지 위로도 뜨게 한다.")]
        [SerializeField] private bool bringCanvasToTopOnOpen = true; // 열 때 최상단 보장
        [SerializeField] private int overlaySortingOrder = 100;     // 오버레이 정렬 순서

        bool _isOpen;   // 현재 열림 상태

        // 싱글톤 등록, 참조 자동 보정 및 버튼 자동 연결, 시작 시 숨김
        void Awake()
        {
            if (Instance == null) Instance = this;

            if (panelRoot == null) panelRoot = gameObject;

            if (openButton != null) openButton.onClick.AddListener(Toggle);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (cardTabButton != null) cardTabButton.onClick.AddListener(ShowCardTab);
            if (comboTabButton != null) comboTabButton.onClick.AddListener(ShowComboTab);
            if (relicTabButton != null) relicTabButton.onClick.AddListener(ShowRelicTab);

            if (startHidden)
            {
                panelRoot.SetActive(false);
                _isOpen = false;
            }
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
            // 전용 루트 캔버스면 sortingOrder만으로 충분, 다른 캔버스 밑에 중첩됐으면 overrideSorting 필요
            if (!canvas.isRootCanvas) canvas.overrideSorting = true;
            canvas.sortingOrder = overlaySortingOrder;
        }

        // 열림/닫힘 토글 — 부모 캔버스가 꺼져 있으면 닫힌 것으로 본다
        public void Toggle()
        {
            bool open = panelRoot != null && panelRoot.activeInHierarchy;
            if (open) Close();
            else Open();
        }

        // 패널을 열고 부적 탭부터 표시.
        // 캔버스가 씬 시작부터 비활성이어도 여기서 켠다. 그때 스케일이 0이면 화면에 안 나오므로 보정한다.
        public void Open()
        {
            bool wasOpen = panelRoot != null && panelRoot.activeInHierarchy;
            if (CodexPanelController.IsOpen) CodexPanelController.Instance.Close();

            _isOpen = true;
            WakeHiddenCanvas();
            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
                panelRoot.transform.SetAsLastSibling();
            }
            EnsureOnTop();
            ShowCardTab();
            if (pauseTimeWhenOpen) Time.timeScale = 0f;
            if (!wasOpen) SfxManager.Instance?.PlayInventoryOpen();
        }

        // 패널 위의 꺼진 캔버스까지 켜고, 시작부터 꺼져 있어 스케일이 0인 루트 캔버스는 다시 맞춘다
        void WakeHiddenCanvas()
        {
            if (panelRoot == null) return;

            var chain = new List<GameObject>();
            Transform t = panelRoot.transform.parent;
            while (t != null)
            {
                chain.Add(t.gameObject);
                t = t.parent;
            }
            for (int i = chain.Count - 1; i >= 0; i--)
            {
                if (!chain[i].activeSelf)
                    chain[i].SetActive(true);
            }

            Canvas canvas = panelRoot.GetComponentInParent<Canvas>();
            if (canvas == null || !CanvasScaleBroken(canvas)) return;
            ApplyFallbackScale(canvas);
            if (_scaleRepair != null) StopCoroutine(_scaleRepair);
            _scaleRepair = StartCoroutine(RepairCanvasScale(canvas));
        }

        Coroutine _scaleRepair;

        static bool CanvasScaleBroken(Canvas canvas)
        {
            RectTransform rt = canvas.transform as RectTransform;
            return rt != null && rt.localScale.sqrMagnitude < 0.0001f;
        }

        // 시작부터 꺼진 캔버스는 CanvasScaler가 스케일 0을 유지하는 경우가 있다.
        // 한 프레임 뒤에도 0이면 스케일러를 끄고 화면 크기에 맞춰 직접 넣는다.
        System.Collections.IEnumerator RepairCanvasScale(Canvas canvas)
        {
            yield return null;
            if (canvas != null && CanvasScaleBroken(canvas))
            {
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler != null) scaler.enabled = false;
                ApplyFallbackScale(canvas);
            }
            _scaleRepair = null;
        }

        static void ApplyFallbackScale(Canvas canvas)
        {
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            float scale = 1f;
            if (scaler != null &&
                scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize &&
                scaler.referenceResolution.x > 1f &&
                scaler.referenceResolution.y > 1f &&
                Screen.width > 0 && Screen.height > 0)
            {
                float logWidth = Mathf.Log(Screen.width / scaler.referenceResolution.x, 2f);
                float logHeight = Mathf.Log(Screen.height / scaler.referenceResolution.y, 2f);
                scale = Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, scaler.matchWidthOrHeight));
            }
            if (scale < 0.01f || float.IsNaN(scale) || float.IsInfinity(scale)) scale = 1f;
            canvas.scaleFactor = scale;
        }

        // 패널을 닫음
        public void Close()
        {
            _isOpen = false;
            if (pauseTimeWhenOpen) Time.timeScale = 1f;
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        // 부적 탭으로 전환하고 갱신(활성 탭 버튼을 앞으로)
        public void ShowCardTab()
        {
            SetTabVisible(cardTabPage);
            BringActiveTabToFront(cardTabButton);
            if (cardTab != null) cardTab.Refresh();
        }

        // 사방비급 탭으로 전환하고 갱신(활성 탭 버튼을 앞으로)
        public void ShowComboTab()
        {
            SetTabVisible(comboTabPage);
            BringActiveTabToFront(comboTabButton);
            if (comboTab != null) comboTab.Refresh();
        }

        // 유물 탭으로 전환하고 갱신(활성 탭 버튼을 앞으로)
        public void ShowRelicTab()
        {
            SetTabVisible(relicTabPage);
            BringActiveTabToFront(relicTabButton);
            if (relicTab != null) relicTab.Refresh();
        }

        void SetTabVisible(GameObject activePage)
        {
            if (cardTabPage != null) cardTabPage.SetActive(cardTabPage == activePage);
            if (comboTabPage != null) comboTabPage.SetActive(comboTabPage == activePage);
            if (relicTabPage != null) relicTabPage.SetActive(relicTabPage == activePage);
        }

        // 현재 열린 탭의 버튼을 형제 중 맨 뒤(가장 위에 그려짐)로 올린다
        void BringActiveTabToFront(Button activeTab)
        {
            if (activeTab != null) activeTab.transform.SetAsLastSibling();
        }
    }
}
