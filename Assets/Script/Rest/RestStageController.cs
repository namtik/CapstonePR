using UnityEngine;
using UnityEngine.UI;

public class RestStageController : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button button1;
    [SerializeField] private Button button2;

    [Header("Hover")]
    [SerializeField] private bool enableButtonHoverScale = true;
    [SerializeField] private float hoverScale = 1.06f;
    [SerializeField] private float pressedScale = 1.0f;
    [SerializeField] private float hoverLerpSpeed = 14f;

    private RestRoundData currentData;
    private RoundManager currentRoundManager;

    private void Awake()
    {
        EnsureButtonsBound();
        EnsureHoverEffects();
        RegisterButtonCallbacks();
    }

    private void OnDestroy()
    {
        UnregisterButtonCallbacks();
    }

    public void BeginRest(RestRoundData data, RoundManager roundmanager)
    {
        currentData = data;
        currentRoundManager = roundmanager;

        EnsureButtonsBound();
        EnsureHoverEffects();
        RegisterButtonCallbacks();
    }

    private void OnButton1Clicked()
    {
        if (currentRoundManager == null)
        {
            Debug.LogWarning("[RestStage] RoundManager 참조가 없어 버튼1 처리를 건너뜁니다.");
            return;
        }

        float healPercent = currentData != null ? currentData.healPercent : 0.2f;
        currentRoundManager.HealPlayer(healPercent);
        Debug.Log($"[RestStage] 버튼1 선택: 최대 체력 {healPercent * 100f}% 회복");
    }

    private void OnButton2Clicked()
    {
        Debug.Log("[RestStage] 버튼2는 아직 기획 중입니다.");
    }

    private void EnsureButtonsBound()
    {
        if (button1 != null && button2 != null)
            return;

        var buttons = GetComponentsInChildren<Button>(true);
        if (buttons == null || buttons.Length == 0)
            return;

        if (button1 == null)
            button1 = FindButtonByName(buttons, "Button");
        if (button2 == null)
            button2 = FindButtonByName(buttons, "Button (1)");

        if (button1 == null && buttons.Length > 0)
            button1 = buttons[0];
        if (button2 == null && buttons.Length > 1)
            button2 = buttons[1];
    }

    private static Button FindButtonByName(Button[] buttons, string name)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && buttons[i].name == name)
                return buttons[i];
        }

        return null;
    }

    private void EnsureHoverEffects()
    {
        if (!enableButtonHoverScale)
            return;

        ConfigureHover(button1);
        ConfigureHover(button2);
    }

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

    private void UnregisterButtonCallbacks()
    {
        if (button1 != null)
            button1.onClick.RemoveListener(OnButton1Clicked);

        if (button2 != null)
            button2.onClick.RemoveListener(OnButton2Clicked);
    }
}
