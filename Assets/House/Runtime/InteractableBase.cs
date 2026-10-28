using UnityEngine;

namespace PaperHouse
{
    /// <summary>Базовый класс для всего, с чем можно взаимодействовать по E.</summary>
    public abstract class InteractableBase : MonoBehaviour
    {
        public abstract string GetPrompt(PlayerInteractor player);
        public abstract void Interact(PlayerInteractor player);
    }
}
