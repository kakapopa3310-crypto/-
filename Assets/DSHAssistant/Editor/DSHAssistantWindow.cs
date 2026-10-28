using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace DSH.Assistant.EditorTools
{
    /// <summary>
    /// Окно ассистента в редакторе: Tools > DSH Assistant.
    /// Настройки хранятся в EditorPrefs этой машины; ключ API — тоже (в открытом виде,
    /// поэтому для команды используйте локальный мост без ключа).
    /// </summary>
    public class DSHAssistantWindow : EditorWindow
    {
        private const string PrefBaseUrl = "DSH.Assistant.BaseUrl";
        private const string PrefModel = "DSH.Assistant.Model";
        private const string PrefApiKey = "DSH.Assistant.ApiKey";
        private const string PrefContext = "DSH.Assistant.IncludeContext";
        private const string PrefPrompt = "DSH.Assistant.SystemPrompt";
        private const string InputControlName = "DSHAssistantInput";

        private static readonly string DefaultPrompt =
            "Ты — DSH, встроенный ИИ-ассистент Unity-проекта «бумага». Отвечай по-русски, кратко и по делу. " +
            "Если просят код — давай готовый C# для Unity 6 (6000.6). " +
            "Учитывай контекст сцены, если он передан. " +
            "Помогай с геймплеем, механиками, шейдерами и архитектурой игры про бумагу.";

        private readonly List<DSHChatMessage> history = new List<DSHChatMessage>();
        private readonly Queue<Action> mainThreadQueue = new Queue<Action>();

        private string baseUrl = "http://127.0.0.1:8787/v1";
        private string model = "deepseek-chat";
        private string apiKey = string.Empty;
        private string systemPrompt = DefaultPrompt;
        private bool includeContext = true;
        private bool showPrompt;

        private string input = string.Empty;
        private string status = "Готов";
        private string lastAnswer = string.Empty;
        private Vector2 scroll;
        private bool busy;
        private CancellationTokenSource pendingRequest;
        private DSHAssistantSettings transientSettings;
        private GUIStyle transcriptStyle;

        [MenuItem("Tools/DSH Assistant %&a", false, 1)]
        public static void Open()
        {
            var window = GetWindow<DSHAssistantWindow>("DSH Assistant");
            window.minSize = new Vector2(520f, 440f);
            window.Show();
        }

        [MenuItem("Tools/DSH Assistant/Создать настройки", false, 20)]
        private static void CreateSettingsAsset()
        {
            if (!AssetDatabase.IsValidFolder("Assets/DSHAssistant"))
                AssetDatabase.CreateFolder("Assets", "DSHAssistant");

            var asset = CreateInstance<DSHAssistantSettings>();
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/DSHAssistant/DSHAssistantSettings.asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            Debug.Log("[DSH Assistant] Созданы настройки: " + path);
        }

        [MenuItem("Tools/DSH Assistant/Добавить чат в сцену", false, 21)]
        private static void AddOverlayToScene()
        {
            DSHProjectInstaller.InstallIntoOpenScene();
        }

        [MenuItem("Tools/DSH Assistant/Проверить подключение", false, 22)]
        private static async void PingBridge()
        {
            var settings = FindSettingsAsset();
            string url = settings != null ? settings.baseUrl : "http://127.0.0.1:8787/v1";

            var uri = new Uri(url.TrimEnd('/'));
            string health = uri.GetLeftPart(UriPartial.Authority) + "/health";

            try
            {
                using (var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) })
                {
                    string body = await client.GetStringAsync(health);
                    Debug.Log("[DSH Assistant] " + health + " → " + body);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DSH Assistant] Мост недоступен: " + health + " — " + e.Message);
            }
        }

        [MenuItem("Tools/DSH Assistant/Запустить мост", false, 23)]
        private static void StartBridge()
        {
            string bridgePath = FindBridgeScript();
            if (bridgePath == null)
            {
                Debug.LogError("[DSH Assistant] Не найден Tools/DSHBridge/run.bat рядом с папкой Assets.");
                return;
            }

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = bridgePath,
                WorkingDirectory = Path.GetDirectoryName(bridgePath),
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(startInfo);
            Debug.Log("[DSH Assistant] Запускаю мост: " + bridgePath);
        }

        [MenuItem("Tools/DSH Assistant/Открыть папку моста", false, 24)]
        private static void RevealBridge()
        {
            string bridgePath = FindBridgeScript();
            if (bridgePath == null)
            {
                Debug.LogError("[DSH Assistant] Не найден Tools/DSHBridge/run.bat рядом с папкой Assets.");
                return;
            }

            EditorUtility.RevealInFinder(bridgePath);
        }

        [MenuItem("Tools/DSH Assistant/Открыть инструкцию", false, 25)]
        private static void OpenDocumentation()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string docsPath = Path.Combine(projectRoot, "DSH_ASSISTANT.md");
            if (!File.Exists(docsPath))
            {
                Debug.LogWarning("[DSH Assistant] Файл DSH_ASSISTANT.md не найден.");
                return;
            }

            EditorUtility.OpenWithDefaultApp(docsPath);
        }

        private static string FindBridgeScript()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string bridgePath = Path.Combine(projectRoot, "Tools", "DSHBridge", "run.bat");
            return File.Exists(bridgePath) ? bridgePath : null;
        }

        private static DSHAssistantSettings FindSettingsAsset()
        {
            string[] guids = AssetDatabase.FindAssets("t:DSHAssistantSettings");
            if (guids == null || guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<DSHAssistantSettings>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        // ------------------------------------------------------------------ жизненный цикл

        private void OnEnable()
        {
            baseUrl = EditorPrefs.GetString(PrefBaseUrl, baseUrl);
            model = EditorPrefs.GetString(PrefModel, model);
            apiKey = EditorPrefs.GetString(PrefApiKey, apiKey);
            includeContext = EditorPrefs.GetBool(PrefContext, includeContext);
            systemPrompt = EditorPrefs.GetString(PrefPrompt, systemPrompt);

            transientSettings = CreateInstance<DSHAssistantSettings>();
            transientSettings.hideFlags = HideFlags.HideAndDontSave;

            EditorApplication.update += PumpMainThread;
        }

        private void OnDisable()
        {
            EditorApplication.update -= PumpMainThread;

            EditorPrefs.SetString(PrefBaseUrl, baseUrl);
            EditorPrefs.SetString(PrefModel, model);
            EditorPrefs.SetString(PrefApiKey, apiKey);
            EditorPrefs.SetBool(PrefContext, includeContext);
            EditorPrefs.SetString(PrefPrompt, systemPrompt);

            if (pendingRequest != null)
            {
                pendingRequest.Cancel();
                pendingRequest.Dispose();
                pendingRequest = null;
            }

            if (transientSettings != null)
            {
                DestroyImmediate(transientSettings);
                transientSettings = null;
            }
        }

        private void PumpMainThread()
        {
            if (mainThreadQueue.Count > 0)
            {
                while (mainThreadQueue.Count > 0)
                    mainThreadQueue.Dequeue()?.Invoke();
                Repaint();
            }
            else if (busy)
            {
                Repaint();
            }
        }

        private void Enqueue(Action action)
        {
            if (action != null)
                mainThreadQueue.Enqueue(action);
        }

        // ------------------------------------------------------------------ запрос

        private async void Send()
        {
            if (busy) return;

            string text = (input ?? string.Empty).Trim();
            if (text.Length == 0) return;

            if (transientSettings == null)
            {
                status = "Окно не инициализировано";
                return;
            }

            transientSettings.baseUrl = baseUrl;
            transientSettings.model = model;
            transientSettings.apiKey = apiKey;
            transientSettings.systemPrompt = systemPrompt;
            transientSettings.includeSceneContext = includeContext;
            transientSettings.temperature = 0.7f;
            transientSettings.maxTokens = 2048;
            transientSettings.timeoutSeconds = 120;

            var outgoing = new List<DSHChatMessage>(history.Count + 2)
            {
                new DSHChatMessage("system", BuildSystemPrompt())
            };

            int skip = Mathf.Max(0, history.Count - 20);
            for (int i = skip; i < history.Count; i++)
                outgoing.Add(history[i]);
            outgoing.Add(new DSHChatMessage("user", text));

            history.Add(new DSHChatMessage("user", text));
            input = string.Empty;
            busy = true;
            status = "Запрос отправлен…";
            Repaint();

            pendingRequest = new CancellationTokenSource();

            try
            {
                string answer = await DSHApiClient.SendChatAsync(
                    transientSettings, outgoing.ToArray(), pendingRequest.Token);

                Enqueue(() =>
                {
                    history.Add(new DSHChatMessage("assistant", answer));
                    lastAnswer = answer;
                    status = "Готово";
                    busy = false;
                    scroll = Vector2.zero;
                });
            }
            catch (OperationCanceledException)
            {
                Enqueue(() => { status = "Запрос отменён"; busy = false; });
            }
            catch (Exception e)
            {
                string message = e.Message;
                Enqueue(() =>
                {
                    history.Add(new DSHChatMessage("assistant", "⚠ " + message));
                    status = "Ошибка";
                    busy = false;
                });
            }
            finally
            {
                Enqueue(() =>
                {
                    if (pendingRequest != null)
                    {
                        pendingRequest.Dispose();
                        pendingRequest = null;
                    }
                });
            }
        }

        private string BuildSystemPrompt()
        {
            string prompt = string.IsNullOrWhiteSpace(systemPrompt) ? DefaultPrompt : systemPrompt;
            if (includeContext)
                prompt += "\n\n" + DSHSceneContext.Build();
            return prompt;
        }

        private string BuildTranscript()
        {
            if (history.Count == 0)
                return "<color=#808080>Пусто. Спросите про сцену, скрипт или ассет.</color>";

            var sb = new StringBuilder(2048);
            foreach (DSHChatMessage message in history)
            {
                if (message.role == "user")
                    sb.Append("<b>Вы</b>\n").Append(message.content).Append("\n\n");
                else
                    sb.Append("<b>Ассистент</b>\n").Append(message.content).Append("\n\n");
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ GUI

        private void OnGUI()
        {
            if (transcriptStyle == null)
            {
                transcriptStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
                {
                    richText = true,
                    fontSize = 12,
                    padding = new RectOffset(6, 6, 4, 4)
                };
            }

            DrawConnectionHeader();

            EditorGUILayout.Space(4f);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(busy ? "● Работает…" : "○ " + status, EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(busy))
                {
                    if (GUILayout.Button("Копировать", EditorStyles.toolbarButton, GUILayout.Width(86f)) &&
                        !string.IsNullOrEmpty(lastAnswer))
                    {
                        EditorGUIUtility.systemCopyBuffer = lastAnswer;
                        status = "Ответ скопирован";
                    }

                    if (GUILayout.Button("Очистить", EditorStyles.toolbarButton, GUILayout.Width(74f)))
                    {
                        history.Clear();
                        lastAnswer = string.Empty;
                        status = "История очищена";
                    }
                }
            }

            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
            string transcript = BuildTranscript();
            float height = transcriptStyle.CalcHeight(
                new GUIContent(transcript), Mathf.Max(200f, position.width - 40f));
            EditorGUILayout.SelectableLabel(transcript, transcriptStyle, GUILayout.Height(height + 8f));
            EditorGUILayout.EndScrollView();

            DrawInputArea();
        }

        private void DrawConnectionHeader()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("API", GUILayout.Width(34f));
                    baseUrl = EditorGUILayout.TextField(baseUrl);
                    GUILayout.Label("Модель", GUILayout.Width(56f));
                    model = EditorGUILayout.TextField(model, GUILayout.Width(160f));
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Ключ", GUILayout.Width(34f));
                    apiKey = EditorGUILayout.PasswordField(apiKey);
                    includeContext = GUILayout.Toggle(includeContext, "Контекст сцены", GUILayout.Width(130f));
                    showPrompt = GUILayout.Toggle(showPrompt, "Промпт", GUILayout.Width(70f));
                }

                if (showPrompt)
                {
                    systemPrompt = EditorGUILayout.TextArea(systemPrompt, GUILayout.MinHeight(54f));
                }
            }
        }

        private void DrawInputArea()
        {
            Event current = Event.current;

            if (current.type == EventType.KeyDown &&
                (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter) &&
                !current.shift && GUI.GetNameOfFocusedControl() == InputControlName &&
                !busy)
            {
                current.Use();
                Send();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.SetNextControlName(InputControlName);
                input = EditorGUILayout.TextArea(input ?? string.Empty, GUILayout.MinHeight(52f));

                using (new EditorGUI.DisabledScope(busy || string.IsNullOrWhiteSpace(input)))
                {
                    if (GUILayout.Button("Отправить\n(Enter)", GUILayout.Width(96f), GUILayout.Height(52f)))
                        Send();
                }
            }

            GUILayout.Label("Enter — отправить, Shift+Enter — перенос строки", EditorStyles.miniLabel);
        }
    }
}
