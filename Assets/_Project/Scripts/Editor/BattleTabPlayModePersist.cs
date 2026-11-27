using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 임시. 플레이 모드에서 바꾼 BattleTabPanel, BattleLogView 인스펙터 값을 종료 후 씬에 다시 쓴다.
/// 배치가 끝나면 이 파일을 지우면 된다.
/// Tools 메뉴에서 끄고 켤 수 있다.
/// </summary>
[InitializeOnLoad]
public static class BattleTabPlayModePersist
{
    const string PrefKey = "Capstone.BattleTabPlayModePersist";
    const string MenuPath = "Tools/탭 레이아웃/플레이 종료 후 인스펙터 유지";

    static readonly string SnapshotFile = Path.Combine(
        Directory.GetParent(Application.dataPath).FullName,
        "Temp",
        "BattleTabPlayMode.json");

    static readonly string LogSnapshotFile = Path.Combine(
        Directory.GetParent(Application.dataPath).FullName,
        "Temp",
        "BattleLogPlayMode.json");

    static BattleTabPlayModePersist()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    public static bool Enabled
    {
        get => EditorPrefs.GetBool(PrefKey, true);
        set => EditorPrefs.SetBool(PrefKey, value);
    }

    [MenuItem(MenuPath)]
    static void ToggleEnabled()
    {
        Enabled = !Enabled;
    }

