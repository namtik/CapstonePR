using System;
using UnityEngine;

public class EnemyStat : MonoBehaviour
{
    public float maxHp;
    public float currentHp;
    public float AttackDamage;
    public float GaugeSpeed;
    public bool IsAlive => currentHp > 0;

    public event Action<float,float> OnHpChanged;// HP 변경 이벤트 (현재 HP, 최대 HP)
    public event Action OnDied; // 사망 이벤트

    //
    public void Initialize(EnemyData data, int columnIndex, NodeType nodeType, DifficultyConfig config)
    {
        maxHp = data.maxHp * config.GetHpMultiplier(columnIndex, nodeType);
        AttackDamage = data.attackDamage;
        GaugeSpeed = data.gaugeSpeed;
        currentHp = maxHp;

        OnHpChanged?.Invoke(currentHp, maxHp);

        Debug.Log($"[{data.enemyName}] 컬럼{columnIndex} / HP:{maxHp} / DMG:{AttackDamage} / Speed:{GaugeSpeed}");
    }

    public void TakeDamage(float damage)
    {
        if (!IsAlive) return;

        currentHp -= damage;
        OnHpChanged?.Invoke(currentHp, maxHp);

        if (!IsAlive) OnDied?.Invoke();
    }

}
