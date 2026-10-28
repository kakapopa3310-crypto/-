using UnityEngine;

namespace PaperHouse
{
    /// <summary>Пауза и настройки: музыка, эффекты, чувствительность мыши.</summary>
    public class SettingsMenu : MonoBehaviour
    {
        public FirstPersonController player;
        public PaperHUD hud;

        private bool open;
        private Texture2D dim;
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle valueStyle;
        private GUIStyle hintStyle;
        private GUIStyle buttonStyle;

        public bool IsOpen
        {
            get { return open; }
        }

        private void Awake()
        {
            if (player == null) player = GetComponent<FirstPersonController>();
            if (hud == null) hud = GetComponent<PaperHUD>();
            dim = MakeTexture(new Color(0f, 0f, 0f, 0.72f));
        }

        private void OnDestroy()
        {
            if (open) Time.timeScale = 1f;
            if (dim != null) Destroy(dim);
        }

        public void Open()
        {
            if (open) return;

            open = true;
            Time.timeScale = 0f;
            if (player != null) player.PauseForMenu();
            if (hud != null) hud.SetStartPanelVisible(false);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Close()
        {
            if (!open) return;

            open = false;
            Time.timeScale = 1f;
            GameSettings.Save();
            if (player != null) player.BeginPlay();
        }

        private void OnGUI()
        {
            if (!open) return;

            EnsureStyles();

            Matrix4x4 previous = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            GUI.depth = -200;

            GUI.DrawTexture(new Rect(0f, 0f, 1920f, 1080f), dim);

            var panel = new Rect(540f, 270f, 840f, 540f);
            GUI.Box(panel, "НАСТРОЙКИ", titleStyle);

            GUI.Label(new Rect(620f, 360f, 200f, 36f), "Музыка", labelStyle);
            float music = GUI.HorizontalSlider(new Rect(830f, 370f, 330f, 24f),
                GameSettings.MusicVolume, 0f, 1f);
            if (!Mathf.Approximately(music, GameSettings.MusicVolume))
                GameSettings.SetMusic(music);
            GUI.Label(new Rect(1180f, 360f, 100f, 36f),
                Mathf.RoundToInt(GameSettings.MusicVolume * 100f) + "%", valueStyle);

            GUI.Label(new Rect(620f, 430f, 200f, 36f), "Эффекты", labelStyle);
            float sfx = GUI.HorizontalSlider(new Rect(830f, 440f, 330f, 24f),
                GameSettings.SfxVolume, 0f, 1f);
            if (!Mathf.Approximately(sfx, GameSettings.SfxVolume))
                GameSettings.SetSfx(sfx);
            GUI.Label(new Rect(1180f, 430f, 100f, 36f),
                Mathf.RoundToInt(GameSettings.SfxVolume * 100f) + "%", valueStyle);

            GUI.Label(new Rect(620f, 500f, 210f, 36f), "Мышь", labelStyle);
            float sensitivity = GUI.HorizontalSlider(new Rect(830f, 510f, 330f, 24f),
                GameSettings.MouseSensitivity, 0.02f, 0.40f);
            if (!Mathf.Approximately(sensitivity, GameSettings.MouseSensitivity))
                GameSettings.SetMouseSensitivity(sensitivity);
            GUI.Label(new Rect(1180f, 500f, 100f, 36f),
                GameSettings.MouseSensitivity.ToString("0.00"), valueStyle);

            if (GUI.Button(new Rect(700f, 620f, 260f, 52f), "Продолжить", buttonStyle))
                Close();

            if (GUI.Button(new Rect(980f, 620f, 200f, 52f), "Сбросить", buttonStyle))
                GameSettings.ResetAll();

            GUI.Label(new Rect(540f, 700f, 840f, 40f),
                "Esc — продолжить игру", hintStyle);

            GUI.matrix = previous;
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;

            Color ink = new Color(0.95f, 0.91f, 0.82f);
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 30,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = ink }
            };
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = ink }
            };
            valueStyle = new GUIStyle(labelStyle)
            {
                alignment = TextAnchor.MiddleRight
            };
            hintStyle = new GUIStyle(labelStyle)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.72f, 0.68f, 0.60f) }
            };
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 20
            };

            if (font != null)
            {
                titleStyle.font = font;
                labelStyle.font = font;
                valueStyle.font = font;
                hintStyle.font = font;
                buttonStyle.font = font;
            }
        }

        private static Texture2D MakeTexture(Color color)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}
