using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using TMPro;

/// <summary>
/// 이 스크립트는 항상 활성화된 오브젝트(예: GameStateController)에 붙여야 합니다.
/// settingCanvas는 비활성 상태로 시작하고, SettingButton을 누르면 활성화됩니다.
/// </summary>
public class SettingPanel : MonoBehaviour
{
    /// <summary>
    /// 볼륨 채널 한 개(배경음/효과음/UI 등). 슬라이더+숫자입력을 묶고,
    /// AudioMixer의 노출 파라미터에 dB로 적용하거나 PlayerPrefs에 저장한다.
    /// 나중에 채널을 추가하려면 인스펙터의 Volume Channels 리스트에 항목을 추가하고
    /// Slider/Input을 연결한 뒤 Key(저장 키)와 Exposed Parameter(믹서 파라미터명)를 지정하면 된다.
    /// </summary>
    [System.Serializable]
    public class VolumeChannel
    {
        [Tooltip("식별자 겸 PlayerPrefs 저장 키. 예: BGM, SFX, UI")]
        public string key;
        [Tooltip("이 채널의 볼륨 슬라이더 (0~1). 비어 있으면 이 채널은 비활성으로 간주한다.")]
        public Slider slider;
        [Tooltip("(선택) 숫자 입력칸 (0~100). 슬라이더와 양방향 동기화.")]
        public TMP_InputField input;
        [Tooltip("(선택) AudioMixer에 노출(Expose)된 파라미터 이름. 지정하면 볼륨을 dB로 믹서에 적용한다.")]
        public string exposedParameter;
    }

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
    [Tooltip("마스터 볼륨 숫자 입력/표시 (0~100). 슬라이더와 양방향으로 동기화된다.")]
    public TMP_InputField masterVolumeInput;
    [Tooltip("(선택) 사운드 설정 → 설정 패널로 돌아가는 버튼.")]
    public Button soundBackButton;

    [Header("추가 볼륨 채널 (배경음 / 효과음 / UI — 나중에 슬라이더만 연결하면 동작)")]
    [Tooltip("배경음/효과음/UI를 카테고리별로 조절하려면 AudioMixer를 연결하고, 아래 채널의 Exposed Parameter에 믹서 노출 파라미터명을 적는다. 믹서가 없으면 값은 PlayerPrefs에만 저장되며, 각 사운드 코드에서 PlayerPrefs.GetFloat(\"Volume_<Key>\", 1f)로 읽어 쓰면 된다.")]
    public AudioMixer audioMixer;
    [Tooltip("볼륨 채널 목록. Key를 저장 키로 사용하고, Slider(+선택 Input)를 연결하면 자동으로 동작한다.")]
    public List<VolumeChannel> volumeChannels = new List<VolumeChannel>
    {
        new VolumeChannel { key = "BGM" },
        new VolumeChannel { key = "SFX" },
        new VolumeChannel { key = "UI" },
    };

    const string MasterVolumeKey = "MasterVolume";
    const string VolumeKeyPrefix = "Volume_";

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

        // 사운드 캔버스 비활성화 + 볼륨 초기화
        if (soundCanvas != null)
            soundCanvas.SetActive(false);
        InitMasterVolume();
        InitVolumeChannels();

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

    // 저장된 마스터 볼륨을 적용하고 슬라이더/숫자입력을 동기화한다.
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

        if (masterVolumeInput != null)
        {
            // 정수(0~100)만 입력받도록 제한
            masterVolumeInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            masterVolumeInput.characterLimit = 3;
            masterVolumeInput.onEndEdit.RemoveListener(OnMasterVolumeInput);
            masterVolumeInput.onEndEdit.AddListener(OnMasterVolumeInput);
        }

