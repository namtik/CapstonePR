using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class EnemyView : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Slider hpBar;
    [SerializeField] private Slider actionGaugeBar;
    [SerializeField] private TMP_Text damageText;
    [SerializeField] private Image enemyImage;

    [Header("공격 예정 표시")]
    [SerializeField] private TMP_Text attackPreviewText;

    [Header("행동 게이지 중앙 눈금")]
    [SerializeField] private bool showMidGaugeMarker = true;
    [SerializeField] private float midGaugeRatio = 0.5f;
    [SerializeField] private Vector2 midGaugeMarkerSize = new Vector2(4f, 28f);
    [SerializeField] private Color midGaugeMarkerColor = new Color(1f, 0.95f, 0.45f, 1f);
    [SerializeField] private string midGaugeMarkerLabel = "MID 5";
    [SerializeField] private int midGaugeMarkerLabelFontSize = 12;
    [SerializeField] private Color midGaugeMarkerLabelColor = new Color(1f, 0.95f, 0.45f, 1f);
    [SerializeField] private Vector2 midGaugeMarkerLabelOffset = new Vector2(0f, 16f);

    [Header("패턴 발동 알림")]
    [SerializeField] private bool showPatternNotice = true;
    [SerializeField] private Vector2 patternNoticeSize = new Vector2(360f, 36f);
    [SerializeField] private float patternNoticeBelowIntentGap = 28f;
    [SerializeField] private float patternNoticeDuration = 1.6f;
    [SerializeField] private Color patternNoticeTextColor = new Color(1f, 0.93f, 0.52f, 1f);
    [SerializeField] private Color patternNoticeBackgroundColor = new Color(0f, 0f, 0f, 0.72f);
    [SerializeField] private int patternNoticeFontSize = 20;

    [Header("게이지 수치 표시")]
    [SerializeField] private bool showGaugeValues = true;
    [SerializeField] private int gaugeValueFontSize = 16;
    [SerializeField] private Color gaugeValueTextColor = Color.white;

    [Header("데미지 연출 설정")]
    [SerializeField] private float fadeTime = 1f;
    [SerializeField] private float floatSpeed = 0.5f;

    [Header("UI 위치 보정")]
    [SerializeField] private float uiVerticalOffset = 76f;
    [SerializeField] private Vector2 hpBarSize = new Vector2(760f, 30f);
    [SerializeField] private Vector2 actionGaugeSize = new Vector2(760f, 24f);
    [SerializeField] private float minimumBarWidth = 760f;
    [SerializeField] private float hpToGaugeGap = 28f;
    [SerializeField] private Vector2 attackPreviewSize = new Vector2(120f, 30f);
    [SerializeField] private Vector2 attackPreviewHeadOffset = new Vector2(0f, 36f);

    [Header("게이지 스타일")]
    [SerializeField] private Color hpBackgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.92f);
    [SerializeField] private Color hpFillColor = new Color(0.82f, 0.16f, 0.16f, 1f);
    [SerializeField] private Color actionBackgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.92f);
    [SerializeField] private Color actionFillColor = new Color(0.94f, 0.72f, 0.12f, 1f);
    [SerializeField] private bool useGaugeFrame = false;
    [SerializeField] private Color hpFrameColor = new Color(0.28f, 0.12f, 0.08f, 1f);
    [SerializeField] private Color actionFrameColor = new Color(0.35f, 0.26f, 0.08f, 1f);
    [SerializeField] private float fillInset = 3f;

    [Header("피격 이펙트 (파티클)")]
    public ParticleSystem qEffect;
    public ParticleSystem wEffect;
    public ParticleSystem eEffect;
    public ParticleSystem rEffect;

    private EnemyStat stat;
    private Vector3 damageTextOriginLocalPos;
    private Color damageTextOriginColor;
    private Coroutine damageCoroutine;
    private static Sprite solidRectSprite;
    private TMP_Text hpValueText;
    private TMP_Text actionGaugeValueText;
    private TMP_Text patternNoticeText;
    private Image patternNoticeBackground;
    private Coroutine patternNoticeCoroutine;

    static Sprite GetSolidRectSprite()
    {
        if (solidRectSprite == null)
        {
            // A generated white sprite avoids scaling artifacts from Unity's default sliced slider sprites.
            solidRectSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            solidRectSprite.name = "EnemyView_SolidRectSprite";
        }

        return solidRectSprite;
    }

    void ConfigureGaugeStyle(Slider slider, Color backgroundColor, Color fillColor, Color frameColor)
    {
        if (slider == null)
            return;

        // Hiding handles enforces a static rectangular gauge look instead of a draggable slider control.
        if (slider.handleRect != null)
            slider.handleRect.gameObject.SetActive(false);

        slider.interactable = false;
        slider.direction = Slider.Direction.LeftToRight;
        slider.transition = Selectable.Transition.None;

        Transform handleArea = slider.transform.Find("Handle Slide Area");
        if (handleArea != null)
            handleArea.gameObject.SetActive(false);

        Transform backgroundTransform = slider.transform.Find("Background");
        if (backgroundTransform != null)
        {
            RectTransform backgroundRect = backgroundTransform as RectTransform;
            if (backgroundRect != null)
            {
                backgroundRect.anchorMin = Vector2.zero;
                backgroundRect.anchorMax = Vector2.one;
                backgroundRect.offsetMin = Vector2.zero;
                backgroundRect.offsetMax = Vector2.zero;
            }

            Image backgroundImage = backgroundTransform.GetComponent<Image>();
            if (backgroundImage != null)
            {
                backgroundImage.sprite = GetSolidRectSprite();
                backgroundImage.type = Image.Type.Simple;
                backgroundImage.color = backgroundColor;
                backgroundImage.raycastTarget = false;
            }

            Outline frameOutline = backgroundTransform.GetComponent<Outline>();
            if (useGaugeFrame)
            {
                if (frameOutline == null)
                    frameOutline = backgroundTransform.gameObject.AddComponent<Outline>();

                frameOutline.effectColor = frameColor;
                frameOutline.effectDistance = new Vector2(1f, -1f);
            }
            else if (frameOutline != null)
            {
                Destroy(frameOutline);
            }
        }

        Transform fillAreaTransform = slider.transform.Find("Fill Area");
        if (fillAreaTransform != null)
        {
            RectTransform fillAreaRect = fillAreaTransform as RectTransform;
            if (fillAreaRect != null)
            {
                fillAreaRect.anchorMin = Vector2.zero;
                fillAreaRect.anchorMax = Vector2.one;
                fillAreaRect.offsetMin = new Vector2(fillInset, fillInset);
                fillAreaRect.offsetMax = new Vector2(-fillInset, -fillInset);
            }
        }

        if (slider.fillRect != null)
        {
            slider.fillRect.anchorMin = Vector2.zero;
            slider.fillRect.anchorMax = Vector2.one;
            slider.fillRect.offsetMin = Vector2.zero;
            slider.fillRect.offsetMax = Vector2.zero;

            Image fillImage = slider.fillRect.GetComponent<Image>();
            if (fillImage != null)
            {
                fillImage.sprite = GetSolidRectSprite();
                fillImage.type = Image.Type.Simple;
                fillImage.color = fillColor;
                fillImage.raycastTarget = false;
            }
        }

        EnsureGaugeValueText(slider);

        if (slider == actionGaugeBar)
            EnsureMidGaugeMarker(slider);
    }

    void EnsureMidGaugeMarker(Slider slider)
    {
        if (slider == null)
            return;

        Transform markerTransform = slider.transform.Find("MidGaugeMarker");
        if (markerTransform == null)
        {
            GameObject markerObject = new GameObject("MidGaugeMarker");
            markerObject.transform.SetParent(slider.transform, false);
            markerTransform = markerObject.transform;

            RectTransform markerRect = markerObject.AddComponent<RectTransform>();
            markerRect.anchorMin = new Vector2(midGaugeRatio, 0.5f);
            markerRect.anchorMax = new Vector2(midGaugeRatio, 0.5f);
            markerRect.pivot = new Vector2(0.5f, 0.5f);
            markerRect.anchoredPosition = Vector2.zero;
            markerRect.sizeDelta = midGaugeMarkerSize;

            Image markerImage = markerObject.AddComponent<Image>();
            markerImage.sprite = GetSolidRectSprite();
            markerImage.type = Image.Type.Simple;
            markerImage.color = midGaugeMarkerColor;
            markerImage.raycastTarget = false;

            GameObject labelObject = new GameObject("MidGaugeLabel");
            labelObject.transform.SetParent(markerObject.transform, false);
            RectTransform labelRect = labelObject.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.5f, 1f);
            labelRect.anchorMax = new Vector2(0.5f, 1f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.anchoredPosition = midGaugeMarkerLabelOffset;
            labelRect.sizeDelta = new Vector2(90f, 20f);

            TMP_Text labelText = labelObject.AddComponent<TextMeshProUGUI>();
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.fontSize = midGaugeMarkerLabelFontSize;
            labelText.color = midGaugeMarkerLabelColor;
            labelText.textWrappingMode = TextWrappingModes.NoWrap;
            labelText.raycastTarget = false;
            labelText.text = midGaugeMarkerLabel;
        }

        if (markerTransform == null)
            return;

        RectTransform existingMarkerRect = markerTransform as RectTransform;
        if (existingMarkerRect != null)
        {
            existingMarkerRect.anchorMin = new Vector2(midGaugeRatio, 0.5f);
            existingMarkerRect.anchorMax = new Vector2(midGaugeRatio, 0.5f);
            existingMarkerRect.pivot = new Vector2(0.5f, 0.5f);
            existingMarkerRect.sizeDelta = midGaugeMarkerSize;
            existingMarkerRect.anchoredPosition = Vector2.zero;
        }

        Image existingMarkerImage = markerTransform.GetComponent<Image>();
        if (existingMarkerImage != null)
            existingMarkerImage.color = midGaugeMarkerColor;

        Transform label = markerTransform.Find("MidGaugeLabel");
        if (label != null)
        {
            RectTransform labelRect = label as RectTransform;
            if (labelRect != null)
                labelRect.anchoredPosition = midGaugeMarkerLabelOffset;

            TMP_Text labelText = label.GetComponent<TMP_Text>();
            if (labelText != null)
            {
                labelText.fontSize = midGaugeMarkerLabelFontSize;
                labelText.color = midGaugeMarkerLabelColor;
                labelText.text = midGaugeMarkerLabel;
            }
        }

        markerTransform.gameObject.SetActive(showMidGaugeMarker);
    }

    void EnsurePatternNotice()
    {
        Transform existing = transform.Find("PatternNotice");
        if (existing == null)
        {
            GameObject noticeObject = new GameObject("PatternNotice");
            noticeObject.transform.SetParent(transform, false);

            RectTransform noticeRect = noticeObject.AddComponent<RectTransform>();
            noticeRect.anchorMin = new Vector2(0.5f, 0.5f);
            noticeRect.anchorMax = new Vector2(0.5f, 0.5f);
            noticeRect.pivot = new Vector2(0.5f, 0.5f);
            noticeRect.sizeDelta = patternNoticeSize;

            patternNoticeBackground = noticeObject.AddComponent<Image>();
            patternNoticeBackground.sprite = GetSolidRectSprite();
            patternNoticeBackground.type = Image.Type.Simple;
            patternNoticeBackground.color = patternNoticeBackgroundColor;
            patternNoticeBackground.raycastTarget = false;

            GameObject textObject = new GameObject("PatternNoticeText");
            textObject.transform.SetParent(noticeObject.transform, false);
            RectTransform textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 4f);
            textRect.offsetMax = new Vector2(-8f, -4f);

            patternNoticeText = textObject.AddComponent<TextMeshProUGUI>();
            patternNoticeText.alignment = TextAlignmentOptions.Center;
            patternNoticeText.textWrappingMode = TextWrappingModes.NoWrap;
            patternNoticeText.raycastTarget = false;
        }
        else
        {
            patternNoticeBackground = existing.GetComponent<Image>();
            Transform textTransform = existing.Find("PatternNoticeText");
            if (textTransform != null)
                patternNoticeText = textTransform.GetComponent<TextMeshProUGUI>();
        }

        if (patternNoticeText != null)
        {
            patternNoticeText.fontSize = patternNoticeFontSize;
            patternNoticeText.color = patternNoticeTextColor;
            patternNoticeText.text = "";
        }

        if (patternNoticeBackground != null)
            patternNoticeBackground.color = patternNoticeBackgroundColor;

        Transform noticeTransform = existing ?? transform.Find("PatternNotice");
        if (noticeTransform != null)
            noticeTransform.gameObject.SetActive(false);
    }

    void EnsureGaugeValueText(Slider slider)
    {
        if (slider == null)
            return;

        string textObjectName = slider == hpBar ? "HpValueText" : "GaugeValueText";
        Transform existing = slider.transform.Find(textObjectName);
        TMP_Text targetText;

        if (existing == null)
        {
            GameObject textObject = new GameObject(textObjectName);
            textObject.transform.SetParent(slider.transform, false);

            RectTransform textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            targetText = textObject.AddComponent<TextMeshProUGUI>();
        }
        else
        {
            targetText = existing.GetComponent<TMP_Text>();
            if (targetText == null)
                targetText = existing.gameObject.AddComponent<TextMeshProUGUI>();
        }

        targetText.alignment = TextAlignmentOptions.Center;
        targetText.fontSize = gaugeValueFontSize;
        targetText.color = gaugeValueTextColor;
        targetText.raycastTarget = false;
        targetText.textWrappingMode = TextWrappingModes.NoWrap;
        targetText.overflowMode = TextOverflowModes.Overflow;
        targetText.gameObject.SetActive(showGaugeValues);

        if (slider == hpBar)
            hpValueText = targetText;
        else if (slider == actionGaugeBar)
            actionGaugeValueText = targetText;
    }

    void UpdateGaugeValueTextStyles()
    {
        ApplyGaugeValueTextStyle(hpValueText);
        ApplyGaugeValueTextStyle(actionGaugeValueText);
    }

    void ApplyGaugeValueTextStyle(TMP_Text text)
    {
        if (text == null)
            return;

        text.fontSize = gaugeValueFontSize;
        text.color = gaugeValueTextColor;
        text.gameObject.SetActive(showGaugeValues);
    }

    void LayoutUiBelowEnemy()
    {
        RectTransform enemyRect = enemyImage != null ? enemyImage.rectTransform : GetComponent<RectTransform>();
        if (enemyRect == null) return;

        float enemyHeight = enemyRect.rect.height;
        if (enemyHeight <= 0f)
            enemyHeight = 360f;

        float resolvedHpWidth = Mathf.Max(minimumBarWidth, hpBarSize.x);
        float resolvedGaugeWidth = Mathf.Max(minimumBarWidth, actionGaugeSize.x);

        Vector2 resolvedHpBarSize = new Vector2(resolvedHpWidth, hpBarSize.y);
        Vector2 resolvedGaugeSize = new Vector2(resolvedGaugeWidth, actionGaugeSize.y);

        // Move enemy UI upward to avoid overlap with bottom combo slots.
        float baseY = -(enemyHeight * 0.5f) - 24f + uiVerticalOffset;

        PositionUiRect(hpBar != null ? hpBar.GetComponent<RectTransform>() : null, baseY, resolvedHpBarSize);
        PositionUiRect(actionGaugeBar != null ? actionGaugeBar.GetComponent<RectTransform>() : null, baseY - hpToGaugeGap, resolvedGaugeSize);

        RectTransform previewRect = attackPreviewText != null ? attackPreviewText.rectTransform : null;
        if (previewRect != null)
        {
            float headY = (enemyHeight * 0.5f) + attackPreviewHeadOffset.y;

            previewRect.anchorMin = new Vector2(0.5f, 0.5f);
            previewRect.anchorMax = new Vector2(0.5f, 0.5f);
            previewRect.pivot = new Vector2(0.5f, 0.5f);
            previewRect.anchoredPosition = new Vector2(attackPreviewHeadOffset.x, headY);
            previewRect.sizeDelta = attackPreviewSize;
        }

        RectTransform noticeRect = patternNoticeBackground != null ? patternNoticeBackground.rectTransform : null;
        if (noticeRect != null)
        {
            float previewY = previewRect != null ? previewRect.anchoredPosition.y : (enemyHeight * 0.5f) + attackPreviewHeadOffset.y;
            float noticeY = previewY - patternNoticeBelowIntentGap;

            noticeRect.anchorMin = new Vector2(0.5f, 0.5f);
            noticeRect.anchorMax = new Vector2(0.5f, 0.5f);
            noticeRect.pivot = new Vector2(0.5f, 0.5f);
            noticeRect.anchoredPosition = new Vector2(attackPreviewHeadOffset.x, noticeY);
            noticeRect.sizeDelta = patternNoticeSize;
        }
    }

    void PositionUiRect(RectTransform rect, float anchoredY, Vector2 size)
    {
        if (rect == null) return;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, anchoredY);
        rect.sizeDelta = size;
    }

    void Awake()
    {
        stat = GetComponent<EnemyStat>();
        stat.OnHpChanged += UpdateHpBar;
        stat.OnAttackCountChanged += UpdateAttackPreview;

        ConfigureGaugeStyle(hpBar, hpBackgroundColor, hpFillColor, hpFrameColor);
        ConfigureGaugeStyle(actionGaugeBar, actionBackgroundColor, actionFillColor, actionFrameColor);
        UpdateGaugeValueTextStyles();
        EnsurePatternNotice();

        Canvas canvas = GetComponentInChildren<Canvas>();
        if (canvas != null)
        {
            canvas.worldCamera = Camera.main;  // 카메라 연결
            canvas.sortingOrder = 10;          // Enemy 이미지보다 앞
        }

        LayoutUiBelowEnemy();
    }

    void Start()
    {
        if (damageText != null)
        {
            damageTextOriginLocalPos = damageText.transform.localPosition;
            damageTextOriginColor = damageText.color;

            if (damageTextOriginColor.a == 0f)
            {
                damageTextOriginColor = new Color(1f, 1f, 1f, 1f);
                damageText.color = damageTextOriginColor;
            }
            damageText.text = "";
        }

        // EnemyStat 이벤트 구독
        stat.OnHpChanged += UpdateHpBar;

        // Action gauge bar: reset to 0 (updated via EnemyController → stat.OnGaugeStepChanged)
        if (actionGaugeBar != null) actionGaugeBar.value = 0f;

        UpdateHpValueText(stat != null ? stat.currentHp : 0f, stat != null ? stat.maxHp : 0f);
        UpdateActionGaugeValueText();

        LayoutUiBelowEnemy();
    }

    void OnDestroy()
    {
        stat.OnHpChanged -= UpdateHpBar;
        stat.OnAttackCountChanged -= UpdateAttackPreview;
        delDamageText();
    }

    // EnemyStat HP 변경 이벤트 핸들러

    void UpdateHpBar(float currentHp, float maxHp)
    {
        if (hpBar == null) return;
        hpBar.value = currentHp / maxHp;
        UpdateHpValueText(currentHp, maxHp);
    }

    public void UpdateActionGauge(float ratio) // 0~1
    {
        if (actionGaugeBar != null)
            actionGaugeBar.value = ratio;

        UpdateActionGaugeValueText();
    }

    void UpdateHpValueText(float currentHp, float maxHp)
    {
        if (hpValueText == null)
            return;

        int current = Mathf.Max(0, Mathf.RoundToInt(currentHp));
        int max = Mathf.Max(0, Mathf.RoundToInt(maxHp));
        hpValueText.text = $"{current}/{max}";
    }

    void UpdateActionGaugeValueText()
    {
        if (actionGaugeValueText == null || stat == null)
            return;

        actionGaugeValueText.text = $"{stat.GaugeStep}/{EnemyStat.GAUGE_MAX_STEPS}";
    }

    public void ShowMidPatternNotice(string message)
    {
        if (!showPatternNotice)
            return;

        EnsurePatternNotice();
        if (patternNoticeText == null)
            return;

        if (patternNoticeCoroutine != null)
            StopCoroutine(patternNoticeCoroutine);

        patternNoticeText.text = message;
        if (patternNoticeBackground != null)
            patternNoticeBackground.gameObject.SetActive(true);

        patternNoticeCoroutine = StartCoroutine(HidePatternNoticeAfterDelay());
    }

    IEnumerator HidePatternNoticeAfterDelay()
    {
        yield return new WaitForSeconds(patternNoticeDuration);

        if (patternNoticeText != null)
            patternNoticeText.text = "";

        if (patternNoticeBackground != null)
            patternNoticeBackground.gameObject.SetActive(false);
    }

    void UpdateAttackPreview(int count)
    {
        if (attackPreviewText == null) return;

        int damagePerHit = Mathf.RoundToInt(stat.AttackDamage);
        if (damagePerHit < 0)
            damagePerHit = 0;

        int hitCount = Mathf.Max(0, count);
        attackPreviewText.text = $"{damagePerHit}x{hitCount}";
    }

    // EnemyController가 TakeDamage 직후 호출
    public void ShowDamage(float damage)
    {
        if (damageText == null) return;

        // 이전 연출 중단 후 재시작
        if (damageCoroutine != null) StopCoroutine(damageCoroutine);
        damageText.text = ((int)damage).ToString();
        damageCoroutine = StartCoroutine(FloatingDamageEffect());
    }

    public void SetSprite(Sprite sprite)
    {
        Debug.Log($"[EnemyView] enemyImage={enemyImage != null}, sprite={sprite?.name}");
        if (enemyImage != null) enemyImage.sprite = sprite;
        LayoutUiBelowEnemy();
    }


    IEnumerator FloatingDamageEffect()
    {
        float timer = 0f;

        // 위치/색상 초기화
        damageText.transform.localPosition = damageTextOriginLocalPos;
        damageText.color = damageTextOriginColor;

        while (timer < fadeTime)
        {
            timer += Time.deltaTime;
            damageText.transform.localPosition += Vector3.up * floatSpeed * Time.deltaTime;
            damageText.color = new Color(
                damageTextOriginColor.r,
                damageTextOriginColor.g,
                damageTextOriginColor.b,
                Mathf.Lerp(1f, 0f, timer / fadeTime)
            );
            yield return null;
        }

        delDamageText();
    }

    void delDamageText()
    {
        if (damageText == null) return;
        damageText.text = "";
        damageText.transform.localPosition = damageTextOriginLocalPos;
        damageText.color = damageTextOriginColor;
    }

    public void PlayHitEffect(string cardType)
    {
        switch (cardType)
        {
            case "Q":
                if (qEffect != null) qEffect.Play();
                break;
            case "W":
                if (wEffect != null) wEffect.Play();
                break;
            case "E":
                if (eEffect != null) eEffect.Play();
                break;
            case "R":
                if (rEffect != null) rEffect.Play();
                break;
        }
    }
}
