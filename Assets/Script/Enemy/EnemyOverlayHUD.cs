using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 3D 몬스터용 적 UI(HP/행동게이지/데미지숫자/공격예고/패턴알림).
// 적마다 바를 복제해 머리 위에 붙인다. 1마리도 같다.
public class EnemyOverlayHUD : MonoBehaviour
{
    const string BarRootName = "EnemyHudBars";

    public static EnemyOverlayHUD Instance { get; private set; } // 씬에 상주하는 단일 패널

    public RectTransform HpBarRect => hpBar != null ? hpBar.transform as RectTransform : null;

    // 활성 HUD 전체(적마다 복제본 포함) — 적 행동 중 HP 바 일괄 숨김용
    static readonly System.Collections.Generic.List<EnemyOverlayHUD> _all = new System.Collections.Generic.List<EnemyOverlayHUD>();
    public static void CollectHpBars(System.Collections.Generic.List<GameObject> into)
    {
        for (int i = 0; i < _all.Count; i++)
        {
            var h = _all[i];
            if (h == null || h.hpBar == null) continue;
            if (!into.Contains(h.hpBar.gameObject)) into.Add(h.hpBar.gameObject);
            // 게이지가 HP 바 밖에 따로 있으면 함께
            if (h.actionGaugeBar != null && !h.actionGaugeBar.transform.IsChildOf(h.hpBar.transform)
                && !into.Contains(h.actionGaugeBar.gameObject))
                into.Add(h.actionGaugeBar.gameObject);
        }
    }

    void OnEnable() { if (!_all.Contains(this)) _all.Add(this); }
    void OnDisable() { _all.Remove(this); }

    public void SetSpecialNote(string note)
    {
        var badge = GetComponent<EnemyNoteBadge>();
        if (badge == null) badge = gameObject.AddComponent<EnemyNoteBadge>();
        // 특이사항 말풍선은 부가 UI — 여기서 예외가 나도 적 스폰/전투 시작이 끊기지 않게 한다
        try { badge.Bind(HpBarRect, note); }
        catch (System.Exception e) { Debug.LogException(e); }
    }

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
    [Tooltip("체력바 왼쪽 끝에서 패턴 알림을 더 옮긴다. x가 음수면 더 왼쪽.")]
    [SerializeField] private Vector2 patternNoticeOffset = new Vector2(-12f, 0f);

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
    private Transform _worldAnchor; // 월드 팔로우 앵커(다중 전투)
    private Camera _renderCamera;   // 월드→스크린 변환 카메라
    private bool _followWorld;      // 월드 좌표를 따라갈지 여부
    private RectTransform _barRoot; // 이 적 전용 바 묶음
    private Renderer[] _bodyRenderers; // 머리 위치 계산용 메시
    private float _barScale = 1f;       // HP/행동 게이지 배율. 양옆도 가운데와 동일
    private Vector2 _hpBasePos;         // 스케일 1일 때 HP 위치
    private Vector2 _gaugeBasePos;      // 스케일 1일 때 행동 게이지 위치
    private bool _barBaseCaptured;      // 기준 위치 저장 여부
    private bool _headLocked;           // 머리 위치를 한 번 잡았는지
    private Vector3 _lockedHead;        // 전투 시작 시 머리 월드 좌표. 애니메이션·피격 흔들림은 무시한다.
    private Vector2 _screenOffset;      // 이 적만 추가로 내리는 화면 픽셀. 전역 오프셋에 더한다.
    private RoundManager _roundManager; // 바 높이 조절값. 씬에 있는 RoundManager에서 읽는다.

    const float DefaultOffsetX = 0f;
    const float DefaultOffsetY = -60f;

