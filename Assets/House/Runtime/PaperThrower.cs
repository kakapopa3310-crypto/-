using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PaperHouse
{
    /// <summary>
    /// Q — бросить бумажный самолётик вперёд.
    /// Бросок короткий, дальность и дуга растут от силы.
    /// </summary>
    public class PaperThrower : MonoBehaviour
    {
        public PlayerInteractor interactor;
        public PaperHUD hud;
        public Material paperMaterial;
        public FirstPersonController controller;

        private void Update()
        {
            if (interactor == null || !interactor.HasPaper) return;

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

        /// <summary>Публичный бросок — им же пользуются тесты из редактора.</summary>
        public void Throw()
        {
            if (interactor == null || !interactor.HasPaper) return;

            Transform view = interactor.view != null ? interactor.view : transform;

            Vector3 start = view.position + view.forward * 0.55f;
            Vector3 flat = new Vector3(view.forward.x, 0f, view.forward.z);
            if (flat.sqrMagnitude < 0.01f)
                flat = transform.forward;
            flat.Normalize();

            float distance = PlayerStats.ThrowDistance;

            // Не даём самолётику пролететь сквозь стену или дверь.
            if (Physics.Raycast(start, flat, out RaycastHit hit, distance + 0.3f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                distance = Mathf.Clamp(hit.distance - 0.25f, 0.4f, distance);

            Vector3 end = start + flat * distance;
            end.y = 0.07f;

            interactor.ConsumePaper();

            var plane = new GameObject("Paper Airplane");
            plane.transform.position = start;
            plane.transform.rotation = Quaternion.LookRotation(flat);

            AddPanel(plane.transform, "Body", Vector3.zero, new Vector3(0.18f, 0.008f, 0.34f),
                Quaternion.identity);
            AddPanel(plane.transform, "Wing Left", new Vector3(-0.13f, 0f, 0.02f),
                new Vector3(0.34f, 0.006f, 0.16f), Quaternion.Euler(0f, 24f, 8f));
            AddPanel(plane.transform, "Wing Right", new Vector3(0.13f, 0f, 0.02f),
                new Vector3(0.34f, 0.006f, 0.16f), Quaternion.Euler(0f, -24f, -8f));

            var projectile = plane.AddComponent<PaperProjectile>();
            projectile.end = end;
            projectile.duration = 0.45f + distance * 0.12f;
            projectile.arcHeight = PlayerStats.ThrowArc;
            projectile.hud = hud;
            projectile.message = "Бумажный самолётик улетел на " + distance.ToString("0.0") + " м";
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
