using UnityEngine;

namespace PaperHouse
{
    /// <summary>Стул: E — сесть, E — встать.</summary>
    public class ChairInteractable : InteractableBase
    {
        public Transform seatPoint;
        public Transform standPoint;
        public FirstPersonController controller;

        public override string GetPrompt(PlayerInteractor player)
        {
            if (controller == null) return "Стул";
            return controller.IsSeated ? "E — встать" : "E — сесть на стул";
        }

        public override void Interact(PlayerInteractor player)
        {
            if (controller == null) return;

            if (controller.IsSeated)
                controller.StandUp();
            else if (seatPoint != null && standPoint != null)
                controller.SitAt(seatPoint, standPoint);
        }
    }
}
