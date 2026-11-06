namespace GoldenHour
{
    public static class TriageEvaluator
    {
        public static TriageCategory Evaluate(PatientData patient)
        {
            if (patient == null)
            {
                throw new System.ArgumentNullException(nameof(patient));
            }

            if (patient.canWalk)
            {
                return TriageCategory.Green;
            }

            if (!patient.breathing || patient.severeBleeding || !patient.followsCommands)
            {
                return TriageCategory.Red;
            }

            return TriageCategory.Yellow;
        }
    }
}
