using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerLook : MonoBehaviour
{
    [Header("Look Settings")]
    public float mouseSensitivity = 0.15f;

    [Header("Camera")]
    public Transform playerCamera;

    private float verticalRotation = 0f;

    private void Awake()
    {
        // Automatically find the camera under Player
        if (playerCamera == null)
        {
            Camera childCamera =
                GetComponentInChildren<Camera>();

            if (childCamera != null)
            {
                playerCamera = childCamera.transform;
            }
            else
            {
                Debug.LogError(
                    "PlayerLook: No Camera found under Player."
                );
            }
        }
    }

    private void Update()
    {
        if (Mouse.current == null)
        {
            return;
        }

        if (playerCamera == null)
        {
            return;
        }

        Vector2 mouseDelta =
            Mouse.current.delta.ReadValue();

        float mouseX =
            mouseDelta.x * mouseSensitivity;

        float mouseY =
            mouseDelta.y * mouseSensitivity;

        // Rotate player left/right
        transform.Rotate(
            Vector3.up * mouseX
        );

        // Rotate camera up/down
        verticalRotation -= mouseY;

        verticalRotation =
            Mathf.Clamp(
                verticalRotation,
                -80f,
                80f
            );

        playerCamera.localRotation =
            Quaternion.Euler(
                verticalRotation,
                0f,
                0f
            );
    }
}