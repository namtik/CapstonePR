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

    [Header("데미지 연출 설정")]
    [SerializeField] private float fadeTime = 1f;
    [SerializeField] private float floatSpeed = 0.5f;

    [Header("UI 위치 보정")]
    [SerializeField] private float uiVerticalOffset = 76f;

    [Header("피격 이펙트 (파티클)")]
    public ParticleSystem qEffect;
    public ParticleSystem wEffect;
    public ParticleSystem eEffect;
    public ParticleSystem rEffect;

    private EnemyStat stat;
    private Vector3 damageTextOriginLocalPos;
    private Color damageTextOriginColor;
    private Coroutine damageCoroutine;

    void LayoutUiBelowEnemy()
    {
        RectTransform enemyRect = enemyImage != null ? enemyImage.rectTransform : GetComponent<RectTransform>();
        if (enemyRect == null) return;

        float enemyHeight = enemyRect.rect.height;
        if (enemyHeight <= 0f)
            enemyHeight = 360f;

        // Move enemy UI upward to avoid overlap with bottom combo slots.
        float baseY = -(enemyHeight * 0.5f) - 24f + uiVerticalOffset;

        PositionUiRect(hpBar != null ? hpBar.GetComponent<RectTransform>() : null, baseY, new Vector2(220f, 18f));
        PositionUiRect(actionGaugeBar != null ? actionGaugeBar.GetComponent<RectTransform>() : null, baseY - 24f, new Vector2(220f, 14f));
        PositionUiRect(attackPreviewText != null ? attackPreviewText.rectTransform : null, baseY - 52f, new Vector2(220f, 28f));
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
    }

    public void UpdateActionGauge(float ratio) // 0~1
    {
        if (actionGaugeBar != null)
            actionGaugeBar.value = ratio;
    }

    void UpdateAttackPreview(int count)
    {
        if (attackPreviewText == null) return;
        if (count <= 0)
            attackPreviewText.text = "";
        else
            attackPreviewText.text = $"{Mathf.RoundToInt(stat.AttackDamage)}";
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
