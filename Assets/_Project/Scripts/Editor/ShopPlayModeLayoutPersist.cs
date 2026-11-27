using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이 모드에서 바꾼 ShopCanvas2 위치·크기를 종료 후 씬에 다시 쓴다.
/// 런타임에 생성된 카드·책 복제본은 씬에 없으므로 저장하지 않는다.
/// </summary>
[InitializeOnLoad]
public static class ShopPlayModeLayoutPersist
{
    const string CanvasName = "ShopCanvas2";
    const string PrefKey = "Capstone.ShopPlayModeLayoutPersist";
    const string MenuPath = "Tools/상점 레이아웃/플레이 종료 후 위치·크기 유지";

    static readonly string SnapshotFile = Path.Combine(
        Directory.GetParent(Application.dataPath).FullName,
        "Temp",
        "ShopPlayModeLayout.json");

    static ShopPlayModeLayoutPersist()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
        SceneView.duringSceneGui += OnSceneGUI;
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

    static void OnHierarchyGUI(int instanceId, Rect rect)
    {
        var go = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
        if (go == null || go.name != CanvasName) return;

        var toggleRect = new Rect(rect.xMax - 18f, rect.y, 16f, rect.height);
        bool next = GUI.Toggle(toggleRect, Enabled, new GUIContent(string.Empty, "플레이 종료 후에도 상점 위치·크기 유지"));
        if (next != Enabled)
            Enabled = next;
    }

    static void OnSceneGUI(SceneView view)
    {
        if (!Application.isPlaying) return;

        Handles.BeginGUI();
        GUILayout.BeginArea(new Rect(8f, 8f, 340f, 26f));
        bool next = GUILayout.Toggle(Enabled, "상점 위치·크기, 종료 후에도 유지", "Button");
        if (next != Enabled)
            Enabled = next;
        GUILayout.EndArea();
        Handles.EndGUI();
    }

    static void WriteSnapshot()
    {
        Transform canvas = FindShopCanvas();
        if (canvas == null)
        {
            DeleteSnapshot();
            return;
        }

        var list = new SnapshotList();
        Collect(canvas, CanvasName, list.items);
        if (list.items.Count == 0)
        {
            DeleteSnapshot();
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(SnapshotFile));
        File.WriteAllText(SnapshotFile, JsonUtility.ToJson(list));
    }

    static void ApplySnapshot()
    {
        if (!File.Exists(SnapshotFile)) return;

        SnapshotList list;
        try
        {
            list = JsonUtility.FromJson<SnapshotList>(File.ReadAllText(SnapshotFile));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"상점 레이아웃 스냅샷을 읽지 못했습니다. {e.Message}");
            DeleteSnapshot();
            return;
        }

        DeleteSnapshot();
        if (list == null || list.items == null || list.items.Count == 0) return;

        Transform canvas = FindShopCanvas();
        if (canvas == null) return;

        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("상점 플레이 모드 레이아웃 적용");

        int changed = 0;
        var names = new List<string>();
        for (int i = 0; i < list.items.Count; i++)
        {
            Snapshot snap = list.items[i];
            Transform target = Resolve(canvas, snap.path);
            if (target is not RectTransform rt) continue;
            if (NearlySame(rt, snap)) continue;

            Undo.RecordObject(rt, "상점 플레이 모드 레이아웃 적용");
            rt.anchorMin = snap.anchorMin;
            rt.anchorMax = snap.anchorMax;
            rt.pivot = snap.pivot;
            rt.localRotation = snap.localRotation;
            if (snap.localScale.sqrMagnitude > 0.0001f)
                rt.localScale = snap.localScale;
            rt.anchoredPosition = snap.anchoredPosition;
            rt.sizeDelta = snap.sizeDelta;
            EditorUtility.SetDirty(rt);
            if (PrefabUtility.IsPartOfPrefabInstance(rt))
                PrefabUtility.RecordPrefabInstancePropertyModifications(rt);

            changed++;
            if (names.Count < 8)
                names.Add(target.name);
        }

