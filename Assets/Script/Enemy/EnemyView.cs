using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class EnemyView : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private bool autoBindOnValidate = true;
    [SerializeField] private Slider hpBar;
    [SerializeField] private Slider actionGaugeBar;
    [SerializeField] private TMP_Text damageText;
    [SerializeField] private Image enemyImage;

    [Header("공격 예정 표시")]
    [SerializeField] private TMP_Text attackPreviewText;

    [Header("게이지 내부 수치 텍스트")]
    [SerializeField] private TMP_Text hpValueText;
    [SerializeField] private TMP_Text actionGaugeValueText;

    [Header("패턴 발동 알림")]
    [SerializeField] private bool showPatternNotice = true;
    [SerializeField] private float patternNoticeDuration = 1.6f;
    [SerializeField] private GameObject patternNoticeObject;
    [SerializeField] private TMP_Text patternNoticeText;

    [Header("데미지 연출 설정")]
    [SerializeField] private float fadeTime = 1f;
    [SerializeField] private float floatSpeed = 0.5f;

    [Header("피격 이펙트 (파티클)")]
    public ParticleSystem qEffect;
    public ParticleSystem wEffect;
    public ParticleSystem eEffect;
    public ParticleSystem rEffect;

    private EnemyStat stat;
    private Vector3 damageTextOriginLocalPos;
    private Color damageTextOriginColor;
    private Coroutine damageCoroutine;
    private Coroutine patternNoticeCoroutine;

    void Reset()
    {
        TryAutoBindReferences();
    }

    void OnValidate()
    {
        if (!autoBindOnValidate)
            return;

        TryAutoBindReferences();
    }

    void TryAutoBindReferences()
    {
        if (enemyImage == null)
            enemyImage = GetComponent<Image>();

        if (hpBar == null)
            hpBar = transform.Find("HpBar")?.GetComponent<Slider>();

        if (actionGaugeBar == null)
            actionGaugeBar = transform.Find("gaugeSlider")?.GetComponent<Slider>();

        if (damageText == null)
            damageText = transform.Find("damageText")?.GetComponent<TMP_Text>();

        // Force-bind known static UI/effect nodes to overwrite stale type-mismatch references.
        Transform previewNode = transform.Find("attackPreviewText");
        if (previewNode != null)
            attackPreviewText = previewNode.GetComponent<TMP_Text>();

        if (hpValueText == null)
            hpValueText = transform.Find("HpBar/HpValueText")?.GetComponent<TMP_Text>();

        if (actionGaugeValueText == null)
            actionGaugeValueText = transform.Find("gaugeSlider/GaugeValueText")?.GetComponent<TMP_Text>();

        Transform qNode = transform.Find("Fx_hit/Hitq");
        if (qNode != null)
            qEffect = qNode.GetComponent<ParticleSystem>();

        Transform wNode = transform.Find("Fx_hit/Hitw");
        if (wNode != null)
            wEffect = wNode.GetComponent<ParticleSystem>();

        Transform eNode = transform.Find("Fx_hit/Hite");
        if (eNode != null)
            eEffect = eNode.GetComponent<ParticleSystem>();

        Transform rNode = transform.Find("Fx_hit/Hitr");
        if (rNode != null)
            rEffect = rNode.GetComponent<ParticleSystem>();

        if (patternNoticeObject == null)
        {
            Transform patternNoticeRoot = transform.Find("PatternNoticeRoot") ?? transform.Find("PatternNotice");
            if (patternNoticeRoot != null)
                patternNoticeObject = patternNoticeRoot.gameObject;
        }

        if (patternNoticeText == null && patternNoticeObject != null)
            patternNoticeText = patternNoticeObject.GetComponentInChildren<TMP_Text>(true);
    }

    void Awake()
    {
        stat = GetComponent<EnemyStat>();
        stat.OnHpChanged += UpdateHpBar;
        stat.OnAttackCountChanged += UpdateAttackPreview;

        if (patternNoticeObject != null)
            patternNoticeObject.SetActive(false);

        Canvas canvas = GetComponentInChildren<Canvas>();
        if (canvas != null)
        {
            canvas.worldCamera = Camera.main;  // 카메라 연결
            canvas.sortingOrder = 10;          // Enemy 이미지보다 앞
        }

        if (stat != null)
            UpdateHpBar(stat.currentHp, stat.maxHp);

        UpdateActionGauge(stat != null ? (float)stat.GaugeStep / EnemyStat.GAUGE_MAX_STEPS : 0f);
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

        // Action gauge bar: reset to 0 (updated via EnemyController → stat.OnGaugeStepChanged)
        if (actionGaugeBar != null) actionGaugeBar.value = 0f;
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
        if (hpBar != null)
            hpBar.value = maxHp > 0f ? currentHp / maxHp : 0f;

        if (hpValueText != null)
        {
            int current = Mathf.Max(0, Mathf.RoundToInt(currentHp));
            int max = Mathf.Max(0, Mathf.RoundToInt(maxHp));
            hpValueText.text = $"{current}/{max}";
        }
    }

    public void UpdateActionGauge(float ratio) // 0~1
    {
        if (actionGaugeBar != null)
            actionGaugeBar.value = ratio;

        if (actionGaugeValueText != null)
        {
            int step = stat != null ? Mathf.Clamp(stat.GaugeStep, 0, EnemyStat.GAUGE_MAX_STEPS) : Mathf.RoundToInt(ratio * EnemyStat.GAUGE_MAX_STEPS);
            actionGaugeValueText.text = $"{step}/{EnemyStat.GAUGE_MAX_STEPS}";
        }
    }

    public void ShowMidPatternNotice(string message)
    {
        if (!showPatternNotice)
            return;

        if (patternNoticeObject == null || patternNoticeText == null)
        {
            Debug.LogWarning("EnemyView: patternNoticeObject/patternNoticeText 참조가 비어 있습니다.");
            return;
        }

        if (patternNoticeCoroutine != null)
            StopCoroutine(patternNoticeCoroutine);

        patternNoticeText.text = message;
        patternNoticeObject.SetActive(true);

        patternNoticeCoroutine = StartCoroutine(HidePatternNoticeAfterDelay());
    }

    IEnumerator HidePatternNoticeAfterDelay()
    {
        yield return new WaitForSeconds(patternNoticeDuration);

        if (patternNoticeText != null)
            patternNoticeText.text = "";

        if (patternNoticeObject != null)
            patternNoticeObject.SetActive(false);
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
