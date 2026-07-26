using System;
using System.Collections;
using UnityEngine;
using Coffee.UIExtensions;
using Battle.Card;

public class EnemyController : MonoBehaviour, IBattleUnit
{
    [Header("공격 설정")]
    [SerializeField] private int fallbackGaugeFullDamage = 10; // 게이지 가득 시 폴백 피해
    [SerializeField] private ParticleSystem hitVFX; // 적 공격 파티클
    [Tooltip("적 공격 파티클(hitVFX)을 UI 위에 렌더(UIParticle)할 때 배율. 안 보이면 키우고, 너무 크면 줄이세요(플레이로 튜닝).")]
    [SerializeField] private float hitVfxUiScale = 100f; // 공격 파티클 UI 배율
    [Tooltip("적 공격 파티클(hitVFX) 재생 속도 배율 — 1 미만이면 더 천천히(=더 오래) 보인다. 순식간에 사라질 때 낮춰라.")]
    [Range(0.2f, 2f)]
    [SerializeField] private float hitVfxPlaybackSpeed = 0.7f; // 공격 파티클 재생 속도 배율

    [Header("새 전투 시스템 공격 시퀀스 (폴백)")]
    [Tooltip("EnemyData.attackSequence(엑셀 1차/2차/3차 공격)가 비어 있을 때만 사용하는 폴백 시퀀스.")]
    [SerializeField] private int[] newSystemAttackSequence = { 8, 9, 20 }; // 새 시스템 공격 피해 시퀀스(폴백)
    private int _newSystemAttackIndex = 0; // 현재 공격 시퀀스 인덱스

    // 실제 사용할 공격 시퀀스: EnemyData.attackSequence 우선, 비어 있으면 인스펙터 폴백
    int[] EffectiveAttackSequence()
    {
        var seq = stat != null && stat.Data != null ? stat.Data.attackSequence : null;
        if (seq != null && seq.Length > 0) return seq;
        return newSystemAttackSequence;
    }
    private bool _nextAttackBuffed = false; // 다음 공격 +50% 강화 여부

    private EnemyStat stat; // 적 스탯 컴포넌트
    private EnemyView view; // 적 뷰 컴포넌트
    private Player player; // 플레이어 참조
    private RoundManager roundManager; // 라운드 매니저 참조
    private bool isDead = false; // 사망 처리 여부
    private MonsterMidPattern midPattern; // 방해행동 패턴 컴포넌트
    private bool _attackPreviewInitialized; // 공격 예고 초기화 여부

    // ── 특이사항(EnemySpecialAbility) 상태 ──
    private const int CYCLE_CURSE_USES = 99; // 백족승 저주가 다음 전환 전까지 유지되도록 큰 값
    private static readonly CardElement[] CurseCycleElements =
        { CardElement.Fire, CardElement.Water, CardElement.Wind, CardElement.Earth }; // 백족승 저주 순환 속성
    private EnemySpecialAbility _ability = EnemySpecialAbility.None; // 이 몬스터의 특이사항
    private bool _abilityResolved = false;   // 특이사항 조회 완료 여부
    private bool _specialBattleStarted = false; // 전투시작 특이사항 1회 트리거 여부
    private bool _halfHpTriggered = false;   // 체력 절반 특이사항 1회 트리거 여부
    private int _escalateBonus = 0;          // 풍식귀: 누적 공격 증가량
    private int _cycleCurseIndex = 0;        // 백족승: 현재 저주 속성 인덱스
    public bool AllowDuplication = true;     // 액괴 복제 허용(복제된 사본은 false로 무한 복제 방지)

    // 특이사항 조회(EnemyStat.Initialize 이후 데이터 준비되면 1회 확정)
    void ResolveAbility()
    {
        if (_abilityResolved) return;
        if (stat == null || stat.Data == null) return;
        _ability = stat.Data.specialAbility;
        _abilityResolved = true;
    }

    // 스탯/뷰 컴포넌트를 캐싱한다
    private void Awake()
    {
        stat = GetComponent<EnemyStat>();
        view = GetComponent<EnemyView>();
    }

