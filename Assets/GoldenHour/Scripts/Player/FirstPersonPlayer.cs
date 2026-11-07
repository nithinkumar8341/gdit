using UnityEngine;

namespace GoldenHour
{
    /// <summary>
    /// First-person controller on Input System actions (keyboard/mouse and gamepad): walking, faster walk,
    /// mouse/stick look, gravity, CharacterController collision, kneeling (camera lowers) and a subtle
    /// head bob (off with motion reduction). Raises <see cref="Stepped"/> every stride for footsteps.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonPlayer : MonoBehaviour
    {
        public const float StandingEyeHeight = 1.52f;
        public const float KneelingEyeHeight = 0.92f;

        [SerializeField] private float moveSpeed = 3.8f;
        [SerializeField] private float sprintSpeed = 6f;
        [SerializeField] private float kneelSpeed = 1.6f;
        [SerializeField] private float mouseSensitivity = 0.1f;
        [SerializeField] private float stickDegreesPerSecond = 140f;
        [SerializeField] private float gravity = -18f;
        [SerializeField] private float strideLength = 1.7f;
        [SerializeField] private Transform cameraTransform;

        private CharacterController characterController;
        private float verticalVelocity;
        private float pitch;
        private bool controlsEnabled;
        private int ignoreLookFrames;
        private float strideProgress;
        private float eyeHeight = StandingEyeHeight;
        private float bobPhase;
        private bool kneelToggle;

        /// <summary>Raised on each footstep; the argument is true while walking faster.</summary>
        public event System.Action<bool> Stepped;

        public Camera PlayerCamera { get; private set; }
        public bool ControlsEnabled => controlsEnabled;
        public bool ForcedKneel { get; set; }
        public bool IsKneeling => ForcedKneel || kneelToggle;
        public float EyeHeight => eyeHeight;
        public float Speed { get; private set; }

        private void Awake()
        {
            GameInput.Ensure();
            characterController = GetComponent<CharacterController>();
            if (cameraTransform == null)
            {
                Camera found = GetComponentInChildren<Camera>();
                if (found != null) cameraTransform = found.transform;
            }

            PlayerCamera = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : null;
        }

        public void Configure(Transform view)
        {
            cameraTransform = view;
            PlayerCamera = view != null ? view.GetComponent<Camera>() : null;
            characterController = GetComponent<CharacterController>();
        }

        public void SetControlsEnabled(bool enabled)
        {
            controlsEnabled = enabled;
            Cursor.lockState = enabled ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !enabled;
            // Skip the first mouse deltas after re-locking so the view does not jump.
            ignoreLookFrames = enabled ? 2 : 0;
        }

        /// <summary>Moves the player without fighting the CharacterController.</summary>
        public void Teleport(Vector3 position, float yaw, float viewPitch)
        {
            if (characterController != null) characterController.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            if (characterController != null) characterController.enabled = true;
            SetPitch(viewPitch);
            verticalVelocity = 0f;
        }

        /// <summary>Turns body and camera to look at a world point.</summary>
        public void LookAt(Vector3 worldPoint)
        {
            if (cameraTransform == null) return;
            Vector3 direction = worldPoint - cameraTransform.position;
            Vector3 flat = new Vector3(direction.x, 0f, direction.z);
            if (flat.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(flat);
            SetPitch(-Mathf.Atan2(direction.y, flat.magnitude) * Mathf.Rad2Deg);
        }

        private void SetPitch(float value)
        {
            pitch = Mathf.Clamp(value, -80f, 80f);
            if (cameraTransform != null) cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void Update()
        {
            if (characterController == null || !characterController.enabled) return;
            Vector2 movementInput = Vector2.zero;
            bool sprint = false;

            if (controlsEnabled)
            {
                if (cameraTransform != null)
                {
                    Vector2 look = GameInput.LookMouse.ReadValue<Vector2>() * (mouseSensitivity * GameSettings.MouseSensitivity);
                    look += GameInput.LookStick.ReadValue<Vector2>() *
                            (stickDegreesPerSecond * GameSettings.MouseSensitivity * Time.deltaTime);
                    if (ignoreLookFrames > 0)
                    {
                        ignoreLookFrames--;
                        look = Vector2.zero;
                    }

                    transform.Rotate(0f, look.x, 0f);
                    SetPitch(pitch - look.y);
                }

                movementInput = Vector2.ClampMagnitude(GameInput.Move.ReadValue<Vector2>(), 1f);
                sprint = GameInput.Held(GameInput.Sprint);
                if (GameInput.Pressed(GameInput.Crouch)) kneelToggle = !kneelToggle;
            }

            float speed = IsKneeling ? kneelSpeed : sprint ? sprintSpeed : moveSpeed;
            Vector3 movement = (transform.right * movementInput.x + transform.forward * movementInput.y) * speed;

            // Gravity always applies so the player settles onto the ground even while a panel is open.
            if (characterController.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            else verticalVelocity += gravity * Time.deltaTime;

            movement.y = verticalVelocity;
            Vector3 before = transform.position;
            characterController.Move(movement * Time.deltaTime);
            TrackStride(before, sprint);
            UpdateCameraHeight();
        }

        private void UpdateCameraHeight()
        {
            if (cameraTransform == null) return;
            float target = IsKneeling ? KneelingEyeHeight : StandingEyeHeight;
            eyeHeight = Mathf.MoveTowards(eyeHeight, target, Time.deltaTime * 2.4f);
            float bob = 0f;
            float sway = 0f;
            if (!GameSettings.ReduceMotion && Speed > 0.2f)
            {
                bobPhase += Time.deltaTime * Speed * 2.2f;
                bob = Mathf.Sin(bobPhase * 2f) * 0.025f;
                sway = Mathf.Sin(bobPhase) * 0.012f;
            }

            cameraTransform.localPosition = new Vector3(sway, eyeHeight + bob, 0f);
        }

        private void TrackStride(Vector3 before, bool sprint)
        {
            Vector3 delta = transform.position - before;
            delta.y = 0f;
            Speed = Time.deltaTime > 0f ? delta.magnitude / Time.deltaTime : 0f;
            if (!controlsEnabled || !characterController.isGrounded || delta.sqrMagnitude < 0.000001f)
            {
                strideProgress = Mathf.Min(strideProgress, strideLength * 0.5f);
                return;
            }

            strideProgress += delta.magnitude;
            float stride = sprint ? strideLength * 1.3f : strideLength;
            if (strideProgress >= stride)
            {
                strideProgress = 0f;
                Stepped?.Invoke(sprint);
            }
        }
    }
}
