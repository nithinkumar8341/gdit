using System.Collections.Generic;

namespace GoldenHour
{
    /// <summary>Rule-based personalised feedback from the score ledger and casualty records.</summary>
    public static class DebriefFeedback
    {
        public static List<string> Build(IncidentDirector director)
        {
            List<string> feedback = new List<string>();
            ScoreLedger ledger = director.Ledger;
            int under = 0;
            int over = 0;
            int wrongBlack = 0;
            float decisionTotal = 0f;
            int decisions = 0;
            foreach (Casualty casualty in director.Casualties)
            {
                TriageRecord record = casualty.Record;
                if (!record.HasTag) continue;
                if (record.FirstTag == TriageTag.Black && record.FirstTruth != TriageTag.Black) wrongBlack++;
                else if (record.FirstTag != record.FirstTruth)
                {
                    if (StartTriage.Priority(record.FirstTag) > StartTriage.Priority(record.FirstTruth)) under++;
                    else over++;
                }

                if (record.DecisionMinutes >= 0f && !casualty.AutoTaggedGreen)
                {
                    decisionTotal += record.DecisionMinutes;
                    decisions++;
                }
            }

            int untagged = director.Casualties.Count - CountTagged(director);
            int preventable = 0;
            foreach (Casualty casualty in director.Casualties)
            {
                if (casualty.Sim.PreventableDeath) preventable++;
            }

            if (under > 0)
            {
                feedback.Add("You under-triaged " + under + " casualt" + (under == 1 ? "y" : "ies") +
                             ". Work through START in order: breathing rate over 30, capillary refill over 2 seconds and not following commands each mean RED.");
            }

            if (over > 0) feedback.Add("You over-triaged " + over + " casualt" + (over == 1 ? "y" : "ies") + ". Over-triage sends scarce ambulances to people who can wait.");
            if (wrongBlack > 0) feedback.Add("You tagged a salvageable casualty BLACK. Only tag BLACK if there is no breathing after the airway has been opened.");
            if (untagged > 0) feedback.Add(untagged + " casualt" + (untagged == 1 ? "y was" : "ies were") + " never tagged. Keep moving: aim for under a minute per person.");
            if (preventable > 0)
            {
                feedback.Add(preventable + " preventable death" + (preventable == 1 ? "" : "s") +
                             ". Control severe bleeding and open airways early; those are the life-saving steps in START.");
            }

            if (ledger.Count("gloves", true) > 0) feedback.Add("You touched casualties without gloves. Put gloves on before the first contact.");
            if (ledger.Count("handover", true) > 0) feedback.Add("Some handovers were out of order. Send RED before YELLOW, and YELLOW before GREEN.");
            if (ledger.Sum("dispatch") < ScoreRules.DispatchFields * ScoreRules.DispatchField)
            {
                feedback.Add("Your dispatch report was incomplete. Count casualties and hazards before calling 112.");
            }

            if (ledger.Count("command") == 0 && HasWalkers(director)) feedback.Add("Shout \"if you can walk, go to the green zone\" first. It clears the walking wounded in seconds.");
            if (decisions > 0)
            {
                float mean = decisionTotal / decisions;
                feedback.Add("Average decision time: " + mean.ToString("0.0") + " simulated minutes per casualty" +
                             (mean > 2f ? " - try to be quicker; START is designed for about 30-60 seconds." : " - good pace."));
            }

            if (ledger.Count("retriage") > 0) feedback.Add("You re-triaged after re-examining. Conditions change, so re-checking people is good practice.");
            if (feedback.Count == 0) feedback.Add("Excellent work: accurate, safe and well prioritised.");
            return feedback;
        }

        private static int CountTagged(IncidentDirector director)
        {
            int count = 0;
            foreach (Casualty casualty in director.Casualties)
            {
                if (casualty.Record.HasTag) count++;
            }

            return count;
        }

        private static bool HasWalkers(IncidentDirector director)
        {
            foreach (CasualtyPlacement placement in director.Scenario.casualties)
            {
                if (placement.profile.initial.canWalk) return true;
            }

            return false;
        }
    }
}
