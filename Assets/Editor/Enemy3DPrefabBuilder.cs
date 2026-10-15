using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 3D 몬스터 전투 프리팹 자동 생성기.
// 무사(EnemySamu) 프리팹을 템플릿으로 컴포넌트 값(EnemyController/EnemyView3D/EnemyStat/MonsterMidPattern)을 복사하고,
// FBX 모델 + 머티리얼 + HudAnchor를 붙여 프리팹으로 저장한 뒤 EnemyData.battlePrefab3D에 연결한다.
// Animator Controller와 EnemyView3D의 동작 매핑(Motion States)은 만들지 않는다 — 직접 구성.
// 이미 존재하는 프리팹은 덮어쓰지 않는다.
public static class Enemy3DPrefabBuilder
{
    const string TemplatePrefabPath = "Assets/Prefab/3D/EnemySamu.prefab"; // 템플릿(무사)
    const string PrefabFolder = "Assets/Prefab/3D";                         // 출력 폴더
    const int EnemyLayer = 7;                                               // Enemy3D 레이어

    // 몬스터 한 종의 생성 사양
    class Spec
    {
        public string prefabName;      // 프리팹 파일/루트 이름
        public string modelName;       // 모델 자식 오브젝트 이름
        public string fbxPath;         // 모델 FBX
        public string materialPath;    // 적용할 머티리얼
        public string enemyDataPath;   // 연결할 EnemyData
    }

    static readonly Spec[] Specs =
    {
        new Spec
        {
            prefabName = "EnemyClayOni", modelName = "토우귀",
            fbxPath = "Assets/3D_Character/Land/토우귀/Character/ClayOni.fbx",
            materialPath = "Assets/3D_Character/Land/토우귀/Character/ClayOni_Base.mat",
            enemyDataPath = "Assets/Script/Enemy/LandPuppetEnemyData.asset",
        },
        new Spec
        {
            prefabName = "EnemyDrownedOni", modelName = "수장귀",
            fbxPath = "Assets/3D_Character/Water/수장귀/Character/DrownedOni.fbx",
            materialPath = "Assets/3D_Character/Water/수장귀/Character/DrownedOni_Base.mat",
            enemyDataPath = "Assets/Script/Enemy/WaterGhostEnemyData.asset",
        },
    };

    [MenuItem("Tools/3D Enemy/Build Missing 3D Enemy Prefabs")]
    public static void BuildAll()
    {
        var template = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePrefabPath);
        if (template == null) { Debug.LogError($"[Enemy3DBuilder] 템플릿 없음: {TemplatePrefabPath}"); return; }

        var tmp = (GameObject)PrefabUtility.InstantiatePrefab(template);
        LogBounds(template.name, new List<Renderer>(tmp.GetComponentsInChildren<Renderer>(true)));
        Object.DestroyImmediate(tmp);

        foreach (var spec in Specs) Build(spec, template);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // batchmode 진입점: -executeMethod Enemy3DPrefabBuilder.BuildAllBatch
    public static void BuildAllBatch()
    {
        BuildAll();
        EditorApplication.Exit(0);
    }

    static void Build(Spec spec, GameObject template)
    {
        string prefabPath = $"{PrefabFolder}/{spec.prefabName}.prefab";
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(spec.fbxPath);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(spec.materialPath);
        if (fbx == null) { Debug.LogError($"[Enemy3DBuilder] FBX 없음: {spec.fbxPath}"); return; }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab != null)
        {
            Debug.Log($"[Enemy3DBuilder] 이미 존재 — 건너뜀: {prefabPath}");
        }
        else
        {
            prefab = CreatePrefab(spec, template, fbx, mat, prefabPath);
            Debug.Log($"[Enemy3DBuilder] 생성: {prefabPath}");
        }

        LinkEnemyData(spec, prefab);
    }

    static GameObject CreatePrefab(Spec spec, GameObject template, GameObject fbx, Material mat, string prefabPath)
    {
        var root = new GameObject(spec.prefabName);

        // 템플릿 컴포넌트 값 복사(참조 필드는 아래에서 이 프리팹 것으로 덮어씀)
        foreach (var src in template.GetComponents<MonoBehaviour>())
        {
            var type = src.GetType();
            var dst = root.GetComponent(type) ?? root.AddComponent(type);
            EditorUtility.CopySerialized(src, dst);
        }

        // 모델
        var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
        model.name = spec.modelName;
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = EnemyLayer;

        var animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>(); // 컨트롤러는 직접 연결

        var renderers = new List<Renderer>();
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            if (mat != null)
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
            renderers.Add(r);
        }

        // HUD 앵커(무사와 동일하게 루트 원점)
        var hud = new GameObject("HudAnchor");
        hud.transform.SetParent(root.transform, false);

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

        LogBounds(spec.prefabName, renderers);

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

    static void LinkEnemyData(Spec spec, GameObject prefab)
    {
        var data = AssetDatabase.LoadAssetAtPath<EnemyData>(spec.enemyDataPath);
        if (data == null) { Debug.LogError($"[Enemy3DBuilder] EnemyData 없음: {spec.enemyDataPath}"); return; }
        if (data.battlePrefab3D == prefab) return;
        if (data.battlePrefab3D != null)
        {
            Debug.Log($"[Enemy3DBuilder] {spec.enemyDataPath} 에 이미 다른 3D 프리팹 연결됨 — 유지");
            return;
        }
        data.battlePrefab3D = prefab;
        EditorUtility.SetDirty(data);
        Debug.Log($"[Enemy3DBuilder] 연결: {spec.enemyDataPath} → {prefab.name}");
    }
}
