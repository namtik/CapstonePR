using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 메뉴바 TopBar1 재화 표시. 장식 바 위에 아이콘과 보유량을 함께 표시한다.
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
    [SerializeField] private float fontSize = 22f;
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

    public Image IconImage => moneyIconImage;

    public Sprite IconSprite
    {
        get
        {
            if (moneyIconImage != null && moneyIconImage.sprite != null)
                return moneyIconImage.sprite;
            return MoneyManager.Instance != null ? MoneyManager.Instance.MoneyIcon : null;
        }
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

    // 영혼이 도착할 때 재화 표시를 잠깐 키운다.
    public void PlayReceivePulse()
    {
        if (!isActiveAndEnabled) return;
        if (_pulseCo != null) StopCoroutine(_pulseCo);
        _pulseCo = StartCoroutine(ReceivePulse());
    }

    public Vector2 ScreenCenter()
    {
        var rt = transform as RectTransform;
        if (rt == null) return new Vector2(Screen.width - 80f, Screen.height - 36f);

        Canvas canvas = GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        return RectTransformUtility.WorldToScreenPoint(cam, (corners[0] + corners[2]) * 0.5f);
    }

    System.Collections.IEnumerator ReceivePulse()
    {
        Vector3 from = transform.localScale;
        Vector3 peak = from * 1.08f;
        float t = 0f;
        while (t < 0.08f)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(from, peak, t / 0.08f);
            yield return null;
        }
        t = 0f;
        while (t < 0.14f)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(peak, from, t / 0.14f);
            yield return null;
        }
        transform.localScale = from;
        _pulseCo = null;
    }

    Coroutine _pulseCo;
}
