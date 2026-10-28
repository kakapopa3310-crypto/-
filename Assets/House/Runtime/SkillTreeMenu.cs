using UnityEngine;

namespace PaperHouse
{
    /// <summary>Экран ноутбука: древо силы, скорости и эффективности.</summary>
    public class SkillTreeMenu : MonoBehaviour
    {
        public FirstPersonController player;
        public PaperHUD hud;
        public AudioSource audioSource;
        public AudioClip clickClip;

        private bool open;
        private Texture2D dim;
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle valueStyle;
        private GUIStyle buttonStyle;
        private GUIStyle hintStyle;

        public bool IsOpen
        {
            get { return open; }
        }

        private void Awake()
        {
            if (player == null) player = GetComponent<FirstPersonController>();
            if (hud == null) hud = GetComponent<PaperHUD>();
            dim = MakeTexture(new Color(0.01f, 0.02f, 0.04f, 0.82f));
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

            if (audioSource != null && clickClip != null)
                AudioService.PlaySfx(audioSource, clickClip, 0.8f);
        }

        public void Close()
        {
            if (!open) return;

            open = false;
            Time.timeScale = 1f;
            if (player != null) player.BeginPlay();

            if (audioSource != null && clickClip != null)
                AudioService.PlaySfx(audioSource, clickClip, 0.8f);
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

            var panel = new Rect(440f, 210f, 1040f, 660f);
            GUI.Box(panel, "НОУТБУК · ДРЕВО ПРОКАЧКИ", titleStyle);

            GUI.Label(new Rect(520f, 285f, 700f, 38f),
                "Деньги: $" + PlayerStats.Money + "   ·   сила — ровнее складка и дальше бросок",
                labelStyle);

            DrawSkillRow(SkillType.Strength, 360f, "СИЛА",
                "складка " + Mathf.RoundToInt(PlayerStats.FoldQuality * 100f) + "%" +
                " · бросок " + PlayerStats.ThrowDistance.ToString("0.0") + " м",
                new Color(0.80f, 0.55f, 0.34f));

            DrawSkillRow(SkillType.Speed, 470f, "СКОРОСТЬ",
                "+" + Mathf.RoundToInt((PlayerStats.SpeedMultiplier - 1f) * 100f) + "% к ходьбе",
                new Color(0.38f, 0.68f, 0.78f));

            DrawSkillRow(SkillType.Efficiency, 580f, "ЭФФЕКТИВНОСТЬ",
                "бумага стоит $" + PlayerStats.PaperValue + " (+$3 за сгиб)",
                new Color(0.52f, 0.72f, 0.42f));

            if (GUI.Button(new Rect(520f, 785f, 260f, 52f), "Закрыть ноутбук", buttonStyle))
                Close();

            GUI.Label(new Rect(440f, 830f, 1040f, 30f),
                "Esc — закрыть ноутбук", hintStyle);

            GUI.matrix = previous;
        }

        private void DrawSkillRow(SkillType skill, float y, string name, string effect, Color accent)
        {
            int level = PlayerStats.GetLevel(skill);
            int cost = PlayerStats.UpgradeCost(skill);

            var row = new Rect(500f, y, 920f, 92f);
            GUI.Box(row, GUIContent.none);

            var nameStyle = new GUIStyle(labelStyle)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = accent }
            };
            GUI.Label(new Rect(520f, y + 12f, 330f, 34f), name + "  ур. " + level, nameStyle);
            GUI.Label(new Rect(520f, y + 48f, 540f, 32f), effect, valueStyle);

            bool canBuy = PlayerStats.Money >= cost;
            bool previousEnabled = GUI.enabled;
            GUI.enabled = canBuy;
            if (GUI.Button(new Rect(1120f, y + 20f, 260f, 52f),
                    "Улучшить — $" + cost, buttonStyle))
            {
                if (PlayerStats.TryUpgrade(skill))
                {
                    if (audioSource != null && clickClip != null)
                        AudioService.PlaySfx(audioSource, clickClip, 0.8f);
                }
            }
            GUI.enabled = previousEnabled;
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;

            Color ink = new Color(0.92f, 0.94f, 0.90f);
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = ink }
            };
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = ink }
            };
            valueStyle = new GUIStyle(labelStyle)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleLeft
            };
            hintStyle = new GUIStyle(labelStyle)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.62f, 0.67f, 0.60f) }
            };
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 18
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
