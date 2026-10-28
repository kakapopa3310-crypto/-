using UnityEngine;

namespace PaperHouse
{
    /// <summary>
    /// Зелёный бак за домом. Принимает бумагу только из рук игрока:
    /// что держишь — то и сдаёшь. С коврика и со стола не продаёт.
    /// </summary>
    public class TrashBinInteractable : InteractableBase
    {
        public AudioSource audioSource;
        public AudioClip sellClip;

        public override string GetPrompt(PlayerInteractor player)
        {
            if (player != null && player.HasPaper)
                return "E — сдать лист за $" + player.CarriedValue;

            return "Бак для бумаги — сложите лист и возьмите его в руки";
        }

        public override void Interact(PlayerInteractor player)
        {
            if (player == null || !player.HasPaper) return;

            int value = player.CarriedValue;
            PlayerStats.Money += value;

            player.ConsumePaper();

            if (audioSource != null && sellClip != null)
                AudioService.PlaySfx(audioSource, sellClip, 0.9f);

            if (player.hud != null)
                player.hud.ShowToast("Сдано за $" + value, 2.5f);

            player.RefreshHud();
        }
    }
}
