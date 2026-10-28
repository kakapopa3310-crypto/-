// DSH Bridge — маленький HTTP-сервер на TcpListener внутри редактора Unity.
// Слушает только 127.0.0.1, поэтому снаружи машины недоступен.
//
//   GET  /ping                     — жив ли мост и что открыто
//   GET  /state                    — состояние + сцена + сборка
//   GET  /console?count=50&type=error — последние логи Unity
//   POST /command {"cmd":"..."}    — команда (см. DshCommands.Invoke)
//
// Ответ: {"ok":true,"data":{...},"elapsedMs":12} либо {"ok":false,"error":"..."}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DshBridge
{
    [InitializeOnLoad]
    public static class DshServer
    {
        public const int DefaultPort = 8787;
        private const string AutoStartKey = "DshBridge.AutoStart";

        private static TcpListener listener;
        private static Thread acceptThread;
        private static volatile bool running;
        private static int port;

        public static int Port { get { return port; } }
        public static bool IsRunning { get { return running; } }
        public static string Url { get { return "http://127.0.0.1:" + port + "/"; } }

        public static bool AutoStart
        {
            get { return EditorPrefs.GetBool(AutoStartKey, true); }
            set { EditorPrefs.SetBool(AutoStartKey, value); }
        }

        static DshServer()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= Stop;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting -= Stop;
            EditorApplication.quitting += Stop;



            if (Application.isBatchMode) return;
            EditorApplication.delayCall += delegate
            {
                if (AutoStart && !running) Start();
            };
        }

        // ------------------------------------------------------------------ life

        public static void Start()
        {
            Start(DefaultPort);
        }

        public static void Start(int preferredPort)
        {
            if (running) return;

            int basePort = preferredPort > 0 ? preferredPort : DefaultPort;
            Exception last = null;

            for (int i = 0; i < 25 && listener == null; i++)
            {
                int candidate = basePort + i;
                TcpListener candidateListener = null;
                try
                {
                    candidateListener = new TcpListener(IPAddress.Loopback, candidate);
                    candidateListener.Start();
                    listener = candidateListener;
                    port = candidate;
                    break;
                }
                catch (Exception e)
                {
                    last = e;
                    if (candidateListener != null) try { candidateListener.Stop(); } catch { }
                }
            }

            if (listener == null)
                throw new InvalidOperationException("Не удалось занять порт " + basePort + "…" + (basePort + 24), last);

            running = true;
            acceptThread = new Thread(AcceptLoop);
            acceptThread.IsBackground = true;
            acceptThread.Name = "DshBridge";
            acceptThread.Start();

            WritePortFile();
            Debug.Log("[DSH] Мост запущен: " + Url + " (проект " + DshPaths.ProjectRoot + ")");
        }

        public static void Stop()
        {
            running = false;

            if (listener != null)
            {
                try { listener.Stop(); } catch { }
                listener = null;
            }

            if (acceptThread != null)
            {
                try { if (!acceptThread.Join(400)) acceptThread.Interrupt(); } catch { }
                acceptThread = null;
            }

            try { if (File.Exists(DshPaths.PortFile)) File.Delete(DshPaths.PortFile); } catch { }
        }

        public static void Restart()
        {
            Stop();
            Start();
        }

        private static void WritePortFile()
        {
            try
            {
                var d = new Dictionary<string, object>();
                d["port"] = port;
                d["url"] = Url;
                d["pid"] = Process.GetCurrentProcess().Id;
                d["project"] = DshPaths.ProjectRoot;
                d["startedAt"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                DshPaths.WriteAllText(DshPaths.PortFile, MiniJson.Serialize(d));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DSH] Не удалось записать файл порта: " + e.Message);
            }
        }

        // ----------------------------------------------------------------- serve

        private static void AcceptLoop()
        {
            while (running)
            {
                TcpClient client;
                try
                {
                    client = listener.AcceptTcpClient();
                }
                catch
                {
                    break;
                }

                ThreadPool.QueueUserWorkItem(delegate (object state)
                {
                    var tcp = (TcpClient)state;
                    try { HandleClient(tcp); }
                    catch (Exception e) { Debug.LogWarning("[DSH] Ошибка запроса: " + e.Message); }
                    finally { try { tcp.Close(); } catch { } }
                }, client);
            }
        }

        private static void HandleClient(TcpClient client)
        {
            var stopwatch = Stopwatch.StartNew();
            string body;
            string head = ReadHead(client, out body);
            if (head == null) return;

            string requestLine = head.Split('\n')[0].Trim();
            string[] parts = requestLine.Split(' ');
            string httpMethod = parts.Length > 0 ? parts[0].ToUpperInvariant() : "GET";
            string url = parts.Length > 1 ? parts[1] : "/";

            string path = url;
            string query = string.Empty;
            int q = url.IndexOf('?');
            if (q >= 0)
            {
                path = url.Substring(0, q);
                query = url.Substring(q + 1);
            }
            path = path.TrimEnd('/');
            if (path.Length == 0) path = "/";

            string error = null;
            object data = null;

            try
            {
                if (httpMethod == "GET")
                {
                    switch (path)
                    {
                        case "/":
                        case "/ping":
                            data = OnMain(DshCommands.Ping);
                            break;
                        case "/state":
                            data = OnMain(DshCommands.State);
                            break;
                        case "/console":
                        case "/logs":
                        {
                            var qs = ParseQuery(query);
                            int count = 50;
                            string countText;
                            if (qs.TryGetValue("count", out countText)) int.TryParse(countText, out count);
                            string type;
                            qs.TryGetValue("type", out type);
                            data = OnMain(delegate { return DshCommands.Console(count, type); });
                            break;
                        }
                        case "/help":
                            data = Help();
                            break;
                        default:
                            throw new ArgumentException("Неизвестный путь: " + path);
                    }
                }
                else if (httpMethod == "POST")
                {
                    var request = ParseRequest(body);
                    data = OnMain(delegate { return DshCommands.Invoke(request); });
                }
                else if (httpMethod == "OPTIONS")
                {
                    data = new Dictionary<string, object> { { "methods", new[] { "GET", "POST" } } };
                }
                else
                {
                    throw new ArgumentException("Метод не поддерживается: " + httpMethod);
                }
            }
            catch (Exception e)
            {
                error = e.GetBaseException().Message;
            }

            var payload = new Dictionary<string, object>();
            payload["ok"] = error == null;
            if (error == null) payload["data"] = data;
            else payload["error"] = error;
            payload["elapsedMs"] = stopwatch.ElapsedMilliseconds;
            payload["port"] = port;

            int status = error == null ? 200 : 400;
            Respond(client, status, MiniJson.Serialize(payload));
        }

        private static object OnMain(Func<Dictionary<string, object>> action)
        {
            Dictionary<string, object> result = null;
            DshMainThread.Run(delegate { result = action(); }, 180000);
            return result;
        }

        private static Dictionary<string, object> Help()
        {
            return new Dictionary<string, object>
            {
                { "endpoints", new object[] {
                    "GET /ping",
                    "GET /state",
                    "GET /console?count=50&type=all|error|warning|log",
                    "POST /command {cmd, arg, args, method, type, count}"
                } },
                { "commands", new object[] {
                    "ping", "state", "console", "menu", "play", "stop", "pause",
                    "compile", "exec", "build", "screenshot", "hierarchy", "scene", "save", "clear-log"
                } },
                { "scripts", "Assets/Editor/DshScripts/, namespace DshScripts, public static method (string) или ()" },
                { "cli", "Tools/dsh-unity.ps1 ping" }
            };
        }

        private static Dictionary<string, string> ParseQuery(string query)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(query)) return result;

            foreach (string pair in query.Split('&'))
            {
                if (pair.Length == 0) continue;
                int eq = pair.IndexOf('=');
                if (eq < 0) result[Uri.UnescapeDataString(pair)] = string.Empty;
                else result[Uri.UnescapeDataString(pair.Substring(0, eq))] = Uri.UnescapeDataString(pair.Substring(eq + 1));
            }
            return result;
        }

        private static DshRequest ParseRequest(string body)
        {
            if (string.IsNullOrEmpty(body)) return new DshRequest();
            try
            {
                var request = JsonUtility.FromJson<DshRequest>(body);
                return request ?? new DshRequest();
            }
            catch (Exception e)
            {
                throw new ArgumentException("Тело запроса не разобрано как JSON: " + e.Message);
            }
        }

        // -------------------------------------------------------------- transport

        private static string ReadHead(TcpClient client, out string body)
        {
            body = string.Empty;
            NetworkStream stream = client.GetStream();
            stream.ReadTimeout = 30000;
            stream.WriteTimeout = 30000;

            var buffer = new MemoryStream();
            var chunk = new byte[8192];
            int headerEnd = -1;

            while (headerEnd < 0)
            {
                int read = stream.Read(chunk, 0, chunk.Length);
                if (read <= 0) return null;
                buffer.Write(chunk, 0, read);
                if (buffer.Length > 2 * 1024 * 1024) return null;
                headerEnd = FindHeaderEnd(buffer.GetBuffer(), (int)buffer.Length);
            }

            byte[] all = buffer.ToArray();
            string head = Encoding.UTF8.GetString(all, 0, headerEnd);

            int contentLength = 0;
            string[] lines = head.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 1; i < lines.Length; i++)
            {
                int colon = lines[i].IndexOf(':');
                if (colon <= 0) continue;
                string key = lines[i].Substring(0, colon).Trim();
                if (!key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)) continue;
                int.TryParse(lines[i].Substring(colon + 1).Trim(), out contentLength);
            }

            int bodyStart = headerEnd + 4;
            int available = all.Length - bodyStart;

            if (contentLength > 0)
            {
                if (available < contentLength)
                {
                    var rest = new MemoryStream();
                    rest.Write(all, bodyStart, available);
                    while (rest.Length < contentLength)
                    {
                        int read = stream.Read(chunk, 0, chunk.Length);
                        if (read <= 0) break;
                        rest.Write(chunk, 0, read);
                    }
                    all = rest.ToArray();
                    bodyStart = 0;
                }

                int length = Math.Min(contentLength, all.Length - bodyStart);
                if (length > 0) body = Encoding.UTF8.GetString(all, bodyStart, length);
            }

            return head;
        }

        private static int FindHeaderEnd(byte[] data, int length)
        {
            for (int i = 3; i < length; i++)
            {
                if (data[i] == '\n' && data[i - 1] == '\r' && data[i - 2] == '\n' && data[i - 3] == '\r') return i - 3;
            }
            return -1;
        }

        private static void Respond(TcpClient client, int status, string json)
        {
            byte[] payload = Encoding.UTF8.GetBytes(json);
            string reason = status == 200 ? "OK" : "Bad Request";

            var header = new StringBuilder();
            header.Append("HTTP/1.1 ").Append(status).Append(' ').Append(reason).Append("\r\n");
            header.Append("Content-Type: application/json; charset=utf-8\r\n");
            header.Append("Content-Length: ").Append(payload.Length).Append("\r\n");
            header.Append("Cache-Control: no-store\r\n");
            header.Append("Access-Control-Allow-Origin: *\r\n");
            header.Append("Access-Control-Allow-Headers: content-type\r\n");
            header.Append("Connection: close\r\n\r\n");

            byte[] head = Encoding.ASCII.GetBytes(header.ToString());
            NetworkStream stream = client.GetStream();
            stream.Write(head, 0, head.Length);
            stream.Write(payload, 0, payload.Length);
            stream.Flush();
        }
    }
}