        // 슬라이더/입력칸 표시 갱신 (이벤트 없이)
        SyncVolumeUI(saved);
    }

    /// <summary>마스터 볼륨(0~1)을 적용·저장하고 슬라이더와 숫자 입력칸을 모두 동기화한다.</summary>
    void SetMasterVolume(float value01)
    {
        float v = Mathf.Clamp01(value01);
        AudioListener.volume = v;
        PlayerPrefs.SetFloat(MasterVolumeKey, v);
        PlayerPrefs.Save();
        SyncVolumeUI(v);
    }

    // 슬라이더와 입력칸 표시를 현재 값으로 맞춘다(콜백을 발생시키지 않음).
    void SyncVolumeUI(float value01)
    {
        if (masterVolumeSlider != null)
            masterVolumeSlider.SetValueWithoutNotify(value01);
        if (masterVolumeInput != null)
            masterVolumeInput.SetTextWithoutNotify(Mathf.RoundToInt(value01 * 100f).ToString());
    }

    // 마스터 볼륨 슬라이더 변경 시 — 전역 볼륨 적용 + 저장 + 숫자 갱신.
    void OnMasterVolumeChanged(float value)
    {
        SetMasterVolume(value);
    }

    // 숫자 입력칸에서 값 확정(Enter/포커스 해제) 시 — 0~100을 볼륨으로 적용.
    void OnMasterVolumeInput(string text)
    {
        if (int.TryParse(text, out int percent))
            SetMasterVolume(Mathf.Clamp(percent, 0, 100) / 100f);
        else
            SyncVolumeUI(AudioListener.volume); // 빈/잘못된 입력이면 현재 값으로 표시 복원
    }

    // ───────────── 추가 볼륨 채널 (배경음 / 효과음 / UI ...) ─────────────

    // 슬라이더가 연결된 채널만 활성화하여 저장값 적용 + 콜백 연결.
    void InitVolumeChannels()
    {
        if (volumeChannels == null) return;

        foreach (var ch in volumeChannels)
        {
            // Key가 없거나 슬라이더가 연결 안 된 항목은 아직 미설정으로 보고 건너뛴다.
            if (ch == null || string.IsNullOrEmpty(ch.key) || ch.slider == null) continue;

            float saved = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKeyPrefix + ch.key, 1f));

            ch.slider.minValue = 0f;
            ch.slider.maxValue = 1f;
            ch.slider.SetValueWithoutNotify(saved);
            ch.slider.onValueChanged.RemoveAllListeners();
            ch.slider.onValueChanged.AddListener(v => SetChannelVolume(ch, v));

            if (ch.input != null)
            {
                ch.input.contentType = TMP_InputField.ContentType.IntegerNumber;
                ch.input.characterLimit = 3;
                ch.input.onEndEdit.RemoveAllListeners();
                ch.input.onEndEdit.AddListener(t => OnChannelInput(ch, t));
            }

            SetChannelVolume(ch, saved); // 저장값을 믹서/저장/표시에 반영
        }
    }

    // 채널 숫자 입력 확정 시 — 0~100 파싱 후 적용.
    void OnChannelInput(VolumeChannel ch, string text)
    {
        if (int.TryParse(text, out int percent))
            SetChannelVolume(ch, Mathf.Clamp(percent, 0, 100) / 100f);
        else
            SyncChannelUI(ch, Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKeyPrefix + ch.key, 1f)));
    }

    /// <summary>채널 볼륨(0~1)을 적용·저장하고 슬라이더/입력칸을 동기화한다.</summary>
    void SetChannelVolume(VolumeChannel ch, float value01)
    {
        if (ch == null || string.IsNullOrEmpty(ch.key)) return;

        float v = Mathf.Clamp01(value01);
        PlayerPrefs.SetFloat(VolumeKeyPrefix + ch.key, v);
        PlayerPrefs.Save();

        // AudioMixer 노출 파라미터가 있으면 dB로 적용 (0 → -80dB 무음, 1 → 0dB)
        if (audioMixer != null && !string.IsNullOrEmpty(ch.exposedParameter))
        {
            float dB = v <= 0.0001f ? -80f : Mathf.Log10(v) * 20f;
            audioMixer.SetFloat(ch.exposedParameter, dB);
        }

        SyncChannelUI(ch, v);
    }

    // 채널 슬라이더/입력칸 표시를 현재 값으로 맞춘다(콜백을 발생시키지 않음).
    void SyncChannelUI(VolumeChannel ch, float value01)
    {
        if (ch.slider != null) ch.slider.SetValueWithoutNotify(value01);
        if (ch.input != null) ch.input.SetTextWithoutNotify(Mathf.RoundToInt(value01 * 100f).ToString());
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
