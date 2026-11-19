using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace GoldenHour
{
    /// <summary>Plays one v2 scenario to the debrief while keeping an independent score tally.</summary>
    public sealed partial class AutomatedPlaytest
    {
        private int expected;
        private readonly List<string> tally = new List<string>();

        private void Expect(int points, string reason)
        {
            expected += points;
            tally.Add((points >= 0 ? "+" : "") + points + " " + reason);
        }

        private IEnumerator PlayScenario(string id)
        {
            IncidentDirector d = IncidentDirector.Instance;
            Check(id + ": scenario loads", d != null && d.Scenario.scenarioId == id);
            if (d == null) yield break;
            expected = 0;
            tally.Clear();
            Time.timeScale = 1f;

            yield return WaitFor(() => d.Phase == IncidentPhase.Intro || d.Phase == IncidentPhase.Briefing, 8f);
            Check(id + ": opening fly-through cutscene plays", d.Phase == IncidentPhase.Intro && Cutscene.Playing && Cutscene.Current == CutsceneKind.Opening);
            if (d.Phase == IncidentPhase.Intro)
            {
                yield return new WaitForSeconds(2.5f);
                if (id == "tutorial" || id == "bus") yield return Screenshot(id + "_00_intro_cutscene");
                Cutscene.Skip();
            }

            yield return WaitFor(() => d.Phase == IncidentPhase.Briefing, 10f);
            Check(id + ": briefing with the not-medically-reviewed disclaimer", d.Phase == IncidentPhase.Briefing &&
                                                                               ScreenTextContains("NOT been medically reviewed"));
            yield return Screenshot(id + "_01_briefing");
            d.BeginScenario();
            yield return null;
            ScenarioData scenario = d.Scenario;
            Check(id + ": casualties and hazards spawned", d.Casualties.Count == scenario.casualties.Count && d.Hazards.Count == scenario.hazards.Count,
                d.Casualties.Count + " casualties, " + d.Hazards.Count + " hazards");
            Check(id + ": NavMesh baked at runtime", ScenarioEnvironment.NavMeshReady);
            float startMinute = d.Minute;
            yield return new WaitForSeconds(0.6f);
            Check(id + ": golden-hour clock runs in real time", d.Minute > startMinute, d.Minute.ToString("0.000"));
            yield return Screenshot(id + "_02_scene");
            Check(id + ": hazard objective shows marked / total", d.ObjectivesText.Contains("(0/" + d.Hazards.Count + ")"), d.ObjectivesText);
            if (id == "highway")
            {
                d.Hud.ShowRadio("Layout check", 3f);
                yield return null;
                Check("highway: tip panel and radio panel do not overlap", d.Hud.TipVisible && !d.Hud.TipRect.Overlaps(d.Hud.RadioRect),
                    d.Hud.TipRect + " vs " + d.Hud.RadioRect);
            }

            GoldenHourAudio sound = d.GetComponent<GoldenHourAudio>();
            Check(id + ": layered music and weather ambience play", sound != null && sound.MusicPlaying);
            Check(id + ": first-person hands are visible", Viewmodel.Instance != null && Viewmodel.Instance.HandsShown);
            if (id == "bus")
            {
                Check("bus: stress rises with unstable RED casualties and the clock (drives heartbeat and vignette)",
                    StressSystem.Instance != null && StressSystem.Evaluate(d) > 0.1f, StressSystem.Evaluate(d).ToString("0.00"));
            }

            if (id == "highway") yield return PhotoModeTest(d);

            if (id == "tutorial") yield return TutorialRealInput(d);
            else d.CollectKit();
            Check(id + ": first-aid bag collected with limited supplies", d.KitCollected && d.Dressings == scenario.dressings && d.Blankets == scenario.blankets);

            Casualty probe = d.Casualties[d.Casualties.Count - 1];
            d.OpenExamination(probe);
            Check(id + ": examination is blocked before the dispatch report", !(d.Modal.IsOpen && d.Modal.Title.StartsWith(IncidentDirector.ExamTitlePrefix)));

            d.InstantActions = true;
            bool skipGloves = id == "highway";
            if (!skipGloves)
            {
                d.PutOnGloves();
                Check(id + ": gloves on (one pair used)", d.GlovesOn && d.Gloves == scenario.gloves - 1);
            }

            foreach (Hazard hazard in d.Hazards)
            {
                d.MarkHazard(hazard);
                Expect(ScoreRules.HazardMarked, "hazard " + hazard.Type);
            }

            Check(id + ": every hazard marked", d.Hazards.TrueForAll(h => h.Marked));
            if (id == "bus" || id == "building") yield return Screenshot(id + "_03_hazards");

            d.OpenDispatch();
            yield return null;
            Check(id + ": dispatch report minigame opens", d.Modal.Title == IncidentDirector.DispatchTitle);
            DispatchReport draft = d.DispatchDraft;
            draft.emergencyNumber = id == "bus" ? "911" : "112";
            draft.locationIndex = scenario.correctLocation;
            draft.casualtyCount = scenario.casualties.Count;
            draft.hazardMask = scenario.HazardMask;
            if (id == "tutorial") yield return Screenshot(id + "_04_dispatch");
            d.SubmitDispatch();
            int fields = id == "bus" ? 3 : 4;
            Expect(fields * ScoreRules.DispatchField, "dispatch");
            Check(id + ": dispatch scored field by field (wrong number in the bus scenario)", d.DispatchFieldsCorrect == fields && d.Phase == IncidentPhase.Response,
                d.DispatchFieldsCorrect + "/4");
            d.ClosePanel();
            yield return null;
            Check(id + ": patient tasks stay hidden until a finding reveals them (none before any examination)",
                d.PatientTaskList.Count == 0 && !d.ObjectivesText.Contains("PATIENT TASKS"), d.PatientTaskList.Count + " task(s)");

            if (skipGloves)
            {
                Casualty touched = d.Casualties.Find(c => !c.Sim.State.canWalk && !c.Sim.InitiallyExpectant);
                d.OpenExamination(touched);
                d.PerformExam(touched, ExamAction.CheckBreathing);
                Expect(ScoreRules.NoGloves, "no gloves " + touched.Id);
                Check(id + ": touching a casualty without gloves is penalised", d.Ledger.Count("gloves", true) == 1);
                d.ClosePanel();
                d.PutOnGloves();
            }

            // Walking wounded.
            List<Casualty> walkers = d.Casualties.FindAll(c => c.Sim.State.canWalk);
            int moving = d.ShoutWalkingWounded();
            if (walkers.Count > 0) Expect(ScoreRules.WalkingCommand, "walking command");
            Check(id + ": shout sends every walking casualty to the green zone", moving == walkers.Count, moving + "/" + walkers.Count);
            Time.timeScale = 4f;
            yield return WaitFor(() => walkers.TrueForAll(c => c.InGreenZone), 30f);
            Time.timeScale = 1f;
            foreach (Casualty walker in walkers)
            {
                if (walker.AutoTaggedGreen) Expect(ScoreRules.FirstTag(TriageTag.Green, walker.Record.FirstTruth), "walker " + walker.Id);
            }

            Check(id + ": walking wounded reach the green zone on the NavMesh and are tagged GREEN",
                walkers.TrueForAll(c => c.InGreenZone && c.Record.CurrentTag == TriageTag.Green));
            if (id == "building") yield return Screenshot(id + "_05_green_zone");

            // Plan: handle the most time-critical casualties first; some scenarios keep one for a rule test.
            Casualty retriageCase = id == "highway" ? Find(d, "H05") : null;
            Casualty deathCase = id == "bus" ? Find(d, "B07") : null;
            Casualty wrongTagCase = id == "building" ? Find(d, "K12") : null;
            Casualty bleeder = d.Casualties.Find(c => c.Profile.initial.bleeding == BleedingLevel.Severe);
            if (id == "tutorial" && bleeder != null)
            {
                float before = bleeder.Sim.State.capillaryRefill;
                d.AdvanceSimulation(1.5f);
                Check(id + ": untreated heavy bleeding worsens circulation over time", bleeder.Sim.State.capillaryRefill > before,
                    before.ToString("0.00") + " -> " + bleeder.Sim.State.capillaryRefill.ToString("0.00") + " s");
            }

            List<Casualty> queue = d.Casualties.FindAll(c => !c.InGreenZone && c != deathCase);
            queue.Sort((a, b) => Urgency(a).CompareTo(Urgency(b)));
            bool screenshotTaken = false;
            Casualty taskCase = null;
            bool blanketChecked = false;
            foreach (Casualty casualty in queue)
            {
                yield return ExamineAndTag(d, casualty, casualty == wrongTagCase);
                bool needsDressing = (IncidentDirector.CareNeeded(casualty) & Treatment.PressureDressing) != 0 && !casualty.Sim.State.deceased;
                if (taskCase == null && needsDressing && d.Dressings > 0)
                {
                    taskCase = casualty;
                    yield return null;
                    Check(id + ": the bleeding finding adds the live task \"Control bleeding on " + casualty.Id + "\"",
                        d.PatientTaskList.Exists(t => t.casualtyId == casualty.Id && t.kind == PatientTaskKind.ControlBleeding && !t.done) &&
                        d.ObjectivesText.Contains("Control bleeding on " + casualty.Id), Flat(d.ObjectivesText));
                }

                if (!screenshotTaken && !casualty.Sim.InitiallyExpectant)
                {
                    screenshotTaken = true;
                    yield return InWorldExamination(d, casualty);
                }

                yield return GiveAid(d, casualty, casualty == retriageCase);
                if (casualty == taskCase)
                {
                    yield return null;
                    Check(id + ": the task is ticked once the dressing is applied to " + casualty.Id,
                        d.PatientTaskList.Exists(t => t.casualtyId == casualty.Id && t.kind == PatientTaskKind.ControlBleeding && t.done) &&
                        !d.ObjectivesText.Contains("[  ] Control bleeding on " + casualty.Id), Flat(d.ObjectivesText));
                    TreatmentVisuals visuals = casualty.GetComponent<TreatmentVisuals>();
                    Transform joint = visuals != null && casualty.Body != null ? casualty.Body.Joint(visuals.DressingJoint ?? string.Empty) : null;
                    string site = d.DressingPart(casualty);
                    Check(id + ": the dressing is visible on " + casualty.Id + "'s body at the bleeding site (Blender model)",
                        visuals != null && visuals.DressingShown && joint != null &&
                        visuals.DressingJoint.StartsWith(site.TrimEnd('L', 'R', '.')) &&
                        Vector3.Distance(visuals.DressingTransform.position, joint.position) < 0.4f &&
                        visuals.DressingTransform.GetComponentsInChildren<Renderer>().Length >= 2,
                        (visuals != null ? visuals.DressingJoint : "none") + " for site " + site);
                    if (id == "tutorial")
                    {
                        yield return new WaitForSecondsRealtime(0.3f);
                        yield return Screenshot("tutorial_07c_treated");
                    }
                }

                if (!blanketChecked && (casualty.Sim.Applied & Treatment.Blanket) != 0)
                {
                    blanketChecked = true;
                    TreatmentVisuals visuals = casualty.GetComponent<TreatmentVisuals>();
                    Check(id + ": the foil blanket is visible on " + casualty.Id + " (Blender model, lying or shawl to suit the pose)",
                        visuals != null && visuals.BlanketShown, casualty.Body != null ? casualty.Body.State.ToString() : "no body");
                }

                d.ClosePanel();
            }

            if (id == "highway") yield return WrongTreatmentAndRetriage(d, retriageCase);
            if (deathCase != null) yield return DeathTest(d, deathCase);
            if (wrongTagCase != null) yield return UnjustifiedRetagTest(d, wrongTagCase);

            Casualty expectant = d.Casualties.Find(c => c.Sim.InitiallyExpectant);
            if (expectant != null)
            {
                if (id == "building") yield return CprMinigame(d, expectant);
                else d.RequestTreatment(expectant, Treatment.Cpr);
                yield return null;
                Check(id + ": CPR in mass-casualty triage is deprioritised (educational note, no score)", d.Hud.StatusText.Contains("deprioritised"));
            }

            d.InstantActions = false;
            yield return Screenshot(id + "_07_triaged");

            // Ambulances in waves; hand over by priority.
            yield return Handovers(d, id);

            yield return WaitFor(() => d.Finished, 10f);
            Check(id + ": scenario ends when every casualty is resolved", d.Finished, d.EndReason);
            if (d.Run.ledger.Exists(e => e.category == "time"))
            {
                Expect(ScoreRules.TimeBonus(scenario.goldenHourMinutes - d.Run.durationMinutes), "time bonus");
            }

            foreach (Casualty casualty in d.Casualties)
            {
                if (casualty.Sim.PreventableDeath && casualty != deathCase) Expect(ScoreRules.PreventableDeath, "unplanned death " + casualty.Id);
            }

            yield return WaitFor(() => d.Phase == IncidentPhase.Debrief || d.Phase == IncidentPhase.Ending, 5f);
            if (d.Phase == IncidentPhase.Ending)
            {
                yield return new WaitForSeconds(1f);
                if (id == "highway") yield return Screenshot(id + "_08_end_cutscene");
                Cutscene.Skip();
                yield return WaitFor(() => d.Phase == IncidentPhase.Debrief, 8f);
            }

            Check(id + ": exact score - game ledger equals the independent tally", d.Ledger.Total == expected,
                "ledger " + d.Ledger.Total + ", tally " + expected + (d.Ledger.Total != expected ? " | tally: " + string.Join("; ", tally) +
                                                                     " | ledger: " + LedgerSummary(d) : string.Empty));
            RunRecord run = d.Run;
            Check(id + ": run telemetry saved as JSON (decisions, path, outcomes)", Session.LastRunPath != null && File.Exists(Session.LastRunPath) &&
                                                                                  run.casualties.Count == d.Casualties.Count && run.path.Count > 0 &&
                                                                                  run.events.Exists(e => e.type == "tag"));
            Check(id + ": 1-3 stars awarded", run.stars >= 1 && run.stars <= 3, run.score + "/" + run.maximumScore + " = " + run.stars + " stars");
            Check(id + ": treatments use readable names in the debrief data (no enum identifiers)",
                run.casualties.TrueForAll(c => !System.Text.RegularExpressions.Regex.IsMatch(c.treatments ?? string.Empty, "[a-z][A-Z]")) &&
                run.casualties.Exists(c => (c.treatments ?? string.Empty).Contains(" ") || c.treatments == "Blanket" || c.treatments == "Reassurance"));
            Check(id + ": debrief shown with feedback and disclaimer", d.Phase == IncidentPhase.Debrief && run.feedback.Count > 0 &&
                                                                     ScreenTextContains("NOT been medically reviewed"));
            yield return DebriefChecks(d, id);
            ScenarioProgress progress = ProfileStore.Load().Get(id);
            Check(id + ": profile records the run and unlocks the next scenario",
                progress.plays >= 1 && progress.bestScore == Mathf.Max(progress.bestScore, run.score) &&
                (string.IsNullOrEmpty(scenario.nextScenarioId) || ProfileStore.Load().Get(scenario.nextScenarioId).unlocked));
        }

        private static string LedgerSummary(IncidentDirector d)
        {
            List<string> items = new List<string>();
            foreach (ScoreEntry entry in d.Ledger.Entries) items.Add(entry.points + " " + entry.category + ":" + entry.casualtyId);
            return string.Join(", ", items);
        }

        private static Casualty Find(IncidentDirector d, string id) => d.Casualties.Find(c => c.Id == id);

        private static float Urgency(Casualty casualty)
        {
            float death = casualty.Sim.EffectiveDeathMinute;
            if (death > 0f) return death;
            float worsen = casualty.Sim.EffectiveWorsenMinute;
            return worsen > 0f ? worsen : 999f;
        }

        // --------------------------------------------------------- examination

        private IEnumerator ExamineAndTag(IncidentDirector d, Casualty casualty, bool deliberateMistake)
        {
            d.OpenExamination(casualty);
            yield return null;
            string id = d.Scenario.scenarioId;
            bool opened = d.Modal.IsOpen && d.Modal.Title == IncidentDirector.ExamTitlePrefix + casualty.Id;
            if (!opened) Check(id + ": examination opens for " + casualty.Id, false, d.Modal.Title);
            bool airwayWasNeeded = casualty.Sim.State.airwayObstructed && !casualty.Sim.State.airwayOpened;
            d.PerformExam(casualty, ExamAction.AskToWalk);
            d.PerformExam(casualty, ExamAction.CheckBreathing);
            if (casualty.Findings[ExamAction.CheckBreathing].text.Contains("NOT breathing"))
            {
                d.PerformExam(casualty, ExamAction.OpenAirway);
                if (airwayWasNeeded && casualty.Sim.State.airwayOpened && (casualty.Profile.indicatedTreatments & Treatment.AirwayOpening) != 0)
                {
                    Expect(ScoreRules.TreatmentEffective, "airway " + casualty.Id);
                }
            }

            d.PerformExam(casualty, ExamAction.CheckCirculation);
            d.PerformExam(casualty, ExamAction.GiveCommand);
            d.PerformExam(casualty, ExamAction.LookForBleeding);
            TriageTag fromFindings = StartFromFindings(casualty);
            TriageTag truth = casualty.Sim.TrueTag;
            Check(id + ": findings alone give the correct START category for " + casualty.Id, fromFindings == truth,
                "findings -> " + fromFindings + ", START " + truth);
            TriageTag chosen = deliberateMistake ? TriageTag.Green : fromFindings;
            float before = d.Minute;
            d.RequestTag(casualty, chosen);
            Expect(ScoreRules.FirstTag(chosen, truth), "tag " + casualty.Id + " " + chosen + " vs " + truth);
            Check(id + ": physical tag placed on " + casualty.Id + (deliberateMistake ? " (deliberately wrong)" : string.Empty),
                casualty.Record.CurrentTag == chosen && casualty.GetComponentInChildren<Renderer>() != null &&
                casualty.transform.Find("Triage Tag") != null);
            Check(id + ": examination and tagging cost simulated time", d.Minute > before - 0.0001f && casualty.Record.FirstExamMinute >= 0f);
        }

        /// <summary>Rebuilds a physiology state purely from the on-screen findings and runs START on it.</summary>
        private static TriageTag StartFromFindings(Casualty casualty)
        {
            PhysiologyState state = new PhysiologyState { capillaryRefill = 1f, radialPulse = true, followsCommands = true };
            IReadOnlyDictionary<ExamAction, Finding> findings = casualty.Findings;
            if (findings.TryGetValue(ExamAction.AskToWalk, out Finding walk)) state.canWalk = walk.text.Contains("walks a few steps");
            if (findings.TryGetValue(ExamAction.CheckBreathing, out Finding breathing))
            {
                Match rate = Regex.Match(breathing.text, @"(\d+) breaths per minute");
                state.respiratoryRate = rate.Success ? int.Parse(rate.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
            }

            if (findings.TryGetValue(ExamAction.OpenAirway, out Finding airway))
            {
                Match started = Regex.Match(airway.text, @"STARTED, (\d+) per minute");
                if (started.Success)
                {
                    state.airwayObstructed = true;
                    state.respiratoryRate = int.Parse(started.Groups[1].Value, CultureInfo.InvariantCulture);
                }
                else if (airway.text.Contains("still NOT breathing"))
                {
                    state.respiratoryRate = 0;
                }
            }

            if (findings.TryGetValue(ExamAction.CheckCirculation, out Finding circulation))
            {
                Match refill = Regex.Match(circulation.text, @"refill ([0-9.]+) s");
                if (refill.Success) state.capillaryRefill = float.Parse(refill.Groups[1].Value, CultureInfo.InvariantCulture);
                state.radialPulse = !circulation.text.Contains("ABSENT") && !circulation.text.Contains("no pulse");
            }

            if (findings.TryGetValue(ExamAction.GiveCommand, out Finding command)) state.followsCommands = command.text.Contains("squeezes");
            return StartTriage.Evaluate(state);
        }

        // ---------------------------------------------------------- supply bag

        /// <summary>Tutorial: open the bag, pick a not-for-triage item (feedback only), then take the dressing from the bag.</summary>
        private IEnumerator SupplyBag(IncidentDirector d, Casualty casualty)
        {
            d.OpenBag(casualty);
            yield return null;
            Check("tutorial: the supply bag opens in the docked panel with item counts",
                d.Modal.Docked && d.Modal.Title == IncidentDirector.BagTitlePrefix + casualty.Id &&
                ScreenTextContains("Pressure dressing") && ScreenTextContains("x" + d.Dressings) && ScreenTextContains("Splint"));
            int score = d.Ledger.Total;
            int dressings = d.Dressings;
            int blankets = d.Blankets;
            d.PickBagItem(BagItem.Splint);
            yield return null;
            Check("tutorial: a wrong item (splint) gives START feedback, uses nothing and does not change the score",
                d.WrongItemPicks == 1 && d.Hud.StatusText.Contains("START") && d.Ledger.Total == score && d.Dressings == dressings &&
                d.Blankets == blankets && d.Modal.Title.StartsWith(IncidentDirector.BagTitlePrefix), d.Hud.StatusText);
            yield return Screenshot("tutorial_07a_supply_bag");
            d.PickBagItem(BagItem.PressureDressing);
            yield return WaitFor(() => !d.ActionRunning, 5f);
            Check("tutorial: the pressure dressing taken from the bag is applied and one is used up",
                (casualty.Sim.Applied & Treatment.PressureDressing) != 0 && d.Dressings == dressings - 1 &&
                d.Modal.Title == IncidentDirector.ExamTitlePrefix + casualty.Id);
        }

        private static string Flat(string text) => Regex.Replace(text ?? string.Empty, "<[^>]+>", string.Empty).Replace("\n", " | ");

        // ------------------------------------------------------ in-world exam

        /// <summary>Docked panel, kneeling framing, toast layout and (tutorial) the body-part marker of a timed check.</summary>
        private IEnumerator InWorldExamination(IncidentDirector d, Casualty casualty)
        {
            string id = d.Scenario.scenarioId;
            d.OpenExamination(casualty);
            yield return null;
            yield return WaitFor(() => !d.player.Gliding, 2f);
            yield return null;
            Rect panel = d.Modal.PanelCanvasRect();
            Check(id + ": examination opens as a compact panel docked on the right, without a full-screen backdrop",
                d.Modal.IsOpen && d.Modal.Docked && panel.xMin > 0f && panel.width <= ModalWindow.DockWidth + 1f &&
                GameObject.Find("Modal Backdrop") == null, panel.ToString("0"));

            Camera view = d.player.PlayerCamera;
            Vector3 viewport = view != null ? view.WorldToViewportPoint(casualty.FocusPoint) : Vector3.zero;
            RectTransform canvas = (RectTransform)d.Modal.Panel.parent;
            float panelLeft = (panel.xMin + canvas.rect.width * 0.5f) / canvas.rect.width;
            Check(id + ": the casualty stays in view, left of the docked panel",
                viewport.z > 0.2f && viewport.x > 0.05f && viewport.x < Mathf.Min(0.62f, panelLeft - 0.03f) && viewport.y > 0.05f && viewport.y < 0.95f,
                "viewport " + viewport.ToString("0.00") + ", panel starts at " + panelLeft.ToString("0.00"));
            Vector3 offset = d.player.transform.position - casualty.FocusPoint;
            offset.y = 0f;
            Check(id + ": the player kneels within arm's reach of " + casualty.Id, d.player.IsKneeling && offset.magnitude < 1.6f,
                offset.magnitude.ToString("0.00") + " m");

            d.Hud.Toast(0, "Layout check A");
            d.Hud.Toast(0, "Layout check B");
            yield return new WaitForSecondsRealtime(1.2f);
            List<Rect> toastRects = d.Hud.ToastRects();
            bool apart = toastRects.Count >= 2;
            for (int a = 0; a < toastRects.Count; a++)
            {
                if (toastRects[a].Overlaps(panel) || toastRects[a].xMin < -canvas.rect.width * 0.5f) apart = false;
                for (int b = a + 1; b < toastRects.Count; b++) apart &= !toastRects[a].Overlaps(toastRects[b]);
            }

            Check(id + ": score messages are queued, never overlap and stay off the examination panel", apart,
                toastRects.Count + " visible: " + string.Join(" ", toastRects.ConvertAll(r => r.ToString("0"))) + " | panel " + panel.ToString("0"));
            yield return Screenshot(id + "_06_examination");
            if (id != "tutorial") yield break;

            Viewmodel hands = Viewmodel.Instance;
            Check("tutorial: first-person hands use the Blender glove model (4 fingers and a thumb per hand, two segments each) in blue nitrile",
                hands != null && hands.UsesModel && hands.FingerSegments == 20 && hands.GlovesVisible,
                hands != null ? hands.FingerSegments + " segments" : "no viewmodel");

            // A timed check shows a world-space marker on the body part involved (here: the forearm for circulation).
            d.InstantActions = false;
            d.PerformExam(casualty, ExamAction.CheckCirculation);
            yield return null;
            yield return null;
            Transform forearm = null;
            foreach (string arm in new[] { "LowerArm.L", "LowerArm.R" })
            {
                Transform joint = casualty.Body != null ? casualty.Body.Joint(arm) : null;
                if (joint != null && (forearm == null || Vector3.Distance(joint.position, d.Marker.Target) < Vector3.Distance(forearm.position, d.Marker.Target))) forearm = joint;
            }

            Vector3 markerView = view != null ? view.WorldToViewportPoint(d.Marker.Position) : Vector3.zero;
            Check("tutorial: a timed check shows a world-space marker on the body part (forearm for circulation)",
                d.ActionRunning && d.Marker.Visible && d.Marker.Label == "Check circulation" && forearm != null &&
                Vector3.Distance(d.Marker.Target, forearm.position) < 0.35f && Vector3.Distance(d.Marker.Position, d.Marker.Target) < 0.1f &&
                markerView.z > 0f && markerView.x > 0f && markerView.x < 1f && markerView.y > 0f && markerView.y < 1f,
                "marker " + d.Marker.Position.ToString("0.00") + " view " + markerView.ToString("0.00"));
            yield return new WaitForSecondsRealtime(0.4f);
            Check("tutorial: the action progress bar stays over the scene, clear of the tutorial tip and the docked panel",
                !d.Hud.ProgressRect.Overlaps(panel) && (!d.Hud.TipVisible || !d.Hud.ProgressRect.Overlaps(d.Hud.TipRect)),
                "progress " + d.Hud.ProgressRect.ToString("0") + " tip " + d.Hud.TipRect.ToString("0"));
            yield return Screenshot("tutorial_06c_body_marker");
            yield return WaitFor(() => !d.ActionRunning, 8f);
            yield return null;
            Check("tutorial: the marker disappears when the check completes and the finding is recorded",
                !d.Marker.Visible && casualty.Findings.ContainsKey(ExamAction.CheckCirculation));
            d.InstantActions = true;
        }

        // ----------------------------------------------------------------- aid

        private IEnumerator GiveAid(IncidentDirector d, Casualty casualty, bool holdBackBlanket)
        {
            if (casualty.Sim.State.deceased) yield break;
            Treatment indicated = casualty.Profile.indicatedTreatments;
            Treatment[] order = { Treatment.PressureDressing, Treatment.RecoveryPosition, Treatment.Blanket, Treatment.Reassure };
            foreach (Treatment treatment in order)
            {
                if ((indicated & treatment) == 0 || (casualty.Sim.Applied & treatment) != 0) continue;
                if (treatment == Treatment.Blanket && (holdBackBlanket || d.Blankets <= 0)) continue;
                if (treatment == Treatment.PressureDressing && d.Dressings <= 0) continue;
                if (treatment == Treatment.RecoveryPosition && d.Scenario.scenarioId == "tutorial")
                {
                    yield return RecoveryMinigame(d, casualty);
                }
                else if (treatment == Treatment.PressureDressing && d.Scenario.scenarioId == "tutorial")
                {
                    yield return SupplyBag(d, casualty);
                }
                else
                {
                    d.RequestTreatment(casualty, treatment);
                }

                yield return WaitFor(() => !d.ActionRunning && !FirstAidMinigame.Active, 5f);
                bool applied = (casualty.Sim.Applied & treatment) != 0;
                if (applied) Expect(ScoreRules.TreatmentEffective, treatment + " " + casualty.Id);
                Check(d.Scenario.scenarioId + ": " + treatment + " applied to " + casualty.Id, applied);
                if (treatment == Treatment.PressureDressing)
                {
                    Check(d.Scenario.scenarioId + ": pressure dressing stops the deterioration for " + casualty.Id, casualty.Sim.Stabilised);
                }
            }
        }

        private IEnumerator PhotoModeTest(IncidentDirector d)
        {
            PhotoMode photo = PhotoMode.Instance;
            Check("highway: photo mode component present", photo != null);
            if (photo == null) yield break;
            photo.Enter();
            yield return null;
            float minute = d.Minute;
            Check("highway: F10 photo mode freezes time and hides the HUD", photo.Active && Time.timeScale == 0f && !Viewmodel.Instance.HandsShown);
            yield return new WaitForSecondsRealtime(0.4f);
            Check("highway: golden-hour clock is paused in photo mode", Mathf.Approximately(minute, d.Minute));
            photo.Capture();
            yield return new WaitForSecondsRealtime(0.5f);
            Check("highway: photo mode saves a PNG", photo.LastPhotoPath != null && File.Exists(photo.LastPhotoPath), photo.LastPhotoPath);
            photo.Exit();
            yield return null;
            Check("highway: leaving photo mode restores play", !photo.Active && Time.timeScale > 0f && Viewmodel.Instance.HandsShown);
        }

        private IEnumerator RecoveryMinigame(IncidentDirector d, Casualty casualty)
        {
            d.InstantActions = false;
            Vector3 focus = casualty.FocusPoint;
            Vector3 stand = focus + (casualty.transform.right * 1.3f);
            stand.y = 0f;
            d.player.Teleport(stand, Quaternion.LookRotation(focus - stand).eulerAngles.y, 30f);
            d.player.LookAt(focus);
            float minuteBefore = d.Minute;
            d.RequestTreatment(casualty, Treatment.RecoveryPosition);
            yield return null;
            Check("tutorial: recovery position opens the roll minigame", FirstAidMinigame.Kind == MinigameKind.RecoveryPosition && d.ActionRunning);
            FirstAidMinigame.TestPress = true;
            yield return null;
            yield return null;
            Check("tutorial: confirming before the casualty is on their side is refused", FirstAidMinigame.Active &&
                                                                                          (casualty.Sim.Applied & Treatment.RecoveryPosition) == 0);
            FirstAidMinigame.TestRotate = 1f;
            yield return WaitFor(() => FirstAidMinigame.RolledTooFar > 0, 6f);
            Check("tutorial: rolling too far (onto the front) is caught and eased back", FirstAidMinigame.RolledTooFar == 1 && casualty.RecoveryRoll < 0.8f);
            FirstAidMinigame.TestRotate = 1f;
            yield return WaitFor(() => casualty.RecoveryRoll >= 0.95f, 4f);
            FirstAidMinigame.TestRotate = 0f;
            yield return Screenshot("tutorial_07b_recovery_minigame");
            FirstAidMinigame.TestPress = true;
            yield return WaitFor(() => !FirstAidMinigame.Active, 2f);
            Check("tutorial: stopping in the on-side band and tilting the head completes the recovery position",
                (casualty.Sim.Applied & Treatment.RecoveryPosition) != 0 && casualty.Body.State == BodyState.Recovery);
            Check("tutorial: the clock kept running during the minigame", d.Minute > minuteBefore);
            d.InstantActions = true;
        }

        private IEnumerator CprMinigame(IncidentDirector d, Casualty casualty)
        {
            d.InstantActions = false;
            Time.timeScale = 1f;
            Vector3 focus = casualty.FocusPoint;
            Vector3 stand = focus + (casualty.transform.right * 1.3f);
            stand.y = 0f;
            d.player.Teleport(stand, Quaternion.LookRotation(focus - stand).eulerAngles.y, 30f);
            d.player.LookAt(focus);
            d.RequestTreatment(casualty, Treatment.Cpr);
            yield return null;
            Check("building: CPR opens the rhythm minigame with a metronome", FirstAidMinigame.Kind == MinigameKind.Cpr);
            float interval = 60f / 110f;
            for (int i = 0; i < FirstAidMinigame.CprCompressions && FirstAidMinigame.Active; i++)
            {
                FirstAidMinigame.TestPress = true;
                if (i == 12) yield return Screenshot("building_06b_cpr_minigame");
                yield return new WaitForSeconds(interval);
            }

            yield return WaitFor(() => !FirstAidMinigame.Active, 2f);
            Check("building: 30 compressions in rhythm measure a rate of 100-120 per minute", FirstAidMinigame.LastAverageRate >= 100f &&
                                                                                                FirstAidMinigame.LastAverageRate <= 120f,
                FirstAidMinigame.LastAverageRate.ToString("0.0") + "/min, " + (FirstAidMinigame.LastInRangeFraction * 100f).ToString("0") + "% in range");
            d.InstantActions = true;
        }

        private IEnumerator WrongTreatmentAndRetriage(IncidentDirector d, Casualty worsening)
        {
            Casualty chest = Find(d, "H04");
            if (chest != null && d.Dressings > 0)
            {
                int dressings = d.Dressings;
                d.RequestTreatment(chest, Treatment.PressureDressing);
                yield return WaitFor(() => !d.ActionRunning, 5f);
                Expect(ScoreRules.TreatmentNotIndicated, "wrong dressing H04");
                Check("highway: a treatment that is not needed costs points and uses up the supply", d.Dressings == dressings - 1 &&
                                                                                                    d.Ledger.Entries[d.Ledger.Entries.Count - 1].points == ScoreRules.TreatmentNotIndicated);
            }

            if (worsening == null) yield break;
            Check("highway: H05 starts YELLOW and was tagged YELLOW", worsening.Record.FirstTag == TriageTag.Yellow);
            float wait = worsening.Sim.EffectiveWorsenMinute - d.Minute + 0.3f;
            if (wait > 0f) d.AdvanceSimulation(wait);
            Check("highway: an untreated YELLOW casualty worsens to RED over time", worsening.Sim.Worsened && worsening.Sim.TrueTag == TriageTag.Red,
                "minute " + d.Minute.ToString("0.0"));
            int before = d.Ledger.Total;
            d.RequestTag(worsening, TriageTag.Red);
            Check("highway: retriage without re-examining is refused", worsening.Record.CurrentTag == TriageTag.Yellow && d.Ledger.Total == before &&
                                                                       d.Run.events.Exists(e => e.type == "retag-blocked"));
            float minute = d.Minute;
            d.PerformExam(worsening, ExamAction.CheckCirculation);
            d.RequestTag(worsening, TriageTag.Red);
            Expect(ScoreRules.JustifiedRetriage, "justified retriage H05");
            Check("highway: re-examining then re-tagging after a real change earns the smaller retriage credit, costs time and is logged",
                worsening.Record.CurrentTag == TriageTag.Red && worsening.Record.Retags == 1 && worsening.Record.FirstTag == TriageTag.Yellow &&
                d.Minute > minute && d.Ledger.Entries[d.Ledger.Entries.Count - 1].points == ScoreRules.JustifiedRetriage);
            if (d.Blankets > 0)
            {
                d.RequestTreatment(worsening, Treatment.Blanket);
                if ((worsening.Sim.Applied & Treatment.Blanket) != 0) Expect(ScoreRules.TreatmentEffective, "blanket H05 late");
            }
        }

        private IEnumerator DeathTest(IncidentDirector d, Casualty casualty)
        {
            float death = casualty.Sim.EffectiveDeathMinute;
            Check("bus: B07 has a defined time of death if the airway is not managed", death > 0f, death.ToString("0.0") + " min");
            if (d.Minute < death) d.AdvanceSimulation(death - d.Minute + 0.1f);
            Check("bus: untreated critical casualty dies at the defined time", casualty.Sim.State.deceased && casualty.Sim.PreventableDeath,
                "minute " + d.Minute.ToString("0.0"));
            Expect(ScoreRules.PreventableDeath, "death B07");
            Check("bus: preventable death is penalised", d.Ledger.Count("death", true) == 1);
            yield return ExamineAndTag(d, casualty, false);
            d.ClosePanel();
            Check("bus: a dead casualty's START category is BLACK", casualty.Record.CurrentTag == TriageTag.Black);
        }

        private IEnumerator UnjustifiedRetagTest(IncidentDirector d, Casualty casualty)
        {
            int before = d.Ledger.Total;
            d.RequestTag(casualty, TriageTag.Yellow);
            Check("building: retriage without re-examining is refused", casualty.Record.CurrentTag == TriageTag.Green && d.Ledger.Total == before);
            d.PerformExam(casualty, ExamAction.AskToWalk);
            d.RequestTag(casualty, TriageTag.Yellow);
            Expect(0, "unjustified retag K12");
            Check("building: correcting your own mistake without a change in condition earns no credit (first tag stays scored)",
                casualty.Record.CurrentTag == TriageTag.Yellow && casualty.Record.Retags == 1 && d.Ledger.Total == before &&
                casualty.Record.FirstTag == TriageTag.Green);
            yield break;
        }

        // ------------------------------------------------------------ handover

        private IEnumerator Handovers(IncidentDirector d, string id)
        {
            ScenarioData scenario = d.Scenario;
            bool carriedReal = false;
            bool wrongDone = false;
            for (int wave = 0; wave < scenario.waves.Count; wave++)
            {
                if (d.Finished) break;
                List<Casualty> waiting = d.HandoverCandidates();
                if (waiting.Count == 0) break;
                Ambulance ambulance = d.SpawnAmbulance(wave);
                Time.timeScale = 4f;
                yield return WaitFor(() => ambulance.State == AmbulanceState.Waiting, 20f);
                Time.timeScale = 1f;
                if (wave == 0)
                {
                    Check(id + ": ambulance drives in with sirens and paramedics", ambulance.State == AmbulanceState.Waiting &&
                                                                                    ambulance.Lead != null && ambulance.Lead.gameObject.activeSelf &&
                                                                                    ambulance.Sirens.Length > 0);
                    yield return new WaitForSeconds(0.6f);
                    d.OpenHandover(ambulance);
                    yield return null;
                    Check(id + ": handover panel lists tagged casualties", d.Modal.Title.StartsWith(IncidentDirector.HandoverTitlePrefix));
                    if (id == "tutorial" || id == "highway") yield return Screenshot(id + "_09_handover");
                }

                while (ambulance.FreeSpaces > 0)
                {
                    waiting = d.HandoverCandidates();
                    if (waiting.Count == 0) break;
                    waiting.Sort((a, b) => StartTriage.Priority(a.Sim.TrueTag).CompareTo(StartTriage.Priority(b.Sim.TrueTag)));
                    Casualty choice = waiting[0];
                    bool expectCorrect = true;
                    if (id == "building" && !wrongDone)
                    {
                        Casualty lower = waiting.Find(c => StartTriage.Priority(c.Sim.TrueTag) > StartTriage.Priority(waiting[0].Sim.TrueTag));
                        if (lower != null)
                        {
                            choice = lower;
                            expectCorrect = false;
                            wrongDone = true;
                        }
                    }

                    bool correct = d.HandOver(ambulance, choice);
                    Expect(correct ? ScoreRules.HandoverCorrect : ScoreRules.HandoverWrong, "handover " + choice.Id);
                    if (!expectCorrect) Check("building: handing over a lower priority while someone more urgent waits is penalised", !correct);
                }

                d.ClosePanel();
                if (!carriedReal && id == "tutorial")
                {
                    carriedReal = true;
                    Casualty first = null;
                    foreach (Casualty casualty in d.Casualties)
                    {
                        if (casualty.AssignedToAmbulance && !casualty.Sim.Transported) first = casualty;
                    }

                    bool usedNavMesh = false;
                    bool carrySampled = false;
                    bool carryPoses = true;
                    string carryDetail = "never sampled";
                    Time.timeScale = 4f;
                    float end = Time.realtimeSinceStartup + 45f;
                    while (first != null && !first.Sim.Transported && Time.realtimeSinceStartup < end)
                    {
                        usedNavMesh |= ambulance.Lead.Walker.UsingNavMesh;
                        if (first.OnStretcher && ambulance.Lead.Walker.Moving && ambulance.Partner != null)
                        {
                            Vector3 gap = ambulance.Lead.transform.position - ambulance.Partner.transform.position;
                            gap.y = 0f;
                            bool poses = ambulance.Lead.Body.Grip == CarryGrip.Behind && ambulance.Partner.Body.Grip == CarryGrip.Front &&
                                         first.transform.IsChildOf(ambulance.StretcherFrame);
                            if (Time.frameCount % 5 == 0) carryPoses &= poses;
                            carryDetail = "grips " + ambulance.Lead.Body.Grip + "/" + ambulance.Partner.Body.Grip + ", gap " + gap.magnitude.ToString("0.00") + " m";
                            if (!carrySampled && gap.magnitude > Ambulance.PartnerDistance - 0.6f && gap.magnitude < Ambulance.PartnerDistance + 0.6f)
                            {
                                carrySampled = true;
                                Time.timeScale = 1f;
                                Vector3 side = ambulance.Lead.transform.right * 4.2f - ambulance.Lead.transform.forward * 1.2f;
                                Vector3 stand = ambulance.Lead.transform.position + side;
                                d.player.ForcedKneel = false;
                                d.player.Teleport(new Vector3(stand.x, d.player.transform.position.y, stand.z), 0f, 0f);
                                d.player.LookAt(ambulance.Lead.transform.position - ambulance.Lead.transform.forward * 1.2f + Vector3.up * 0.8f);
                                yield return null;
                                yield return Screenshot("tutorial_09b_stretcher_carry");
                                Time.timeScale = 4f;
                            }
                        }

                        if (first.OnStretcher && !usedNavMesh) usedNavMesh = ambulance.Lead.Walker.UsingNavMesh;
                        yield return null;
                    }

                    Time.timeScale = 1f;
                    Check("tutorial: paramedics walk on the NavMesh and carry the casualty on a stretcher to the ambulance",
                        first != null && first.Sim.Transported && usedNavMesh);
                    Check("tutorial: two paramedics carry the stretcher in carry poses (head end hands behind, foot end hands in front)",
                        carrySampled && carryPoses, carryDetail);
                }

                ambulance.CompleteLoadingNow();
                if (ambulance.FreeSpaces > 0) d.ReleaseAmbulance(ambulance);
                yield return null;
            }

            string unresolved = string.Empty;
            foreach (Casualty c in d.Casualties)
            {
                if (c.Sim.State.deceased || c.Sim.Transported || c.InGreenZone || c.Record.CurrentTag == TriageTag.Black) continue;
                unresolved += c.Id + "(tag " + c.Record.CurrentTag + ", true " + c.Sim.TrueTag + ", assigned " + c.AssignedToAmbulance + ", heading " +
                              c.HeadingToGreenZone + ", walk " + c.Sim.State.canWalk + ") ";
            }

            Check(id + ": every surviving stretcher case was handed over", unresolved.Length == 0, unresolved + " waves " + scenario.waves.Count);
            d.HandoverProgress(out int handed, out int toHand);
            d.AidProgress(out int aidGiven, out int aidNeeded);
            string objectivesText = d.ObjectivesText;
            Check(id + ": handover objective ticks when every stretcher case is sent", handed == toHand && toHand > 0 &&
                                                                                        objectivesText.Contains("[X]</color> Hand over by priority (" + handed + "/" + toHand),
                handed + "/" + toHand);
            bool aidTicked = objectivesText.Contains("[X]</color> Give indicated aid (" + aidGiven + "/" + aidNeeded + ")");
            bool aidListed = objectivesText.Contains("Give indicated aid (" + aidGiven + "/" + aidNeeded + ")");
            Check(id + ": aid objective counts casualties whose indicated care is complete and ticks only when all are done",
                aidListed && aidTicked == (aidGiven == aidNeeded && aidNeeded > 0), aidGiven + "/" + aidNeeded);
        }

        // ------------------------------------------------------------- debrief

        private IEnumerator DebriefChecks(IncidentDirector d, string id)
        {
            yield return Screenshot(id + "_10_debrief");
            if (!DebriefScreen.HasTabs) yield break;
            foreach (string tab in DebriefScreen.Tabs)
            {
                DebriefScreen.ShowTab(d, tab);
                yield return null;
                if (id == "highway") yield return Screenshot(id + "_11_debrief_" + tab.ToLowerInvariant());
            }

            DebriefScreen.ShowTab(d, "Casualties");
            yield return null;
            Check(id + ": debrief explains each casualty with the START step that applied", ScreenTextContains("START step"));
            DebriefScreen.ShowTab(d, "Map");
            yield return null;
            Check(id + ": debrief shows a top-down movement heatmap", DebriefScreen.HeatmapTexture != null && DebriefScreen.HeatmapCells > 0);
            DebriefScreen.ShowTab(d, "Timeline");
            yield return null;
            Check(id + ": debrief shows the event timeline and decision times", ScreenTextContains("min") && ScreenTextContains("Decision time"));
        }

        // ------------------------------------------------- third-person character

        /// <summary>
        /// The full skinned main character in third person: visible from behind, runs with W + Shift, walks with W, idles,
        /// and V switches back to first person (body then only casts a shadow; gloved hands return).
        /// </summary>
        private IEnumerator ThirdPersonCharacter(FirstPersonPlayer player)
        {
            PlayerAvatar avatar = player.GetComponent<PlayerAvatar>();
            Vector3 start = player.transform.position;
            float yaw = player.transform.eulerAngles.y;
            FirstPersonPlayer.ViewOverride = true;
            yield return new WaitForSeconds(0.5f);
            Camera view = player.PlayerCamera;
            Vector3 chest = player.transform.position + Vector3.up * 1.2f;
            Vector3 onScreen = view.WorldToViewportPoint(chest);
            float cameraBack = Vector3.Distance(view.transform.position, player.EyePosition);
            Check("tutorial: third person shows the full 3D main character (skinned Blender model with Idle/Walk/Run/Kneel)",
                avatar != null && avatar.Built && avatar.BodyVisible && avatar.ClipCount >= 4 && avatar.Skin != null &&
                avatar.Skin.sharedMesh.vertexCount > 2000 && cameraBack > 1.5f &&
                onScreen.z > 0f && onScreen.x > 0.25f && onScreen.x < 0.75f && onScreen.y > 0.1f && onScreen.y < 0.9f,
                "camera " + cameraBack.ToString("0.00") + " m behind the eyes, chest at viewport " + onScreen.ToString("0.00"));
            yield return Screenshot("tutorial_01a_third_person_idle");

            Keyboard keyboard = testKeyboard ?? Keyboard.current;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftShift));
            yield return new WaitForSeconds(0.9f);
            bool running = avatar != null && avatar.Playing("Run") && player.Speed > PlayerAvatar.RunThreshold;
            string runDetail = (avatar != null ? avatar.CurrentClip : "none") + " at " + player.Speed.ToString("0.0") + " m/s";
            yield return Screenshot("tutorial_01b_third_person_run");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return new WaitForSeconds(0.7f);
            bool walking = avatar != null && avatar.Playing("Walk") && player.Speed > PlayerAvatar.WalkThreshold && player.Speed <= PlayerAvatar.RunThreshold;
            string walkDetail = (avatar != null ? avatar.CurrentClip : "none") + " at " + player.Speed.ToString("0.0") + " m/s";
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSeconds(0.6f);
            bool idle = avatar != null && avatar.Playing("Idle");
            Check("tutorial: the character runs (W + Shift), walks (W) and idles with matching animations",
                running && walking && idle, "run: " + runDetail + "; walk: " + walkDetail + "; idle: " + (avatar != null ? avatar.CurrentClip : "none"));

            yield return TapKey(Key.V);
            yield return new WaitForSeconds(0.5f);
            Check("tutorial: V switches to first person (body casts a shadow only, gloved-hand viewmodel shown)",
                !FirstPersonPlayer.ThirdPersonPreferred && avatar != null && !avatar.BodyVisible && Viewmodel.Instance != null &&
                Viewmodel.Instance.HandsShown && Vector3.Distance(view.transform.position, player.EyePosition) < 0.2f);
            player.Teleport(start, yaw, 0f);
            yield return null;
        }

        // ------------------------------------------------------- tutorial input

        private IEnumerator TutorialRealInput(IncidentDirector d)
        {
            FirstPersonPlayer player = FindAnyObjectByType<FirstPersonPlayer>();
            yield return ThirdPersonCharacter(player);
            GoldenHourAudio audio = FindAnyObjectByType<GoldenHourAudio>();
            int steps = audio != null ? audio.FootstepsPlayed : 0;
            Vector3 before = player.transform.position;
            yield return HoldKey(Key.W, 1f);
            Check("tutorial: real W key moves the player with footsteps", Vector3.Distance(Flat(before), Flat(player.transform.position)) > 2f &&
                                                                          audio != null && audio.FootstepsPlayed > steps);
            Check("tutorial: guided tutorial tip is shown", d.Hud.TipVisible && d.Hud.TipText.Contains("TUTORIAL 1/"));
            yield return TapKey(Key.C);
            yield return new WaitForSeconds(0.5f);
            Check("tutorial: kneel key lowers the camera", player.IsKneeling && player.EyeHeight < FirstPersonPlayer.StandingEyeHeight - 0.2f,
                player.EyeHeight.ToString("0.00"));
            yield return TapKey(Key.C);

            d.PauseForTest(true);
            float paused = d.Minute;
            yield return new WaitForSecondsRealtime(0.5f);
            Check("tutorial: Esc pause stops the golden-hour clock", Mathf.Approximately(d.Minute, paused) && Time.timeScale == 0f);
            yield return Screenshot("tutorial_02b_pause");
            d.PauseForTest(false);

            Vector3 focus = d.Kit.transform.position + Vector3.up * 0.35f;
            player.Teleport(Flat(focus) + new Vector3(0f, 0.1f, -1.6f), 0f, 0f);
            player.LookAt(focus);
            yield return null;
            yield return null;
            Check("tutorial: interaction prompt and outline on the bag", d.Hud.Root.activeSelf && InteractionOutline.Current != null);
            yield return Screenshot("tutorial_03_outline");
            yield return TapKey(Key.E);
            Check("tutorial: real E key collects the first-aid bag", d.KitCollected);
            yield return TapKey(Key.G);
            yield return WaitFor(() => d.GlovesOn, 4f);
            Check("tutorial: real G key puts gloves on (viewmodel hands gloved)", d.GlovesOn && Viewmodel.Instance != null && Viewmodel.Instance.GlovesVisible);
            yield return Screenshot("tutorial_03b_gloves");
        }
    }
}
