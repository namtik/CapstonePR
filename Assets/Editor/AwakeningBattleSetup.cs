using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Battle;
using Battle.UI;

namespace Sashindo.VFX.Editor
{
    internal static class AwakeningBattleSetup
    {
        private const string ApplyMenu = "Tools/Sashindo/각성 연출 적용";
        private const string EntryName = "Sashindo_AwakeningVFX";
        private const string EntryPath =
            "Assets/Sashindo/AwakeningVFX/Prefabs/Sashindo_AwakeningVFX.prefab";
        private const string FlickerPath = "Assets/Resources/VFX/AwakeningDurationFlicker.prefab";
        private const string DimShaderPath = "Assets/Resources/UI/AwakenRadialDim.shader";

        [MenuItem(ApplyMenu, true)]
        private static bool CanApply() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem(ApplyMenu)]
        private static void Apply()
        {
            var selected = Selection.activeGameObject;
            var controller = selected != null ? selected.GetComponent<NewBattleController>() : null;
            Scene scene = controller != null && controller.gameObject.scene.IsValid()
                ? controller.gameObject.scene : SceneManager.GetActiveScene();

            if (!scene.IsValid() || !scene.isLoaded)
            {
                Explain("전투 씬을 연 뒤 다시 적용해 주세요.");
                return;
            }

            if (controller == null || controller.gameObject.scene != scene)
                controller = FindSingle<NewBattleController>(scene);

            if (controller == null)
            {
                Explain("전투 씬에서 적용할 NewBattleController를 선택해 주세요.");
                return;
            }

            var entryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EntryPath);
            var flickerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FlickerPath);
            var entryPreset = entryPrefab != null ? entryPrefab.GetComponent<AwakeningVfx>() : null;
            if (entryPreset == null || flickerPrefab == null || flickerPrefab.GetComponent<AwakeningDurationFlicker>() == null
                || AssetDatabase.LoadAssetAtPath<Shader>(DimShaderPath) == null)
            {
                Explain("각성 패키지의 프리팹과 셰이더를 모두 가져와 주세요.");
                return;
            }

            var battleSettings = new SerializedObject(controller);
            var entrySlot = battleSettings.FindProperty("awakenEntryVfx");
            var flickerSlot = battleSettings.FindProperty("awakenScreenEffectPrefab");
            if (entrySlot == null || flickerSlot == null)
            {
                Explain("각성 진입 연동이 포함된 NewBattleController.cs를 가져온 뒤 다시 적용해 주세요.");
                return;
            }

            var hudSlot = battleSettings.FindProperty("handHud");
            var hud = hudSlot?.objectReferenceValue as CardHandHUD;
            if (hud == null)
                hud = FindSingle<CardHandHUD>(scene);
            if (hudSlot == null || hud == null)
            {
                Explain("NewBattleController의 Hand Hud 슬롯에 전투 HUD를 연결해 주세요.");
                return;
            }

            var hudSettings = new SerializedObject(hud);
            var dimColor = hudSettings.FindProperty("awakenDimColor");
            var dimRadius = hudSettings.FindProperty("awakenDimBrightRadius");
            if (dimColor == null || dimRadius == null)
            {
                Explain("현재 CardHandHUD에서 각성 화면 딤 설정을 찾을 수 없습니다.");
                return;
            }

            var entry = entrySlot.objectReferenceValue as AwakeningVfx;
            if (entry == null || entry.gameObject.scene != scene)
                entry = FindNamedEntry(scene);
            if (entry != null && !entry.isActiveAndEnabled)
            {
                Explain("씬에 있는 Sashindo_AwakeningVFX 오브젝트와 컴포넌트를 활성화한 뒤 다시 적용해 주세요.");
                Selection.activeGameObject = entry.gameObject;
                return;
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply Sashindo Awakening Effects");
            if (entry == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(entryPrefab, scene);
                Undo.RegisterCreatedObjectUndo(instance, "Add Awakening Entry Effect");
                entry = instance.GetComponent<AwakeningVfx>();
            }

            ApplyEntryPreset(entryPreset, entry);
            entrySlot.objectReferenceValue = entry;
            flickerSlot.objectReferenceValue = flickerPrefab;
            hudSlot.objectReferenceValue = hud;
            battleSettings.ApplyModifiedProperties();
            dimColor.colorValue = new Color(0f, 0f, 0f, .35f);
            dimRadius.floatValue = 0f;
            hudSettings.ApplyModifiedProperties();

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = controller.gameObject;
            Debug.Log("각성 진입 연출, 지속 깜빡임, 전체 화면 딤을 적용했습니다. 씬을 저장해 주세요.", controller);
        }

        private static AwakeningVfx FindNamedEntry(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == EntryName && root.TryGetComponent(out AwakeningVfx entry))
                    return entry;
            return null;
        }

        private static T FindSingle<T>(Scene scene) where T : Component
        {
            T result = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var candidate in root.GetComponentsInChildren<T>(true))
                {
                    if (result != null)
                        return null;
                    result = candidate;
                }
            }
            return result;
        }

        private static void ApplyEntryPreset(AwakeningVfx source, AwakeningVfx target)
        {
            var from = new SerializedObject(source);
            var to = new SerializedObject(target);
            var property = from.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                // Keep references to scene objects such as the optional ground anchor.
                if (property.propertyPath == "m_Script" || property.propertyPath == "groundAnchor")
                    continue;
                to.CopyFromSerializedProperty(property);
            }
            to.ApplyModifiedProperties();
        }

        private static void Explain(string message)
        {
            EditorUtility.DisplayDialog("각성 연출 적용", message, "확인");
        }
    }
}
