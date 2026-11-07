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
        private Vector3 glideFrom;
        private Vector3 glideTo;
        private Quaternion glideYawFrom;
        private Quaternion glideYawTo;
        private float glidePitchFrom;
        private float glidePitchTo;
        private float glideProgress = -1f;
        private const float GlideSeconds = 0.45f;

        /// <summary>Raised on each footstep; the argument is true while walking faster.</summary>
        public event System.Action<bool> Stepped;

        public Camera PlayerCamera { get; private set; }
        public bool ControlsEnabled => controlsEnabled;
        public bool ForcedKneel { get; set; }
        public bool IsKneeling => ForcedKneel || kneelToggle;
        public float EyeHeight => eyeHeight;
        public float Speed { get; private set; }
        public bool Gliding => glideProgress >= 0f;

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
            glideProgress = -1f;
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

        /// <summary>
        /// Moves (smoothly, or instantly) to <paramref name="feet"/> and turns so that <paramref name="focus"/> appears at
        /// horizontal viewport position <paramref name="viewportX"/> as seen from kneeling eye height.
        /// </summary>
        public void FrameTarget(Vector3 feet, Vector3 focus, float viewportX, bool instant)
        {
            float eye = IsKneeling ? KneelingEyeHeight : StandingEyeHeight;
            Vector3 direction = focus - (feet + Vector3.up * eye);
            Vector3 flat = new Vector3(direction.x, 0f, direction.z);
            if (flat.sqrMagnitude < 0.0001f) flat = transform.forward;
            float yaw = Quaternion.LookRotation(flat).eulerAngles.y;
            if (PlayerCamera != null)
            {
                float tanHalf = Mathf.Tan(PlayerCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * PlayerCamera.aspect;
                yaw += Mathf.Atan((0.5f - viewportX) * 2f * tanHalf) * Mathf.Rad2Deg;
            }

            float targetPitch = Mathf.Clamp(-Mathf.Atan2(direction.y, flat.magnitude) * Mathf.Rad2Deg, -80f, 80f);
            if (instant)
            {
                Teleport(feet, yaw, targetPitch);
                eyeHeight = eye;
                glideProgress = -1f;
                return;
            }

            glideFrom = transform.position;
            glideTo = feet;
            glideYawFrom = transform.rotation;
            glideYawTo = Quaternion.Euler(0f, yaw, 0f);
            glidePitchFrom = pitch;
            glidePitchTo = targetPitch;
            glideProgress = 0f;
            if (characterController != null) characterController.enabled = false;
        }

        private void UpdateGlide()
        {
            glideProgress = Mathf.Min(1f, glideProgress + Time.unscaledDeltaTime / GlideSeconds);
            float t = Mathf.SmoothStep(0f, 1f, glideProgress);
            transform.SetPositionAndRotation(Vector3.Lerp(glideFrom, glideTo, t), Quaternion.Slerp(glideYawFrom, glideYawTo, t));
            SetPitch(Mathf.Lerp(glidePitchFrom, glidePitchTo, t));
            if (glideProgress >= 1f)
            {
                glideProgress = -1f;
                verticalVelocity = 0f;
                if (characterController != null) characterController.enabled = true;
            }
        }

        private void SetPitch(float value)
        {
            pitch = Mathf.Clamp(value, -80f, 80f);
            if (cameraTransform != null) cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void Update()
        {
            if (Gliding)
            {
                UpdateGlide();
                UpdateCameraHeight();
                return;
            }

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
