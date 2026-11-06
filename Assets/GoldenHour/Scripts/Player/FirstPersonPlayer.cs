using UnityEngine;
using UnityEngine.InputSystem;

namespace GoldenHour
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonPlayer : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 4.5f;
        [SerializeField] private float mouseSensitivity = 0.12f;
        [SerializeField] private float gravity = -18f;
        [SerializeField] private Transform cameraTransform;

        private CharacterController characterController;
        private float verticalVelocity;
        private float pitch;
        private bool controlsEnabled;

        public Camera PlayerCamera { get; private set; }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            PlayerCamera = GetComponentInChildren<Camera>();
            if (cameraTransform == null && PlayerCamera != null)
            {
                cameraTransform = PlayerCamera.transform;
            }
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
        }

        private void Update()
        {
            if (!controlsEnabled || Keyboard.current == null || characterController == null)
            {
                return;
            }

            if (cameraTransform != null && Mouse.current != null)
            {
                Vector2 look = Mouse.current.delta.ReadValue() * mouseSensitivity;
                transform.Rotate(0f, look.x, 0f);
                pitch = Mathf.Clamp(pitch - look.y, -78f, 78f);
                cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }

            Vector2 movementInput = Vector2.zero;
            if (Keyboard.current.wKey.isPressed) movementInput.y += 1f;
            if (Keyboard.current.sKey.isPressed) movementInput.y -= 1f;
            if (Keyboard.current.dKey.isPressed) movementInput.x += 1f;
            if (Keyboard.current.aKey.isPressed) movementInput.x -= 1f;

            Vector3 movement = transform.right * movementInput.x +
                               transform.forward * movementInput.y;
            movement = Vector3.ClampMagnitude(movement, 1f) * moveSpeed;

            if (characterController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
            }

            movement.y = verticalVelocity;
            characterController.Move(movement * Time.deltaTime);
        }
    }
}
