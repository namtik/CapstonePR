using UnityEditor;
using UnityEngine;
using Battle;

[CustomEditor(typeof(DeckDebugTool))]
public class DeckDebugToolEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var tool = (DeckDebugTool)target;
        EditorGUILayout.Space();

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "게임 시작 전: 위 '덱 목록'을 채우고 'Use As Starting Deck'를 켠 뒤 ▶ Play 하면 그 덱으로 시작합니다.\n" +
                "아래 버튼들은 플레이 중에 활성화됩니다.",
                MessageType.Info);
        }

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            EditorGUILayout.LabelField("런 덱 (다음 전투 / 시작 덱)", EditorStyles.boldLabel);
            if (GUILayout.Button("현재 런 덱 → 목록으로 불러오기")) tool.LoadFromRunDeck();
            if (GUILayout.Button("목록 → 런 덱 적용 (다음 전투부터)")) tool.ApplyToRunDeck();
            if (GUILayout.Button("런 덱 기본값으로 리셋")) tool.ResetRunDeck();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("현재 전투 (즉시 반영)", EditorStyles.boldLabel);
            if (GUILayout.Button("목록 전부 → 현재 전투 뽑을 더미에 추가")) tool.AddListToCurrentBattle();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("빠른추가 → 패")) tool.AddQuickCardToHand();
            if (GUILayout.Button("빠른추가 → 뽑을 더미")) tool.AddQuickCardToDraw();
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("카드 목록(ID·이름) 콘솔 출력")) tool.DumpCardList();
    }
}
