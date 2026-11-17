using System;
using System.Collections.Generic;
using UnityEngine;

// 적 행동 포커스(카메라 줌인) 동안 손패·덱·묘지·적 HP 같은 HUD를 잠시 페이드아웃해 화면을 비운다.
// - EnemyActionFocusCamera.Focus/Release와 짝으로 Hide/Show를 호출한다.
// - 대상마다 CanvasGroup으로 알파만 조절(SetActive 안 건드림) → 다른 스크립트의 표시/숨김 로직과 충돌하지 않는다.
// - forceVisible이 true를 돌려주는 동안(예: 방해 '버리기' 카드 선택)은 숨기지 않는다.
// - 씬에 없으면 첫 호출 때 자동 생성된다.
public class EnemyActionHudFader : MonoBehaviour
{
    [Serializable]
    public struct Settings
    {
        [Tooltip("적 행동 중 HUD 숨김 사용 여부.")]
        public bool enabled;
        [Tooltip("사라지는 시간(초).")]
        public float fadeOutDuration;
        [Tooltip("다시 나타나는 시간(초).")]
        public float fadeInDuration;
        [Tooltip("손패 숨김.")]
        public bool hideHand;
        [Tooltip("덱(뽑을 더미)·묘지(버린 더미) 숨김.")]
        public bool hidePiles;
        [Tooltip("몬스터 체력 바(행동 게이지 포함) 숨김.")]
        public bool hideEnemyHp;
        [Tooltip("추가로 숨길 오브젝트(선택).")]
        public GameObject[] extraTargets;

        public static Settings Default => new Settings
        {
            enabled = true,
            fadeOutDuration = 0.2f,
            fadeInDuration = 0.3f,
            hideHand = true,
            hidePiles = true,
            hideEnemyHp = true,
            extraTargets = null,
        };
    }

    class Entry
    {
        public CanvasGroup group;
        public float baseAlpha;
        public bool baseBlocks;
    }

    static EnemyActionHudFader _instance;

    readonly List<Entry> _entries = new List<Entry>();
    Settings _s;
    Func<bool> _forceVisible;
    bool _hidden;
    float _alpha = 1f; // 1=보임, 0=숨김

    // targets를 숨기기 시작. 이미 숨기는 중이면 대상만 갱신(연속 행동 시 깜빡임 없음).
    public static void Hide(IEnumerable<GameObject> targets, Settings s, Func<bool> forceVisible = null)
    {
        if (!s.enabled || targets == null) return;
        if (_instance == null)
        {
            var go = new GameObject("[EnemyActionHudFader]");
            _instance = go.AddComponent<EnemyActionHudFader>();
        }
        _instance.Begin(targets, s, forceVisible);
    }

    // 다시 보이기. immediate면 즉시 원복(전투 시작/종료).
    public static void Show(bool immediate = false)
    {
        if (_instance == null) return;
        _instance._hidden = false;
        if (immediate)
        {
            _instance._alpha = 1f;
            _instance.RestoreAll();
        }
    }

    void Begin(IEnumerable<GameObject> targets, Settings s, Func<bool> forceVisible)
    {
        _s = s;
        _forceVisible = forceVisible;
        _hidden = true;
        foreach (var t in targets)
        {
            if (t == null || Contains(t)) continue;
            var cg = t.GetComponent<CanvasGroup>();
            if (cg == null) cg = t.AddComponent<CanvasGroup>();
            var e = new Entry { group = cg };
            e.baseAlpha = e.group.alpha;
            e.baseBlocks = e.group.blocksRaycasts;
            _entries.Add(e);
        }
        Apply();
    }

    bool Contains(GameObject go)
    {
        for (int i = 0; i < _entries.Count; i++)
            if (_entries[i].group != null && _entries[i].group.gameObject == go) return true;
        return false;
    }

    void Update()
    {
        if (_entries.Count == 0) return;
        bool wantHidden = _hidden && !(_forceVisible != null && SafeInvoke(_forceVisible));
        float dur = wantHidden ? _s.fadeOutDuration : _s.fadeInDuration;
        float step = dur > 0.0001f ? Time.unscaledDeltaTime / dur : 1f; // 히트스톱/일시정지와 무관
        _alpha = Mathf.MoveTowards(_alpha, wantHidden ? 0f : 1f, step);

        if (!_hidden && _alpha >= 1f) { RestoreAll(); return; }
        Apply();
    }

    static bool SafeInvoke(Func<bool> f)
    {
        try { return f(); } catch { return false; }
    }

    void Apply()
    {
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            var e = _entries[i];
            if (e.group == null) { _entries.RemoveAt(i); continue; }
            e.group.alpha = e.baseAlpha * _alpha;
            e.group.blocksRaycasts = e.baseBlocks && _alpha > 0.5f;
        }
    }

    // 원래 상태로 되돌리고 대상 목록을 비운다
    void RestoreAll()
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            var e = _entries[i];
            if (e.group == null) continue;
            // 붙인 CanvasGroup도 지우지 않고 원값(alpha 1)으로만 둔다 — 같은 프레임 재숨김 시 지연 Destroy와 엇갈리지 않게
            e.group.alpha = e.baseAlpha;
            e.group.blocksRaycasts = e.baseBlocks;
        }
        _entries.Clear();
    }

    void OnDisable() { _alpha = 1f; RestoreAll(); }
    void OnDestroy() { if (_instance == this) _instance = null; }
}
