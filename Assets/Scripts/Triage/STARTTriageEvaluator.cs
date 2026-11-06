public static class STARTTriageEvaluator
{
    public static TriageCategory Evaluate(PatientData patient)
    {
        // 1. Walking wounded
        if (patient.canWalk)
        {
            return TriageCategory.Green;
        }

        // 2. Patient is not breathing
        if (!patient.breathing)
        {
            // Airway has not been opened
            if (!patient.airwayOpened)
            {
                return TriageCategory.Black;
            }

            // Breathing resumes after airway intervention
            if (patient.breathingAfterAirway)
            {
                return TriageCategory.Red;
            }

            // Still not breathing
            return TriageCategory.Black;
        }

        // 3. Respiratory rate
        if (patient.respiratoryRate > 30)
        {
            return TriageCategory.Red;
        }

        // 4. Perfusion
        if (!patient.radialPulsePresent)
        {
            return TriageCategory.Red;
        }

        // 5. Mental status
        if (!patient.followsCommands)
        {
            return TriageCategory.Red;
        }

        // 6. Otherwise delayed
        return TriageCategory.Yellow;
    }
}
