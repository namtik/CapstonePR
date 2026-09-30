using UnityEditor;
using UnityEngine;

// 스테이지(n-n)마다 마릿수와 몬스터 풀을 바로 보이게 한다.
[CustomEditor(typeof(EnemyEncounterConfig))]
public class EnemyEncounterConfigEditor : Editor
{
    bool[] _lapOpen = System.Array.Empty<bool>();
    bool[] _poolOpen = System.Array.Empty<bool>();

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var cfg = (EnemyEncounterConfig)target;

        EditorGUILayout.HelpBox(
            "일반 전투만 이 에셋을 씁니다. 정예/보스는 항상 1마리입니다.\n" +
            "아래에서 1-1, 1-2처럼 스테이지마다 마리 수와 몬스터 풀을 정하세요.\n" +
            "해당 스테이지의 몬스터 풀이 비어 있으면 폴백 풀에서 뽑습니다.",
            MessageType.Info);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("stagesPerLap"), new GUIContent("바퀴당 스테이지 수"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("fallbackPool"), new GUIContent("폴백 몬스터 풀"), true);

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

        int poolIndex = 0;
        for (int i = 0; i < laps.arraySize; i++)
        {
            SerializedProperty lapProp = laps.GetArrayElementAtIndex(i);
            SerializedProperty lapNumber = lapProp.FindPropertyRelative("lap");
            SerializedProperty stages = lapProp.FindPropertyRelative("stages");
            int lap = Mathf.Max(1, lapNumber.intValue);

            _lapOpen[i] = EditorGUILayout.Foldout(_lapOpen[i], $"{lap}바퀴", true, EditorStyles.foldoutHeader);
            if (!_lapOpen[i])
            {
                poolIndex += stages != null ? stages.arraySize : 0;
                continue;
            }

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(lapNumber, new GUIContent("바퀴 번호"));
            if (stages == null)
            {
                EditorGUI.indentLevel--;
                continue;
            }

            EnsurePoolFoldouts(poolIndex + stages.arraySize);

            for (int s = 0; s < stages.arraySize; s++)
            {
                SerializedProperty stageProp = stages.GetArrayElementAtIndex(s);
                SerializedProperty countProp = stageProp.FindPropertyRelative("enemyCount");
                SerializedProperty dupProp = stageProp.FindPropertyRelative("allowDuplicates");
                SerializedProperty poolProp = stageProp.FindPropertyRelative("monsterPool");

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"{lap}-{s + 1}", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(countProp, new GUIContent("마리 수"));
                EditorGUILayout.PropertyField(dupProp, new GUIContent("같은 몬스터 중복 허용"));

                int poolId = poolIndex + s;
                int poolCount = poolProp != null ? poolProp.arraySize : 0;
                _poolOpen[poolId] = EditorGUILayout.Foldout(
                    _poolOpen[poolId],
                    $"몬스터 풀 ({poolCount})",
                    true);
                if (_poolOpen[poolId] && poolProp != null)
                    EditorGUILayout.PropertyField(poolProp, GUIContent.none, true);

                EditorGUILayout.EndVertical();
            }

            poolIndex += stages.arraySize;
            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
    }

    void EnsureLapFoldouts(int count)
    {
        if (_lapOpen != null && _lapOpen.Length == count) return;
        var next = new bool[Mathf.Max(0, count)];
        for (int i = 0; i < next.Length; i++)
            next[i] = true;
        _lapOpen = next;
    }

    void EnsurePoolFoldouts(int count)
    {
        if (_poolOpen != null && _poolOpen.Length >= count) return;
        var next = new bool[Mathf.Max(count, 0)];
        if (_poolOpen != null)
            System.Array.Copy(_poolOpen, next, _poolOpen.Length);
        _poolOpen = next;
    }
}
