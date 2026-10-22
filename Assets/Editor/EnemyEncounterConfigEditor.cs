using UnityEditor;
using UnityEngine;

// 스테이지(n-n)마다 등장 조합 프리셋을 바로 보이게 한다.
[CustomEditor(typeof(EnemyEncounterConfig))]
public class EnemyEncounterConfigEditor : Editor
{
    bool[] _lapOpen = System.Array.Empty<bool>();
    bool[] _eliteOpen = System.Array.Empty<bool>();

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var cfg = (EnemyEncounterConfig)target;

        EditorGUILayout.HelpBox(
            "일반 전투는 위 스테이지 프리셋, 정예 전투는 아래 라운드 프리셋을 씁니다. 보스는 항상 1마리입니다.\n" +
            "1-1, 1-2처럼 스테이지마다 등장 조합을 넣고, 정예는 1라운드/2라운드 목록에 넣으세요.\n" +
            "프리셋 수를 늘리면 칸이 추가되고, 전투 시작 시 그중 하나가 랜덤으로 선택됩니다.\n" +
            "유효한 프리셋이 없으면 라운드 에셋에 들어 있던 적 목록을 사용합니다.",
            MessageType.Info);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("stagesPerLap"), new GUIContent("바퀴당 스테이지 수"));

        if (GUILayout.Button("빠진 바퀴/스테이지 칸 채우기"))
        {
            Undo.RecordObject(cfg, "Ensure encounter layout");
            cfg.EnsureLayout();
            EditorUtility.SetDirty(cfg);
            serializedObject.Update();
        }

        SerializedProperty laps = serializedObject.FindProperty("laps");
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("스테이지별 등장", EditorStyles.boldLabel);
        laps.arraySize = Mathf.Max(1, EditorGUILayout.IntField("바퀴 수", laps.arraySize));
        EnsureLapFoldouts(laps.arraySize);

        for (int i = 0; i < laps.arraySize; i++)
        {
            SerializedProperty lapProp = laps.GetArrayElementAtIndex(i);
            SerializedProperty lapNumber = lapProp.FindPropertyRelative("lap");
            SerializedProperty stages = lapProp.FindPropertyRelative("stages");
            int lap = Mathf.Max(1, lapNumber.intValue);

            _lapOpen[i] = EditorGUILayout.Foldout(_lapOpen[i], $"{lap}바퀴", true, EditorStyles.foldoutHeader);
            if (!_lapOpen[i]) continue;

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(lapNumber, new GUIContent("바퀴 번호"));
            if (stages == null)
            {
                EditorGUI.indentLevel--;
                continue;
            }

            for (int s = 0; s < stages.arraySize; s++)
                DrawStage(lap, stages.GetArrayElementAtIndex(s), s);

            EditorGUI.indentLevel--;
        }

        DrawEliteSection();
        serializedObject.ApplyModifiedProperties();
    }

    void DrawEliteSection()
    {
        SerializedProperty eliteLaps = serializedObject.FindProperty("eliteLaps");
        EditorGUILayout.Space(12f);
        EditorGUILayout.LabelField("정예 등장", EditorStyles.boldLabel);
        if (eliteLaps == null) return;

        eliteLaps.arraySize = Mathf.Max(2, EditorGUILayout.IntField("정예 라운드 수", eliteLaps.arraySize));
        EnsureEliteFoldouts(eliteLaps.arraySize);

        for (int i = 0; i < eliteLaps.arraySize; i++)
        {
            SerializedProperty lapProp = eliteLaps.GetArrayElementAtIndex(i);
            SerializedProperty lapNumber = lapProp.FindPropertyRelative("lap");
            SerializedProperty presets = lapProp.FindPropertyRelative("presets");
            int lap = Mathf.Max(1, lapNumber != null ? lapNumber.intValue : i + 1);

            _eliteOpen[i] = EditorGUILayout.Foldout(_eliteOpen[i], $"{lap}라운드 정예", true, EditorStyles.foldoutHeader);
            if (!_eliteOpen[i]) continue;

            EditorGUI.indentLevel++;
            EditorGUILayout.BeginVertical("box");
            if (lapNumber != null)
                EditorGUILayout.PropertyField(lapNumber, new GUIContent("라운드 번호"));
            DrawPresetList(presets);
            EditorGUILayout.EndVertical();
            EditorGUI.indentLevel--;
        }
    }

    static void DrawStage(int lap, SerializedProperty stageProp, int stageIndex)
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField($"{lap}-{stageIndex + 1}", EditorStyles.boldLabel);
        DrawPresetList(stageProp.FindPropertyRelative("presets"));
        EditorGUILayout.EndVertical();
    }

    static void DrawPresetList(SerializedProperty presets)
    {
        if (presets == null) return;

        int nextCount = Mathf.Max(0, EditorGUILayout.IntField("프리셋 수", presets.arraySize));
        if (nextCount != presets.arraySize)
            presets.arraySize = nextCount;

        EditorGUI.indentLevel++;
        for (int p = 0; p < presets.arraySize; p++)
        {
            SerializedProperty monsters = presets.GetArrayElementAtIndex(p).FindPropertyRelative("monsters");
            if (monsters == null) continue;
            EditorGUILayout.PropertyField(monsters, new GUIContent($"프리셋 {p + 1}"), true);
        }
        EditorGUI.indentLevel--;
    }

    void EnsureLapFoldouts(int count)
    {
        if (_lapOpen != null && _lapOpen.Length == count) return;
        var next = new bool[Mathf.Max(0, count)];
        for (int i = 0; i < next.Length; i++)
            next[i] = true;
        _lapOpen = next;
    }

    void EnsureEliteFoldouts(int count)
    {
        if (_eliteOpen != null && _eliteOpen.Length == count) return;
        var next = new bool[Mathf.Max(0, count)];
        for (int i = 0; i < next.Length; i++)
            next[i] = true;
        _eliteOpen = next;
    }
}
