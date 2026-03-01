using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
public class EnemyView : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Slider hpBar;
    [SerializeField] private TMP_Text damageText;
    [SerializeField] private Image enemyImage;

    [Header("데미지 연출 설정")]
    [SerializeField] private float fadeTime = 1f;
    [SerializeField] private float floatSpeed = 0.5f;

    [Header("피격 이펙트 (파티클)")]
    public ParticleSystem qEffect;
    public ParticleSystem wEffect;
    public ParticleSystem eEffect;
    public ParticleSystem rEffect;

    private EnemyStat stat;
    private Vector3 damageTextOriginPos;
    private Color damageTextOriginColor;
    private Coroutine damageCoroutine;

    void Awake()
    {
        stat = GetComponent<EnemyStat>();
    }

    void Start()
    {
        if (damageText != null)
        {
            damageTextOriginPos = damageText.transform.position;
            damageTextOriginColor = damageText.color;
            damageText.text = "";
        }

        // EnemyStat 이벤트 구독
        stat.OnHpChanged += UpdateHpBar;
    }

    void OnDestroy()
    {
        stat.OnHpChanged -= UpdateHpBar;
        delDamageText();
    }

    // EnemyStat HP 변경 이벤트 핸들러

    void UpdateHpBar(float currentHp, float maxHp)
    {
        if (hpBar == null) return;
        hpBar.value = currentHp / maxHp;
    }


    // EnemyController가 TakeDamage 직후 호출
    public void ShowDamage(float damage)
    {
        if (damageText == null) return;

        // 이전 연출 중단 후 재시작
        if (damageCoroutine != null) StopCoroutine(damageCoroutine);
        damageText.text = ((int)damage).ToString();
        damageCoroutine = StartCoroutine(FloatingDamageEffect());
    }

    public void SetSprite(Sprite sprite)
    {
        if (enemyImage != null) enemyImage.sprite = sprite;
    }


    IEnumerator FloatingDamageEffect()
    {
        float timer = 0f;

        // 위치/색상 초기화
        damageText.transform.position = damageTextOriginPos;
        damageText.color = damageTextOriginColor;

        while (timer < fadeTime)
        {
            timer += Time.deltaTime;
            damageText.transform.position += Vector3.up * floatSpeed * Time.deltaTime;
            damageText.color = new Color(
                damageTextOriginColor.r,
                damageTextOriginColor.g,
                damageTextOriginColor.b,
                Mathf.Lerp(1f, 0f, timer / fadeTime)
            );
            yield return null;
        }

        delDamageText();
    }

    void delDamageText()
    {
        if (damageText == null) return;
        damageText.text = "";
        damageText.transform.position = damageTextOriginPos;
        damageText.color = damageTextOriginColor;
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
