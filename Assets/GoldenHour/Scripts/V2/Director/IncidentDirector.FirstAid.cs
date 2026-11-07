using UnityEngine;

namespace GoldenHour
{
    /// <summary>Interactive first-aid procedures (recovery position and CPR) as minigames.</summary>
    public sealed partial class IncidentDirector
    {
        private void StartRecoveryPosition(Casualty casualty)
        {
            if (InstantActions)
            {
                RunAction("Recovery position - " + casualty.Id, 4f, false, () => FinishTreatment(casualty, Treatment.RecoveryPosition));
                return;
            }

            StartMinigame(casualty, MinigameKind.RecoveryPosition, success =>
            {
                if (success)
                {
                    FinishTreatment(casualty, Treatment.RecoveryPosition);
                    hud.SetStatus(casualty.Id + " is on their side with the airway open. Keep checking their breathing.", 5f);
                }
                else
                {
                    casualty.ApplyRestPose();
                    hud.SetStatus("Recovery position cancelled.", 3f);
                }
            });
        }

        private void StartCpr(Casualty casualty)
        {
            if (InstantActions)
            {
                RunAction("CPR - " + casualty.Id, 6f, false, () => FinishTreatment(casualty, Treatment.Cpr));
                return;
            }

            StartMinigame(casualty, MinigameKind.Cpr, success =>
            {
                if (!success)
                {
                    hud.SetStatus("CPR stopped.", 3f);
                    return;
                }

                Log("cpr", casualty.Id, "Rate " + FirstAidMinigame.LastAverageRate.ToString("0") + "/min, " +
                                        Mathf.RoundToInt(FirstAidMinigame.LastInRangeFraction * 100f) + "% of compressions in 100-120");
                FinishTreatment(casualty, Treatment.Cpr);
                hud.SetStatus("Average rate " + Mathf.RoundToInt(FirstAidMinigame.LastAverageRate) + "/min (target 100-120). " +
                              "In mass-casualty triage, CPR is deprioritised: the time is better used for people who can still be saved.", 9f);
            });
        }

        private void StartMinigame(Casualty casualty, MinigameKind kind, System.Action<bool> done)
        {
            modal.SetHidden(true);
            hud.SetProgress(string.Empty, 0f, false);
            FrameCasualty(casualty, 0.5f);
            FirstAidMinigame.Begin(this, casualty, kind, hud.OverlayRoot.transform, success =>
            {
                modal.SetHidden(false);
                if (examTarget == casualty && modal.IsOpen) FrameCasualty(casualty, ExamFramingX);
                done(success);
                if (examTarget == casualty && modal.IsOpen) BuildExamPanel();
            });
        }
    }
}
