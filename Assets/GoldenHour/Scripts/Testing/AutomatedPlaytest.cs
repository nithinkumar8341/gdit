using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GoldenHour
{
    /// <summary>
    /// End-to-end playtest that drives the real game loop in a player build. Inactive unless the
    /// player is launched with <c>-goldenhour-autotest &lt;outputDirectory&gt;</c>. Writes screenshots
    /// and <c>autotest_results.txt</c>, then quits with exit code 0 (all passed) or 1.
    /// The player's saved preferences are restored afterwards.
    /// </summary>
    public sealed class AutomatedPlaytest : MonoBehaviour
    {
        private const string Argument = "-goldenhour-autotest";
        private readonly List<string> lines = new List<string>();
        private string outputDirectory;
        private int failures;
        private float savedVolume;
        private float savedSensitivity;
        private Difficulty savedDifficulty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, Argument);
            if (index < 0 || index + 1 >= args.Length) return;
            GameObject runner = new GameObject("Automated Playtest");
            DontDestroyOnLoad(runner);
            runner.AddComponent<AutomatedPlaytest>().outputDirectory = Path.GetFullPath(args[index + 1]);
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(outputDirectory);
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            savedVolume = GameSettings.Volume;
            savedSensitivity = GameSettings.MouseSensitivity;
            savedDifficulty = GameSettings.Difficulty;
            Log("Golden Hour automated playtest | " + DateTime.Now.ToString("u") + " | " +
                Screen.width + "x" + Screen.height + " | Unity " + Application.unityVersion);

            yield return MainMenuChecks();
            yield return GameplayChecks();
            yield return RestartChecks();

            GameSettings.Volume = savedVolume;
            GameSettings.MouseSensitivity = savedSensitivity;
            GameSettings.Difficulty = savedDifficulty;
            Log(failures == 0 ? "RESULT: ALL CHECKS PASSED" : "RESULT: " + failures + " CHECK(S) FAILED");
            File.WriteAllLines(Path.Combine(outputDirectory, "autotest_results.txt"), lines);
            Application.Quit(failures == 0 ? 0 : 1);
        }

        // ------------------------------------------------------------ main menu

        private IEnumerator MainMenuChecks()
        {
            MainMenuController menu = null;
            yield return WaitFor(() => (menu = FindAnyObjectByType<MainMenuController>()) != null, 10f);
            Check("Main menu scene loads first", menu != null && SceneManager.GetActiveScene().name == "MainMenu",
                SceneManager.GetActiveScene().name);
            if (menu == null) yield break;
            Check("Main menu uses the Input System UI module",
                FindAnyObjectByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() != null);
            yield return WaitFor(() => SceneFader.Alpha < 0.01f, 3f);
            Check("Menu fades in from black", SceneFader.Alpha < 0.01f, SceneFader.Alpha.ToString("0.00"));

            ButtonFeedback feedback = FindAnyObjectByType<ButtonFeedback>();
            if (feedback != null)
            {
                feedback.OnPointerEnter(null);
                yield return new WaitForSecondsRealtime(0.2f);
                float hoverScale = feedback.transform.localScale.x;
                feedback.OnPointerDown(null);
                yield return new WaitForSecondsRealtime(0.2f);
                float pressScale = feedback.transform.localScale.x;
                feedback.OnPointerUp(null);
                feedback.OnPointerExit(null);
                Check("Buttons react to hover (grow) and press (shrink)",
                    hoverScale > 1.02f && pressScale < 0.98f,
                    "hover " + hoverScale.ToString("0.000") + ", press " + pressScale.ToString("0.000"));
            }
            else
            {
                Check("Buttons have hover/click feedback", false);
            }

            yield return Screenshot("01_main_menu");

            menu.ShowHowToPlay();
            yield return null;
            Check("How to Play view opens", menu.CurrentView == "How To Play");
            Check("How to Play shows the educational disclaimer", ScreenTextContains("not medical training"));
            yield return Screenshot("02_how_to_play");

            menu.ShowSettings();
            yield return null;
            Slider volume = FindSlider("Master Volume");
            Slider sensitivity = FindSlider("Mouse Sensitivity");
            Check("Settings view has volume and mouse sensitivity sliders",
                menu.CurrentView == "Settings" && volume != null && sensitivity != null);
            if (volume != null)
            {
                volume.value = 0.35f;
                Check("Volume slider changes AudioListener.volume",
                    Mathf.Approximately(AudioListener.volume, 0.35f), AudioListener.volume.ToString("0.00"));
                volume.value = savedVolume;
            }

            if (sensitivity != null)
            {
                sensitivity.value = 1.7f;
                Check("Sensitivity slider saves the setting",
                    Mathf.Approximately(GameSettings.MouseSensitivity, 1.7f), GameSettings.MouseSensitivity.ToString("0.00"));
            }

            menu.SetDifficulty(Difficulty.Easy);
            yield return null;
            Check("Difficulty option switches to Easy", GameSettings.Difficulty == Difficulty.Easy &&
                ScreenTextContains("> EASY <"));
            yield return Screenshot("03_settings");

            menu.ShowHome();
            yield return null;
            menu.StartGame();
            yield return null;
            Check("Start fades out before loading", SceneFader.IsLoading && SceneFader.Alpha > 0f,
                SceneFader.Alpha.ToString("0.00"));
        }

        // ------------------------------------------------------------- gameplay

        private IEnumerator GameplayChecks()
        {
            GameplayController game = null;
            yield return WaitFor(() => (game = FindAnyObjectByType<GameplayController>()) != null, 15f);
            Check("Start loads the gameplay scene", game != null, SceneManager.GetActiveScene().name);
            if (game == null) yield break;
            yield return WaitFor(() => !SceneFader.IsLoading && SceneFader.Alpha < 0.01f, 3f);
            Check("Gameplay fades in", SceneFader.Alpha < 0.01f);

            GameManager manager = FindAnyObjectByType<GameManager>();
            ScoreManager score = FindAnyObjectByType<ScoreManager>();
            FirstPersonPlayer player = FindAnyObjectByType<FirstPersonPlayer>();
            Check("Existing Player, GameManager and ScoreManager are present",
                manager != null && score != null && player != null && GameObject.Find("Player") != null);
            Check("Five patients spawned", game.Patients.Count == 5, game.Patients.Count.ToString());
            Check("First-aid kit spawned", game.Kit != null);
            Check("Briefing shown before play", game.Modal.Title == "SCENE BRIEFING", game.Modal.Title);
            Check("Briefing has the disclaimer", ScreenTextContains("not medical training"));
            GoldenHourAudio gameAudio = FindAnyObjectByType<GoldenHourAudio>();
            Check("Procedural roadside ambience is playing", gameAudio != null && gameAudio.AmbiencePlaying);
            Check("Difficulty carried into the scenario", game.Difficulty == Difficulty.Easy);
            yield return Screenshot("04_briefing");
            if (manager == null || score == null || player == null || game.Patients.Count != 5) yield break;

            AccidentEnvironment scene = FindAnyObjectByType<AccidentEnvironment>();
            yield return WaitFor(() => scene != null && scene.Smoke != null && scene.Smoke.particleCount > 0, 3f);
            VisualChecks(player);
            yield return HazardFlashCheck();

            game.BeginSimulation();
            yield return new WaitForSeconds(0.6f);
            Check("Timer runs after briefing", manager.scenarioRunning && !manager.scenarioPaused);
            Check("Cursor locked while exploring", Cursor.lockState == CursorLockMode.Locked);
            Check("Player rests on the ground (gravity/collision)",
                player.transform.position.y > -0.1f && player.transform.position.y < 0.4f,
                player.transform.position.ToString("F2"));
            Check("Tutorial hint shown at the start", game.Hud.TutorialVisible);
            CompassCheck(game, player, "Next person");
            VisibilityCheck(game, player);
            yield return Screenshot("05_scene_start");

            // WASD through the Input System, with footsteps.
            Vector3 before = player.transform.position;
            int stepsBefore = gameAudio != null ? gameAudio.FootstepsPlayed : 0;
            yield return HoldKey(Key.W, 1.0f);
            float moved = Vector3.Distance(Flat(before), Flat(player.transform.position));
            Check("W key moves the player forward", moved > 2f && player.transform.position.z > before.z,
                "moved " + moved.ToString("F2") + " m");
            int steps = gameAudio != null ? gameAudio.FootstepsPlayed - stepsBefore : 0;
            Check("Footstep sounds play while walking", steps >= 1, steps + " steps");

            // Esc pause / resume.
            float timeBefore = manager.GetRemainingTime();
            yield return TapKey(Key.Escape);
            Check("Esc opens the pause menu and pauses the timer",
                game.Modal.Title == "PAUSED" && manager.scenarioPaused, game.Modal.Title);
            Check("Pause menu has volume and mouse sensitivity sliders",
                FindSlider("Master Volume") != null && FindSlider("Mouse Sensitivity") != null);
            Check("Cursor unlocked while paused", Cursor.lockState == CursorLockMode.None);
            yield return new WaitForSeconds(0.6f);
            Check("Timer does not advance while paused",
                Mathf.Abs(manager.GetRemainingTime() - timeBefore) < 0.2f);
            yield return Screenshot("06_pause_menu");
            yield return TapKey(Key.Escape);
            Check("Esc again resumes", !game.Modal.IsOpen && !manager.scenarioPaused);

            // Kit before any triage is refused.
            yield return Approach(player, game.Kit.transform.position + Vector3.up * 0.35f, null);
            Check("Kit refuses use before any triage", !game.TryInteract() && !game.Modal.IsOpen);
            yield return null;
            Check("Reach-scene objective completes past the barriers",
                game.Objectives.IsComplete(ObjectiveTracker.ReachScene));

            // Examine + triage every patient; P04 is first triaged wrongly to test feedback/reassessment.
            for (int i = 0; i < game.Patients.Count; i++)
            {
                PatientActor patient = game.Patients[i];
                Check(patient.Data.patientId + " beacon pulses before examination",
                    patient.Beacon != null && patient.Beacon.Pulsing);
                yield return Approach(player, patient.FocusPoint, patient);
                if (i == 0)
                {
                    yield return TapKey(Key.E);
                }
                else
                {
                    game.TryInteract();
                }

                bool opened = game.Modal.Title.StartsWith("PATIENT EXAMINATION");
                Check("Raycast + E examines " + patient.Data.patientId, opened, game.Modal.Title);
                Check(patient.Data.patientId + " examination hides the expected triage",
                    opened && !ScreenTextContains("Scenario key") && !ScreenTextContains(patient.Data.reasoning));
                Check(patient.Data.patientId + " data matches START-style rule",
                    TriageEvaluator.Evaluate(patient.Data) == patient.Data.correctTriage);
                if (i == 1)
                {
                    Check("Easy difficulty shows what to look for", ScreenTextContains("EASY HINT"));
                    yield return Screenshot("07_examination");
                }

                if (patient.Data.patientId == "P04")
                {
                    int correctBefore = score.correctClassifications;
                    int scoreBefore = score.score;
                    game.AssignTriage(patient, TriageCategory.Green);
                    yield return null;
                    Check("Wrong triage gives feedback without credit",
                        score.correctClassifications == correctBefore && score.triageMistakes == 1 &&
                        score.score == scoreBefore - ScoreManager.WrongTriagePenalty &&
                        game.Modal.Title.StartsWith("TRIAGE FEEDBACK"));
                    yield return Screenshot("08_wrong_triage_feedback");
                    game.OpenExamination(patient);
                    yield return null;
                    Check("Reassessment reopens the examination", game.Modal.Title.StartsWith("PATIENT EXAMINATION"));
                }

                game.AssignTriage(patient, patient.Data.correctTriage);
                yield return null;
                Check(patient.Data.patientId + " triage feedback shown",
                    game.Modal.Title.StartsWith("TRIAGE FEEDBACK") && patient.HasBeenTriaged && patient.LastTriageWasCorrect);
                game.ResumeSimulation();
                yield return null;
            }

            Check("Examine-all and triage-all objectives complete",
                game.Objectives.IsComplete(ObjectiveTracker.ExamineAll) &&
                game.Objectives.IsComplete(ObjectiveTracker.TriageAll));
            Check("Score counts five correct triage decisions", score.correctClassifications == 5 &&
                score.incorrectClassifications == 0, score.correctClassifications + " correct");
            Check("Beacons take the assigned triage colour",
                game.Patients[1].Beacon.CurrentColor == Palette.ForCategory(game.Patients[1].AssignedCategory));

            player.Teleport(new Vector3(-1.5f, 0.1f, -5.5f), 0f, 0f);
            player.LookAt(new Vector3(0.5f, 0.3f, 7f));
            yield return new WaitForSeconds(0.4f);
            CompassCheck(game, player, "First-aid kit");
            yield return Screenshot("09_triaged_scene");

            // First-aid kit: lid animation and inventory.
            Quaternion lidClosed = game.Kit.Lid != null ? game.Kit.Lid.localRotation : Quaternion.identity;
            yield return Approach(player, game.Kit.transform.position + Vector3.up * 0.35f, null);
            bool kitOpened = game.TryInteract();
            Check("Raycast + E opens the first-aid kit inventory",
                kitOpened && game.Modal.Title.StartsWith("FIRST-AID KIT"), game.Modal.Title);
            yield return new WaitForSeconds(0.9f);
            Check("Kit lid animates open", game.Kit.Lid != null &&
                Quaternion.Angle(lidClosed, game.Kit.Lid.localRotation) > 90f);
            yield return Screenshot("10_kit_inventory");
            game.Modal.Close();
            yield return null;
            yield return Screenshot("11_kit_lid_open");

            // Supplies: one wrong choice, then the recommended item for everyone.
            SupplyData[] supplies = game.supplies.ToArray();
            PatientActor first = game.Patients[0];
            SupplyData wrong = Array.Find(supplies, s => s != first.Data.recommendedSupply);
            int aidBefore = score.completedTreatments;
            int scoreBeforeWrong = score.score;
            bool wrongAccepted = game.ApplySupply(first, wrong);
            yield return null;
            Check("Wrong supply gives feedback and no reward",
                !wrongAccepted && !first.HasReceivedAid && score.completedTreatments == aidBefore &&
                score.score == scoreBeforeWrong - ScoreManager.WrongSupplyPenalty &&
                ScreenTextContains("does not fit"));
            yield return Screenshot("12_wrong_supply");

            foreach (PatientActor patient in game.Patients)
            {
                if (patient == game.Patients[game.Patients.Count - 1])
                {
                    // Before the final aid ends the scenario: the tutorial has timed out and the
                    // crash close-up is captured with the HUD.
                    game.ResumeSimulation();
                    yield return WaitFor(() => !game.Hud.TutorialVisible, GameplayHud.TutorialSeconds + 2f);
                    Check("Tutorial hint disappears after about 15 seconds", !game.Hud.TutorialVisible);
                    player.Teleport(new Vector3(-3.4f, 0.1f, -1.2f), 0f, 0f);
                    player.LookAt(new Vector3(0.6f, 0.9f, 5.4f));
                    yield return new WaitForSeconds(0.5f);
                    yield return Screenshot("15_crash_closeup");
                }

                bool applied = game.ApplySupply(patient, patient.Data.recommendedSupply);
                yield return null;
                Check(patient.Data.patientId + " simulated aid with " + patient.Data.recommendedSupply.displayName,
                    applied && patient.HasReceivedAid && game.Modal.Title.StartsWith("SIMULATED AID COMPLETE"));
                Check(patient.Data.patientId + " beacon settles after aid", !patient.Beacon.Pulsing);
                if (patient == game.Patients[game.Patients.Count - 1]) yield return Screenshot("13_aid_complete");
                game.ResumeSimulation();
                yield return null;
            }

            // Final report.
            yield return WaitFor(() => game.IsFinished, 3f);
            Check("Scenario finishes after all aid", game.IsFinished && game.Modal.Title == "FINAL SCENARIO REPORT");
            Check("All five objectives complete", game.Objectives.CompletedCount == 5,
                game.Objectives.CompletedCount + "/5");
            int expected = 5 * ScoreManager.CorrectTriagePoints + 5 * ScoreManager.AidPoints +
                           5 * ScoreManager.ObjectivePoints - ScoreManager.WrongTriagePenalty -
                           ScoreManager.WrongSupplyPenalty + score.timeBonus;
            Check("Final score is consistent", score.score == expected,
                "score " + score.score + ", expected " + expected + ", time bonus " + score.timeBonus);
            Check("Time bonus capped and below one correct triage",
                score.timeBonus >= 0 && score.timeBonus <= ScoreManager.MaxTimeBonus &&
                ScoreManager.MaxTimeBonus < ScoreManager.CorrectTriagePoints);
            Check("Final report includes the disclaimer, rating and difficulty",
                game.FinalReport.Contains("not medical training") && game.FinalReport.Contains("Rating") &&
                game.FinalReport.Contains("Difficulty: EASY"));
            Check("Timer stopped at the end", !manager.scenarioRunning && manager.scenarioCompleted);
            yield return Screenshot("14_final_report");
        }

        /// <summary>Overlay HUD, lighting, fog, post-processing and smoke.</summary>
        private void VisualChecks(FirstPersonPlayer player)
        {
            bool overlay = true;
            foreach (Canvas canvas in FindObjectsByType<Canvas>())
            {
                if (canvas.isRootCanvas) overlay &= canvas.renderMode == RenderMode.ScreenSpaceOverlay;
            }

            Check("All UI canvases are Screen Space - Overlay (drawn over the 3D world)", overlay);
            Check("Light fog enabled", RenderSettings.fog && RenderSettings.fogMode == FogMode.Linear);
            Light sun = RenderSettings.sun;
            Check("Warm low sun with soft shadows", sun != null && sun.shadows == LightShadows.Soft &&
                sun.color.r > sun.color.b && sun.transform.eulerAngles.x < 35f,
                sun != null ? sun.transform.eulerAngles.ToString("F0") : "no sun");
            Camera view = player.PlayerCamera;
            Volume volume = FindAnyObjectByType<Volume>();
            Check("Camera renders URP post-processing",
                view != null && view.GetUniversalAdditionalCameraData().renderPostProcessing);
            Check("Post-processing profile has bloom, vignette and colour grading",
                volume != null && volume.sharedProfile != null &&
                volume.sharedProfile.TryGet(out Bloom bloom) && bloom.active && bloom.intensity.value > 0f &&
                volume.sharedProfile.TryGet(out Vignette vignette) && vignette.intensity.value > 0f &&
                volume.sharedProfile.TryGet(out ColorAdjustments grading) && grading.active);
            AccidentEnvironment environment = FindAnyObjectByType<AccidentEnvironment>();
            Check("Smoke particles are emitting from the damaged vehicle",
                environment != null && environment.Smoke != null && environment.Smoke.isPlaying &&
                environment.Smoke.particleCount > 0,
                environment != null && environment.Smoke != null ? environment.Smoke.particleCount + " particles" : "none");
            Check("Roadside detail present (skid marks, fences, signs, grass)",
                GameObject.Find("Skid Mark") != null && GameObject.Find("Fence Post") != null &&
                GameObject.Find("Accident Board") != null && GameObject.Find("Grass Tuft") != null &&
                GameObject.Find("Street Lamp Bulb") != null);
        }

        private IEnumerator HazardFlashCheck()
        {
            AccidentEnvironment environment = FindAnyObjectByType<AccidentEnvironment>();
            if (environment == null || environment.HazardLights.Length == 0)
            {
                Check("Hazard lights flash on the damaged vehicle", false);
                yield break;
            }

            bool initial = environment.HazardLights[0].IsOn;
            bool toggled = false;
            float end = Time.realtimeSinceStartup + 1.5f;
            while (!toggled && Time.realtimeSinceStartup < end)
            {
                toggled = environment.HazardLights[0].IsOn != initial;
                yield return null;
            }

            Check("Hazard lights flash on the damaged vehicle", toggled,
                environment.HazardLights.Length + " lamps");
        }

        private void CompassCheck(GameplayController game, FirstPersonPlayer player, string expectedLabel)
        {
            Transform view = player.PlayerCamera.transform;
            Vector3 target;
            if (expectedLabel == "First-aid kit")
            {
                target = game.Kit.transform.position;
            }
            else
            {
                PatientActor nearest = null;
                foreach (PatientActor patient in game.Patients)
                {
                    if (patient.HasBeenExamined) continue;
                    if (nearest == null || (patient.FocusPoint - view.position).sqrMagnitude <
                        (nearest.FocusPoint - view.position).sqrMagnitude) nearest = patient;
                }

                target = nearest != null ? nearest.FocusPoint : view.position;
            }

            Vector3 to = target - view.position;
            to.y = 0f;
            Vector3 forward = view.forward;
            forward.y = 0f;
            float expected = Vector3.SignedAngle(forward, to, Vector3.up);
            Check("Compass arrow points to the " + (expectedLabel == "First-aid kit" ? "kit" : "nearest unexamined person"),
                game.Hud.CompassVisible && game.Hud.CompassLabel.Contains(expectedLabel) &&
                Mathf.Abs(Mathf.DeltaAngle(game.Hud.CompassAngle, expected)) < 6f,
                game.Hud.CompassLabel.Replace("<b>", string.Empty).Replace("</b>", string.Empty) +
                " | arrow " + game.Hud.CompassAngle.ToString("F0") + " deg, expected " + expected.ToString("F0"));
        }

        /// <summary>From the start position every beacon is on screen with a clear line of sight.</summary>
        private void VisibilityCheck(GameplayController game, FirstPersonPlayer player)
        {
            Camera view = player.PlayerCamera;
            int visible = 0;
            foreach (PatientActor patient in game.Patients)
            {
                Transform marker = patient.Beacon != null ? patient.Beacon.transform.Find("Beacon Marker") : null;
                if (marker == null) continue;
                Vector3 viewport = view.WorldToViewportPoint(marker.position);
                bool onScreen = viewport.z > 0f && viewport.x > 0f && viewport.x < 1f && viewport.y > 0f && viewport.y < 1f;
                Vector3 eye = view.transform.position;
                bool clear = !Physics.Linecast(eye, marker.position, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);
                if (onScreen && clear) visible++;
            }

            float distance = Vector3.Distance(Flat(player.transform.position), Flat(game.Patients[0].transform.position));
            Check("All five patient beacons visible from the start position", visible == 5, visible + "/5 visible");
            Check("Player starts close to the accident", player.transform.position.z > -14.5f,
                "start z " + player.transform.position.z.ToString("F1") + ", nearest patient " + distance.ToString("F1") + " m");
        }

        private IEnumerator RestartChecks()
        {
            GameplayController game = FindAnyObjectByType<GameplayController>();
            if (game == null) yield break;
            GameSettings.Difficulty = Difficulty.Normal;
            Dictionary<string, Vector3> before = new Dictionary<string, Vector3>();
            foreach (PatientActor patient in game.Patients) before[patient.Data.patientId] = patient.transform.position;

            game.RestartScenario();
            yield return null;
            Check("Restart fades out", SceneFader.IsLoading && SceneFader.Alpha > 0f);
            GameplayController restarted = null;
            yield return WaitFor(() => (restarted = FindAnyObjectByType<GameplayController>()) != null &&
                                       restarted != game, 15f);
            yield return WaitFor(() => !SceneFader.IsLoading, 3f);
            yield return null;
            Check("Restart reloads a fresh scenario", restarted != null && !restarted.HasStarted &&
                restarted.Modal.Title == "SCENE BRIEFING");
            ScoreManager score = FindAnyObjectByType<ScoreManager>();
            Check("Score resets on restart", score != null && score.score == 0);
            if (restarted == null || score == null) yield break;

            int moved = 0;
            bool rulesHold = true;
            foreach (PatientActor patient in restarted.Patients)
            {
                if (Vector3.Distance(before[patient.Data.patientId], patient.transform.position) > 0.5f) moved++;
                rulesHold &= TriageEvaluator.Evaluate(patient.Data) == patient.Data.correctTriage;
            }

            Check("Restart shuffles which person is at which position", restarted.LayoutShuffled && moved >= 2,
                moved + " of 5 moved | layout " + string.Join(",", restarted.Layout));
            Check("Triage rule still holds for every shuffled person", rulesHold);

            // Normal difficulty: no hint; scoring unchanged after shuffling.
            restarted.BeginSimulation();
            yield return new WaitForSeconds(0.5f);
            Check("Difficulty Normal applied after restart", restarted.Difficulty == Difficulty.Normal);
            CompassCheck(restarted, FindAnyObjectByType<FirstPersonPlayer>(), "Next person");
            yield return Screenshot("16_restart_shuffled");
            restarted.OpenExamination(restarted.Patients[0]);
            yield return null;
            Check("Normal difficulty shows no hint", !ScreenTextContains("EASY HINT"));
            foreach (PatientActor patient in restarted.Patients)
            {
                restarted.OpenExamination(patient);
                restarted.AssignTriage(patient, patient.Data.correctTriage);
                yield return null;
            }

            restarted.ResumeSimulation();
            yield return null;
            int expected = 5 * ScoreManager.CorrectTriagePoints + 2 * ScoreManager.ObjectivePoints;
            Check("Scoring is correct with shuffled positions", score.correctClassifications == 5 &&
                score.score == expected, "score " + score.score + ", expected " + expected);

            restarted.ReturnToMenu();
            MainMenuController menu = null;
            yield return WaitFor(() => (menu = FindAnyObjectByType<MainMenuController>()) != null, 15f);
            Check("Main Menu button returns to the menu", menu != null);
            yield return WaitFor(() => !SceneFader.IsLoading, 3f);
            Check("Cursor visible in menu", Cursor.visible && Cursor.lockState == CursorLockMode.None);
        }

        // -------------------------------------------------------------- helpers

        /// <summary>Places the player near a target with a clear line of sight, then looks at it.</summary>
        private IEnumerator Approach(FirstPersonPlayer player, Vector3 focus, PatientActor patient)
        {
            Vector3 best = focus + new Vector3(0f, 0f, -1.8f);
            for (int step = 0; step < 16; step++)
            {
                float angle = 180f + (step % 2 == 0 ? 1 : -1) * (step / 2) * 22.5f;
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 1.9f;
                Vector3 feet = new Vector3(focus.x + offset.x, 0.1f, focus.z + offset.z);
                if (Physics.CheckCapsule(feet + Vector3.up * 0.35f, feet + Vector3.up * 1.6f, 0.3f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                Vector3 eye = feet + Vector3.up * 1.52f;
                if (Physics.Raycast(eye, focus - eye, out RaycastHit hit, 4f) &&
                    (patient == null ? hit.collider.GetComponentInParent<FirstAidKitInteractable>() != null
                                     : hit.collider.GetComponentInParent<PatientActor>() == patient))
                {
                    best = feet;
                    break;
                }
            }

            player.Teleport(best, 0f, 0f);
            player.LookAt(focus);
            yield return null;
            yield return null;
        }

        private static Slider FindSlider(string sliderName)
        {
            foreach (Slider slider in FindObjectsByType<Slider>(FindObjectsInactive.Exclude))
            {
                if (slider.name == sliderName) return slider;
            }

            return null;
        }

        private static IEnumerator HoldKey(Key key, float seconds)
        {
            Keyboard keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return new WaitForSeconds(seconds);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;
        }

        private static IEnumerator TapKey(Key key)
        {
            Keyboard keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }

        private static IEnumerator WaitFor(Func<bool> condition, float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        }

        private IEnumerator Screenshot(string name)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(outputDirectory, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            yield return null;
            yield return null;
            Log("  screenshot " + name + ".png");
        }

        private static bool ScreenTextContains(string value)
        {
            foreach (TMPro.TMP_Text text in FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Exclude))
            {
                if (text.text != null && text.text.Contains(value)) return true;
            }

            return false;
        }

        private static Vector3 Flat(Vector3 value) => new Vector3(value.x, 0f, value.z);

        private void Check(string name, bool passed, string detail = null)
        {
            if (!passed) failures++;
            Log((passed ? "PASS  " : "FAIL  ") + name + (string.IsNullOrEmpty(detail) ? string.Empty : "  [" + detail + "]"));
        }

        private void Log(string line)
        {
            lines.Add(line);
            Debug.Log("[Autotest] " + line);
        }
    }
}
