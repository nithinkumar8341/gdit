using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    public float interactionDistance = 10f;

    [Header("Camera")]
    public Camera playerCamera;

    private void Awake()
    {
        if (playerCamera == null)
        {
            playerCamera =
                GetComponentInChildren<Camera>();
        }
    }

    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            TryInteract();
        }
    }

    private void TryInteract()
    {
        if (playerCamera == null)
        {
            return;
        }

        Ray ray =
            new Ray(
                playerCamera.transform.position,
                playerCamera.transform.forward
            );

        RaycastHit hit;

        if (Physics.Raycast(
            ray,
            out hit,
            interactionDistance))
        {
            // Check whether the object is a patient
            PatientInteractable patient =
                hit.collider.GetComponent<PatientInteractable>();

            if (patient != null)
            {
                patient.Interact();
                return;
            }

            // Generic interaction
            Debug.Log(
                "INTERACTED WITH: " +
                hit.collider.gameObject.name
            );
        }
        else
        {
            Debug.Log(
                "Nothing to interact with."
            );
        }
    }
}