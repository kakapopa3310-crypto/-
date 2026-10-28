// DSH Bridge — окно в редакторе: статус моста, быстрые действия, хвост консоли.

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DshBridge
{
    public class DshBridgeWindow : EditorWindow
    {
        private Vector2 scroll;
        private string menuPath = "Window/General/Console";

        [MenuItem("Tools/DeepSeek Harness/Bridge Window %#d", false, 10)]
        public static void Open()
        {
            var window = GetWindow<DshBridgeWindow>(false, "DSH Bridge", true);
            window.minSize = new Vector2(420, 260);
            window.Show();
        }

        [MenuItem("Tools/DeepSeek Harness/Start Bridge", false, 11)]
        public static void StartBridge()
        {
            try { DshServer.Start(); }
            catch (Exception e) { Debug.LogError("[DSH] " + e.Message); }
        }

        [MenuItem("Tools/DeepSeek Harness/Stop Bridge", false, 12)]
        public static void StopBridge()
        {
            DshServer.Stop();
            Debug.Log("[DSH] Мост остановлен.");
        }

        [MenuItem("Tools/DeepSeek Harness/Restart Bridge", false, 13)]
        public static void RestartBridge()
        {
            try { DshServer.Restart(); }
            catch (Exception e) { Debug.LogError("[DSH] " + e.Message); }
        }

        [MenuItem("Tools/DeepSeek Harness/Copy Bridge URL", false, 14)]
        public static void CopyUrl()
        {
            EditorGUIUtility.systemCopyBuffer = DshServer.IsRunning ? DshServer.Url : "Мост не запущен";
            Debug.Log("[DSH] " + EditorGUIUtility.systemCopyBuffer);
        }

        [MenuItem("Tools/DeepSeek Harness/Capture Screenshot", false, 15)]
        public static void CaptureScreenshot()
        {
            try
            {
                var result = DshCommands.Screenshot(null);
                Debug.Log("[DSH] Скриншот: " + result["path"]);
                EditorUtility.RevealInFinder(Convert.ToString(result["path"]));
            }
            catch (Exception e) { Debug.LogError("[DSH] " + e.Message); }
        }

        [MenuItem("Tools/DeepSeek Harness/Open Project Folder", false, 16)]
        public static void OpenProjectFolder()
        {
            EditorUtility.RevealInFinder(DshPaths.ProjectRoot);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            var status = DshServer.IsRunning ? "● работает" : "○ остановлен";
            var style = new GUIStyle(EditorStyles.boldLabel);
            style.normal.textColor = DshServer.IsRunning ? new Color(0.25f, 0.8f, 0.35f) : new Color(0.8f, 0.45f, 0.3f);
            GUILayout.Label(status, style, GUILayout.Width(110));
            GUILayout.Label(DshServer.IsRunning ? DshServer.Url : "порт не занят");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(DshServer.IsRunning ? "Перезапустить" : "Запустить", GUILayout.Width(120)))
            {
                try
                {
                    if (DshServer.IsRunning) DshServer.Restart();
                    else DshServer.Start();
                }
                catch (Exception e) { Debug.LogError("[DSH] " + e.Message); }
            }
            if (GUILayout.Button("Стоп", GUILayout.Width(60))) DshServer.Stop();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Проект:", GUILayout.Width(56));
            EditorGUILayout.SelectableLabel(DshPaths.ProjectRoot, EditorStyles.miniLabel, GUILayout.Height(16));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Unity:", GUILayout.Width(56));
            GUILayout.Label(Application.unityVersion + "  •  " + EditorUserBuildSettings.activeBuildTarget + "  •  " + (EditorApplication.isPlaying ? "Play Mode" : "Edit Mode"), EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Копировать URL", GUILayout.Width(120)))
            {
                EditorGUIUtility.systemCopyBuffer = DshServer.Url;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Команды из CLI", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Tools/dsh-unity.ps1 ping\n" +
                "Tools/dsh-unity.ps1 state\n" +
                "Tools/dsh-unity.ps1 console -Count 30 -Type error\n" +
                "Tools/dsh-unity.ps1 exec -Arg DshScripts.Examples.Ping -Params \"привет\"\n" +
                "Tools/dsh-unity.ps1 screenshot",
                MessageType.None);

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            menuPath = EditorGUILayout.TextField("Меню", menuPath);
            if (GUILayout.Button("Выполнить", GUILayout.Width(90)))
            {
                try
                {
                    EditorApplication.ExecuteMenuItem(menuPath);
                }
                catch (Exception e) { Debug.LogError("[DSH] " + e.Message); }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Консоль (" + DshLog.ErrorCount + " ошибок, " + DshLog.WarningCount + " предупреждений)", EditorStyles.boldLabel);

            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(80));
            var entries = DshLog.Snapshot(120, "all");
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                string type = Convert.ToString(entry["type"]);
                var entryStyle = new GUIStyle(EditorStyles.miniLabel);
                entryStyle.wordWrap = true;
                if (type == "error" || type == "exception" || type == "assert") entryStyle.normal.textColor = new Color(0.95f, 0.45f, 0.4f);
                else if (type == "warning") entryStyle.normal.textColor = new Color(0.95f, 0.8f, 0.35f);

                GUILayout.Label(Convert.ToString(entry["time"]) + "  " + Convert.ToString(entry["message"]), entryStyle);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Очистить лог", GUILayout.Width(120))) DshLog.Clear();
            if (GUILayout.Button("Консоль Unity", GUILayout.Width(120))) EditorApplication.ExecuteMenuItem("Window/General/Console");
            GUILayout.FlexibleSpace();
            DshServer.AutoStart = EditorGUILayout.ToggleLeft("Автозапуск при открытии проекта", DshServer.AutoStart, GUILayout.Width(260));
            EditorGUILayout.EndHorizontal();
        }

        private void OnInspectorUpdate()
        {
            if (DshServer.IsRunning) Repaint();
        }
    }
}
