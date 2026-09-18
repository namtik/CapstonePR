using Battle;
using UnityEngine;
using UnityEngine.UI;

public class RestStageController : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button button1;   // 명상(회복)
    [SerializeField] private Button button2;   // 깨달음(콤보 순서 변경)

    [Header("깨달음")]
    [SerializeField] private RestEnlightenmentPanel enlightenmentPanel; // 보유 콤보 목록/재배열 패널

    [Header("Hover")]
    [SerializeField] private bool enableButtonHoverScale = true;   // 버튼 호버 스케일 사용 여부
    [SerializeField] private float hoverScale = 1.06f;   // 호버 시 확대 배율
    [SerializeField] private float pressedScale = 1.0f;   // 눌렀을 때 배율
    [SerializeField] private float hoverLerpSpeed = 14f;   // 스케일 보간 속도

    private RestRoundData currentData;   // 현재 휴식 라운드 데이터
    private RoundManager currentRoundManager;   // 라운드 관리자 참조
    private bool choiceLocked;   // 명상/깨달음 확정 후 추가 입력 차단

    // 버튼 바인딩과 호버 효과, 콜백을 설정한다
    private void Awake()
    {
        EnsureButtonsBound();
        EnsureHoverEffects();
        RegisterButtonCallbacks();
        BindEnlightenmentPanel();
        ShowChoiceView();
    }

    // 파괴 시 버튼 콜백을 해제한다
    private void OnDestroy()
    {
        UnregisterButtonCallbacks();
        UnbindEnlightenmentPanel();
    }

    // 휴식 라운드를 시작하며 데이터와 버튼을 초기화한다
    public void BeginRest(RestRoundData data, RoundManager roundmanager)
    {
        currentData = data;
        currentRoundManager = roundmanager;
        choiceLocked = false;

        EnsureButtonsBound();
        EnsureHoverEffects();
        RegisterButtonCallbacks();
        BindEnlightenmentPanel();
        ShowChoiceView();
        RefreshEnlightenmentInteractable();
    }

    // 명상: 최대 체력 비율만큼 회복하고 맵으로 돌아간다
    private void OnButton1Clicked()
    {
        if (choiceLocked)
            return;

        if (currentRoundManager == null)
        {
            Debug.LogWarning("[RestStage] RoundManager 참조가 없어 버튼1 처리를 건너뜁니다.");
            return;
        }

        choiceLocked = true;
        SetChoiceButtonsInteractable(false);

        float healPercent = currentData != null ? currentData.healPercent : 0.2f;
        currentRoundManager.HealPlayer(healPercent);
        Debug.Log($"[RestStage] 명상 선택: 최대 체력 {healPercent * 100f}% 회복");
    }

    // 깨달음: 보유 콤보 목록을 연다. 확정 전에는 뒤로 가 명상을 다시 고를 수 있다
    private void OnButton2Clicked()
    {
        if (choiceLocked)
            return;

        if (!HasOwnedCombos())
            return;

        SetChoiceButtonsVisible(false);
        if (enlightenmentPanel != null)
            enlightenmentPanel.Open();
    }

    void OnEnlightenmentBack()
    {
        if (choiceLocked)
            return;

        ShowChoiceView();
        RefreshEnlightenmentInteractable();
    }

    void OnEnlightenmentConfirmed()
    {
        if (currentRoundManager == null)
        {
            Debug.LogWarning("[RestStage] RoundManager 참조가 없어 깨달음 종료를 건너뜁니다.");
            ShowChoiceView();
            return;
        }

        choiceLocked = true;
        SetChoiceButtonsInteractable(false);
        currentRoundManager.ReturnToMap();
        Debug.Log("[RestStage] 깨달음 확정 — 맵으로 복귀");
    }

    void ShowChoiceView()
    {
        if (enlightenmentPanel != null)
            enlightenmentPanel.Hide();

        SetChoiceButtonsVisible(true);
        SetChoiceButtonsInteractable(!choiceLocked);
    }

    void RefreshEnlightenmentInteractable()
    {
        if (button2 == null)
            return;

        button2.interactable = !choiceLocked && HasOwnedCombos();
    }

    static bool HasOwnedCombos()
    {
        var nb = NewBattleController.Instance;
        if (nb == null || nb.OwnedComboSkills == null)
            return false;

        for (int i = 0; i < nb.OwnedComboSkills.Count; i++)
        {
            if (nb.OwnedComboSkills[i] != null)
                return true;
        }

        return false;
    }

    void SetChoiceButtonsVisible(bool visible)
    {
        if (button1 != null)
            button1.gameObject.SetActive(visible);
        if (button2 != null)
            button2.gameObject.SetActive(visible);
    }

    void SetChoiceButtonsInteractable(bool interactable)
    {
        if (button1 != null)
            button1.interactable = interactable;
        if (button2 != null)
            button2.interactable = interactable && HasOwnedCombos();
    }

    void BindEnlightenmentPanel()
    {
        if (enlightenmentPanel == null)
            enlightenmentPanel = GetComponentInChildren<RestEnlightenmentPanel>(true);

        if (enlightenmentPanel == null)
            return;

        enlightenmentPanel.OnBackToChoice -= OnEnlightenmentBack;
        enlightenmentPanel.OnConfirmed -= OnEnlightenmentConfirmed;
        enlightenmentPanel.OnBackToChoice += OnEnlightenmentBack;
        enlightenmentPanel.OnConfirmed += OnEnlightenmentConfirmed;
    }

    void UnbindEnlightenmentPanel()
    {
        if (enlightenmentPanel == null)
            return;

        enlightenmentPanel.OnBackToChoice -= OnEnlightenmentBack;
        enlightenmentPanel.OnConfirmed -= OnEnlightenmentConfirmed;
    }

    // 인스펙터에 비어 있는 버튼 참조를 자식에서 찾아 채운다
    private void EnsureButtonsBound()
    {
        if (button1 != null && button2 != null)
            return;

        var buttons = GetComponentsInChildren<Button>(true);
        if (buttons == null || buttons.Length == 0)
            return;

        if (button1 == null)
            button1 = FindButtonByName(buttons, "HealButton") ?? FindButtonByName(buttons, "Button");
        if (button2 == null)
            button2 = FindButtonByName(buttons, "Button (1)");

        if (button1 == null && buttons.Length > 0)
            button1 = buttons[0];
        if (button2 == null && buttons.Length > 1)
            button2 = buttons[1];
    }

    // 버튼 배열에서 이름이 일치하는 버튼을 찾아 반환한다
    private static Button FindButtonByName(Button[] buttons, string name)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && buttons[i].name == name)
                return buttons[i];
        }

        return null;
    }

    // 호버 스케일이 켜져 있으면 각 버튼에 호버 효과를 설정한다
    private void EnsureHoverEffects()
    {
        if (!enableButtonHoverScale)
            return;

        ConfigureHover(button1);
        ConfigureHover(button2);
    }

    // 버튼에 ButtonHoverScale을 보장하고 스케일 값을 적용한다
    private void ConfigureHover(Button button)
    {
        if (button == null)
            return;

        ButtonHoverScale hover = button.GetComponent<ButtonHoverScale>();
        if (hover == null)
            hover = button.gameObject.AddComponent<ButtonHoverScale>();

        hover.enabled = true;
        hover.Configure(hoverScale, pressedScale, hoverLerpSpeed);
    }

    // 버튼 클릭 콜백을 중복 없이 등록한다
    private void RegisterButtonCallbacks()
    {
        if (button1 != null)
        {
            button1.onClick.RemoveListener(OnButton1Clicked);
            button1.onClick.AddListener(OnButton1Clicked);
        }

        if (button2 != null)
        {
            button2.onClick.RemoveListener(OnButton2Clicked);
            button2.onClick.AddListener(OnButton2Clicked);
        }
    }

    // 버튼 클릭 콜백을 모두 해제한다
    private void UnregisterButtonCallbacks()
    {
        if (button1 != null)
            button1.onClick.RemoveListener(OnButton1Clicked);

        if (button2 != null)
            button2.onClick.RemoveListener(OnButton2Clicked);
    }
}
