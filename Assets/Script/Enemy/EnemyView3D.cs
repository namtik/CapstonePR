using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 3D 리깅 몬스터용 적 뷰. IEnemyView를 구현하여 EnemyController/RoundManager와 결합하되,
// 시각 표현은 Animator(Idle/Hit/Attack/Die) + 머티리얼 플래시 + 메쉬 흔들기 + 월드 파티클로 구동한다.
// 수치/게이지/데미지/알림 UI는 Overlay의 EnemyOverlayHUD로 위임한다.
[RequireComponent(typeof(EnemyStat))]
public class EnemyView3D : MonoBehaviour, IEnemyView
{
    [Header("모델/애니메이션")]
    [Tooltip("몬스터 모델의 Animator. 파라미터: Hit/Attack/Die(트리거), 기본 상태 Idle.")]
    [SerializeField] private Animator animator; // 모델 Animator
    [SerializeField] private string hitTrigger = "Hit";     // 피격 트리거
    [SerializeField] private string attackTrigger = "Attack"; // 공격 트리거
    [SerializeField] private string dieTrigger = "Die";     // 사망 트리거
    [Tooltip("Die 상태/클립이 준비되어 있으면 체크. 없으면 사망 시 트리거 없이 축소 연출만 재생.")]
    [SerializeField] private bool hasDieState = false;      // Die 상태 존재 여부

    [Header("피격 반응 — 흔들기")]
    [Tooltip("피격 시 흔들 대상(모델 루트). 비우면 이 오브젝트. 상태이상 패널 추적 대상이기도 함.")]
    [SerializeField] private Transform shakeTarget;         // 흔들기 대상(모델)
    [SerializeField] private float shakeDuration = 0.18f;   // 흔들림 지속(초)
    [SerializeField] private float shakeMagnitude = 0.12f;  // 흔들림 세기(월드 유닛)

    [Header("피격 반응 — 머티리얼 플래시")]
    [SerializeField] private Renderer[] flashRenderers;     // 플래시 적용 렌더러(스킨드 메쉬 등)
    [SerializeField] private Color hitFlashColor = Color.red; // 플래시 색
    [Range(0f, 1f)] [SerializeField] private float flashStrength = 0.8f; // 플래시 강도
    [SerializeField] private float flashDuration = 0.15f;   // 플래시 지속(초)
    [Tooltip("색을 덮어쓸 셰이더 프로퍼티. URP Lit/Toon은 보통 _BaseColor. 없으면 플래시 생략.")]
    [SerializeField] private string colorProperty = "_BaseColor"; // 색 프로퍼티명

    [Header("피격 이펙트(월드 파티클, 카드 타입별)")]
    public ParticleSystem qEffect; // Q 피격 파티클
    public ParticleSystem wEffect; // W 피격 파티클
    public ParticleSystem eEffect; // E 피격 파티클
    public ParticleSystem rEffect; // R 피격 파티클
    public ParticleSystem lEffect; // 런처 피격 파티클
    [Tooltip("적 공격 임팩트 순간(Attack 클립의 Animation Event 'OnAttackImpact')에 재생할 파티클.")]
    [SerializeField] private ParticleSystem attackVfx; // 적 공격 파티클

    [Header("HUD/카메라")]
    [Tooltip("Overlay HUD가 따라갈 앵커(보통 머리 위 빈 오브젝트). 비우면 이 오브젝트.")]
    [SerializeField] private Transform hudAnchor;   // HUD 팔로우 앵커
    [Tooltip("몬스터를 렌더하는 스테이지 카메라. 비우면 Camera.main. 3단계에서 주입.")]
    [SerializeField] private Camera renderCamera;   // 렌더 카메라

    [Header("사망 연출")]
    [Tooltip("사망 후 파괴까지의 시간(초). Die 클립 길이에 맞춰라. 클립 없으면 이 시간 동안 축소.")]
    [SerializeField] private float deathDuration = 1f; // 사망 연출 시간

    public event Action OnAttackMotionLastFrame; // 공격 임팩트 프레임 도달(VFX 동기화)

    private EnemyStat _stat;              // 적 스탯
    private EnemyOverlayHUD _hud;         // 위임 대상 Overlay HUD
    private float _hitReactionEndTime;    // 피격 연출 종료 시각
    private Vector3 _shakeBasePos;        // 흔들기 기준 위치
    private Coroutine _shakeCo;           // 흔들기 코루틴
    private Coroutine _flashCo;           // 플래시 코루틴
    private readonly List<Color> _baseColors = new List<Color>(); // 렌더러별 기준 색
    private MaterialPropertyBlock _mpb;   // 플래시용 프로퍼티 블록
    private bool _dead;                   // 사망 처리 여부

