using UnityEngine;

namespace PaperHouse
{
    /// <summary>Мусорка за домом: сдаёт всю бумагу с коврика за деньги.</summary>
    public class TrashBinInteractable : InteractableBase
    {
        public PaperMatInteractable mat;
        public AudioSource audioSource;
        public AudioClip sellClip;

        public override string GetPrompt(PlayerInteractor player)
        {
            if (mat == null) return "Мусорка";

            if (mat.PlacedCount == 0)
                return "Мусорка пуста — принесите сложенную бумагу";

            return "E — сдать " + mat.PlacedCount + " лист(ов) за $" + mat.PlacedValue;
        }

        public override void Interact(PlayerInteractor player)
        {
            if (mat == null || mat.PlacedCount == 0) return;

            int value = mat.PlacedValue;
            PlayerStats.Money += value;
            mat.ClearPlaced();

            if (audioSource != null && sellClip != null)
                AudioService.PlaySfx(audioSource, sellClip, 0.9f);

            if (player != null) player.RefreshHud();
        }
    }
}
