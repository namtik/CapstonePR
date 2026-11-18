using System.IO;
using Battle.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// [임시] 보상창 프리팹 골격 생성기 — 한 번 실행 후 삭제해도 됨.
//  1) Assets/Prefab/UI/CardRewardPanel.prefab 생성(배경·카드 슬롯 4개·스킵 버튼)
//  2) Assets/Prefab/ComboRewardPanel.prefab 에 배경 Panel·SkipButton 추가
//  3) 열린 씬의 RoundManager.cardRewardPanelPrefab 에 1)을 연결(씬 저장 필요)
public static class RewardPanelPrefabBuilder
{
    const string CardPrefabPath = "Assets/Prefab/UI/CardRewardPanel.prefab";
    const string ComboPrefabPath = "Assets/Prefab/ComboRewardPanel.prefab";
    const string CardPanelSprite = "Assets/Sprite/UI/Reward/Win_Select_Cardpanal.png";
    const string ComboPanelSprite = "Assets/Sprite/UI/Reward/Win_Select_Comboskillpanal.png";
    const string SkipSprite = "Assets/Sprite/UI/Reward/Win_Select_NochoiceBT.png";

    static readonly Vector2 PanelSize = new Vector2(1920f, 829f);
    static readonly Vector2 SkipSize = new Vector2(422f, 93f);

    [MenuItem("Tools/Battle UI/Build Reward Panel Prefabs")]
    public static void Build()
    {
        var cardPanel = AssetDatabase.LoadAssetAtPath<Sprite>(CardPanelSprite);
        var comboPanel = AssetDatabase.LoadAssetAtPath<Sprite>(ComboPanelSprite);
        var skip = AssetDatabase.LoadAssetAtPath<Sprite>(SkipSprite);
        if (cardPanel == null || comboPanel == null || skip == null)
            Debug.LogWarning("[RewardPanelBuilder] Sprite/UI/Reward 이미지 일부를 찾지 못했습니다(Texture Type=Sprite 확인).");

        CardRewardPanelView cardPrefab = BuildCardRewardPrefab(cardPanel, skip);
        PatchComboPrefab(comboPanel, skip);
        AssignToRoundManager(cardPrefab);
    }

    // 1) 부적 보상창 프리팹
    static CardRewardPanelView BuildCardRewardPrefab(Sprite panelSprite, Sprite skipSprite)
    {
        if (File.Exists(CardPrefabPath) &&
            !EditorUtility.DisplayDialog("CardRewardPanel", CardPrefabPath + " 이(가) 이미 있습니다. 덮어쓸까요?", "덮어쓰기", "건너뛰기"))
            return AssetDatabase.LoadAssetAtPath<CardRewardPanelView>(CardPrefabPath);

        var root = NewUI("CardRewardPanel", null);
        Stretch(root);
        var view = root.gameObject.AddComponent<CardRewardPanelView>();

        var dim = NewUI("Dim", root);
        Stretch(dim);
        var dimImg = dim.gameObject.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.78f);
        dimImg.raycastTarget = true; // 뒤쪽 클릭 차단

        var panel = NewUI("Panel", root);
        Center(panel, PanelSize, Vector2.zero);
        var panelImg = panel.gameObject.AddComponent<Image>();
        panelImg.sprite = panelSprite;
        panelImg.preserveAspect = true;
        panelImg.raycastTarget = false;

        // 카드 슬롯 4개 — 슬롯 크기에 맞춰 카드가 비율 유지로 맞춰짐(카드 300×400 × 1.5)
        var slotsRoot = NewUI("CardSlots", root);
        Center(slotsRoot, new Vector2(1800f, 620f), new Vector2(0f, -10f));
        var slots = new RectTransform[4];
        for (int i = 0; i < slots.Length; i++)
        {
            var s = NewUI("Slot" + i, slotsRoot);
            Center(s, new Vector2(450f, 600f), new Vector2(-720f + 480f * i, 0f));
            slots[i] = s;
        }

        Button skipBtn = NewSkipButton(root, skipSprite, new Vector2(0f, -440f));

        var so = new SerializedObject(view);
        var list = so.FindProperty("cardSlots");
        list.arraySize = slots.Length;
        for (int i = 0; i < slots.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
        so.FindProperty("skipButton").objectReferenceValue = skipBtn;
        so.ApplyModifiedPropertiesWithoutUndo();

        Directory.CreateDirectory(Path.GetDirectoryName(CardPrefabPath));
        var saved = PrefabUtility.SaveAsPrefabAsset(root.gameObject, CardPrefabPath);
        Object.DestroyImmediate(root.gameObject);
        Debug.Log("[RewardPanelBuilder] 생성: " + CardPrefabPath);
        return saved != null ? saved.GetComponent<CardRewardPanelView>() : null;
    }

    // 2) 사방비급 보상창 프리팹에 배경·스킵 버튼 추가
    static void PatchComboPrefab(Sprite panelSprite, Sprite skipSprite)
    {
        if (!File.Exists(ComboPrefabPath)) { Debug.LogWarning("[RewardPanelBuilder] 없음: " + ComboPrefabPath); return; }

        GameObject root = PrefabUtility.LoadPrefabContents(ComboPrefabPath);
        try
        {
            var panelUI = root.GetComponent<ComboBookRewardPanelUI>();
            var rootRt = (RectTransform)root.transform;

            Transform panel = root.transform.Find("Panel");
            if (panel == null)
            {
                var rt = NewUI("Panel", rootRt);
                Center(rt, PanelSize, Vector2.zero);
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = panelSprite;
                img.preserveAspect = true;
                img.raycastTarget = false;
                rt.SetAsFirstSibling(); // 책 목록보다 뒤
            }

            Transform skipTf = root.transform.Find("SkipButton");
            Button skipBtn = skipTf != null ? skipTf.GetComponent<Button>() : NewSkipButton(rootRt, skipSprite, new Vector2(0f, -340f));
            skipBtn.transform.SetAsLastSibling();

            if (panelUI != null)
            {
                var so = new SerializedObject(panelUI);
                so.FindProperty("skipButton").objectReferenceValue = skipBtn;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, ComboPrefabPath);
            Debug.Log("[RewardPanelBuilder] 수정: " + ComboPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // 3) 씬의 RoundManager에 부적 보상창 프리팹 연결
    static void AssignToRoundManager(CardRewardPanelView prefab)
    {
        if (prefab == null) return;
        var rm = Object.FindFirstObjectByType<RoundManager>(FindObjectsInactive.Include);
        if (rm == null)
        {
            Debug.LogWarning("[RewardPanelBuilder] 열린 씬에 RoundManager가 없습니다 — Card Reward Panel Prefab 을 직접 연결하세요.");
            return;
        }
        var so = new SerializedObject(rm);
        so.FindProperty("cardRewardPanelPrefab").objectReferenceValue = prefab;
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(rm.gameObject.scene);
        Debug.Log("[RewardPanelBuilder] RoundManager.cardRewardPanelPrefab 연결 완료 — 씬을 저장하세요.");
    }

    static Button NewSkipButton(RectTransform parent, Sprite sprite, Vector2 pos)
    {
        var rt = NewUI("SkipButton", parent);
        Center(rt, SkipSize, pos);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = true;
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.None;
        rt.gameObject.AddComponent<ButtonHoverScale>(); // 기본 호버 1.06배
        return btn;
    }

    static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        if (parent != null) go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void Center(RectTransform rt, Vector2 size, Vector2 pos)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }
}
