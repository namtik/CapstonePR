using UnityEngine;
using UnityEngine.UI;

public class Enemy : MonoBehaviour
{
    public float maxHp = 50f;
    public float currentHp;
    public float attackDamage = 12f;

    [Header("UI 연결")]
    public Slider hpBar;
    public Slider actionSlider;

    private float actionGauge = 0f;
    private float gaugeSpeed = 10f; // 초당 10%
    private Player player;

    void Start()
    {
        currentHp = maxHp;
        player = Object.FindFirstObjectByType<Player>();
    }

    void Update()
    {
        if (actionGauge < 100f)
        {
            actionGauge += gaugeSpeed * Time.deltaTime;
            UpdateUI();
        }
        else
        {
            Attack();
        }
    }

    void Attack()
    {
        if (player != null)
        {
            player.TakeDamage(attackDamage);
            actionGauge = 0f;
            Debug.Log("적이 공격했습니다");
        }
    }

    void UpdateUI()
    {
        if (hpBar != null) hpBar.value = currentHp / maxHp;
        if (actionSlider != null) actionSlider.value = actionGauge / 100f;
    }

    public void TakeDamage(float damage)
    {
        currentHp -= damage;
        if (hpBar != null) hpBar.value = currentHp / maxHp;

        if (currentHp <= 0)
        {
            Debug.Log("적이 죽었습니다");
            Destroy(gameObject);
        }
    }
}