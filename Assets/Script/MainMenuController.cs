using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게임 시작 화면(메인 메뉴) 컨트롤러.
/// 씬에 미리 배치된 메인 화면 GameObject에 붙이고, 게임 플레이 / 설정 / 게임 종료 3개 버튼을 연결한다.
/// - 게임 플레이: 메인 화면을 닫고 맵으로 진입 (GameStateController.StartGame)
/// - 설정: 설정 패널 열기 (SettingPanel.OpenSettings)
/// - 게임 종료: 애플리케이션 종료
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("메인 화면 루트")]
    [Tooltip("메인 메뉴 전체 루트. 비워두면 이 컴포넌트가 붙은 오브젝트를 사용한다.")]
    [SerializeField] private GameObject mainMenuRoot;

    [Header("버튼")]
    [SerializeField] private Button playButton;     // 게임 플레이
    [SerializeField] private Button settingsButton; // 설정
    [SerializeField] private Button quitButton;     // 게임 종료

    [Header("참조 (비워두면 자동 탐색)")]
    [SerializeField] private GameStateController gameStateController;
    [SerializeField] private SettingPanel settingPanel;

    void Awake()
    {
        if (mainMenuRoot == null)
            mainMenuRoot = gameObject;

        ResolveReferences();

        if (playButton != null)
        {
            playButton.onClick.RemoveListener(OnPlay);
            playButton.onClick.AddListener(OnPlay);
        }
        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveListener(OnSettings);
            settingsButton.onClick.AddListener(OnSettings);
        }
        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(OnQuit);
            quitButton.onClick.AddListener(OnQuit);
        }
    }

    void ResolveReferences()
    {
        if (gameStateController == null)
            gameStateController = GameStateController.Instance != null
                ? GameStateController.Instance
                : FindFirstObjectByType<GameStateController>(FindObjectsInactive.Include);

        if (settingPanel == null)
            settingPanel = FindFirstObjectByType<SettingPanel>(FindObjectsInactive.Include);
    }

    /// <summary>메인 화면을 다시 표시(외부 호출용).</summary>
    public void Show()
    {
        Time.timeScale = 1f;
        if (mainMenuRoot != null)
            mainMenuRoot.SetActive(true);
    }

    /// <summary>메인 화면 숨김.</summary>
    public void Hide()
    {
        if (mainMenuRoot != null)
            mainMenuRoot.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────
    // 버튼 콜백
    // ─────────────────────────────────────────────────────────────

    void OnPlay()
    {
        ResolveReferences();

        if (gameStateController != null)
        {
            // GameStateController가 메인 화면을 닫고 맵을 띄운다.
            gameStateController.StartGame();
        }
        else
        {
            Debug.LogError("[MainMenuController] GameStateController를 찾지 못해 게임을 시작할 수 없습니다.");
            Hide();
        }
    }

    void OnSettings()
    {
        ResolveReferences();

        if (settingPanel != null)
            settingPanel.OpenSettings();
        else
            Debug.LogWarning("[MainMenuController] SettingPanel을 찾지 못해 설정을 열 수 없습니다.");
    }

    void OnQuit()
    {
        Debug.Log("[MainMenuController] 게임 종료");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
