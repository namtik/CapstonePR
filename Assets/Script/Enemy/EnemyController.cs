using UnityEngine;

public class EnemyController : MonoBehaviour
{
    [Header("공격 투사체 설정")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform projectileSpawnPoint;
    [SerializeField] private Sprite projectileSprite;

    private EnemyStat stat;
    private EnemyView view;
    private Player player;
    private Roundmanager roundmanager;
    private float actionGauge = 0f;
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
    }

    void OnDestroy()
    {
        stat.OnDied -= HandleDeath;
    }

    void Update()
    {
        if (!stat.IsAlive) return;

        UpdateActionGauge();
    }

    public void TakeDamage(float damage)
    {
        if (!stat.IsAlive) return;

        stat.TakeDamage(damage);
        view.ShowDamage(damage);
    }

    void UpdateActionGauge()
    {
        actionGauge += stat.GaugeSpeed * Time.deltaTime;

        if (actionGauge >= 100f)
        {
            Attack();
            actionGauge = 0f;
        }
    }

    void Attack()
    {
        if (player == null) return;

        Vector3 spawnPos = projectileSpawnPoint != null
            ? projectileSpawnPoint.position
            : transform.position;

        GameObject go = projectilePrefab != null
            ? Instantiate(projectilePrefab, spawnPos, Quaternion.identity)
            : new GameObject("Projectile");

        Projectile projectile = go.GetComponent<Projectile>()
                             ?? go.AddComponent<Projectile>();

        if (projectileSprite != null)
            projectile.projectileSprite = projectileSprite;

        projectile.Initialize(player.transform, stat.AttackDamage);
    }

    void HandleDeath()
    {
        if (isDead) return; // 중복 처리 방지
        isDead = true;

        roundmanager?.HandleEnemyDied();

        Destroy(gameObject);
    }
}
