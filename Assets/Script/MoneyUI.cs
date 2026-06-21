using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 메뉴바 재화 표시. pill 배경 위에 아이콘 + 숫자를 함께 표시한다.
public class MoneyUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image moneyIconImage;
    [SerializeField] private TMP_Text moneyText;

    [Header("Background")]
    [Tooltip("배경 스프라이트. 지정하면 backgroundImage에 자동 적용.")]
    [SerializeField] private Sprite backgroundSprite;

    [Header("Text Style (Inspector)")]
    [SerializeField] private TMP_FontAsset fontAsset;
    [SerializeField] private float fontSize = 36f;
    [SerializeField] private Color textColor = Color.white;

    void Awake()
    {
        ResolveReferences();
        ApplyBackgroundSprite();
        ApplyMoneyIcon();
    }

    void OnEnable()
    {
        ResolveReferences();
        ApplyBackgroundSprite();
        ApplyMoneyIcon();
        ApplyTextStyle();

        if (MoneyManager.Instance != null)
        {
            MoneyManager.Instance.OnMoneyChanged += UpdateMoneyDisplay;
            UpdateMoneyDisplay(MoneyManager.Instance.CurrentMoney);
        }
    }

    void OnDisable()
    {
        if (MoneyManager.Instance != null)
            MoneyManager.Instance.OnMoneyChanged -= UpdateMoneyDisplay;
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

        if (moneyIconImage == null)
        {
            Transform iconTransform = transform.Find("MoneyIcon");
            if (iconTransform != null)
                moneyIconImage = iconTransform.GetComponent<Image>();
        }

        if (moneyText == null)
        {
            Transform textTransform = transform.Find("MoneyAmountText");
            if (textTransform != null)
                moneyText = textTransform.GetComponent<TMP_Text>();
        }
    }

    void ApplyBackgroundSprite()
    {
        if (backgroundImage == null || backgroundSprite == null)
            return;

        backgroundImage.sprite = backgroundSprite;
        backgroundImage.type = Image.Type.Simple;
        backgroundImage.preserveAspect = false;
        backgroundImage.raycastTarget = false;
    }

    void ApplyMoneyIcon()
    {
        if (moneyIconImage == null || MoneyManager.Instance == null || MoneyManager.Instance.MoneyIcon == null)
            return;

        moneyIconImage.sprite = MoneyManager.Instance.MoneyIcon;
        moneyIconImage.preserveAspect = true;
        moneyIconImage.raycastTarget = false;
    }

    void ApplyTextStyle()
    {
        if (moneyText == null)
            return;

        if (fontAsset != null)
            moneyText.font = fontAsset;

        moneyText.fontSize = fontSize;
        moneyText.color = textColor;
    }

    void UpdateMoneyDisplay(int money)
    {
        if (moneyText != null)
            moneyText.text = money.ToString();
    }
}
