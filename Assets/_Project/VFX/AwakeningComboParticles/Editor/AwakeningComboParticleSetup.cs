using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Sashindo.VFX.Editor
{
    internal static class AwakeningComboParticleSetup
    {
        private const string Menu = "Tools/Sashindo/사방비급 파티클 적용";
        private const string ControllerPath = "Assets/_Project/Scripts/Battle/NewBattleController.cs";

        [MenuItem(Menu, true)]
        private static bool CanApply() => !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;

        [MenuItem(Menu)]
        private static void ApplyMenu()
        {
            try
            {
                if (Apply())
                    Debug.Log("사방비급 3D 파티클을 적용했습니다. 스크립트 컴파일이 끝나면 전투에서 확인할 수 있습니다.");
                else
                    Debug.Log("사방비급 3D 파티클이 이미 적용되어 있습니다.");
            }
            catch (Exception error)
            {
                Debug.LogError("사방비급 적용을 중단했습니다. " + error.Message);
                EditorUtility.DisplayDialog("사방비급 파티클 적용", error.Message, "확인");
            }
        }

        internal static bool Apply()
        {
            if (!CanApply()) throw new InvalidOperationException("Play 모드를 끄고 컴파일이 끝난 뒤 적용해 주세요.");
            if (Resources.Load<Shader>("SashindoAwakening/ComboParticle") == null)
                throw new InvalidOperationException("패키지의 ComboParticle 셰이더를 함께 가져와 주세요.");

            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ControllerPath));
            string oldSource = File.ReadAllText(path);
            string source = AddParticleHooks(oldSource);
            if (source == oldSource) return false;

            string staging = path + ".combo-particles.tmp";
            if (File.Exists(staging))
                throw new IOException("이전 적용에서 남은 임시 파일이 있습니다. " + staging);
            try
            {
                File.WriteAllText(staging, source);
                File.Replace(staging, path, null);
            }
            finally
            {
                if (File.Exists(staging)) File.Delete(staging);
            }
            AssetDatabase.ImportAsset(ControllerPath);
            return true;
        }

        internal static string AddParticleHooks(string oldSource)
        {
            if (oldSource.Contains("Sashindo.VFX.AwakeningComboParticle.Spawn("))
            {
                const string previous = "awakenMarkerVerticalSquash, awakenMarkerCenterOffset, awakenComboParticleScale);";
                if (!oldSource.Contains(previous)) return oldSource;
                string lineEnding = oldSource.Contains("\r\n") ? "\r\n" : "\n";
                return ReplaceOnce(oldSource, previous,
                    "awakenMarkerVerticalSquash, awakenMarkerCenterOffset, awakenComboParticleScale," + lineEnding +
                    "                handHud != null ? handHud.AwakenMarkerLayer : null);");
            }
            string newline = oldSource.Contains("\r\n") ? "\r\n" : "\n";
            string source = oldSource.Replace("\r\n", "\n");
            source = ReplaceOnce(source, "        [Tooltip(\"각성 종료 후 표식이 하나씩 터지는 간격(초).\")]",
                "        [Tooltip(\"사방비급 성립 시 몬스터 주변에 맺히는 금빛 파티클의 크기.\")]\n        [SerializeField, Min(.1f)] private float awakenComboParticleScale = 1.32f;\n        [Tooltip(\"각성 종료 후 표식이 하나씩 터지는 간격(초).\")]");
            source = ReplaceOnce(source, "        private Coroutine _awakenExplosionRoutine;",
                "        private readonly List<GameObject> _awakenDetonatingParticles = new List<GameObject>();\n        private Coroutine _awakenExplosionRoutine;");
            source = ReplaceOnce(source, "        void OnDestroy()\n        {",
                "        void OnDestroy()\n        {\n            ClearAwakenComboParticles();");
            source = ReplaceOnce(source, "        public void StartBattle()\n        {",
                "        public void StartBattle()\n        {\n            ClearAwakenComboParticles();");
            source = ReplaceOnce(source, "            if (_awakenExplosionRoutine != null) { StopCoroutine(_awakenExplosionRoutine); _awakenExplosionRoutine = null; }\n            _awakenMarkerPositions.Clear();\n            _awakenMarkerHandles.Clear();",
                "            ClearAwakenComboParticles();");
            source = ReplaceOnce(source, "                _awakenMarkerPositions.Clear();\n                _awakenMarkerHandles.Clear();\n\n                if (handHud != null)",
                "                _awakenMarkerPositions.Clear();\n                RemoveAwakenComboParticles(_awakenMarkerHandles);\n\n                if (handHud != null)");
            source = ReplaceOnce(source, "                    if (_awakenExplosionRoutine != null) StopCoroutine(_awakenExplosionRoutine);\n                    _awakenExplosionRoutine = StartCoroutine(ExplodeMarkersSequentially(skills, positions, handles, damageCount));",
                "                    CancelAwakenComboBurst();\n                    _awakenDetonatingParticles.AddRange(handles);\n                    _awakenExplosionRoutine = StartCoroutine(ExplodeMarkersSequentially(skills, positions, handles, damageCount));");
            source = ReplaceOnce(source, "            if (!awakenMarkerSequence || effectOverlay == null) return;\n            Vector2 pos = ComputeAwakenMarkerPosition(_awakenMarkerPositions.Count);\n            // 표식은 딤 바로 위 레이어(다른 UI 아래)에 생성 — 레이어 없으면 effectOverlay 기본 위치로 폴백\n            RectTransform layer = handHud != null ? handHud.AwakenMarkerLayer : null;\n            GameObject handle = effectOverlay.SpawnAwakenMarker(pos, layer);\n            _awakenMarkerPositions.Add(pos);\n            _awakenMarkerHandles.Add(handle);\n        }",
                "            if (!awakenMarkerSequence) return;\n            int index = _awakenMarkerHandles.Count;\n            float radius = awakenMarkerRadius + index * awakenMarkerRadiusGrowth;\n            if (awakenMarkerMaxRadius > 0f) radius = Mathf.Min(radius, awakenMarkerMaxRadius);\n\n            var handle = Sashindo.VFX.AwakeningComboParticle.Spawn(transform, _enemy, index, radius,\n                awakenMarkerVerticalSquash, awakenMarkerCenterOffset, awakenComboParticleScale,\n                handHud != null ? handHud.AwakenMarkerLayer : null);\n            _awakenMarkerPositions.Add(ComputeAwakenMarkerPosition(index));\n            _awakenMarkerHandles.Add(handle);\n        }\n\n        void ClearAwakenComboParticles()\n        {\n            CancelAwakenComboBurst();\n            _awakenMarkerPositions.Clear();\n            RemoveAwakenComboParticles(_awakenMarkerHandles);\n        }\n\n        void CancelAwakenComboBurst()\n        {\n            if (_awakenExplosionRoutine != null)\n            {\n                StopCoroutine(_awakenExplosionRoutine);\n                _awakenExplosionRoutine = null;\n            }\n            RemoveAwakenComboParticles(_awakenDetonatingParticles);\n        }\n\n        void RemoveAwakenComboParticles(List<GameObject> handles)\n        {\n            foreach (var handle in handles)\n                if (handle != null) Destroy(handle);\n            handles.Clear();\n        }");
            source = ReplaceOnce(source, "                var skill = skills[i];\n                if (skill == null) continue;\n\n                Vector2 pos = i < positions.Count\n                    ? positions[i]\n                    : (effectOverlay != null ? effectOverlay.EnemyAnchoredPos : Vector2.zero);\n\n                // 표식 제거 + 그 자리에서 폭발 이펙트\n                if (i < handles.Count && effectOverlay != null) effectOverlay.DestroyAwakenMarker(handles[i]);",
                "                var skill = skills[i];\n                Vector2 pos = i < positions.Count\n                    ? positions[i]\n                    : (effectOverlay != null ? effectOverlay.EnemyAnchoredPos : Vector2.zero);\n\n                if (i < handles.Count && handles[i] != null)\n                {\n                    var handle = handles[i];\n                    if (effectOverlay != null && handle.TryGetComponent<Sashindo.VFX.AwakeningComboParticle>(out var marker)\n                        && marker.TryGetBurstScreenPosition(out var screen)\n                        && effectOverlay.TryAnchoredFromScreen(screen, out var at))\n                        pos = at;\n\n                    handles[i] = null;\n                    Destroy(handle);\n                }\n\n                if (skill == null) continue;");
            source = ReplaceOnce(source, "            ApplyAwakenComboGaugeRecovery(skills.Count);\n            _awakenExplosionRoutine = null;",
                "            RemoveAwakenComboParticles(_awakenDetonatingParticles);\n            ApplyAwakenComboGaugeRecovery(skills.Count);\n            _awakenExplosionRoutine = null;");
            return source.Replace("\n", newline);
        }

        private static string ReplaceOnce(string source, string before, string after)
        {
            int match = source.IndexOf(before, StringComparison.Ordinal);
            if (match < 0 || source.IndexOf(before, match + before.Length, StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException("현재 NewBattleController에서 적용할 위치를 확인할 수 없습니다. " +
                    before.TrimStart().Split('\n')[0]);
            return source.Remove(match, before.Length).Insert(match, after);
        }
    }
}