    [MenuItem(MenuPath, true)]
    static bool ToggleEnabledValidate()
    {
        Menu.SetChecked(MenuPath, Enabled);
        return true;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            if (!Enabled)
            {
                DeleteSnapshot();
                return;
            }

            WriteSnapshot();
            return;
        }

        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += ApplySnapshot;
    }

    static void WriteSnapshot()
    {
        var panel = UnityEngine.Object.FindFirstObjectByType<Battle.UI.BattleTabPanel>(FindObjectsInactive.Include);
        if (!WriteComponent(panel, SnapshotFile, true))
            DeleteFile(SnapshotFile);

        var log = UnityEngine.Object.FindFirstObjectByType<Battle.UI.BattleLogView>(FindObjectsInactive.Include);
        if (!WriteComponent(log, LogSnapshotFile, false))
            DeleteFile(LogSnapshotFile);
    }

    static bool WriteComponent(UnityEngine.Object target, string file, bool tab)
    {
        if (target == null) return false;

        var list = new PropList();
        var so = new SerializedObject(target);
        SerializedProperty it = so.GetIterator();
        bool enter = true;
        while (it.NextVisible(enter))
        {
            enter = false;
            if (it.propertyPath == "m_Script") continue;
            if (tab && (it.propertyPath == "panelRoot" || it.propertyPath.StartsWith("columns")))
                continue;

            Prop prop = Read(it);
            if (prop != null) list.items.Add(prop);
        }

        if (list.items.Count == 0) return false;

        Directory.CreateDirectory(Path.GetDirectoryName(file));
        File.WriteAllText(file, JsonUtility.ToJson(list));
        return true;
    }

    static void ApplySnapshot()
    {
        ApplyFile(
            SnapshotFile,
            UnityEngine.Object.FindFirstObjectByType<Battle.UI.BattleTabPanel>(FindObjectsInactive.Include),
            "탭 플레이 모드 인스펙터 적용",
            "탭");
        ApplyFile(
            LogSnapshotFile,
            UnityEngine.Object.FindFirstObjectByType<Battle.UI.BattleLogView>(FindObjectsInactive.Include),
            "전투 기록 플레이 모드 인스펙터 적용",
            "전투 기록");
    }

    static void ApplyFile(string file, UnityEngine.Object target, string undoName, string label)
    {
        if (!File.Exists(file)) return;

        PropList list;
        try
        {
            list = JsonUtility.FromJson<PropList>(File.ReadAllText(file));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"{label} 레이아웃 스냅샷을 읽지 못했습니다. {e.Message}");
            DeleteFile(file);
            return;
        }

        DeleteFile(file);
        if (list == null || list.items == null || list.items.Count == 0 || target == null) return;

        var so = new SerializedObject(target);
        Undo.RecordObject(target, undoName);
        int changed = 0;
        for (int i = 0; i < list.items.Count; i++)
        {
            Prop prop = list.items[i];
            SerializedProperty dst = so.FindProperty(prop.path);
            if (dst == null) continue;
            if (Write(dst, prop)) changed++;
        }

        if (changed == 0) return;

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
        var component = target as Component;
        if (component != null)
            EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
        Debug.Log($"{label} 인스펙터 값 {changed}개를 씬에 반영했습니다. 씬을 저장하면 남습니다.");
    }

    static Prop Read(SerializedProperty prop)
    {
        var item = new Prop { path = prop.propertyPath, kind = prop.propertyType.ToString() };
        switch (prop.propertyType)
        {
            case SerializedPropertyType.Float:
                item.x = prop.floatValue;
                return item;
            case SerializedPropertyType.Integer:
            case SerializedPropertyType.Enum:
            case SerializedPropertyType.LayerMask:
                item.n = prop.intValue;
                return item;
            case SerializedPropertyType.Boolean:
                item.n = prop.boolValue ? 1 : 0;
                return item;
            case SerializedPropertyType.Vector2:
                item.x = prop.vector2Value.x;
                item.y = prop.vector2Value.y;
                return item;
            case SerializedPropertyType.Vector3:
                item.x = prop.vector3Value.x;
                item.y = prop.vector3Value.y;
                item.z = prop.vector3Value.z;
                return item;
            case SerializedPropertyType.Vector4:
                item.x = prop.vector4Value.x;
                item.y = prop.vector4Value.y;
                item.z = prop.vector4Value.z;
                item.w = prop.vector4Value.w;
                return item;
            case SerializedPropertyType.Color:
                Color c = prop.colorValue;
                item.x = c.r;
                item.y = c.g;
                item.z = c.b;
                item.w = c.a;
                return item;
            case SerializedPropertyType.String:
                item.text = prop.stringValue;
                return item;
            case SerializedPropertyType.ObjectReference:
                UnityEngine.Object obj = prop.objectReferenceValue;
                if (obj != null && !EditorUtility.IsPersistent(obj)) return null;
                item.text = obj == null ? string.Empty : AssetDatabase.GetAssetPath(obj);
                return item;
            default:
                return null;
        }
    }

    static bool Write(SerializedProperty prop, Prop item)
    {
        switch (item.kind)
        {
            case "Float":
                if (Mathf.Approximately(prop.floatValue, item.x)) return false;
                prop.floatValue = item.x;
                return true;
            case "Integer":
            case "Enum":
            case "LayerMask":
                if (prop.intValue == item.n) return false;
                prop.intValue = item.n;
                return true;
            case "Boolean":
                bool value = item.n != 0;
                if (prop.boolValue == value) return false;
                prop.boolValue = value;
                return true;
            case "Vector2":
                var v2 = new Vector2(item.x, item.y);
                if (prop.vector2Value == v2) return false;
                prop.vector2Value = v2;
                return true;
            case "Vector3":
                var v3 = new Vector3(item.x, item.y, item.z);
                if (prop.vector3Value == v3) return false;
                prop.vector3Value = v3;
                return true;
            case "Vector4":
                var v4 = new Vector4(item.x, item.y, item.z, item.w);
                if (prop.vector4Value == v4) return false;
                prop.vector4Value = v4;
                return true;
            case "Color":
                var color = new Color(item.x, item.y, item.z, item.w);
                if (prop.colorValue == color) return false;
                prop.colorValue = color;
                return true;
            case "String":
                if (prop.stringValue == item.text) return false;
                prop.stringValue = item.text ?? string.Empty;
                return true;
            case "ObjectReference":
                UnityEngine.Object asset = string.IsNullOrEmpty(item.text)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(item.text);
                if (prop.objectReferenceValue == asset) return false;
                prop.objectReferenceValue = asset;
                return true;
            default:
                return false;
        }
    }

    static void DeleteSnapshot()
    {
        DeleteFile(SnapshotFile);
        DeleteFile(LogSnapshotFile);
    }

    static void DeleteFile(string file)
    {
        if (File.Exists(file))
            File.Delete(file);
    }

    [Serializable]
    class PropList
    {
        public List<Prop> items = new List<Prop>();
    }

    [Serializable]
    class Prop
    {
        public string path;
        public string kind;
        public string text;
        public int n;
        public float x;
        public float y;
        public float z;
        public float w;
    }
}
