using UnityEngine;
using UnityEngine.UI;

public class Enemy : MonoBehaviour
{
    [Header("능력치")]
    public float maxHp = 100f;
    public float currentHp;
    public float attackDamage = 10f;
    private float actionGauge = 0f;
    private float gaugeSpeed = 10f;

    [Header("UI")]
    public Slider hpBar;
    public Slider actionSlider;

    [Header("투사체 설정")]
    public GameObject projectilePrefab;
    public Transform projectileSpawnPoint;
    public Sprite projectileSprite; // 투사체 스프라이트

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
            Vector3 spawnPosition = projectileSpawnPoint != null 
                ? projectileSpawnPoint.position 
                : transform.position;

            GameObject projectile;
            
            if (projectilePrefab != null)
            {
                projectile = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);
            }
            else
            {
                projectile = new GameObject("Projectile");
                projectile.transform.position = spawnPosition;
            }
            
            Projectile projectileScript = projectile.GetComponent<Projectile>();
            if (projectileScript == null)
            {
                projectileScript = projectile.AddComponent<Projectile>();
            }
            
            // 스프라이트 할당
            if (projectileSprite != null)
            {
                projectileScript.projectileSprite = projectileSprite;
            }
            
            projectileScript.Initialize(player.transform, attackDamage);
            actionGauge = 0f;
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
            Destroy(gameObject);
        }
    }
}
