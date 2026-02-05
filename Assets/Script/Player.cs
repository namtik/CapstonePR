using UnityEngine;
using UnityEngine.UI;

public class Player : MonoBehaviour
{
    public float maxHp = 100f;
    public float currentHp;
    public float attackDamage = 10f;

    public Slider hpBar;

    void Awake()
    {
        currentHp = maxHp;
        UpdateUI();
    }

    public void TakeDamage(float damage)
    {
        currentHp -= damage;
        UpdateUI();
        if (currentHp <= 0) Debug.Log("플레이어 사망");
    }

    void UpdateUI()
    {
        if (hpBar != null) hpBar.value = currentHp / maxHp;
    }
}