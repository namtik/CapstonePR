using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 이 스크립트는 항상 활성화된 오브젝트(예: GameStateController)에 붙여야 합니다.
/// settingCanvas는 비활성 상태로 시작하고, SettingButton을 누르면 활성화됩니다.
/// </summary>
public class SettingPanel : MonoBehaviour
{
    [Header("설정 캔버스 (비활성 상태로 시작)")]
    public GameObject settingCanvas;

    [Header("패널 내부 버튼")]
    public Button resumeButton;
    public Button restartButton;
    public Button reloadButton;
    public Button quitButton;

    [Header("추가 버튼")]
    [Tooltip("클릭 시 메인 화면으로 돌아간다.")]
    public Button mainMenuButton;
    [Tooltip("클릭 시 사운드 설정 캔버스를 연다.")]
    public Button soundButton;

    [Header("사운드 설정 캔버스 (설정 캔버스와 별개의 오브젝트, 비활성 상태로 시작)")]
    public GameObject soundCanvas;
    [Tooltip("마스터 볼륨 슬라이더 (0~1). 모든 사운드에 적용된다.")]
    public Slider masterVolumeSlider;
    [Tooltip("(선택) 사운드 설정 → 설정 패널로 돌아가는 버튼.")]
    public Button soundBackButton;

    const string MasterVolumeKey = "MasterVolume";

    void Start()
    {
        // 설정 캔버스 비활성화
        if (settingCanvas != null)
            settingCanvas.SetActive(false);

        // 패널 내부 버튼 연결
        if (resumeButton != null)
            resumeButton.onClick.AddListener(CloseSettings);
        if (restartButton != null)
            restartButton.onClick.AddListener(RestartGame);
        if (reloadButton != null)
            reloadButton.onClick.AddListener(ReloadCurrentScene);
        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);

        // 추가 버튼 연결
        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(GoToMainMenu);
        if (soundButton != null)
            soundButton.onClick.AddListener(OpenSoundSettings);
        if (soundBackButton != null)
            soundBackButton.onClick.AddListener(CloseSoundSettings);

        // 사운드 캔버스 비활성화 + 마스터 볼륨 초기화
        if (soundCanvas != null)
            soundCanvas.SetActive(false);
        InitMasterVolume();

        // 모든 스테이지의 SettingButton을 자동으로 찾아서 연결
        BindAllSettingButtons();
    }

    void BindAllSettingButtons()
    {
        var allButtons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var btn in allButtons)
        {
            if (btn.gameObject.name == "SettingButton")
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(OpenSettings);
            }
        }
    }

    public void OpenSettings()
    {
        if (settingCanvas != null)
        {
            settingCanvas.SetActive(true);
            EnsureCanvasComponents();
            Time.timeScale = 0f;
        }
    }

    void EnsureCanvasComponents()
    {
        // Canvas 컴포넌트 확인 및 추가
        var canvas = settingCanvas.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = settingCanvas.AddComponent<Canvas>();
        }
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        // CanvasScaler 확인 및 추가
        var scaler = settingCanvas.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = settingCanvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }

        // GraphicRaycaster 확인 및 추가
        if (settingCanvas.GetComponent<GraphicRaycaster>() == null)
        {
            settingCanvas.AddComponent<GraphicRaycaster>();
        }
    }

    public void CloseSettings()
    {
        if (settingCanvas != null)
        {
            settingCanvas.SetActive(false);
            Time.timeScale = 1f;
        }
    }

    public void RestartGame()
    {
        TryRestartRunToMap();
    }

    public void ReloadCurrentScene()
    {
        TryRestartRunToMap();
    }

    void TryRestartRunToMap()
    {
        Time.timeScale = 1f;

        if (settingCanvas != null)
            settingCanvas.SetActive(false);

        var stateController = GameStateController.Instance;
        if (stateController == null)
        {
            Debug.LogError("GameStateController.Instance가 null이라 런 재시작을 수행할 수 없습니다.");
            return;
        }

        stateController.RestartRunToMap();
    }

    // ───────────── 메인 화면 / 사운드 설정 ─────────────

    /// <summary>설정창 [메인화면으로 돌아가기] — 설정/사운드 캔버스를 닫고 메인 메뉴로 이동.</summary>
    public void GoToMainMenu()
    {
        if (soundCanvas != null)
            soundCanvas.SetActive(false);
        if (settingCanvas != null)
            settingCanvas.SetActive(false);
        Time.timeScale = 1f;

        var stateController = GameStateController.Instance;
        if (stateController == null)
        {
            Debug.LogError("GameStateController.Instance가 null이라 메인 화면으로 돌아갈 수 없습니다.");
            return;
        }

        stateController.ReturnToMainMenu();
    }

    /// <summary>설정창 [사운드] — 설정 패널을 숨기고 사운드 설정 캔버스를 연다. (일시정지 유지)</summary>
    public void OpenSoundSettings()
    {
        if (soundCanvas != null)
            soundCanvas.SetActive(true);
        if (settingCanvas != null)
            settingCanvas.SetActive(false);
    }

    /// <summary>사운드 설정 [뒤로] — 사운드 캔버스를 닫고 설정 패널로 복귀.</summary>
    public void CloseSoundSettings()
    {
        if (soundCanvas != null)
            soundCanvas.SetActive(false);
        if (settingCanvas != null)
            settingCanvas.SetActive(true);
    }

    // 저장된 마스터 볼륨을 적용하고 슬라이더를 동기화한다.
    void InitMasterVolume()
    {
        float saved = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, 1f));
        AudioListener.volume = saved;

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.minValue = 0f;
            masterVolumeSlider.maxValue = 1f;
            masterVolumeSlider.SetValueWithoutNotify(saved);
            masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        }
    }

    // 마스터 볼륨 슬라이더 변경 시 — 전역 볼륨 적용 + 저장.
    void OnMasterVolumeChanged(float value)
    {
        float v = Mathf.Clamp01(value);
        AudioListener.volume = v;
        PlayerPrefs.SetFloat(MasterVolumeKey, v);
        PlayerPrefs.Save();
    }

    public void QuitGame()
    {
        Debug.Log("게임 종료");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
