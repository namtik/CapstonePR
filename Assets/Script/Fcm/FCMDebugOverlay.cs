using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// FCM 디버그 오버레이
/// 사용법:
///   1. 빈 오브젝트에 이 컴포넌트를 추가
///   2. Play 모드에서 Tab 키로 표시/숨김 토글
///   3. 게이지 5 도달 시 자동으로 분석 결과 표시
/// </summary>
public class FCMDebugOverlay : MonoBehaviour
{
    [Header("표시 설정")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;
    [SerializeField] private float displayDuration = 4f;
    [SerializeField] private bool alwaysVisible = false;

    [Header("폰트 설정")]
    [SerializeField] private TMP_FontAsset koreanFont;

    // ─── UI 참조 ───
    private Canvas canvas;
    private GameObject panel;
    private bool isVisible = false;

    // 특성 바
    private Image[] featureBars = new Image[4];
    private TMP_Text[] featureLabels = new TMP_Text[4];

    // 소속도
    private Image[] memberBars = new Image[3];
    private TMP_Text[] memberLabels = new TMP_Text[3];

    // 경로
    private Image[] routeBars = new Image[4];
    private TMP_Text[] routeLabels = new TMP_Text[4];

    // 결과
    private TMP_Text resultText;
    private TMP_Text reasonText;
    private Image resultBg;

    // 콤보 슬롯 표시
    private TMP_Text comboSlotText;

    // 애니메이션
    private Coroutine hideCoroutine;

    // ─── 색상 ───
    private static readonly Color[] FeatureColors = {
        new Color(0.89f, 0.29f, 0.29f), // f1 빨강
        new Color(0.85f, 0.35f, 0.19f), // f2 주황
        new Color(0.33f, 0.29f, 0.72f), // f3 보라
        new Color(0.50f, 0.47f, 0.87f), // f4 연보라
    };

    private static readonly Color[] TypeColors = {
        new Color(0.89f, 0.29f, 0.29f), // 러시형 빨강
        new Color(0.85f, 0.35f, 0.19f), // 의존형 주황
        new Color(0.11f, 0.62f, 0.46f), // 탐색형 초록
    };

    private static readonly Color[] ActionColors = {
        new Color(0.22f, 0.54f, 0.87f), // 셔플 파랑
        new Color(0.89f, 0.29f, 0.29f), // 저주 빨강
        new Color(0.50f, 0.47f, 0.87f), // 무속성 보라
    };

    private static readonly string[] FeatureNames = { "f1 긴급도", "f2 집중도", "f3 반복성", "f4 오염도" };
    private static readonly string[] TypeNames = { "콤보 러시형", "경로 의존형", "탐색/분산형" };
    private static readonly string[] SlotNames = { "Q(화)", "W(수)", "E(풍)", "R(지)" };
    private static readonly Color[] SlotColors = {
        new Color(0.89f, 0.29f, 0.29f),
        new Color(0.22f, 0.54f, 0.87f),
        new Color(0.73f, 0.47f, 0.10f),
        new Color(0.39f, 0.60f, 0.13f),
    };

    // ═══════════════════════════════════════════
    // 초기화
    // ═══════════════════════════════════════════

    void Start()
    {
        BuildUI();
        SetVisible(alwaysVisible);
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            if (isVisible)
                SetVisible(false);
            else
                RefreshAndShow();
        }
    }


