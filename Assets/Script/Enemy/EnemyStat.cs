using System;
using System.Collections.Generic;
using UnityEngine;

public class EnemyStat : MonoBehaviour
{
    public float maxHp;
    public float currentHp;
    public float AttackDamage;
    public float GaugeSpeed;
    public bool IsAlive => currentHp > 0;

    public float guard = 0f;
    public Dictionary<string, int> statusEffects = new Dictionary<string, int>();

    private int plannedAttackCount;
    private int currentAttackCount;
    private bool hasDied = false; // ��� �÷��� �߰�

    // Player-action-driven 10-step gauge
    public const int GAUGE_MAX_STEPS = 10;
    private int gaugeStep = 0;
    private bool midPatternTriggered = false;
    public int GaugeStep => gaugeStep;

    private EnemyData enemyData;
    private int columnIndex;
    private NodeType nodeType;
    private DifficultyConfig config;

    public event Action<float,float> OnHpChanged;// HP ���� �̺�Ʈ (���� HP, �ִ� HP)
    public event Action OnDied; // ��� �̺�Ʈ
    public event Action<int> OnAttackCountChanged;
    public event Action OnMidPattern;              // 50% step reached
    public event Action OnGaugeFull;               // 100% - enemy attacks
    public event Action<float> OnGaugeStepChanged; // gauge ratio 0~1

    public int CurrentAttackCount => currentAttackCount;
    public int PlannedAttackCount => plannedAttackCount;

    private void Awake()
    {
        statusEffects["burn"] = 0;
        statusEffects["wet"] = 0;
        statusEffects["freeze"] = 0;
    }
    public void Initialize(EnemyData data, int columnIndex, NodeType nodeType, DifficultyConfig config)
    {
        this.enemyData = data;
        this.columnIndex = columnIndex;
        this.nodeType = nodeType;
        this.config = config;

        maxHp = data.maxHp * config.GetHpMultiplier(columnIndex, nodeType);
        AttackDamage = data.attackDamage;
        GaugeSpeed = data.gaugeSpeed;
        currentHp = maxHp;
        hasDied = false;
    gaugeStep = 0;
    midPatternTriggered = false;

        OnHpChanged?.Invoke(currentHp, maxHp);

        RollNewAttackPlan();

        Debug.Log($"[{data.enemyName}] �÷�{columnIndex} / HP:{maxHp} / DMG:{AttackDamage} / Speed:{GaugeSpeed} / ����Ƚ��:{currentAttackCount}");
    }

    public void TakeDamage(float damage)
    {
        if (!IsAlive) return;

        currentHp -= damage;
        OnHpChanged?.Invoke(currentHp, maxHp);

        // ��� �÷��׸� Ȯ���Ͽ� OnDied �̺�Ʈ�� �� ���� �߻��ϵ��� ��
        if (!IsAlive && !hasDied)
        {
            hasDied = true;
            Debug.Log($"[EnemyStat.OnDied] {enemyData.enemyName} ��� - OnDied �̺�Ʈ �߻�");
            OnDied?.Invoke();
        }
    }

    public void RollNewAttackPlan()
    {
        plannedAttackCount = config.GetAttackCount(enemyData.baseAttackCount, columnIndex, nodeType);
        currentAttackCount = plannedAttackCount;
        OnAttackCountChanged?.Invoke(currentAttackCount);
    }

    public void ReduceAttackCount(int amount = 1)
    {
        currentAttackCount = Mathf.Max(0, currentAttackCount - amount);
        OnAttackCountChanged?.Invoke(currentAttackCount);
    }

    /// <summary>Called by ElementSlotSystem.UseSlot — advances the 10-step gauge</summary>
    public void ConsumeGaugeStep()
    {
        if (!IsAlive) return;
        if (statusEffects["freeze"]>0)
        {
            statusEffects["freeze"]--; // 감소
            return; // 얼린 상태면 게이지 안 차오름
        }
            
        gaugeStep++;
        OnGaugeStepChanged?.Invoke((float)gaugeStep / GAUGE_MAX_STEPS);

        // Mid-point (step 5): trigger disruption pattern once per cycle
        if (gaugeStep == GAUGE_MAX_STEPS / 2 && !midPatternTriggered)
        {
            midPatternTriggered = true;
            OnMidPattern?.Invoke();
        }

        // Full gauge (step 10): enemy attacks, gauge resets
        if (gaugeStep >= GAUGE_MAX_STEPS)
        {
            gaugeStep = 0;
            midPatternTriggered = false;
            OnGaugeStepChanged?.Invoke(0f);
            OnGaugeFull?.Invoke();
        }
    }
}


