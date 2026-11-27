using Sashindo.VFX;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GuardShieldEffect))]
[CanEditMultipleObjects]
internal sealed class GuardShieldEffectEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("방어 이펙트 색상과 불투명도", EditorStyles.boldLabel);
        Draw("talismanColor", "부적 색상", "바운스하는 부적의 빛 색상이에요.");
        DrawOpacity("talismanOpacity", "부적 불투명도 (%)", guard => guard.TalismanOpacity);
        Draw("shieldColor", "방패 색상", "바깥으로 펼쳐지는 방패의 빛 색상이에요.");
        DrawOpacity("shieldOpacity", "방패 불투명도 (%)", guard => guard.ShieldOpacity);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("부적 모션", EditorStyles.boldLabel);
        Draw("talismanPlaybackSpeed", "부적 재생 속도 (배)", "1은 기본 속도, 2는 두 배 속도예요. 방패는 부적이 튀는 순간 펼쳐져요. 변경값은 다음 재생부터 적용돼요.");
        Draw("talismanBounceStrength", "부적 바운스 강도 (%)", "부적이 눌렸다 튀는 높이와 늘어나는 정도예요. 0%는 바운스 없음, 40%는 기본, 100%는 이전 강도예요.");
        EditorGUILayout.Space();
        var speedMode = serializedObject.FindProperty("useSpeedSettings");
        EditorGUILayout.LabelField("방패 속도", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(speedMode, new GUIContent("기본 속도와 가속도 사용"));
        EditorGUI.BeginChangeCheck();
        Draw("baseSpeed", "기본 속도", "출발 순간의 속도. 1은 1초에 전체 경로를 이동하는 속도예요.");
        Draw("accelerationRate", "가속도", "매초 기본 속도에 더해지는 양. 0이면 처음부터 끝까지 같은 속도예요.");
        if (EditorGUI.EndChangeCheck())
            speedMode.boolValue = true;

        if (!speedMode.hasMultipleDifferentValues && !speedMode.boolValue)
        {
            EditorGUILayout.HelpBox("기존 곡선 사용 중이에요. 기본 속도나 가속도 값을 바꾸면 새 방식이 켜져요.", MessageType.Info);
            Draw("expansionDuration", "기존 펼침 시간 (초)", "이전 방식으로 화면을 덮기까지 걸리는 시간.");
            Draw("acceleration", "기존 가속 곡선", "이전 방식에서 1은 일정한 속도예요.");
        }
        Draw("fadeDuration", "사라지는 시간 (초)", "방패가 커지는 동안 투명해지는 시간. 최대 펼침 시간의 60%를 사용해요.");
        Draw("playOnEnable", "활성화 시 자동 재생", "전투용 방패는 방어도 획득 시 재생돼요.");

        serializedObject.ApplyModifiedProperties();

        if (targets.Length != 1)
            return;

        var effect = (GuardShieldEffect)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("방패가 펼쳐지기 시작하는 시간", $"{effect.ShieldStartDelay:0.###}초");
        EditorGUILayout.LabelField("예상 펼침 시간", $"{effect.ExpansionTime:0.###}초");
        EditorGUILayout.LabelField("실제 페이드 시간", $"{effect.EffectiveFadeTime:0.###}초");
        using (new EditorGUI.DisabledScope(!Application.isPlaying || !effect.isActiveAndEnabled
                   || EditorUtility.IsPersistent(effect)))
        {
            if (GUILayout.Button("방패 다시 재생"))
                effect.Play();
        }
        if (Application.isPlaying && EditorUtility.IsPersistent(effect))
            EditorGUILayout.LabelField("전투용 프리팹 값은 다음 방어도 획득부터 반영돼요.", EditorStyles.wordWrappedMiniLabel);
        else if (!Application.isPlaying)
            EditorGUILayout.LabelField("Play 모드에서 Hierarchy의 방패를 선택하면 다시 재생할 수 있어요.",
                EditorStyles.wordWrappedMiniLabel);
    }

    private void Draw(string property, string label, string tooltip)
    {
        EditorGUILayout.PropertyField(serializedObject.FindProperty(property), new GUIContent(label, tooltip));
    }

    private void DrawOpacity(string propertyName, string labelText, System.Func<GuardShieldEffect, float> readOpacity)
    {
        var property = serializedObject.FindProperty(propertyName);
        var label = new GUIContent(labelText, "0%는 완전 투명, 100%는 안쪽까지 불투명해요. 등장과 페이드에는 서서히 변해요.");
        var rect = EditorGUILayout.GetControlRect();
        label = EditorGUI.BeginProperty(rect, label, property);
        bool previousMixed = EditorGUI.showMixedValue;
        float value = readOpacity((GuardShieldEffect)target);
        foreach (GuardShieldEffect effect in targets)
        {
            if (!Mathf.Approximately(value, readOpacity(effect)))
            {
                EditorGUI.showMixedValue = true;
                break;
            }
        }
        EditorGUI.BeginChangeCheck();
        float percent = EditorGUI.Slider(rect, label, value * 100f, 0f, 100f);
        if (EditorGUI.EndChangeCheck())
            property.floatValue = percent * 0.01f;
        EditorGUI.showMixedValue = previousMixed;
        EditorGUI.EndProperty();
    }
}
