using System;
using UnityEngine;

public class EnemyStat : MonoBehaviour
{
    public float maxHp;
    public float currentHp;
    public float AttackDamage;
    public float GaugeSpeed;
    public bool IsAlive => currentHp > 0;

    private int plannedAttackCount;
    private int currentAttackCount;

    private EnemyData enemyData;
    private int columnIndex;
    private NodeType nodeType;
    private DifficultyConfig config;

    public event Action<float,float> OnHpChanged;// HP 변경 이벤트 (현재 HP, 최대 HP)
    public event Action OnDied; // 사망 이벤트
    public event Action<int> OnAttackCountChanged;

    public int CurrentAttackCount => currentAttackCount;
    public int PlannedAttackCount => plannedAttackCount;

    //
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

        OnHpChanged?.Invoke(currentHp, maxHp);

        RollNewAttackPlan();

        Debug.Log($"[{data.enemyName}] 컬럼{columnIndex} / HP:{maxHp} / DMG:{AttackDamage} / Speed:{GaugeSpeed} / 공격횟수:{currentAttackCount}");
    }

    public void TakeDamage(float damage)
    {
        if (!IsAlive) return;

        currentHp -= damage;
        OnHpChanged?.Invoke(currentHp, maxHp);

        if (!IsAlive) OnDied?.Invoke();
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
}


