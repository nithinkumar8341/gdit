using System;

[Serializable]
public class PatientData
{
    // ==========================================
    // PATIENT IDENTITY
    // ==========================================

    public string patientID;
    public string patientName;


    // ==========================================
    // START TRIAGE INFORMATION
    // ==========================================

    // Can the patient walk?
    public bool canWalk;

    // Is the patient breathing?
    public bool breathing;

    // Respiratory rate
    public int respiratoryRate;

    // Is radial pulse present?
    public bool radialPulsePresent;

    // Can the patient follow commands?
    public bool followsCommands;


    // ==========================================
    // AIRWAY INTERVENTION
    // ==========================================

    public bool airwayOpened;

    // Used for the airway intervention scenario
    public bool breathingAfterAirway;


    // ==========================================
    // TRIAGE RESULT
    // ==========================================

    public TriageCategory currentCategory;

    public bool hasBeenAssessed;


    // ==========================================
    // ASSESSMENT INFORMATION
    // ==========================================

    public float assessmentTime;


    // ==========================================
    // GAME STATE
    // ==========================================

    public bool isSaved;
    public bool isDead;
}
