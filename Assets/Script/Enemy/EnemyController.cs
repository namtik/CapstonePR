using UnityEngine;

public class EnemyController : MonoBehaviour
{
    [Header("공격 설정")]
    [SerializeField] private int fallbackGaugeFullDamage = 10;

    private EnemyStat stat;
    private EnemyView view;
    private Player player;
    private Roundmanager roundmanager;
    private bool isDead = false;

    private void Awake()
    {
        stat = GetComponent<EnemyStat>();
        view = GetComponent<EnemyView>();
    }

    private void Start()
    {
        player = Object.FindFirstObjectByType<Player>();
        roundmanager = Object.FindFirstObjectByType<Roundmanager>();

        stat.OnDied += HandleDeath;
        stat.OnMidPattern  += HandleMidPattern;
        stat.OnGaugeFull   += HandleGaugeFull;
        stat.OnGaugeStepChanged += view.UpdateActionGauge;
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

    public void TakeDamage(float damage, string cardtype)
    {
        if (!stat.IsAlive) return;

        stat.TakeDamage(damage);
        view.ShowDamage(damage);
        view.PlayHitEffect(cardtype);
    }

    public void TakeDamage(float damage)
    {
        TakeDamage(damage, "Default");
    }

    /// <summary>Called by ElementSlotSystem each time the player uses a slot</summary>
    public void OnPlayerAction()
    {
        stat.ConsumeGaugeStep();
    }

    void HandleMidPattern()
    {
        string patternMessage = ElementSlotSystem.Instance?.TriggerDisruptionPattern();
        if (string.IsNullOrEmpty(patternMessage))
            patternMessage = "패턴 발동";

        view?.ShowMidPatternNotice(patternMessage);
    }

    void HandleGaugeFull()
    {
        if (player == null)
            player = Object.FindFirstObjectByType<Player>();

        int damagePerHit = Mathf.RoundToInt(stat.AttackDamage);
        if (damagePerHit <= 0)
            damagePerHit = fallbackGaugeFullDamage;

        int hitCount = Mathf.Max(0, stat.CurrentAttackCount);

        if (player != null)
        {
            // We resolve planned multi-hit attacks as separate hits so count-based difficulty is felt directly in combat.
            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
                player.TakeDamage(damagePerHit);

            Debug.Log($"[EnemyController] Gauge full -> direct damage {damagePerHit}x{hitCount}");
        }

        stat.RollNewAttackPlan();
    }
    void HandleDeath()
    {
        if (isDead) return;
        isDead = true;

        // OnDied 이벤트로 인해 Roundmanager.HandleEnemyDied가 호출됨
        // 여기서 직접 호출하지 않음 (중복 호출 방지)
        
        Destroy(gameObject);
    }

}
