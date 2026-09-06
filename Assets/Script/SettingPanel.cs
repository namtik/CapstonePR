using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using TMPro;

// 인게임/메인 메뉴 설정 패널과 사운드 설정을 관리한다
public class SettingPanel : MonoBehaviour
{
    // 볼륨 채널 한 개(슬라이더+숫자입력+믹서 파라미터)를 묶는 데이터
    [System.Serializable]
    public class VolumeChannel
    {
        [Tooltip("식별자 겸 PlayerPrefs 저장 키. 예: BGM, SFX, UI")]
        public string key; // 저장 키
        [Tooltip("이 채널의 볼륨 슬라이더 (0~1). 비어 있으면 이 채널은 비활성으로 간주한다.")]
        public Slider slider; // 볼륨 슬라이더
        [Tooltip("(선택) 숫자 입력칸 (0~100). 슬라이더와 양방향 동기화.")]
        public TMP_InputField input; // 숫자 입력칸
        [Tooltip("(선택) AudioMixer에 노출(Expose)된 파라미터 이름. 지정하면 볼륨을 dB로 믹서에 적용한다.")]
        public string exposedParameter; // 믹서 노출 파라미터명
    }

    [Header("설정 캔버스 (비활성 상태로 시작)")]
    public GameObject settingCanvas; // 설정 패널 캔버스

    [Header("패널 내부 버튼")]
    public Button resumeButton; // 닫기(계속) 버튼
    public Button restartButton; // 재시작 버튼
    public Button reloadButton; // 씬 재로드 버튼
    public Button quitButton; // 게임 종료 버튼

    [Header("추가 버튼")]
    [Tooltip("클릭 시 메인 화면으로 돌아간다.")]
    public Button mainMenuButton; // 메인 화면 이동 버튼
    [Tooltip("클릭 시 사운드 설정 캔버스를 연다.")]
    public Button soundButton; // 사운드 설정 열기 버튼

    [Header("사운드 설정 캔버스 (설정 캔버스와 별개의 오브젝트, 비활성 상태로 시작)")]
    public GameObject soundCanvas; // 사운드 설정 캔버스
    [Tooltip("마스터 볼륨 슬라이더 (0~1). 모든 사운드에 적용된다.")]
    public Slider masterVolumeSlider; // 마스터 볼륨 슬라이더
    [Tooltip("마스터 볼륨 숫자 입력/표시 (0~100). 슬라이더와 양방향으로 동기화된다.")]
    public TMP_InputField masterVolumeInput; // 마스터 볼륨 숫자 입력칸
    [Tooltip("(선택) 사운드 설정 → 설정 패널로 돌아가는 버튼.")]
    public Button soundBackButton; // 사운드 설정 뒤로 버튼

    [Header("일시정지 연출")]
    [Tooltip("전체 화면 딤. 비워두면 연출 없이 바로 연다.")]
    public CanvasGroup dimGroup; // 배경 딤
    [Tooltip("중앙 패널 CanvasGroup. 비워두면 스케일/페이드 연출을 생략한다.")]
    public CanvasGroup panelGroup; // 중앙 패널
    [Tooltip("확인 팝업 루트. 메인 메뉴/종료 전에 띄운다.")]
    public GameObject confirmRoot; // 확인 팝업
    public TMP_Text confirmMessage; // 확인 문구
    public Button confirmYesButton; // 확인 예
    public Button confirmNoButton; // 확인 아니오
    [SerializeField, Range(0.08f, 0.6f)] float openDuration = 0.2f; // 딤 열림 시간
    [SerializeField, Range(0.08f, 0.6f)] float closeDuration = 0.18f; // 딤 닫힘 시간
    [SerializeField, Range(0.2f, 1f)] float dimAlpha = 0.72f; // 딤 최대 알파
    [SerializeField, Range(0.03f, 0.16f)] float buttonStagger = 0.06f; // 버튼 간 펼침 간격
    [SerializeField, Range(0.08f, 0.4f)] float buttonAnimDuration = 0.22f; // 버튼 하나 연출 시간
    [SerializeField] float buttonFoldOffset = 56f; // 접힐 때 위로 올라가는 거리

