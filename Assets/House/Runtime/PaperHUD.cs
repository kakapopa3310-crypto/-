using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace PaperHouse
{
    /// <summary>Минимальный HUD: прицел, подсказка, счётчик бумаги и управление.</summary>
    public class PaperHUD : MonoBehaviour
    {
        private static readonly Color Ink = new Color(0.95f, 0.92f, 0.84f, 1f);
        private static readonly Color Panel = new Color(0.09f, 0.08f, 0.07f, 0.72f);

        private GameObject promptPanel;
        private GameObject startPanel;
        private GameObject toastPanel;
        private Text promptText;
        private Text counterText;
        private Text hintText;
        private Text toastText;
        private Coroutine toastRoutine;

        private void Awake()
        {
            Build();
        }

        public void SetStarted(bool started)
        {
            SetStartPanelVisible(!started);
        }

        public void SetStartPanelVisible(bool visible)
        {
            if (startPanel != null)
                startPanel.SetActive(visible);
        }

        public void ShowToast(string text, float seconds = 3.5f)
        {
            if (toastPanel == null || toastText == null) return;

            if (toastRoutine != null)
                StopCoroutine(toastRoutine);

            toastPanel.SetActive(true);
            toastText.text = text;
            toastRoutine = StartCoroutine(HideToast(seconds));
        }

        private IEnumerator HideToast(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (toastPanel != null) toastPanel.SetActive(false);
            toastRoutine = null;
        }

        public void SetPrompt(string text)
        {
            if (promptPanel == null || promptText == null) return;

            bool show = !string.IsNullOrEmpty(text);
            promptPanel.SetActive(show);
            promptText.text = show ? text : string.Empty;
        }

        public void SetCounters(bool hasPaper, int placed)
        {
            if (counterText == null) return;

            string inHand = hasPaper ? "1 лист в руках" : "руки пусты";
            string onMat = placed == 1 ? "1 лист" : placed + " листов";
            int fold = Mathf.RoundToInt(PlayerStats.FoldAmount * 100f);
            counterText.text = "БУМАГА\n" +
                               inHand + "  ·  на коврике: " + onMat + "\n" +
                               "Деньги: $" + PlayerStats.Money +
                               "  ·  сила: " + PlayerStats.StrengthLevel +
                               " (" + fold + "%)";
        }

        public void SetHint(string text)
        {
            if (hintText != null)
                hintText.text = text;
        }

        private void Build()
        {
            var canvasObject = new GameObject("Paper HUD", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            CreateCrosshair(canvasObject.transform);

            counterText = CreateText("Counter", canvasObject.transform, 17, Ink, TextAnchor.UpperLeft);
            SetRect(counterText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(26f, -116f), new Vector2(650f, -22f));

            hintText = CreateText("Hint", canvasObject.transform, 13, new Color(0.86f, 0.83f, 0.76f, 0.8f),
                TextAnchor.LowerLeft);
            SetRect(hintText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(26f, 20f), new Vector2(1180f, 62f));

            promptPanel = CreatePanel("Prompt Panel", canvasObject.transform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 84f),
                new Vector2(780f, 52f), Panel);
            promptText = CreateText("Prompt", promptPanel.transform, 18, Ink, TextAnchor.MiddleCenter);
            SetRect(promptText.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(12f, 4f), new Vector2(-12f, -4f));
            promptPanel.SetActive(false);

            toastPanel = CreatePanel("Toast Panel", canvasObject.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -64f),
                new Vector2(940f, 48f), Panel);
            toastText = CreateText("Toast", toastPanel.transform, 18, Ink, TextAnchor.MiddleCenter);
            SetRect(toastText.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(12f, 4f), new Vector2(-12f, -4f));
            toastPanel.SetActive(false);

            // Стартовая карточка. В Unity клик по вкладке Game сначала
            // передаёт фокус игре — без этого WASD остаётся редактору.
            startPanel = CreatePanel("Start Panel", canvasObject.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(860f, 190f), new Color(0.07f, 0.065f, 0.055f, 0.92f));

            Text startTitle = CreateText("Start Title", startPanel.transform, 26, Ink,
                TextAnchor.MiddleCenter);
            startTitle.text = "КЛИКНИТЕ В ОКНЕ GAME";
            SetRect(startTitle.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 1f),
                new Vector2(20f, -8f), new Vector2(-20f, -18f));

            Text startHint = CreateText("Start Hint", startPanel.transform, 17,
                new Color(0.86f, 0.81f, 0.70f, 1f), TextAnchor.MiddleCenter);
            startHint.text = "чтобы захватить мышь и ходить\n\n" +
                             "WASD — идти · мышь — смотреть · E — взаимодействовать";
            SetRect(startHint.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.55f),
                new Vector2(20f, 16f), new Vector2(-20f, -4f));
        }

        private static void CreateCrosshair(Transform parent)
        {
            var horizontal = CreatePanel("Crosshair H", parent,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(12f, 2f), new Color(1f, 1f, 1f, 0.8f));
            var vertical = CreatePanel("Crosshair V", parent,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(2f, 12f), new Color(1f, 1f, 1f, 0.8f));
            horizontal.GetComponent<Image>().raycastTarget = false;
            vertical.GetComponent<Image>().raycastTarget = false;
        }

        private static GameObject CreatePanel(string name, Transform parent, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return go;
        }

        private static Text CreateText(string name, Transform parent, int fontSize, Color color,
            TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = GetFont();
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
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
