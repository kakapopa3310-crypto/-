using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PaperHouse
{
    /// <summary>Q — запустить сложенный лист к луне. Персонаж до неё не допрыгнет.</summary>
    public class PaperThrower : MonoBehaviour
    {
        public Transform moon;
        public PlayerInteractor interactor;
        public PaperHUD hud;
        public Material paperMaterial;
        public FirstPersonController controller;
        public float duration = 4.5f;

        private void Update()
        {
            if (interactor == null || !interactor.HasPaper) return;
            if (moon == null) return;

            if (controller != null &&
                ((controller.settingsMenu != null && controller.settingsMenu.IsOpen) ||
                 (controller.skillTree != null && controller.skillTree.IsOpen)))
                return;

            if (ThrowPressed())
                Throw();
        }

        private static bool ThrowPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Q);
#endif
        }

        private void Throw()
        {
            interactor.ConsumePaper();

            Vector3 start = interactor.view != null
                ? interactor.view.position + interactor.view.forward * 0.6f
                : transform.position + Vector3.up * 1.6f;

            var plane = new GameObject("Paper Airplane");
            plane.transform.position = start;
            plane.transform.rotation = Quaternion.LookRotation(moon.position - start);

            AddPanel(plane.transform, "Body", Vector3.zero, new Vector3(0.18f, 0.008f, 0.34f),
                Quaternion.identity);
            AddPanel(plane.transform, "Wing Left", new Vector3(-0.13f, 0f, 0.02f),
                new Vector3(0.34f, 0.006f, 0.16f), Quaternion.Euler(0f, 24f, 8f));
            AddPanel(plane.transform, "Wing Right", new Vector3(0.13f, 0f, 0.02f),
                new Vector3(0.34f, 0.006f, 0.16f), Quaternion.Euler(0f, -24f, -8f));

            var projectile = plane.AddComponent<PaperProjectile>();
            projectile.end = moon.position;
            projectile.duration = duration;
            projectile.arcHeight = 9f;
            projectile.hud = hud;
            projectile.message = "Сложенный лист долетел до луны!";
        }

        private void AddPanel(Transform parent, string name, Vector3 position, Vector3 scale,
            Quaternion rotation)
        {
            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = name;
            Collider collider = panel.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            panel.transform.SetParent(parent, false);
            panel.transform.localPosition = position;
            panel.transform.localScale = scale;
            panel.transform.localRotation = rotation;

            if (paperMaterial != null)
                panel.GetComponent<MeshRenderer>().sharedMaterial = paperMaterial;
        }
    }
}
