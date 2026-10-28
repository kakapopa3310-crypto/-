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
        public PaperPileInteractable pile;
        public PaperHUD hud;

        private InteractableBase current;
        private FirstPersonController fpc;

        public bool HasPaper { get; private set; }

        /// <summary>Сколько сгибов у листа в руках.</summary>
        public int CarriedFolds { get; private set; }

        /// <summary>Сколько дадут за лист в руках.</summary>
        public int CarriedValue
        {
            get { return PlayerStats.PaperValue + CarriedFolds * 3; }
        }

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

            if (heldPaperVisual != null)
            {
                handBaseScale = heldPaperVisual.transform.localScale;
                handBasePosition = heldPaperVisual.transform.localPosition;
            }
        }

        private Vector3 handBaseScale = Vector3.one;
        private Vector3 handBasePosition;

        private void Start()
        {
            RefreshHud();
            if (hud != null)
                hud.SetHint("WASD — идти · Мышь — смотреть · Shift — бежать · Space — прыжок\n" +
                            "E — взаимодействовать · Q — бросить бумагу · Esc — настройки");
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

        /// <summary>Взять лист в руки. folds — сколько у него сгибов (0 — плоский).</summary>
        public void TakePaper(int folds = 0)
        {
            if (HasPaper) return;

            HasPaper = true;
            CarriedFolds = Mathf.Clamp(folds, 0, 2);

            if (heldPaperVisual != null)
                heldPaperVisual.SetActive(true);

            ApplyHeldPaperShape();
            RefreshHud();
        }

        public void ConsumePaper()
        {
            HasPaper = false;
            CarriedFolds = 0;

            if (heldPaperVisual != null)
                heldPaperVisual.SetActive(false);

            ApplyHeldPaperShape();
            RefreshHud();
        }

        /// <summary>
        /// Лист в руках выглядит сложенным так же, как в мире:
        /// 0 — целый, 1 — пополам, 2 — вчетверо.
        /// </summary>
        private void ApplyHeldPaperShape()
        {
            if (heldPaperVisual == null) return;

            float lengthScale = CarriedFolds >= 1 ? 0.5f : 1f;
            float widthScale = CarriedFolds >= 2 ? 0.5f : 1f;

            heldPaperVisual.transform.localScale = new Vector3(
                handBaseScale.x * widthScale,
                handBaseScale.y,
                handBaseScale.z * lengthScale);

            // Сдвигаем к руке, чтобы сложенный лист не «уезжал» из кадра.
            heldPaperVisual.transform.localPosition = handBasePosition + new Vector3(
                handBaseScale.x * 0.5f * (1f - widthScale),
                0f,
                handBaseScale.z * 0.5f * (1f - lengthScale));
        }

        public void RefreshHud()
        {
            if (hud == null) return;

            hud.SetCounters(HasPaper, CarriedFolds,
                mat != null ? mat.PlacedCount : 0,
                pile != null ? pile.Count : 0);

            if (current != null)
                hud.SetPrompt(current.GetPrompt(this));
        }
    }
}
