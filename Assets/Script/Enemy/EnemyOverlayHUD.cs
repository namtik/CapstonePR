using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 3D 몬스터용 적 UI(HP/행동게이지/데미지숫자/공격예고/패턴알림)를 Screen Space-Overlay 캔버스에 상주시키고,
// 스폰된 3D 몬스터의 월드 위치를 매 프레임 WorldToScreen으로 따라다니게 하는 패널.
// EnemyView3D가 이 패널로 수치/텍스트 표시를 위임한다. (상태이상 아이콘은 기존 StatusPanelUI가 담당)
public class EnemyOverlayHUD : MonoBehaviour
{
    public static EnemyOverlayHUD Instance { get; private set; } // 씬에 상주하는 단일 패널

    [Header("팔로우")]
    [Tooltip("몬스터의 스크린 위치로 이동시킬 루트. 이 아래에 HP/게이지/데미지 등을 둔다. 비우면 이 오브젝트 자신을 사용.")]
    [SerializeField] private RectTransform followRoot; // 몬스터 스크린 위치로 이동하는 루트
    [Tooltip("몬스터 앵커에서 UI를 얼마나 띄울지(스크린 픽셀 오프셋).")]
    [SerializeField] private Vector2 screenOffset = new Vector2(0f, 120f); // 스크린 오프셋

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

    private Camera _followCam;      // 몬스터를 렌더하는 스테이지 카메라
    private Transform _followWorld; // 따라갈 몬스터 월드 앵커
    private bool _following;        // 팔로우 활성 여부
    private int _gaugeMax = 20;     // 게이지 최대치(표시용)
    private Vector3 _damageOriginLocal; // 데미지 텍스트 원점(팔로우 루트 로컬)
    private Coroutine _patternCo;   // 패턴 알림 코루틴

    // 싱글턴 등록 및 초기 상태 정리
    void Awake()
    {
        Instance = this;
        if (followRoot == null) followRoot = transform as RectTransform;
        if (patternNoticeObject != null) patternNoticeObject.SetActive(false);
        if (damageTextTemplate != null)
        {
            _damageOriginLocal = damageTextTemplate.transform.localPosition;
            damageTextTemplate.text = "";
        }
        SetVisible(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // 스폰된 몬스터에 바인딩 — 따라갈 월드 앵커와 렌더 카메라를 지정하고 패널을 표시한다.
    public void Bind(Transform worldAnchor, Camera renderCamera, int gaugeMax)
    {
        _followWorld = worldAnchor;
        _followCam = renderCamera != null ? renderCamera : Camera.main;
        _following = worldAnchor != null;
        _gaugeMax = Mathf.Max(1, gaugeMax);
        SetVisible(true);
    }

    // 바인딩 해제(몬스터 사망/전투 종료) — 패널을 숨긴다.
    public void Unbind()
    {
        _following = false;
        _followWorld = null;
        SetVisible(false);
    }

    // 패널 표시/숨김
    void SetVisible(bool on)
    {
        if (followRoot != null) followRoot.gameObject.SetActive(on);
    }

    // 매 프레임 몬스터의 스크린 위치로 패널을 이동(카메라 뒤면 숨김)
    void LateUpdate()
    {
        if (!_following || _followWorld == null || _followCam == null) return;

        Vector3 sp = _followCam.WorldToScreenPoint(_followWorld.position);
        bool behind = sp.z < 0f; // 카메라 뒤
        if (followRoot.gameObject.activeSelf == behind) followRoot.gameObject.SetActive(!behind);
        if (behind) return;

        // Overlay 캔버스에서 RectTransform.position은 스크린 픽셀 좌표 → WorldToScreenPoint 값 직접 대입
        followRoot.position = new Vector3(sp.x + screenOffset.x, sp.y + screenOffset.y, 0f);
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

        var cg = inst.GetComponent<CanvasGroup>() ?? inst.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 1f;

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
            cg.alpha = Mathf.Lerp(1f, 0f, t / fadeTime);
            yield return null;
        }
        if (inst != null) Destroy(inst.gameObject);
    }
}
