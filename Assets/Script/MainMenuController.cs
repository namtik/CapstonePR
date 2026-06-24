using UnityEngine;
using UnityEngine.UI;

// 게임 시작 화면(메인 메뉴)의 버튼과 설정/사운드 캔버스를 제어하는 컨트롤러
public class MainMenuController : MonoBehaviour
{
    [Header("메인 화면 루트")]
    [Tooltip("메인 메뉴 전체 루트. 비워두면 이 컴포넌트가 붙은 오브젝트를 사용한다.")]
    [SerializeField] private GameObject mainMenuRoot; // 메인 메뉴 루트 오브젝트

    [Header("버튼")]
    [SerializeField] private Button playButton;     // 게임 플레이 버튼
    [SerializeField] private Button tutorialButton; // 조작법 버튼
    [SerializeField] private Button settingsButton; // 설정 버튼
    [SerializeField] private Button quitButton;     // 게임 종료 버튼

    [Header("조작법")]
    [SerializeField] private TutorialPanelController tutorialPanel; // 조작법 팝업

    [Header("메인 메뉴 전용 설정 캔버스")]
    [Tooltip("메인 메뉴의 설정 버튼으로 열 캔버스. 지정하면 인게임 공용 SettingCanvas 대신 이 캔버스를 연다.")]
    [SerializeField] private GameObject mainMenuSettingCanvas; // 메인 메뉴 전용 설정 캔버스
    [Tooltip("(선택) 위 설정 캔버스의 닫기/뒤로 버튼. 지정하면 자동으로 닫기 동작에 연결한다.")]
    [SerializeField] private Button settingsBackButton; // 설정 닫기/뒤로 버튼
    [Tooltip("(선택) 위 설정 캔버스의 사운드 버튼. 지정하면 자동으로 사운드 메뉴 열기에 연결한다.")]
    [SerializeField] private Button soundButton; // 사운드 메뉴 열기 버튼
    [Tooltip("사운드 메뉴 캔버스. 사운드 버튼으로 연다. (비활성 상태로 시작)")]
    [SerializeField] private GameObject soundMenu; // 사운드 메뉴 캔버스

    [Header("참조 (비워두면 자동 탐색)")]
    [SerializeField] private GameStateController gameStateController; // 게임 상태 컨트롤러 참조
    [SerializeField] private SettingPanel settingPanel; // 설정 패널 참조

    // 초기화: 루트/참조 정리, 전용 캔버스 비활성화, 버튼 리스너 연결
    void Awake()
    {
        if (mainMenuRoot == null)
            mainMenuRoot = gameObject;

        ResolveReferences();

        if (mainMenuSettingCanvas != null)
            mainMenuSettingCanvas.SetActive(false);
        if (soundMenu != null)
            soundMenu.SetActive(false);

        if (playButton != null)
        {
            playButton.onClick.RemoveListener(OnPlay);
            playButton.onClick.AddListener(OnPlay);
        }
        if (tutorialButton != null)
        {
            tutorialButton.onClick.RemoveListener(OnTutorial);
            tutorialButton.onClick.AddListener(OnTutorial);
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

    // 비어 있는 참조를 씬에서 자동 탐색해 채운다
    void ResolveReferences()
    {
        if (gameStateController == null)
            gameStateController = GameStateController.Instance != null
                ? GameStateController.Instance
                : FindFirstObjectByType<GameStateController>(FindObjectsInactive.Include);

        if (settingPanel == null)
            settingPanel = FindFirstObjectByType<SettingPanel>(FindObjectsInactive.Include);
    }

    // 메인 화면을 다시 표시한다 (외부 호출용)
    public void Show()
    {
        Time.timeScale = 1f;
        if (mainMenuRoot != null)
            mainMenuRoot.SetActive(true);
    }

    // 메인 화면을 숨긴다
    public void Hide()
    {
        if (mainMenuRoot != null)
            mainMenuRoot.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────
    // 버튼 콜백
    // ─────────────────────────────────────────────────────────────

    // [게임 플레이]: 메인 화면을 닫고 맵으로 진입
    void OnPlay()
    {
        ResolveReferences();

        if (gameStateController != null)
        {
            gameStateController.StartGame();
        }
        else
        {
            Debug.LogError("[MainMenuController] GameStateController를 찾지 못해 게임을 시작할 수 없습니다.");
            Hide();
        }
    }

    // [조작법]: 튜토리얼 팝업을 연다
    void OnTutorial()
    {
        if (tutorialPanel != null)
        {
            tutorialPanel.Open();
            return;
        }

        Debug.LogWarning("[MainMenuController] TutorialPanelController가 연결되지 않아 조작법을 열 수 없습니다.");
    }

    // [설정]: 전용 설정 캔버스 또는 공용 설정 패널을 연다
    void OnSettings()
    {
        if (mainMenuSettingCanvas != null)
        {
            mainMenuSettingCanvas.SetActive(true);
            return;
        }

        ResolveReferences();

        if (settingPanel != null)
            settingPanel.OpenSettings();
        else
            Debug.LogWarning("[MainMenuController] 메인 메뉴 설정 캔버스도, SettingPanel도 찾지 못해 설정을 열 수 없습니다.");
    }

    // 메인 메뉴 설정 캔버스를 닫는다 (닫기/뒤로 버튼에 연결)
    public void CloseSettings()
    {
        if (mainMenuSettingCanvas != null)
            mainMenuSettingCanvas.SetActive(false);
    }

    // 설정 캔버스를 닫고 공용 사운드 메뉴를 연다
    public void OpenSoundMenu()
    {
        ResolveReferences();

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

    // [게임 종료]: 애플리케이션 종료
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
