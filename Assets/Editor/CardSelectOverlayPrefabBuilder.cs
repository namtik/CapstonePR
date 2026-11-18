using System.IO;
using Battle.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// [임시] 카드 선택 오버레이 프리팹 골격 생성기 — 한 번 실행 후 삭제해도 됨.
//  1) Assets/Prefab/UI/CardSelectOverlay.prefab 생성(딤·띠·"부적을 선택하세요"·카드 줄·빈 칸·확인)
//  2) Assets/Prefab/CardHandHUD.prefab 의 selectOverlayPrefab 에 연결
public static class CardSelectOverlayPrefabBuilder
{
    const string PrefabPath = "Assets/Prefab/UI/CardSelectOverlay.prefab";
    const string HudPrefabPath = "Assets/Prefab/CardHandHUD.prefab";
    const string Dir = "Assets/Sprite/UI/SelectCard/";

    [MenuItem("Tools/Battle UI/Build Card Select Overlay Prefab")]
    public static void Build()
    {
        if (File.Exists(PrefabPath) &&
            !EditorUtility.DisplayDialog("CardSelectOverlay", PrefabPath + " 이(가) 이미 있습니다. 덮어쓸까요?", "덮어쓰기", "취소"))
            return;

        var bandSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Dir + "UI_Battle_Select_Panal.png");
        var titleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Dir + "UI_Battle_Select_Text.png");
        var slotSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Dir + "UI_Battle_Select_Cardback.png");
        var btnSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Dir + "UI_Battle_Select_BT.png");
        if (bandSprite == null || titleSprite == null || slotSprite == null || btnSprite == null)
            Debug.LogWarning("[CardSelectOverlayBuilder] UI/SelectCard 이미지 일부를 찾지 못했습니다(Texture Type=Sprite 확인).");

        var root = NewUI("CardSelectOverlay", null);
        Stretch(root);
        var overlay = root.gameObject.AddComponent<CardSelectOverlay>();

        // 딤(뒤쪽 클릭 차단)
        var dim = NewUI("Dim", root);
        Stretch(dim);
        var dimImg = dim.gameObject.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.45f);
        dimImg.raycastTarget = true;

        // 가운데 띠(스프라이트 자체가 반투명)
        var band = NewUI("Band", root);
        Center(band, new Vector2(1918f, 505f), Vector2.zero);
        var bandImg = band.gameObject.AddComponent<Image>();
        bandImg.sprite = bandSprite;
        bandImg.raycastTarget = false;

        // 상단 "부적을 선택하세요"
        var title = NewUI("Title", root);
        title.anchorMin = title.anchorMax = new Vector2(0.5f, 1f);
        title.pivot = new Vector2(0.5f, 0.5f);
        title.sizeDelta = new Vector2(371f, 42f) * 1.4f;
        title.anchoredPosition = new Vector2(0f, -45f);
        var titleImg = title.gameObject.AddComponent<Image>();
        titleImg.sprite = titleSprite;
        titleImg.preserveAspect = true;
        titleImg.raycastTarget = false;

        // 파편 선택 카드 줄
        var row = NewUI("CardRow", root);
        Center(row, new Vector2(1800f, 505f), Vector2.zero);

        // 빈 칸(카드 표시 크기 375×500)
        var slot = NewUI("Slot0", root);
        Center(slot, new Vector2(375f, 500f), Vector2.zero);
        var slotImg = slot.gameObject.AddComponent<Image>();
        slotImg.sprite = slotSprite;
        slotImg.color = new Color(1f, 1f, 1f, 0.55f);
        slotImg.raycastTarget = true;
        var slotBtn = slot.gameObject.AddComponent<Button>();
        slotBtn.targetGraphic = slotImg;
        slotBtn.transition = Selectable.Transition.None;

        // 확인 버튼
        var confirm = NewUI("ConfirmButton", root);
        Center(confirm, new Vector2(241f, 71f), new Vector2(0f, -300f));
        var confirmImg = confirm.gameObject.AddComponent<Image>();
        confirmImg.sprite = btnSprite;
        confirmImg.raycastTarget = true;
        var confirmBtn = confirm.gameObject.AddComponent<Button>();
        confirmBtn.targetGraphic = confirmImg;
        confirmBtn.transition = Selectable.Transition.ColorTint; // 칸이 비면 비활성 색
        confirm.gameObject.AddComponent<ButtonHoverScale>();

        var so = new SerializedObject(overlay);
        so.FindProperty("dim").objectReferenceValue = dim.gameObject;
        so.FindProperty("band").objectReferenceValue = band.gameObject;
        so.FindProperty("title").objectReferenceValue = title.gameObject;
        so.FindProperty("cardRow").objectReferenceValue = row;
        var slots = so.FindProperty("slots");
        slots.arraySize = 1;
        slots.GetArrayElementAtIndex(0).objectReferenceValue = slotBtn;
        so.FindProperty("confirmButton").objectReferenceValue = confirmBtn;
        so.ApplyModifiedPropertiesWithoutUndo();

        root.gameObject.SetActive(false);
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        var saved = PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
        Object.DestroyImmediate(root.gameObject);
        if (saved == null) return;
        Debug.Log("[CardSelectOverlayBuilder] 생성: " + PrefabPath);

        AssignToHudPrefab(saved.GetComponent<CardSelectOverlay>());
        Selection.activeObject = saved;
        EditorGUIUtility.PingObject(saved);
    }

    // CardHandHUD 프리팹에 연결(씬 인스턴스는 프리팹 값을 따름)
    static void AssignToHudPrefab(CardSelectOverlay overlay)
    {
        if (overlay == null || !File.Exists(HudPrefabPath)) return;
        GameObject root = PrefabUtility.LoadPrefabContents(HudPrefabPath);
        try
        {
            var hud = root.GetComponentInChildren<CardHandHUD>(true);
            if (hud == null) { Debug.LogWarning("[CardSelectOverlayBuilder] CardHandHUD 프리팹에 컴포넌트가 없습니다."); return; }
            var so = new SerializedObject(hud);
            so.FindProperty("selectOverlayPrefab").objectReferenceValue = overlay;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
            Debug.Log("[CardSelectOverlayBuilder] CardHandHUD.selectOverlayPrefab 연결 완료");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
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