    // FCM 분석 결과를 표시한다.
    public void ShowAnalysis(float[] fcmFeatures, float f4, float[] membership, int dominant,
                              MonsterDecisionTree.Decision decision,
                              FeatureExtractor.RouteInfo[] routes)
    {
        // 특성 바 갱신 (f1~f3 + f4)
        float[] allFeatures = new float[] { fcmFeatures[0], fcmFeatures[1], fcmFeatures[2], f4 };
        for (int i = 0; i < 4; i++)
        {
            featureBars[i].fillAmount = allFeatures[i];
            featureLabels[i].text = $"{FeatureNames[i]}  {allFeatures[i]:F2}";
        }

        // 소속도 바 갱신
        for (int i = 0; i < 3; i++)
        {
            memberBars[i].fillAmount = membership[i];
            int pct = Mathf.RoundToInt(membership[i] * 100);
            memberLabels[i].text = $"{TypeNames[i]}  {pct}%";
            memberLabels[i].color = (i == dominant) ? TypeColors[i] : new Color(0.6f, 0.6f, 0.6f);
            memberBars[i].color = (i == dominant) ? TypeColors[i] : new Color(0.4f, 0.4f, 0.4f, 0.5f);
        }

        // 경로 바 갱신
        ComboSystem combo = ComboSystem.Instance;
        for (int i = 0; i < 4; i++)
        {
            float prox = (routes != null && i < routes.Length) ? routes[i].BestProximity : 0f;
            routeBars[i].fillAmount = prox;
            routeBars[i].color = prox >= 0.67f ? new Color(0.89f, 0.29f, 0.29f) :
                                  prox >= 0.33f ? new Color(0.73f, 0.47f, 0.10f) :
                                  new Color(0.4f, 0.4f, 0.4f, 0.5f);

            string skillName = "";
            if (routes != null && routes[i].BestSkillIndex >= 0 && combo != null &&
                routes[i].BestSkillIndex < combo.learnedSkills.Count)
            {
                skillName = combo.learnedSkills[routes[i].BestSkillIndex].name;
            }
            routeLabels[i].text = $"{SlotNames[i]}  {Mathf.RoundToInt(prox * 100)}%  {skillName}";
        }

        // 콤보 슬롯 표시
        int[] comboSlot = FeatureExtractor.GetComboSlotAsIndices();
        string slotStr = "";
        for (int i = 0; i < 3; i++)
        {
            slotStr += (comboSlot[i] >= 0) ? SlotNames[comboSlot[i]].Substring(0, 1) : "·";
            if (i < 2) slotStr += " ";
        }
        comboSlotText.text = $"콤보 슬롯: [ {slotStr} ]";

        // 결과 표시
        int actionIdx = (int)decision.ChosenAction;
        string actionName = MonsterDecisionTree.GetActionName(decision.ChosenAction);
        string targetStr = decision.TargetSlot >= 0
            ? $" → {SlotNames[decision.TargetSlot]}"
            : "";

        resultText.text = $"{actionName}{targetStr}";
        resultText.color = ActionColors[actionIdx];
        resultBg.color = new Color(ActionColors[actionIdx].r, ActionColors[actionIdx].g,
                                    ActionColors[actionIdx].b, 0.15f);
        reasonText.text = decision.Reason;

        // 표시
        SetVisible(true);

        if (!alwaysVisible)
        {
            if (hideCoroutine != null) StopCoroutine(hideCoroutine);
            hideCoroutine = StartCoroutine(HideAfterDelay());
        }
    }


    // 현재 게임 상태에서 즉시 분석하고 표시
    public void RefreshAndShow()
    {
        float[] fcmFeatures = FeatureExtractor.ExtractFCMFeatures(out FeatureExtractor.RouteInfo[] routes);
        float f4 = FeatureExtractor.CalcPollution(routes);
        float[] membership = FCMAnalyzer.CalcMembership(fcmFeatures);
        int dominant = FCMAnalyzer.GetDominantType(membership);

        var tree = new MonsterDecisionTree();
        var decision = tree.Decide(membership, fcmFeatures, f4, routes);

        ShowAnalysis(fcmFeatures, f4, membership, dominant, decision, routes);
    }

    IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(displayDuration);
        SetVisible(false);
    }

    void SetVisible(bool visible)
    {
        isVisible = visible;
        if (panel != null) panel.SetActive(visible);
    }

    // UI 빌드 (코드로 생성)
    void BuildUI()
    {
        // 캔버스
        GameObject canvasObj = new GameObject("FCMDebugCanvas");
        canvasObj.transform.SetParent(transform);
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObj.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<GraphicRaycaster>();

        // 메인 패널 (좌측 상단)
        panel = CreatePanel(canvasObj.transform, "FCMPanel",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(20f, -20f), new Vector2(340f, 520f));

        float y = -8f;

        // 제목
        CreateLabel(panel.transform, "Title", "FCM 몬스터 AI 분석", 14, FontStyles.Bold,
            Color.white, new Vector2(10f, y), new Vector2(320f, 24f));
        y -= 28f;

        // ── 콤보 슬롯 ──
        comboSlotText = CreateLabel(panel.transform, "ComboSlot", "콤보 슬롯: [ · · · ]",
            11, FontStyles.Normal, new Color(0.8f, 0.8f, 0.8f),
            new Vector2(10f, y), new Vector2(320f, 18f));
        y -= 24f;

        // ── 특성 벡터 ──
        CreateLabel(panel.transform, "FeatHeader", "특성 벡터 (f1~f3=FCM / f4=트리)", 11, FontStyles.Bold,
            new Color(0.7f, 0.7f, 0.7f), new Vector2(10f, y), new Vector2(320f, 16f));
        y -= 20f;

        for (int i = 0; i < 4; i++)
        {
            featureLabels[i] = CreateLabel(panel.transform, $"FeatLabel{i}",
                $"{FeatureNames[i]}  0.00", 10, FontStyles.Normal,
                FeatureColors[i], new Vector2(10f, y), new Vector2(160f, 16f));

            featureBars[i] = CreateBar(panel.transform, $"FeatBar{i}",
                FeatureColors[i], new Vector2(175f, y + 2f), new Vector2(145f, 12f));
            y -= 19f;
        }
        y -= 6f;

        // ── Top-4 경로 ──
        CreateLabel(panel.transform, "RouteHeader", "Top-4 경로", 11, FontStyles.Bold,
            new Color(0.7f, 0.7f, 0.7f), new Vector2(10f, y), new Vector2(320f, 16f));
        y -= 20f;

        for (int i = 0; i < 4; i++)
        {
            routeLabels[i] = CreateLabel(panel.transform, $"RouteLabel{i}",
                $"{SlotNames[i]}  0%", 10, FontStyles.Normal,
                SlotColors[i], new Vector2(10f, y), new Vector2(200f, 16f));

            routeBars[i] = CreateBar(panel.transform, $"RouteBar{i}",
                SlotColors[i], new Vector2(175f, y + 2f), new Vector2(145f, 12f));
            y -= 19f;
        }
        y -= 6f;

        // ── FCM 소속도 ──
        CreateLabel(panel.transform, "MemHeader", "플레이어 유형 (FCM)", 11, FontStyles.Bold,
            new Color(0.7f, 0.7f, 0.7f), new Vector2(10f, y), new Vector2(320f, 16f));
        y -= 20f;

        for (int i = 0; i < 3; i++)
        {
            memberLabels[i] = CreateLabel(panel.transform, $"MemLabel{i}",
                $"{TypeNames[i]}  0%", 10, FontStyles.Normal,
                TypeColors[i], new Vector2(10f, y), new Vector2(180f, 16f));

            memberBars[i] = CreateBar(panel.transform, $"MemBar{i}",
                TypeColors[i], new Vector2(175f, y + 2f), new Vector2(145f, 12f));
            y -= 19f;
        }
        y -= 10f;

        // ── 결과 ──
        CreateLabel(panel.transform, "ResultHeader", "몬스터 행동", 11, FontStyles.Bold,
            new Color(0.7f, 0.7f, 0.7f), new Vector2(10f, y), new Vector2(320f, 16f));
        y -= 22f;

        // 결과 박스
        GameObject resultBox = CreatePanel(panel.transform, "ResultBox",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(10f, y), new Vector2(310f, 36f));
        resultBg = resultBox.GetComponent<Image>();
        resultBg.color = new Color(0.2f, 0.4f, 0.8f, 0.15f);

        resultText = CreateLabel(resultBox.transform, "ResultText", "-", 14, FontStyles.Bold,
            Color.white, new Vector2(10f, -4f), new Vector2(290f, 28f));
        y -= 40f;

        reasonText = CreateLabel(panel.transform, "ReasonText", "", 9, FontStyles.Italic,
            new Color(0.6f, 0.6f, 0.6f), new Vector2(10f, y), new Vector2(320f, 16f));
        y -= 22f;

        // ── Tab 안내 ──
        CreateLabel(panel.transform, "HintText", $"[{toggleKey}] 토글  |  게이지 5에서 자동 갱신",
            9, FontStyles.Normal, new Color(0.5f, 0.5f, 0.5f),
            new Vector2(10f, y), new Vector2(320f, 16f));

        panel.SetActive(alwaysVisible);
    }

    // ─── UI 헬퍼 ───

    GameObject CreatePanel(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 pos, Vector2 size)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;

        Image bg = obj.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.12f, 0.92f);

        return obj;
    }

    TMP_Text CreateLabel(Transform parent, string name, string text,
        int fontSize, FontStyles style, Color color, Vector2 pos, Vector2 size)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;

        TMP_Text tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.raycastTarget = false;
        tmp.enableAutoSizing = false;
        tmp.overflowMode = TextOverflowModes.Overflow;

        if (koreanFont != null)
            tmp.font = koreanFont;

        return tmp;
    }

    Image CreateBar(Transform parent, string name, Color color, Vector2 pos, Vector2 size)
    {
        // 배경
        GameObject bgObj = new GameObject(name + "_Bg");
        bgObj.transform.SetParent(parent, false);

        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 1f);
        bgRect.anchorMax = new Vector2(0f, 1f);
        bgRect.pivot = new Vector2(0f, 1f);
        bgRect.anchoredPosition = pos;
        bgRect.sizeDelta = size;

        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.25f, 0.8f);

        // 전경 (fillAmount 방식)
        GameObject fillObj = new GameObject(name + "_Fill");
        fillObj.transform.SetParent(bgObj.transform, false);

        RectTransform fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;
        fillRect.anchoredPosition = Vector2.zero;

        Image fillImage = fillObj.AddComponent<Image>();
        fillImage.color = color;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = 0;
        fillImage.fillAmount = 0f;

        return fillImage;
    }
}