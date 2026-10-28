using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PaperHouse
{
    /// <summary>FPS-управление: WASD, мышь, бег, прыжок, пауза, посадка на стул.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        public Transform cameraTransform;
        public float walkSpeed = 3.2f;
        public float runSpeed = 5.2f;
        public float gravity = -22f;
        public float lookLimit = 85f;

        [Header("Звук шагов")]
        public AudioSource footstepSource;

        public SettingsMenu settingsMenu;
        public SkillTreeMenu skillTree;

        private CharacterController controller;
        private PaperHUD hud;
        private float pitch;
        private Vector3 verticalVelocity;
        private bool started;
        private bool seated;
        private Transform standPoint;

        public bool HasStarted
        {
            get { return started; }
        }

        public bool IsSeated
        {
            get { return seated; }
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            hud = GetComponent<PaperHUD>();

            if (cameraTransform == null)
            {
                Camera camera = GetComponentInChildren<Camera>();
                if (camera != null) cameraTransform = camera.transform;
            }

            LockCursor(false);
        }

        private void OnDisable()
        {
            StopFootsteps();
            LockCursor(false);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && started)
            {
                started = false;
                LockCursor(false);
                StopFootsteps();
                if (hud != null) hud.SetStartPanelVisible(true);
            }
        }

        private void Update()
        {
            if (cameraTransform == null) return;

            if (settingsMenu != null && settingsMenu.IsOpen)
            {
                if (EscapePressed()) settingsMenu.Close();
                return;
            }

            if (skillTree != null && skillTree.IsOpen)
            {
                if (EscapePressed()) skillTree.Close();
                return;
            }

            if (!started)
            {
                if (StartPressed()) BeginPlay();
                return;
            }

            if (EscapePressed())
            {
                if (settingsMenu != null)
                {
                    settingsMenu.Open();
                }
                else
                {
                    started = false;
                    LockCursor(false);
                    StopFootsteps();
                    if (hud != null) hud.SetStartPanelVisible(true);
                }
                return;
            }

            Look();

            if (seated)
                StopFootsteps();
            else
                Move();
        }

        public void BeginPlay()
        {
            started = true;
            LockCursor(true);
            if (hud != null)
            {
                hud.SetStartPanelVisible(false);
                hud.SetStarted(true);
            }
        }

        public void PauseForMenu()
        {
            started = false;
            LockCursor(false);
            StopFootsteps();
            if (hud != null)
                hud.SetStartPanelVisible(false);
        }

        public void SitAt(Transform seatPoint, Transform stand)
        {
            if (seatPoint == null) return;

            seated = true;
            standPoint = stand;
            verticalVelocity = Vector3.zero;
            StopFootsteps();

            controller.enabled = false;
            transform.position = seatPoint.position;
            transform.rotation = seatPoint.rotation;
            LockCursor(true);
            Physics.SyncTransforms();
        }

        public void StandUp()
        {
            if (!seated) return;

            seated = false;
            if (standPoint != null)
            {
                transform.position = standPoint.position;
                transform.rotation = standPoint.rotation;
            }

            controller.enabled = true;
            verticalVelocity = Vector3.zero;
            LockCursor(true);
            Physics.SyncTransforms();
        }

        private bool StartPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                return true;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return false;

            return keyboard.anyKey.wasPressedThisFrame &&
                   !keyboard.escapeKey.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(0) || Input.anyKeyDown;
#endif
        }

        private bool EscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        private void Look()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current == null) return;
            Vector2 delta = Mouse.current.delta.ReadValue() * GameSettings.MouseSensitivity;
#else
            Vector2 delta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) *
                            (GameSettings.MouseSensitivity * 10f);
#endif
            transform.Rotate(0f, delta.x, 0f, Space.Self);
            pitch = Mathf.Clamp(pitch - delta.y, -lookLimit, lookLimit);
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void Move()
        {
            float x = 0f;
            float z = 0f;
            bool run = false;
            bool jump = false;

#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed) x -= 1f;
                if (keyboard.dKey.isPressed) x += 1f;
                if (keyboard.sKey.isPressed) z -= 1f;
                if (keyboard.wKey.isPressed) z += 1f;
                run = keyboard.leftShiftKey.isPressed;
                jump = keyboard.spaceKey.wasPressedThisFrame;
            }
#else
            x = Input.GetAxisRaw("Horizontal");
            z = Input.GetAxisRaw("Vertical");
            run = Input.GetKey(KeyCode.LeftShift);
            jump = Input.GetKeyDown(KeyCode.Space);
#endif

            Vector3 move = transform.right * x + transform.forward * z;
            if (move.sqrMagnitude > 1f)
                move.Normalize();

            if (controller.isGrounded && verticalVelocity.y < 0f)
                verticalVelocity.y = -2f;

            if (jump && controller.isGrounded)
                verticalVelocity.y = Mathf.Sqrt(PlayerStats.JumpHeight * -2f * gravity);

            verticalVelocity.y += gravity * Time.deltaTime;

            float speed = (run ? runSpeed : walkSpeed) * PlayerStats.SpeedMultiplier;
            controller.Move((move * speed + verticalVelocity) * Time.deltaTime);

            UpdateFootsteps(move);
        }

        private void UpdateFootsteps(Vector3 move)
        {
            if (footstepSource == null) return;

            footstepSource.volume = GameSettings.SfxVolume * 0.55f;
            bool moving = controller.isGrounded && move.sqrMagnitude > 0.01f;

            if (moving && !footstepSource.isPlaying)
                footstepSource.Play();
            else if (!moving && footstepSource.isPlaying)
                footstepSource.Stop();
        }

        private void StopFootsteps()
        {
            if (footstepSource != null && footstepSource.isPlaying)
                footstepSource.Stop();
        }

        public void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
