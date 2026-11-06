using UnityEngine;
using TMPro;

public class PatientUIController : MonoBehaviour
{
    public static PatientUIController Instance;

    [Header("UI")]
    public TMP_Text patientInfoText;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void ShowPatientInfo(PatientData patient)
    {
        if (patient == null)
        {
            return;
        }

        if (patientInfoText == null)
        {
            Debug.LogError(
                "Patient Info Text is not assigned."
            );

            return;
        }

        patientInfoText.text =
            "PATIENT INFORMATION\n\n" +
            "ID: " + patient.patientID + "\n" +
            "Name: " + patient.patientName + "\n" +
            "Status: Assessed\n" +
            "Triage: " + patient.currentCategory;
    }

    public void ClearPatientInfo()
    {
        if (patientInfoText != null)
        {
            patientInfoText.text =
                "PATIENT INFORMATION";
        }
    }
}