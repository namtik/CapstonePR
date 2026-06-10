using UnityEngine;
using UnityEngine.UI;

// 보스 스테이지 클리어 시 게임 클리어 화면을 표시하는 컨트롤러
public class GameClearController : MonoBehaviour
{
    // 전역 싱글턴 인스턴스
    public static GameClearController Instance { get; private set; }

    [Header("게임 클리어 캔버스 (비활성 상태로 시작)")]
    [SerializeField] private GameObject gameClearCanvas; // 클리어 화면 캔버스

    [Tooltip("게임 클리어 화면이 떠 있는 동안 숨길 인게임 공용 UI(GlobalUI). 클리어 화면을 닫으면 다시 켜진다.")]
    [SerializeField] private GameObject globalUI; // 클리어 중 숨길 공용 UI

    [Header("버튼")]
    [Tooltip("클릭 시 진행 중이던 런을 정리하고 메인 메뉴로 돌아간다.")]
    [SerializeField] private Button mainMenuButton; // 메인 메뉴 버튼
    [Tooltip("클릭 시 게임을 종료한다.")]
    [SerializeField] private Button quitButton; // 게임 종료 버튼

    // 캔버스 연결 여부 (클리어 화면 표시 준비 상태)
    public bool IsReady => gameClearCanvas != null;

    // 초기화: 싱글턴 설정, 캔버스 비활성화, 버튼 리스너 연결
    void Awake()
    {
        Instance = this;

        if (gameClearCanvas != null)
            gameClearCanvas.SetActive(false);

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveListener(OnMainMenuClicked);
            mainMenuButton.onClick.AddListener(OnMainMenuClicked);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(OnQuitClicked);
            quitButton.onClick.AddListener(OnQuitClicked);
        }
    }

    // 파괴 시 싱글턴 참조 해제
    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // 게임 클리어 화면을 표시한다 (보스 클리어 시 RoundManager가 호출)
    public void ShowGameClear()
    {
        if (gameClearCanvas == null)
        {
            Debug.LogError("[GameClear] gameClearCanvas가 비어 있어 게임 클리어 화면을 표시할 수 없습니다. 인스펙터에 캔버스를 연결하세요.");
            return;
        }

        if (globalUI != null)
            globalUI.SetActive(false);

        gameClearCanvas.SetActive(true);
        Time.timeScale = 0f;
    }

    // 클리어 화면 [메인 메뉴]: 런을 정리하고 메인 메뉴로 복귀
    public void OnMainMenuClicked()
    {
        Time.timeScale = 1f;

        if (gameClearCanvas != null)
            gameClearCanvas.SetActive(false);

        if (globalUI != null)
            globalUI.SetActive(true);

        GameStateController state = GameStateController.Instance;
        if (state != null)
            state.ReturnToMainMenu();
        else
            Debug.LogError("[GameClear] GameStateController.Instance가 null이라 메인 메뉴로 돌아갈 수 없습니다.");
    }

    // 클리어 화면 [게임 종료]: 애플리케이션 종료
    public void OnQuitClicked()
    {
        Debug.Log("게임 종료");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
