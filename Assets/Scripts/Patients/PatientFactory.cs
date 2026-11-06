using UnityEngine;

public class PatientFactory : MonoBehaviour
{
    public PatientController CreatePatient(
        string id,
        string name,
        bool canWalk,
        bool breathing,
        int respiratoryRate,
        bool pulsePresent,
        bool followsCommands
    )
    {
        // Create a basic GameObject
        GameObject patientObject = new GameObject(id);

        // Add PatientController
        PatientController controller =
            patientObject.AddComponent<PatientController>();

        // Create patient data
        PatientData data = new PatientData();

        data.patientID = id;
        data.patientName = name;

        data.canWalk = canWalk;
        data.breathing = breathing;
        data.respiratoryRate = respiratoryRate;
        data.radialPulsePresent = pulsePresent;
        data.followsCommands = followsCommands;

        // Assign data to controller
        controller.patientData = data;

        return controller;
    }
}