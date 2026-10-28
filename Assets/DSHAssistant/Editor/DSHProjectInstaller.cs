using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DSH.Assistant.EditorTools
{
    /// <summary>
    /// Ставит ассистента в проект: создаёт настройки и добавляет бумажную записку
    /// в открытую сцену. InstallBatch используется при автоматической установке.
    /// </summary>
    public static class DSHProjectInstaller
    {
        public const string SettingsPath = "Assets/DSHAssistant/DSHAssistantSettings.asset";
        public const string OverlayName = "DSH — бумажный ассистент";

        private const string SettingsFolder = "Assets/DSHAssistant";

        [MenuItem("Tools/DSH Assistant/Установить в открытую сцену", false, 2)]
        public static void InstallIntoOpenScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[DSH Assistant] Нет открытой сцены.");
                return;
            }

            DSHAssistantSettings settings = EnsureSettings();
            GameObject overlay = EnsureOverlay(settings);

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = overlay;
            Debug.Log("[DSH Assistant] Записка \"" + OverlayName + "\" добавлена в сцену \"" + scene.name + "\".");
        }

        /// <summary>Точка входа для Unity -executeMethod. Открывает сцену, ставит объект и сохраняет.</summary>
        public static void InstallBatch()
        {
            DSHAssistantSettings settings = EnsureSettings();
            string scenePath = FindScenePath();
            if (string.IsNullOrEmpty(scenePath))
            {
                Debug.LogError("[DSH Assistant] В проекте не найдена сцена для установки.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            GameObject overlay = EnsureOverlay(settings);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[DSH Assistant] INSTALL_OK scene=" + scenePath +
                      " object=" + overlay.name + " settings=" + SettingsPath);
        }

        private static DSHAssistantSettings EnsureSettings()
        {
            if (!AssetDatabase.IsValidFolder(SettingsFolder))
                AssetDatabase.CreateFolder("Assets", "DSHAssistant");

            DSHAssistantSettings settings =
                AssetDatabase.LoadAssetAtPath<DSHAssistantSettings>(SettingsPath);
            if (settings != null) return settings;

            settings = ScriptableObject.CreateInstance<DSHAssistantSettings>();
            settings.name = "DSHAssistantSettings";
            settings.baseUrl = "http://127.0.0.1:8787/v1";
            settings.model = "deepseek-chat";
            settings.systemPrompt =
                "Ты — DSH, встроенный ИИ-ассистент Unity-проекта «бумага». Отвечай по-русски, кратко и по делу. " +
                "Если просят код — давай готовый C# для Unity 6 (6000.6), без псевдокода. " +
                "Учитывай контекст сцены, если он передан. Не выдумывай имена объектов, которых нет в контексте. " +
                "Помогай с геймплеем, механиками, шейдерами и архитектурой игры про бумагу.";

            AssetDatabase.CreateAsset(settings, SettingsPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        private static GameObject EnsureOverlay(DSHAssistantSettings settings)
        {
            DSHChatOverlay[] overlays = UnityEngine.Object.FindObjectsByType<DSHChatOverlay>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            DSHChatOverlay overlay = overlays.Length > 0 ? overlays[0] : null;
            GameObject go;
            if (overlay == null)
            {
                go = new GameObject(OverlayName);
                Undo.RegisterCreatedObjectUndo(go, "Add DSH Assistant");
                overlay = go.AddComponent<DSHChatOverlay>();
            }
            else
            {
                go = overlay.gameObject;
                go.name = OverlayName;
            }

            // Прямое присваивание + SetDirty надёжнее SerializedObject для
            // компонента, который только что добавлен в сцену.
            overlay.Settings = settings;
            EditorUtility.SetDirty(overlay);

            var serialized = new SerializedObject(overlay);
            SerializedProperty visibleProperty = serialized.FindProperty("visibleOnStart");
            if (visibleProperty != null)
                visibleProperty.boolValue = true;

            serialized.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log("[DSH Assistant] settings_ref=" +
                      (overlay.Settings != null ? overlay.Settings.name : "null"));
            return go;
        }

        private static string FindScenePath()
        {
            string[] guids = AssetDatabase.FindAssets("t:Scene");
            if (guids != null)
            {
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (path.EndsWith("SampleScene.unity", StringComparison.OrdinalIgnoreCase))
                        return path;
                }
            }

            if (EditorBuildSettings.scenes != null && EditorBuildSettings.scenes.Length > 0 &&
                !string.IsNullOrEmpty(EditorBuildSettings.scenes[0].path))
                return EditorBuildSettings.scenes[0].path;

            if (guids != null)
            {
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(path) && path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                        return path;
                }
            }

            return null;
        }
    }
}
