using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using Battle.AI;

/// <summary>
/// FCM-RBFN 적 AI 실시간 디버그 오버레이 (v6).
/// 신 방해행동 AI(EnemyDisruptionAI/OnlineQLearner)의 결정·보상을 화면에 표시한다.
/// 데이터는 AiDebug 허브에서 폴링 — 새 결정/보상이 게시될 때만 다시 그린다.
///   · 특성 x[6] (플레이 빈도)  · FCM 유형 소속도[5]  · 상황 컨텍스트
///   · Q값/방해행동[6] (선택/제외 표시)  · 결정 결과  · 보상 r + drift
/// 글리프 안전: 특수기호(▶✕μ—) 미사용 → 한글+ASCII만 사용(폰트 아틀라스 누락 방지).
/// </summary>
public class FCMDebugOverlay : MonoBehaviour
{
    [Header("표시 설정")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;
    [SerializeField] private float displayDuration = 6f;
    [SerializeField] private bool alwaysVisible = true;
    [Header("폰트")]
    [SerializeField] private TMP_FontAsset koreanFont;
    [Tooltip("전체 글자/패널 배율 — 화면에서 작게 보이면 인스펙터에서 키우세요(예: 1.6, 2.0). 게임뷰 창이 작을수록 크게.")]
    [SerializeField] private float uiScale = 1.4f;

    private Canvas canvas;
    private GameObject panel;
    private bool isVisible = false;
    private TMP_FontAsset _font;   // 실제 사용 폰트(글리프 충분한 것으로 자동 해석)

    private Image[] featureBars = new Image[6];
    private TMP_Text[] featureLabels = new TMP_Text[6];
    private Image[] memberBars = new Image[5];
    private TMP_Text[] memberLabels = new TMP_Text[5];
    private Image[] qBars = new Image[6];
    private TMP_Text[] qLabels = new TMP_Text[6];
    private TMP_Text contextLabel1, contextLabel2, resultText, rewardText;
    private Image resultBg;
    private Coroutine hideCoroutine;

    private int _seenDec = -1, _seenRew = -1;

    static readonly Color GREY = new Color(0.6f, 0.6f, 0.6f);
    static readonly Color DIM = new Color(0.4f, 0.4f, 0.45f, 0.7f);
    // 특성 x[6] = [f1,f2,f3,f4,f5,f7]
    static readonly Color[] FC = { new Color(0.92f, 0.40f, 0.40f), new Color(0.82f, 0.56f, 0.30f), new Color(0.30f, 0.70f, 0.50f), new Color(0.38f, 0.62f, 0.92f), new Color(0.95f, 0.80f, 0.30f), new Color(0.68f, 0.54f, 0.88f) };
    static readonly string[] FN = { "f1 화상사용", "f2 파편사용", "f3 연쇄사용", "f4 방어사용", "f5 저코스트", "f7 각성활용" };
    // FCM 유형 소속도[5]
    static readonly Color[] TC = { new Color(0.92f, 0.40f, 0.40f), new Color(0.82f, 0.56f, 0.30f), new Color(0.20f, 0.70f, 0.62f), new Color(0.95f, 0.80f, 0.30f), new Color(0.40f, 0.62f, 0.92f) };
    static readonly string[] TN = { "화상형", "파편형", "연속타격형", "피버형", "안정형" };
    // 방해행동 6종
    static readonly Color[] AC = { new Color(0.70f, 0.48f, 0.90f), new Color(0.58f, 0.62f, 0.72f), new Color(0.30f, 0.78f, 0.86f), new Color(0.92f, 0.40f, 0.36f), new Color(0.36f, 0.78f, 0.50f), new Color(0.95f, 0.66f, 0.26f) };
    static readonly string[] AN = { "저주", "탈진", "흡수", "강화", "회복", "버리기" };

    void Start()
    {
        BuildUI();
        _seenDec = AiDebug.DecisionVersion;
        _seenRew = AiDebug.RewardVersion;
        SetVisible(alwaysVisible);
        if (alwaysVisible) Refresh();
    }

    void Update()
    {
        // 수동 토글
        if (Input.GetKeyDown(toggleKey))
        {
            if (isVisible) SetVisible(false);
            else { SetVisible(true); Refresh(); if (!alwaysVisible) RestartHide(); }
        }

        // 새 결정/보상 감지 → 다시 그림 (+ 비상시 팝업/자동숨김)
        bool changed = AiDebug.DecisionVersion != _seenDec || AiDebug.RewardVersion != _seenRew;
        if (changed)
        {
            _seenDec = AiDebug.DecisionVersion;
            _seenRew = AiDebug.RewardVersion;
            if (!alwaysVisible && !isVisible) SetVisible(true);
            if (isVisible || alwaysVisible) Refresh();
            if (!alwaysVisible && isVisible) RestartHide();
        }
    }

    void RestartHide()
    {
        if (hideCoroutine != null) StopCoroutine(hideCoroutine);
        hideCoroutine = StartCoroutine(HideAfter());
    }
    IEnumerator HideAfter() { yield return new WaitForSeconds(displayDuration); SetVisible(false); }
    void SetVisible(bool v) { isVisible = v; if (panel != null) panel.SetActive(v); }

    /// <summary>AiDebug 허브의 최신 결정/보상으로 패널을 갱신.</summary>
    void Refresh()
    {
        if (!AiDebug.HasDecision)
        {
            for (int i = 0; i < 6; i++) { featureBars[i].fillAmount = 0; featureLabels[i].text = $"{FN[i]}  -"; }
            for (int i = 0; i < 5; i++) { memberBars[i].fillAmount = 0; memberLabels[i].text = $"  {TN[i]}  -"; }
            for (int i = 0; i < 6; i++) { qBars[i].fillAmount = 0; qLabels[i].text = $"  {AN[i]}  -"; }
            contextLabel1.text = "상황: 대기";
            contextLabel2.text = "";
            resultText.text = "결정 대기...";
            resultText.color = Color.white;
            resultBg.color = new Color(0.2f, 0.2f, 0.25f, 0.25f);
            rewardText.text = "보상 대기...";
            rewardText.color = GREY;
            return;
        }

        var d = AiDebug.LastDecision;

        // ── 특성 x[6] ──
        for (int i = 0; i < 6; i++)
        {
            float v = (d.features != null && i < d.features.Length) ? d.features[i] : 0f;
            featureBars[i].fillAmount = Mathf.Clamp01(v);
            featureBars[i].color = FC[i];
            featureLabels[i].text = $"{FN[i]}  {v:F2}";
        }

        // ── FCM 유형 소속도[5] (dominant 강조) ──
        int dom = ArgMax(d.membership);
        for (int i = 0; i < 5; i++)
        {
            float v = (d.membership != null && i < d.membership.Length) ? d.membership[i] : 0f;
            memberBars[i].fillAmount = Mathf.Clamp01(v);
            bool top = (i == dom);
            memberBars[i].color = top ? TC[i] : new Color(TC[i].r, TC[i].g, TC[i].b, 0.35f);
            memberLabels[i].text = $"{(top ? "> " : "  ")}{TN[i]}  {Mathf.RoundToInt(v * 100)}%";
            memberLabels[i].color = top ? TC[i] : GREY;
        }

        // ── 상황 컨텍스트 c[8] ──
        if (d.context != null && d.context.Length >= 7)
        {
            contextLabel1.text = $"적HP {d.context[0]:P0}    플HP {d.context[1]:P0}";
            contextLabel2.text = $"손패 {d.handCount}  화{d.context[2]:P0} 파{d.context[3]:P0} 연{d.context[4]:P0} 방{d.context[5]:P0} 무{d.context[6]:P0}";
        }

        // ── Q값 / 방해행동 [6] (선택 '>' · 제외 '(X)' ) ──
        float qmin = float.PositiveInfinity, qmax = float.NegativeInfinity;
        if (d.q != null)
            for (int a = 0; a < d.q.Length; a++) { if (d.q[a] < qmin) qmin = d.q[a]; if (d.q[a] > qmax) qmax = d.q[a]; }
        float span = Mathf.Max(1e-4f, qmax - qmin);
        for (int a = 0; a < 6; a++)
        {
            float q = (d.q != null && a < d.q.Length) ? d.q[a] : 0f;
            bool avail = (d.mask == null) || (a < d.mask.Length && d.mask[a]);
            bool chosen = (a == d.action);
            qBars[a].fillAmount = (d.q != null) ? Mathf.Clamp01((q - qmin) / span) : 0f;
            qBars[a].color = avail ? (chosen ? AC[a] : new Color(AC[a].r, AC[a].g, AC[a].b, 0.55f)) : DIM;
            qLabels[a].text = $"{(chosen ? "> " : "  ")}{AN[a]} {q:+0.00;-0.00}{(avail ? "" : " (X)")}";
            qLabels[a].color = avail ? (chosen ? Color.white : new Color(0.8f, 0.8f, 0.82f)) : GREY;
        }

        // ── 결정 결과 ──
        int ai = Mathf.Clamp(d.action, 0, 5);
        resultText.text = $"선택: {AN[ai]}{(d.explored ? "   (탐험)" : "")}";
        resultText.color = AC[ai];
        resultBg.color = new Color(AC[ai].r, AC[ai].g, AC[ai].b, 0.18f);

        // ── 보상 (즉시/발현) + drift ──
        if (AiDebug.RewardVersion > 0 && AiDebug.RewardAction >= 0)
        {
            int ra = Mathf.Clamp(AiDebug.RewardAction, 0, 5);
            string kind = AiDebug.RewardImmediate ? "즉시" : "발현";
            rewardText.text = $"보상: {AN[ra]}  r={AiDebug.Reward:+0.00;-0.00} ({kind})  d={AiDebug.Drift:F3}";
            rewardText.color = AiDebug.Reward >= 0f ? new Color(0.4f, 0.85f, 0.5f) : new Color(0.94f, 0.45f, 0.4f);
        }
        else
        {
            rewardText.text = "보상 대기...";
            rewardText.color = GREY;
        }
    }

    static int ArgMax(float[] v)
    {
        if (v == null || v.Length == 0) return 0;
        int idx = 0; float best = v[0];
        for (int i = 1; i < v.Length; i++) if (v[i] > best) { best = v[i]; idx = i; }
        return idx;
    }

    /// <summary>
    /// 표시용 폰트 해석. 할당된 koreanFont의 글리프가 충분하면 그대로 쓰고,
    /// 부족하면(예: NanumGothic SDF = 94자 제한 → 한글 대부분 □ 깨짐) Resources의 전체 한글 폰트로 교체.
    /// </summary>
    TMP_FontAsset ResolveFont()
    {
        if (koreanFont != null && koreanFont.characterTable != null && koreanFont.characterTable.Count >= 2000)
            return koreanFont;
        // 전체 한글 폰트(Assets/Resources/) — 약 1.2만 자 수록.
        var full = Resources.Load<TMP_FontAsset>("SpoqaHanSansNeo-Bold SDF")
                ?? Resources.Load<TMP_FontAsset>("NanumGothic SDF");
        return full != null ? full : koreanFont;
    }

    void BuildUI()
    {
        float s = uiScale < 0.5f ? 1.4f : uiScale; // 새 필드가 0으로 역직렬화돼도 기본 1.4로 폴백
        _font = ResolveFont(); // 글리프 충분한 한글 폰트로 해석(NanumGothic 94자 → 깨짐 방지)
        int F(float px) => Mathf.RoundToInt(px * s);          // 폰트 크기 배율
        Vector2 V(float x, float y) => new Vector2(x * s, y * s); // 위치/크기 배율

        var co = new GameObject("FCMDebugCanvas"); co.transform.SetParent(transform);
        canvas = co.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
        co.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        co.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
        co.AddComponent<GraphicRaycaster>();

        // 레이아웃 상수(기준 px, uiScale 배율 적용). 라벨 폭을 넉넉히 → 바와 겹침/잘림 방지.
        const float W = 360, LBLW = 168, BARX = 178, BARW = 172, BARH = 11, ROW = 18;
        panel = MP(co.transform, "P", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), V(16, -16), V(W, 540));
        float y = -8;
        ML(panel.transform, "T", "FCM-RBFN 적 AI 실시간", F(15), FontStyles.Bold, Color.white, V(10, y), V(W - 20, 24), s); y -= 24;

        ML(panel.transform, "FH", "특성 x[6] (플레이 빈도)", F(11), FontStyles.Bold, GREY, V(10, y), V(W - 20, 18), s); y -= 19;
        for (int i = 0; i < 6; i++)
        {
            featureLabels[i] = ML(panel.transform, $"FL{i}", $"{FN[i]}  -", F(11), FontStyles.Normal, FC[i], V(10, y), V(LBLW, ROW), s);
            featureBars[i] = MkBar(panel.transform, $"FB{i}", FC[i], V(BARX, y + 1), V(BARW, BARH), s); y -= ROW;
        }
        y -= 4;

        ML(panel.transform, "MH", "FCM 유형 소속도 [5]", F(11), FontStyles.Bold, GREY, V(10, y), V(W - 20, 18), s); y -= 19;
        for (int i = 0; i < 5; i++)
        {
            memberLabels[i] = ML(panel.transform, $"ML{i}", $"  {TN[i]}  -", F(11), FontStyles.Normal, TC[i], V(10, y), V(LBLW, ROW), s);
            memberBars[i] = MkBar(panel.transform, $"MB{i}", TC[i], V(BARX, y + 1), V(BARW, BARH), s); y -= ROW;
        }
        y -= 4;

        ML(panel.transform, "CH", "상황 (컨텍스트)", F(11), FontStyles.Bold, GREY, V(10, y), V(W - 20, 18), s); y -= 19;
        contextLabel1 = ML(panel.transform, "C1", "상황: 대기", F(11), FontStyles.Normal, new Color(0.88f, 0.88f, 0.88f), V(10, y), V(W - 20, ROW), s); y -= ROW;
        contextLabel2 = ML(panel.transform, "C2", "", F(11), FontStyles.Normal, new Color(0.88f, 0.88f, 0.88f), V(10, y), V(W - 20, ROW), s); y -= ROW;
        y -= 3;

        ML(panel.transform, "QH", "Q값 / 방해행동 [6]   (> 선택, X 제외)", F(11), FontStyles.Bold, GREY, V(10, y), V(W - 20, 18), s); y -= 19;
        for (int a = 0; a < 6; a++)
        {
            qLabels[a] = ML(panel.transform, $"QL{a}", $"  {AN[a]}  -", F(11), FontStyles.Normal, new Color(0.8f, 0.8f, 0.82f), V(10, y), V(LBLW, ROW), s);
            qBars[a] = MkBar(panel.transform, $"QB{a}", AC[a], V(BARX, y + 1), V(BARW, BARH), s); y -= ROW;
        }
        y -= 6;

        var rb = MP(panel.transform, "RB", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), V(10, y), V(W - 20, 30));
        resultBg = rb.GetComponent<Image>(); resultBg.color = new Color(0.2f, 0.2f, 0.25f, 0.25f);
        resultText = ML(rb.transform, "RT", "결정 대기...", F(15), FontStyles.Bold, Color.white, V(8, -3), V(W - 36, 26), s); y -= 34;

