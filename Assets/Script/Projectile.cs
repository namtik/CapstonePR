using UnityEngine;
using UnityEngine.UI;

public class Projectile : MonoBehaviour
{
    public float speed;  
    public float damage = 10f;
    public Sprite projectileSprite;

    private Transform target;
    private Canvas canvas;
    private RectTransform rectTransform;

    public void Initialize(Transform targetTransform, float attackDamage)
    {
        target = targetTransform;
        damage = attackDamage;
    }

    void Start()
    {
        canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas != null)
            transform.SetParent(canvas.transform, true);

        Image image = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();

        rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
            rectTransform.sizeDelta = new Vector2(50f, 50f);

        if (projectileSprite != null)
        {
            image.sprite = projectileSprite;
            image.color = Color.white;
            image.preserveAspect = true;
        }
    }

    void Update()
    {
        if (target == null) { Destroy(gameObject); return; }

        Vector3 screenPos = Camera.main.WorldToScreenPoint(target.position);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.GetComponent<RectTransform>(),
            screenPos,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main,
            out Vector2 canvasPos
        );

        Vector3 targetCanvasPos = new Vector3(canvasPos.x, canvasPos.y, 0f);

        transform.localPosition = Vector3.MoveTowards(
            transform.localPosition, targetCanvasPos, speed * Time.deltaTime
        );

        if (Vector2.Distance(transform.localPosition, targetCanvasPos) < 10f)
            HitPlayer();
    }

    void HitPlayer()
    {
        Player player = target.GetComponent<Player>();
        if (player != null)
            player.OnProjectileHit(damage);

        Destroy(gameObject);
    }
}
