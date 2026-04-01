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
        ElementSlotSystem.Instance?.TriggerDisruptionPattern();
    }

    void HandleGaugeFull()
    {
        if (player == null)
            player = Object.FindFirstObjectByType<Player>();

        int damageToApply = Mathf.RoundToInt(stat.AttackDamage);
        if (damageToApply <= 0)
            damageToApply = fallbackGaugeFullDamage;

        if (player != null)
        {
            player.TakeDamage(damageToApply);
            Debug.Log($"[EnemyController] Gauge full -> direct damage {damageToApply}");
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
