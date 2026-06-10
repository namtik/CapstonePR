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
    [Tooltip("기본 크기 배율 — 텍스트 원본 폰트 크기에 곱해짐. 1.0=원본, 2.0=두 배.")]
    [SerializeField] private float baseScale = 2.0f;
    [Tooltip("출현 직후 임팩트 — baseScale에 곱하는 추가 배율. 1.5 = 50% 더 커졌다가 줄어듦.")]
    [SerializeField] private float burstScale = 1.6f;
    [Tooltip("burstScale에서 baseScale로 줄어드는 시간(초).")]
    [SerializeField] private float burstDuration = 0.15f;
    [Tooltip("연출 색 강조 사용 여부.")]
    [SerializeField] private bool overrideColor = true;
    [Tooltip("일반 데미지 강조 색.")]
    [SerializeField] private Color damageColor = new Color(1f, 0.92f, 0.3f, 1f);
    [Tooltip("화상 등 상태이상 피해 색.")]
    [SerializeField] private Color burnDamageColor = new Color(1f, 0.35f, 0.2f, 1f);
    [Tooltip("연속 타격 시 겹침 방지 — 임의 X 오프셋(±). 0이면 비활성.")]
    [SerializeField] private float randomXOffset = 60f;
    [Tooltip("연속 타격 시 겹침 방지 — 임의 Y 오프셋(±). 0이면 비활성.")]
    [SerializeField] private float randomYOffset = 40f;
    [Tooltip("연출 텍스트 앞에 표시할 접두어(예: '-').")]
    [SerializeField] private string damagePrefix = "-";

    [Header("피격 이펙트 (파티클)")]
    [Tooltip("피격 파티클 재생 속도 배율 — 1 미만이면 더 천천히(=더 오래) 보인다. 순식간에 사라질 때 낮춰라.")]
    [Range(0.2f, 2f)]
    [SerializeField] private float hitEffectPlaybackSpeed = 0.7f;
    public ParticleSystem qEffect;
    public ParticleSystem wEffect;
    public ParticleSystem eEffect;
    public ParticleSystem rEffect;
    public ParticleSystem LEffect;

    [Header("피격 반응 (빨강 플래시 + 흔들기)")]
    [SerializeField] private bool hitReactionEnabled = true;
    [Tooltip("흔들 대상. 비우면 몬스터 본체(이 오브젝트)의 RectTransform 사용.")]
    [SerializeField] private RectTransform shakeTarget;
    [Tooltip("플래시(색 변경) 대상 Image. 비우면 enemyImage 사용.")]
    [SerializeField] private Image flashTarget;
    [Tooltip("피격 시 잠깐 번쩍이는 색(기본 빨강).")]
    [SerializeField] private Color hitFlashColor = Color.red;

    [Header("피격 반응 — 데미지 3단계 경계")]
    [Tooltip("데미지가 이 값 미만이면 '약'. 기본 20.")]
    [SerializeField] private float mediumDamageThreshold = 20f;
    [Tooltip("데미지가 이 값 이상이면 '강'. 기본 60. (그 사이는 '중')")]
    [SerializeField] private float strongDamageThreshold = 60f;

    [Header("피격 반응 — 단계별 세기 (약/중/강)")]
    [SerializeField] private HitReactionTier weakHit = new HitReactionTier(0.15f, 6f, 0.10f, 0.5f);
    [SerializeField] private HitReactionTier mediumHit = new HitReactionTier(0.22f, 14f, 0.13f, 0.75f);
    [SerializeField] private HitReactionTier strongHit = new HitReactionTier(0.32f, 26f, 0.16f, 1f);

    [Header("피격 이미지 교체")]
    [Tooltip("피격 시 hitSprite로 잠깐 바꿨다가 원래 이미지로 되돌릴지 여부.")]
    [SerializeField] private bool hitSpriteSwapEnabled = true;
    [Tooltip("피격 이미지를 유지하는 시간(초). 이 시간이 지나면 원래 이미지로 복귀.")]
    [SerializeField] private float hitSpriteDuration = 0.25f;
    [Tooltip("피격 당한 이미지. 보통 EnemyData.hitSprite로 주입되며, 비우면 교체하지 않는다.")]
    [SerializeField] private Sprite hitSprite;

    [Header("공격 모션 (이미지 교체 — 여러 프레임)")]
    [Tooltip("적 공격 시 attackSprites를 순서대로 잠깐 재생한 뒤 평상시 이미지로 복귀할지 여부.")]
    [SerializeField] private bool attackMotionEnabled = true;
    [Tooltip("프레임당 표시 시간(초). 여러 장이면 이 간격으로 순차 재생. 0이면 모션 없음.")]
    [SerializeField] private float attackFrameDuration = 0.08f;
    [Tooltip("공격 모션 프레임들. 보통 EnemyData.attackSprites로 주입되며, 비우면 모션이 재생되지 않는다.")]
    [SerializeField] private Sprite[] attackSprites;

    // 피격 반응 런타임 상태
    private Image _flashImage;
    private Color _flashBaseColor;
    private Coroutine _flashCo;
    private RectTransform _shakeRt;
    private Vector2 _shakeBasePos;
    private Coroutine _shakeCo;
    private float _hitReactionEndTime; // 마지막 피격 연출이 끝나는 시각(Time.time 기준)

    // 피격 이미지 교체 런타임 상태
    private Sprite _normalSprite;      // 평상시 스프라이트(피격/공격 모션 후 복귀 대상)
    private Coroutine _hitSpriteCo;

    // 공격 모션 런타임 상태
    private Coroutine _attackSpriteCo;
    private float _attackMotionEndTime; // 진행 중 공격 모션이 끝나는 시각(Time.time 기준)

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
        stat.OnDamaged += HandleDamaged;

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
        stat.OnDamaged -= HandleDamaged;
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

    /// <summary>새 전투 시스템: 다음 공격 데미지를 attackPreviewText에 표시.</summary>
    public void SetAttackPreviewDamage(int damage)
    {
        if (attackPreviewText == null) return;
        attackPreviewText.text = damage.ToString();
    }

    // EnemyController가 TakeDamage 직후 호출 — 히트마다 별개 텍스트 인스턴스 생성 (5x5 등 멀티히트 동시 표시)
    public void ShowDamage(float damage) => ShowDamage(damage, isBurn: false);

    /// <summary>isBurn=true이면 화상 색 사용.</summary>
    public void ShowDamage(float damage, bool isBurn)
    {
        if (damageText == null) return;
        if (damage <= 0) return;

        int dmg = Mathf.Max(0, (int)damage);
        string content = string.IsNullOrEmpty(damagePrefix) ? dmg.ToString() : $"{damagePrefix}{dmg}";

        // 색은 리치텍스트 태그로 직접 박아넣음 — TMP의 머티리얼/그래디언트가 .color를 가리는 케이스를 우회.
        Color baseColor = isBurn ? burnDamageColor : (overrideColor ? damageColor : damageTextOriginColor);
        string hex = ColorUtility.ToHtmlStringRGB(baseColor);
        string textValue = $"<color=#{hex}>{content}</color>";

        // 템플릿(damageText) 복제 — 원본은 빈 상태 유지, 클론이 각자 떠오르고 사라짐
        var clone = Instantiate(damageText, damageText.transform.parent);
        clone.name = isBurn ? "DamageText_Burn" : "DamageText_Clone";
        clone.richText = true;
        clone.text = textValue;
        clone.gameObject.SetActive(true);
        Debug.Log($"[ShowDamage] dmg={dmg} isBurn={isBurn} → 색={baseColor} (richText)");
        StartCoroutine(FloatingDamageEffectFor(clone, isBurn));
    }

    public void SetSprite(Sprite sprite)
    {
        Debug.Log($"[EnemyView] enemyImage={enemyImage != null}, sprite={sprite?.name}");
        if (enemyImage != null) enemyImage.sprite = sprite;
        // 피격 이미지에서 되돌릴 기준 스프라이트로 기억(피격 연출 진행 중이 아닐 때만 갱신).
        if (_hitSpriteCo == null && _attackSpriteCo == null) _normalSprite = sprite;
    }

    /// <summary>EnemyData에서 피격 당한 이미지를 주입. null이면 교체하지 않는다.</summary>
    public void SetHitSprite(Sprite sprite)
    {
        hitSprite = sprite;
    }

    /// <summary>EnemyData에서 공격 모션 프레임들을 주입. 비거나 null이면 기존(인스펙터) 값 유지.</summary>
    public void SetAttackSprites(Sprite[] sprites)
    {
        if (sprites != null && sprites.Length > 0) attackSprites = sprites;
    }


    /// <summary>각 히트마다 복제된 텍스트 인스턴스를 띄우는 코루틴 — 끝나면 자기 자신 파괴.</summary>
    IEnumerator FloatingDamageEffectFor(TMP_Text textInstance, bool isBurn = false)
    {
        if (textInstance == null) yield break;

        float timer = 0f;

        // 위치 (템플릿 원점 + 임의 X/Y 오프셋)
        Vector3 startPos = damageTextOriginLocalPos;
        if (randomXOffset > 0f) startPos.x += Random.Range(-randomXOffset, randomXOffset);
        if (randomYOffset > 0f) startPos.y += Random.Range(-randomYOffset, randomYOffset);
        textInstance.transform.localPosition = startPos;

        // 색은 ShowDamage에서 리치텍스트(<color>)로 박았으니 여기선 알파만 다룸.
        // CanvasGroup으로 알파 페이드 — TMP 색 설정을 건드리지 않고 깔끔하게 처리.
        var cg = textInstance.GetComponent<CanvasGroup>();
        if (cg == null) cg = textInstance.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 1f;

        Vector3 baseSize = Vector3.one * Mathf.Max(0.01f, baseScale);
        Vector3 burstSize = baseSize * Mathf.Max(1f, burstScale);
        textInstance.transform.localScale = burstSize;

        while (timer < fadeTime)
        {
            if (textInstance == null) yield break;
            timer += Time.deltaTime;
            textInstance.transform.localPosition += Vector3.up * floatSpeed * Time.deltaTime;

            if (timer < burstDuration && burstDuration > 0f)
            {
                float t = timer / burstDuration;
                float eased = 1f - (1f - t) * (1f - t); // ease-out
                textInstance.transform.localScale = Vector3.Lerp(burstSize, baseSize, eased);
            }
            else
            {
                textInstance.transform.localScale = baseSize;
            }

            cg.alpha = Mathf.Lerp(1f, 0f, timer / fadeTime);
            yield return null;
        }

        if (textInstance != null) Destroy(textInstance.gameObject);
    }

    void delDamageText()
    {
        if (damageText == null) return;
        damageText.text = "";
        damageText.transform.localPosition = damageTextOriginLocalPos;
        damageText.transform.localScale = Vector3.one;
        damageText.color = damageTextOriginColor;
    }

    public void PlayHitEffect(string cardType)
    {
        switch (cardType)
        {
            case "Q": PlayHitParticle(qEffect); break;
            case "W": PlayHitParticle(wEffect); break;
            case "E": PlayHitParticle(eEffect); break;
            case "R": PlayHitParticle(rEffect); break;
            case "L": PlayHitParticle(LEffect); break;
        }
    }

    /// <summary>피격 파티클 재생 — hitEffectPlaybackSpeed로 시뮬레이션 속도를 낮춰 더 오래 보이게 한다.</summary>
    void PlayHitParticle(ParticleSystem ps)
    {
        if (ps == null) return;

        float spd = Mathf.Max(0.01f, hitEffectPlaybackSpeed);
        // 하위 파티클까지 모두 같은 속도로 — 직접 set이라 반복 호출에도 누적되지 않음(멱등).
        var systems = ps.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            var main = systems[i].main;
            main.simulationSpeed = spd;
        }
        ps.Play();
    }

    // ───────────── 피격 반응: 빨강 플래시 + 흔들기 (데미지 3단계) ─────────────

    /// <summary>현재 진행 중인 피격 연출(플래시+흔들기)이 끝날 때까지 남은 시간(초). 없으면 0.</summary>
    public float HitReactionRemaining => Mathf.Max(0f, _hitReactionEndTime - Time.time);

    /// <summary>피격 흔들림이 적용되는 RectTransform(미지정 시 본체). 상태 패널이 흔들림을 따라갈 때 사용.</summary>
    public RectTransform ShakeTarget => shakeTarget != null ? shakeTarget : (transform as RectTransform);

    // EnemyStat.OnDamaged 구독 핸들러 — 데미지 크기로 단계를 골라 연출 재생.
    void HandleDamaged(float damage)
    {
        if (!hitReactionEnabled || damage <= 0f) return;

        HitReactionTier tier =
            damage >= strongDamageThreshold ? strongHit :
            damage >= mediumDamageThreshold ? mediumHit :
            weakHit;

        // 사망 시 EnemyController가 이 시간만큼 기다렸다가 적을 제거한다(피격 연출 후 사라짐).
        _hitReactionEndTime = Time.time + Mathf.Max(tier.flashDuration, tier.shakeDuration);

        PlayFlash(tier);
        PlayShake(tier);
        PlayHitSpriteSwap();
    }

    // ───────────── 피격 이미지 교체: hitSprite로 잠깐 바꿨다가 원래대로 복귀 ─────────────

    void PlayHitSpriteSwap()
    {
        if (!hitSpriteSwapEnabled || hitSprite == null || hitSpriteDuration <= 0f) return;
        if (enemyImage == null) return;
        // 공격 모션 중에는 피격 이미지 교체를 건너뜀 — 공격 프레임이 우선(둘이 sprite를 다투지 않게). 플래시/흔들기는 그대로 적용됨.
        if (_attackSpriteCo != null) return;

        // 진행 중이 아닐 때만 평상시 스프라이트 캡처 — 연속 피격에도 원본을 잃지 않음.
        if (_hitSpriteCo == null)
        {
            if (_normalSprite == null) _normalSprite = enemyImage.sprite;
        }
        else
        {
            StopCoroutine(_hitSpriteCo);
        }
        _hitSpriteCo = StartCoroutine(HitSpriteRoutine());
    }

    IEnumerator HitSpriteRoutine()
    {
        if (enemyImage == null) { _hitSpriteCo = null; yield break; }

        enemyImage.sprite = hitSprite;

        float t = 0f;
        while (t < hitSpriteDuration)
        {
            if (enemyImage == null) { _hitSpriteCo = null; yield break; }
            t += Time.deltaTime;
            yield return null;
        }

        if (enemyImage != null) enemyImage.sprite = _normalSprite;
        _hitSpriteCo = null;
    }

    // ───────────── 공격 모션: attackSprites를 순서대로 재생 후 평상시 이미지로 복귀 ─────────────

    /// <summary>진행 중인 공격 모션이 끝날 때까지 남은 시간(초). 없으면 0.</summary>
    public float AttackMotionRemaining => _attackMotionEndTime > 0f ? Mathf.Max(0f, _attackMotionEndTime - Time.time) : 0f;

    /// <summary>적 공격 시 호출 — attackSprites를 프레임마다 잠깐 보여준 뒤 평상시 이미지로 복귀.</summary>
    public void PlayAttackMotion()
    {
        if (!attackMotionEnabled || attackSprites == null || attackSprites.Length == 0) return;
        if (attackFrameDuration <= 0f || enemyImage == null) return;

        // 공격 모션이 sprite를 독점 — 진행 중인 피격 이미지 교체를 멈춤(그 복귀가 공격 프레임 사이에 기본을 끼워넣는 것 방지).
        if (_hitSpriteCo != null)
        {
            StopCoroutine(_hitSpriteCo);
            _hitSpriteCo = null;
        }

        // 진행 중이 아닐 때만 평상시 스프라이트 캡처 — 연속 호출에도 원본을 잃지 않음.
        if (_attackSpriteCo == null)
        {
            if (_normalSprite == null) _normalSprite = enemyImage.sprite;
        }
        else
        {
            StopCoroutine(_attackSpriteCo);
        }
        _attackSpriteCo = StartCoroutine(AttackMotionRoutine());
    }

    IEnumerator AttackMotionRoutine()
    {
        if (enemyImage == null) { _attackSpriteCo = null; yield break; }

        _attackMotionEndTime = Time.time + attackFrameDuration * attackSprites.Length;

        for (int i = 0; i < attackSprites.Length; i++)
        {
            if (enemyImage == null) { _attackSpriteCo = null; yield break; }
            if (attackSprites[i] != null) enemyImage.sprite = attackSprites[i];

            float t = 0f;
            while (t < attackFrameDuration)
            {
                if (enemyImage == null) { _attackSpriteCo = null; yield break; }
                t += Time.deltaTime;
                yield return null;
            }
        }

        if (enemyImage != null) enemyImage.sprite = _normalSprite;
        _attackMotionEndTime = 0f;
        _attackSpriteCo = null;
    }

    void PlayFlash(HitReactionTier tier)
    {
        if (tier == null || tier.flashDuration <= 0f || tier.flashStrength <= 0f) return;

        Image img = flashTarget != null ? flashTarget : enemyImage;
        if (img == null) return;

        // 진행 중이 아닐 때만 기준색 캡처 — 연속 피격에도 원래 색을 잃지 않음.
        if (_flashCo == null)
        {
            _flashImage = img;
            _flashBaseColor = img.color;
        }
        else
        {
            StopCoroutine(_flashCo);
        }
        _flashCo = StartCoroutine(FlashRoutine(tier));
    }

    IEnumerator FlashRoutine(HitReactionTier tier)
    {
        if (_flashImage == null) { _flashCo = null; yield break; }

        Color target = Color.Lerp(_flashBaseColor, hitFlashColor, Mathf.Clamp01(tier.flashStrength));
        _flashImage.color = target;

        float t = 0f;
        while (t < tier.flashDuration)
        {
            if (_flashImage == null) { _flashCo = null; yield break; }
            t += Time.deltaTime;
            _flashImage.color = Color.Lerp(target, _flashBaseColor, t / tier.flashDuration);
            yield return null;
        }

        if (_flashImage != null) _flashImage.color = _flashBaseColor;
        _flashCo = null;
    }

    void PlayShake(HitReactionTier tier)
    {
        if (tier == null || tier.shakeDuration <= 0f || tier.shakeMagnitude <= 0f) return;

        RectTransform rt = shakeTarget != null ? shakeTarget : (transform as RectTransform);
        if (rt == null) return;

        // 진행 중이 아닐 때만 기준 위치 캡처. 진행 중이면 기준 위치로 되돌린 뒤 새로 시작.
        if (_shakeCo == null)
        {
            _shakeRt = rt;
            _shakeBasePos = rt.anchoredPosition;
        }
        else
        {
            StopCoroutine(_shakeCo);
            if (_shakeRt != null) _shakeRt.anchoredPosition = _shakeBasePos;
        }
        _shakeCo = StartCoroutine(ShakeRoutine(tier));
    }

    IEnumerator ShakeRoutine(HitReactionTier tier)
    {
        if (_shakeRt == null) { _shakeCo = null; yield break; }

        float t = 0f;
        while (t < tier.shakeDuration)
        {
            if (_shakeRt == null) { _shakeCo = null; yield break; }
            t += Time.deltaTime;
            float damp = 1f - (t / tier.shakeDuration);   // 점점 약해지는 감쇠
            float mag = tier.shakeMagnitude * damp;
            Vector2 offset = new Vector2(Random.Range(-mag, mag), Random.Range(-mag, mag));
            _shakeRt.anchoredPosition = _shakeBasePos + offset;
            yield return null;
        }

        if (_shakeRt != null) _shakeRt.anchoredPosition = _shakeBasePos;
        _shakeCo = null;
    }

    /// <summary>피격 반응 한 단계의 세기 묶음(인스펙터 노출).</summary>
    [System.Serializable]
    public class HitReactionTier
    {
        [Tooltip("흔들림 지속시간(초).")] public float shakeDuration;
        [Tooltip("흔들림 세기(픽셀).")] public float shakeMagnitude;
        [Tooltip("빨강 플래시 지속시간(초).")] public float flashDuration;
        [Range(0f, 1f)] [Tooltip("플래시 강도(0=변화 없음, 1=완전히 플래시 색).")] public float flashStrength;

        public HitReactionTier() { }
        public HitReactionTier(float shakeDuration, float shakeMagnitude, float flashDuration, float flashStrength)
        {
            this.shakeDuration = shakeDuration;
            this.shakeMagnitude = shakeMagnitude;
            this.flashDuration = flashDuration;
            this.flashStrength = flashStrength;
        }
    }
}
