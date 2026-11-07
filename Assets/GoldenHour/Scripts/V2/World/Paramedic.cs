using UnityEngine;

namespace GoldenHour
{
    /// <summary>A paramedic NPC. The lead paramedic accepts handovers from the player.</summary>
    public sealed class Paramedic : MonoBehaviour, IInteractable
    {
        public Ambulance Ambulance { get; set; }
        public HumanoidBody Body { get; set; }
        public Walker Walker { get; set; }
        public bool Lead { get; set; }

        public string Prompt => "Hand over casualties to Ambulance " + Ambulance.Number +
                                " (" + Ambulance.FreeSpaces + " stretcher place" + (Ambulance.FreeSpaces == 1 ? "" : "s") + " left)";

        public bool CanInteract => Lead && Ambulance.State == AmbulanceState.Waiting && Ambulance.FreeSpaces > 0;
        public Vector3 FocusPoint => transform.position + Vector3.up * 1.3f;

        public void Interact()
        {
            if (IncidentDirector.Instance != null) IncidentDirector.Instance.OpenHandover(Ambulance);
        }
    }
}
