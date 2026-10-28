using UnityEngine;

namespace PaperHouse
{
    /// <summary>Бесконечная стопка бумаги: ровно один лист за одно нажатие.</summary>
    public class PaperStackInteractable : InteractableBase
    {
        public AudioSource audioSource;
        public AudioClip pickClip;

        public override string GetPrompt(PlayerInteractor player)
        {
            return player.HasPaper
                ? "В руках уже 1 лист — сначала положите его на коврик"
                : "E — взять 1 лист бумаги";
        }

        public override void Interact(PlayerInteractor player)
        {
            if (player.HasPaper)
                return;

            player.TakePaper();
            if (audioSource != null && pickClip != null)
                AudioService.PlaySfx(audioSource, pickClip, 0.8f);
        }
    }
}