    // 스탯 이벤트 구독, 참조 캐싱
    void Awake()
    {
        _stat = GetComponent<EnemyStat>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (shakeTarget == null) shakeTarget = animator != null ? animator.transform : transform;
        if (flashRenderers == null || flashRenderers.Length == 0)
            flashRenderers = GetComponentsInChildren<Renderer>(true);

        _mpb = new MaterialPropertyBlock();
        CacheBaseColors();

        _stat.OnHpChanged += HandleHpChanged;
        _stat.OnDamaged += HandleDamaged;
    }

    // HUD 바인딩 및 초기 UI 반영
    void Start()
    {
        _hud = EnemyOverlayHUD.Instance;
        if (_hud != null)
        {
            _hud.Bind(hudAnchor != null ? hudAnchor : transform,
                      renderCamera != null ? renderCamera : Camera.main,
                      _stat != null ? _stat.GaugeMaxSteps : EnemyStat.GAUGE_MAX_STEPS);
            if (_stat != null) _hud.SetHp(_stat.currentHp, _stat.maxHp);
            _hud.SetGauge(0f);
        }
    }

    void OnDestroy()
    {
        if (_stat != null)
        {
            _stat.OnHpChanged -= HandleHpChanged;
            _stat.OnDamaged -= HandleDamaged;
        }
        // 이 몬스터가 파괴될 때 HUD가 떠 있으면 숨김(다음 몬스터가 다시 Bind).
        if (_hud != null) _hud.Unbind();
    }

    // 3단계 배선용: 스테이지 카메라를 주입한다.
    public void SetRenderCamera(Camera cam)
    {
        renderCamera = cam;
        if (_hud != null)
            _hud.Bind(hudAnchor != null ? hudAnchor : transform, cam,
                      _stat != null ? _stat.GaugeMaxSteps : EnemyStat.GAUGE_MAX_STEPS);
    }

    // 렌더러별 기준 색을 캐싱(플래시 복귀용)
    void CacheBaseColors()
    {
        _baseColors.Clear();
        if (flashRenderers == null) return;
        foreach (var r in flashRenderers)
        {
            Color c = Color.white;
            if (r != null && r.sharedMaterial != null && r.sharedMaterial.HasProperty(colorProperty))
                c = r.sharedMaterial.GetColor(colorProperty);
            _baseColors.Add(c);
        }
    }

    // ── EnemyStat 이벤트 핸들러 ──

    // HP 변경 → HUD 갱신
    void HandleHpChanged(float current, float max)
    {
        if (_hud != null) _hud.SetHp(current, max);
    }

    // 피격 → Hit 애니 + 플래시 + 흔들기
    void HandleDamaged(float damage)
    {
        if (_dead || damage <= 0f) return;
        if (animator != null && !string.IsNullOrEmpty(hitTrigger)) animator.SetTrigger(hitTrigger);
        _hitReactionEndTime = Time.time + Mathf.Max(flashDuration, shakeDuration);
        StartFlash();
        StartShake();
    }

    // ── IEnemyView: 수치/텍스트(HUD 위임) ──
    public void UpdateActionGauge(float ratio) { if (_hud != null) _hud.SetGauge(ratio); }
    public void SetAttackPreviewDamage(int damage) { if (_hud != null) _hud.SetPreview(damage); }
    public void ShowDamage(float damage) => ShowDamage(damage, false);
    public void ShowDamage(float damage, bool isBurn) { if (_hud != null) _hud.ShowDamage(damage, isBurn); }
    public void ShowMidPatternNotice(string message) { if (_hud != null) _hud.ShowNotice(message); }

    // ── IEnemyView: 연출 ──

    // 카드 타입별 월드 피격 파티클 재생
    public void PlayHitEffect(string cardType)
    {
        ParticleSystem ps = cardType switch
        {
            "Q" => qEffect, "W" => wEffect, "E" => eEffect, "R" => rEffect, "L" => lEffect, _ => null
        };
        if (ps != null) ps.Play();
    }

    // 공격 모션 트리거(임팩트는 Animation Event → OnAttackImpact)
    public void PlayAttackMotion()
    {
        if (animator != null && !string.IsNullOrEmpty(attackTrigger)) animator.SetTrigger(attackTrigger);
    }

