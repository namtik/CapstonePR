using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 화면 전환(맵 ↔ 스테이지) 공용 페이드. 모든 UI 위를 덮는 검은 오버레이를 하나만 두고 함께 쓴다.
// - Transition(mid): 어두워짐 → mid 실행(화면 교체) → 준비될 때까지 대기 → 밝아짐. 그동안 클릭을 막는다.
// - 이미 다른 연출(걷기 전환 등)이 화면을 덮고 있으면 mid를 즉시 실행하고, 밝히는 건 그 연출에 맡긴다.
// - 씬에 없으면 첫 호출 때 자동 생성된다. 시간은 unscaled라 일시정지와 무관하다.
public class ScreenFader : MonoBehaviour
{
    [Tooltip("어두워지는 시간(초).")]
    [SerializeField] private float fadeOutDuration = 0.3f;
    [Tooltip("새 화면이 밝아지는 시간(초).")]
    [SerializeField] private float fadeInDuration = 0.35f;
    [Tooltip("완전히 어두운 상태로 유지하는 최소 시간(초). 화면 교체가 한눈에 보이지 않게 한다.")]
    [SerializeField] private float holdDuration = 0.08f;
    [Tooltip("새 화면 준비(스테이지 로딩 등)를 기다리는 최대 시간(초).")]
    [SerializeField] private float readyTimeout = 5f;
    [SerializeField] private Color fadeColor = Color.black;

    static ScreenFader _instance;
    CanvasGroup _group;
    Image _image;
    bool _transitioning;

    public static ScreenFader Instance
    {
        get
        {
            if (_instance == null) _instance = FindFirstObjectByType<ScreenFader>();
            if (_instance == null) _instance = new GameObject("ScreenFader").AddComponent<ScreenFader>();
            return _instance;
        }
    }

    // 화면이 덮여 있거나 전환 중인지(이때 들어온 전환 요청은 즉시 실행)
    public static bool IsCovering => _instance != null && (_instance._transitioning || (_instance._group != null && _instance._group.alpha >= 0.99f));

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(this); return; }
        _instance = this;
        EnsureOverlay();
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // 페이드 아웃 → mid → (ready 대기) → 페이드 인
    public static void Transition(Action mid, Func<bool> ready = null)
    {
        if (IsCovering)
        {
            mid?.Invoke();
            return;
        }
        var f = Instance;
        if (!f.isActiveAndEnabled)
        {
            mid?.Invoke();
            return;
        }
        f.StartCoroutine(f.Run(mid, ready));
    }

    IEnumerator Run(Action mid, Func<bool> ready)
    {
        _transitioning = true;
        EnsureOverlay();
        _group.blocksRaycasts = true;

        yield return Fade(_group.alpha, 1f, fadeOutDuration);

        try { mid?.Invoke(); }
        catch (Exception e) { Debug.LogException(e); }

        // 새 화면이 최소 한 프레임 그려지고, 준비될 때까지 어두운 채로 기다린다
        float waited = 0f;
        yield return null;
        while (waited < holdDuration || (ready != null && !SafeReady(ready) && waited < readyTimeout))
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        yield return Fade(1f, 0f, fadeInDuration);
        _group.blocksRaycasts = false;
        _transitioning = false;
    }

    static bool SafeReady(Func<bool> ready)
    {
        try { return ready(); }
        catch (Exception e) { Debug.LogException(e); return true; }
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        if (duration <= 0f) { _group.alpha = to; yield break; }
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float k = t / duration;
            _group.alpha = Mathf.Lerp(from, to, k * k * (3f - 2f * k));
            yield return null;
        }
        _group.alpha = to;
    }

    // 다른 연출(걷기 전환)이 같은 오버레이를 직접 조절할 때 사용
    public void SetAlpha(float alpha)
    {
        EnsureOverlay();
        _group.alpha = Mathf.Clamp01(alpha);
    }

    public float Alpha => _group != null ? _group.alpha : 0f;

    public void SetBlocking(bool block)
    {
        EnsureOverlay();
        _group.blocksRaycasts = block;
    }

    // 모든 UI 위를 덮는 전용 오버레이 캔버스
    void EnsureOverlay()
    {
        if (_group != null) return;
        var go = new GameObject("ScreenFadeOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        go.transform.SetParent(transform, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        go.AddComponent<GraphicRaycaster>();
        _group = go.GetComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;

        _image = new GameObject("Fade", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        _image.transform.SetParent(go.transform, false);
        var r = _image.rectTransform;
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
        _image.color = fadeColor;
    }
}
