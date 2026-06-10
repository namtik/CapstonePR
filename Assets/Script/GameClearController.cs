using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보스 스테이지를 클리어하면 게임 클리어 화면을 표시한다.
/// 이 스크립트는 항상 활성화된 오브젝트(예: GameStateController)에 붙인다.
/// gameClearCanvas는 비활성 상태로 시작하고, 보스 클리어 시 Roundmanager가 활성화한다.
/// 화면(캔버스)과 버튼 배치는 직접 구성한 뒤 아래 필드에 연결하면 된다.
/// 버튼은 [메인 메뉴], [게임 종료] 2개.
/// </summary>
public class GameClearController : MonoBehaviour
{
    public static GameClearController Instance { get; private set; }

    [Header("게임 클리어 캔버스 (비활성 상태로 시작)")]
    [SerializeField] private GameObject gameClearCanvas;

    [Tooltip("게임 클리어 화면이 떠 있는 동안 숨길 인게임 공용 UI(GlobalUI). 클리어 화면을 닫으면 다시 켜진다.")]
    [SerializeField] private GameObject globalUI;

    [Header("버튼")]
    [Tooltip("클릭 시 진행 중이던 런을 정리하고 메인 메뉴로 돌아간다.")]
    [SerializeField] private Button mainMenuButton;
    [Tooltip("클릭 시 게임을 종료한다.")]
    [SerializeField] private Button quitButton;

    /// <summary>캔버스가 연결되어 게임 클리어 화면을 표시할 준비가 됐는지 여부.</summary>
    public bool IsReady => gameClearCanvas != null;

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

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>게임 클리어 화면을 표시한다. (보스 클리어 시 Roundmanager가 호출)</summary>
    public void ShowGameClear()
    {
        if (gameClearCanvas == null)
        {
            Debug.LogError("[GameClear] gameClearCanvas가 비어 있어 게임 클리어 화면을 표시할 수 없습니다. 인스펙터에 캔버스를 연결하세요.");
            return;
        }

        // 클리어 화면이 떠 있는 동안 인게임 공용 UI(HUD 등)를 숨긴다.
        if (globalUI != null)
            globalUI.SetActive(false);

        gameClearCanvas.SetActive(true);
        Time.timeScale = 0f;
    }

    /// <summary>게임 클리어 [메인 메뉴] — 런을 정리하고 메인 메뉴로 돌아간다.</summary>
    public void OnMainMenuClicked()
    {
        Time.timeScale = 1f;

        if (gameClearCanvas != null)
            gameClearCanvas.SetActive(false);

        // 숨겼던 공용 UI를 다시 켜서 다음 런에 HUD가 꺼진 채 남지 않도록 한다.
        if (globalUI != null)
            globalUI.SetActive(true);

        GameStateController state = GameStateController.Instance;
        if (state != null)
            state.ReturnToMainMenu();
        else
            Debug.LogError("[GameClear] GameStateController.Instance가 null이라 메인 메뉴로 돌아갈 수 없습니다.");
    }

    /// <summary>게임 클리어 [게임 종료].</summary>
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