    // 참조를 해결하고 이벤트 구독 및 공격 파티클을 설정한다
    private void Start()
    {
        player = Player.Resolve(true);
        roundManager = FindFirstObjectByType<RoundManager>();

        stat.OnDied += HandleDeath;
        stat.OnMidPattern  += HandleMidPattern;
        stat.OnGaugeFull   += HandleGaugeFull;
        stat.OnGaugeStepChanged += view.UpdateActionGauge;
        stat.OnDamaged += HandleSpecialDamaged;   // 특이사항(화웅): 피격 반사
        stat.OnHpChanged += HandleSpecialHpChanged; // 특이사항(액괴/무사): 체력 절반
        midPattern = GetComponent<MonsterMidPattern>();

        // 공격 모션이 마지막(임팩트) 프레임에 도달할 때 공격 이펙트를 한 번 더 재생
        if (view != null) view.OnAttackMotionLastFrame += PlayAttackVfx;

        // 적 공격 파티클(hitVFX)이 Screen Space - Overlay UI 뒤로 가려지지 않도록 UIParticle로
        // UI 레이어 안에서 렌더한다. (월드 ParticleSystem은 어떤 sortingOrder로도 Overlay UI 위로 못 올라옴 →
        // CardEffectOverlay와 동일하게 UIParticle 사용)
        if (hitVFX != null)
        {
            var uip = hitVFX.GetComponent<UIParticle>();
            if (uip == null) uip = hitVFX.gameObject.AddComponent<UIParticle>();
            uip.scale = hitVfxUiScale;
            uip.RefreshParticles();
            // UI 공간에서 보이도록 모든 파티클을 Local 시뮬레이션으로 강제(World면 화면 밖에서 터질 수 있음)
            var plist = uip.particles;
            if (plist != null)
            {
                for (int i = 0; i < plist.Count; i++)
                {
                    var ps = plist[i];
                    if (ps == null) continue;
                    var m = ps.main;
                    m.simulationSpace = ParticleSystemSimulationSpace.Local;
                    m.simulationSpeed = Mathf.Max(0.01f, hitVfxPlaybackSpeed); // 더 천천히 재생해 오래 보이게
                }
            }
        }
    }

    // 이벤트 구독을 해제한다
    void OnDestroy()
    {
        stat.OnDied -= HandleDeath;
        stat.OnMidPattern  -= HandleMidPattern;
        stat.OnGaugeFull   -= HandleGaugeFull;
        stat.OnGaugeStepChanged -= view.UpdateActionGauge;
        stat.OnDamaged -= HandleSpecialDamaged;
        stat.OnHpChanged -= HandleSpecialHpChanged;
        if (view != null) view.OnAttackMotionLastFrame -= PlayAttackVfx;
    }

    // 새 시스템 공격 예고를 한 번 지연 초기화한다
    void Update()
    {
        if (!stat.IsAlive) return;

        // NewBattleController가 적 스폰 직후엔 아직 Instance가 없을 수 있어 한 번만 지연 갱신
        if (!_attackPreviewInitialized && Battle.NewBattleController.Instance != null)
        {
            UpdateAttackPreviewForNewSystem();
            _attackPreviewInitialized = true;
        }

        // 전투 시작 특이사항(구미호 매혹/백족승 저주)을 전투 준비(덱 포함) 완료 후 1회 트리거
        if (!_specialBattleStarted
            && Battle.NewBattleController.Instance != null
            && Battle.NewBattleController.Instance.InBattle)
        {
            _specialBattleStarted = true;
            TriggerBattleStartAbility();
        }
    }

    // 전투 시작 특이사항 트리거
    void TriggerBattleStartAbility()
    {
        ResolveAbility();
        var nbc = Battle.NewBattleController.Instance;
        if (nbc == null) return;
        if (_ability == EnemySpecialAbility.CharmCardsOnBattleStart)
            nbc.InsertCharmCards(7); // 구미호: 매혹 7장
        else if (_ability == EnemySpecialAbility.CycleElementCurse)
        {
            _cycleCurseIndex = UnityEngine.Random.Range(0, CurseCycleElements.Length);
            nbc.ApplyElementCurse(CurseCycleElements[_cycleCurseIndex], CYCLE_CURSE_USES); // 백족승: 초기 저주
        }
    }

    // 특이사항(화웅): 피격 시 플레이어에게 1 피해 반사
    void HandleSpecialDamaged(float dmg)
    {
        ResolveAbility();
        if (_ability != EnemySpecialAbility.ReflectDamageOnHit) return;
        if (player == null) player = Player.Resolve(true);
        player?.TakeDamage(1f, "enemy_reflect");
    }

