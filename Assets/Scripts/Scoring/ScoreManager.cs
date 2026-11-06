using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public int score;

    public int correctClassifications;

    public int incorrectClassifications;


    // ==========================================
    // RECORD TRIAGE RESULT
    // ==========================================

    public void RecordResult(
        TriageCategory selected,
        TriageCategory expected)
    {
        if (selected == expected)
        {
            score += 100;

            correctClassifications++;

            Debug.Log(
                "Correct classification! +100"
            );
        }
        else
        {
            score -= 100;

            incorrectClassifications++;

            Debug.Log(
                "Incorrect classification! -100"
            );
        }
    }


    // ==========================================
    // PRINT FINAL SUMMARY
    // ==========================================

    public void PrintSummary()
    {
        Debug.Log(
            "=== GOLDEN HOUR TRIAGE REPORT ==="
        );

        Debug.Log(
            "Score: " + score
        );

        Debug.Log(
            "Correct: " +
            correctClassifications
        );

        Debug.Log(
            "Incorrect: " +
            incorrectClassifications
        );
    }
}