using UnityEngine;

namespace PaperHouse
{
    /// <summary>Ноутбук на столе открывает древо прокачки.</summary>
    public class LaptopInteractable : InteractableBase
    {
        public SkillTreeMenu skillTree;
        public FirstPersonController controller;
        public AudioSource audioSource;
        public AudioClip clickClip;

        public override string GetPrompt(PlayerInteractor player)
        {
            if (skillTree == null) return "Ноутбук";
            if (controller == null || !controller.IsSeated)
                return "Сначала сядьте на стул";
            return skillTree.IsOpen ? "E — закрыть ноутбук" : "E — открыть ноутбук";
        }

        public override void Interact(PlayerInteractor player)
        {
            if (skillTree == null) return;
            if (controller == null || !controller.IsSeated) return;

            if (skillTree.IsOpen)
                skillTree.Close();
            else
                skillTree.Open();

            if (audioSource != null && clickClip != null)
                AudioService.PlaySfx(audioSource, clickClip, 0.8f);
        }
    }
}
