#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Battle.Relic;

// 유물 SO(.asset) 19개 + RelicDatabase를 메뉴 한 번으로 생성·갱신하는 에디터 도구.
// 재실행 안전: 같은 경로의 에셋은 새로 만들지 않고 필드만 갱신한다(아이콘 수동 변경분도 유지하려면 아래 주석 참고).
public static class RelicAssetGenerator
{
    const string RelicFolder = "Assets/Relic/Generated";       // 유물 에셋 저장 폴더
    const string ResourcesFolder = "Assets/Resources";          // RelicDatabase는 Resources에 둬야 자동 로드됨
    const string DbPath = "Assets/Resources/RelicDatabase.asset";

    [MenuItem("Tools/Relic/Generate Relic Assets")]
    public static void Generate()
    {
        EnsureFolder("Assets/Relic");
        EnsureFolder(RelicFolder);
        EnsureFolder(ResourcesFolder);

        var list = new List<RelicSO>();

        // ── 비전투형 ─────────────────────────────────────────────
        list.Add(Make<MaxHpRelic>("10000", "불로단",
            "획득 시, 최대 체력이 10 증가한다.", RelicCategory.NonCombat,
            r => r.amount = 10, "UI_Item_001"));
        list.Add(Make<ShopDiscountRelic>("10001", "할인패",
            "보유 시, 상점에서 상품의 가격이 50% 할인된다.", RelicCategory.NonCombat,
            r => r.priceMultiplier = 0.5f, "UI_Item_002"));
        list.Add(Make<GoldBonusRelic>("10005", "복주머니",
            "보상으로 받는 골드량이 30 만큼 증가한다.", RelicCategory.NonCombat,
            r => r.goldBonus = 30, "UI_Item_005"));
        list.Add(Make<FullHealRelic>("10012", "주작의 깃털",
            "획득 시, 모든 체력을 회복한다.", RelicCategory.NonCombat,
            null, "UI_Item_004"));
        list.Add(Make<RandomizeDeckRelic>("10017", "망각의 붓",
            "획득 시, 현재 보유한 카드를 랜덤한 카드로 모두 변환한다.", RelicCategory.NonCombat,
            null, "UI_Item_003"));
        list.Add(Make<BasicCardDoubleRelic>("10004", "태초의 서",
            "획득 시, 기본 카드의 효과가 2배가 된다.", RelicCategory.NonCombat,
            null, "UI_Item_006"));
        list.Add(Make<GrantAllRelicsRelic>("10018", "유물 호리병",
            "유물 라운드 진입 시, 제시된 모든 유물을 획득한다.", RelicCategory.NonCombat,
            null, "UI_Item_004"));

        // ── 전투형: 전투 시작 ────────────────────────────────────
        list.Add(Make<GuardOnBattleStartRelic>("10002", "호신부",
            "매 전투 시작 시 방어도 5를 얻는다.", RelicCategory.Combat,
            r => r.guardAmount = 5, "UI_Item_003"));
        list.Add(Make<FragmentsOnBattleStartRelic>("10007", "깨진 불상",
            "매 전투 시작 시, 뽑을 카드 더미에 무작위 파편 카드 5장을 섞어 넣는다.", RelicCategory.Combat,
            r => r.fragmentCount = 5, "UI_Item_001"));
        list.Add(Make<FrostOnBattleStartRelic>("10008", "서리화",
            "매 전투 시작 시, 무작위 적 1명에게 빙결을 7 부여한다.", RelicCategory.Combat,
            r => r.frostAmount = 7, "frosted flower"));
        list.Add(Make<AwakenOnBattleStartRelic>("10010", "황룡옥적",
            "매 전투 시작 시, 각성 상태가 된다.", RelicCategory.Combat,
            null, "UI_Item_003"));
        list.Add(Make<ChainOnBattleStartRelic>("10016", "신선도복",
            "매 전투 시작 시, 연쇄 20을 얻는다.", RelicCategory.Combat,
            r => r.chainAmount = 20, "UI_Item_002"));
        list.Add(Make<FirstCardRecastRelic>("10015", "복사경",
            "매 전투 시작 시, 처음 사용하는 카드의 효과를 한번 더 발동한다.", RelicCategory.Combat,
            null, "UI_Item_001"));

        // ── 전투형: 질의/훅 ──────────────────────────────────────
        list.Add(Make<AwakenDurationRelic>("10009", "황룡향로",
            "각성 유지 시간이 2초 증가한다.", RelicCategory.Combat,
            r => r.extraSeconds = 2f, "UI_Item_002"));
        list.Add(Make<ComboAwakenTimeRelic>("10014", "황룡방울",
            "콤보 스킬 발동 시, 각성 시간이 0.5초 만큼 증가한다.", RelicCategory.Combat,
            r => r.bonusSeconds = 0.5f, "UI_Item_006"));
        list.Add(Make<DrawOnHpLostRelic>("10003", "혈묵주",
            "체력을 잃을 시, 카드를 1장 뽑는다.", RelicCategory.Combat,
            r => r.drawCount = 1, "UI_Item_004"));
        list.Add(Make<DamageOnHpLostRelic>("10019", "피 묻은 가시",
            "체력을 잃을 시, 무작위 적에게 피해를 5 만큼 준다.", RelicCategory.Combat,
            r => r.damage = 5, "UI_Item_005"));
        list.Add(Make<DrawEveryNCardsRelic>("10006", "웃는 탈",
            "카드를 3장 사용 시, 카드를 1장 뽑는다.", RelicCategory.Combat,
            r => { r.everyN = 3; r.drawCount = 1; }, "UI_Item_006"));
        list.Add(Make<DDrawDiscardRedrawRelic>("10011", "비급서",
            "D 드로우의 효과가 현재 패를 모두 버리고 뽑을 카드 더미에서 5장을 뽑는다로 바뀐다.", RelicCategory.Combat,
            null, "bookre"));
        list.Add(Make<CostReductionHandLimitRelic>("10013", "독주",
            "모든 카드의 코스트가 1만큼 줄어든다. 전투 중 패에 카드를 1장만 보유할 수 있다.", RelicCategory.Combat,
            r => { r.gaugeCostDelta = -1; r.handLimit = 1; }, "UI_Item_005"));

        WriteDatabase(list);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[RelicGen] 유물 {list.Count}개 + RelicDatabase 생성/갱신 완료 → {DbPath}");
        EditorUtility.DisplayDialog("Relic 생성 완료",
            $"유물 {list.Count}개와 RelicDatabase를 생성/갱신했습니다.\n경로: {RelicFolder}\nDB: {DbPath}", "확인");
    }

