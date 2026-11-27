using Battle.UI;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BattleLogView))]
public class BattleLogViewEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("플레이 중에 탭 패널을 연 뒤 버튼으로 콤보 배치를 미리 볼 수 있습니다.", MessageType.Info);
            GUI.enabled = false;
        }

        BattleLogView view = (BattleLogView)target;
        string maxLabel = BattleLogCombatPopup.IsMaxComboPreviewOpen ? "콤보 최대 미리보기 닫기" : "콤보 최대 개수 띄우기";
        if (GUILayout.Button(maxLabel, GUILayout.Height(28f)))
            view.ToggleMaxComboPreview();
        string overflowLabel = BattleLogCombatPopup.IsOverflowComboPreviewOpen ? "20개 초과 미리보기 닫기" : "콤보 20개 초과 미리보기";
        if (GUILayout.Button(overflowLabel, GUILayout.Height(28f)))
            view.ToggleOverflowComboPreview();
        GUI.enabled = true;
    }
}
