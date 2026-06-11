using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Battle;

namespace Battle.EditorTools
{
    /// <summary>
    /// NewBattleController 커스텀 인스펙터.
    /// 콤보 DB 사용 시 ownedComboIds를 효과 체크박스 + 커맨드 드롭다운으로 편집.
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
            var ownedProp = serializedObject.FindProperty("ownedComboIds");

            // ownedComboIds는 아래에서 커스텀 UI로 그리므로 기본 인스펙터에서 제외
            DrawPropertiesExcluding(serializedObject, "ownedComboIds");

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

            _comboFoldout = EditorGUILayout.Foldout(_comboFoldout, "보유 콤보 선택 (효과 → 커맨드)", true);
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
                    ? "현재: 전체 보유 (선택 안 하면 효과별 대표 커맨드 전체 사용)"
                    : $"현재: {ownedCount}개 효과 선택됨",
                EditorStyles.miniLabel);

            // 전체 선택 / 해제 버튼
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("전체 선택", GUILayout.Width(80)))
            {
                ownedProp.ClearArray();
                foreach (var c in _allCombos)
                {
                    var opts = ComboSkillDatabase.GetCommandOptions(c.refComboId);
                    if (opts.Count > 0) AddId(ownedProp, opts[0].comboId);
                }
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

            // 효과별: [체크박스] 효과 + [드롭다운] 커맨드(슬롯 순서)
            foreach (var combo in _allCombos)
            {
                var opts = ComboSkillDatabase.GetCommandOptions(combo.refComboId);
                if (opts.Count == 0) continue;

                // 이 효과로 현재 보유 중인 커맨드 찾기
                int ownedArrIdx = -1, selIdx = 0;
                for (int oi = 0; oi < opts.Count; oi++)
                {
                    int arrIdx = IndexOfId(ownedProp, opts[oi].comboId);
                    if (arrIdx >= 0) { ownedArrIdx = arrIdx; selIdx = oi; break; }
                }
                bool owned = ownedArrIdx >= 0;

                EditorGUILayout.BeginHorizontal();

                bool newOwned = EditorGUILayout.ToggleLeft(
                    new GUIContent($"[{combo.refComboId}] {combo.displayName}", combo.descriptionKR),
                    owned, GUILayout.Width(170));

                // 커맨드 드롭다운은 항상 그리고 미보유 시 비활성 → 컨트롤 수가 매 프레임 동일(IMGUI 안정)
                var labels = new string[opts.Count];
                for (int li = 0; li < opts.Count; li++) labels[li] = opts[li].label;

                EditorGUI.BeginDisabledGroup(!owned);
                int newSel = EditorGUILayout.Popup(selIdx, labels, GUILayout.Width(120));
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.LabelField(
                    new GUIContent(Shorten(combo.descriptionKR), combo.descriptionKR),
                    EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();

                // 변경은 컨트롤을 모두 그린 뒤 반영
                if (newOwned != owned)
                {
                    if (newOwned) AddId(ownedProp, opts[selIdx].comboId);                  // 체크 → 커맨드 보유(기본=첫)
                    else if (ownedArrIdx >= 0) ownedProp.DeleteArrayElementAtIndex(ownedArrIdx); // 해제 → 제거
                }
                else if (owned && newSel != selIdx)                                        // 커맨드 변경
                {
                    if (ownedArrIdx >= 0) ownedProp.DeleteArrayElementAtIndex(ownedArrIdx);
                    AddId(ownedProp, opts[newSel].comboId);
                }
            }

            // 플레이 중: 체크 변경을 즉시 전투에 반영(다음 전투/각성 기다리지 않고)
            if (Application.isPlaying)
            {
                EditorGUILayout.Space(2);
                if (GUILayout.Button("▶ 지금 적용 (보유 콤보 런타임 갱신)"))
                {
                    serializedObject.ApplyModifiedProperties(); // 체크 변경 먼저 반영
                    (target as NewBattleController)?.RefreshOwnedCombosRuntime();
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

        static int IndexOfId(SerializedProperty arrayProp, int id)
        {
            if (arrayProp == null) return -1;
            for (int i = 0; i < arrayProp.arraySize; i++)
                if (arrayProp.GetArrayElementAtIndex(i).intValue == id) return i;
            return -1;
        }

        static void AddId(SerializedProperty arrayProp, int id)
        {
            if (arrayProp == null) return;
            if (IndexOfId(arrayProp, id) >= 0) return;
            int n = arrayProp.arraySize;
            arrayProp.InsertArrayElementAtIndex(n);
            arrayProp.GetArrayElementAtIndex(n).intValue = id;
        }

        static string Shorten(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\n", " ");
            return s.Length > 40 ? s.Substring(0, 39) + "…" : s;
        }
    }
}