    // 특이사항(액괴/무사): 체력 절반 도달 시 1회 트리거
    void HandleSpecialHpChanged(float current, float max)
    {
        ResolveAbility();
        if (_halfHpTriggered || max <= 0f) return;
        if (!(current > 0f && current <= max * 0.5f)) return;

        if (_ability == EnemySpecialAbility.DuplicateAtHalfHp)
        {
            _halfHpTriggered = true;
            if (AllowDuplication && roundManager != null && stat.Data != null)
            {
                roundManager.RequeueEnemyCopy(stat.Data);
                view?.ShowMidPatternNotice("복제!");
            }
        }
        else if (_ability == EnemySpecialAbility.DoubleDamageAtHalfHp)
        {
            _halfHpTriggered = true; // 이후 공격 2배(HandleGaugeFull에서 적용)
            view?.ShowMidPatternNotice("피해 2배!");
        }
    }

    // 사망 시 특이사항(오염된 원소 4종) 트리거
    void TriggerOnDeathAbility()
    {
        ResolveAbility();
        var nbc = Battle.NewBattleController.Instance;
        if (nbc == null) return;
        switch (_ability)
        {
            case EnemySpecialAbility.OnDeathCurseElement: nbc.CurseRandomElement(5); break; // 오염된 불
            case EnemySpecialAbility.OnDeathDiscardCards: nbc.ForcePlayerDiscard(2); break;  // 오염된 물
            case EnemySpecialAbility.OnDeathResetAwaken:  nbc.ResetAwakenGauge(); break;     // 오염된 바람
            case EnemySpecialAbility.OnDeathInsertExhaust: nbc.InsertExhaustCards(2); break; // 오염된 땅
        }
    }

    // 새 시스템 모드에서 다음 공격 시퀀스 값을 뷰에 미리 표시한다
    void UpdateAttackPreviewForNewSystem()
    {
        if (Battle.NewBattleController.Instance == null) return;
        int[] seq = EffectiveAttackSequence();
        if (seq == null || seq.Length == 0) return;
        if (view == null) return;

        int nextDamage = seq[_newSystemAttackIndex % seq.Length];

        // 실제 공격 계산(HandleGaugeFull)과 동일하게 예고에도 데미지 수정 효과를 반영한다
        ResolveAbility();
        if (_ability == EnemySpecialAbility.EscalatingAttack)
            nextDamage += _escalateBonus;                       // 풍식귀: 이번 공격에 더해질 누적 증가
        if (_nextAttackBuffed)
            nextDamage = Mathf.CeilToInt(nextDamage * 1.5f);    // 방해행동-강화: 다음 공격 +50%
        if (_ability == EnemySpecialAbility.DoubleDamageAtHalfHp && _halfHpTriggered)
            nextDamage *= 2;                                    // 무사: 체력 절반 도달 후 2배

        view.SetAttackPreviewDamage(nextDamage);
    }

    // 카드 타입을 고려해 적에게 피해를 가한다
    public void TakeDamage(float damage, string cardtype = "normal")
    {
        if (!stat.IsAlive) return;

        // 실제 피해
        if (damage > 0)
        {
            if (player == null)
                player = Player.Resolve(true);

            if (player != null && player.GetStatus("launcher") > 0)
            {
                stat.TakeDamage(player.attackDamage / 10f);
                view.PlayHitEffect("L");
                player.AddStatus("launcher", -1);
            }
            stat.TakeDamage(damage);
            view.ShowDamage(damage);
            view.PlayHitEffect(cardtype);
        }
    }

    // 기본 카드 타입으로 피해를 가한다
    public void TakeDamage(float damage)
    {
        TakeDamage(damage, "Default");
    }

    public event Action<string, int> OnStatusChanged; // 상태이상 변경 이벤트
    public event Action<string, int> OnStatusApplied; // 상태이상 '부여'(양수 증가) 이벤트 — 연출용
    // 상태이상 스택을 더하고 변경 신호를 보낸다
    public void AddStatus(string type, int amount)
    {
        if (!stat.statusEffects.ContainsKey(type)) return;
        if (type == "freeze" && stat.statusEffects["wet"] > 0)
        {
            stat.isNewlyFrozen = true;
        }

        // 기획서 0.6v: 스택형 키워드 최대 999
        stat.statusEffects[type] = Mathf.Clamp(stat.statusEffects[type] + amount, 0, 999);

        // 일반적인 상태이상 추가 시 갱신 신호 발송
        OnStatusChanged?.Invoke(type, stat.statusEffects[type]);

        // 양수 부여일 때만 '부여' 신호(빙결 등 연출 트리거용)
        if (amount > 0) OnStatusApplied?.Invoke(type, amount);
    }

    // 상태이상 스택 값을 조회한다
    public int GetStatus(string type)
    {
        return stat.statusEffects.ContainsKey(type) ? stat.statusEffects[type] : 0;
    }

