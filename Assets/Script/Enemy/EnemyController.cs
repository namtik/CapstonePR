using System;
using System.Collections;
using UnityEngine;

public class EnemyController : MonoBehaviour, IBattleUnit
{
    [Header("공격 설정")]
    [SerializeField] private int fallbackGaugeFullDamage = 10;
    [SerializeField] private ParticleSystem hitVFX;

    private EnemyStat stat;
    private EnemyView view;
    private Player player;
    private Roundmanager roundmanager;
    private bool isDead = false;
    private MonsterMidPattern midPattern;

    private void Awake()
    {
        stat = GetComponent<EnemyStat>();
        view = GetComponent<EnemyView>();
    }

    private void Start()
    {
        player = FindFirstObjectByType<Player>();
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

        stat.statusEffects[type] += amount;

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
            stat.statusEffects[type] = amount;
            OnStatusChanged?.Invoke(type, amount);
        }
    }
    public float GetAttackDamage()
    {
        return stat.AttackDamage;
    }

    public void AddGuard(float amount)
    {
        stat.guard += amount;
    }
    /// <summary>Called by ElementSlotSystem each time the player uses a slot</summary>
    public void OnPlayerAction()
    {
        stat.ConsumeGaugeStep();
    }

    void HandleMidPattern()
    {
        string patternMessage;

        // FCM 시스템이 있으면 사용, 없으면 기존 랜덤 폴백
        if (midPattern != null)
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

    void HandleGaugeFull()
    {
        midPattern?.OnGauge10();
        if (player == null)
            player = FindFirstObjectByType<Player>();

        // 새 전투 시스템: 적 공격 피격 처리 전 화상 발동 (PDF 명세)
        if (Battle.NewBattleController.Instance != null)
        {
            ApplyBurnBeforeAttack();
            if (!stat.IsAlive) return; // 화상으로 적이 사망하면 공격하지 않음
        }

        int damagePerHit = Mathf.RoundToInt(stat.AttackDamage);
        if (damagePerHit <= 0)
            damagePerHit = fallbackGaugeFullDamage;

        int hitCount = Mathf.Max(0, stat.CurrentAttackCount);
        if (player != null)
        {
            // 연타 연출을 위해 코루틴으로 분리하여 호출
            StartCoroutine(ExecuteMultiHit(hitCount, damagePerHit));
        }

        stat.RollNewAttackPlan();
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

        // 데미지 숫자 + 화상 알림 텍스트
        if (view != null)
        {
            view.ShowDamage(burn);
            view.ShowMidPatternNotice($"화상 {burn}!");
        }

        int after = burn / 2;
        stat.statusEffects["burn"] = after;
        OnStatusChanged?.Invoke("burn", after);
        Debug.Log($"[화상] 적 {burn} 고정피해, 잔여 화상={after}");
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

        Destroy(gameObject);
    }

}
