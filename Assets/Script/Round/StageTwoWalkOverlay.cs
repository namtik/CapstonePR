using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The TS2 atlas contains the 22 diagonal brush poses in reading order, four per row.
public sealed class StageTwoWalkOverlay : MonoBehaviour
{
    [SerializeField] RawImage brush;
    [SerializeField] CanvasGroup title;
    [SerializeField] Text titleLabel;
    [SerializeField] CanvasGroup bookNotice;
    [SerializeField] AudioSource soundtrack;
    [SerializeField, Min(0.01f)] float brushDuration = 0.6f;
    [SerializeField, Min(0.01f)] float titleDuration = 1.73f;
    [SerializeField, Min(0.01f)] float revealDuration = 0.67f;

    bool showBookNotice;
    EventSystem menuEvents;
    bool previousNavigation;

    public static StageTwoWalkOverlay Open(Transform parent, bool bookAcquired, int stageNumber = 2)
    {
        var prefab = Resources.Load<GameObject>("UI/StageTwoWalkOverlay");
        if (prefab == null)
        {
            Debug.LogWarning($"[StageTwoWalk] 스테이지 {stageNumber} 전환 프리팹을 찾을 수 없습니다.");
            return null;
        }
        var overlay = Instantiate(prefab, parent, false).GetComponent<StageTwoWalkOverlay>();
        overlay.titleLabel.text = $"스테이지 {stageNumber}";
        overlay.showBookNotice = bookAcquired;
        overlay.bookNotice.alpha = bookAcquired ? 1f : 0f;
        overlay.title.alpha = 0f;
        overlay.SetBrush(0f);
        overlay.menuEvents = EventSystem.current;
        if (overlay.menuEvents != null)
        {
            overlay.previousNavigation = overlay.menuEvents.sendNavigationEvents;
            overlay.menuEvents.sendNavigationEvents = false;
        }
        return overlay;
    }

    public void BeginWalk() => PlaySoundtrack(0f);

    void PlaySoundtrack(float offset)
    {
        soundtrack.volume *= SettingPanel.GetChannelVolume("SFX");
        soundtrack.time = offset;
        soundtrack.Play();
    }

    public IEnumerator CoverAndReveal(System.Action whenCovered, System.Action beforeReveal = null, bool alreadyCovered = false)
    {
        try
        {
            if (alreadyCovered)
                PlaySoundtrack(.97f + brushDuration);
            else
                yield return CoverScreen();
            whenCovered?.Invoke();
            yield return RevealMap(beforeReveal);
        }
        finally
        {
            Destroy(gameObject);
        }
    }

    IEnumerator CoverScreen()
    {
        PlaySoundtrack(.97f); // Start at the brush stroke, after the stage 2 walk-in.
        for (float t = 0f; t < brushDuration; t += Time.unscaledDeltaTime)
        {
            SetBrush(t / brushDuration);
            yield return null;
        }
        SetBrush(1f);
    }

    void OnDisable()
    {
        if (menuEvents != null) menuEvents.sendNavigationEvents = previousNavigation;
        menuEvents = null;
    }

    public void UpdateWalk(float elapsed, float duration)
    {
        float closing = Mathf.Min(duration, brushDuration);
        SetBrush(Mathf.InverseLerp(duration - closing, duration, elapsed));
        if (showBookNotice)
            bookNotice.alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.4f, .62f, elapsed / duration));
    }

    public IEnumerator RevealMap(System.Action beforeReveal = null)
    {
        bookNotice.alpha = 0f;
        SetBrush(1f);
        for (float t = 0f; t < titleDuration; t += Time.unscaledDeltaTime)
        {
            float enter = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / .45f));
            title.alpha = enter;
            title.transform.localScale = Vector3.one * Mathf.Lerp(1.07f, 1f, enter);
            yield return null;
        }
        beforeReveal?.Invoke();
        for (float t = 0f; t < revealDuration; t += Time.unscaledDeltaTime)
        {
            title.alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / .2f));
            SetBrush(1f - t / revealDuration);
            yield return null;
        }
        title.alpha = 0f;
        SetBrush(0f);
    }

    void SetBrush(float coverage)
    {
        int frame = Mathf.CeilToInt(Mathf.Clamp01(coverage) * 22f) - 1;
        brush.enabled = frame >= 0;
        if (frame < 0) return;
        // Inset half a texel to keep adjacent tiles from bleeding at the screen edges.
        float dx = .5f / brush.texture.width, dy = .5f / brush.texture.height;
        brush.uvRect = new Rect(frame % 4 / 4f + dx, (5 - frame / 4) / 6f + dy,
            .25f - 2f * dx, 1f / 6f - 2f * dy);
    }
}
