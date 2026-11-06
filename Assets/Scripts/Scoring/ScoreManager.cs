using UnityEngine;

/// <summary>
/// Golden Hour scoring. The score is recalculated from the current state so that a
/// reassessed (corrected) triage decision is reflected immediately. Accuracy always
/// dominates: one correct triage (100) is worth more than the maximum time bonus (50).
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public const int CorrectTriagePoints = 100;
    public const int AidPoints = 75;
    public const int ObjectivePoints = 25;
    public const int WrongTriagePenalty = 15;
    public const int WrongSupplyPenalty = 10;
    public const int MaxTimeBonus = 50;
    public const float SecondsPerBonusPoint = 18f;

    public int score;
    public int correctClassifications;
    public int incorrectClassifications;
    public int triageMistakes;
    public int supplyMistakes;
    public int examinedPatients;
    public int completedTreatments;
    public int completedObjectives;
    public int timeBonus;
    private bool timeBonusAwarded;

    public void RecordResult(TriageCategory selected, TriageCategory expected)
    {
        SetTriageResult(false, false, selected == expected);
    }

    /// <summary>Records a triage decision, replacing the previous one for the same patient.</summary>
    public void SetTriageResult(bool previouslyTriaged, bool wasPreviouslyCorrect, bool isCorrect)
    {
        if (previouslyTriaged)
        {
            if (wasPreviouslyCorrect) correctClassifications--;
            else incorrectClassifications--;
        }

        if (isCorrect) correctClassifications++;
        else
        {
            incorrectClassifications++;
            triageMistakes++;
        }

        Recalculate();
    }

    public void MarkExamined()
    {
        examinedPatients++;
        Recalculate();
    }

    public void MarkTreatmentCompleted()
    {
        completedTreatments++;
        Recalculate();
    }

    public void RecordWrongSupply()
    {
        supplyMistakes++;
        Recalculate();
    }

    public void AwardObjective()
    {
        completedObjectives++;
        Recalculate();
    }

    public int AddTimeBonus(float remainingSeconds)
    {
        if (timeBonusAwarded)
        {
            return 0;
        }

        timeBonusAwarded = true;
        timeBonus = Mathf.Clamp(Mathf.FloorToInt(remainingSeconds / SecondsPerBonusPoint),
            0, MaxTimeBonus);
        Recalculate();
        return timeBonus;
    }

    public static int MaximumScore(int patientCount, int objectiveCount)
    {
        return patientCount * (CorrectTriagePoints + AidPoints) +
               objectiveCount * ObjectivePoints + MaxTimeBonus;
    }

    public void ResetScore()
    {
        score = 0;
        correctClassifications = 0;
        incorrectClassifications = 0;
        triageMistakes = 0;
        supplyMistakes = 0;
        examinedPatients = 0;
        completedTreatments = 0;
        completedObjectives = 0;
        timeBonus = 0;
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

    private void Recalculate()
    {
        int total = correctClassifications * CorrectTriagePoints +
                    completedTreatments * AidPoints +
                    completedObjectives * ObjectivePoints +
                    timeBonus -
                    triageMistakes * WrongTriagePenalty -
                    supplyMistakes * WrongSupplyPenalty;
        score = Mathf.Max(0, total);
    }
}
