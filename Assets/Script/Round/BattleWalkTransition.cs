using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 전투 보상이 끝난 뒤 맵으로 돌아가기 전, "다음 장소로 걸어가는" 연출.
// 카드(손패) HUD를 잠시 숨기고, 3D 스테이지 카메라를 앞으로 걸어가듯(머리 흔들림 포함) 이동시킨 뒤
// 화면을 어둡게 덮고 onDone(맵 복귀)을 호출한다. 연출이 끝나면 카메라 위치·HUD 표시를 원래대로 되돌린다.
public class BattleWalkTransition : MonoBehaviour
{
    [Header("걷기")]
    [Tooltip("걷는 시간(초). 1~2초 권장.")]
    [SerializeField] private float walkDuration = 1.6f;
    [Tooltip("카메라가 앞으로 나아가는 거리(월드 미터).")]
    [SerializeField] private float walkDistance = 3.5f;
    [Tooltip("초당 걸음 수 — 머리 흔들림 주기.")]
    [SerializeField] private float stepsPerSecond = 1.8f;
    [Tooltip("걸음마다 위아래로 흔들리는 높이(월드 미터).")]
    [SerializeField] private float bobHeight = 0.06f;
    [Tooltip("걸음마다 좌우로 기우는 각도(도).")]
    [SerializeField] private float swayRollDegrees = 0.8f;

    [Header("UI")]
    [Tooltip("카드 HUD가 사라지는/다시 나타나는 시간(초).")]
    [SerializeField] private float hudFadeDuration = 0.25f;
    [Tooltip("걷기 막바지에 화면을 어둡게 덮는 시간(초).")]
    [SerializeField] private float fadeToBlackDuration = 0.35f;
    [Tooltip("맵이 뜬 뒤 어둠이 걷히는 시간(초).")]
    [SerializeField] private float fadeFromBlackDuration = 0.3f;

    public bool IsPlaying { get; private set; }

    CanvasGroup _blackGroup;

    // 걷기 연출 후 onDone 호출. 이미 재생 중이면 무시.
    public void Play(System.Action onDone)
    {
        if (IsPlaying) return;
        StartCoroutine(Run(onDone));
    }

    IEnumerator Run(System.Action onDone)
    {
        IsPlaying = true;
        EnsureBlackOverlay();
        _blackGroup.blocksRaycasts = true; // 연출 중 클릭 차단

        // 1) 카드(손패) HUD 숨김
        CanvasGroup hud = ResolveHandHudGroup();
        float hudAlpha = hud != null ? hud.alpha : 1f;
        if (hud != null) yield return FadeGroup(hud, hud.alpha, 0f, hudFadeDuration);

        // 2) 스테이지 카메라 전진 + 머리 흔들림
        var stage = BattleStageController.Current;
        Camera cam = stage != null ? stage.StageCamera : null;
        bool canWalk = cam != null && cam.isActiveAndEnabled;
        Transform camT = canWalk ? cam.transform : null;
        Vector3 startPos = camT != null ? camT.position : Vector3.zero;
        Quaternion startRot = camT != null ? camT.rotation : Quaternion.identity;
        Vector3 forward = Vector3.zero;
        if (camT != null)
        {
            forward = Vector3.ProjectOnPlane(camT.forward, Vector3.up);
            forward = forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;
        }

        float duration = Mathf.Max(0.1f, walkDuration);
        float fadeStart = duration - Mathf.Min(fadeToBlackDuration, duration);
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float u = t / duration;
            if (camT != null)
            {
                float travel = EaseInOut(u) * walkDistance;
                // 출발·도착 시엔 흔들림이 잦아들도록 진폭을 sin 엔벨로프로 감쌈
                float envelope = Mathf.Sin(u * Mathf.PI);
                float phase = t * stepsPerSecond * Mathf.PI * 2f;
                float bob = Mathf.Abs(Mathf.Sin(phase * 0.5f)) * bobHeight * envelope;
                float roll = Mathf.Sin(phase * 0.5f) * swayRollDegrees * envelope;
                camT.position = startPos + forward * travel + Vector3.up * bob;
                camT.rotation = startRot * Quaternion.Euler(0f, 0f, roll);
            }
            if (t >= fadeStart && fadeToBlackDuration > 0f)
                _blackGroup.alpha = Mathf.Clamp01((t - fadeStart) / fadeToBlackDuration);
            yield return null;
        }
        _blackGroup.alpha = 1f;

        // 3) 어두운 화면 뒤에서 원상 복구 후 맵 복귀
        if (camT != null)
        {
            camT.position = startPos;
            camT.rotation = startRot;
        }
        if (hud != null) hud.alpha = hudAlpha;

        onDone?.Invoke();

        yield return null; // 맵 UI가 한 프레임 그려진 뒤 어둠을 걷음
        yield return FadeGroup(_blackGroup, 1f, 0f, fadeFromBlackDuration);
        _blackGroup.blocksRaycasts = false;
        IsPlaying = false;
    }

    static float EaseInOut(float u) => u * u * (3f - 2f * u);

    static IEnumerator FadeGroup(CanvasGroup g, float from, float to, float duration)
    {
        if (duration <= 0f) { g.alpha = to; yield break; }
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            g.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        g.alpha = to;
    }

    // 손패 HUD의 CanvasGroup(없으면 추가)
    static CanvasGroup ResolveHandHudGroup()
    {
        var hud = FindFirstObjectByType<Battle.UI.CardHandHUD>();
        if (hud == null || !hud.gameObject.activeInHierarchy) return null;
        var g = hud.GetComponent<CanvasGroup>();
        if (g == null) g = hud.gameObject.AddComponent<CanvasGroup>();
        return g;
    }

    // 모든 UI 위를 덮는 전용 오버레이 캔버스(검은 화면)
    void EnsureBlackOverlay()
    {
        if (_blackGroup != null) return;
        var go = new GameObject("WalkTransitionOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        go.transform.SetParent(transform, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        go.AddComponent<GraphicRaycaster>();
        _blackGroup = go.GetComponent<CanvasGroup>();
        _blackGroup.alpha = 0f;
        _blackGroup.blocksRaycasts = false;
        _blackGroup.interactable = false;

        var img = new GameObject("Black", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.transform.SetParent(go.transform, false);
        var r = img.rectTransform;
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
        img.color = Color.black;
    }
}
