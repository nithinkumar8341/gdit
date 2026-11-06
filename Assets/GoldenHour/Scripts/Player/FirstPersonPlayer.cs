using UnityEngine;
using UnityEngine.InputSystem;

namespace GoldenHour
{
    /// <summary>
    /// First-person WASD + mouse-look controller (Input System only) with gravity and
    /// CharacterController collision. Cursor is locked only while controls are enabled.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonPlayer : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 4.2f;
        [SerializeField] private float sprintSpeed = 6.5f;
        [SerializeField] private float mouseSensitivity = 0.1f;
        [SerializeField] private float gravity = -18f;
        [SerializeField] private Transform cameraTransform;

        private CharacterController characterController;
        private float verticalVelocity;
        private float pitch;
        private bool controlsEnabled;
        private int ignoreLookFrames;

        public Camera PlayerCamera { get; private set; }
        public bool ControlsEnabled => controlsEnabled;

        private void Awake()
        {
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
            Keyboard keyboard = Keyboard.current;

            if (controlsEnabled && keyboard != null)
            {
                if (cameraTransform != null && Mouse.current != null)
                {
                    Vector2 look = Mouse.current.delta.ReadValue() * mouseSensitivity;
                    if (ignoreLookFrames > 0)
                    {
                        ignoreLookFrames--;
                        look = Vector2.zero;
                    }

                    transform.Rotate(0f, look.x, 0f);
                    SetPitch(pitch - look.y);
                }

                if (keyboard.wKey.isPressed) movementInput.y += 1f;
                if (keyboard.sKey.isPressed) movementInput.y -= 1f;
                if (keyboard.dKey.isPressed) movementInput.x += 1f;
                if (keyboard.aKey.isPressed) movementInput.x -= 1f;
                sprint = keyboard.leftShiftKey.isPressed;
            }

            Vector3 movement = transform.right * movementInput.x + transform.forward * movementInput.y;
            movement = Vector3.ClampMagnitude(movement, 1f) * (sprint ? sprintSpeed : moveSpeed);

            // Gravity always applies so the player settles onto the ground even while a panel is open.
            if (characterController.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            else verticalVelocity += gravity * Time.deltaTime;

            movement.y = verticalVelocity;
            characterController.Move(movement * Time.deltaTime);
        }
    }
}
