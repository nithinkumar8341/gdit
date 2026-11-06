using UnityEngine;

public class PatientProfileTester : MonoBehaviour
{
    private void Start()
    {
        PatientFactory factory =
            gameObject.AddComponent<PatientFactory>();

        Debug.Log("=== GOLDEN HOUR PATIENT PROFILES ===");

        // Patient 01 - Walking wounded
        PatientController patient01 =
            factory.CreatePatient(
                "P01",
                "Casualty 01",
                true,
                true,
                18,
                true,
                true
            );

        TestPatient(patient01);


        // Patient 02 - High respiratory rate
        PatientController patient02 =
            factory.CreatePatient(
                "P02",
                "Casualty 02",
                false,
                true,
                35,
                true,
                true
            );

        TestPatient(patient02);


        // Patient 03 - No radial pulse
        PatientController patient03 =
            factory.CreatePatient(
                "P03",
                "Casualty 03",
                false,
                true,
                20,
                false,
                true
            );

        TestPatient(patient03);


        // Patient 04 - Cannot follow commands
        PatientController patient04 =
            factory.CreatePatient(
                "P04",
                "Casualty 04",
                false,
                true,
                20,
                true,
                false
            );

        TestPatient(patient04);


        // Patient 05 - Stable delayed patient
        PatientController patient05 =
            factory.CreatePatient(
                "P05",
                "Casualty 05",
                false,
                true,
                20,
                true,
                true
            );

        TestPatient(patient05);


        Debug.Log("=== PATIENT PROFILE TEST COMPLETE ===");
    }

    private void TestPatient(PatientController patient)
    {
        TriageCategory result =
            patient.AssessPatient();

        Debug.Log(
            patient.patientData.patientID +
            " | " +
            patient.patientData.patientName +
            " | Result: " +
            result
        );
    }
}