        rewardText = ML(panel.transform, "RW", "보상 대기...", F(12), FontStyles.Bold, GREY, V(10, y), V(W - 20, 20), s); y -= 21;
        ML(panel.transform, "HT", $"[{toggleKey}] 토글", F(9), FontStyles.Normal, new Color(0.5f, 0.5f, 0.5f), V(10, y), V(W - 20, 16), s);

        // 패널 높이를 실제 내용에 맞춤(잘림 방지)
        var pr = panel.GetComponent<RectTransform>();
        pr.sizeDelta = new Vector2(W * s, (-y + 14) * s);
        panel.SetActive(alwaysVisible);
    }

    GameObject MP(Transform p, string n, Vector2 amin, Vector2 amax, Vector2 piv, Vector2 pos, Vector2 sz) { var o = new GameObject(n); o.transform.SetParent(p, false); var r = o.AddComponent<RectTransform>(); r.anchorMin = amin; r.anchorMax = amax; r.pivot = piv; r.anchoredPosition = pos; r.sizeDelta = sz; o.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.12f, 0.92f); return o; }
    TMP_Text ML(Transform p, string n, string txt, int sz, FontStyles st, Color c, Vector2 pos, Vector2 size, float scale) { var o = new GameObject(n); o.transform.SetParent(p, false); var r = o.AddComponent<RectTransform>(); r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1); r.anchoredPosition = pos; r.sizeDelta = size; var t = o.AddComponent<TextMeshProUGUI>(); t.text = txt; t.fontSize = sz; t.fontStyle = st; t.color = c; t.raycastTarget = false; t.enableAutoSizing = false; t.overflowMode = TextOverflowModes.Overflow; t.textWrappingMode = TextWrappingModes.NoWrap; if (_font != null) t.font = _font; return t; }
    Image MkBar(Transform p, string n, Color c, Vector2 pos, Vector2 sz, float scale) { var bo = new GameObject(n + "_B"); bo.transform.SetParent(p, false); var br = bo.AddComponent<RectTransform>(); br.anchorMin = new Vector2(0, 1); br.anchorMax = new Vector2(0, 1); br.pivot = new Vector2(0, 1); br.anchoredPosition = pos; br.sizeDelta = sz; bo.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f, 0.8f); var fo = new GameObject(n + "_F"); fo.transform.SetParent(bo.transform, false); var fr = fo.AddComponent<RectTransform>(); fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.sizeDelta = Vector2.zero; fr.anchoredPosition = Vector2.zero; var fi = fo.AddComponent<Image>(); fi.color = c; fi.type = Image.Type.Filled; fi.fillMethod = Image.FillMethod.Horizontal; fi.fillOrigin = 0; fi.fillAmount = 0; return fi; }

    // ── 구 FCM(콤보슬롯) 시스템 호환 스텁 ──
    // MonsterMidPattern.Execute()가 호출하지만, SampleScene(신 카드 시스템)에선 도달하지 않는 죽은 경로.
    // 신 AI 오버레이로 대체되어 동작하지 않음(컴파일 호환용).
    public void ShowAnalysis(float[] fcm, float f4, float[] mem, int dom,
        MonsterDecisionTree.Decision dec, FeatureExtractor.ThreatInfo[] threats,
        float[] actionProbs = null)
    { }
}