    // 싱글턴 등록 및 초기 상태 정리
    void Awake()
    {
        if (Instance == null) Instance = this;
        AdoptClonedBars();
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

    // 3D 몬스터 전투 시작. followWorld면 이 적 전용 바를 만들어 머리 위에 붙인다.
    public void Bind(Transform worldAnchor, Camera renderCamera, int gaugeMax, bool followWorld = false, float barScale = 1f, Vector2 screenOffset = default)
    {
        _gaugeMax = Mathf.Max(1, gaugeMax);
        _worldAnchor = worldAnchor;
        _renderCamera = renderCamera;
        _followWorld = followWorld && worldAnchor != null;
        _barScale = Mathf.Max(0.01f, barScale);
        _screenOffset = screenOffset;
        _headLocked = false;
        if (_roundManager == null) _roundManager = FindFirstObjectByType<RoundManager>();
        if (_followWorld) PreparePerEnemyBars();
        if (_barRoot != null) ApplyBarScale();
        CacheBodyRenderers();
        SetVisible(true);
        FollowAnchor();
    }

    // 이 앵커에 묶여 있을 때만 해제한다. 파괴가 프레임 끝에 일어나므로, 그 사이 같은 HUD를 새 몬스터가
    // 다시 Bind했다면(스테이지 다시 시작 F3 등) 옛 몬스터의 OnDestroy가 새 몬스터의 HP바를 숨기지 않게 한다.
    public void UnbindIfBoundTo(Transform anchor)
    {
        if (_worldAnchor != null && _worldAnchor != anchor) return;
        Unbind();
    }

    // 바인딩 해제(몬스터 사망/전투 종료) — 패널을 숨긴다.
    public void Unbind()
    {
        _followWorld = false;
        _worldAnchor = null;
        _headLocked = false;
        SetVisible(false);
    }

    // 씬에 있는 HUD를 복제해 다른 몬스터 전용 패널을 만든다.
    // 바를 먼저 이 오브젝트 자식으로 옮겨 두어야 클론이 같은 슬라이더를 공유하지 않는다.
    public EnemyOverlayHUD CreateClone()
    {
        PreparePerEnemyBars();
        // 복제 순간에는 원본 크기여야 클론이 이미 줄어든 바를 복사하지 않는다.
        float savedScale = _barScale;
        _barScale = 1f;
        ApplyBarScale();
        GameObject go = Instantiate(gameObject, transform.parent);
        _barScale = savedScale;
        ApplyBarScale();
        go.name = gameObject.name + "_Clone";
        var hud = go.GetComponent<EnemyOverlayHUD>();
        if (hud != null)
        {
            hud.AdoptClonedBars();
            hud.SetVisible(false);
        }
        return hud;
    }

    void LateUpdate()
    {
        FollowAnchor();
    }

    // EnemyPos에 하나뿐인 바를 이 HUD 아래로 복제하고, 원본은 꺼 둔다.
    void PreparePerEnemyBars()
    {
        if (_barRoot != null) return;
        if (AdoptClonedBars()) return;

        GameObject srcHp = hpBar != null ? hpBar.gameObject : null;
        GameObject srcGauge = actionGaugeBar != null ? actionGaugeBar.gameObject : null;
        GameObject srcDamage = damageTextTemplate != null ? damageTextTemplate.gameObject : null;
        GameObject srcNotice = patternNoticeObject;
        // 게이지는 HP바 자식이다. HP를 복제하면 같이 따라오므로 따로 복제하지 않는다.
        bool gaugeInsideHp = srcGauge != null && srcHp != null && srcGauge.transform.IsChildOf(srcHp.transform);

        _barRoot = CreateBarRoot();

        GameObject hp = CloneWidget(srcHp);
        GameObject gauge = gaugeInsideHp ? null : CloneWidget(srcGauge);
        GameObject damage = CloneWidget(srcDamage);
        GameObject notice = CloneWidget(srcNotice);
        Rewire(hp, gauge, damage, notice);
        LayoutWidgets();

        HideSceneTemplate(srcHp);
        HideSceneTemplate(srcGauge);
        HideSceneTemplate(srcDamage);
        HideSceneTemplate(srcNotice);
    }

    RectTransform CreateBarRoot()
    {
        var go = new GameObject(BarRootName, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(800f, 160f);
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one;
        return rt;
    }

    GameObject CloneWidget(GameObject src)
    {
        if (src == null || _barRoot == null) return null;
        var go = Instantiate(src, _barRoot);
        go.name = src.name;
        return go;
    }

    void Rewire(GameObject hp, GameObject gauge, GameObject damage, GameObject notice)
    {
        if (hp != null)
        {
            hpBar = hp.GetComponent<Slider>();
            hpValueText = FindNamed<TMP_Text>(hp.transform, "HpValueText");
            AlignHpReadout(hpValueText);
            // HP 바 안에 들어 있는 게이지가 세트다. 루트에 따로 붙은 복제본은 지운다.
            var nested = hp.transform.Find("gaugeSlider");
            if (nested != null)
            {
                if (gauge != null && gauge != nested.gameObject)
                    Destroy(gauge);
                gauge = nested.gameObject;
            }
        }
        if (gauge != null)
        {
            actionGaugeBar = gauge.GetComponent<Slider>();
            actionGaugeValueText = FindNamed<TMP_Text>(gauge.transform, "GaugeValueText");
            attackPreviewText = FindNamed<TMP_Text>(gauge.transform, "attackPreviewText");
        }
        if (damage != null)
            damageTextTemplate = damage.GetComponent<TMP_Text>();
        if (notice != null)
        {
            patternNoticeObject = notice;
            patternNoticeText = FindNamed<TMP_Text>(notice.transform, "PatternNoticeText");
        }
    }

    // 복제된 HUD는 직렬화 참조가 원본 바를 가리킬 수 있다. 자식 이름으로 다시 연결한다.
    bool AdoptClonedBars()
    {
        if (_barRoot == null)
            _barRoot = transform.Find(BarRootName) as RectTransform;
        if (_barRoot == null) return false;

        var hp = _barRoot.Find("HpBar");
        var gauge = _barRoot.Find("gaugeSlider");
        var damage = _barRoot.Find("damageText");
        var notice = _barRoot.Find("PatternNoticeRoot");
        if (hp == null && gauge == null) return false;

        Rewire(
            hp != null ? hp.gameObject : null,
            gauge != null ? gauge.gameObject : null,
            damage != null ? damage.gameObject : null,
            notice != null ? notice.gameObject : null);
        return true;
    }

    void LayoutWidgets()
    {
        var hpRt = hpBar != null ? hpBar.transform as RectTransform : null;
        var gaugeRt = actionGaugeBar != null ? actionGaugeBar.transform as RectTransform : null;
        float hpHalf = hpRt != null ? hpRt.sizeDelta.y * 0.5f : 25f;
        bool gaugeInsideHp = gaugeRt != null && hpRt != null && gaugeRt.IsChildOf(hpRt);
        // HP바의 씬 좌표(y 약 -317)와 게이지의 로컬 좌표(y 약 5.5)는 부모가 다르다.
        // 둘을 빼서 간격을 만들면 게이지만 수백 px 아래로 나간다. 값은 그대로라 작동은 된다.
        Vector2 gaugeAnchorMin = Vector2.zero, gaugeAnchorMax = Vector2.zero, gaugePivot = Vector2.zero;
        Vector2 gaugePos = Vector2.zero, gaugeSize = Vector2.zero;
        if (gaugeInsideHp)
        {
            gaugeAnchorMin = gaugeRt.anchorMin;
            gaugeAnchorMax = gaugeRt.anchorMax;
            gaugePivot = gaugeRt.pivot;
            gaugePos = gaugeRt.anchoredPosition;
            gaugeSize = gaugeRt.sizeDelta;
        }

        // 세트 바닥이 머리 기준점에 닿게 올린다. HP 안 게이지는 원래 자리를 유지한다.
        PlaceBar(hpRt, new Vector2(0f, hpHalf));
        if (gaugeInsideHp)
        {
            gaugeRt.anchorMin = gaugeAnchorMin;
            gaugeRt.anchorMax = gaugeAnchorMax;
            gaugeRt.pivot = gaugePivot;
            gaugeRt.anchoredPosition = gaugePos;
            gaugeRt.sizeDelta = gaugeSize;
        }
        else
        {
            float gaugeHalf = gaugeRt != null ? gaugeRt.sizeDelta.y * 0.5f : 4f;
            PlaceBar(gaugeRt, new Vector2(0f, hpHalf * 2f + 6f + gaugeHalf));
        }
        if (hpRt != null) _hpBasePos = hpRt.anchoredPosition;
        if (gaugeRt != null) _gaugeBasePos = gaugeRt.anchoredPosition;
        _barBaseCaptured = hpRt != null || gaugeRt != null;

        float aboveBars = hpHalf * 2f + 28f;
        PlaceFree(damageTextTemplate != null ? damageTextTemplate.transform as RectTransform : null, new Vector2(0f, aboveBars));
        PlacePatternNotice(hpRt);
        if (damageTextTemplate != null)
        {
            damageTextTemplate.text = "";
            _damageOriginLocal = damageTextTemplate.transform.localPosition;
        }
    }

    // HP/행동 게이지에 배율을 적용한다. 둘의 간격 비율은 유지한다.
    void ApplyBarScale()
    {
        CaptureBarBaseIfNeeded();
        var hpRt = hpBar != null ? hpBar.transform as RectTransform : null;
        var gaugeRt = actionGaugeBar != null ? actionGaugeBar.transform as RectTransform : null;
        bool gaugeInsideHp = gaugeRt != null && hpRt != null && gaugeRt.IsChildOf(hpRt);
        if (hpRt != null)
        {
            hpRt.localScale = Vector3.one * _barScale;
            hpRt.anchoredPosition = new Vector2(_hpBasePos.x, _hpBasePos.y * _barScale);
        }
        if (gaugeRt != null && !gaugeInsideHp)
        {
            gaugeRt.localScale = Vector3.one * _barScale;
            gaugeRt.anchoredPosition = new Vector2(_gaugeBasePos.x, _gaugeBasePos.y * _barScale);
        }
    }

    void CaptureBarBaseIfNeeded()
    {
        if (_barBaseCaptured) return;
        var hpRt = hpBar != null ? hpBar.transform as RectTransform : null;
        var gaugeRt = actionGaugeBar != null ? actionGaugeBar.transform as RectTransform : null;
        if (hpRt != null) _hpBasePos = hpRt.anchoredPosition;
        if (gaugeRt != null) _gaugeBasePos = gaugeRt.anchoredPosition;
        _barBaseCaptured = hpRt != null || gaugeRt != null;
    }

    static void PlaceBar(RectTransform rt, Vector2 pos)
    {
        if (rt == null) return;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;
        rt.anchoredPosition = pos;
    }

    // 패턴 알림은 씬 좌표를 쓰지 않는다. 체력바 왼쪽 끝에 글자 오른쪽을 붙인다.
    void PlacePatternNotice(RectTransform hpRt)
    {
        if (patternNoticeObject == null) return;

        float barWidth = 0f;
        if (hpRt != null) barWidth = hpRt.rect.width > 1f ? hpRt.rect.width : hpRt.sizeDelta.x;
        float barLeft = hpRt != null ? hpRt.anchoredPosition.x - barWidth * 0.5f : 0f;
        float barY = hpRt != null ? hpRt.anchoredPosition.y : 0f;
        var rt = patternNoticeObject.transform as RectTransform;
        if (rt == null) rt = patternNoticeObject.AddComponent<RectTransform>();
        PlaceFree(rt, new Vector2(barLeft, barY) + patternNoticeOffset);

        if (patternNoticeText == null) return;
        RectTransform textRt = patternNoticeText.rectTransform;
        textRt.anchorMin = textRt.anchorMax = new Vector2(0.5f, 0.5f);
        textRt.pivot = new Vector2(1f, 0.5f);
        textRt.anchoredPosition = Vector2.zero;
        patternNoticeText.alignment = TextAlignmentOptions.MidlineRight;
    }

    static void PlaceFree(RectTransform rt, Vector2 pos)
    {
        if (rt == null) return;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;
        rt.anchoredPosition = pos;
    }

    // EnemyPos에 남은 원본만 끈다. 다른 적의 전용 바는 건드리지 않는다.
    void HideSceneTemplate(GameObject src)
    {
        if (src == null) return;
        if (src.transform.IsChildOf(transform)) return;
        if (src.transform.parent != null && src.transform.parent.name == BarRootName) return;
        src.SetActive(false);
    }

    void CacheBodyRenderers()
    {
        _bodyRenderers = null;
        Transform root = ResolveBoundsRoot();
        if (root == null) return;
        _bodyRenderers = root.GetComponentsInChildren<Renderer>(true);
    }

    Transform ResolveBoundsRoot()
    {
        if (_worldAnchor == null) return null;
        if (_worldAnchor.GetComponentInChildren<Renderer>(true) == null && _worldAnchor.parent != null)
            return _worldAnchor.parent;
        return _worldAnchor;
    }

    // 전용 바가 있으면 머리 위에 붙인다.
    void FollowAnchor()
    {
        if (_barRoot == null || _worldAnchor == null) return;
        Camera cam = _renderCamera != null ? _renderCamera : Camera.main;
        if (cam == null) return;

        if (!_headLocked && TrySampleHead(out Vector3 sampled))
        {
            _lockedHead = sampled;
            _headLocked = true;
        }

        float offsetX = _roundManager != null ? _roundManager.EnemyBarOffsetX : DefaultOffsetX;
        float offsetY = _roundManager != null ? _roundManager.EnemyBarOffsetY : DefaultOffsetY;
        Vector3 world = _headLocked ? _lockedHead : _worldAnchor.position + Vector3.up * 1.6f;
        Vector3 sp = cam.WorldToScreenPoint(world);
        if (sp.z < 0f) return;
        _barRoot.position = new Vector3(sp.x + offsetX + _screenOffset.x, sp.y + offsetY + _screenOffset.y, 0f);
    }

    // 메시 꼭대기를 한 번만 읽는다. 이후 공격·대기·피격 흔들림은 바 위치에 반영하지 않는다.
    bool TrySampleHead(out Vector3 point)
    {
        point = _worldAnchor.position + Vector3.up * 1.6f;
        if (_bodyRenderers == null || _bodyRenderers.Length == 0) return false;

        // 몸체(스킨드 메시)가 있으면 그것만 기준으로 삼고, 크기 0인 부속은 뺀다.
        // 스킨드 bounds는 본 기준이라 실제 정점보다 여유가 있어(우산귀 HP바가 너무 높던 원인) 구운 정점 최고점으로 좁힌다.
        bool any = SampleBodyTop(true, out Bounds bounds, out float top);
        if (!any) any = SampleBodyTop(false, out bounds, out top); // 스킨드 메시가 없는 모델은 일반 렌더러로
        if (!any) return false;
        point = new Vector3(bounds.center.x, top, bounds.center.z);
        return true;
    }

    // skinnedOnly: 스킨드 메시만 볼지. top은 실제 정점 기준 최고점(가능하면), 아니면 bounds.max.y
    bool SampleBodyTop(bool skinnedOnly, out Bounds bounds, out float top)
    {
        bounds = default;
        top = float.NegativeInfinity;
        bool any = false;
        for (int i = 0; i < _bodyRenderers.Length; i++)
        {
            Renderer r = _bodyRenderers[i];
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            bool skinned = r is SkinnedMeshRenderer;
            if (skinnedOnly && !skinned) continue;
            Bounds b = r.bounds;
            if (b.size.sqrMagnitude < 1e-6f) continue; // 스케일 0으로 숨겨 둔 부속(물방울 등)
            if (!any) { bounds = b; any = true; }
            else bounds.Encapsulate(b);

            float rTop = b.max.y;
            // 구운 최고점이 bounds 높이의 위쪽 절반 안에 있을 때만 믿는다(몸 중앙으로 떨어지는 오계산 방지)
            if (skinned && TryBakedTop((SkinnedMeshRenderer)r, b, out float baked)
                && baked <= b.max.y + 0.01f && baked >= b.center.y)
                rTop = baked; // 스킨드 bounds는 본 기준이라 여유가 있다 — 현재 포즈 정점 최고점으로 좁힌다
            if (rTop > top) top = rTop;
        }
        return any;
    }

    static Mesh _bakeMesh;

    // 현재 포즈를 구워 월드 기준 정점 최고 높이를 구한다(전투 시작 때 한 번만 호출)
    // 구운 정점을 월드로 옮길 때 부모 스케일 반영 여부가 헷갈리기 쉽다(우산귀는 루트 스케일 2라 잘못 옮기면 높이가 절반 → 몸 중앙).
    // 두 방식으로 옮겨 보고, 렌더러 bounds와 높이 범위가 더 잘 맞는 쪽을 쓴다.
    static bool TryBakedTop(SkinnedMeshRenderer smr, Bounds reference, out float top)
    {
        top = 0f;
        if (smr == null || smr.sharedMesh == null || !smr.sharedMesh.isReadable) return false;
        if (_bakeMesh == null) _bakeMesh = new Mesh { name = "EnemyHudBake" };
        try
        {
            smr.BakeMesh(_bakeMesh, true);
            Vector3[] verts = _bakeMesh.vertices;
            if (verts == null || verts.Length == 0) return false;
            Transform t = smr.transform;
            Matrix4x4 withScale = t.localToWorldMatrix;
            Matrix4x4 noScale = Matrix4x4.TRS(t.position, t.rotation, Vector3.one);
            float minA = float.PositiveInfinity, maxA = float.NegativeInfinity;
            float minB = float.PositiveInfinity, maxB = float.NegativeInfinity;
            for (int i = 0; i < verts.Length; i++)
            {
                float ya = withScale.MultiplyPoint3x4(verts[i]).y;
                float yb = noScale.MultiplyPoint3x4(verts[i]).y;
                if (ya < minA) minA = ya; if (ya > maxA) maxA = ya;
                if (yb < minB) minB = yb; if (yb > maxB) maxB = yb;
            }
            float errA = Mathf.Abs(minA - reference.min.y) + Mathf.Abs(maxA - reference.max.y);
            float errB = Mathf.Abs(minB - reference.min.y) + Mathf.Abs(maxB - reference.max.y);
            top = errA <= errB ? maxA : maxB;
            return !float.IsInfinity(top);
        }
        catch (System.Exception)
        {
            return false;
        }
    }

    static T FindNamed<T>(Transform root, string childName) where T : Component
    {
        if (root == null) return null;
        var all = root.GetComponentsInChildren<T>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].gameObject.name == childName)
                return all[i];
        }
        return null;
    }

    // HP/게이지만 켜고 끈다. damageText 원본은 클론 소스라 항상 꺼 둔다.
    void SetVisible(bool on)
    {
        if (hpBar != null) hpBar.gameObject.SetActive(on);
        if (actionGaugeBar != null) actionGaugeBar.gameObject.SetActive(on);
        if (!on)
        {
            var badge = GetComponent<EnemyNoteBadge>();
            if (badge != null) badge.HideBubble();
            var intent = GetComponent<EnemyAttackIntent>();
            if (intent != null) intent.Hide();
            EnemyPowerUpMark.Apply(HpBarRect, false);
        }
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
        {
            AlignHpReadout(hpValueText);
            hpValueText.text = $"{Mathf.Max(0, Mathf.RoundToInt(current))}/{Mathf.Max(0, Mathf.RoundToInt(max))}";
        }
    }

    // 숫자가 바 한가운데에 오게 맞춘다. 왼쪽 정렬 + 오른쪽 여백이면 두 자리는 가운데처럼 보이고 세 자리부터 한쪽으로 밀린다.
    public static void AlignHpReadout(TMP_Text text)
    {
        if (text == null) return;

        RectTransform rt = text.rectTransform;
        RectTransform parent = rt.parent as RectTransform;
        float barWidth = 0f;
        if (parent != null)
        {
            barWidth = parent.rect.width;
            if (barWidth < 1f) barWidth = parent.sizeDelta.x;
        }

        float y = rt.anchoredPosition.y;
        float height = rt.sizeDelta.y > 1f ? rt.sizeDelta.y : 50f;
        float width = Mathf.Max(rt.sizeDelta.x, barWidth);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(width, height);

        text.alignment = TextAlignmentOptions.Center;
        Vector4 margin = text.margin;
        text.margin = new Vector4(0f, margin.y, 0f, margin.w);
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
    }

    // 행동 게이지 바/수치 갱신(0~1)
    public void SetGauge(float ratio)
    {
        if (actionGaugeBar != null) actionGaugeBar.value = ratio;
        if (actionGaugeValueText != null)
            actionGaugeValueText.text = $"{Mathf.Clamp(Mathf.RoundToInt(ratio * _gaugeMax), 0, _gaugeMax)}/{_gaugeMax}";
    }
    public void SetPreview(int damage)
    {
        if (attackPreviewText != null) attackPreviewText.text = damage.ToString();
        var intent = GetComponent<EnemyAttackIntent>();
        if (intent == null) intent = gameObject.AddComponent<EnemyAttackIntent>();
        intent.SetDamage(HpBarRect, damage);
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
