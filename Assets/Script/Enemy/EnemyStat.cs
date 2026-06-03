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

    // Player-action-driven gauge (full = gaugeMaxSteps, mid pattern = half). 기획서 0.6v: per-enemy 10~30
    public const int GAUGE_MAX_STEPS = 20; // 기본/폴백값
    private int gaugeMaxSteps = GAUGE_MAX_STEPS; // Initialize에서 적별 10~30으로 설정
    private int gaugeStep = 0;
    private bool midPatternTriggered = false;
    public int GaugeStep => gaugeStep;

    private EnemyData enemyData;
    private EnemyController controller;
    private int columnIndex;
    private NodeType nodeType;
    private DifficultyConfig config;

    public NodeType NodeType => nodeType;
    /// <summary>물8(208) 즉사 예외 판정용 — 보스/정예 여부.</summary>
    public bool IsBossOrElite => nodeType == NodeType.Boss || nodeType == NodeType.Elite;

    public event Action<float,float> OnHpChanged;// HP ���� �̺�Ʈ (���� HP, �ִ� HP)
    public event Action OnDied; // ��� �̺�Ʈ
    public event Action<int> OnAttackCountChanged;
    public event Action OnMidPattern;              // 50% step reached
    public event Action OnGaugeFull;               // 100% - enemy attacks
    public event Action<float> OnGaugeStepChanged; // gauge ratio 0~1
    public bool isNewlyFrozen = false;

    public int CurrentAttackCount => currentAttackCount;
    public int PlannedAttackCount => plannedAttackCount;

    private void Awake()
    {
        statusEffects["burn"] = 0;
        statusEffects["wet"] = 0;
        statusEffects["freeze"] = 0;
        statusEffects["frost"] = 0; // 새 전투 시스템 빙결(데이터 키 FROST). 게이지 상승을 막음.
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
        // 기획서 0.6v: 적마다 행동 게이지 최대치 10~30 (미설정/0이면 기본 20)
        gaugeMaxSteps = Mathf.Clamp(data.actionGaugeMax > 0 ? data.actionGaugeMax : GAUGE_MAX_STEPS, 10, 30);

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

    /// <summary>기획서 0.6v [방해행동-회복]: 적 HP 회복(최대치 클램프).</summary>
    public void Heal(float amount)
    {
        if (!IsAlive || amount <= 0f) return;
        currentHp = Mathf.Min(maxHp, currentHp + amount);
        OnHpChanged?.Invoke(currentHp, maxHp);
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

        // 새 전투 시스템 빙결(FROST): 스택이 있으면 게이지 상승을 막고 스택 1 소모.
        if (statusEffects.TryGetValue("frost", out int frost) && frost > 0)
        {
            var ctrl = GetComponent<EnemyController>();
            if (ctrl != null) ctrl.SetStatus("frost", frost - 1);
            else statusEffects["frost"] = frost - 1;
            return;
        }

        if (statusEffects.ContainsKey("freeze") && statusEffects["freeze"] > 0)
        {
            if (isNewlyFrozen)
            {
                isNewlyFrozen = false; 
            }
            else
            {
  
                EnemyController myController = GetComponent<EnemyController>();
                if (myController != null)
                {
                    myController.AddStatus("freeze", -1);
                }
                return; 
            }
        }

        gaugeStep++;
        OnGaugeStepChanged?.Invoke((float)gaugeStep / gaugeMaxSteps);

        // Mid-point (절반): trigger disruption pattern once per cycle
        if (gaugeStep == gaugeMaxSteps / 2 && !midPatternTriggered)
        {
            midPatternTriggered = true;
            OnMidPattern?.Invoke();
        }

        // Full gauge: enemy attacks, gauge resets (초과분은 다음 호출에서 자연 이월)
        if (gaugeStep >= gaugeMaxSteps)
        {
            gaugeStep = 0;
            midPatternTriggered = false;
            OnGaugeStepChanged?.Invoke(0f);
            OnGaugeFull?.Invoke();
        }
    }
}


