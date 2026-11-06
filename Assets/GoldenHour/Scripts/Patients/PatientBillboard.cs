using UnityEngine;

namespace GoldenHour
{
    /// <summary>Keeps a world-space label facing the main camera.</summary>
    public sealed class PatientBillboard : MonoBehaviour
    {
        private Camera mainCamera;

        private void LateUpdate()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) return;
            Vector3 away = transform.position - mainCamera.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(away);
        }
    }
}
