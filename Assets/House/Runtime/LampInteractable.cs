using UnityEngine;

namespace PaperHouse
{
    /// <summary>Настольная лампа: E включает и выключает свет.</summary>
    public class LampInteractable : InteractableBase
    {
        public Light lampLight;
        public Renderer bulbRenderer;

        [Header("Звук")]
        public AudioSource audioSource;
        public AudioClip clickClip;

        private bool isOn = true;

        private void Awake()
        {
            ApplyState();
        }

        public override string GetPrompt(PlayerInteractor player)
        {
            return isOn ? "E — выключить лампу" : "E — включить лампу";
        }

        public override void Interact(PlayerInteractor player)
        {
            isOn = !isOn;
            ApplyState();

            if (audioSource != null && clickClip != null)
                AudioService.PlaySfx(audioSource, clickClip, 0.8f);
        }

        private void ApplyState()
        {
            if (lampLight != null)
                lampLight.enabled = isOn;
            if (bulbRenderer != null)
                bulbRenderer.enabled = isOn;
        }
    }
}
