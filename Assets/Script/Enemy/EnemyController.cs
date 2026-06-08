using System;
using System.Collections;
using UnityEngine;

public class EnemyController : MonoBehaviour, IBattleUnit
{
    [Header("공격 설정")]
    [SerializeField] private int fallbackGaugeFullDamage = 10;
    [SerializeField] private ParticleSystem hitVFX;

    [Header("새 전투 시스템 공격 시퀀스 (PDF: 16-18-40 순환)")]
    [SerializeField] private int[] newSystemAttackSequence = { 8, 9, 20 };
    private int _newSystemAttackIndex = 0;
    // 기획서 0.6v [방해행동-강화]: 다음 공격 1회 한정 +50% 피해
    private bool _nextAttackBuffed = false;

    private EnemyStat stat;
    private EnemyView view;
    private Player player;
    private Roundmanager roundmanager;
    private bool isDead = false;
    private MonsterMidPattern midPattern;
    private bool _attackPreviewInitialized;

    private void Awake()
    {
        stat = GetComponent<EnemyStat>();
        view = GetComponent<EnemyView>();
    }

    private void Start()
    {
        player = Player.Resolve(true);
        roundmanager = FindFirstObjectByType<Roundmanager>();

        stat.OnDied += HandleDeath;
        stat.OnMidPattern  += HandleMidPattern;
        stat.OnGaugeFull   += HandleGaugeFull;
        stat.OnGaugeStepChanged += view.UpdateActionGauge;
        midPattern = GetComponent<MonsterMidPattern>();
    }

    void OnDestroy()
    {
        stat.OnDied -= HandleDeath;
        stat.OnMidPattern  -= HandleMidPattern;
        stat.OnGaugeFull   -= HandleGaugeFull;
        stat.OnGaugeStepChanged -= view.UpdateActionGauge;
    }

    void Update()
    {
        if (!stat.IsAlive) return;

        // NewBattleController가 적 스폰 직후엔 아직 Instance가 없을 수 있어 한 번만 지연 갱신
        if (!_attackPreviewInitialized && Battle.NewBattleController.Instance != null)
        {
            UpdateAttackPreviewForNewSystem();
            _attackPreviewInitialized = true;
        }
    }

    /// <summary>새 시스템 모드: 다음 공격 시퀀스 값을 EnemyView에 미리 표시.</summary>
    void UpdateAttackPreviewForNewSystem()
    {
        if (Battle.NewBattleController.Instance == null) return;
        if (newSystemAttackSequence == null || newSystemAttackSequence.Length == 0) return;
        if (view == null) return;

        int nextDamage = newSystemAttackSequence[_newSystemAttackIndex % newSystemAttackSequence.Length];
        view.SetAttackPreviewDamage(nextDamage);
    }

    public void TakeDamage(float damage, string cardtype = "normal")
    {
        if (!stat.IsAlive) return;

        // 실제 피해
        if (damage > 0)
        {
            if (player.statusEffects["launcher"] > 0)
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

    public void TakeDamage(float damage)
    {
        TakeDamage(damage, "Default");
    }

    public event Action<string, int> OnStatusChanged;
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
    }

    public int GetStatus(string type)
    {
        return stat.statusEffects.ContainsKey(type) ? stat.statusEffects[type] : 0;
    }

    public void SetStatus(string type, int amount)
    {
        if (stat.statusEffects.ContainsKey(type))
        {
            int v = Mathf.Clamp(amount, 0, 999); // 기획서 0.6v: 스택 최대 999
            stat.statusEffects[type] = v;
            OnStatusChanged?.Invoke(type, v);
        }
    }
    public float GetAttackDamage()
    {
        return stat.AttackDamage;
    }

    public void AddGuard(float amount)
    {
        stat.guard = Mathf.Clamp(stat.guard + amount, 0f, 999f); // 기획서 0.6v: 방어도 최대 999
    }
    /// <summary>Called by ElementSlotSystem each time the player uses a slot</summary>
    public void OnPlayerAction()
    {
        stat.ConsumeGaugeStep();
    }

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

    /// <summary>기획서 0.6v [방해행동-강화]: 다음 적 공격 1회를 +50% 강화.</summary>
    public void BuffNextAttack() => _nextAttackBuffed = true;

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

        if (Battle.NewBattleController.Instance != null && newSystemAttackSequence != null && newSystemAttackSequence.Length > 0)
        {
            // PDF: 일반 16 → 일반 18 → 강 40 순차 반복
            damagePerHit = newSystemAttackSequence[_newSystemAttackIndex % newSystemAttackSequence.Length];
            hitCount = 1;
            _newSystemAttackIndex++;
            Debug.Log($"[적 공격] 시퀀스 인덱스={_newSystemAttackIndex - 1}, 피해={damagePerHit}");
        }
        else
        {
            damagePerHit = Mathf.RoundToInt(stat.AttackDamage);
            if (damagePerHit <= 0) damagePerHit = fallbackGaugeFullDamage;
            hitCount = Mathf.Max(0, stat.CurrentAttackCount);
        }

        // 기획서 0.6v [방해행동-강화]: 다음 공격 1회 한정 +50% (소수 올림)
        if (_nextAttackBuffed)
        {
            damagePerHit = Mathf.CeilToInt(damagePerHit * 1.5f);
            _nextAttackBuffed = false;
            view?.ShowMidPatternNotice("강화!");
            Debug.Log($"[적 강화] 다음 공격 +50% → {damagePerHit}");
        }

        if (player != null)
        {
            // 연타 연출을 위해 코루틴으로 분리하여 호출
            StartCoroutine(ExecuteMultiHit(hitCount, damagePerHit));
        }

        stat.RollNewAttackPlan();

        // 새 시스템: 다음 공격 시퀀스 값을 미리 표시
        UpdateAttackPreviewForNewSystem();
    }

    /// <summary>
    /// PDF [화상]: 적 공격 시 피격 처리 전 발동
    /// 1. 화상 스택 N만큼 고정피해
    /// 2. 화상 스택 n/2 (절반 감소)
    /// </summary>
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
    IEnumerator ExecuteMultiHit(int count, int damage)
    {
        for (int i = 0; i < count; i++)
        {
            player.TakeDamage(damage);

            if (hitVFX != null)
            {
                hitVFX.Stop();
                hitVFX.Play();
            }

            // 타격 사이의 짧은 간격 (0.1~0.15초 정도가 적당합니다)
            yield return new WaitForSeconds(0.15f);
        }
        Debug.Log($"[EnemyController] {count} hit");
    }
    void HandleDeath()
    {
        if (isDead) return;
        isDead = true;
        midPattern?.OnBattleEnd();
        // OnDied 이벤트로 인해 Roundmanager.HandleEnemyDied가 호출됨
        // 여기서 직접 호출하지 않음 (중복 호출 방지)

        // 피격 연출(빨강 플래시 + 흔들기)이 끝난 뒤에 사라지도록 대기 후 제거.
        StartCoroutine(DestroyAfterHitReaction());
    }

    IEnumerator DestroyAfterHitReaction()
    {
        float wait = view != null ? view.HitReactionRemaining : 0f;
        if (wait > 0f) yield return new WaitForSeconds(wait);
        Destroy(gameObject);
    }

}
