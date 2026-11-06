using UnityEngine;

public class GoldenHourLogicTester : MonoBehaviour
{
    private void Start()
    {
        Debug.Log("=== GOLDEN HOUR TEST STARTED ===");

        TestPatient("Patient 1 - Walker", true, true, 18, true, true, false, false);
        TestPatient("Patient 2 - High Respiratory Rate", false, true, 35, true, true, false, false);
        TestPatient("Patient 3 - No Pulse", false, true, 20, false, true, false, false);
        TestPatient("Patient 4 - Mental Status Failure", false, true, 20, true, false, false, false);
        TestPatient("Patient 5 - Delayed", false, true, 20, true, true, false, false);
        TestPatient("Patient 6 - No Breathing", false, false, 0, false, false, false, false);
        TestPatient("Patient 7 - Breathing After Airway", false, false, 0, false, false, true, true);
    }

    private void TestPatient(
        string id,
        bool canWalk,
        bool breathing,
        int respiratoryRate,
        bool pulsePresent,
        bool followsCommands,
        bool airwayOpened,
        bool breathingAfterAirway)
    {
        PatientData patient = new PatientData();

        patient.patientID = id;
        patient.canWalk = canWalk;
        patient.breathing = breathing;
        patient.respiratoryRate = respiratoryRate;
        patient.radialPulsePresent = pulsePresent;
        patient.followsCommands = followsCommands;
        patient.airwayOpened = airwayOpened;
        patient.breathingAfterAirway = breathingAfterAirway;

        TriageCategory result = STARTTriageEvaluator.Evaluate(patient);

        Debug.Log(patient.patientID + " → " + result);
    }
}