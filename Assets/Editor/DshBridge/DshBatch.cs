// DSH Bridge — входные точки для Unity в batchmode:
//   Unity.exe -batchmode -quit -projectPath <проект> -executeMethod DshBridge.DshBatch.Report
//   Unity.exe -batchmode -quit -projectPath <проект> -executeMethod DshBridge.DshBatch.Run -dshCmd <файл.json> -dshOut <результат.json>
//
// Результат всегда пишется в JSON-файл: stdout в batchmode неудобен.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DshBridge
{
    public static class DshBatch
    {
        /// <summary>Пинг: проект, версия Unity, состояние, счётчики лога.</summary>
        public static void Ping()
        {
            WriteEnvelope("ping", DshCommands.Ping);
        }

        /// <summary>Состояние: сцена, сборка, выделение, последняя ошибка.</summary>
        public static void State()
        {
            WriteEnvelope("state", DshCommands.State);
        }

        /// <summary>Принудительный реимпорт и запрос компиляции скриптов.</summary>
        public static void Compile()
        {
            var started = Stopwatch.StartNew();

            var compiled = new Dictionary<string, object>();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();

            compiled["requested"] = true;
            compiled["failed"] = EditorUtility.scriptCompilationFailed;
            compiled["errors"] = DshLog.ErrorCount;
            compiled["warnings"] = DshLog.WarningCount;
            compiled["elapsedMs"] = started.ElapsedMilliseconds;

            WriteEnvelope("compile", delegate { return compiled; });
        }

        /// <summary>Выполнить команду из JSON-файла (-dshCmd) или параметров -dshMethod/-dshArgs.</summary>
        public static void Run()
        {
            DshRequest request = null;

            string commandFile = GetArg("-dshCmd");
            if (!string.IsNullOrEmpty(commandFile) && File.Exists(commandFile))
            {
                try
                {
                    request = JsonUtility.FromJson<DshRequest>(File.ReadAllText(commandFile, Encoding.UTF8));
                }
                catch (Exception e)
                {
                    WriteRaw(Envelope(false, null, "Не удалось прочитать -dshCmd: " + e.Message), "batch:error");
                    return;
                }
            }

            if (request == null) request = new DshRequest();

            string method = GetArg("-dshMethod");
            if (!string.IsNullOrEmpty(method)) request.method = method;

            string args = GetArg("-dshArgs");
            if (!string.IsNullOrEmpty(args)) request.args = args;

            string command = GetArg("-dshCommand");
            if (!string.IsNullOrEmpty(command)) request.cmd = command;

            if (string.IsNullOrEmpty(request.cmd))
                request.cmd = string.IsNullOrEmpty(request.method) ? "state" : "exec";

            var started = Stopwatch.StartNew();
            string label = "batch:" + request.cmd;

            try
            {
                var data = DshCommands.Invoke(request);
                var payload = new Dictionary<string, object>();
                payload["ok"] = true;
                payload["command"] = request.cmd;
                payload["data"] = data;
                payload["elapsedMs"] = started.ElapsedMilliseconds;
                payload["unity"] = Application.unityVersion;
                payload["project"] = DshPaths.ProjectRoot;
                WriteRaw(MiniJson.Serialize(payload), label);
            }
            catch (Exception e)
            {
                WriteRaw(Envelope(false, null, e.GetBaseException().Message), label);
            }
        }

        // ------------------------------------------------------------------ utils

        private static void WriteEnvelope(string label, Func<Dictionary<string, object>> action)
        {
            var started = Stopwatch.StartNew();
            try
            {
                var payload = new Dictionary<string, object>();
                payload["ok"] = true;
                payload["command"] = label;
                payload["data"] = action();
                payload["elapsedMs"] = started.ElapsedMilliseconds;
                payload["unity"] = Application.unityVersion;
                payload["project"] = DshPaths.ProjectRoot;
                WriteRaw(MiniJson.Serialize(payload), label);
            }
            catch (Exception e)
            {
                WriteRaw(Envelope(false, null, e.GetBaseException().Message), label);
            }
        }

        private static string Envelope(bool ok, object data, string error)
        {
            var payload = new Dictionary<string, object>();
            payload["ok"] = ok;
            if (ok) payload["data"] = data;
            else payload["error"] = error;
            payload["time"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            return MiniJson.Serialize(payload);
        }

        private static void WriteRaw(string json, string label)
        {
            string path = GetArg("-dshOut");
            if (string.IsNullOrEmpty(path)) path = DshPaths.BatchResultFile;

            DshPaths.WriteAllText(path, json);
            if (string.IsNullOrEmpty(label)) label = "result";
            Debug.Log("[DSH] " + label + " -> " + path);
        }

        public static string GetArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            }
            return null;
        }
    }
}
