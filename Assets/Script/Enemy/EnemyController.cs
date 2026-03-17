using UnityEngine;
using System.Collections;

public class EnemyController : MonoBehaviour
{
    [Header("투사체 설정")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform projectileSpawnPoint;
    [SerializeField] private Sprite projectileSprite;

    private EnemyStat stat;
    private EnemyView view;
    private Player player;
    private Roundmanager roundmanager;
    private float actionGauge = 0f;
    private bool isDead = false;
    private bool isFiring = false;

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
        if (isFiring) return; // 발사 중에는 게이지 정지

        actionGauge += stat.GaugeSpeed * Time.deltaTime;
        view.UpdateActionGauge(actionGauge / 100f);

        if (actionGauge >= 100f)
        {
            if (stat.CurrentAttackCount > 0)
            {
                StartCoroutine(FireProjectiles(stat.CurrentAttackCount));
            }
            // 공격 횟수가 0이면 투사체 발사 없이 스킵

            actionGauge = 0f;
            view.UpdateActionGauge(0f);
            stat.RollNewAttackPlan(); // 다음 사이클 공격 예고 생성
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
        if (isDead) return;
        isDead = true;

        // OnDied 이벤트로 인해 Roundmanager.HandleEnemyDied가 호출됨
        // 여기서 직접 호출하지 않음 (중복 호출 방지)
        
        Destroy(gameObject);
    }

    IEnumerator FireProjectiles(int count)
    {
        isFiring = true;
        for (int i = 0; i < count; i++)
        {
            FireSingleProjectile();
            if (i < count - 1)
                yield return new WaitForSeconds(0.2f);
        }
        isFiring = false;
    }

    void FireSingleProjectile()
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
}
