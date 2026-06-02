using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Battle;

namespace Battle.EditorTools
{
    /// <summary>
    /// NewBattleController 커스텀 인스펙터.
    /// 콤보 DB 사용 시 ownedComboRefIds를 숫자 대신 '설명 포함 체크박스 목록'으로 편집.
    /// </summary>
    [CustomEditor(typeof(NewBattleController))]
    public class NewBattleControllerEditor : Editor
    {
        private List<ComboSkillDef> _allCombos; // DB의 canonical 콤보 캐시 (편집기 표시용)
        private bool _comboFoldout = true;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var useDbProp = serializedObject.FindProperty("useComboDatabase");
            var ownedProp = serializedObject.FindProperty("ownedComboRefIds");

            // ownedComboRefIds는 아래에서 커스텀 UI로 그리므로 기본 인스펙터에서 제외
            DrawPropertiesExcluding(serializedObject, "ownedComboRefIds");

            if (useDbProp != null && useDbProp.boolValue)
            {
                EditorGUILayout.Space();
                DrawComboChecklist(ownedProp);
            }

            serializedObject.ApplyModifiedProperties();
        }

        void DrawComboChecklist(SerializedProperty ownedProp)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _comboFoldout = EditorGUILayout.Foldout(_comboFoldout, "보유 콤보 선택 (Owned Combo Ref Ids)", true);
            if (!_comboFoldout) { EditorGUILayout.EndVertical(); return; }

            if (_allCombos == null)
                ReloadCombos();

            if (_allCombos == null || _allCombos.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "콤보 DB를 불러올 수 없습니다.\nTools > Combo DB > Excel → JSON 변환을 먼저 실행하세요.",
                    MessageType.Warning);
                if (GUILayout.Button("다시 불러오기")) ReloadCombos();
                EditorGUILayout.EndVertical();
                return;
            }

            int ownedCount = ownedProp != null ? ownedProp.arraySize : 0;
            EditorGUILayout.LabelField(
                ownedCount == 0
                    ? "현재: 전체 보유 (아무것도 체크 안 하면 모든 콤보 사용)"
                    : $"현재: {ownedCount}종 선택됨",
                EditorStyles.miniLabel);

            // 전체 선택 / 해제 버튼
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("전체 선택", GUILayout.Width(80)))
            {
                ownedProp.ClearArray();
                foreach (var c in _allCombos) AddRefId(ownedProp, c.refComboId);
            }
            if (GUILayout.Button("전체 해제(=전체 보유)", GUILayout.Width(150)))
            {
                ownedProp.ClearArray();
            }
            if (GUILayout.Button("DB 새로고침", GUILayout.Width(100)))
            {
                ReloadCombos();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);

            // 콤보별 체크박스
            foreach (var combo in _allCombos)
            {
                int idx = IndexOfRefId(ownedProp, combo.refComboId);
                bool owned = idx >= 0;

                EditorGUILayout.BeginHorizontal();
                bool newOwned = EditorGUILayout.ToggleLeft(
                    new GUIContent(
                        $"[{combo.refComboId}] {combo.ComboString()}",
                        combo.descriptionKR),
                    owned,
                    GUILayout.Width(150));

                EditorGUILayout.LabelField(
                    new GUIContent(Shorten(combo.descriptionKR), combo.descriptionKR),
                    EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();

                if (newOwned != owned)
                {
                    if (newOwned) AddRefId(ownedProp, combo.refComboId);
                    else if (idx >= 0) ownedProp.DeleteArrayElementAtIndex(idx);
                }
            }

            EditorGUILayout.EndVertical();
        }

        void ReloadCombos()
        {
            // 편집기에서도 Resources.Load는 동작. 재변환 반영 위해 캐시 초기화 후 재로드.
            ComboSkillDatabase.ResetCache();
            _allCombos = ComboSkillDatabase.BuildOwnedCombos(null); // null = 전체(canonical 20종)
        }

        static int IndexOfRefId(SerializedProperty arrayProp, int refId)
        {
            if (arrayProp == null) return -1;
            for (int i = 0; i < arrayProp.arraySize; i++)
                if (arrayProp.GetArrayElementAtIndex(i).intValue == refId) return i;
            return -1;
        }

        static void AddRefId(SerializedProperty arrayProp, int refId)
        {
            if (arrayProp == null) return;
            if (IndexOfRefId(arrayProp, refId) >= 0) return;
            int n = arrayProp.arraySize;
            arrayProp.InsertArrayElementAtIndex(n);
            arrayProp.GetArrayElementAtIndex(n).intValue = refId;
        }

        static string Shorten(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\n", " ");
            return s.Length > 40 ? s.Substring(0, 39) + "…" : s;
        }
    }
}
