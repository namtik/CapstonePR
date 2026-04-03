using System.Collections;
using UnityEngine;

public class EnemyController : MonoBehaviour
{
    [Header("공격 설정")]
    [SerializeField] private int fallbackGaugeFullDamage = 10;
    [SerializeField] private ParticleSystem hitVFX;

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

    public void TakeDamage(float damage, string cardtype = "normal")
    {
        if (!stat.IsAlive) return;

        // 실제 피해
        if (damage > 0)
        {
            stat.TakeDamage(damage);
            view.ShowDamage(damage);
            view.PlayHitEffect(cardtype);
        }
    }

    public void TakeDamage(float damage)
    {
        TakeDamage(damage, "Default");
    }

    public void AddStatus(string type, int amount)
    {
        if (!stat.statusEffects.ContainsKey(type)) return;
        stat.statusEffects[type] += amount;
    }

    public int GetStatus(string type)
    {
        return stat.statusEffects.ContainsKey(type) ? stat.statusEffects[type] : 0;
    }

    public void SetStatus(string type, int amount)
    {
        if (stat.statusEffects.ContainsKey(type)) stat.statusEffects[type] = amount;
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
            // 연타 연출을 위해 코루틴으로 분리하여 호출
            StartCoroutine(ExecuteMultiHit(hitCount, damagePerHit));
        }

        stat.RollNewAttackPlan();
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
            yield return new WaitForSeconds(0.1f);
        }
        Debug.Log($"[EnemyController] {count} hit");
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
