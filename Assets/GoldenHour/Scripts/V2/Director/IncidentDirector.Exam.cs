using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoldenHour
{
    /// <summary>Examination by actions, triage tags (with retriage rules) and first aid.</summary>
    public sealed partial class IncidentDirector
    {
        public const string ExamTitlePrefix = "CASUALTY ";
        private Casualty examTarget;

        public Casualty ExamTarget => examTarget;

        public static float ExamSeconds(ExamAction action)
        {
            switch (action)
            {
                case ExamAction.AskToWalk: return 1.5f;
                case ExamAction.CheckBreathing: return 3f;
                case ExamAction.OpenAirway: return 2.5f;
                case ExamAction.CheckCirculation: return 3f;
                case ExamAction.GiveCommand: return 2f;
                default: return 1.5f;
            }
        }

        private static string ExamLabel(ExamAction action)
        {
            switch (action)
            {
                case ExamAction.AskToWalk: return "Ask to walk";
                case ExamAction.CheckBreathing: return "Check breathing";
                case ExamAction.OpenAirway: return "Open airway";
                case ExamAction.CheckCirculation: return "Check circulation";
                case ExamAction.GiveCommand: return "Give a command";
                default: return "Look for bleeding";
            }
        }

        /// <summary>Hands-on actions need gloves; asking, commanding and looking do not.</summary>
        private static bool NeedsContact(ExamAction action)
        {
            return action == ExamAction.CheckBreathing || action == ExamAction.OpenAirway || action == ExamAction.CheckCirculation;
        }

        public void OpenExamination(Casualty casualty)
        {
            if (Finished || casualty == null) return;
            if (!Dispatched)
            {
                hud.SetStatus("Scene safety first: mark hazards and radio the dispatch report (" + GameInput.KeyName(GameInput.Radio) +
                              ") before treating anyone.");
                return;
            }

            if (!casualty.CanInteract) return;
            examTarget = casualty;
            if (player != null) player.ForcedKneel = true;
            if (audioManager != null) audioManager.PlayPatient();
            Log("exam-open", casualty.Id, "Opened examination");
            BuildExamPanel();
        }

        private void BuildExamPanel()
        {
            Casualty casualty = examTarget;
            if (casualty == null) return;
            CasualtyProfile profile = casualty.Profile;
            StringBuilder body = new StringBuilder();
            body.Append("<b>").Append(profile.displayName).Append("</b>, about ").Append(profile.age).Append(" - ").Append(profile.appearance);
            body.Append("\n<color=#CDD3D8>").Append(profile.injurySummary).Append("</color>\n");
            if (casualty.Sim.State.deceased)
            {
                body.Append("\n<color=#FF8A8A>No signs of life.</color>\n");
            }

            body.Append("\n<b>FINDINGS</b>  <size=85%><color=#CDD3D8>(minute ").Append(Minute.ToString("0.0")).Append(")</color></size>\n");
            if (casualty.Findings.Count == 0) body.Append("<i>Nothing checked yet. Use the actions below; each one takes time.</i>\n");
            foreach (ExamAction action in System.Enum.GetValues(typeof(ExamAction)))
            {
                if (!casualty.Findings.TryGetValue(action, out Finding finding)) continue;
                float age = Minute - finding.minute;
                body.Append("- ").Append(finding.text).Append("  <size=80%><color=#9AA4AD>(")
                    .Append(age < 0.95f ? "just now" : Mathf.RoundToInt(age) + " min ago").Append(")</color></size>\n");
            }

            body.Append("\n<b>TAG</b>  ").Append(casualty.Record.HasTag ? TriageTagVisual.UiLabel(casualty.Record.CurrentTag) : "not tagged");
            if (casualty.Record.HasTag && !casualty.Record.CanRetag) body.Append("  <size=85%><color=#CDD3D8>(re-examine to change)</color></size>");
            if (casualty.Sim.Applied != Treatment.None) body.Append("\n<b>AID GIVEN</b>  ").Append(TreatmentNames.Describe(casualty.Sim.Applied));
            if (!GlovesOn) body.Append("\n<color=#FF8A8A>Gloves are OFF - hands-on checks and aid without gloves are penalised.</color>");
            if (HintsEnabled) body.Append("\n<size=85%><color=#F6D171>HINT (START): walking? -> breathing (open the airway if not) -> rate over 30? -> circulation (refill over 2 s / no radial pulse) -> follows commands?</color></size>");

            OpenPanel(ExamTitlePrefix + casualty.Id, body.ToString(), new Vector2(1240f, 820f), 372f, true);
            Transform panel = modal.Panel;
            bool idle = !ActionRunning && !Finished;
            bool alive = !casualty.Sim.State.deceased;

            ExamAction[] actions =
            {
                ExamAction.AskToWalk, ExamAction.CheckBreathing, ExamAction.OpenAirway,
                ExamAction.LookForBleeding, ExamAction.CheckCirculation, ExamAction.GiveCommand
            };
            for (int i = 0; i < actions.Length; i++)
            {
                ExamAction action = actions[i];
                float x = -400f + (i % 3) * 400f;
                float y = modal.RowY(i < 3 ? 4 : 3);
                Button button = UiFactory.Button(panel, ExamLabel(action) + "  <size=75%>(" + ExamSeconds(action).ToString("0.#") + " s)</size>",
                    new Vector2(x, y), new Vector2(380f, 54f), () => PerformExam(casualty, action), Palette.Slate);
                button.interactable = idle;
            }

            TriageTag[] tags = { TriageTag.Red, TriageTag.Yellow, TriageTag.Green, TriageTag.Black };
            for (int i = 0; i < tags.Length; i++)
            {
                TriageTag tag = tags[i];
                TagRequestResult check = casualty.Record.Check(tag);
                bool allowed = check == TagRequestResult.FirstTag || check == TagRequestResult.Retag;
                string text = (casualty.Record.HasTag ? "RE-TAG " : "TAG ") + TriageTagVisual.UiLabel(tag).Split('|')[0].Trim();
                Button button = UiFactory.Button(panel, text, new Vector2(-450f + i * 300f, modal.RowY(2)), new Vector2(285f, 54f),
                    () => RequestTag(casualty, tag), TriageTagVisual.ColorFor(tag));
                button.interactable = idle && allowed;
            }

            Treatment[] treatments = { Treatment.PressureDressing, Treatment.AirwayOpening, Treatment.RecoveryPosition, Treatment.Blanket, Treatment.Cpr };
            for (int i = 0; i < treatments.Length; i++)
            {
                Treatment treatment = treatments[i];
                if (treatment == Treatment.AirwayOpening) treatment = Treatment.Reassure;
                Button button = UiFactory.Button(panel, TreatmentLabel(treatment), new Vector2(-480f + i * 240f, modal.RowY(1)),
                    new Vector2(228f, 54f), () => RequestTreatment(casualty, treatment), new Color32(52, 92, 120, 255));
                button.interactable = idle && alive && (casualty.Sim.Applied & treatment) == 0;
            }

            UiFactory.Button(panel, "CLOSE  <size=75%>(Esc)</size>", new Vector2(0f, modal.RowY(0)), new Vector2(260f, 50f), CloseModal,
                Palette.Charcoal);
        }

        private string TreatmentLabel(Treatment treatment)
        {
            switch (treatment)
            {
                case Treatment.PressureDressing: return "Pressure dressing (" + Dressings + ")";
                case Treatment.RecoveryPosition: return "Recovery position";
                case Treatment.Blanket: return "Blanket (" + Blankets + ")";
                case Treatment.Cpr: return "CPR";
                case Treatment.Reassure: return "Reassure";
                default: return TreatmentNames.Name(treatment);
            }
        }

        private void ApplyGlovePenalty(Casualty casualty, string what)
        {
            if (GlovesOn || casualty.GlovesPenaltyApplied) return;
            casualty.GlovesPenaltyApplied = true;
            Score("gloves", ScoreRules.NoGloves, "Touched " + casualty.Id + " without gloves (" + what + ")", casualty.Id);
        }

        public void PerformExam(Casualty casualty, ExamAction action)
        {
            if (ActionRunning || Finished) return;
            if (NeedsContact(action)) ApplyGlovePenalty(casualty, ExamLabel(action).ToLowerInvariant());
            RunAction(ExamLabel(action) + " - " + casualty.Id, ExamSeconds(action), false, () =>
            {
                string text = ExamResult(casualty, action);
                casualty.AddFinding(action, text, Minute);
                casualty.Record.RecordExamination(Minute, ExamLabel(action) + ": " + text);
                Log("exam", casualty.Id, ExamLabel(action) + ": " + text);
                if (examTarget == casualty && modal.IsOpen) BuildExamPanel();
            });
        }

        private string ExamResult(Casualty casualty, ExamAction action)
        {
            PhysiologyState state = casualty.Sim.State;
            switch (action)
            {
                case ExamAction.AskToWalk:
                    if (state.deceased) return "Walking: no response.";
                    if (state.canWalk) return "Walking: gets up and walks a few steps.";
                    return state.followsCommands ? "Walking: tries, but cannot stand or walk." : "Walking: no response to the request.";
                case ExamAction.CheckBreathing:
                    if (state.Breathing) return "Breathing: " + casualty.Sim.MeasureRespiratoryRate() + " breaths per minute.";
                    return "Breathing: NOT breathing.";
                case ExamAction.OpenAirway:
                    if (state.deceased || state.respiratoryRate <= 0) return "Airway opened (head tilt, chin lift): still NOT breathing.";
                    if (state.airwayObstructed && !state.airwayOpened)
                    {
                        TreatmentResult result = casualty.Sim.Treat(Treatment.AirwayOpening);
                        if (result == TreatmentResult.Effective)
                        {
                            if ((casualty.Profile.indicatedTreatments & Treatment.AirwayOpening) != 0)
                            {
                                Score("aid", ScoreRules.TreatmentEffective, "Opened " + casualty.Id + "'s airway", casualty.Id);
                            }

                            return "Airway opened: breathing STARTED, " + casualty.Sim.MeasureRespiratoryRate() +
                                   " per minute. Keep the airway open.";
                        }
                    }

                    return "Airway: already open and clear.";
                case ExamAction.CheckCirculation:
                    if (state.deceased) return "Circulation: no pulse.";
                    return "Circulation: capillary refill " + (Mathf.Ceil(state.capillaryRefill * 10f - 0.001f) / 10f).ToString("0.0") + " s, radial pulse " +
                           (state.radialPulse ? "present" : "ABSENT") + ".";
                case ExamAction.GiveCommand:
                    if (state.deceased) return "Commands: no response.";
                    return state.followsCommands ? "Commands: squeezes your hand when asked." : "Commands: does NOT follow simple commands.";
                default:
                    if (state.bleeding == BleedingLevel.Severe) return "Bleeding: heavy, continuous bleeding (" + BleedingSite(casualty) + ").";
                    if (state.bleeding == BleedingLevel.Minor)
                    {
                        return state.bleedingControlled ? "Bleeding: controlled by the dressing." : "Bleeding: minor only.";
                    }

                    return "Bleeding: none visible.";
            }
        }

        private static string BleedingSite(Casualty casualty)
        {
            string summary = casualty.Profile.injurySummary.ToLowerInvariant();
            if (summary.Contains("leg") || summary.Contains("thigh")) return "leg";
            if (summary.Contains("arm") || summary.Contains("forearm")) return "arm";
            if (summary.Contains("head") || summary.Contains("scalp")) return "head";
            return "wound";
        }

        public void RequestTag(Casualty casualty, TriageTag tag)
        {
            if (ActionRunning || Finished) return;
            TagRequestResult check = casualty.Record.Check(tag);
            if (check == TagRequestResult.RejectedNoExamination)
            {
                hud.SetStatus("Examine " + casualty.Id + " before tagging.");
                return;
            }

            if (check == TagRequestResult.RejectedNeedsReexamination)
            {
                hud.SetStatus("Re-examine " + casualty.Id + " before changing the tag (retriage must be justified).");
                Log("retag-blocked", casualty.Id, "Retag to " + tag + " blocked: no re-examination");
                return;
            }

            if (check == TagRequestResult.RejectedSameTag) return;
            bool retag = check == TagRequestResult.Retag;
            RunAction((retag ? "Re-tagging " : "Attaching tag to ") + casualty.Id, retag ? 3f : 1.5f, false, () =>
            {
                TriageTag truth = casualty.Sim.Evaluate(out StartStep step);
                int points = casualty.Record.ApplyTag(Minute, tag, truth, step, out TagRequestResult result);
                casualty.SetTag(tag);
                if (result == TagRequestResult.FirstTag)
                {
                    Score("triage", points, casualty.Id + " tagged " + tag + " - " + ScoreRules.FirstTagVerdict(tag, truth), casualty.Id);
                }
                else if (result == TagRequestResult.Retag)
                {
                    Score("retriage", points, casualty.Id + " re-tagged " + tag + (points > 0 ? " after a real change in condition" : " (no change in condition)"),
                        casualty.Id);
                }

                Log("tag", casualty.Id, (retag ? "retag " : "tag ") + tag + " (START: " + truth + ", " + step + ")");
                if (examTarget == casualty && modal.IsOpen) BuildExamPanel();
            });
        }

        public void RequestTreatment(Casualty casualty, Treatment treatment)
        {
            if (ActionRunning || Finished) return;
            if (casualty.Sim.State.deceased && treatment != Treatment.Cpr)
            {
                hud.SetStatus(casualty.Id + " shows no signs of life.");
                return;
            }

            if ((casualty.Sim.Applied & treatment) != 0)
            {
                hud.SetStatus(TreatmentNames.Name(treatment) + " already given to " + casualty.Id + ".");
                return;
            }

            if (treatment == Treatment.PressureDressing && Dressings <= 0)
            {
                hud.SetStatus(KitCollected ? "No pressure dressings left." : "Collect the first-aid bag first.");
                return;
            }

            if (treatment == Treatment.Blanket && Blankets <= 0)
            {
                hud.SetStatus(KitCollected ? "No blankets left." : "Collect the first-aid bag first.");
                return;
            }

            if (treatment != Treatment.Reassure) ApplyGlovePenalty(casualty, TreatmentNames.Name(treatment).ToLowerInvariant());
            switch (treatment)
            {
                case Treatment.PressureDressing:
                    RunAction("Pressure dressing on " + casualty.Id + " - HOLD " + GameInput.KeyName(GameInput.Primary), 3f, true,
                        () => FinishTreatment(casualty, treatment));
                    break;
                case Treatment.RecoveryPosition:
                    StartRecoveryPosition(casualty);
                    break;
                case Treatment.Cpr:
                    StartCpr(casualty);
                    break;
                case Treatment.Blanket:
                    RunAction("Wrapping " + casualty.Id + " in a blanket", 2f, false, () => FinishTreatment(casualty, treatment));
                    break;
                default:
                    RunAction("Reassuring " + casualty.Id, 1.5f, false, () => FinishTreatment(casualty, treatment));
                    break;
            }
        }

        /// <summary>Applies the treatment to the simulation, consumes supplies and scores it.</summary>
        public void FinishTreatment(Casualty casualty, Treatment treatment)
        {
            if (treatment == Treatment.PressureDressing) Dressings = Mathf.Max(0, Dressings - 1);
            if (treatment == Treatment.Blanket) Blankets = Mathf.Max(0, Blankets - 1);
            TreatmentResult result = casualty.Sim.Treat(treatment);
            bool indicated = (casualty.Profile.indicatedTreatments & treatment) != 0;
            if (result == TreatmentResult.Effective && indicated)
            {
                Score("aid", ScoreRules.TreatmentEffective, TreatmentLabel(treatment).Split('(')[0].Trim() + " for " + casualty.Id, casualty.Id);
                if (treatment == Treatment.Reassure) casualty.Say("Thank you... thank you.");
            }
            else if (treatment == Treatment.Cpr)
            {
                hud.SetStatus("In mass-casualty triage, CPR is deprioritised: the time is better used for people who can still be saved.", 8f);
            }
            else if (result == TreatmentResult.NotIndicated || (result == TreatmentResult.Effective && !indicated))
            {
                if (treatment != Treatment.Reassure)
                {
                    Score("aid", ScoreRules.TreatmentNotIndicated, TreatmentLabel(treatment).Split('(')[0].Trim() + " was not needed for " + casualty.Id,
                        casualty.Id);
                }
            }

            Log("treat", casualty.Id, TreatmentNames.Name(treatment) + " -> " + result + (indicated ? " (indicated)" : " (not indicated)"));
            casualty.ApplyRestPose();
            if (examTarget == casualty && modal.IsOpen) BuildExamPanel();
        }
    }
}
