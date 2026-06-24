using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 메뉴바 중앙 상단에 현재 스테이지(바퀴-스테이지)를 n-n 형식으로 표시한다.
public class RunStageDisplayUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TMP_Text stageText;

    [Header("Background")]
    [Tooltip("배경 스프라이트. 지정하면 backgroundImage에 자동 적용.")]
    [SerializeField] private Sprite backgroundSprite;

    [Header("Text Style (Inspector)")]
    [SerializeField] private TMP_FontAsset fontAsset;
    [SerializeField] private float fontSize = 40f;
    [SerializeField] private Color textColor = Color.white;
    [Tooltip("표시 형식. {0}=바퀴, {1}=스테이지. 기본 \"1-3\"")]
    [SerializeField] private string displayFormat = "{0}-{1}";

    void Awake()
    {
        ResolveReferences();
        ApplyBackgroundSprite();
    }

    void OnEnable()
    {
        ResolveReferences();
        ApplyBackgroundSprite();
        ApplyTextStyle();
        RefreshDisplay();

        if (GameStateController.Instance != null)
            GameStateController.Instance.OnRunStageDisplayChanged += RefreshDisplay;
    }

    void OnDisable()
    {
        if (GameStateController.Instance != null)
            GameStateController.Instance.OnRunStageDisplayChanged -= RefreshDisplay;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        ApplyBackgroundSprite();
        ApplyTextStyle();
    }
#endif

    void ResolveReferences()
    {
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        if (stageText == null)
        {
            Transform textTransform = transform.Find("StageText");
            if (textTransform != null)
                stageText = textTransform.GetComponent<TMP_Text>();
        }
    }

    void ApplyBackgroundSprite()
    {
        if (backgroundImage == null || backgroundSprite == null)
            return;

        backgroundImage.sprite = backgroundSprite;
        backgroundImage.type = Image.Type.Simple;
        backgroundImage.preserveAspect = true;
    }

    void ApplyTextStyle()
    {
        if (stageText == null)
            return;

        if (fontAsset != null)
            stageText.font = fontAsset;

        stageText.fontSize = fontSize;
        stageText.color = textColor;
    }

    void RefreshDisplay()
    {
        if (stageText == null)
            return;

        GameStateController state = GameStateController.Instance;
        if (state == null)
        {
            stageText.text = string.Format(displayFormat, 1, 1);
            return;
        }

        stageText.text = string.Format(
            displayFormat,
            state.CurrentDisplayLap,
            state.CurrentDisplayStage);
    }
}
