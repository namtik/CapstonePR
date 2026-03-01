using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

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
    private float fadeTime = 1f; // 데미지 텍스트가 사라지는데 걸리는 시간
    public TMP_Text Takedamtext; // 받은 데미지 텍스트
    
    private Vector3 originalPos; // 텍스트의 원래 위치 저장
    private Color originalColor; // 텍스트의 원래 색상 저장

    [Header("투사체 설정")]
    public GameObject projectilePrefab;
    public Transform projectileSpawnPoint;
    public Sprite projectileSprite; // 투사체 스프라이트

    private Player player;
    private Coroutine damageCoroutine; // 데미지 표시용

    [Header("피격 이펙트 (파티클)")]
    public ParticleSystem qEffect;
    public ParticleSystem wEffect;
    public ParticleSystem eEffect;
    public ParticleSystem rEffect;

    void Start()
    {
        currentHp = maxHp;
        player = Object.FindFirstObjectByType<Player>();
        if (Takedamtext != null)
        {
            originalPos = Takedamtext.transform.position;
            originalColor = Takedamtext.color;
            Takedamtext.text = ""; // 처음엔 안 보이게
        }
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
        
        if (Takedamtext != null)
        {
            if (damageCoroutine != null) StopCoroutine(damageCoroutine);

            // 데미지 텍스트 내용 설정
            Takedamtext.text = damage.ToString();

            // 코루틴 시작
            damageCoroutine = StartCoroutine(FloatingDamageEffect());
        }

        if (currentHp <= 0)
        {
            // Ensure UI shows zero before destruction
            if (hpBar != null) hpBar.value = 0f;

            // If this is the last enemy in the scene, notify battle clear
            var enemies = Object.FindObjectsOfType<Enemy>();
            if (enemies == null || enemies.Length <= 1)
            {
                // 각주: BattleManger를 통해 전투 클리어 처리
                BattleManger bm = Object.FindFirstObjectByType<BattleManger>();
                if (bm != null)
                {
                    bm.OnBattleClear();
                }
                else
                {
                    Debug.LogWarning("Enemy: BattleManger를 찾을 수 없습니다.");
                }
            }

            Destroy(gameObject);
        }
    }

    IEnumerator FloatingDamageEffect()
    {
        float timer = 0f;

        // 연출 시작 전 초기화(위치, 색 초기화)
        Takedamtext.transform.position = originalPos;
        Takedamtext.color = originalColor;

        while (timer < fadeTime)
        {
            timer += Time.deltaTime;

            // 위로 이동 (현재 위치에서 Y축으로 조금씩 이동)
            Takedamtext.transform.position += Vector3.up * 0.5f * Time.deltaTime;

            // 서서히 투명해지기
            float alpha = Mathf.Lerp(1f, 0f, timer / fadeTime);
            Takedamtext.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);

            yield return null; // 한 프레임 대기
        }

        // 연출 종료 후 처리
        Takedamtext.text = ""; // 텍스트 초기화
        Takedamtext.transform.position = originalPos; // 위치 복구
        Takedamtext.color = originalColor; // 색상 복구
    }

    public void PlayHitEffect(string cardType)
    {
        switch (cardType)
        {
            case "Q":
                if (qEffect != null) qEffect.Play();
                break;
            case "W":
                if (wEffect != null) wEffect.Play();
                break;
            case "E":
                if (eEffect != null) eEffect.Play();
                break;
            case "R":
                if (rEffect != null) rEffect.Play();
                break;
        }
    }
}