    // Animation Event(공격 클립 임팩트 프레임)에서 호출 — 공격 파티클 재생 + 동기화 이벤트 발생
    public void OnAttackImpact()
    {
        if (attackVfx != null) { attackVfx.Stop(); attackVfx.Play(); }
        OnAttackMotionLastFrame?.Invoke();
    }

    public bool HasAttackMotion => animator != null; // 공격 애니 보유(임팩트 이벤트 사용) 여부
    public float HitReactionRemaining => Mathf.Max(0f, _hitReactionEndTime - Time.time);
    public Transform ShakeTarget => shakeTarget != null ? shakeTarget : transform;

    // ── IEnemyView: 사망 ──

    // 사망 연출 시작 — Die 트리거(있으면) + 축소 페이드
    public void PlayDeath()
    {
        if (_dead) return;
        _dead = true;
        if (hasDieState && animator != null && !string.IsNullOrEmpty(dieTrigger))
            animator.SetTrigger(dieTrigger);
        StartCoroutine(DeathShrink());
    }

    public float DeathDuration => deathDuration;

    // 사망 시 모델을 축소해 사라지게 한다(Die 클립 부재 시 시각적 소멸 보장)
    IEnumerator DeathShrink()
    {
        Transform t = shakeTarget != null ? shakeTarget : transform;
        Vector3 from = t.localScale;
        // Die 애니가 있으면 대부분 재생시키고 마지막 구간에만 축소, 없으면 전체 구간 축소.
        float shrinkStart = hasDieState ? deathDuration * 0.6f : 0f;
        float elapsed = 0f;
        while (elapsed < deathDuration)
        {
            elapsed += Time.deltaTime;
            if (elapsed >= shrinkStart)
            {
                float k = Mathf.InverseLerp(shrinkStart, deathDuration, elapsed);
                t.localScale = Vector3.Lerp(from, Vector3.zero, k);
            }
            yield return null;
        }
    }

    // ── IEnemyView: 비주얼 주입(3D는 프리팹에 baked이므로 no-op) ──
    public void SetSprite(Sprite sprite) { }
    public void SetHitSprite(Sprite sprite) { }
    public void SetAttackSprites(Sprite[] sprites) { }

    // ── 플래시 ──
    void StartFlash()
    {
        if (flashRenderers == null || flashRenderers.Length == 0 || flashDuration <= 0f) return;
        if (_flashCo != null) StopCoroutine(_flashCo);
        _flashCo = StartCoroutine(FlashRoutine());
    }

    IEnumerator FlashRoutine()
    {
        float t = 0f;
        while (t < flashDuration)
        {
            t += Time.deltaTime;
            float k = 1f - (t / flashDuration); // 강→약
            ApplyFlash(k * flashStrength);
            yield return null;
        }
        ApplyFlash(0f); // 기준 색 복귀
        _flashCo = null;
    }

    // 각 렌더러에 기준색↔플래시색을 strength 만큼 섞어 적용
    void ApplyFlash(float strength)
    {
        if (flashRenderers == null) return;
        for (int i = 0; i < flashRenderers.Length; i++)
        {
            var r = flashRenderers[i];
            if (r == null || r.sharedMaterial == null || !r.sharedMaterial.HasProperty(colorProperty)) continue;
            Color baseC = i < _baseColors.Count ? _baseColors[i] : Color.white;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(colorProperty, Color.Lerp(baseC, hitFlashColor, Mathf.Clamp01(strength)));
            r.SetPropertyBlock(_mpb);
        }
    }

    // ── 흔들기 ──
    void StartShake()
    {
        if (shakeDuration <= 0f || shakeMagnitude <= 0f) return;
        Transform t = shakeTarget != null ? shakeTarget : transform;
        if (_shakeCo != null) { StopCoroutine(_shakeCo); t.localPosition = _shakeBasePos; }
        else _shakeBasePos = t.localPosition;
        _shakeCo = StartCoroutine(ShakeRoutine(t));
    }

    IEnumerator ShakeRoutine(Transform t)
    {
        float e = 0f;
        while (e < shakeDuration)
        {
            e += Time.deltaTime;
            float damp = 1f - (e / shakeDuration);
            float mag = shakeMagnitude * damp;
            t.localPosition = _shakeBasePos + new Vector3(
                UnityEngine.Random.Range(-mag, mag), UnityEngine.Random.Range(-mag, mag), 0f);
            yield return null;
        }
        t.localPosition = _shakeBasePos;
        _shakeCo = null;
    }
}
