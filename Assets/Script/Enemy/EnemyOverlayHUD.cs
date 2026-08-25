using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 3D 몬스터용 적 UI(HP/행동게이지/데미지숫자/공격예고/패턴알림).
// 2D 전투와 같은 캔버스 좌표(EnemyPos)에 고정해 두고, EnemyView3D가 수치만 위임한다.
public class EnemyOverlayHUD : MonoBehaviour
{
    public static EnemyOverlayHUD Instance { get; private set; } // 씬에 상주하는 단일 패널

    [Header("표시 루트")]
    [Tooltip("미사용(구 팔로우 필드). 비워 두면 무시.")]
    [SerializeField] private RectTransform followRoot; // 호환용
    [SerializeField] private Vector2 screenOffset = new Vector2(0f, 120f); // 호환용(미사용)

    [Header("게이지/수치")]
    [SerializeField] private Slider hpBar; // 체력 바
    [SerializeField] private TMP_Text hpValueText; // HP 수치 텍스트
    [SerializeField] private Slider actionGaugeBar; // 행동 게이지 바
    [SerializeField] private TMP_Text actionGaugeValueText; // 게이지 수치 텍스트
    [SerializeField] private TMP_Text attackPreviewText; // 다음 공격 예고 텍스트

    [Header("패턴 알림")]
    [SerializeField] private GameObject patternNoticeObject; // 패턴 알림 오브젝트
    [SerializeField] private TMP_Text patternNoticeText; // 패턴 알림 텍스트
    [SerializeField] private float patternNoticeDuration = 1.6f; // 패턴 알림 표시 시간

    [Header("데미지 숫자")]
    [SerializeField] private TMP_Text damageTextTemplate; // 데미지 텍스트 템플릿(빈 상태 유지, 클론이 떠오름)
    [SerializeField] private float fadeTime = 1f; // 페이드 시간
    [SerializeField] private float floatSpeed = 60f; // 상승 속도(px/s)
    [SerializeField] private float baseScale = 1f; // 기본 배율
    [SerializeField] private float burstScale = 1.4f; // 출현 임팩트 배율
    [SerializeField] private float burstDuration = 0.15f; // 임팩트 수축 시간
    [SerializeField] private float randomXOffset = 40f; // 겹침 방지 X 오프셋
    [SerializeField] private float randomYOffset = 30f; // 겹침 방지 Y 오프셋
    [SerializeField] private string damagePrefix = "-"; // 데미지 접두어
    [SerializeField] private Color damageColor = new Color(1f, 0.92f, 0.3f, 1f); // 일반 데미지 색
    [SerializeField] private Color burnDamageColor = new Color(1f, 0.35f, 0.2f, 1f); // 화상 피해 색

    private int _gaugeMax = 20;     // 게이지 최대치(표시용)
    private Vector3 _damageOriginLocal; // 데미지 텍스트 원점
    private Coroutine _patternCo;   // 패턴 알림 코루틴

    // 싱글턴 등록 및 초기 상태 정리
    void Awake()
    {
        Instance = this;
        if (patternNoticeObject != null) patternNoticeObject.SetActive(false);
        if (damageTextTemplate != null)
        {
            _damageOriginLocal = damageTextTemplate.transform.localPosition;
            damageTextTemplate.text = "";
            damageTextTemplate.gameObject.SetActive(false); // 원본은 템플릿 — 켜 두면 "100"이 상주함
        }
        SetVisible(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // 3D 몬스터 전투 시작 — 2D와 같은 고정 좌표에 패널을 표시한다.
    public void Bind(Transform worldAnchor, Camera renderCamera, int gaugeMax)
    {
        _gaugeMax = Mathf.Max(1, gaugeMax);
        SetVisible(true);
    }

    // 바인딩 해제(몬스터 사망/전투 종료) — 패널을 숨긴다.
    public void Unbind()
    {
        SetVisible(false);
    }

    // HP/게이지만 켜고 끈다. damageText 원본은 클론 소스라 항상 꺼 둔다.
    void SetVisible(bool on)
    {
        if (hpBar != null) hpBar.gameObject.SetActive(on);
        if (actionGaugeBar != null) actionGaugeBar.gameObject.SetActive(on);
        if (damageTextTemplate != null)
        {
            damageTextTemplate.text = "";
            damageTextTemplate.gameObject.SetActive(false);
        }
        if (!on)
        {
            if (patternNoticeObject != null) patternNoticeObject.SetActive(false);
            ClearDamageClones();
        }
    }

    // 전투 종료 시 페이드 중이던 데미지 클론을 정리한다.
    void ClearDamageClones()
    {
        if (damageTextTemplate == null) return;
        Transform parent = damageTextTemplate.transform.parent;
        if (parent == null) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child == null || child == damageTextTemplate.transform) continue;
            if (child.name.StartsWith("DamageText_")) Destroy(child.gameObject);
        }
    }

