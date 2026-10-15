using System.IO;
using UnityEditor;
using UnityEngine;

// 3D 몬스터 프리팹 빌더 창: Enemy3DBuildSpecs 스펙을 추가/수정/삭제하고 개별·일괄 생성한다.
public class Enemy3DPrefabBuilderWindow : EditorWindow
{
    Enemy3DBuildSpecs specs; // 편집 중인 스펙 에셋
    Vector2 scroll;          // 스크롤 위치

    [MenuItem("Tools/3D Enemy/Prefab Builder")]
    public static void Open() => GetWindow<Enemy3DPrefabBuilderWindow>("3D Enemy Builder");

    void OnEnable()
    {
        if (specs == null) specs = Enemy3DPrefabBuilder.LoadOrCreateSpecs();
    }

    void OnGUI()
    {
        specs = (Enemy3DBuildSpecs)EditorGUILayout.ObjectField("스펙 에셋", specs, typeof(Enemy3DBuildSpecs), false);
        if (specs == null)
        {
            if (GUILayout.Button("스펙 에셋 찾기/생성")) specs = Enemy3DPrefabBuilder.LoadOrCreateSpecs();
            return;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        Enemy3DBuildSpecsGUI.Draw(specs);
        EditorGUILayout.EndScrollView();
    }
}

// 스펙 에셋 인스펙터(창과 같은 UI)
[CustomEditor(typeof(Enemy3DBuildSpecs))]
public class Enemy3DBuildSpecsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (GUILayout.Button("빌더 창 열기")) Enemy3DPrefabBuilderWindow.Open();
        EditorGUILayout.Space();
        Enemy3DBuildSpecsGUI.Draw((Enemy3DBuildSpecs)target);
    }
}

// 창과 인스펙터가 공유하는 스펙 편집 UI
static class Enemy3DBuildSpecsGUI
{
    enum Op { None, Build, Rebuild, Duplicate, Remove, Up, Down }

    public static void Draw(Enemy3DBuildSpecs specs)
    {
        var so = new SerializedObject(specs);
        so.Update();

        // 공통 설정
        EditorGUILayout.LabelField("공통 설정", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(so.FindProperty("template"), new GUIContent("템플릿 프리팹"));
        DrawFolderField(so.FindProperty("outputFolder"));
        var layer = so.FindProperty("enemyLayer");
        layer.intValue = EditorGUILayout.LayerField(new GUIContent("모델 레이어"), layer.intValue);

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField($"스펙 ({specs.entries.Count})", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ 스펙 추가", GUILayout.Width(90)))
            {
                so.ApplyModifiedProperties();
                Undo.RecordObject(specs, "Add 3D Enemy Spec");
                specs.entries.Add(new Enemy3DBuildSpecs.Entry { prefabName = "EnemyNew" });
                EditorUtility.SetDirty(specs);
                GUIUtility.ExitGUI();
            }
        }

        DrawDropArea(so, specs);

        // 스펙 목록
        var entries = so.FindProperty("entries");
        int target = -1;
        Op op = Op.None;
        for (int i = 0; i < entries.arraySize; i++)
        {
            var p = entries.GetArrayElementAtIndex(i);
            var e = specs.entries[i];
            var row = DrawEntry(specs, p, e, i, entries.arraySize);
            if (row != Op.None) { op = row; target = i; }
        }

        so.ApplyModifiedProperties();

        EditorGUILayout.Space();
        if (GUILayout.Button("누락된 프리팹 전체 생성 (체크된 스펙)", GUILayout.Height(28)))
        {
            Enemy3DPrefabBuilder.BuildAll(specs);
            GUIUtility.ExitGUI();
        }

        if (op != Op.None) Apply(specs, op, target);
    }

    static Op DrawEntry(Enemy3DBuildSpecs specs, SerializedProperty p, Enemy3DBuildSpecs.Entry e, int index, int count)
    {
        Op op = Op.None;
        string error = Enemy3DPrefabBuilder.Validate(specs, e);
        string path = string.IsNullOrWhiteSpace(e.prefabName) ? null : specs.PrefabPath(e);
        var prefab = path != null ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var enabled = p.FindPropertyRelative("enabled");
                enabled.boolValue = EditorGUILayout.Toggle(enabled.boolValue, GUILayout.Width(16));

                string status = error != null ? "⚠ 오류" : prefab != null ? "✓ 생성됨" : "○ 미생성";
                string title = string.IsNullOrWhiteSpace(e.prefabName) ? "(이름 없음)" : e.prefabName;
                if (!string.IsNullOrEmpty(e.ModelName)) title += $"  [{e.ModelName}]";
                p.isExpanded = EditorGUILayout.Foldout(p.isExpanded, $"{title}   {status}", true);

                using (new EditorGUI.DisabledScope(error != null))
                {
                    if (prefab == null)
                    {
                        if (GUILayout.Button("생성", EditorStyles.miniButtonLeft, GUILayout.Width(44))) op = Op.Build;
                    }
                    else if (GUILayout.Button("재생성", EditorStyles.miniButtonLeft, GUILayout.Width(44))) op = Op.Rebuild;
                }
                using (new EditorGUI.DisabledScope(prefab == null))
                    if (GUILayout.Button("선택", EditorStyles.miniButtonMid, GUILayout.Width(36)))
                    {
                        Selection.activeObject = prefab;
                        EditorGUIUtility.PingObject(prefab);
                    }
                using (new EditorGUI.DisabledScope(index == 0))
                    if (GUILayout.Button("▲", EditorStyles.miniButtonMid, GUILayout.Width(22))) op = Op.Up;
                using (new EditorGUI.DisabledScope(index == count - 1))
                    if (GUILayout.Button("▼", EditorStyles.miniButtonMid, GUILayout.Width(22))) op = Op.Down;
                if (GUILayout.Button("복제", EditorStyles.miniButtonMid, GUILayout.Width(36))) op = Op.Duplicate;
                if (GUILayout.Button("✕", EditorStyles.miniButtonRight, GUILayout.Width(22))) op = Op.Remove;
            }

            if (p.isExpanded)
            {
                EditorGUI.indentLevel++;
                var it = p.Copy();
                var end = p.GetEndProperty();
                it.NextVisible(true);
                while (!SerializedProperty.EqualContents(it, end))
                {
                    if (it.name != "enabled") EditorGUILayout.PropertyField(it, true);
                    if (!it.NextVisible(false)) break;
                }
                if (path != null) EditorGUILayout.LabelField("저장 경로", path, EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }

            if (error != null) EditorGUILayout.HelpBox(error, MessageType.Warning);
        }
        return op;
    }

