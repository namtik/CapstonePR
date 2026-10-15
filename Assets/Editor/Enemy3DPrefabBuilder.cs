using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 3D 몬스터 전투 프리팹 자동 생성기.
// 무사(EnemySamu) 프리팹을 템플릿으로 컴포넌트 값(EnemyController/EnemyView3D/EnemyStat/MonsterMidPattern)을 복사하고,
// FBX 모델 + 머티리얼 + HudAnchor를 붙여 프리팹으로 저장한 뒤 EnemyData.battlePrefab3D에 연결한다.
// Animator Controller와 EnemyView3D의 동작 매핑(Motion States)은 만들지 않는다 — 직접 구성.
// 스펙은 Enemy3DBuildSpecs 에셋에서 관리(Tools/3D Enemy/Prefab Builder 창 또는 인스펙터).
// 기본은 이미 존재하는 프리팹을 덮어쓰지 않으며, 재생성은 창의 '재생성' 버튼으로만 한다.
public static class Enemy3DPrefabBuilder
{
    const string SpecsAssetPath = "Assets/Editor/Enemy3DBuildSpecs.asset";  // 스펙 에셋 기본 위치
    const string DefaultTemplatePath = "Assets/Prefab/3D/EnemySamu.prefab"; // 템플릿(무사)

