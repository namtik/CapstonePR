using Battle.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// UI_Tab_Frame의 RectTransform을 바꾸면 BattleTabPanel의 탭 프레임 값에 맞춘다.
/// 플레이가 시작되면 그 값이 프레임 크기와 위치를 다시 쓰기 때문이다.
/// </summary>
[InitializeOnLoad]
public static class BattleTabFrameSync
{
    static bool hasLast;
    static int panelId;
    static Vector2 lastRectSize;
    static Vector2 lastRectPos;
    static Vector2 lastFieldSize;
    static Vector2 lastFieldPos;

    static BattleTabFrameSync()
    {
        EditorApplication.update += Sync;
    }

    static void Sync()
    {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            hasLast = false;
            return;
        }

        var panel = Object.FindFirstObjectByType<BattleTabPanel>(FindObjectsInactive.Include);
        if (panel == null) return;

        var so = new SerializedObject(panel);
        var rootProp = so.FindProperty("panelRoot");
        var root = rootProp != null ? rootProp.objectReferenceValue as GameObject : null;
        if (root == null) return;
        var rect = root.GetComponent<RectTransform>();
        if (rect == null) return;

        var sizeProp = so.FindProperty("frameSize");
        var posProp = so.FindProperty("framePosition");
        if (sizeProp == null || posProp == null) return;

        Vector2 rectSize = rect.sizeDelta;
        Vector2 rectPos = rect.anchoredPosition;
        Vector2 fieldSize = sizeProp.vector2Value;
        Vector2 fieldPos = posProp.vector2Value;

        if (panel.GetInstanceID() != panelId)
            hasLast = false;

        if (!hasLast)
        {
            if (rectSize != fieldSize || rectPos != fieldPos)
                WriteField(so, sizeProp, posProp, rectSize, rectPos, panel);
            Remember(panel, rect.sizeDelta, rect.anchoredPosition, sizeProp.vector2Value, posProp.vector2Value);
            return;
        }

        bool rectChanged = rectSize != lastRectSize || rectPos != lastRectPos;
        bool fieldChanged = fieldSize != lastFieldSize || fieldPos != lastFieldPos;
        if (rectChanged && !fieldChanged)
        {
            WriteField(so, sizeProp, posProp, rectSize, rectPos, panel);
            Remember(panel, rectSize, rectPos, rectSize, rectPos);
        }
        else if (fieldChanged && !rectChanged)
        {
            WriteRect(rect, fieldSize, fieldPos);
            Remember(panel, fieldSize, fieldPos, fieldSize, fieldPos);
        }
        else
        {
            Remember(panel, rectSize, rectPos, fieldSize, fieldPos);
        }
    }

    static void WriteField(SerializedObject so, SerializedProperty sizeProp, SerializedProperty posProp, Vector2 size, Vector2 pos, BattleTabPanel panel)
    {
        sizeProp.vector2Value = size;
        posProp.vector2Value = pos;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(panel);
        EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
    }

    static void WriteRect(RectTransform rect, Vector2 size, Vector2 pos)
    {
        rect.sizeDelta = size;
        rect.anchoredPosition = pos;
        EditorUtility.SetDirty(rect);
        EditorSceneManager.MarkSceneDirty(rect.gameObject.scene);
    }

    static void Remember(BattleTabPanel panel, Vector2 rectSize, Vector2 rectPos, Vector2 fieldSize, Vector2 fieldPos)
    {
        panelId = panel.GetInstanceID();
        lastRectSize = rectSize;
        lastRectPos = rectPos;
        lastFieldSize = fieldSize;
        lastFieldPos = fieldPos;
        hasLast = true;
    }
}