    // 상태이상 스택을 지정 값으로 설정한다
    public void SetStatus(string type, int amount)
    {
        if (stat.statusEffects.ContainsKey(type))
        {
            int v = Mathf.Clamp(amount, 0, 999); // 기획서 0.6v: 스택 최대 999
            stat.statusEffects[type] = v;
            OnStatusChanged?.Invoke(type, v);
        }
    }
    // 적 공격력을 반환한다(다음 공격 시퀀스 값 기준, 없으면 폴백 상수)
    public float GetAttackDamage()
    {
        int[] seq = EffectiveAttackSequence();
        if (seq != null && seq.Length > 0)
            return seq[_newSystemAttackIndex % seq.Length];
        return fallbackGaugeFullDamage;
    }

    // 방어도를 더한다
    public void AddGuard(float amount)
    {
        stat.guard = Mathf.Clamp(stat.guard + amount, 0f, 999f); // 기획서 0.6v: 방어도 최대 999
    }
    // 플레이어가 슬롯을 사용할 때마다 호출 — 게이지를 한 단계 소비한다
    public void OnPlayerAction()
    {
        stat.ConsumeGaugeStep();
    }

    // 방해행동을 실행하고 알림을 표시한다
    void HandleMidPattern()
    {
        string patternMessage = null;

        // 새 전투 시스템: NewBattleController가 방해 행동 처리
        if (Battle.NewBattleController.Instance != null)
        {
            patternMessage = Battle.NewBattleController.Instance.TriggerDisruption();
        }
        // FCM 시스템이 있으면 사용, 없으면 기존 랜덤 폴백
        else if (midPattern != null)
        {
            patternMessage = midPattern.Execute();
        }
        else
        {
            patternMessage = ElementSlotSystem.Instance?.TriggerDisruptionPattern();
        }

        if (string.IsNullOrEmpty(patternMessage))
            patternMessage = "패턴 발동";

        view?.ShowMidPatternNotice(patternMessage);
    }

    // 기획서 0.6v [방해행동-강화]: 다음 적 공격 1회를 +50% 강화한다
    public void BuffNextAttack()
    {
        _nextAttackBuffed = true;
        UpdateAttackPreviewForNewSystem(); // 강화 발동 즉시 공격 예고에 +50% 반영
    }
    public bool IsNextAttackBuffed => _nextAttackBuffed; // 다음 공격 강화 적용 여부

    // 게이지 가득 시 화상/강화를 처리하고 적 공격을 실행한다
    void HandleGaugeFull()
    {
        midPattern?.OnGauge10();
        if (player == null)
            player = Player.Resolve(true);

        // 새 전투 시스템: 적 공격 피격 처리 전 화상 발동 (PDF 명세)
        if (Battle.NewBattleController.Instance != null)
        {
            ApplyBurnBeforeAttack();
            if (!stat.IsAlive) return; // 화상으로 적이 사망하면 공격하지 않음
        }

        int damagePerHit;
        int hitCount;

        int[] atkSeq = EffectiveAttackSequence();
        if (Battle.NewBattleController.Instance != null && atkSeq != null && atkSeq.Length > 0)
        {
            // 엑셀 1차/2차/3차 공격을 순차 반복(비어 있으면 인스펙터 폴백 시퀀스)
            damagePerHit = atkSeq[_newSystemAttackIndex % atkSeq.Length];
            hitCount = 1;
            _newSystemAttackIndex++;
            Debug.Log($"[적 공격] 시퀀스 인덱스={_newSystemAttackIndex - 1}, 피해={damagePerHit}");
        }
        else
        {
            // 폴백(신 시스템 미가동/시퀀스 없음): 상수 피해 1회
            damagePerHit = fallbackGaugeFullDamage;
            hitCount = 1;
        }

        // 특이사항(풍식귀): 공격 시 피해가 1씩 누적 증가
        ResolveAbility();
        if (_ability == EnemySpecialAbility.EscalatingAttack)
        {
            damagePerHit += _escalateBonus;
            _escalateBonus++;
        }

        // 기획서 0.6v [방해행동-강화]: 다음 공격 1회 한정 +50% (소수 올림)
        if (_nextAttackBuffed)
        {
            damagePerHit = Mathf.CeilToInt(damagePerHit * 1.5f);
            _nextAttackBuffed = false;
            view?.ShowMidPatternNotice("강화!");
            Debug.Log($"[적 강화] 다음 공격 +50% → {damagePerHit}");
        }

        // 특이사항(무사): 체력 절반 도달 후 피해 2배
        if (_ability == EnemySpecialAbility.DoubleDamageAtHalfHp && _halfHpTriggered)
            damagePerHit *= 2;

        if (player != null)
        {
            // 연타 연출을 위해 코루틴으로 분리하여 호출
            StartCoroutine(ExecuteMultiHit(hitCount, damagePerHit));
        }

        // 특이사항(백족승): 공격 시 다른 속성으로 저주 전환
        if (_ability == EnemySpecialAbility.CycleElementCurse && Battle.NewBattleController.Instance != null)
        {
            _cycleCurseIndex = (_cycleCurseIndex + 1) % CurseCycleElements.Length;
            Battle.NewBattleController.Instance.ApplyElementCurse(CurseCycleElements[_cycleCurseIndex], CYCLE_CURSE_USES);
        }

        stat.RollNewAttackPlan();

        // 새 시스템: 다음 공격 시퀀스 값을 미리 표시
        UpdateAttackPreviewForNewSystem();
    }

