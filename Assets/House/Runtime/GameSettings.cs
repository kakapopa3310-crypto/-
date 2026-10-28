using System;
using UnityEngine;

namespace PaperHouse
{
    /// <summary>Пользовательские настройки: музыка, эффекты, чувствительность мыши.</summary>
    public static class GameSettings
    {
        private const string MusicKey = "PaperHouse.Music";
        private const string SfxKey = "PaperHouse.Sfx";
        private const string SensitivityKey = "PaperHouse.Sensitivity";

        public const float DefaultMusic = 0.35f;
        public const float DefaultSfx = 0.80f;
        public const float DefaultSensitivity = 0.11f;

        private static float music = PlayerPrefs.GetFloat(MusicKey, DefaultMusic);
        private static float sfx = PlayerPrefs.GetFloat(SfxKey, DefaultSfx);
        private static float sensitivity = PlayerPrefs.GetFloat(SensitivityKey, DefaultSensitivity);

        public static event Action Changed;

        public static float MusicVolume
        {
            get { return music; }
        }

        public static float SfxVolume
        {
            get { return sfx; }
        }

        public static float MouseSensitivity
        {
            get { return sensitivity; }
        }

        public static void SetMusic(float value)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(music, value)) return;
            music = value;
            PlayerPrefs.SetFloat(MusicKey, music);
            Changed?.Invoke();
        }

        public static void SetSfx(float value)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(sfx, value)) return;
            sfx = value;
            PlayerPrefs.SetFloat(SfxKey, sfx);
            Changed?.Invoke();
        }

        public static void SetMouseSensitivity(float value)
        {
            value = Mathf.Clamp(value, 0.02f, 0.40f);
            if (Mathf.Approximately(sensitivity, value)) return;
            sensitivity = value;
            PlayerPrefs.SetFloat(SensitivityKey, sensitivity);
            Changed?.Invoke();
        }

        public static void ResetAll()
        {
            music = DefaultMusic;
            sfx = DefaultSfx;
            sensitivity = DefaultSensitivity;
            PlayerPrefs.SetFloat(MusicKey, music);
            PlayerPrefs.SetFloat(SfxKey, sfx);
            PlayerPrefs.SetFloat(SensitivityKey, sensitivity);
            Changed?.Invoke();
        }

        public static void Save()
        {
            PlayerPrefs.Save();
        }
    }
}
