using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public int score;
    public int correctClassifications;
    public int incorrectClassifications;
    public int examinedPatients;
    public int completedTreatments;
    public int completedObjectives;
    private bool timeBonusAwarded;

    public void RecordResult(
        TriageCategory selected,
        TriageCategory expected)
    {
        SetTriageResult(false, false, selected == expected);
    }

    public void SetTriageResult(bool previouslyTriaged, bool wasPreviouslyCorrect,
        bool isCorrect)
    {
        if (!previouslyTriaged)
        {
            score += isCorrect ? 100 : -25;
        }

        if (previouslyTriaged && wasPreviouslyCorrect == isCorrect)
        {
            return;
        }

        if (previouslyTriaged)
        {
            if (wasPreviouslyCorrect) correctClassifications--;
            else incorrectClassifications--;
        }

        if (isCorrect) correctClassifications++;
        else incorrectClassifications++;
    }

    public void MarkExamined()
    {
        examinedPatients++;
    }

    public void MarkTreatmentCompleted()
    {
        completedTreatments++;
        score += 75;
    }

    public void AwardObjective()
    {
        completedObjectives++;
        score += 25;
    }

    public int AddTimeBonus(float remainingSeconds)
    {
        if (timeBonusAwarded)
        {
            return 0;
        }

        timeBonusAwarded = true;
        int bonus = Mathf.Clamp(Mathf.FloorToInt(remainingSeconds / 18f), 0, 50);
        score += bonus;
        return bonus;
    }

    public void ResetScore()
    {
        score = 0;
        correctClassifications = 0;
        incorrectClassifications = 0;
        examinedPatients = 0;
        completedTreatments = 0;
        completedObjectives = 0;
        timeBonusAwarded = false;
    }

    public void PrintSummary()
    {
        Debug.Log("Golden Hour report | Score: " + score +
                  " | Correct triage: " + correctClassifications +
                  " | Incorrect triage: " + incorrectClassifications +
                  " | Examined: " + examinedPatients +
                  " | Simulated aid: " + completedTreatments);
    }
}