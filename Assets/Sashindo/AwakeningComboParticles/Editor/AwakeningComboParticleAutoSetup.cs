
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Sashindo.VFX.Editor
{
    [InitializeOnLoad]
    internal static class AwakeningComboParticleAutoSetup
    {
        private const string AttemptKey = "Sashindo.ComboParticles.AutoSetup.Wide1";
        private const string Receipt = "Library/Sashindo/ComboParticles.Wide1.applied";
        private const string ShaderPath =
            "Assets/Sashindo/AwakeningComboParticles/Resources/SashindoAwakening/ComboParticle.shader";

        static AwakeningComboParticleAutoSetup()
        {
            if (AssetDatabase.IsAssetImportWorkerProcess()) return;
            EditorApplication.projectChanged += QueueApply;
            QueueApply();
        }

        private static void QueueApply()
        {
            EditorApplication.update -= ApplyWhenReady;
            EditorApplication.update += ApplyWhenReady;
        }

        private static void ApplyWhenReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            EditorApplication.update -= ApplyWhenReady;
            var project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var receipt = Path.Combine(project, Receipt);
            if (File.Exists(receipt) || SessionState.GetBool(AttemptKey, false))
            {
                EditorApplication.projectChanged -= QueueApply;
                return;
            }

            // The package can still be importing its assets when the editor assembly first loads.
            if (AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath) == null)
                return;

            EditorApplication.projectChanged -= QueueApply;
            SessionState.SetBool(AttemptKey, true);
            try
            {
                bool changed = AwakeningComboParticleSetup.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(receipt));
                File.WriteAllText(receipt, "Wide1");
                if (changed)
                    Debug.Log("[Sashindo] 사방비급 3D 파티클 전투 연결을 자동 적용했습니다.");
            }
            catch (Exception error)
            {
                Debug.LogError("[Sashindo] 사방비급 자동 적용을 마치지 못했습니다. " + error.Message);
            }
        }
    }
}
