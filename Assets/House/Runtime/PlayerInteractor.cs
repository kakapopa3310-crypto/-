using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PaperHouse
{
    /// <summary>Луч из камеры, подсказка и действие по E.</summary>
    public class PlayerInteractor : MonoBehaviour
    {
        public Transform view;
        public float reach = 3f;
        public GameObject heldPaperVisual;
        public PaperMatInteractable mat;
        public PaperHUD hud;

        private InteractableBase current;
        private FirstPersonController fpc;

        public bool HasPaper { get; private set; }

        private void Awake()
        {
            fpc = GetComponent<FirstPersonController>();
            if (view == null)
            {
                Camera camera = GetComponentInChildren<Camera>();
                if (camera != null) view = camera.transform;
            }

            if (hud == null)
                hud = GetComponent<PaperHUD>();
        }

        private void Start()
        {
            RefreshHud();
            if (hud != null)
                hud.SetHint("WASD — идти · Мышь — смотреть · Shift — бежать · Space — прыжок\n" +
                            "E — взаимодействовать · Q — запустить бумагу к луне · Esc — настройки");
        }

        private void Update()
        {
            if (fpc != null &&
                ((fpc.settingsMenu != null && fpc.settingsMenu.IsOpen) ||
                 (fpc.skillTree != null && fpc.skillTree.IsOpen)))
            {
                if (hud != null) hud.SetPrompt(string.Empty);
                return;
            }

            UpdateFocus();

            if (current != null && InteractPressed())
            {
                current.Interact(this);
                RefreshHud();
            }
        }

        private void UpdateFocus()
        {
            InteractableBase found = null;

            if (view != null &&
                Physics.Raycast(view.position, view.forward, out RaycastHit hit, reach,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            {
                found = hit.collider.GetComponentInParent<InteractableBase>();
            }

            if (found != current)
            {
                current = found;
                if (hud != null)
                    hud.SetPrompt(current != null ? current.GetPrompt(this) : string.Empty);
            }
            else if (current != null && hud != null)
            {
                hud.SetPrompt(current.GetPrompt(this));
            }
        }

        private static bool InteractPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.E);
#endif
        }

        public void TakePaper()
        {
            if (HasPaper) return;
            HasPaper = true;
            if (heldPaperVisual != null)
                heldPaperVisual.SetActive(true);
            RefreshHud();
        }

        public void ConsumePaper()
        {
            HasPaper = false;
            if (heldPaperVisual != null)
                heldPaperVisual.SetActive(false);
            RefreshHud();
        }

        public void RefreshHud()
        {
            if (hud == null) return;

            hud.SetCounters(HasPaper, mat != null ? mat.PlacedCount : 0);
            if (current != null)
                hud.SetPrompt(current.GetPrompt(this));
        }
    }
}
