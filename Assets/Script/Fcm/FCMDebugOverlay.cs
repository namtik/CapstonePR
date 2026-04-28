using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>FCM 디버그 오버레이 (v5 - RBFN 확률 표시)</summary>
public class FCMDebugOverlay : MonoBehaviour
{
    [Header("표시 설정")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;
    [SerializeField] private float displayDuration = 4f;
    [SerializeField] private bool alwaysVisible = false;
    [Header("폰트")]
    [SerializeField] private TMP_FontAsset koreanFont;

    private Canvas canvas;
    private GameObject panel;
    private bool isVisible = false;

    private Image[] featureBars = new Image[6];
    private TMP_Text[] featureLabels = new TMP_Text[6];
    private Image[] memberBars = new Image[4];
    private TMP_Text[] memberLabels = new TMP_Text[4];
    private TMP_Text[] threatLabels = new TMP_Text[4];
    // RBFN 확률 바
    private Image[] probBars = new Image[3];
    private TMP_Text[] probLabels = new TMP_Text[3];
    private TMP_Text resultText, reasonText, comboSlotText;
    private Image resultBg;
    private Coroutine hideCoroutine;

    static readonly Color[] FC = { new Color(0.89f, 0.29f, 0.29f), new Color(0.33f, 0.29f, 0.72f), new Color(0.20f, 0.60f, 0.40f), new Color(0.95f, 0.60f, 0.07f), new Color(0.40f, 0.70f, 0.90f), new Color(0.50f, 0.47f, 0.87f) };
    static readonly string[] FN = { "f1 긴급도", "f3 반복성", "f5 보충진행", "f7 위협밀도", "f8 슬롯균형", "f4 오염도(트리)" };
    static readonly Color[] TC = { new Color(0.89f, 0.29f, 0.29f), new Color(0.85f, 0.35f, 0.19f), new Color(0.11f, 0.62f, 0.46f), new Color(0.95f, 0.60f, 0.07f) };
    static readonly string[] TN = { "콤보 러시형", "경로 의존형", "탐색/분산형", "버스트형" };
    static readonly string[] SN = { "Q(화)", "W(수)", "E(풍)", "R(지)" };
    static readonly Color[] AC = { new Color(0.22f, 0.54f, 0.87f), new Color(0.89f, 0.29f, 0.29f), new Color(0.50f, 0.47f, 0.87f) };
    static readonly string[] AN = { "셔플", "저주", "무속성" };

    void Start() { BuildUI(); SetVisible(alwaysVisible); }
    void Update() { if (Input.GetKeyDown(toggleKey)) { if (isVisible) SetVisible(false); else RefreshAndShow(); } }

    public void ShowAnalysis(float[] fcm, float f4, float[] mem, int dom,
        MonsterDecisionTree.Decision dec, FeatureExtractor.ThreatInfo[] threats,
        float[] actionProbs = null)
    {
        float[] all = { fcm[0], fcm[1], fcm[2], fcm[3], fcm[4], f4 };
        for (int i = 0; i < 6; i++) { featureBars[i].fillAmount = all[i]; featureLabels[i].text = $"{FN[i]}  {all[i]:F2}"; }

        ComboSystem combo = ComboSystem.Instance;
        for (int i = 0; i < 4; i++)
        {
            if (i < threats.Length && combo != null)
            {
                var t = threats[i];
                string n = t.SkillIndex >= 0 ? combo.learnedSkills[t.SkillIndex].name : "?";
                string ch = t.HasChain && t.ChainSkillIndex >= 0 ? $" -> {combo.learnedSkills[t.ChainSkillIndex].name}" : "";
                threatLabels[i].color = t.MinCards <= 1 ? new Color(0.89f, 0.29f, 0.29f) : t.MinCards <= 2 ? new Color(0.73f, 0.47f, 0.10f) : new Color(0.6f, 0.6f, 0.6f);
                threatLabels[i].text = $"[{t.MinCards}장] {n} ({Mathf.RoundToInt(t.Proximity * 100)}%){ch}";
            }
            else threatLabels[i].text = "";
        }

        for (int i = 0; i < 4; i++)
        {
            memberBars[i].fillAmount = mem[i];
            memberLabels[i].text = $"{TN[i]}  {Mathf.RoundToInt(mem[i] * 100)}%";
            memberLabels[i].color = (i == dom) ? TC[i] : new Color(0.6f, 0.6f, 0.6f);
            memberBars[i].color = (i == dom) ? TC[i] : new Color(0.4f, 0.4f, 0.4f, 0.5f);
        }

        // RBFN 확률 표시
        for (int i = 0; i < 3; i++)
        {
            float p = (actionProbs != null && i < actionProbs.Length) ? actionProbs[i] : 0f;
            probBars[i].fillAmount = p;
            probBars[i].color = AC[i];
            probLabels[i].text = $"{AN[i]}  {Mathf.RoundToInt(p * 100)}%";
            probLabels[i].color = (actionProbs != null) ? AC[i] : new Color(0.4f, 0.4f, 0.4f);
        }

        int[] cs = FeatureExtractor.GetComboSlotAsIndices();
        string ss = ""; for (int i = 0; i < 3; i++) { ss += (cs[i] >= 0) ? SN[cs[i]].Substring(0, 1) : "-"; if (i < 2) ss += " "; }
        comboSlotText.text = $"콤보 슬롯: [ {ss} ]";

        int ai = (int)dec.ChosenAction;
        resultText.text = $"{MonsterDecisionTree.GetActionName(dec.ChosenAction)}" + (dec.TargetSlot >= 0 ? $" -> {SN[dec.TargetSlot]}" : "");
        resultText.color = AC[ai]; resultBg.color = new Color(AC[ai].r, AC[ai].g, AC[ai].b, 0.15f);
        reasonText.text = dec.Reason;

        SetVisible(true);
        if (!alwaysVisible) { if (hideCoroutine != null) StopCoroutine(hideCoroutine); hideCoroutine = StartCoroutine(Hide()); }
    }

    public void RefreshAndShow()
    {
        float[] fcm = FeatureExtractor.ExtractFCMFeatures(out FeatureExtractor.ThreatInfo[] threats);
        float f4 = FeatureExtractor.CalcPollution(threats);
        float[] mem = FCMAnalyzer.CalcMembership(fcm); int dom = FCMAnalyzer.GetDominantType(mem);
        var tree = new MonsterDecisionTree();
        var dec = tree.Decide(mem, fcm, f4, threats);
        ShowAnalysis(fcm, f4, mem, dom, dec, threats);
    }

    IEnumerator Hide() { yield return new WaitForSeconds(displayDuration); SetVisible(false); }
    void SetVisible(bool v) { isVisible = v; if (panel != null) panel.SetActive(v); }

    void BuildUI()
    {
        var co = new GameObject("FCMDebugCanvas"); co.transform.SetParent(transform);
        canvas = co.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
        co.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        co.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
        co.AddComponent<GraphicRaycaster>();

        panel = MP(co.transform, "P", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -20), new Vector2(340, 620));
        float y = -8;
        ML(panel.transform, "T", "FCM+RBFN 몬스터 AI", 14, FontStyles.Bold, Color.white, new Vector2(10, y), new Vector2(320, 24)); y -= 28;
        comboSlotText = ML(panel.transform, "CS", "콤보 슬롯: [ - - - ]", 11, FontStyles.Normal, new Color(0.8f, 0.8f, 0.8f), new Vector2(10, y), new Vector2(320, 18)); y -= 24;

        ML(panel.transform, "FH", "특성 벡터 (5D FCM + f4 트리)", 10, FontStyles.Bold, new Color(0.7f, 0.7f, 0.7f), new Vector2(10, y), new Vector2(320, 16)); y -= 18;
        for (int i = 0; i < 6; i++)
        {
            featureLabels[i] = ML(panel.transform, $"FL{i}", $"{FN[i]}  0.00", 9, FontStyles.Normal, FC[i], new Vector2(10, y), new Vector2(160, 14));
            featureBars[i] = MkBar(panel.transform, $"FB{i}", FC[i], new Vector2(175, y + 1), new Vector2(145, 10)); y -= 16;
        }
        y -= 4;

        ML(panel.transform, "TH", "위협 콤보", 10, FontStyles.Bold, new Color(0.7f, 0.7f, 0.7f), new Vector2(10, y), new Vector2(320, 16)); y -= 18;
        for (int i = 0; i < 4; i++) { threatLabels[i] = ML(panel.transform, $"TL{i}", "", 9, FontStyles.Normal, new Color(0.8f, 0.8f, 0.8f), new Vector2(10, y), new Vector2(320, 14)); y -= 16; }
        y -= 4;

        ML(panel.transform, "MH", "FCM 유형", 10, FontStyles.Bold, new Color(0.7f, 0.7f, 0.7f), new Vector2(10, y), new Vector2(320, 16)); y -= 18;
        for (int i = 0; i < 4; i++)
        {
            memberLabels[i] = ML(panel.transform, $"ML{i}", $"{TN[i]}  0%", 9, FontStyles.Normal, TC[i], new Vector2(10, y), new Vector2(170, 14));
            memberBars[i] = MkBar(panel.transform, $"MB{i}", TC[i], new Vector2(175, y + 1), new Vector2(145, 10)); y -= 16;
        }
        y -= 4;

        // RBFN 확률
        ML(panel.transform, "PH", "RBFN 행동 확률", 10, FontStyles.Bold, new Color(0.7f, 0.7f, 0.7f), new Vector2(10, y), new Vector2(320, 16)); y -= 18;
        for (int i = 0; i < 3; i++)
        {
            probLabels[i] = ML(panel.transform, $"PL{i}", $"{AN[i]}  0%", 9, FontStyles.Normal, AC[i], new Vector2(10, y), new Vector2(120, 14));
            probBars[i] = MkBar(panel.transform, $"PB{i}", AC[i], new Vector2(135, y + 1), new Vector2(185, 10)); y -= 16;
        }
        y -= 8;

        ML(panel.transform, "RH", "몬스터 행동", 10, FontStyles.Bold, new Color(0.7f, 0.7f, 0.7f), new Vector2(10, y), new Vector2(320, 16)); y -= 20;
        var rb = MP(panel.transform, "RB", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, y), new Vector2(310, 32));
        resultBg = rb.GetComponent<Image>(); resultBg.color = new Color(0.2f, 0.4f, 0.8f, 0.15f);
        resultText = ML(rb.transform, "RT", "-", 13, FontStyles.Bold, Color.white, new Vector2(8, -3), new Vector2(290, 26)); y -= 36;
        reasonText = ML(panel.transform, "RR", "", 8, FontStyles.Italic, new Color(0.6f, 0.6f, 0.6f), new Vector2(10, y), new Vector2(320, 14)); y -= 18;
        ML(panel.transform, "HT", $"[{toggleKey}] 토글", 8, FontStyles.Normal, new Color(0.5f, 0.5f, 0.5f), new Vector2(10, y), new Vector2(320, 14));
        panel.SetActive(alwaysVisible);
    }

    GameObject MP(Transform p, string n, Vector2 amin, Vector2 amax, Vector2 piv, Vector2 pos, Vector2 sz) { var o = new GameObject(n); o.transform.SetParent(p, false); var r = o.AddComponent<RectTransform>(); r.anchorMin = amin; r.anchorMax = amax; r.pivot = piv; r.anchoredPosition = pos; r.sizeDelta = sz; o.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.12f, 0.92f); return o; }
    TMP_Text ML(Transform p, string n, string txt, int sz, FontStyles st, Color c, Vector2 pos, Vector2 size) { var o = new GameObject(n); o.transform.SetParent(p, false); var r = o.AddComponent<RectTransform>(); r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1); r.anchoredPosition = pos; r.sizeDelta = size; var t = o.AddComponent<TextMeshProUGUI>(); t.text = txt; t.fontSize = sz; t.fontStyle = st; t.color = c; t.raycastTarget = false; t.enableAutoSizing = false; t.overflowMode = TextOverflowModes.Overflow; if (koreanFont != null) t.font = koreanFont; return t; }
    Image MkBar(Transform p, string n, Color c, Vector2 pos, Vector2 sz) { var bo = new GameObject(n + "_B"); bo.transform.SetParent(p, false); var br = bo.AddComponent<RectTransform>(); br.anchorMin = new Vector2(0, 1); br.anchorMax = new Vector2(0, 1); br.pivot = new Vector2(0, 1); br.anchoredPosition = pos; br.sizeDelta = sz; bo.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f, 0.8f); var fo = new GameObject(n + "_F"); fo.transform.SetParent(bo.transform, false); var fr = fo.AddComponent<RectTransform>(); fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.sizeDelta = Vector2.zero; fr.anchoredPosition = Vector2.zero; var fi = fo.AddComponent<Image>(); fi.color = c; fi.type = Image.Type.Filled; fi.fillMethod = Image.FillMethod.Horizontal; fi.fillOrigin = 0; fi.fillAmount = 0; return fi; }
}