    // PDF [화상]: 적 공격 전 화상 스택만큼 고정피해를 주고 화상을 절반으로 줄인다
    void ApplyBurnBeforeAttack()
    {
        if (stat == null) return;
        if (!stat.statusEffects.TryGetValue("burn", out int burn)) return;
        if (burn <= 0) return;

        stat.TakeDamage(burn);

        // 데미지 숫자(화상 색) + 화상 알림 텍스트
        if (view != null)
        {
            view.ShowDamage(burn, isBurn: true);
            view.ShowMidPatternNotice($"화상 {burn}!");
        }

        // 불126: 화상이 적 행동 시에도 줄어들지 않음 — 활성 시 절반 감소 생략.
        bool burnPersists = Battle.NewBattleController.Instance != null
            && Battle.NewBattleController.Instance.BurnPersistsOnEnemyTurn;
        int after = burnPersists ? burn : burn / 2;
        stat.statusEffects["burn"] = after;
        OnStatusChanged?.Invoke("burn", after);
        Debug.Log($"[화상] 적 {burn} 고정피해, 잔여 화상={after}{(burnPersists ? " (유지)" : "")}");
    }
    // 공격 모션과 함께 지정 횟수만큼 연타 피해를 가한다
    IEnumerator ExecuteMultiHit(int count, int damage)
    {
        view?.PlayAttackMotion(); // 공격 모션(이미지 교체) 재생 — 피격 방식과 동일 구조
        for (int i = 0; i < count; i++)
        {
            player.TakeDamage(damage, "enemy_attack");
            // 공격 모션이 있으면 마지막(임팩트) 프레임에서만 이펙트(이벤트로 처리). 모션이 없을 때만 시작 시 폴백 재생.
            if (view == null || !view.HasAttackMotion) PlayAttackVfx();

            // 타격 사이의 짧은 간격 (0.1~0.15초 정도가 적당합니다)
            yield return new WaitForSeconds(0.15f);
        }
        Debug.Log($"[EnemyController] {count} hit");
    }

    // 적 공격 파티클을 활성화 보장 후 재생한다
    void PlayAttackVfx()
    {
        if (hitVFX == null) return;
        // VFX 오브젝트가 비활성 상태면 Play()해도 보이지 않으므로 먼저 활성화 보장(공격 이펙트 누락 방지)
        if (!hitVFX.gameObject.activeSelf) hitVFX.gameObject.SetActive(true);
        hitVFX.Stop();
        hitVFX.Play();
    }
    // 사망 처리 후 피격 연출이 끝나면 적을 제거한다
    void HandleDeath()
    {
        if (isDead) return;
        isDead = true;
        TriggerOnDeathAbility(); // 특이사항(오염된 원소 4종): 처치 시 페널티
        midPattern?.OnBattleEnd();
        // OnDied 이벤트로 인해 RoundManager.HandleEnemyDied가 호출됨
        // 여기서 직접 호출하지 않음 (중복 호출 방지)

        // 피격 연출(빨강 플래시 + 흔들기)이 끝난 뒤에 사라지도록 대기 후 제거.
        StartCoroutine(DestroyAfterHitReaction());
    }

    // 피격 연출이 끝날 때까지 대기한 뒤 오브젝트를 파괴한다
    IEnumerator DestroyAfterHitReaction()
    {
        float wait = view != null ? view.HitReactionRemaining : 0f;
        if (wait > 0f) yield return new WaitForSeconds(wait);
        Destroy(gameObject);
    }

}
