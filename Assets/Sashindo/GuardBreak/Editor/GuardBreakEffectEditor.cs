using Sashindo.VFX;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GuardBreakEffect))]
[CanEditMultipleObjects]
internal sealed class GuardBreakEffectEditor : Editor
{
    private static readonly string[] Settings =
    {
        "fillColor", "fillOpacity", "crackColor",
        "fragmentCount", "sizeVariation", "spread", "rotation", "fall", "seed", "randomizeOnReplay", "impactPoint",
        "fadeInTime", "holdTime", "crackTime", "scatterTime", "playOnEnable"
    };

    private double previewStart;
    private bool previewing;
    private float previewTime;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        foreach (string name in Settings)
            EditorGUILayout.PropertyField(serializedObject.FindProperty(name));
        serializedObject.ApplyModifiedProperties();

        if (targets.Length != 1)
            return;

        var effect = (GuardBreakEffect)target;
        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(EditorUtility.IsPersistent(effect) || !effect.isActiveAndEnabled))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("파괴 미리보기"))
                {
                    if (Application.isPlaying)
                        effect.Play();
                    else
                        StartPreview(effect);
                }
                if (GUILayout.Button("미리보기 닫기"))
                {
                    EndPreview();
                    effect.Stop();
                    RefreshViews();
                }
            }

            if (!Application.isPlaying)
            {
                EditorGUI.BeginChangeCheck();
                previewTime = EditorGUILayout.Slider("장면 확인 (초)", previewTime, 0f, effect.Duration);
                if (EditorGUI.EndChangeCheck())
                {
                    EndPreview();
                    effect.Sample(previewTime);
                    RefreshViews();
                }
            }
        }

        if (EditorUtility.IsPersistent(effect))
            EditorGUILayout.HelpBox("프리팹을 씬에 배치한 뒤 Fullscreen Fracture에서 미리보기 버튼을 누르세요.", MessageType.Info);
    }

    private void StartPreview(GuardBreakEffect effect)
    {
        EndPreview();
        effect.PreparePlayback();
        previewStart = EditorApplication.timeSinceStartup;
        previewing = true;
        EditorApplication.update += AdvancePreview;
    }

    private void AdvancePreview()
    {
        if (target is not GuardBreakEffect effect || !effect || !effect.isActiveAndEnabled || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EndPreview();
            return;
        }

        previewTime = (float)(EditorApplication.timeSinceStartup - previewStart);
        effect.Sample(previewTime);
        RefreshViews();
        Repaint();
        if (previewTime >= effect.Duration)
            EndPreview();
    }

    private void EndPreview()
    {
        if (!previewing)
            return;
        previewing = false;
        EditorApplication.update -= AdvancePreview;
    }

    private static void RefreshViews()
    {
        Canvas.ForceUpdateCanvases();
        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
    }

    private void OnDisable()
    {
        EndPreview();
        if (!Application.isPlaying && target is GuardBreakEffect effect && effect)
        {
            effect.Stop();
            RefreshViews();
        }
    }
}