    // RelicDatabase 에셋을 만들고 relics 목록을 채운다(private 직렬화 필드라 SerializedObject로 접근)
    static void WriteDatabase(List<RelicSO> relics)
    {
        var db = AssetDatabase.LoadAssetAtPath<RelicDatabase>(DbPath);
        if (db == null)
        {
            db = ScriptableObject.CreateInstance<RelicDatabase>();
            AssetDatabase.CreateAsset(db, DbPath);
        }

        var so = new SerializedObject(db);
        var listProp = so.FindProperty("relics");
        listProp.ClearArray();
        for (int i = 0; i < relics.Count; i++)
        {
            listProp.InsertArrayElementAtIndex(i);
            listProp.GetArrayElementAtIndex(i).objectReferenceValue = relics[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(db);
    }

    // 유물 에셋 1개를 생성 또는 갱신
    static T Make<T>(string id, string displayName, string description,
                     RelicCategory category, System.Action<T> setFields, string iconName) where T : RelicSO
    {
        string path = $"{RelicFolder}/{id}_{typeof(T).Name}.asset";
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
        }

        asset.id = id;
        asset.displayName = displayName;
        asset.description = description;
        asset.category = category;

        if (!string.IsNullOrEmpty(iconName))
        {
            var sprite = LoadSpriteByName(iconName);
            if (sprite != null) asset.icon = sprite;
            else Debug.LogWarning($"[RelicGen] 아이콘 스프라이트를 찾지 못함: '{iconName}' ({id} {displayName}) — 수동 지정 필요");
        }

        setFields?.Invoke(asset);
        EditorUtility.SetDirty(asset);
        return asset;
    }

    // 이름으로 Sprite 에셋을 찾는다(정확 일치 우선, 없으면 첫 후보)
    static Sprite LoadSpriteByName(string spriteName)
    {
        string[] guids = AssetDatabase.FindAssets($"{spriteName} t:Sprite");
        Sprite fallback = null;
        foreach (var guid in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(p);
            if (sp == null) continue;
            if (sp.name == spriteName) return sp;
            if (fallback == null) fallback = sp;
        }
        return fallback;
    }

    // 폴더가 없으면 상위부터 재귀 생성
    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
#endif
