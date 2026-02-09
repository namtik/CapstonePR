using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 300f;
    public float damage = 10f;
    public Sprite projectileSprite; // Inspector에서 할당할 스프라이트
    private Transform target;

    public void Initialize(Transform targetTransform, float attackDamage)
    {
        target = targetTransform;
        damage = attackDamage;
    }

    void Start()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
            transform.SetParent(canvas.transform, true);
        }

        UnityEngine.UI.Image image = gameObject.GetComponent<UnityEngine.UI.Image>();
        if (image == null)
        {
            image = gameObject.AddComponent<UnityEngine.UI.Image>();
        }

        RectTransform rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.sizeDelta = new Vector2(50f, 50f);
        }

        // 할당된 스프라이트 사용
        if (projectileSprite != null)
        {
            image.sprite = projectileSprite;
            image.color = Color.white;
            image.preserveAspect = true;
        }
        else
        {
            Debug.LogWarning("Projectile: 스프라이트가 할당되지 않았습니다!");
        }
    }

    void Update()
    {
        if (target != null)
        {
            Vector3 targetPos = target.position;
            targetPos.z = transform.position.z;
            
            transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);

            float distance = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.y),
                new Vector2(target.position.x, target.position.y)
            );

            if (distance < 50f)
            {
                HitPlayer();
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void HitPlayer()
    {
        Player player = target.GetComponent<Player>();
        if (player != null)
        {
            player.OnProjectileHit(damage);
        }
        Destroy(gameObject);
    }
}
