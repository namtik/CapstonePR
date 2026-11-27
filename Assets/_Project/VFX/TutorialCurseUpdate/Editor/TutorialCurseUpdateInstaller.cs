using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Sashindo.Editor
{
    internal static class TutorialCurseUpdateInstaller
    {
        internal const string MainScenePath = "Assets/_Project/Scenes/MainScene.unity";
        internal const string PackageRoot = "Assets/_Project/VFX/TutorialCurseUpdate";
        internal const string CanvasPrefabPath = PackageRoot + "/TutorialCanvas.prefab";
        internal const string ButtonPrefabPath = PackageRoot + "/TutorialButton.prefab";
        private const string AutoInstallReceipt = "Library/SashindoTutorialCurseAutoApply.v1";

        [InitializeOnLoadMethod]
        private static void ScheduleAutomaticInstall()
        {
            if (!File.Exists(AutoInstallReceipt))
                EditorApplication.update += ApplyAfterImport;
        }

        private static void ApplyAfterImport()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            EditorApplication.update -= ApplyAfterImport;
            RunInstall(false);
        }

        [MenuItem("Tools/Sashindo/Apply Tutorial and Curse Update")]
        private static void ApplyFromMenu()
        {
            RunInstall(true);
        }

        private static void RunInstall(bool showDialog)
        {
            try
            {
                bool changed = Apply(out bool saved);
                if (saved)
                    WriteAutoInstallReceipt();
                else
                {
                    EditorSceneManager.sceneSaved -= OnPendingSceneSaved;
                    EditorSceneManager.sceneSaved += OnPendingSceneSaved;
                }

                string message = !changed
                    ? "튜토리얼이 이미 MainScene에 적용되어 있습니다."
                    : saved
                        ? "MainScene에 튜토리얼을 적용하고 저장했습니다."
                        : "열려 있는 MainScene에 튜토리얼을 적용했습니다. 기존 미저장 편집 내용과 함께 씬을 저장하면 적용이 완료됩니다.";
                Debug.Log("[사신도 업데이트] " + message);
                if (showDialog)
                    EditorUtility.DisplayDialog("사신도 업데이트", message, "확인");
            }
            catch (Exception exception)
            {
                Debug.LogError("[사신도 업데이트] " + exception.Message);
                if (showDialog)
                    EditorUtility.DisplayDialog("사신도 업데이트", exception.Message, "확인");
            }
        }

        private static void OnPendingSceneSaved(Scene scene)
        {
            if (scene.path != MainScenePath)
                return;

            var menu = GetMainMenu(scene);
            var canvasPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPrefabPath);
            var buttonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPrefabPath);
            if (menu == null || canvasPrefab == null || buttonPrefab == null ||
                !IsInstanceOf(menu.transform.Find("TutorialCanvas"), canvasPrefab) ||
                !IsInstanceOf(menu.transform.Find("ButtonGroup/TutorialButton"), buttonPrefab))
                return;

            EditorSceneManager.sceneSaved -= OnPendingSceneSaved;
            WriteAutoInstallReceipt();
        }

        private static void WriteAutoInstallReceipt()
        {
            File.WriteAllText(AutoInstallReceipt, "Tutorial and curse update applied.\n");
        }

        private static GameObject GetMainMenu(Scene scene)
        {
            return scene.GetRootGameObjects().SingleOrDefault(root => root.GetComponent<MainMenuController>() != null);
        }

        private static bool Apply(out bool saved)
        {
            saved = false;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("플레이 모드를 종료한 뒤 다시 실행해 주세요.");

            var canvasPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPrefabPath);
            var buttonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPrefabPath);
            if (canvasPrefab == null || canvasPrefab.GetComponent<TutorialPanelController>() == null ||
                buttonPrefab == null || buttonPrefab.GetComponent<Button>() == null)
                throw new InvalidOperationException("패키지에서 튜토리얼 프리팹을 찾지 못했습니다.");

            var scene = SceneManager.GetSceneByPath(MainScenePath);
            bool openedForInstall = !scene.isLoaded;
            bool hadUnsavedEdits = !openedForInstall && scene.isDirty;
            if (openedForInstall)
                scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Additive);

            var menu = GetMainMenu(scene);
            var group = menu != null ? menu.transform.Find("ButtonGroup") as RectTransform : null;
            var play = menu != null ? menu.transform.Find("PlayButton") as RectTransform : null;
            var layout = group != null ? group.GetComponent<VerticalLayoutGroup>() : null;
            if (menu == null || group == null || play == null || layout == null)
                throw new InvalidOperationException("MainScene에서 메인 메뉴의 ButtonGroup과 PlayButton을 찾지 못했습니다.");

            var existingCanvas = menu.transform.Find("TutorialCanvas");
            var existingButton = group.Find("TutorialButton");
            if (IsInstanceOf(existingCanvas, canvasPrefab) && IsInstanceOf(existingButton, buttonPrefab))
            {
                saved = !hadUnsavedEdits;
                if (openedForInstall)
                    EditorSceneManager.CloseScene(scene, true);
                return false;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply Sashindo Tutorial Update");

            try
            {
                if (existingCanvas != null)
                    Undo.DestroyObjectImmediate(existingCanvas.gameObject);
                if (existingButton != null)
                    Undo.DestroyObjectImmediate(existingButton.gameObject);

                var canvas = Instantiate(canvasPrefab, menu.transform);
                canvas.transform.SetAsLastSibling();
                var controller = canvas.GetComponent<TutorialPanelController>();
                var trigger = Instantiate(buttonPrefab, group);
                trigger.transform.SetAsFirstSibling();
                var triggerButton = trigger.GetComponent<Button>();
                Undo.RecordObject(triggerButton, "Connect Tutorial Button");
                UnityEventTools.AddPersistentListener(triggerButton.onClick, controller.Open);
                PrefabUtility.RecordPrefabInstancePropertyModifications(triggerButton);

                Undo.RecordObjects(new UnityEngine.Object[] { play, group, layout }, "Position Main Menu Buttons");
                play.anchorMin = play.anchorMax = new Vector2(0.5f, 1f);
                play.anchoredPosition = new Vector2(0f, -622f);
                group.anchoredPosition = new Vector2(0f, -234f);
                layout.spacing = 12f;
                PrefabUtility.RecordPrefabInstancePropertyModifications(play);
                PrefabUtility.RecordPrefabInstancePropertyModifications(group);
                PrefabUtility.RecordPrefabInstancePropertyModifications(layout);

                EditorSceneManager.MarkSceneDirty(scene);
                if (!hadUnsavedEdits)
                {
                    if (!EditorSceneManager.SaveScene(scene))
                        throw new InvalidOperationException("MainScene을 저장하지 못했습니다.");
                    saved = true;
                }
                Undo.CollapseUndoOperations(undoGroup);
            }
            catch
            {
                Undo.RevertAllDownToGroup(undoGroup);
                throw;
            }

            if (openedForInstall)
                EditorSceneManager.CloseScene(scene, true);
            return true;
        }

        private static bool IsInstanceOf(Transform instance, GameObject prefab)
        {
            return instance != null && PrefabUtility.GetCorrespondingObjectFromSource(instance.gameObject) == prefab;
        }

        private static GameObject Instantiate(GameObject prefab, Transform parent)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            Undo.RegisterCreatedObjectUndo(instance, "Install " + prefab.name);
            return instance;
        }
    }
}
