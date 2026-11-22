using UnityEditor;
using UnityEngine;

namespace Sashindo.VFX
{
    [CustomEditor(typeof(AwakeningVfx))]
    [CanEditMultipleObjects]
    internal sealed class AwakeningVfxEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Play VFX"))
                        foreach (Object selection in targets)
                            ((AwakeningVfx)selection).Play();

                    if (GUILayout.Button("Stop VFX"))
                        foreach (Object selection in targets)
                            ((AwakeningVfx)selection).Stop();
                }
            }

            if (!Application.isPlaying)
                EditorGUILayout.HelpBox("Play 모드에서 Play VFX를 눌러 연출을 확인할 수 있습니다.", MessageType.Info);
        }
    }
}
