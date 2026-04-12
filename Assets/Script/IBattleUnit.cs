using System;
using UnityEngine;

public interface IBattleUnit
{
    event Action<string, int> OnStatusChanged;
    void TakeDamage(float damage, string damageType = "normal");
    void AddStatus(string type, int amount);
    int GetStatus(string type);
    void SetStatus(string type, int amount);
    void AddGuard(float amount);
    float GetAttackDamage();
}

