using UnityEngine;

namespace PaperHouse
{
    /// <summary>Музыка, эмбиент и общий коэффициент громкости эффектов.</summary>
    public class AudioService : MonoBehaviour
    {
        public AudioSource musicSource;
        public AudioSource ambientSource;

        private void Awake()
        {
            GameSettings.Changed += Apply;
            Apply();

            if (musicSource != null)
            {
                musicSource.loop = true;
                if (!musicSource.isPlaying)
                    musicSource.Play();
            }
        }

        private void OnDestroy()
        {
            GameSettings.Changed -= Apply;
        }

        public void Apply()
        {
            if (musicSource != null)
                musicSource.volume = GameSettings.MusicVolume;

            if (ambientSource != null)
                ambientSource.volume = GameSettings.SfxVolume * 0.12f;
        }

        public static void PlaySfx(AudioSource source, AudioClip clip, float volume = 1f)
        {
            if (source == null || clip == null) return;
            source.PlayOneShot(clip, volume * GameSettings.SfxVolume);
        }
    }
}
