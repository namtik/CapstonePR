using UnityEngine;

public class EnemyController : MonoBehaviour
{
    [Header("���� ����ü ����")]
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

    public void TakeDamage(float damage, string cardtype)
    {
        if (!stat.IsAlive) return;

        stat.TakeDamage(damage);
        view.ShowDamage(damage);
        view.PlayHitEffect(cardtype);
    }

    public void TakeDamage(float damage)
    {
        TakeDamage(damage, "Default");
    }


    void UpdateActionGauge()
    {
        actionGauge += stat.GaugeSpeed * Time.deltaTime;
        view.UpdateActionGauge(actionGauge / 100f);

        if (actionGauge >= 100f)
        {
            Attack();
            actionGauge = 0f;
            view.UpdateActionGauge(0f);
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
        if (isDead) return; // �ߺ� ó�� ����
        isDead = true;

        roundmanager?.HandleEnemyDied();

        Destroy(gameObject);
    }
}
