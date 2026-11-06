using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;

    [Header("Gravity")]
    public float gravity = -9.81f;

    private CharacterController characterController;
    private float verticalVelocity;

    private void Awake()
    {
        characterController =
            GetComponent<CharacterController>();

        if (characterController == null)
        {
            characterController =
                gameObject.AddComponent<CharacterController>();
        }
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        // ==========================================
        // WASD MOVEMENT
        // ==========================================

        Vector2 input = Vector2.zero;

        if (Keyboard.current.aKey.isPressed)
            input.x -= 1f;

        if (Keyboard.current.dKey.isPressed)
            input.x += 1f;

        if (Keyboard.current.sKey.isPressed)
            input.y -= 1f;

        if (Keyboard.current.wKey.isPressed)
            input.y += 1f;

        Vector3 movement =
            transform.right * input.x +
            transform.forward * input.y;

        if (movement.magnitude > 1f)
        {
            movement.Normalize();
        }


        // ==========================================
        // GRAVITY
        // ==========================================

        if (characterController.isGrounded)
        {
            verticalVelocity = -2f;
        }
        else
        {
            verticalVelocity +=
                gravity * Time.deltaTime;
        }

        movement.y = verticalVelocity;


        // ==========================================
        // APPLY MOVEMENT
        // ==========================================

        characterController.Move(
            movement * Time.deltaTime
        );
    }
}