    // 스펙 에셋을 찾고, 없으면 기존 하드코딩 스펙으로 새로 만든다.
    public static Enemy3DBuildSpecs LoadOrCreateSpecs()
    {
        var guids = AssetDatabase.FindAssets("t:Enemy3DBuildSpecs");
        if (guids.Length > 0)
            return AssetDatabase.LoadAssetAtPath<Enemy3DBuildSpecs>(AssetDatabase.GUIDToAssetPath(guids[0]));

        var specs = ScriptableObject.CreateInstance<Enemy3DBuildSpecs>();
        specs.template = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultTemplatePath);
        specs.entries.Add(DefaultEntry("EnemyClayOni", "토우귀",
            "Assets/3D_Character/Land/토우귀/Character/ClayOni.fbx",
            "Assets/3D_Character/Land/토우귀/Character/ClayOni_Base.mat",
            "Assets/Script/Enemy/LandPuppetEnemyData.asset"));
        specs.entries.Add(DefaultEntry("EnemyDrownedOni", "수장귀",
            "Assets/3D_Character/Water/수장귀/Character/DrownedOni.fbx",
            "Assets/3D_Character/Water/수장귀/Character/DrownedOni_Base.mat",
            "Assets/Script/Enemy/WaterGhostEnemyData.asset"));
        AssetDatabase.CreateAsset(specs, SpecsAssetPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Enemy3DBuilder] 스펙 에셋 생성: {SpecsAssetPath}");
        return specs;
    }

    static Enemy3DBuildSpecs.Entry DefaultEntry(string prefabName, string modelName, string fbxPath, string matPath, string dataPath)
    {
        return new Enemy3DBuildSpecs.Entry
        {
            prefabName = prefabName, modelName = modelName,
            fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath),
            material = AssetDatabase.LoadAssetAtPath<Material>(matPath),
            enemyData = AssetDatabase.LoadAssetAtPath<EnemyData>(dataPath),
        };
    }

    [MenuItem("Tools/3D Enemy/Build Missing 3D Enemy Prefabs")]
    public static void BuildAll() => BuildAll(LoadOrCreateSpecs());

    public static void BuildAll(Enemy3DBuildSpecs specs)
    {
        if (!CheckTemplate(specs)) return;

        var tmp = (GameObject)PrefabUtility.InstantiatePrefab(specs.template);
        LogBounds(specs.template.name, new List<Renderer>(tmp.GetComponentsInChildren<Renderer>(true)));
        Object.DestroyImmediate(tmp);

        foreach (var e in specs.entries)
            if (e.enabled) Build(specs, e, false);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // batchmode 진입점: -executeMethod Enemy3DPrefabBuilder.BuildAllBatch
    public static void BuildAllBatch()
    {
        BuildAll();
        EditorApplication.Exit(0);
    }

    // 스펙 하나 생성. overwrite면 기존 프리팹을 같은 경로(GUID 유지)에 다시 저장한다.
    public static void BuildOne(Enemy3DBuildSpecs specs, Enemy3DBuildSpecs.Entry e, bool overwrite)
    {
        if (!CheckTemplate(specs)) return;
        Build(specs, e, overwrite);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // 스펙 오류 메시지(없으면 null)
    public static string Validate(Enemy3DBuildSpecs specs, Enemy3DBuildSpecs.Entry e)
    {
        if (string.IsNullOrWhiteSpace(e.prefabName)) return "프리팹 이름이 비어 있음";
        if (e.prefabName.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0) return "프리팹 이름에 파일명 불가 문자 포함";
        if (e.fbx == null) return "FBX 미지정";
        foreach (var other in specs.entries)
            if (other != e && other.prefabName == e.prefabName) return "프리팹 이름 중복";
        return null;
    }

    static bool CheckTemplate(Enemy3DBuildSpecs specs)
    {
        if (specs == null) { Debug.LogError("[Enemy3DBuilder] 스펙 에셋 없음"); return false; }
        if (specs.template == null) { Debug.LogError("[Enemy3DBuilder] 템플릿 프리팹 미지정"); return false; }
        if (specs.template.GetComponent<EnemyView3D>() == null) { Debug.LogError("[Enemy3DBuilder] 템플릿에 EnemyView3D 없음"); return false; }
        if (!AssetDatabase.IsValidFolder(specs.outputFolder)) { Debug.LogError($"[Enemy3DBuilder] 출력 폴더 없음: {specs.outputFolder}"); return false; }
        return true;
    }

    static void Build(Enemy3DBuildSpecs specs, Enemy3DBuildSpecs.Entry e, bool overwrite)
    {
        string error = Validate(specs, e);
        if (error != null) { Debug.LogError($"[Enemy3DBuilder] {e.prefabName}: {error}"); return; }

        string prefabPath = specs.PrefabPath(e);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab != null && !overwrite)
        {
            Debug.Log($"[Enemy3DBuilder] 이미 존재 — 건너뜀: {prefabPath}");
        }
        else
        {
            bool existed = prefab != null;
            prefab = CreatePrefab(specs, e, prefabPath);
            Debug.Log($"[Enemy3DBuilder] {(existed ? "재생성" : "생성")}: {prefabPath}");
        }

        LinkEnemyData(e, prefab);
    }

    static GameObject CreatePrefab(Enemy3DBuildSpecs specs, Enemy3DBuildSpecs.Entry e, string prefabPath)
    {
        var root = new GameObject(e.prefabName);

        // 템플릿 컴포넌트 값 복사(참조 필드는 아래에서 이 프리팹 것으로 덮어씀)
        foreach (var src in specs.template.GetComponents<MonoBehaviour>())
        {
            var type = src.GetType();
            var dst = root.GetComponent(type) ?? root.AddComponent(type);
            EditorUtility.CopySerialized(src, dst);
        }

        // 모델
        var model = (GameObject)PrefabUtility.InstantiatePrefab(e.fbx, root.transform);
        model.name = e.ModelName;
        model.transform.localPosition = e.modelPosition;
        model.transform.localRotation = Quaternion.Euler(e.modelRotation);
        model.transform.localScale = e.modelScale;
        foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = specs.enemyLayer;

        var animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>(); // 컨트롤러는 직접 연결

        var renderers = new List<Renderer>();
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            if (e.material != null)
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = e.material;
                r.sharedMaterials = mats;
            }
            renderers.Add(r);
        }

        // HUD 앵커(기본은 무사와 동일하게 루트 원점)
        var hud = new GameObject("HudAnchor");
        hud.transform.SetParent(root.transform, false);
        hud.transform.localPosition = e.hudAnchorPosition;

        // EnemyView3D 참조를 이 모델 기준으로 설정. 동작 매핑은 컨트롤러 상태 이름에 맞춰 직접 채우도록 비워 둠.
        var view = root.GetComponent<EnemyView3D>();
        var so = new SerializedObject(view);
        so.FindProperty("animator").objectReferenceValue = animator;
        so.FindProperty("shakeTarget").objectReferenceValue = model.transform;
        so.FindProperty("hudAnchor").objectReferenceValue = hud.transform;

        var fr = so.FindProperty("flashRenderers");
        fr.arraySize = renderers.Count;
        for (int i = 0; i < renderers.Count; i++) fr.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];

        so.FindProperty("motionStates").arraySize = 0;
        so.ApplyModifiedPropertiesWithoutUndo();

        LogBounds(e.prefabName, renderers);

        var saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        return saved;
    }

    // 다른 몬스터와 크기 비교용 높이 로그
    static void LogBounds(string label, List<Renderer> renderers)
    {
        if (renderers.Count == 0) return;
        var b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        Debug.Log($"[Enemy3DBuilder] {label} bounds size={b.size} center={b.center}");
    }

    static void LinkEnemyData(Enemy3DBuildSpecs.Entry e, GameObject prefab)
    {
        var data = e.enemyData;
        if (data == null || prefab == null) return;
        if (data.battlePrefab3D == prefab) return;
        if (data.battlePrefab3D != null && !e.replaceExistingLink)
        {
            Debug.Log($"[Enemy3DBuilder] {data.name} 에 이미 다른 3D 프리팹 연결됨({data.battlePrefab3D.name}) — 유지");
            return;
        }
        Undo.RecordObject(data, "Link 3D Enemy Prefab");
        data.battlePrefab3D = prefab;
        EditorUtility.SetDirty(data);
        Debug.Log($"[Enemy3DBuilder] 연결: {data.name} → {prefab.name}");
    }
}
