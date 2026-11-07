using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoldenHour
{
    /// <summary>Briefing, dispatch report, handover, radio, casualty tablet, pause and tutorial panels.</summary>
    public sealed partial class IncidentDirector
    {
        public const string TabletTitle = "CASUALTY TABLET";
        public const string DispatchTitle = "DISPATCH REPORT - 112";
        public const string HandoverTitlePrefix = "HANDOVER - AMBULANCE ";
        private readonly DispatchReport dispatchDraft = new DispatchReport();

        public DispatchReport DispatchDraft => dispatchDraft;
        public int DispatchFieldsCorrect { get; private set; } = -1;

        // -------------------------------------------------------------- briefing

        private void OnStartPresentation()
        {
            if (player != null) player.SetControlsEnabled(false);
            hud.SetVisible(false);
            if (Cutscene.Play(this, CutsceneKind.Opening, ShowBriefing)) Phase = IncidentPhase.Intro;
            else ShowBriefing();
        }

        private void UpdateIntro()
        {
            if (GameInput.Pressed(GameInput.Pause) || GameInput.Pressed(GameInput.Interact) || GameInput.Pressed(GameInput.Primary)) Cutscene.Skip();
        }

        public void ShowBriefing()
        {
            Phase = IncidentPhase.Briefing;
            hud.SetVisible(false);
            string body = Scenario.briefing + "\n\n<b>First:</b> make the scene safe - collect the first-aid bag, spot and mark hazards (" +
                          GameInput.KeyName(GameInput.Interact) + "), put on gloves (" + GameInput.KeyName(GameInput.Gloves) +
                          ") and radio the dispatch report (" + GameInput.KeyName(GameInput.Radio) + ").\n" +
                          "<b>The golden-hour clock never stops</b> while you examine, tag or treat. Only the pause menu stops it.\n\n" +
                          "<size=85%><color=#CDD3D8><i>" + Disclaimer + "</i></color></size>";
            modal.Show(Scenario.title.ToUpperInvariant() + "  |  BRIEFING", body, new Vector2(1000f, 640f), 100f, false);
            UiFactory.Button(modal.Panel, "BEGIN", new Vector2(0f, modal.RowY(0)), new Vector2(300f, 58f), BeginScenario);
        }

        public void BeginScenario()
        {
            if (Phase != IncidentPhase.Briefing) return;
            modal.Close();
            Phase = IncidentPhase.Safety;
            hud.SetVisible(true);
            if (player != null) player.SetControlsEnabled(true);
            Log("phase", string.Empty, "Scene safety phase started");
            Radio(Scenario.radio.Count > 0 && Scenario.radio[0].minute <= 0f
                ? Scenario.radio[0].text
                : "Incident command: you are first on scene. Make it safe, then send me a report.");
            if (Scenario.radio.Count > 0 && Scenario.radio[0].minute <= 0f) playedCues.Add(0);
            if (Scenario.tutorial) tutorialStep = 0;
            else hud.ShowTip("<b>TIP</b>  Hazards first. Look around, mark them with " + GameInput.KeyName(GameInput.Interact) +
                             ", then radio 112 with " + GameInput.KeyName(GameInput.Radio) + ".", 12f);
            RefreshHud();
        }

        // -------------------------------------------------------------- tutorial

        private void UpdateTutorial()
        {
            if (tutorialStep < 0 || Scenario.tutorialSteps == null || tutorialStep >= Scenario.tutorialSteps.Length) return;
            bool advance;
            switch (tutorialStep)
            {
                case 0: advance = KitCollected; break;
                case 1: advance = GlovesOn; break;
                case 2: advance = hazards.TrueForAll(h => h.Marked); break;
                case 3: advance = Dispatched; break;
                case 4: advance = shoutCredited || casualties.TrueForAll(c => !c.Sim.State.canWalk || c.InGreenZone); break;
                case 5: advance = casualties.FindAll(c => c.Record.HasTag).Count >= casualties.Count; break;
                case 6: advance = casualties.Exists(c => c.Sim.Applied != Treatment.None); break;
                case 7: advance = casualties.Exists(c => c.Sim.Transported || c.AssignedToAmbulance); break;
                default: advance = false; break;
            }

            string text = "<b>TUTORIAL " + (tutorialStep + 1) + "/" + Scenario.tutorialSteps.Length + "</b>  " + KeyTokens(Scenario.tutorialSteps[tutorialStep]);
            if (hud.TipVisible == false || !hud.TipText.Contains("TUTORIAL " + (tutorialStep + 1) + "/")) hud.ShowTip(text, 0f);
            if (advance)
            {
                tutorialStep++;
                if (tutorialStep >= Scenario.tutorialSteps.Length) hud.ShowTip(string.Empty, 0f);
            }
        }

        public int TutorialStep => tutorialStep;

        /// <summary>Replaces {Interact}, {Gloves}, {Radio}, {Shout}, {Tablet}, {Primary}, {Kneel} with the current key bindings.</summary>
        public static string KeyTokens(string text)
        {
            return text.Replace("{Interact}", GameInput.KeyName(GameInput.Interact)).Replace("{Gloves}", GameInput.KeyName(GameInput.Gloves))
                .Replace("{Radio}", GameInput.KeyName(GameInput.Radio)).Replace("{Shout}", GameInput.KeyName(GameInput.Shout))
                .Replace("{Tablet}", GameInput.KeyName(GameInput.Tablet)).Replace("{Primary}", GameInput.KeyName(GameInput.Primary))
                .Replace("{Kneel}", GameInput.KeyName(GameInput.Crouch));
        }

        // ------------------------------------------------------------- dispatch

        private void OpenRadio()
        {
            if (!Dispatched)
            {
                OpenDispatch();
                return;
            }

            StringBuilder log = new StringBuilder();
            int shown = 0;
            for (int i = run.events.Count - 1; i >= 0 && shown < 9; i--)
            {
                if (run.events[i].type != "radio") continue;
                log.Insert(0, "<color=#9AA4AD>" + run.events[i].minute.ToString("00.0") + "</color>  " + run.events[i].detail + "\n");
                shown++;
            }

            OpenPanel("RADIO LOG", log.Length > 0 ? log.ToString() : "No messages yet.", new Vector2(900f, 560f), 100f, true);
            UiFactory.Button(modal.Panel, "CLOSE", new Vector2(0f, modal.RowY(0)), new Vector2(240f, 50f), CloseModal, Palette.Slate);
        }

        public void OpenDispatch()
        {
            if (Dispatched || Finished) return;
            if (audioManager != null) audioManager.PlayRadio();
            StringBuilder body = new StringBuilder("Tell the emergency call-taker what they need to send the right help. You are scored on each answer.\n");
            body.Append("<size=85%><color=#CDD3D8>Look around before you call: count the casualties and the hazards you can see.</color></size>");
            OpenPanel(DispatchTitle, body.ToString(), new Vector2(1240f, 840f), 680f, true);
            Transform panel = modal.Panel;
            float y = 250f;

            Label(panel, "1. Number to call", y);
            string[] numbers = { "112", "911", "999", "101" };
            for (int i = 0; i < numbers.Length; i++)
            {
                string number = numbers[i];
                Option(panel, number, new Vector2(-160f + i * 150f, y), new Vector2(140f, 46f), dispatchDraft.emergencyNumber == number,
                    () => { dispatchDraft.emergencyNumber = number; OpenDispatch(); });
            }

            y -= 80f;
            Label(panel, "2. Location", y);
            for (int i = 0; i < Scenario.locationOptions.Length; i++)
            {
                int index = i;
                Option(panel, Scenario.locationOptions[i], new Vector2(-160f + i * 290f + 70f, y), new Vector2(280f, 46f),
                    dispatchDraft.locationIndex == index, () => { dispatchDraft.locationIndex = index; OpenDispatch(); });
            }

            y -= 80f;
            Label(panel, "3. Number of casualties", y);
            int actual = casualties.Count;
            int[] counts = { Mathf.Max(1, actual - 4), Mathf.Max(1, actual - 2), actual, actual + 2, actual + 4 };
            for (int i = 0; i < counts.Length; i++)
            {
                int count = counts[i];
                Option(panel, count.ToString(), new Vector2(-160f + i * 120f, y), new Vector2(110f, 46f), dispatchDraft.casualtyCount == count,
                    () => { dispatchDraft.casualtyCount = count; OpenDispatch(); });
            }

            y -= 80f;
            Label(panel, "4. Hazards seen", y);
            HazardType[] types = (HazardType[])System.Enum.GetValues(typeof(HazardType));
            for (int i = 0; i < types.Length; i++)
            {
                HazardType type = types[i];
                int bit = 1 << (int)type;
                bool on = (dispatchDraft.hazardMask & bit) != 0;
                float x = -160f + (i % 3) * 290f + 70f;
                float rowY = y - (i / 3) * 56f;
                Option(panel, (on ? "[X] " : "[  ] ") + ScenarioData.HazardName(type), new Vector2(x, rowY), new Vector2(280f, 46f), on,
                    () => { dispatchDraft.hazardMask ^= bit; OpenDispatch(); });
            }

            bool complete = dispatchDraft.locationIndex >= 0 && dispatchDraft.casualtyCount >= 0 && !string.IsNullOrEmpty(dispatchDraft.emergencyNumber);
            Button send = UiFactory.Button(panel, "SEND REPORT", new Vector2(130f, modal.RowY(0)), new Vector2(300f, 54f), SubmitDispatch);
            send.interactable = complete;
            UiFactory.Button(panel, "NOT YET", new Vector2(-200f, modal.RowY(0)), new Vector2(220f, 54f), CloseModal, Palette.Slate);
        }

        private static void Label(Transform panel, string text, float y)
        {
            UiFactory.Text(panel, "<b>" + text + "</b>", 19, new Vector2(330f, 46f), new Vector2(-430f, y), TextAlignmentOptions.MidlineLeft);
        }

        private static void Option(Transform panel, string text, Vector2 position, Vector2 size, bool selected, System.Action onClick)
        {
            UiFactory.Button(panel, text, position, size, onClick, selected ? Palette.EmergencyRed : Palette.Slate);
        }

        /// <summary>Scores and sends the dispatch report; ambulance arrival times count from now.</summary>
        public void SubmitDispatch()
        {
            if (Dispatched) return;
            if (string.IsNullOrEmpty(dispatchDraft.emergencyNumber)) dispatchDraft.emergencyNumber = "-";
            int fields = dispatchDraft.CorrectFields(Scenario.correctLocation, casualties.Count, Scenario.HazardMask);
            DispatchFieldsCorrect = fields;
            DispatchMinute = Minute;
            Phase = IncidentPhase.Response;
            Score("dispatch", fields * ScoreRules.DispatchField, "Dispatch report: " + fields + "/" + ScoreRules.DispatchFields + " details correct");
            StringBuilder feedback = new StringBuilder();
            feedback.Append(dispatchDraft.emergencyNumber == "112" ? "<color=#7FE0A6>112</color> - the European emergency number (verify locally)." :
                "<color=#FF8A8A>" + dispatchDraft.emergencyNumber + "</color> - this game uses 112, the European emergency number (verify locally).");
            feedback.Append("\nLocation: ").Append(dispatchDraft.locationIndex == Scenario.correctLocation ? "<color=#7FE0A6>correct</color>" :
                "<color=#FF8A8A>wrong</color> - it was " + Scenario.locationOptions[Scenario.correctLocation]);
            feedback.Append("\nCasualties: ").Append(Mathf.Abs(dispatchDraft.casualtyCount - casualties.Count) <= 1
                ? "<color=#7FE0A6>about right</color>" : "<color=#FF8A8A>off</color> - there are " + casualties.Count);
            feedback.Append("\nHazards: ").Append(dispatchDraft.hazardMask == Scenario.HazardMask ? "<color=#7FE0A6>all correct</color>" :
                "<color=#FF8A8A>not quite</color> - present: " + HazardList(Scenario.HazardMask));
            Log("dispatch", string.Empty, "Dispatch sent: " + fields + "/4 correct");
            Radio("Control: Copy. Ambulances are being sent; the first is due in about " + Mathf.RoundToInt(Scenario.waves.Count > 0 ? Scenario.waves[0].minute : 10f) +
                  " minutes. Start triage.");
            OpenPanel("DISPATCH SENT", feedback.ToString() + "\n\nNow shout for the walking wounded (" + GameInput.KeyName(GameInput.Shout) +
                                       "), then examine and tag everyone else.", new Vector2(900f, 480f), 100f, true);
            UiFactory.Button(modal.Panel, "CONTINUE", new Vector2(0f, modal.RowY(0)), new Vector2(260f, 54f), CloseModal);
        }

        private static string HazardList(int mask)
        {
            List<string> names = new List<string>();
            foreach (HazardType type in System.Enum.GetValues(typeof(HazardType)))
            {
                if ((mask & (1 << (int)type)) != 0) names.Add(ScenarioData.HazardName(type));
            }

            return names.Count > 0 ? string.Join(", ", names) : "none";
        }

        // ------------------------------------------------------------- handover

        public List<Casualty> HandoverCandidates()
        {
            return casualties.FindAll(c => !c.Sim.State.deceased && !c.Sim.Transported && !c.AssignedToAmbulance && !c.HeadingToGreenZone && !c.InGreenZone &&
                                           c.Record.HasTag && c.Record.CurrentTag != TriageTag.Black);
        }

        public void OpenHandover(Ambulance ambulance)
        {
            if (Finished) return;
            List<Casualty> candidates = HandoverCandidates();
            candidates.Sort((a, b) => StartTriage.Priority(a.Record.CurrentTag).CompareTo(StartTriage.Priority(b.Record.CurrentTag)));
            string body = "The crew has <b>" + ambulance.FreeSpaces + "</b> stretcher place" + (ambulance.FreeSpaces == 1 ? "" : "s") +
                          ". Hand over the highest-priority casualty first; they are scored on their real condition now.\n" +
                          (candidates.Count == 0 ? "<color=#F6D171>Nobody tagged is waiting. Tag casualties first - the crew will wait.</color>" : string.Empty);
            OpenPanel(HandoverTitlePrefix + ambulance.Number, body, new Vector2(1000f, 760f), 560f, true);
            Transform panel = modal.Panel;
            float y = 180f;
            foreach (Casualty casualty in candidates)
            {
                Casualty current = casualty;
                string notes = casualty.Findings.Count > 0 ? "  <size=75%>" + casualty.Findings.Count + " findings</size>" : string.Empty;
                Button button = UiFactory.Button(panel, current.Id + "  |  " + TriageTagVisual.UiLabel(current.Record.CurrentTag) + notes,
                    new Vector2(0f, y), new Vector2(760f, 50f), () => HandOver(ambulance, current), TriageTagVisual.ColorFor(current.Record.CurrentTag));
                button.interactable = ambulance.FreeSpaces > 0;
                y -= 58f;
                if (y < -230f) break;
            }

            UiFactory.Button(panel, "RELEASE AMBULANCE", new Vector2(-150f, modal.RowY(0)), new Vector2(280f, 52f), () =>
            {
                ReleaseAmbulance(ambulance);
                CloseModal();
            }, Palette.Slate);
            UiFactory.Button(panel, "CLOSE", new Vector2(170f, modal.RowY(0)), new Vector2(220f, 52f), CloseModal, Palette.Charcoal);
        }

        /// <summary>Hands a casualty to the ambulance crew; scored against the START priority of everyone waiting.</summary>
        public bool HandOver(Ambulance ambulance, Casualty casualty)
        {
            if (Finished || ambulance.FreeSpaces <= 0 || casualty.AssignedToAmbulance || casualty.Sim.Transported) return false;
            List<Casualty> waiting = HandoverCandidates();
            List<TriageTag> truths = new List<TriageTag>();
            foreach (Casualty other in waiting)
            {
                if (other != casualty) truths.Add(other.Sim.TrueTag);
            }

            TriageTag truth = casualty.Sim.TrueTag;
            bool correct = HandoverRules.IsCorrect(truth, truths);
            Score("handover", correct ? ScoreRules.HandoverCorrect : ScoreRules.HandoverWrong,
                "Handover " + casualty.Id + " (" + truth + ") to ambulance " + ambulance.Number + (correct ? " - right priority" : " - someone more urgent was waiting"),
                casualty.Id);
            Log("handover", casualty.Id, "Ambulance " + ambulance.Number + ", START " + truth + (correct ? ", correct order" : ", wrong order"));
            ambulance.Assign(casualty);
            if (modal.IsOpen && modal.Title.StartsWith(HandoverTitlePrefix))
            {
                if (ambulance.FreeSpaces > 0) OpenHandover(ambulance);
                else CloseModal();
            }

            return correct;
        }

        public void ReleaseAmbulance(Ambulance ambulance)
        {
            ambulance.Release();
            Log("ambulance", string.Empty, "Released ambulance " + ambulance.Number);
        }

        // --------------------------------------------------------------- tablet

        public void OpenTablet()
        {
            StringBuilder body = new StringBuilder("<size=90%>");
            foreach (Casualty casualty in casualties)
            {
                string tag = casualty.Record.HasTag ? TriageTagVisual.UiLabel(casualty.Record.CurrentTag) : "untagged";
                string state = casualty.Sim.Transported ? "transported" : casualty.InGreenZone ? "green zone" :
                    casualty.AssignedToAmbulance ? "awaiting stretcher" : casualty.Sim.State.deceased && casualty.Record.HasTag ? "deceased" : "on scene";
                body.Append("<b>").Append(casualty.Id).Append("</b>  ").Append(tag).Append("  <color=#9AA4AD>").Append(state).Append("</color>");
                foreach (KeyValuePair<ExamAction, Finding> finding in casualty.Findings)
                {
                    body.Append("\n    ").Append(finding.Value.text).Append(" <color=#9AA4AD>@").Append(finding.Value.minute.ToString("0.0")).Append("</color>");
                }

                body.Append("\n");
            }

            body.Append("</size>");
            OpenPanel(TabletTitle, body.ToString(), new Vector2(1100f, 820f), 90f, true);
            UiFactory.Button(modal.Panel, "CLOSE (" + GameInput.KeyName(GameInput.Tablet) + ")", new Vector2(0f, modal.RowY(0)), new Vector2(240f, 48f),
                CloseModal, Palette.Slate);
        }

        // ---------------------------------------------------------------- pause

        private void ShowPauseMenu()
        {
            paused = true;
            Time.timeScale = 0f;
            OpenPanel("PAUSED", "The golden-hour clock is stopped.", new Vector2(700f, 700f), 530f, true);
            Transform panel = modal.Panel;
            UiFactory.Text(panel, "Master volume", 19, new Vector2(400f, 32f), new Vector2(0f, 190f), TextAlignmentOptions.Center, Palette.LightGrey);
            UiFactory.VolumeSlider(panel, new Vector2(0f, 156f), new Vector2(420f, 32f));
            UiFactory.Text(panel, "Mouse / stick sensitivity", 19, new Vector2(400f, 32f), new Vector2(0f, 110f), TextAlignmentOptions.Center, Palette.LightGrey);
            UiFactory.SensitivitySlider(panel, new Vector2(0f, 76f), new Vector2(420f, 32f));
            UiFactory.Text(panel, Scenario.title + "  |  " + DifficultyBand, 16, new Vector2(600f, 30f), new Vector2(0f, 26f), TextAlignmentOptions.Center,
                Palette.LightGrey);
            UiFactory.Button(panel, "RESUME", new Vector2(0f, modal.RowY(3)), new Vector2(320f, 54f), ResumeFromPause);
            UiFactory.Button(panel, "CASUALTY TABLET", new Vector2(0f, modal.RowY(2)), new Vector2(320f, 54f), () =>
            {
                ResumeFromPause();
                OpenTablet();
            }, Palette.Slate);
            UiFactory.Button(panel, "RESTART", new Vector2(0f, modal.RowY(1)), new Vector2(320f, 54f), RestartScenario, Palette.Slate);
            UiFactory.Button(panel, "MAIN MENU", new Vector2(0f, modal.RowY(0)), new Vector2(320f, 54f), ReturnToMenu, Palette.Slate);
        }

        public void ResumeFromPause()
        {
            paused = false;
            Time.timeScale = Session.TimeScaleOverride > 0f ? Session.TimeScaleOverride : 1f;
            CloseModal();
        }

        public void PauseForTest(bool pause)
        {
            if (pause) ShowPauseMenu();
            else ResumeFromPause();
        }

        // ---------------------------------------------------------------- ending

        private void OnEndPresentation()
        {
            hud.SetVisible(false);
            hud.SetProgress(string.Empty, 0f, false);
            if (!Cutscene.Play(this, CutsceneKind.Ending, ShowDebrief)) ShowDebrief();
        }

        public void ShowDebrief()
        {
            Phase = IncidentPhase.Debrief;
            hud.ClearToasts();
            if (player != null) player.SetControlsEnabled(false);
            DebriefScreen.Show(this, modal);
        }
    }
}
