using UnityEngine;

public class PatientInteractable : MonoBehaviour
{
    private PatientController patientController;

    private void Awake()
    {
        patientController =
            GetComponent<PatientController>();

        if (patientController == null)
        {
            Debug.LogError(
                "PatientInteractable requires PatientController."
            );
        }
    }

    public void Interact()
    {
        if (patientController == null)
        {
            return;
        }

        if (patientController.patientData == null)
        {
            Debug.LogError(
                "PatientData is missing on " +
                gameObject.name
            );

            return;
        }

        TriageCategory result =
            patientController.AssessPatient();

        Debug.Log(
            "PATIENT ASSESSED: " +
            patientController.patientData.patientID +
            " → " +
            result
        );

        if (PatientUIController.Instance != null)
        {
            PatientUIController.Instance.ShowPatientInfo(
                patientController.patientData
            );
        }
    }
}