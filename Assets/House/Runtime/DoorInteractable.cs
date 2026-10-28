using UnityEngine;

namespace PaperHouse
{
    /// <summary>Дверь на петле: E открывает и закрывает её.</summary>
    public class DoorInteractable : InteractableBase
    {
        public Transform pivot;
        public float openAngle = -105f;
        public float speed = 220f;

        [Header("Звук")]
        public AudioSource audioSource;
        public AudioClip openClip;
        public AudioClip closeClip;

        private bool isOpen;
        private float targetAngle;

        private void Awake()
        {
            if (pivot == null) pivot = transform;
        }

        public override string GetPrompt(PlayerInteractor player)
        {
            return isOpen ? "E — закрыть дверь" : "E — открыть дверь";
        }

        public override void Interact(PlayerInteractor player)
        {
            isOpen = !isOpen;
            targetAngle = isOpen ? openAngle : 0f;

            if (audioSource != null)
            {
                AudioClip clip = isOpen ? openClip : closeClip;
                if (clip != null)
                    AudioService.PlaySfx(audioSource, clip);
            }
        }

        private void Update()
        {
            if (pivot == null) return;
            Quaternion target = Quaternion.Euler(0f, targetAngle, 0f);
            pivot.localRotation = Quaternion.RotateTowards(pivot.localRotation, target, speed * Time.deltaTime);
        }
    }
}
