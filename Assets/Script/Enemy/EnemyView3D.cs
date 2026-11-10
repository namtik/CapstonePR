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

    [Header("동작별 클립(여러 개면 재생 시 랜덤)")]
    [Tooltip("동작마다 해당하는 Animator 상태 이름을 나열. 2개 이상이면 재생 때 랜덤으로 하나 선택.\n" +
             "비워두면 그 동작은 위의 트리거(Hit/Attack/Die)로 폴백. 버프/디버프는 여기에만 채운다.")]
    [SerializeField] private MotionStates[] motionStates;   // 동작→상태 이름 목록
    [Tooltip("CrossFade 블렌드 시간(초). 0이면 즉시 전환.")]
    [SerializeField] private float motionCrossFade = 0.1f;  // 동작 전환 블렌드 시간

    // 동작 종류별로 재생 가능한 Animator 상태 이름들. 인스펙터 배열을 Awake에서 딕셔너리로 캐싱.
    [Serializable]
    public struct MotionStates
    {
        public EnemyMotion motion;      // 동작 종류
        public string[] stateNames;     // 이 동작에 해당하는 Animator 상태 이름들(1개 이상 시 랜덤)
    }
    private readonly Dictionary<EnemyMotion, string[]> _motionMap = new Dictionary<EnemyMotion, string[]>();

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

    [Header("공격 히트스톱(임팩트 순간 정지 후 재개)")]
    [Tooltip("공격 임팩트 순간 모션을 멈췄다가 다시 진행시키는 연출 사용 여부.")]
    [SerializeField] private bool enableHitStop = true;
    [Tooltip("임팩트 시점(공격 상태의 정규화 시간 0~1). 클립에 Animation Event 'OnAttackImpact'가 있으면 그쪽이 우선.")]
    [Range(0f, 1f)] [SerializeField] private float attackImpactNormalizedTime = 0.45f;
    [Tooltip("공격 상태를 못 찾을 때(트리거 폴백 등) 공격 시작부터 임팩트까지 대기 시간(초).")]
    [SerializeField] private float attackImpactFallbackDelay = 0.35f;
    [Tooltip("임팩트를 끝까지 못 찾으면 이 시간(초) 뒤 강제로 임팩트 처리(피해 누락 방지).")]
    [SerializeField] private float attackImpactTimeout = 1.5f;
    [Tooltip("정지 시간(초, 실시간). 0.15~0.3 권장 — 너무 짧으면 카메라 연출에 묻혀 정지가 안 느껴짐.")]
    [SerializeField] private float hitStopDuration = 0.22f;
    [Tooltip("정지 중 애니메이션 속도. 0=완전 정지, 0.05 정도면 미세하게 밀리는 느낌.")]
    [Range(0f, 0.3f)] [SerializeField] private float hitStopAnimSpeed = 0f;
    [Tooltip("정지가 풀린 직후 애니메이션 가속 배율(휘두름이 '터져 나가는' 느낌).")]
    [SerializeField] private float resumeSpeedBoost = 1.8f;
    [Tooltip("재개 가속 유지 시간(초, 실시간).")]
    [SerializeField] private float resumeBoostDuration = 0.12f;
    [Tooltip("정지 중 모델 미세 떨림 세기(월드 유닛). 0이면 끔.")]
    [SerializeField] private float hitStopTremble = 0.05f;
    [Tooltip("정지 순간 모델을 번쩍이게 할 색(정지가 눈에 띄게). 알파 0이면 끔.")]
    [SerializeField] private Color hitStopFlashColor = new Color(1f, 1f, 1f, 1f);
    [Range(0f, 1f)] [SerializeField] private float hitStopFlashStrength = 0.55f;
    [Tooltip("임팩트 판정 경로(이벤트/정규화시간/지연/타임아웃)와 시점을 콘솔에 출력.")]
    [SerializeField] private bool logHitStop = true;
    [Tooltip("Animation Event 'PauseMotion'의 Float 값이 0 이하일 때 쓰는 기본 정지 시간(초, 실시간).")]
    [SerializeField] private float pauseMotionDefault = 0.2f;
    [Tooltip("임팩트 순간 카메라 펀치(줌인·초점 쏠림·흔들기).")]
    [SerializeField] private HitStopDirector.CameraPunch cameraPunch = HitStopDirector.CameraPunch.Default;

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
    private bool _followWorld;            // 여러 마리일 때 HUD를 발 아래에 붙일지
    private float _barScale = 1f;         // 양옆 적은 0.6, 가운데는 1
    private float _hitReactionEndTime;    // 피격 연출 종료 시각
    private Vector3 _shakeBasePos;        // 흔들기 기준 위치
    private Coroutine _shakeCo;           // 흔들기 코루틴
    private Coroutine _flashCo;           // 플래시 코루틴
    private readonly List<Color> _baseColors = new List<Color>(); // 렌더러별 기준 색
    private MaterialPropertyBlock _mpb;   // 플래시용 프로퍼티 블록
    private bool _dead;                   // 사망 처리 여부

    private Coroutine _impactWatchCo;     // 공격 임팩트 감시 코루틴
    private Coroutine _hitStopCo;         // 히트스톱(정지 후 재개) 코루틴
    private bool _impactPending;          // 이번 공격의 임팩트가 아직 발생 전인지
    private float _baseAnimSpeed = 1f;    // 히트스톱 전 Animator 속도(복귀용)
    private bool _hitStopActive;          // 히트스톱 진행 중 여부
    private Coroutine _pauseCo;           // PauseMotion(애니메이션 이벤트) 정지 코루틴
    private bool _pauseActive;            // PauseMotion 정지 중 여부
    private float _pauseBaseSpeed = 1f;   // PauseMotion 전 Animator 속도(복귀용)
    private string _attackState;          // 재생 중인 공격 상태 이름(트리거 폴백이면 null)
    private float _attackStartTime = -1f; // 공격 모션 시작 시각

    // 스탯 이벤트 구독, 참조 캐싱
    void Awake()
    {
        _stat = GetComponent<EnemyStat>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        // Animator가 자식에 있으면 Animation Event(OnAttackImpact)가 이 컴포넌트에 닿지 않으므로 중계기를 붙인다.
        if (animator != null && animator.gameObject != gameObject)
        {
            var relay = animator.GetComponent<EnemyAnimationEventRelay>()
                        ?? animator.gameObject.AddComponent<EnemyAnimationEventRelay>();
            relay.Bind(this);
        }
        if (shakeTarget == null) shakeTarget = animator != null ? animator.transform : transform;
        if (flashRenderers == null || flashRenderers.Length == 0)
            flashRenderers = GetComponentsInChildren<Renderer>(true);

        _mpb = new MaterialPropertyBlock();
        CacheBaseColors();
        BuildMotionMap();

        _stat.OnHpChanged += HandleHpChanged;
        _stat.OnDamaged += HandleDamaged;
    }

    // 인스펙터의 동작별 상태 배열을 조회용 딕셔너리로 캐싱(빈 항목은 제외)
    void BuildMotionMap()
    {
        _motionMap.Clear();
        if (motionStates == null) return;
        foreach (var m in motionStates)
        {
            if (m.stateNames != null && m.stateNames.Length > 0)
                _motionMap[m.motion] = m.stateNames;
        }
    }

    // 동작 애니메이션 재생. 등록된 상태가 있으면 랜덤 CrossFade, 없으면 폴백 트리거(있을 때만).
    // 반환값: CrossFade로 재생한 상태 이름(트리거 폴백/미재생이면 null).
    string PlayMotionInternal(EnemyMotion motion, string fallbackTrigger)
    {
        if (animator == null) return null;
        if (_motionMap.TryGetValue(motion, out var states) && states.Length > 0)
        {
            string state = states.Length == 1 ? states[0] : states[UnityEngine.Random.Range(0, states.Length)];
            animator.CrossFade(state, motionCrossFade);
            return state;
        }
        if (!string.IsNullOrEmpty(fallbackTrigger)) animator.SetTrigger(fallbackTrigger);
        return null;
    }

    // HUD 바인딩 및 초기 UI 반영(RoundManager가 이미 연결했으면 유지)
    void Start()
    {
        if (_hud == null) BindHud();
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
    // 여기서 Bind하면 아직 슬롯 HUD가 정해지기 전에 공용 Instance를 집어가
    // 양옆 적의 바가 풀린다. 카메라는 저장만 하고, 연결은 SetOverlayHud에서 한다.
    public void SetRenderCamera(Camera cam)
    {
        renderCamera = cam;
        if (_hud != null) BindHud(_followWorld);
    }

    // RoundManager가 몬스터별 HUD를 직접 연결한다.
    public void SetOverlayHud(EnemyOverlayHUD hud, bool followWorld, float barScale = 1f)
    {
        if (_hud != null && _hud != hud) _hud.Unbind();
        _hud = hud;
        _followWorld = followWorld;
        _barScale = barScale;
        BindHud(followWorld);
    }

    void BindHud(bool followWorld = false)
    {
        if (_hud == null) _hud = EnemyOverlayHUD.Instance;
        if (_hud == null)
            _hud = FindFirstObjectByType<EnemyOverlayHUD>(FindObjectsInactive.Include);

        if (_hud == null) return;

        Transform anchor = hudAnchor != null ? hudAnchor : transform;
        Camera cam = renderCamera != null ? renderCamera : Camera.main;
        _hud.Bind(anchor, cam, _stat != null ? _stat.GaugeMaxSteps : EnemyStat.GAUGE_MAX_STEPS, followWorld, _barScale);
        if (_stat != null) _hud.SetHp(_stat.currentHp, _stat.maxHp);
        _hud.SetGauge(0f);
        if (_pendingNote != null) _hud.SetSpecialNote(_pendingNote);
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
        CancelHitStop(); // 정지 중 피격되면 Hit 애니가 멈춘 채 재생되지 않도록 속도 복귀
        PlayMotionInternal(EnemyMotion.Hit, hitTrigger);
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

    // 공격 모션 재생 + 임팩트 감시 시작.
    // 임팩트 판정 우선순위: Animation Event 'OnAttackImpact', 없으면 상태 정규화 시간, 그래도 없으면 지연/타임아웃. 반드시 1회 발생.
    public void PlayAttackMotion()
    {
        CancelHitStop();
        // 이전 공격 임팩트가 아직 안 났으면 먼저 처리(대기 중인 피해가 사라지지 않게)
        if (_impactPending) FireAttackImpact();

        string state = PlayMotionInternal(EnemyMotion.Attack, attackTrigger);
        if (animator == null) return;
        _attackState = state;
        _attackStartTime = Time.time;

        _impactPending = true;
        if (_impactWatchCo != null) StopCoroutine(_impactWatchCo);
        _impactWatchCo = StartCoroutine(AttackImpactWatch(state));
    }

    // 공격 상태가 임팩트 시점에 도달하는지 감시(Animation Event가 먼저 오면 그쪽이 처리하고 종료)
    IEnumerator AttackImpactWatch(string state)
    {
        float start = Time.time;
        bool seenState = false;
        yield return null; // CrossFade/트리거가 반영될 한 프레임 대기

        string via = "timeout";
        float atNorm = -1f;
        while (_impactPending)
        {
            float elapsed = Time.time - start;
            if (elapsed >= attackImpactTimeout) { via = "timeout"; break; }

            if (!string.IsNullOrEmpty(state) && animator != null)
            {
                // 전이 중이면 다음 상태, 아니면 현재 상태에서 공격 상태를 찾는다
                AnimatorStateInfo info = animator.IsInTransition(0)
                    ? animator.GetNextAnimatorStateInfo(0)
                    : animator.GetCurrentAnimatorStateInfo(0);
                if (info.IsName(state))
                {
                    seenState = true;
                    atNorm = info.normalizedTime;
                    if (info.normalizedTime >= attackImpactNormalizedTime) { via = "normalizedTime"; break; }
                }
                else if (!seenState && elapsed >= attackImpactFallbackDelay * 2f)
                {
                    via = $"state-not-found('{state}')"; break; // 상태를 끝내 못 찾음: 지연 폴백
                }
            }
            else if (elapsed >= attackImpactFallbackDelay)
            {
                via = "fallbackDelay"; break; // 상태 이름을 모름(트리거 폴백): 고정 지연
            }
            yield return null;
        }

        _impactWatchCo = null;
        if (_impactPending)
        {
            if (logHitStop) Debug.Log($"[HitStop] {name} 임팩트 via={via} state={state} norm={atNorm:F2} t={Time.time - start:F2}s");
            FireAttackImpact();
        }
    }

    // 임팩트 1회 처리: 공격 VFX + 동기화 이벤트(피해 적용) + 히트스톱
    void FireAttackImpact()
    {
        if (!_impactPending) return;
        _impactPending = false;
        if (_impactWatchCo != null) { StopCoroutine(_impactWatchCo); _impactWatchCo = null; }

        if (attackVfx != null) { attackVfx.Stop(); attackVfx.Play(); }
        OnAttackMotionLastFrame?.Invoke();

        if (enableHitStop && !_dead && animator != null && isActiveAndEnabled)
        {
            CancelPause(); // 히트스톱이 우선(정지 속도가 겹쳐 기준 속도를 잃지 않게)
            if (_hitStopCo != null) StopCoroutine(_hitStopCo);
            _hitStopCo = StartCoroutine(HitStopRoutine());
        }
    }

    // 정지, 재개(가속), 원속도 순서. 실시간 기준이라 일시정지 메뉴와 무관.
    IEnumerator HitStopRoutine()
    {
        if (!_hitStopActive) _baseAnimSpeed = animator.speed;
        _hitStopActive = true;

        HitStopDirector.Punch(ImpactFocusWorld(), cameraPunch);

        // 정지 + 번쩍임 + 미세 떨림
        animator.speed = hitStopAnimSpeed;
        bool doFlash = hitStopFlashColor.a > 0f && hitStopFlashStrength > 0f && _flashCo == null;
        Transform t = shakeTarget != null ? shakeTarget : transform;
        bool tremble = hitStopTremble > 0f && _shakeCo == null;
        Vector3 basePos = t.localPosition;
        float e = 0f;
        while (e < hitStopDuration)
        {
            e += Time.unscaledDeltaTime;
            float k01 = Mathf.Clamp01(e / Mathf.Max(0.0001f, hitStopDuration));
            if (doFlash && _flashCo == null)
                ApplyFlash(hitStopFlashStrength * (1f - k01) * (1f - k01), hitStopFlashColor);
            if (tremble && _shakeCo == null)
            {
                float m = hitStopTremble * (1f - e / Mathf.Max(0.0001f, hitStopDuration));
                t.localPosition = basePos + new Vector3(
                    UnityEngine.Random.Range(-m, m), UnityEngine.Random.Range(-m, m), 0f);
            }
            yield return null;
        }
        if (tremble && _shakeCo == null) t.localPosition = basePos;
        if (doFlash && _flashCo == null) ApplyFlash(0f);

        // 재개 가속(휘두름이 터져 나가는 느낌)
        animator.speed = _baseAnimSpeed * Mathf.Max(1f, resumeSpeedBoost);
        e = 0f;
        while (e < resumeBoostDuration)
        {
            e += Time.unscaledDeltaTime;
            yield return null;
        }

        animator.speed = _baseAnimSpeed;
        _hitStopActive = false;
        _hitStopCo = null;
    }

    // Animation Event 'PauseMotion' — 이 프레임에서 모션만 seconds초 멈췄다가 이어간다(카메라 연출·피해 없음).
    // 이벤트 Inspector의 Float 칸에 정지 시간(초)을 입력. 0 이하면 pauseMotionDefault 사용.
    public void PauseMotion(float seconds)
    {
        if (_dead || animator == null || !isActiveAndEnabled) return;
        if (_hitStopActive) return; // 히트스톱 중이면 무시
        float dur = seconds > 0f ? seconds : pauseMotionDefault;
        if (dur <= 0f) return;
        if (_pauseCo != null) StopCoroutine(_pauseCo);
        _pauseCo = StartCoroutine(PauseMotionRoutine(dur));
    }

    IEnumerator PauseMotionRoutine(float dur)
    {
        if (!_pauseActive) _pauseBaseSpeed = animator.speed;
        _pauseActive = true;
        animator.speed = 0f;
        float e = 0f;
        while (e < dur)
        {
            e += Time.unscaledDeltaTime;
            yield return null;
        }
        if (animator != null) animator.speed = _pauseBaseSpeed;
        _pauseActive = false;
        _pauseCo = null;
    }

    // 진행 중 PauseMotion 정지를 즉시 끝내고 Animator 속도를 복귀
    void CancelPause()
    {
        if (_pauseCo != null) { StopCoroutine(_pauseCo); _pauseCo = null; }
        if (_pauseActive && animator != null) animator.speed = _pauseBaseSpeed;
        _pauseActive = false;
    }

    // 진행 중 히트스톱(및 PauseMotion 정지)을 즉시 끝내고 Animator 속도를 복귀
    void CancelHitStop()
    {
        CancelPause();
        if (_hitStopCo != null) { StopCoroutine(_hitStopCo); _hitStopCo = null; }
        if (_hitStopActive && animator != null) animator.speed = _baseAnimSpeed;
        if (_hitStopActive && _flashCo == null) ApplyFlash(0f);
        _hitStopActive = false;
    }

    // 카메라 펀치 초점 — 모델 바운드 중심(없으면 흔들기 대상 위치)
    Vector3 ImpactFocusWorld()
    {
        if (TryGetAimBounds(out Bounds b)) return b.center;
        return shakeTarget != null ? shakeTarget.position : transform.position;
    }

    // 지정 동작 재생(버프/디버프 등). 등록된 클립이 여러 개면 랜덤 선택.
    public void PlayMotion(EnemyMotion motion)
    {
        if (_dead) return;
        PlayMotionInternal(motion, null); // 버프/디버프는 트리거 폴백 없음(리스트로만 구동)
    }

    // Animation Event(공격 클립 임팩트 프레임)에서 호출 — 공격 파티클 재생 + 동기화 이벤트 발생
    // (감시 코루틴보다 먼저 오면 이 타이밍이 임팩트가 된다. 같은 공격에서 중복 호출돼도 1회만 처리)
    public void OnAttackImpact()
    {
        if (_impactPending && logHitStop) Debug.Log($"[HitStop] {name} 임팩트 via=AnimationEvent");
        FireAttackImpact();
    }

    public bool HasAttackMotion => animator != null;

    // 공격 모션 재생 중 여부: 임팩트 대기/히트스톱 중이거나, 공격 상태가 아직 끝나지 않았으면 true
    public bool IsAttackMotionPlaying
    {
        get
        {
            if (_dead || animator == null || _attackStartTime < 0f) return false;
            if (_impactPending || _hitStopActive || _pauseActive) return true;
            if (Time.time - _attackStartTime > 5f) return false; // 안전 상한
            if (string.IsNullOrEmpty(_attackState))
                return Time.time - _attackStartTime < attackImpactFallbackDelay + 0.5f; // 상태 이름을 모르면 추정
            if (animator.IsInTransition(0))
                return animator.GetNextAnimatorStateInfo(0).IsName(_attackState)
                       || animator.GetCurrentAnimatorStateInfo(0).IsName(_attackState);
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            return info.IsName(_attackState) && info.normalizedTime < 1f;
        }
    } // 공격 애니 보유(임팩트 이벤트 사용) 여부
    public float HitReactionRemaining => Mathf.Max(0f, _hitReactionEndTime - Time.time);
    public Transform ShakeTarget => shakeTarget != null ? shakeTarget : transform;
    public RectTransform HpBarRect => _hud != null ? _hud.HpBarRect : null;

    string _pendingNote;

    public void SetSpecialNote(string note)
    {
        _pendingNote = note;
        if (_hud != null) _hud.SetSpecialNote(note);
    }

    // 카드 지정용 화면 좌표 — 렌더러 바운드 중심을 스테이지 카메라로 투영
    public bool TryGetAimScreen(out Vector2 screenCenter, out float screenRadius)
    {
        screenCenter = default;
        screenRadius = 0f;
        Camera cam = ResolveAimCamera();
        if (cam == null) return false;

        Vector3 world = transform.position;
        if (TryGetAimBounds(out Bounds bounds))
            world = bounds.center;
        else if (shakeTarget != null)
            world = shakeTarget.position;

        Vector3 sp = cam.WorldToScreenPoint(world);
        if (sp.z < 0f) return false;
        screenCenter = new Vector2(sp.x, sp.y);

        float raw = 160f;
        if (TryGetAimBounds(out bounds))
        {
            float maxd = 0f;
            Vector3 e = bounds.extents;
            Vector3[] pts =
            {
                bounds.center + new Vector3(e.x, 0f, 0f),
                bounds.center + new Vector3(-e.x, 0f, 0f),
                bounds.center + new Vector3(0f, e.y, 0f),
                bounds.center + new Vector3(0f, -e.y, 0f),
                bounds.center + new Vector3(0f, 0f, e.z),
                bounds.center + new Vector3(0f, 0f, -e.z),
            };
            for (int i = 0; i < pts.Length; i++)
            {
                Vector3 p = cam.WorldToScreenPoint(pts[i]);
                if (p.z < 0f) continue;
                maxd = Mathf.Max(maxd, Vector2.Distance(screenCenter, new Vector2(p.x, p.y)));
            }
            if (maxd > 1f) raw = maxd * 1.12f;
        }
        screenRadius = Mathf.Clamp(raw, 110f, 380f);
        return true;
    }

    // 콜라이더 레이캐스트 또는 화면 반경으로 지정 판정
    public bool ContainsAimPoint(Vector2 screenPos)
    {
        Camera cam = ResolveAimCamera();
        if (cam != null)
        {
            Ray ray = cam.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 250f)
                && (hit.transform == transform || hit.transform.IsChildOf(transform)))
                return true;
        }
        if (!TryGetAimScreen(out Vector2 center, out float radius)) return false;
        return Vector2.Distance(screenPos, center) <= (radius * 1.2f + 40f) * 1.3f;
    }

    Camera ResolveAimCamera()
    {
        if (renderCamera != null) return renderCamera;
        if (BattleStageController.Current != null && BattleStageController.Current.StageCamera != null)
            return BattleStageController.Current.StageCamera;
        return Camera.main;
    }

    bool TryGetAimBounds(out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        if (flashRenderers == null) return false;
        for (int i = 0; i < flashRenderers.Length; i++)
        {
            var r = flashRenderers[i];
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return any;
    }

    // ── IEnemyView: 사망 ──

    // 사망 연출 시작 — Die 트리거(있으면) + 축소 페이드
    public void PlayDeath()
    {
        if (_dead) return;
        _dead = true;
        CancelHitStop();
        if (_impactWatchCo != null) { StopCoroutine(_impactWatchCo); _impactWatchCo = null; }
        _impactPending = false; // 죽은 적의 공격 임팩트는 발생시키지 않음
        if (hasDieState) PlayMotionInternal(EnemyMotion.Die, dieTrigger);
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
    void ApplyFlash(float strength) => ApplyFlash(strength, hitFlashColor);

    void ApplyFlash(float strength, Color flashColor)
    {
        if (flashRenderers == null) return;
        for (int i = 0; i < flashRenderers.Length; i++)
        {
            var r = flashRenderers[i];
            if (r == null || r.sharedMaterial == null || !r.sharedMaterial.HasProperty(colorProperty)) continue;
            Color baseC = i < _baseColors.Count ? _baseColors[i] : Color.white;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(colorProperty, Color.Lerp(baseC, flashColor, Mathf.Clamp01(strength)));
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
