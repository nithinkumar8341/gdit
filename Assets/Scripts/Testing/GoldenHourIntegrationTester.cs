using UnityEngine;

public class GoldenHourIntegrationTester : MonoBehaviour
{
    private void Start()
    {
        Debug.Log("================================");
        Debug.Log("GOLDEN HOUR INTEGRATION TEST");
        Debug.Log("================================");


        // ==========================================
        // GET MANAGERS
        // ==========================================

        GameManager gameManager =
            FindAnyObjectByType<GameManager>();

        ScoreManager scoreManager =
             FindAnyObjectByType<ScoreManager>();


        if (gameManager == null)
        {
            Debug.LogError(
                "GameManager not found!"
            );

            return;
        }

        if (scoreManager == null)
        {
            Debug.LogError(
                "ScoreManager not found!"
            );

            return;
        }


        Debug.Log("Managers found successfully.");


        // ==========================================
        // CREATE TEST PATIENTS
        // ==========================================

        PatientFactory factory =
            gameObject.AddComponent<PatientFactory>();


        // Patient 1 → GREEN

        PatientController patient1 =
            factory.CreatePatient(
                "TEST_01",
                "Test Patient 01",
                true,
                true,
                18,
                true,
                true
            );


        // Patient 2 → RED

        PatientController patient2 =
            factory.CreatePatient(
                "TEST_02",
                "Test Patient 02",
                false,
                true,
                35,
                true,
                true
            );


        // Patient 3 → YELLOW

        PatientController patient3 =
            factory.CreatePatient(
                "TEST_03",
                "Test Patient 03",
                false,
                true,
                20,
                true,
                true
            );


        // ==========================================
        // ASSESS PATIENTS
        // ==========================================

        TriageCategory result1 =
            patient1.AssessPatient();

        TriageCategory result2 =
            patient2.AssessPatient();

        TriageCategory result3 =
            patient3.AssessPatient();


        // ==========================================
        // RECORD SCORES
        // ==========================================

        scoreManager.RecordResult(
            result1,
            TriageCategory.Green
        );

        scoreManager.RecordResult(
            result2,
            TriageCategory.Red
        );

        scoreManager.RecordResult(
            result3,
            TriageCategory.Yellow
        );


        // ==========================================
        // PRINT SUMMARY
        // ==========================================

        scoreManager.PrintSummary();


        Debug.Log("================================");
        Debug.Log("GOLDEN HOUR INTEGRATION TEST COMPLETE");
        Debug.Log("================================");
    }
}