    [Header("추가 볼륨 채널 (배경음 / 효과음 / UI — 나중에 슬라이더만 연결하면 동작)")]
    [Tooltip("배경음/효과음/UI를 카테고리별로 조절하려면 AudioMixer를 연결하고, 아래 채널의 Exposed Parameter에 믹서 노출 파라미터명을 적는다. 믹서가 없으면 값은 PlayerPrefs에만 저장되며, 각 사운드 코드에서 PlayerPrefs.GetFloat(\"Volume_<Key>\", 1f)로 읽어 쓰면 된다.")]
    public AudioMixer audioMixer; // 볼륨 적용 대상 오디오 믹서
    [Tooltip("볼륨 채널 목록. Key를 저장 키로 사용하고, Slider(+선택 Input)를 연결하면 자동으로 동작한다.")]
    public List<VolumeChannel> volumeChannels = new List<VolumeChannel> // 추가 볼륨 채널 목록
    {
        new VolumeChannel { key = "BGM" },
        new VolumeChannel { key = "SFX" },
        new VolumeChannel { key = "UI" },
    };

    const string MasterVolumeKey = "MasterVolume"; // 마스터 볼륨 저장 키
    const string VolumeKeyPrefix = "Volume_"; // 채널 볼륨 저장 키 접두사

    GameObject soundReturnCanvas; // 사운드 설정을 닫을 때 복귀할 캔버스
    Coroutine animRoutine; // 열림/닫힘 연출
    System.Action pendingConfirm; // 확인 팝업에서 예 선택 시 실행
    bool isClosing; // 닫힘 연출 진행 중

    readonly List<RectTransform> menuButtons = new List<RectTransform>();
    readonly List<CanvasGroup> menuButtonGroups = new List<CanvasGroup>();
    readonly List<ButtonHoverScale> menuHovers = new List<ButtonHoverScale>();
    readonly List<Vector2> menuRestPos = new List<Vector2>();
    readonly List<Vector3> menuRestScale = new List<Vector3>();
    readonly List<Vector2> menuFromPos = new List<Vector2>();
    readonly List<Vector3> menuFromScale = new List<Vector3>();
    readonly List<float> menuFromAlpha = new List<float>();
    VerticalLayoutGroup menuLayout; // 버튼 세로 레이아웃(연출 중 잠시 끔)
    bool menuRestCached; // 펼친 상태 기준값 저장 여부

    // 캔버스 초기화, 버튼 연결, 볼륨 초기화 수행
    void Start()
    {
        if (settingCanvas != null)
            settingCanvas.SetActive(false);

        if (resumeButton != null)
            resumeButton.onClick.AddListener(CloseSettings);
        if (restartButton != null)
            restartButton.onClick.AddListener(RestartGame);
        if (reloadButton != null)
            reloadButton.onClick.AddListener(ReloadCurrentScene);
        if (quitButton != null)
            quitButton.onClick.AddListener(RequestQuit);
        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(RequestMainMenu);
        if (soundButton != null)
            soundButton.onClick.AddListener(OpenSoundSettings);
        if (soundBackButton != null)
            soundBackButton.onClick.AddListener(CloseSoundSettings);

        if (confirmYesButton != null)
            confirmYesButton.onClick.AddListener(ConfirmYes);
        if (confirmNoButton != null)
            confirmNoButton.onClick.AddListener(HideConfirm);
        if (dimGroup != null)
        {
            var dimButton = dimGroup.GetComponent<Button>();
            if (dimButton != null)
                dimButton.onClick.AddListener(OnDimClicked);
        }

        HideConfirm();

        if (soundCanvas != null)
            soundCanvas.SetActive(false);
        InitMasterVolume();
        InitVolumeChannels();

        HideInGameSettingButtons();
    }

