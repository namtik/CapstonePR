using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AwakeningDurationFlicker))]
[CanEditMultipleObjects]
internal sealed class AwakeningDurationFlickerInspector : Editor
{
    private const string AdvancedState = "Sashindo.AwakeningDurationFlicker.Advanced";
    private static readonly GUIContent AdvancedLabel = new GUIContent("Advanced Settings",
        "Detailed settings for timing, glow, and the brush layout.");

    public override void OnInspectorGUI()
    {
        if (serializedObject.FindProperty("brushLength") == null)
        {
            DrawDefaultInspector();
            return;
        }

        serializedObject.Update();
        Draw("startingFrequency");
        Draw("maximumFrequency");
        Draw("fullSpeedAtPercent");
        Draw("effectIntensity");
        Draw("edgeGlowColor");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Brush Strokes", EditorStyles.boldLabel);
        Draw("brushLength");
        Draw("brushWidth");
        Draw("brushTravel");
        Draw("startingBrushCount");
        Draw("maximumBrushCount");
        Draw("brushColor");

        EditorGUILayout.Space();
        bool wasExpanded = SessionState.GetBool(AdvancedState, false);
        bool expanded = EditorGUILayout.Foldout(wasExpanded, AdvancedLabel, true);
        if (expanded != wasExpanded)
            SessionState.SetBool(AdvancedState, expanded);
        if (expanded)
            DrawAdvanced();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawAdvanced()
    {
        using (new EditorGUI.IndentLevelScope())
        {
            EditorGUILayout.LabelField("Pulse Shape", EditorStyles.boldLabel);
            Draw("pulseRiseFraction");
            Draw("extensionSmoothingRate");

            Draw("startDelay");
            Draw("startFadeSeconds");
            Draw("endFadeSeconds");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Darkness and Sorting", EditorStyles.boldLabel);
            Draw("restingDarkness");
            Draw("peakDarkness");
            Draw("canvasSortingOrder");

            Draw("glowEdges");
            Draw("edgeGlowOpacity");
            Draw("endGlowMultiplier");
            Draw("horizontalGlowReach");
            Draw("verticalGlowReach");
            Draw("glowBetweenPulses");

            Draw("brushEdges");
            Draw("brushVariation");
            Draw("brushSpacing");
            Draw("brushSpawnSpread");
            Draw("mirrorTopBrushDirection");
            Draw("brushTextureDetail");
            Draw("brushVisibleFraction");
            Draw("brushOpacity");
        }
    }

    private void Draw(string propertyName)
    {
        var property = serializedObject.FindProperty(propertyName);
        if (property != null)
            EditorGUILayout.PropertyField(property);
    }
}
