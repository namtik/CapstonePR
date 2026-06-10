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

    [Header("메인 메뉴 전용 설정 캔버스")]
    [Tooltip("메인 메뉴의 설정 버튼으로 열 캔버스. 지정하면 인게임 공용 SettingCanvas 대신 이 캔버스를 연다.")]
    [SerializeField] private GameObject mainMenuSettingCanvas;
    [Tooltip("(선택) 위 설정 캔버스의 닫기/뒤로 버튼. 지정하면 자동으로 닫기 동작에 연결한다.")]
    [SerializeField] private Button settingsBackButton;
    [Tooltip("(선택) 위 설정 캔버스의 사운드 버튼. 지정하면 자동으로 사운드 메뉴 열기에 연결한다.")]
    [SerializeField] private Button soundButton;
    [Tooltip("사운드 메뉴 캔버스. 사운드 버튼으로 연다. (비활성 상태로 시작)")]
    [SerializeField] private GameObject soundMenu;

    [Header("참조 (비워두면 자동 탐색)")]
    [SerializeField] private GameStateController gameStateController;
    [SerializeField] private SettingPanel settingPanel;

    void Awake()
    {
        if (mainMenuRoot == null)
            mainMenuRoot = gameObject;

        ResolveReferences();

        // 메인 메뉴 전용 설정 캔버스 / 사운드 메뉴는 비활성 상태로 시작한다.
        if (mainMenuSettingCanvas != null)
            mainMenuSettingCanvas.SetActive(false);
        if (soundMenu != null)
            soundMenu.SetActive(false);

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
        if (settingsBackButton != null)
        {
            settingsBackButton.onClick.RemoveListener(CloseSettings);
            settingsBackButton.onClick.AddListener(CloseSettings);
        }
        if (soundButton != null)
        {
            soundButton.onClick.RemoveListener(OpenSoundMenu);
            soundButton.onClick.AddListener(OpenSoundMenu);
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
        // 메인 메뉴 전용 설정 캔버스가 지정되어 있으면 인게임 공용 SettingCanvas 대신 그것을 연다.
        if (mainMenuSettingCanvas != null)
        {
            mainMenuSettingCanvas.SetActive(true);
            return;
        }

        // 폴백: 전용 캔버스가 없으면 기존처럼 공용 설정 패널을 연다.
        ResolveReferences();

        if (settingPanel != null)
            settingPanel.OpenSettings();
        else
            Debug.LogWarning("[MainMenuController] 메인 메뉴 설정 캔버스도, SettingPanel도 찾지 못해 설정을 열 수 없습니다.");
    }

    /// <summary>메인 메뉴 설정 캔버스를 닫는다. 캔버스 내부의 닫기/뒤로 버튼 OnClick에 연결해 쓰면 된다.</summary>
    public void CloseSettings()
    {
        if (mainMenuSettingCanvas != null)
            mainMenuSettingCanvas.SetActive(false);
    }

    /// <summary>설정 캔버스를 닫고 (공용) 사운드 메뉴를 연다.</summary>
    public void OpenSoundMenu()
    {
        ResolveReferences();

        // 공용 SoundMenu의 [뒤로] 버튼은 SettingPanel.CloseSoundSettings에 묶여 있다.
        // SettingPanel을 통해 열어서, 닫을 때 메인 메뉴 설정 캔버스로 복귀하도록 복귀 대상을 지정한다.
        if (settingPanel != null)
        {
            settingPanel.OpenSoundFromExternal(mainMenuSettingCanvas);
            return;
        }

        // 폴백: SettingPanel을 못 찾으면 직접 토글 (이 경우 뒤로 버튼 복귀는 보장되지 않음).
        if (soundMenu != null)
            soundMenu.SetActive(true);
        if (mainMenuSettingCanvas != null)
            mainMenuSettingCanvas.SetActive(false);
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