    // ESC로 설정/사운드 창을 토글한다
    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape))
            return;

        if (soundCanvas != null && soundCanvas.activeSelf)
        {
            CloseSoundSettings();
            return;
        }

        if (IsConfirmVisible())
        {
            HideConfirm();
            return;
        }

        if (settingCanvas != null && settingCanvas.activeSelf)
        {
            CloseSettings();
            return;
        }

        if (IsMainMenuVisible())
            return;

        OpenSettings();
    }

    // 메인 메뉴가 떠 있는 동안에는 인게임 설정 창을 열지 않는다
    static bool IsMainMenuVisible()
    {
        return FindFirstObjectByType<MainMenuController>() != null;
    }

    // 인게임 우상단 설정 아이콘을 숨긴다(메인 메뉴 설정 버튼은 유지)
    void HideInGameSettingButtons()
    {
        var allButtons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var btn in allButtons)
        {
            if (btn.gameObject.name != "SettingButton")
                continue;

            if (btn.GetComponentInParent<MainMenuController>(true) != null)
                continue;

            btn.gameObject.SetActive(false);
        }
    }

    // 설정 캔버스를 열고 게임을 일시정지한다
    public void OpenSettings()
    {
        if (settingCanvas == null || isClosing)
            return;

        HideConfirm();
        settingCanvas.SetActive(true);
        EnsureCanvasComponents();
        Time.timeScale = 0f;
        PlayAnim(true);
    }

    // 딤을 클릭하면 확인 팝업이 없을 때 창을 닫는다
    public void OnDimClicked()
    {
        if (IsConfirmVisible() || isClosing)
            return;

        CloseSettings();
    }

    // 설정 캔버스에 필요한 Canvas/Scaler/Raycaster를 보장한다
    void EnsureCanvasComponents()
    {
        var canvas = settingCanvas.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = settingCanvas.AddComponent<Canvas>();
        }
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        var scaler = settingCanvas.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = settingCanvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }

        if (settingCanvas.GetComponent<GraphicRaycaster>() == null)
        {
            settingCanvas.AddComponent<GraphicRaycaster>();
        }
    }

    // 설정 캔버스를 닫고 게임 시간을 복구한다
    public void CloseSettings()
    {
        if (settingCanvas == null || !settingCanvas.activeSelf || isClosing)
            return;

        HideConfirm();
        PlayAnim(false);
    }

    // 열림/닫힘 연출을 시작한다
    void PlayAnim(bool open)
    {
        if (animRoutine != null)
            StopCoroutine(animRoutine);

        animRoutine = StartCoroutine(AnimateRoutine(open));
    }

    IEnumerator AnimateRoutine(bool open)
    {
        isClosing = !open;
        PrepareMenuButtons(open);

        float dimDur = Mathf.Max(0.01f, open ? openDuration : closeDuration);
        float btnDur = Mathf.Max(0.01f, buttonAnimDuration);
        float stagger = Mathf.Max(0f, buttonStagger);
        int n = menuButtons.Count;
        float btnTotal = n == 0 ? 0f : (n - 1) * stagger + btnDur;
        float total = Mathf.Max(dimDur, btnTotal);

        float startDim = open ? 0f : (dimGroup != null ? dimGroup.alpha : dimAlpha);
        float endDim = open ? dimAlpha : 0f;
        if (dimGroup != null)
            dimGroup.alpha = startDim;
        if (panelGroup != null)
        {
            panelGroup.alpha = 1f;
            panelGroup.transform.localScale = Vector3.one;
        }

        float t = 0f;
        while (t < total)
        {
            t += Time.unscaledDeltaTime;
            if (dimGroup != null)
            {
                float dimK = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dimDur));
                dimGroup.alpha = Mathf.Lerp(startDim, endDim, dimK);
            }

            for (int i = 0; i < n; i++)
            {
                float startAt = open ? i * stagger : (n - 1 - i) * stagger;
                float k = Mathf.Clamp01((t - startAt) / btnDur);
                k = open ? EaseOutBack(k) : EaseInCubic(k);
                ApplyMenuButton(i, k, open);
            }

            yield return null;
        }

        if (dimGroup != null)
            dimGroup.alpha = endDim;
        for (int i = 0; i < n; i++)
            ApplyMenuButton(i, 1f, open);

        if (open)
            FinishOpenButtons();

        animRoutine = null;
        if (!open)
            FinishClose();
        else
            isClosing = false;
    }

    // 버튼 기준 위치를 잡고, 레이아웃을 끈 뒤 펼침/접힘 시작값을 저장한다
    void PrepareMenuButtons(bool open)
    {
        CollectMenuButtons();
        if (menuButtons.Count == 0)
            return;

        if (open)
            CacheMenuRestIfNeeded();

        if (menuLayout != null)
            menuLayout.enabled = false;

        if (open)
        {
            for (int i = 0; i < menuButtons.Count; i++)
                SnapMenuButtonHidden(i);
        }

        menuFromPos.Clear();
        menuFromScale.Clear();
        menuFromAlpha.Clear();
        for (int i = 0; i < menuButtons.Count; i++)
        {
            menuFromPos.Add(menuButtons[i].anchoredPosition);
            menuFromScale.Add(menuButtons[i].localScale);
            menuFromAlpha.Add(menuButtonGroups[i] != null ? menuButtonGroups[i].alpha : 1f);
            if (menuHovers[i] != null)
                menuHovers[i].enabled = false;
        }
    }

    // 접힌 스케일을 기준으로 다시 잡지 않도록, 펼친 상태는 최초 1회만 저장한다
    void CacheMenuRestIfNeeded()
    {
        RestoreMenuButtonsRest();

        if (menuRestCached)
            return;

        if (menuLayout != null)
            menuLayout.enabled = true;
        Canvas.ForceUpdateCanvases();
        var group = menuButtons[0].parent as RectTransform;
        if (group != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(group);

        menuRestPos.Clear();
        menuRestScale.Clear();
        for (int i = 0; i < menuButtons.Count; i++)
        {
            menuRestPos.Add(menuButtons[i].anchoredPosition);
            menuRestScale.Add(Vector3.one);
            menuButtons[i].localScale = Vector3.one;
        }

        menuRestCached = true;
    }

    void RestoreMenuButtonsRest()
    {
        for (int i = 0; i < menuButtons.Count; i++)
        {
            if (i < menuRestPos.Count)
                menuButtons[i].anchoredPosition = menuRestPos[i];
            menuButtons[i].localScale = i < menuRestScale.Count ? menuRestScale[i] : Vector3.one;
            if (menuButtonGroups[i] != null)
                menuButtonGroups[i].alpha = 1f;
        }
    }

    void CollectMenuButtons()
    {
        if (menuButtons.Count > 0)
            return;

        Button[] sources = { resumeButton, soundButton, mainMenuButton, quitButton };
        RectTransform group = null;
        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] == null)
                continue;

            var rt = sources[i].transform as RectTransform;
            if (rt == null)
                continue;

            menuButtons.Add(rt);
            if (group == null)
                group = rt.parent as RectTransform;

            var cg = sources[i].GetComponent<CanvasGroup>();
            if (cg == null)
                cg = sources[i].gameObject.AddComponent<CanvasGroup>();
            menuButtonGroups.Add(cg);
            menuHovers.Add(sources[i].GetComponent<ButtonHoverScale>());
        }

        if (group != null)
            menuLayout = group.GetComponent<VerticalLayoutGroup>();
    }

    void SnapMenuButtonHidden(int i)
    {
        Vector2 rest = i < menuRestPos.Count ? menuRestPos[i] : menuButtons[i].anchoredPosition;
        Vector3 restScale = i < menuRestScale.Count ? menuRestScale[i] : menuButtons[i].localScale;
        menuButtons[i].anchoredPosition = rest + Vector2.up * buttonFoldOffset;
        menuButtons[i].localScale = new Vector3(restScale.x, restScale.y * 0.12f, restScale.z);
        if (menuButtonGroups[i] != null)
            menuButtonGroups[i].alpha = 0f;
    }

    // k=0 시작값, k=1 목표값. 열릴 때는 펼침, 닫힐 때는 위로 접힘
    void ApplyMenuButton(int i, float k, bool open)
    {
        Vector2 rest = i < menuRestPos.Count ? menuRestPos[i] : menuFromPos[i];
        Vector3 restScale = i < menuRestScale.Count ? menuRestScale[i] : menuFromScale[i];
        Vector2 hiddenPos = rest + Vector2.up * buttonFoldOffset;
        Vector3 hiddenScale = new Vector3(restScale.x, restScale.y * 0.12f, restScale.z);

        Vector2 toPos = open ? rest : hiddenPos;
        Vector3 toScale = open ? restScale : hiddenScale;
        float toAlpha = open ? 1f : 0f;

        menuButtons[i].anchoredPosition = Vector2.LerpUnclamped(menuFromPos[i], toPos, k);
        menuButtons[i].localScale = Vector3.LerpUnclamped(menuFromScale[i], toScale, k);
        if (menuButtonGroups[i] != null)
            menuButtonGroups[i].alpha = Mathf.Lerp(menuFromAlpha[i], toAlpha, Mathf.Clamp01(k));
    }

    void FinishOpenButtons()
    {
        for (int i = 0; i < menuButtons.Count; i++)
        {
            if (i < menuRestPos.Count)
                menuButtons[i].anchoredPosition = menuRestPos[i];
            if (i < menuRestScale.Count)
                menuButtons[i].localScale = menuRestScale[i];
            if (menuButtonGroups[i] != null)
                menuButtonGroups[i].alpha = 1f;
            if (menuHovers[i] != null)
            {
                menuHovers[i].enabled = true;
                if (i < menuRestScale.Count)
                    menuHovers[i].SetBaseScale(menuRestScale[i]);
            }
        }

        if (menuLayout != null)
            menuLayout.enabled = true;
    }

    void FinishClose()
    {
        isClosing = false;
        RestoreMenuButtonsRest();
        if (menuLayout != null)
            menuLayout.enabled = true;
        if (settingCanvas != null)
            settingCanvas.SetActive(false);
        Time.timeScale = 1f;
    }

    static float EaseOutBack(float k)
    {
        k = Mathf.Clamp01(k);
        const float s = 1.2f;
        k -= 1f;
        return k * k * ((s + 1f) * k + s) + 1f;
    }

    static float EaseInCubic(float k)
    {
        k = Mathf.Clamp01(k);
        return k * k * k;
    }

    bool IsConfirmVisible()
    {
        return confirmRoot != null && confirmRoot.activeSelf;
    }

    void RequestMainMenu()
    {
        ShowConfirm("메인 메뉴로 돌아갈까요?\n현재 진행은 저장되지 않습니다.", GoToMainMenu);
    }

    void RequestQuit()
    {
        ShowConfirm("게임을 종료할까요?", QuitGame);
    }

    void ShowConfirm(string message, System.Action onYes)
    {
        pendingConfirm = onYes;
        if (confirmMessage != null)
            confirmMessage.text = message;
        if (confirmRoot != null)
            confirmRoot.SetActive(true);
        else
            onYes?.Invoke();
    }

    void HideConfirm()
    {
        pendingConfirm = null;
        if (confirmRoot != null)
            confirmRoot.SetActive(false);
    }

    void ConfirmYes()
    {
        var action = pendingConfirm;
        HideConfirm();
        action?.Invoke();
    }

    // 런을 재시작해 맵으로 돌아간다
    public void RestartGame()
    {
        TryRestartRunToMap();
    }

    // 현재 씬을 재시작해 맵으로 돌아간다
    public void ReloadCurrentScene()
    {
        TryRestartRunToMap();
    }

    // 시간을 복구하고 런을 재시작해 맵으로 이동한다
    void TryRestartRunToMap()
    {
        StopAnim();
        HideConfirm();
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

    // 설정/사운드 캔버스를 닫고 메인 메뉴로 이동한다
    public void GoToMainMenu()
    {
        StopAnim();
        HideConfirm();
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

    // 설정 패널을 숨기고 사운드 설정 캔버스를 연다(일시정지 유지)
    public void OpenSoundSettings()
    {
        StopAnim();
        HideConfirm();
        soundReturnCanvas = settingCanvas;

        if (soundCanvas != null)
            soundCanvas.SetActive(true);
        if (settingCanvas != null)
            settingCanvas.SetActive(false);
    }

    // 외부에서 공용 사운드 설정을 열고 닫을 때 returnCanvas로 복귀하게 한다
    public void OpenSoundFromExternal(GameObject returnCanvas)
    {
        soundReturnCanvas = returnCanvas;

        if (soundCanvas != null)
            soundCanvas.SetActive(true);
        if (returnCanvas != null)
            returnCanvas.SetActive(false);
    }

    // 사운드 캔버스를 닫고 열었던 맥락의 캔버스로 복귀한다
    public void CloseSoundSettings()
    {
        if (soundCanvas != null)
            soundCanvas.SetActive(false);

        GameObject ret = soundReturnCanvas != null ? soundReturnCanvas : settingCanvas;
        soundReturnCanvas = null;

        if (ret == settingCanvas)
        {
            OpenSettings();
            return;
        }

        if (ret != null)
            ret.SetActive(true);
    }

    void StopAnim()
    {
        if (animRoutine != null)
        {
            StopCoroutine(animRoutine);
            animRoutine = null;
        }
        isClosing = false;
        if (menuButtons.Count > 0)
            FinishOpenButtons();
    }

    // 저장된 마스터 볼륨을 적용하고 슬라이더/숫자입력을 동기화한다
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
            masterVolumeInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            masterVolumeInput.characterLimit = 3;
            masterVolumeInput.onEndEdit.RemoveListener(OnMasterVolumeInput);
            masterVolumeInput.onEndEdit.AddListener(OnMasterVolumeInput);
        }

        SyncVolumeUI(saved);
    }

    // 마스터 볼륨(0~1)을 적용·저장하고 슬라이더/숫자 입력칸을 동기화한다
    void SetMasterVolume(float value01)
    {
        float v = Mathf.Clamp01(value01);
        AudioListener.volume = v;
        PlayerPrefs.SetFloat(MasterVolumeKey, v);
        PlayerPrefs.Save();
        SyncVolumeUI(v);
    }

    // 슬라이더와 입력칸 표시를 현재 값으로 맞춘다(콜백 미발생)
    void SyncVolumeUI(float value01)
    {
        if (masterVolumeSlider != null)
            masterVolumeSlider.SetValueWithoutNotify(value01);
        if (masterVolumeInput != null)
            masterVolumeInput.SetTextWithoutNotify(Mathf.RoundToInt(value01 * 100f).ToString());
    }

    // 마스터 볼륨 슬라이더 변경 시 전역 볼륨을 적용·저장한다
    void OnMasterVolumeChanged(float value)
    {
        SetMasterVolume(value);
    }

    // 숫자 입력칸 값 확정 시 0~100을 볼륨으로 적용한다
    void OnMasterVolumeInput(string text)
    {
        if (int.TryParse(text, out int percent))
            SetMasterVolume(Mathf.Clamp(percent, 0, 100) / 100f);
        else
            SyncVolumeUI(AudioListener.volume);
    }

    // 슬라이더가 연결된 채널만 저장값 적용 후 콜백을 연결한다
    void InitVolumeChannels()
    {
        if (volumeChannels == null) return;

        foreach (var ch in volumeChannels)
        {
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

            SetChannelVolume(ch, saved);
        }
    }

    // 채널 숫자 입력 확정 시 0~100을 파싱해 적용한다
    void OnChannelInput(VolumeChannel ch, string text)
    {
        if (int.TryParse(text, out int percent))
            SetChannelVolume(ch, Mathf.Clamp(percent, 0, 100) / 100f);
        else
            SyncChannelUI(ch, Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKeyPrefix + ch.key, 1f)));
    }

    // 채널 볼륨(0~1)을 적용·저장하고 믹서와 UI를 동기화한다
    void SetChannelVolume(VolumeChannel ch, float value01)
    {
        if (ch == null || string.IsNullOrEmpty(ch.key)) return;

        float v = Mathf.Clamp01(value01);
        PlayerPrefs.SetFloat(VolumeKeyPrefix + ch.key, v);
        PlayerPrefs.Save();

        if (audioMixer != null && !string.IsNullOrEmpty(ch.exposedParameter))
        {
            float dB = v <= 0.0001f ? -80f : Mathf.Log10(v) * 20f;
            audioMixer.SetFloat(ch.exposedParameter, dB);
        }

        SyncChannelUI(ch, v);
    }

    // 채널 슬라이더/입력칸 표시를 현재 값으로 맞춘다(콜백 미발생)
    void SyncChannelUI(VolumeChannel ch, float value01)
    {
        if (ch.slider != null) ch.slider.SetValueWithoutNotify(value01);
        if (ch.input != null) ch.input.SetTextWithoutNotify(Mathf.RoundToInt(value01 * 100f).ToString());
    }

    // 게임을 종료한다(에디터에서는 플레이 중지)
    public void QuitGame()
    {
        StopAnim();
        HideConfirm();
        Debug.Log("게임 종료");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
