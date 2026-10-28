// DSH Bridge — общие утилиты: мини-JSON, кольцевой буфер логов, маршал на главный поток.
// Живёт в Assets/Editor/..., поэтому попадает в Assembly-CSharp-Editor и не мешает билду.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace DshBridge
{
    /// <summary>Минимальный сериализатор JSON: словари, списки, строки, числа, bool, null.</summary>
    internal static class MiniJson
    {
        public static string Serialize(object value)
        {
            var sb = new StringBuilder(512);
            Write(sb, value);
            return sb.ToString();
        }

        private static void Write(StringBuilder sb, object value)
        {
            if (value == null) { sb.Append("null"); return; }

            string text = value as string;
            if (text != null) { WriteString(sb, text); return; }

            if (value is bool) { sb.Append(((bool)value) ? "true" : "false"); return; }

            if (value is int || value is long || value is short || value is byte || value is sbyte ||
                value is uint || value is ulong || value is ushort)
            {
                sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }

            if (value is float) { sb.Append(((float)value).ToString("R", CultureInfo.InvariantCulture)); return; }
            if (value is double) { sb.Append(((double)value).ToString("R", CultureInfo.InvariantCulture)); return; }
            if (value is decimal) { sb.Append(((decimal)value).ToString(CultureInfo.InvariantCulture)); return; }

            var dictionary = value as IDictionary<string, object>;
            if (dictionary != null)
            {
                sb.Append('{');
                bool first = true;
                foreach (var pair in dictionary)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    WriteString(sb, pair.Key);
                    sb.Append(':');
                    Write(sb, pair.Value);
                }
                sb.Append('}');
                return;
            }

            var list = value as IList;
            if (list != null)
            {
                sb.Append('[');
                for (int i = 0; i < list.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    Write(sb, list[i]);
                }
                sb.Append(']');
                return;
            }

            var enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                sb.Append('[');
                bool first = true;
                foreach (var item in enumerable)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    Write(sb, item);
                }
                sb.Append(']');
                return;
            }

            WriteString(sb, Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        public static void WriteString(StringBuilder sb, string value)
        {
            if (value == null) { sb.Append("null"); return; }
            sb.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ' || c == '\u007f')
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
        }
    }

    /// <summary>Пути, которыми пользуется мост.</summary>
    public static class DshPaths
    {
        public static string ProjectRoot
        {
            get { return Directory.GetParent(Application.dataPath).FullName; }
        }

        /// <summary>Служебные файлы моста (порт, результаты). Library не попадает в VCS.</summary>
        public static string StateDir
        {
            get
            {
                string dir = Path.Combine(ProjectRoot, "Library", "DshBridge");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static string PortFile
        {
            get { return Path.Combine(StateDir, "bridge.json"); }
        }

        public static string ScreenshotDir
        {
            get
            {
                string dir = Path.Combine(ProjectRoot, "Logs", "DshShots");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static string BatchResultFile
        {
            get { return Path.Combine(StateDir, "batch-result.json"); }
        }

        public static void EnsureDir(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        }

        public static void WriteAllText(string path, string text)
        {
            EnsureDir(path);
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }
    }

    /// <summary>Последние сообщения консоли Unity, доступные по HTTP.</summary>
    public static class DshLog
    {
        private const int Capacity = 400;

        private static readonly ConcurrentQueue<Dictionary<string, object>> entries =
            new ConcurrentQueue<Dictionary<string, object>>();

        private static int total;
        private static int errors;
        private static int warnings;

        [InitializeOnLoadMethod]
        private static void Init()
        {
            Application.logMessageReceivedThreaded += OnMessage;
        }

        private static void OnMessage(string message, string stackTrace, LogType type)
        {
            if (message == null) message = string.Empty;

            var entry = new Dictionary<string, object>();
            entry["type"] = type.ToString().ToLowerInvariant();
            entry["message"] = message;
            entry["stack"] = Trim(stackTrace, 4000);
            entry["time"] = DateTime.Now.ToString("HH:mm:ss.fff");

            entries.Enqueue(entry);
            Interlocked.Increment(ref total);
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                Interlocked.Increment(ref errors);
            else if (type == LogType.Warning)
                Interlocked.Increment(ref warnings);

            Dictionary<string, object> ignored;
            while (entries.Count > Capacity) entries.TryDequeue(out ignored);
        }

        private static string Trim(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Length <= max ? text : text.Substring(0, max) + "...";
        }

        public static int Total { get { return total; } }
        public static int ErrorCount { get { return errors; } }
        public static int WarningCount { get { return warnings; } }

        /// <summary>Последние записи; typeFilter: all | error | warning | log.</summary>
        public static List<Dictionary<string, object>> Snapshot(int count, string typeFilter)
        {
            if (count <= 0) count = 50;
            string filter = string.IsNullOrEmpty(typeFilter) ? "all" : typeFilter.Trim().ToLowerInvariant();

            var all = new List<Dictionary<string, object>>(entries);
            var result = new List<Dictionary<string, object>>();
            for (int i = 0; i < all.Count; i++)
            {
                string type = Convert.ToString(all[i]["type"]);
                bool match = filter == "all" ||
                             (filter == "error" && (type == "error" || type == "exception" || type == "assert")) ||
                             (filter == "warning" && type == "warning") ||
                             (filter == "log" && type == "log");
                if (match) result.Add(all[i]);
            }

            if (result.Count > count) result.RemoveRange(0, result.Count - count);
            return result;
        }

        public static void Clear()
        {
            Dictionary<string, object> ignored;
            while (entries.TryDequeue(out ignored)) { }
            total = 0;
            errors = 0;
            warnings = 0;
        }
    }

    /// <summary>Очередь работы на главный поток Unity (API редактора не потокобезопасен).</summary>
    public static class DshMainThread
    {
        private static readonly ConcurrentQueue<Action> queue = new ConcurrentQueue<Action>();
        private static int mainThreadId;

        [InitializeOnLoadMethod]
        private static void Init()
        {
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
            EditorApplication.update += Pump;
        }

        private static void Pump()
        {
            Action action;
            while (queue.TryDequeue(out action))
            {
                try { action(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        public static bool IsMainThread
        {
            get { return Thread.CurrentThread.ManagedThreadId == mainThreadId; }
        }

        public static void Run(Action action, int timeoutMs)
        {
            if (action == null) return;
            if (IsMainThread) { action(); return; }

            Exception captured = null;
            using (var done = new ManualResetEventSlim(false))
            {
                queue.Enqueue(delegate
                {
                    try { action(); }
                    catch (Exception e) { captured = e; }
                    finally { done.Set(); }
                });

                if (!done.Wait(timeoutMs))
                {
                    throw new TimeoutException(
                        "Главный поток Unity не ответил за " + timeoutMs + " мс (редактор занят, в фоне или показывает модальное окно).");
                }
            }

            if (captured != null) throw new InvalidOperationException(captured.Message, captured);
        }
    }
}