        Undo.CollapseUndoOperations(group);
        if (changed == 0) return;

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log($"상점 레이아웃 {changed}개를 씬에 반영했습니다. ({string.Join(", ", names)}) 씬을 저장하면 남습니다.");
    }

    static void Collect(Transform node, string path, List<Snapshot> into)
    {
        if (node.name.Contains("(Clone)")) return;

        if (node is RectTransform rt)
            into.Add(Read(rt, path));

        var used = new Dictionary<string, int>();
        for (int i = 0; i < node.childCount; i++)
        {
            Transform child = node.GetChild(i);
            if (child.name.Contains("(Clone)")) continue;

            used.TryGetValue(child.name, out int ordinal);
            used[child.name] = ordinal + 1;
            string segment = ordinal == 0 ? child.name : child.name + "#" + ordinal;
            Collect(child, path + "/" + segment, into);
        }
    }

    static Snapshot Read(RectTransform rt, string path)
    {
        return new Snapshot
        {
            path = path,
            anchorMin = rt.anchorMin,
            anchorMax = rt.anchorMax,
            pivot = rt.pivot,
            anchoredPosition = rt.anchoredPosition,
            sizeDelta = rt.sizeDelta,
            localScale = rt.localScale,
            localRotation = rt.localRotation
        };
    }

    static Transform Resolve(Transform canvas, string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        string[] parts = path.Split('/');
        if (parts.Length == 0 || parts[0] != CanvasName) return null;

        Transform current = canvas;
        for (int i = 1; i < parts.Length; i++)
        {
            current = FindChildSegment(current, parts[i]);
            if (current == null) return null;
        }

        return current;
    }

    // 같은 이름이 여러 개면 Name, Name#1 순으로 찾는다. 런타임 복제본은 건너뛴다.
    static Transform FindChildSegment(Transform parent, string segment)
    {
        string name = segment;
        int ordinal = 0;
        int hash = segment.LastIndexOf('#');
        if (hash > 0 && int.TryParse(segment.Substring(hash + 1), out int parsed))
        {
            name = segment.Substring(0, hash);
            ordinal = parsed;
        }

        int seen = 0;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name != name) continue;
            if (seen == ordinal) return child;
            seen++;
        }

        return null;
    }

    static bool NearlySame(RectTransform rt, Snapshot snap)
    {
        return Near(rt.anchorMin, snap.anchorMin)
            && Near(rt.anchorMax, snap.anchorMax)
            && Near(rt.pivot, snap.pivot)
            && Near(rt.anchoredPosition, snap.anchoredPosition)
            && Near(rt.sizeDelta, snap.sizeDelta)
            && Near(rt.localScale, snap.localScale)
            && Quaternion.Angle(rt.localRotation, snap.localRotation) < 0.1f;
    }

    static bool Near(Vector2 a, Vector2 b)
    {
        return (a - b).sqrMagnitude < 0.01f;
    }

    static bool Near(Vector3 a, Vector3 b)
    {
        return (a - b).sqrMagnitude < 0.0001f;
    }

    static Transform FindShopCanvas()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                Transform found = FindNamed(roots[r].transform, CanvasName);
                if (found != null) return found;
            }
        }

        return null;
    }

    static Transform FindNamed(Transform node, string name)
    {
        if (node.name == name) return node;

        for (int i = 0; i < node.childCount; i++)
        {
            Transform found = FindNamed(node.GetChild(i), name);
            if (found != null) return found;
        }

        return null;
    }

    static void DeleteSnapshot()
    {
        if (File.Exists(SnapshotFile))
            File.Delete(SnapshotFile);
    }

    [Serializable]
    class SnapshotList
    {
        public List<Snapshot> items = new List<Snapshot>();
    }

    [Serializable]
    class Snapshot
    {
        public string path;
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
        public Vector3 localScale;
        public Quaternion localRotation;
    }
}
