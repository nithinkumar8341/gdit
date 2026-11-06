using UnityEngine;

public class PatientController : MonoBehaviour
{
    [Header("Patient Information")]
    public PatientData patientData;

    // Called when the patient object starts
    private void Start()
    {
        if (patientData == null)
        {
            Debug.LogWarning(
                gameObject.name +
                " does not have PatientData assigned."
            );
        }
    }

    // ==========================================
    // ASSESS PATIENT
    // ==========================================

    public TriageCategory AssessPatient()
    {
        if (patientData == null)
        {
            Debug.LogError(
                "PatientData is missing on " +
                gameObject.name
            );

            return TriageCategory.Black;
        }

        patientData.hasBeenAssessed = true;

        TriageCategory result =
            STARTTriageEvaluator.Evaluate(patientData);

        patientData.currentCategory = result;

        Debug.Log(
            patientData.patientID +
            " assessed → " +
            result
        );

        return result;
    }

    // ==========================================
    // AIRWAY INTERVENTION
    // ==========================================

    public void OpenAirway()
    {
        if (patientData == null)
        {
            Debug.LogError(
                "PatientData is missing."
            );

            return;
        }

        patientData.airwayOpened = true;

        Debug.Log(
            patientData.patientID +
            " airway repositioned."
        );
    }

    // ==========================================
    // GET CURRENT CATEGORY
    // ==========================================

    public TriageCategory GetCategory()
    {
        if (patientData == null)
        {
            return TriageCategory.Black;
        }

        return patientData.currentCategory;
    }
}