    static void Apply(Enemy3DBuildSpecs specs, Op op, int i)
    {
        var e = specs.entries[i];
        switch (op)
        {
            case Op.Build:
                Enemy3DPrefabBuilder.BuildOne(specs, e, false);
                break;
            case Op.Rebuild:
                if (EditorUtility.DisplayDialog("프리팹 재생성",
                        $"{specs.PrefabPath(e)} 를 다시 만듭니다.\n직접 연결한 Animator Controller·Motion States 등 프리팹 수정 사항이 사라집니다. (GUID는 유지)",
                        "재생성", "취소"))
                    Enemy3DPrefabBuilder.BuildOne(specs, e, true);
                break;
            case Op.Duplicate:
                Undo.RecordObject(specs, "Duplicate 3D Enemy Spec");
                var copy = JsonUtility.FromJson<Enemy3DBuildSpecs.Entry>(JsonUtility.ToJson(e));
                copy.fbx = e.fbx; copy.material = e.material; copy.enemyData = e.enemyData; // 에셋 참조 유지
                copy.prefabName = e.prefabName + "_Copy";
                specs.entries.Insert(i + 1, copy);
                break;
            case Op.Remove:
                Undo.RecordObject(specs, "Remove 3D Enemy Spec");
                specs.entries.RemoveAt(i); // 스펙만 삭제(이미 만든 프리팹은 그대로)
                break;
            case Op.Up:
            case Op.Down:
                Undo.RecordObject(specs, "Move 3D Enemy Spec");
                int j = op == Op.Up ? i - 1 : i + 1;
                (specs.entries[i], specs.entries[j]) = (specs.entries[j], specs.entries[i]);
                break;
        }
        EditorUtility.SetDirty(specs);
        GUIUtility.ExitGUI();
    }

    // 출력 폴더: 텍스트 + 폴더 선택
    static void DrawFolderField(SerializedProperty folder)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PropertyField(folder, new GUIContent("출력 폴더"));
            if (GUILayout.Button("…", GUILayout.Width(24)))
            {
                string abs = EditorUtility.OpenFolderPanel("프리팹 출력 폴더", folder.stringValue, "");
                string dataPath = Application.dataPath.Replace('\\', '/');
                abs = abs.Replace('\\', '/');
                if (abs.StartsWith(dataPath)) folder.stringValue = "Assets" + abs.Substring(dataPath.Length);
                else if (!string.IsNullOrEmpty(abs)) Debug.LogWarning("[Enemy3DBuilder] 프로젝트 Assets 안의 폴더만 선택 가능");
            }
        }
        if (!AssetDatabase.IsValidFolder(folder.stringValue))
            EditorGUILayout.HelpBox("출력 폴더가 존재하지 않음", MessageType.Error);
    }

    // FBX 드래그 → 스펙 자동 추가(같은 폴더의 '{이름}_Base.mat' 머티리얼 자동 지정)
    static void DrawDropArea(SerializedObject so, Enemy3DBuildSpecs specs)
    {
        var rect = GUILayoutUtility.GetRect(0, 32, GUILayout.ExpandWidth(true));
        GUI.Box(rect, "여기에 FBX를 끌어다 놓으면 스펙 추가", EditorStyles.helpBox);

        var evt = Event.current;
        if (!rect.Contains(evt.mousePosition)) return;
        if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform) return;

        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        if (evt.type != EventType.DragPerform) { evt.Use(); return; }

        DragAndDrop.AcceptDrag();
        so.ApplyModifiedProperties();
        Undo.RecordObject(specs, "Add 3D Enemy Spec From FBX");
        foreach (var obj in DragAndDrop.objectReferences)
        {
            if (!(obj is GameObject go)) continue;
            string path = AssetDatabase.GetAssetPath(go);
            if (!(AssetImporter.GetAtPath(path) is ModelImporter)) continue;

            string dir = Path.GetDirectoryName(path).Replace('\\', '/');
            specs.entries.Add(new Enemy3DBuildSpecs.Entry
            {
                prefabName = "Enemy" + go.name,
                fbx = go,
                material = AssetDatabase.LoadAssetAtPath<Material>($"{dir}/{go.name}_Base.mat"),
            });
        }
        EditorUtility.SetDirty(specs);
        evt.Use();
        GUIUtility.ExitGUI();
    }
}