    // ── EnemyView3D가 위임하는 표시 API ──

    // HP 바/수치 갱신
    public void SetHp(float current, float max)
    {
        if (hpBar != null) hpBar.value = max > 0f ? current / max : 0f;
        if (hpValueText != null)
            hpValueText.text = $"{Mathf.Max(0, Mathf.RoundToInt(current))}/{Mathf.Max(0, Mathf.RoundToInt(max))}";
    }

    // 행동 게이지 바/수치 갱신(0~1)
    public void SetGauge(float ratio)
    {
        if (actionGaugeBar != null) actionGaugeBar.value = ratio;
        if (actionGaugeValueText != null)
            actionGaugeValueText.text = $"{Mathf.Clamp(Mathf.RoundToInt(ratio * _gaugeMax), 0, _gaugeMax)}/{_gaugeMax}";
    }

    // 다음 공격 예고 데미지 표시
    public void SetPreview(int damage)
    {
        if (attackPreviewText != null) attackPreviewText.text = damage.ToString();
    }

    // 방해행동/특이사항 알림 표시(일정 시간 후 숨김)
    public void ShowNotice(string message)
    {
        if (patternNoticeObject == null || patternNoticeText == null) return;
        if (_patternCo != null) StopCoroutine(_patternCo);
        patternNoticeText.text = message;
        patternNoticeObject.SetActive(true);
        _patternCo = StartCoroutine(HideNoticeAfter());
    }

    IEnumerator HideNoticeAfter()
    {
        yield return new WaitForSeconds(patternNoticeDuration);
        if (patternNoticeText != null) patternNoticeText.text = "";
        if (patternNoticeObject != null) patternNoticeObject.SetActive(false);
    }

    // 데미지 숫자를 클론으로 띄운다(화상 여부로 색 분기)
    public void ShowDamage(float damage, bool isBurn)
    {
        if (damageTextTemplate == null || damage <= 0f) return;

        int dmg = Mathf.Max(0, (int)damage);
        string content = string.IsNullOrEmpty(damagePrefix) ? dmg.ToString() : $"{damagePrefix}{dmg}";
        Color c = isBurn ? burnDamageColor : damageColor;
        string hex = ColorUtility.ToHtmlStringRGB(c);

        var clone = Instantiate(damageTextTemplate, damageTextTemplate.transform.parent);
        clone.name = isBurn ? "DamageText_Burn" : "DamageText_Clone";
        clone.richText = true;
        clone.text = $"<color=#{hex}>{content}</color>";
        clone.gameObject.SetActive(true);
        StartCoroutine(FloatDamage(clone));
    }

    // 데미지 텍스트를 떠오르며 사라지게 한 뒤 파괴
    IEnumerator FloatDamage(TMP_Text inst)
    {
        if (inst == null) yield break;

        Vector3 start = _damageOriginLocal;
        if (randomXOffset > 0f) start.x += Random.Range(-randomXOffset, randomXOffset);
        if (randomYOffset > 0f) start.y += Random.Range(-randomYOffset, randomYOffset);
        inst.transform.localPosition = start;

        var cg = inst.GetComponent<CanvasGroup>();
        if (cg == null) cg = inst.gameObject.AddComponent<CanvasGroup>();
        if (cg != null) cg.alpha = 1f;

        Vector3 baseSize = Vector3.one * Mathf.Max(0.01f, baseScale);
        Vector3 burstSize = baseSize * Mathf.Max(1f, burstScale);
        inst.transform.localScale = burstSize;

        float t = 0f;
        while (t < fadeTime)
        {
            if (inst == null) yield break;
            t += Time.deltaTime;
            inst.transform.localPosition += Vector3.up * floatSpeed * Time.deltaTime;
            inst.transform.localScale = (t < burstDuration && burstDuration > 0f)
                ? Vector3.Lerp(burstSize, baseSize, 1f - (1f - t / burstDuration) * (1f - t / burstDuration))
                : baseSize;
            if (cg != null) cg.alpha = Mathf.Lerp(1f, 0f, t / fadeTime);
            yield return null;
        }
        if (inst != null) Destroy(inst.gameObject);
    }
}
