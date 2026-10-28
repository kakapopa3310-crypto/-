using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace DSH.Assistant
{
    /// <summary>
    /// Внутриигровой ассистент в виде бумажной записки. UI строится кодом —
    /// префабы и ссылки не нужны. F1 открывает и закрывает окно.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("DSH/Chat Overlay")]
    public class DSHChatOverlay : MonoBehaviour
    {
        // Палитра «бумага»: тёплый лист, тёмные чернила, красная пометка.
        private static readonly Color Paper = new Color(0.955f, 0.925f, 0.835f, 0.985f);
        private static readonly Color PaperLight = new Color(1f, 0.988f, 0.945f, 1f);
        private static readonly Color Ink = new Color(0.16f, 0.14f, 0.11f, 1f);
        private static readonly Color InkSoft = new Color(0.42f, 0.36f, 0.28f, 1f);
        private static readonly Color Accent = new Color(0.55f, 0.18f, 0.14f, 1f);
        private static readonly Color Rule = new Color(0.72f, 0.62f, 0.47f, 0.68f);

        [Tooltip("Настройки подключения. Если пусто — используются значения по умолчанию (локальный мост).")]
        [SerializeField] private DSHAssistantSettings settings;

        /// <summary>Настройки подключения; используется установщиком проекта.</summary>
        public DSHAssistantSettings Settings
        {
            get { return settings; }
            set { settings = value; }
        }

        [Tooltip("Показывать окно сразу при старте.")]
        [SerializeField] private bool visibleOnStart = true;

        [Tooltip("Сколько последних сообщений отправлять в модель (без системного).")]
        [SerializeField, Range(2, 80)] private int historyLimit = 20;

        private readonly List<DSHChatMessage> history = new List<DSHChatMessage>();
        private static DSHAssistantSettings fallbackSettings;

        private GameObject canvasObject;
        private GameObject panel;
        private ScrollRect scrollRect;
        private Text transcript;
        private InputField inputField;
        private Button sendButton;
        private Text statusLabel;

        private bool busy;
        private CancellationTokenSource pendingRequest;

        private const string WelcomeText =
            "<b><color=#8C2F25>DSH</color></b>\n" +
            "Привет. Я встроен в проект «бумага»: вижу активную сцену, выделенные объекты " +
            "и могу помочь с кодом, механиками и ассетами.\n\n" +
            "<color=#6B5B45>Спросите что-нибудь. F1 — свернуть записку.</color>";

        private void Awake()
        {
            BuildUi();
            SetVisible(visibleOnStart);
            SetStatus("Готов · " + ResolveSettings().baseUrl);
        }

        private void Update()
        {
            if (panel == null) return;
            if (TogglePressed())
                SetVisible(!panel.activeSelf);
        }

        private void OnDestroy()
        {
            if (pendingRequest != null)
            {
                pendingRequest.Cancel();
                pendingRequest.Dispose();
                pendingRequest = null;
            }

            // Canvas создаётся корневым объектом, поэтому удаляем его вручную.
            if (canvasObject != null)
            {
                Destroy(canvasObject);
                canvasObject = null;
            }
        }

        private bool TogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.f1Key.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.F1);
#endif
        }

        // ------------------------------------------------------------------ логика

        private DSHAssistantSettings ResolveSettings()
        {
            if (settings != null) return settings;

            if (fallbackSettings == null)
            {
                fallbackSettings = ScriptableObject.CreateInstance<DSHAssistantSettings>();
                fallbackSettings.name = "DSHAssistantSettings (runtime default)";
            }
            return fallbackSettings;
        }

        private async void Send()
        {
            if (busy) return;

            string text = inputField != null ? inputField.text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(text)) return;

            DSHAssistantSettings active = ResolveSettings();
            var outgoing = new List<DSHChatMessage>(history.Count + 2)
            {
                new DSHChatMessage("system", BuildSystemPrompt(active))
            };

            int skip = Mathf.Max(0, history.Count - Mathf.Max(2, historyLimit));
            for (int i = skip; i < history.Count; i++)
                outgoing.Add(history[i]);
            outgoing.Add(new DSHChatMessage("user", text));

            history.Add(new DSHChatMessage("user", text));
            RebuildTranscript();
            if (inputField != null) inputField.text = string.Empty;

            busy = true;
            ApplyBusyState();
            SetStatus("Запрос отправлен…");

            pendingRequest = new CancellationTokenSource();
            try
            {
                string answer = await DSHApiClient.SendChatAsync(active, outgoing.ToArray(), pendingRequest.Token);
                history.Add(new DSHChatMessage("assistant", answer));
                SetStatus("Готово");
            }
            catch (OperationCanceledException)
            {
                SetStatus("Запрос отменён");
            }
            catch (Exception e)
            {
                history.Add(new DSHChatMessage("assistant", "⚠ Ошибка: " + e.Message));
                SetStatus("Ошибка");
                Debug.LogWarning("[DSH Assistant] " + e);
            }
            finally
            {
                if (pendingRequest != null)
                {
                    pendingRequest.Dispose();
                    pendingRequest = null;
                }

                busy = false;
                ApplyBusyState();
                RebuildTranscript();

                if (inputField != null && inputField.gameObject.activeInHierarchy)
                    inputField.ActivateInputField();
            }
        }

        private static string BuildSystemPrompt(DSHAssistantSettings active)
        {
            string prompt = string.IsNullOrWhiteSpace(active.systemPrompt)
                ? "Ты — DSH, ассистент Unity-проекта «бумага»."
                : active.systemPrompt;

            if (active.includeSceneContext)
                prompt += "\n\n" + DSHSceneContext.Build();

            return prompt;
        }

        private void RebuildTranscript()
        {
            if (transcript == null) return;

            var sb = new StringBuilder(2048);
            foreach (DSHChatMessage message in history)
            {
                if (message.role == "user")
                    sb.Append("<b><color=#8C2F25>Вы</color></b>\n").Append(Escape(message.content)).Append("\n\n");
                else
                    sb.Append("<b><color=#3E5A3A>DSH</color></b>\n").Append(Escape(message.content)).Append("\n\n");
            }

            transcript.text = sb.Length == 0 ? WelcomeText : sb.ToString();

            Canvas.ForceUpdateCanvases();
            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = 0f;
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            // Заменяем "<" на визуально похожий символ, чтобы текст модели
            // не интерпретировался как rich text.
            return value.Replace("<", "\u2039");
        }

        private void ApplyBusyState()
        {
            if (sendButton != null) sendButton.interactable = !busy;
            if (inputField != null) inputField.interactable = !busy;
        }

        private void SetStatus(string text)
        {
            if (statusLabel != null)
                statusLabel.text = text;
        }

        private void SetVisible(bool visible)
        {
            if (panel != null)
                panel.SetActive(visible);
        }

        // ------------------------------------------------------------------ UI

        private void BuildUi()
        {
            EnsureEventSystem();

            // Canvas — корневой объект: дочерний унаследовал бы масштаб родителя
            // и UI поехал бы.
            canvasObject = new GameObject(
                "DSH Assistant Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Лист бумаги.
            panel = new GameObject("Paper Note", typeof(RectTransform), typeof(Image));
            var panelRect = (RectTransform)panel.transform;
            panelRect.SetParent(canvasObject.transform, false);
            panelRect.anchorMin = new Vector2(1f, 0f);
            panelRect.anchorMax = new Vector2(1f, 0f);
            panelRect.pivot = new Vector2(1f, 0f);
            panelRect.sizeDelta = new Vector2(600f, 470f);
            panelRect.anchoredPosition = new Vector2(-30f, 30f);
            panel.GetComponent<Image>().color = Paper;

            var shadow = panel.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.08f, 0.06f, 0.04f, 0.32f);
            shadow.effectDistance = new Vector2(7f, -7f);

            // Красная линия на полях — как в школьной тетради.
            var ruleObject = new GameObject("Margin Rule", typeof(RectTransform), typeof(Image));
            var ruleRect = (RectTransform)ruleObject.transform;
            ruleRect.SetParent(panelRect, false);
            SetRect(ruleRect, new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(22f, 74f), new Vector2(24f, -56f));
            ruleObject.GetComponent<Image>().color = Rule;
            ruleObject.GetComponent<Image>().raycastTarget = false;

            // Заголовок.
            Text title = CreateText("Title", panelRect, 22, Ink);
            title.text = "<b>DSH</b>  <size=12><color=#8C5A3C>бумажный ассистент</color></size>";
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(40f, -54f), new Vector2(-38f, -12f));

            Text hint = CreateText("Hint", panelRect, 11, InkSoft);
            hint.alignment = TextAnchor.MiddleRight;
            hint.text = "F1 — свернуть";
            SetRect(hint.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-130f, -48f), new Vector2(-38f, -20f));

            // Статус.
            statusLabel = CreateText("Status", panelRect, 11, InkSoft);
            SetRect(statusLabel.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(40f, 8f), new Vector2(-24f, 28f));

            // Область истории.
            var scrollObject = new GameObject("Scroll", typeof(RectTransform), typeof(Image));
            var scrollRectTransform = (RectTransform)scrollObject.transform;
            scrollRectTransform.SetParent(panelRect, false);
            SetRect(scrollRectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(24f, 94f), new Vector2(-24f, -62f));
            scrollObject.GetComponent<Image>().color = new Color(0.42f, 0.33f, 0.20f, 0.055f);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            var viewportRect = (RectTransform)viewport.transform;
            viewportRect.SetParent(scrollRectTransform, false);
            SetRect(viewportRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var contentRect = (RectTransform)content.transform;
            contentRect.SetParent(viewportRect, false);
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);

            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 4f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            transcript = CreateText("Transcript", contentRect, 14, Ink);
            transcript.alignment = TextAnchor.UpperLeft;
            transcript.horizontalOverflow = HorizontalWrapMode.Wrap;
            transcript.verticalOverflow = VerticalWrapMode.Overflow;
            transcript.supportRichText = true;

            scrollRect = scrollObject.AddComponent<ScrollRect>();
            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 30f;

            // Поле ввода — как строчка на листе.
            var inputObject = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(InputField));
            var inputRect = (RectTransform)inputObject.transform;
            inputRect.SetParent(panelRect, false);
            SetRect(inputRect, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(40f, 38f), new Vector2(-110f, 88f));

            var inputBackground = inputObject.GetComponent<Image>();
            inputBackground.color = PaperLight;

            Text inputText = CreateText("Text", inputRect, 14, Ink);
            inputText.supportRichText = false;
            inputText.alignment = TextAnchor.MiddleLeft;
            SetRect(inputText.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(10f, 2f), new Vector2(-10f, -2f));

            Text placeholder = CreateText("Placeholder", inputRect, 14, new Color(0.42f, 0.36f, 0.28f, 0.5f));
            placeholder.text = "Спросите DSH…  Enter — отправить";
            placeholder.alignment = TextAnchor.MiddleLeft;
            SetRect(placeholder.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(10f, 2f), new Vector2(-10f, -2f));

            inputField = inputObject.GetComponent<InputField>();
            inputField.targetGraphic = inputBackground;
            inputField.textComponent = inputText;
            inputField.placeholder = placeholder;
            inputField.lineType = InputField.LineType.SingleLine;
            inputField.onSubmit.AddListener(_ => Send());

            // Кнопка отправки — сургучная печать.
            var buttonObject = new GameObject("Send", typeof(RectTransform), typeof(Image), typeof(Button));
            var buttonRect = (RectTransform)buttonObject.transform;
            buttonRect.SetParent(panelRect, false);
            SetRect(buttonRect, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-100f, 38f), new Vector2(-24f, 88f));

            var buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.color = Accent;

            sendButton = buttonObject.GetComponent<Button>();
            sendButton.targetGraphic = buttonImage;
            sendButton.onClick.AddListener(Send);

            Text buttonLabel = CreateText("Label", buttonRect, 14, PaperLight);
            buttonLabel.text = "→";
            buttonLabel.fontSize = 22;
            buttonLabel.alignment = TextAnchor.MiddleCenter;
            SetRect(buttonLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            RebuildTranscript();
        }

        private static void EnsureEventSystem()
        {
            EventSystem existing = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (existing != null)
            {
#if ENABLE_INPUT_SYSTEM
                if (existing.GetComponent<InputSystemUIInputModule>() == null)
                    existing.gameObject.AddComponent<InputSystemUIInputModule>();
#endif
                return;
            }

            var eventSystemObject = new GameObject("DSH EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
#else
            eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
        }

        private static Text CreateText(string name, Transform parent, int fontSize, Color color)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);

            var text = textObject.GetComponent<Text>();
            text.font = GetFont();
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            if (text.font == null)
                text.text = "<font unavailable>";
            return text;
        }

        private static Font cachedFont;

        private static Font GetFont()
        {
            if (cachedFont != null) return cachedFont;

            cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (cachedFont == null)
                cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return cachedFont;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
