// DSH Bridge — команды, которые умеет выполнять мост.
// Все методы вызываются на главном потоке Unity (см. DshMainThread).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DshBridge
{
    /// <summary>Тело POST /command и файл -dshCmd для batch-режима.</summary>
    [Serializable]
    public class DshRequest
    {
        public string cmd;
        public string arg;
        public string args;
        public string method;
        public string type;
        public int count;
    }

    public static class DshCommands
    {
        public const string ScriptNamespacePrefix = "DshScripts";

        private static int hierarchyNodes;

        // ---------------------------------------------------------------- router

        public static Dictionary<string, object> Invoke(DshRequest request)
        {
            if (request == null) throw new ArgumentException("Пустое тело запроса.");

            string cmd = (request.cmd ?? string.Empty).Trim().ToLowerInvariant();
            switch (cmd)
            {
                case "": return Ping();
                case "ping": return Ping();
                case "state": return State();
                case "console":
                case "logs": return Console(request.count > 0 ? request.count : 50, request.type);
                case "menu": return Menu(request.arg);
                case "play":
                    EditorApplication.isPlaying = true;
                    return Ok("вход в Play Mode");
                case "stop":
                    EditorApplication.isPlaying = false;
                    return Ok("выход из Play Mode");
                case "pause":
                    EditorApplication.isPaused = !EditorApplication.isPaused;
                    return Ok(EditorApplication.isPaused ? "пауза" : "продолжение");
                case "compile": return Compile();
                case "exec": return Exec(request.method ?? request.arg, request.args);
                case "build": return Build(request.arg);
                case "screenshot": return Screenshot(request.arg);
                case "hierarchy": return Hierarchy(request.arg);
                case "scene": return OpenScene(request.arg);
                case "save": return SaveScene();
                case "clear-log":
                    DshLog.Clear();
                    return Ok("консоль очищена");
                default:
                    throw new ArgumentException("Неизвестная команда: " + request.cmd);
            }
        }

        // ----------------------------------------------------------------- basics

        public static Dictionary<string, object> Ping()
        {
            var d = new Dictionary<string, object>();
            d["project"] = ProjectName();
            d["projectPath"] = DshPaths.ProjectRoot;
            d["unity"] = Application.unityVersion;
            d["unityEditor"] = EditorApplication.applicationPath;
            d["version"] = Application.version;
            d["playing"] = EditorApplication.isPlaying;
            d["paused"] = EditorApplication.isPaused;
            d["compiling"] = EditorApplication.isCompiling;
            d["updating"] = EditorApplication.isUpdating;
            d["batch"] = Application.isBatchMode;
            d["port"] = DshServer.Port;
            d["pid"] = System.Diagnostics.Process.GetCurrentProcess().Id;
            d["time"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            var log = new Dictionary<string, object>();
            log["total"] = DshLog.Total;
            log["errors"] = DshLog.ErrorCount;
            log["warnings"] = DshLog.WarningCount;
            d["log"] = log;
            return d;
        }

        public static Dictionary<string, object> State()
        {
            var d = Ping();

            var scene = EditorSceneManager.GetActiveScene();
            var sceneInfo = new Dictionary<string, object>();
            sceneInfo["name"] = scene.name;
            sceneInfo["path"] = scene.path;
            sceneInfo["dirty"] = scene.isDirty;
            d["scene"] = sceneInfo;

            var selection = new List<object>();
            foreach (var go in Selection.gameObjects) selection.Add(go.name);
            d["selection"] = selection;

            var buildTarget = new Dictionary<string, object>();
            buildTarget["target"] = EditorUserBuildSettings.activeBuildTarget.ToString();
            buildTarget["group"] = EditorUserBuildSettings.selectedBuildTargetGroup.ToString();
            var scenes = new List<object>();
            foreach (var s in EditorBuildSettings.scenes) scenes.Add(new Dictionary<string, object> { { "path", s.path }, { "enabled", s.enabled } });
            buildTarget["scenes"] = scenes;
            d["build"] = buildTarget;

            var lastError = DshLog.Snapshot(1, "error");
            if (lastError.Count > 0) d["lastError"] = lastError[0];
            return d;
        }

        public static Dictionary<string, object> Console(int count, string type)
        {
            var d = new Dictionary<string, object>();
            d["entries"] = DshLog.Snapshot(count, type);
            d["total"] = DshLog.Total;
            d["errors"] = DshLog.ErrorCount;
            d["warnings"] = DshLog.WarningCount;
            return d;
        }

        // ------------------------------------------------------------------ menu

        public static Dictionary<string, object> Menu(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("arg: путь пункта меню, например 'Window/General/Console'.");
            bool ok = EditorApplication.ExecuteMenuItem(path);
            if (!ok) throw new InvalidOperationException("Пункт меню не найден или недоступен: " + path);
            return new Dictionary<string, object>
            {
                { "executed", ok },
                { "path", path }
            };
        }

        // -------------------------------------------------------------- compile

        public static Dictionary<string, object> Compile()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();

            var d = new Dictionary<string, object>();
            d["requested"] = true;
            d["failed"] = EditorUtility.scriptCompilationFailed;
            d["compiling"] = EditorApplication.isCompiling;
            d["errors"] = DshLog.ErrorCount;
            return d;
        }

        // ------------------------------------------------------------------ exec

        public static Dictionary<string, object> Exec(string method, string args)
        {
            if (string.IsNullOrWhiteSpace(method))
                throw new ArgumentException("method: 'DshScripts.ИмяКласса.ИмяМетода'.");

            int dot = method.LastIndexOf('.');
            if (dot <= 0) throw new ArgumentException("method должен быть в виде Namespace.Type.Method, получено: " + method);

            string typeName = method.Substring(0, dot);
            string methodName = method.Substring(dot + 1);

            if (!typeName.StartsWith(ScriptNamespacePrefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Из моста можно вызывать только типы из namespace " + ScriptNamespacePrefix +
                    " (папка Assets/Editor/DshScripts). Получено: " + typeName);
            }

            Type type = FindType(typeName);
            if (type == null) throw new InvalidOperationException("Тип не найден: " + typeName);

            MethodInfo info = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            if (info == null) throw new InvalidOperationException("Публичный статический метод не найден: " + typeName + "." + methodName);

            var parameters = info.GetParameters();
            object result;
            if (parameters.Length == 0)
            {
                result = info.Invoke(null, null);
            }
            else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
            {
                result = info.Invoke(null, new object[] { args ?? string.Empty });
            }
            else
            {
                throw new InvalidOperationException(
                    "Метод должен быть без параметров или с одним string. Сигнатура: " + info);
            }

            AssetDatabase.Refresh();

            var d = new Dictionary<string, object>();
            d["method"] = method;
            d["result"] = result == null ? null : result.ToString();
            d["resultType"] = result == null ? "void" : result.GetType().Name;
            d["assembly"] = type.Assembly.GetName().Name;
            return d;
        }

        private static Type FindType(string fullName)
        {
            // Сначала быстрые кандидаты, потом общий перебор сборок.
            string[] preferred =
            {
                "Assembly-CSharp-Editor", "Assembly-CSharp", "Assembly-CSharp-firstpass"
            };
            for (int i = 0; i < preferred.Length; i++)
            {
                var candidate = AppDomain.CurrentDomain.GetAssemblies();
                for (int j = 0; j < candidate.Length; j++)
                {
                    if (candidate[j].GetName().Name != preferred[i]) continue;
                    Type found = candidate[j].GetType(fullName, false);
                    if (found != null) return found;
                }
            }

#pragma warning disable UAC0005 // AppDomain.GetAssemblies может отдать выгруженные сборки — здесь только чтение типов
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
#pragma warning restore UAC0005
            for (int i = 0; i < assemblies.Length; i++)
            {
                var assembly = assemblies[i];
                string name = assembly.GetName().Name;
                if (name.StartsWith("System", StringComparison.Ordinal) ||
                    name == "mscorlib" || name == "netstandard" || name == "UnityEngine" ||
                    name == "UnityEditor") continue;

                Type type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            return null;
        }

        // ----------------------------------------------------------------- build

        public static Dictionary<string, object> Build(string targetArg)
        {
            string target = string.IsNullOrEmpty(targetArg) ? "win64" : targetArg.Trim().ToLowerInvariant();
            BuildTarget buildTarget;
            string extension;

            switch (target)
            {
                case "win64":
                case "windows":
                    buildTarget = BuildTarget.StandaloneWindows64; extension = ".exe"; break;
                case "linux64":
                case "linux":
                    buildTarget = BuildTarget.StandaloneLinux64; extension = string.Empty; break;
                case "webgl":
                    buildTarget = BuildTarget.WebGL; extension = string.Empty; break;
                case "android":
                    buildTarget = BuildTarget.Android; extension = ".apk"; break;
                default:
                    throw new ArgumentException("Неизвестная платформа: " + target + " (win64 | linux64 | webgl | android).");
            }

            var scenes = new List<string>();
            foreach (var s in EditorBuildSettings.scenes) if (s.enabled) scenes.Add(s.path);
            if (scenes.Count == 0)
                throw new InvalidOperationException("В Build Settings нет включённых сцен — добавь сцену (File → Build Settings).");

            string outDir = Path.Combine(DshPaths.ProjectRoot, "Builds", target);
            Directory.CreateDirectory(outDir);

            string location = buildTarget == BuildTarget.WebGL
                ? outDir
                : Path.Combine(outDir, ProjectName() + extension);

            var options = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = location,
                target = buildTarget,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);

            var d = new Dictionary<string, object>();
            d["result"] = report.summary.result.ToString();
            d["output"] = report.summary.outputPath;
            d["errors"] = (int)report.summary.totalErrors;
            d["warnings"] = (int)report.summary.totalWarnings;
            d["sizeBytes"] = (long)report.summary.totalSize;
            d["durationSec"] = Math.Round(report.summary.totalTime.TotalSeconds, 1);
            d["scenes"] = scenes;
            return d;
        }

        // ------------------------------------------------------------ screenshot

        public static Dictionary<string, object> Screenshot(string pathArg)
        {
            string path = pathArg;
            if (string.IsNullOrEmpty(path))
            {
                path = Path.Combine(DshPaths.ScreenshotDir, "shot-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png");
            }
            else if (!Path.IsPathRooted(path))
            {
                path = Path.Combine(DshPaths.ProjectRoot, path);
            }
            DshPaths.EnsureDir(path);

            Camera camera = null;
            if (EditorApplication.isPlaying) camera = Camera.main;
            if (camera == null)
            {
                var cameras = UnityEngine.Object.FindObjectsOfType<Camera>();
                for (int i = 0; i < cameras.Length; i++)
                {
                    if (cameras[i].CompareTag("MainCamera")) { camera = cameras[i]; break; }
                }
                if (camera == null && cameras.Length > 0) camera = cameras[0];
            }
            if (camera == null && SceneView.lastActiveSceneView != null) camera = SceneView.lastActiveSceneView.camera;
            if (camera == null) throw new InvalidOperationException("В сцене нет камеры — снимать нечего.");

            const int width = 1280;
            const int height = 720;
            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var previous = camera.targetTexture;

            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
                camera.targetTexture = previous;

                RenderTexture.active = renderTexture;
                var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();

                byte[] png = texture.EncodeToPNG();
                File.WriteAllBytes(path, png);
                UnityEngine.Object.DestroyImmediate(texture);
            }
            finally
            {
                camera.targetTexture = previous;
                RenderTexture.active = null;
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }

            return new Dictionary<string, object>
            {
                { "path", path },
                { "camera", camera.name },
                { "width", width },
                { "height", height }
            };
        }

        // ------------------------------------------------------------- hierarchy

        /// <summary>Дерево активной сцены. Если передан путь — сцена сначала открывается.</summary>
        public static Dictionary<string, object> Hierarchy(string scenePath)
        {
            if (!string.IsNullOrEmpty(scenePath))
            {
                if (!File.Exists(scenePath)) throw new FileNotFoundException("Сцена не найдена: " + scenePath);
                if (!EditorApplication.isPlaying) EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            hierarchyNodes = 0;
            var scene = SceneManager.GetActiveScene();
            var roots = new List<object>();
            var gameObjects = scene.GetRootGameObjects();

            for (int i = 0; i < gameObjects.Length && hierarchyNodes < 600; i++)
            {
                roots.Add(Node(gameObjects[i].transform, 0, 12));
            }

            var d = new Dictionary<string, object>();
            d["scene"] = scene.name;
            d["path"] = scene.path;
            d["roots"] = roots;
            d["count"] = hierarchyNodes;
            d["truncated"] = hierarchyNodes >= 600;
            return d;
        }

        private static Dictionary<string, object> Node(Transform transform, int depth, int maxDepth)
        {
            hierarchyNodes++;
            var node = new Dictionary<string, object>();
            node["name"] = transform.name;
            node["active"] = transform.gameObject.activeSelf;
            node["path"] = GetPath(transform);
            node["components"] = ComponentNames(transform.gameObject);

            if (depth < maxDepth && transform.childCount > 0 && hierarchyNodes < 600)
            {
                var children = new List<object>();
                for (int i = 0; i < transform.childCount && hierarchyNodes < 600; i++)
                    children.Add(Node(transform.GetChild(i), depth + 1, maxDepth));
                node["children"] = children;
            }
            return node;
        }

        private static List<object> ComponentNames(GameObject go)
        {
            var names = new List<object>();
            var components = go.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                names.Add(components[i] == null ? "<missing>" : components[i].GetType().Name);
            }
            return names;
        }

        private static string GetPath(Transform transform)
        {
            var parts = new List<string>();
            var current = transform;
            while (current != null)
            {
                parts.Insert(0, current.name);
                current = current.parent;
            }
            return string.Join("/", parts.ToArray());
        }

        // ----------------------------------------------------------------- scene

        public static Dictionary<string, object> OpenScene(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("arg: путь к сцене (.unity).");
            if (!File.Exists(path)) throw new FileNotFoundException("Сцена не найдена: " + path);
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Сначала выйди из Play Mode.");

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            return new Dictionary<string, object>
            {
                { "name", scene.name },
                { "path", scene.path },
                { "roots", scene.rootCount }
            };
        }

        public static Dictionary<string, object> SaveScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                string dir = Path.Combine(DshPaths.ProjectRoot, "Assets", "Scenes");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "Main.unity");
                EditorSceneManager.SaveScene(scene, path);
                AssetDatabase.Refresh();
                return new Dictionary<string, object> { { "saved", true }, { "path", path }, { "created", true } };
            }

            bool saved = EditorSceneManager.SaveScene(scene);
            return new Dictionary<string, object> { { "saved", saved }, { "path", scene.path } };
        }

        // ----------------------------------------------------------------- utils

        public static string ProjectName()
        {
            string name = PlayerSettings.productName;
            if (!string.IsNullOrEmpty(name)) return name;
            return new DirectoryInfo(DshPaths.ProjectRoot).Name;
        }

        private static Dictionary<string, object> Ok(string message)
        {
            return new Dictionary<string, object> { { "message", message } };
        }
    }
}
