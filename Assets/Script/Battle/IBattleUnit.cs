using UnityEngine;

public interface IBattleUnit
{
    void TakeDamage(float damage, string damageType = "normal");
    void AddStatus(string type, int amount);
    int GetStatus(string type);
    void SetStatus(string type, int amount);
    void AddGuard(float amount);
}
