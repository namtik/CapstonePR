using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 유물 획득 확인 팝업 — SettingCanvas > ConfirmPopup(설정창 확인 팝업)을 복제해 쓴다.
// 원본은 평소 꺼져 있는 설정 캔버스 안에 있어 그대로 켜면 보이지 않으므로, 별도 오버레이 캔버스에 사본을 하나 만든다.
// 유물을 얻는 모든 경로(상점·유물 상자·보스 유물)와 상점의 모든 구매(카드·유물·콤보)에서 클릭 시 Show()로 확인을 받는다.
public static class RelicAcquireConfirm
{
    const int SortingOrder = 5100; // 설정창(5000)보다 앞

    static GameObject s_root;
    static TMP_Text s_message;
    static Action s_onYes;
    static Action s_onNo;

    public static bool IsOpen => s_root != null && s_root.activeSelf;

    // 기본 획득 문구
    public static string AcquireMessage(string relicName)
        => $"<b>{SafeName(relicName)}</b>\n이 유물을 획득할까요?";

    // 상점 구매 문구
    public static string PurchaseMessage(string relicName, int price)
        => $"<b>{SafeName(relicName)}</b>\n{price}골드에 구매할까요?";

    static string SafeName(string relicName)
        => string.IsNullOrWhiteSpace(relicName) ? "유물" : relicName.Trim();

    // 확인 팝업을 띄운다. 템플릿을 못 찾으면 확인 없이 바로 onYes를 실행한다.
    public static void Show(string message, Action onYes, Action onNo = null)
    {
        if (IsOpen) return; // 이미 떠 있으면 중복 요청 무시
        if (!Ensure())
        {
            onYes?.Invoke();
            return;
        }

        s_onYes = onYes;
        s_onNo = onNo;
        if (s_message != null) s_message.text = message;
        s_root.SetActive(true);
        s_root.transform.SetAsLastSibling();
    }

    static void Close(bool yes)
    {
        Action action = yes ? s_onYes : s_onNo;
        s_onYes = null;
        s_onNo = null;
        if (s_root != null) s_root.SetActive(false);
        action?.Invoke();
    }

    static bool Ensure()
    {
        if (s_root != null) return true; // 씬이 다시 로드되면 파괴되어 null이 되므로 다시 만든다

        GameObject template = FindTemplate();
        if (template == null)
        {
            Debug.LogWarning("[RelicAcquireConfirm] SettingCanvas > ConfirmPopup을 찾지 못해 확인 없이 바로 획득합니다.");
            return false;
        }

        var canvasGo = new GameObject("RelicConfirmCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        // 원본 캔버스와 같은 해상도 기준으로 맞춘다
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        CanvasScaler src = template.transform.parent != null
            ? template.transform.parent.GetComponentInParent<CanvasScaler>(true)
            : null;
        if (src != null)
        {
            scaler.uiScaleMode = src.uiScaleMode;
            scaler.referenceResolution = src.referenceResolution;
            scaler.screenMatchMode = src.screenMatchMode;
            scaler.matchWidthOrHeight = src.matchWidthOrHeight;
            scaler.scaleFactor = src.scaleFactor;
            scaler.referencePixelsPerUnit = src.referencePixelsPerUnit;
        }

        s_root = UnityEngine.Object.Instantiate(template, canvasGo.transform, false);
        s_root.name = "RelicConfirmPopup";
        s_root.SetActive(false);

        s_message = FindChild<TMP_Text>(s_root.transform, "ConfirmMessage");
        // SettingPanel은 원본 버튼에 코드로 리스너를 붙이므로 사본에는 없다 — 새로 연결
        Button yes = FindChild<Button>(s_root.transform, "ConfirmYes");
        Button no = FindChild<Button>(s_root.transform, "ConfirmNo");
        if (yes != null)
        {
            yes.onClick.RemoveAllListeners();
            yes.onClick.AddListener(() => Close(true));
        }
        if (no != null)
        {
            no.onClick.RemoveAllListeners();
            no.onClick.AddListener(() => Close(false));
        }
        return true;
    }

    static GameObject FindTemplate()
    {
        SettingPanel panel = SettingPanel.Instance;
        if (panel == null) panel = UnityEngine.Object.FindFirstObjectByType<SettingPanel>(FindObjectsInactive.Include);
        if (panel != null && panel.confirmRoot != null) return panel.confirmRoot;

        // 폴백: 이름으로 SettingCanvas > ConfirmPopup 탐색(비활성 포함)
        foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t == null || !t.gameObject.scene.IsValid()) continue;
            if (t.name == "ConfirmPopup" && t.parent != null && t.parent.name == "SettingCanvas")
                return t.gameObject;
        }
        return null;
    }

    static T FindChild<T>(Transform root, string name) where T : Component
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name != name) continue;
            T comp = t.GetComponent<T>();
            if (comp != null) return comp;
        }
        return null;
    }
}
