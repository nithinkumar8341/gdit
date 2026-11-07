using UnityEngine;

namespace GoldenHour
{
    /// <summary>The first-aid bag on the ground: collecting it opens the lid and fills the player's supplies.</summary>
    public sealed class KitPickup : MonoBehaviour, IInteractable
    {
        public string Prompt => "Collect the first-aid bag";
        public bool CanInteract => IncidentDirector.Instance != null && !IncidentDirector.Instance.KitCollected;
        public Vector3 FocusPoint => transform.position + Vector3.up * 0.35f;

        public void Interact()
        {
            if (IncidentDirector.Instance != null) IncidentDirector.Instance.CollectKit();
        }
    }
}
