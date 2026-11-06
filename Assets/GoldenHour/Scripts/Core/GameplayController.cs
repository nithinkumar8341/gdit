using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace GoldenHour
{
    /// <summary>
    /// Coordinates the scenario loop: briefing → find/examine → triage → first-aid kit →
    /// supply selection → simulated aid → objectives → final report.
    /// </summary>
    public sealed class GameplayController : MonoBehaviour
    {
        public const string MainMenuScene = "MainMenu";
        public const float InteractionRange = 3.6f;

        private const string Disclaimer =
            "This is an educational simulation, not medical training or medical advice. " +
            "In a real emergency, call local emergency services and follow qualified guidance.";

        [Header("Existing scene systems")]
        public GameManager gameManager;
        public ScoreManager scoreManager;
        public FirstPersonPlayer player;
        public Transform playerCamera;

        [Header("Scenario data")]
        public List<PatientData> patientProfiles = new List<PatientData>();
        public List<SupplyData> supplies = new List<SupplyData>();

        [Header("Assets")]
        public Material baseMaterial;
        public GameObject mannequinPrefab;
        public GameObject kitPrefab;

        private static readonly Vector3[] PatientPositions =
        {
            new Vector3(4.4f, 0f, -2.5f),
            new Vector3(-2.8f, 0f, 1.2f),
            new Vector3(3.4f, 0f, 9.2f),
            new Vector3(-3.6f, 0f, 9.6f),
            new Vector3(0.6f, 0f, 13.6f)
        };

        private static readonly float[] PatientYaw = { -100f, 70f, -150f, 160f, 95f };

        private readonly List<PatientActor> patients = new List<PatientActor>();
        private readonly ObjectiveTracker objectives = new ObjectiveTracker();
        private GoldenHourAudio audioManager;
        private GameplayHud hud;
        private ModalWindow modal;
        private FirstAidKitInteractable firstAidKit;
        private bool paused;

        public IReadOnlyList<PatientActor> Patients => patients;
        public ObjectiveTracker Objectives => objectives;
        public FirstAidKitInteractable Kit => firstAidKit;
        public ModalWindow Modal => modal;
        public bool HasStarted { get; private set; }
        public bool IsFinished { get; private set; }
        public string FinalReport { get; private set; } = string.Empty;

        private void Start()
        {
            MaterialFactory.Template = baseMaterial;
            ResolveSceneReferences();
            AccidentEnvironment environment = GetComponent<AccidentEnvironment>();
            if (environment == null) environment = gameObject.AddComponent<AccidentEnvironment>();
            environment.Build();
            PlacePlayer();
            BuildPatients();
            BuildKit();

            audioManager = GetComponent<GoldenHourAudio>();
            UiFactory.ButtonPressed = audioManager != null ? audioManager.PlayButton : (System.Action)null;
            Canvas canvas = UiFactory.CreateCanvas("Golden Hour Gameplay UI");
            hud = new GameplayHud(canvas.transform);
            modal = new ModalWindow(canvas.transform);
            objectives.Completed += OnObjectiveCompleted;

            if (scoreManager != null) scoreManager.ResetScore();
            if (gameManager != null) gameManager.ResetScenario();
            ShowBriefing();
            RefreshHud();
        }

        private void ResolveSceneReferences()
        {
            if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
            if (scoreManager == null) scoreManager = FindAnyObjectByType<ScoreManager>();
            if (player == null) player = FindAnyObjectByType<FirstPersonPlayer>();
            if (player != null && playerCamera == null && player.PlayerCamera != null)
            {
                playerCamera = player.PlayerCamera.transform;
            }
        }

        private void PlacePlayer()
        {
            if (player == null)
            {
                Debug.LogError("Golden Hour scene requires the existing Player object.");
                return;
            }

            player.Teleport(AccidentEnvironment.PlayerStart, 0f, 8f);
        }

        private void BuildPatients()
        {
            if (patientProfiles == null || patientProfiles.Count != PatientPositions.Length)
            {
                Debug.LogError("Five PatientData assets must be assigned to GameplayController.");
                return;
            }

            Transform group = new GameObject("Patients").transform;
            for (int i = 0; i < patientProfiles.Count; i++)
            {
                GameObject patient = new GameObject(patientProfiles[i].patientId);
                patient.transform.SetParent(group, false);
                patient.transform.SetPositionAndRotation(PatientPositions[i],
                    Quaternion.Euler(0f, PatientYaw[i], 0f));
                PatientActor actor = patient.AddComponent<PatientActor>();
                actor.Initialize(patientProfiles[i], mannequinPrefab);
                patients.Add(actor);
            }
        }

        private void BuildKit()
        {
            GameObject kit = new GameObject("First Aid Kit");
            kit.transform.SetPositionAndRotation(new Vector3(2.4f, 0.02f, -7.2f),
                Quaternion.Euler(0f, -20f, 0f));
            Transform lid = null;
            if (kitPrefab != null)
            {
                GameObject model = Instantiate(kitPrefab, kit.transform);
                model.name = "Kit Model";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                lid = FindChild(model.transform, "KitHinge");
            }

            if (lid == null) lid = BuildFallbackKit(kit.transform);
            BoxCollider collider = kit.AddComponent<BoxCollider>();
            collider.size = new Vector3(1.3f, 0.75f, 1f);
            collider.center = new Vector3(0f, 0.37f, 0f);
            firstAidKit = kit.AddComponent<FirstAidKitInteractable>();
            firstAidKit.Initialize(lid);
        }

        private static Transform BuildFallbackKit(Transform parent)
        {
            Material red = MaterialFactory.Create(Palette.EmergencyRed);
            Material white = MaterialFactory.Create(Color.white);
            AccidentEnvironment.Box(parent, "Kit Base", new Vector3(0f, 0.26f, 0f), new Vector3(1.25f, 0.52f, 0.92f), red);
            Transform hinge = new GameObject("KitHinge").transform;
            hinge.SetParent(parent, false);
            hinge.localPosition = new Vector3(0f, 0.52f, -0.46f);
            AccidentEnvironment.Box(hinge, "Lid", new Vector3(0f, 0.08f, 0.46f), new Vector3(1.27f, 0.16f, 0.94f), red);
            AccidentEnvironment.Box(hinge, "Cross", new Vector3(0f, 0.17f, 0.46f), new Vector3(0.56f, 0.03f, 0.2f), white);
            return hinge;
        }

        private void Update()
        {
            if (!HasStarted || IsFinished) return;
            hud.Tick();
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (paused || (modal.IsOpen && modal.Closable)) ResumeSimulation();
                else if (!modal.IsOpen) ShowPauseMenu();
                return;
            }

            if (!modal.IsOpen)
            {
                if (player != null && player.transform.position.z > AccidentEnvironment.SceneEntryZ)
                {
                    objectives.Set(ObjectiveTracker.ReachScene, true);
                }

                UpdatePrompt();
                if (keyboard != null && keyboard.eKey.wasPressedThisFrame) TryInteract();
            }

            RefreshHud();
            if (gameManager != null && gameManager.scenarioCompleted) FinishScenario();
        }

        // ------------------------------------------------------------------ flow

        private void ShowBriefing()
        {
            if (player != null) player.SetControlsEnabled(false);
            hud.SetVisible(false);
            modal.Show("SCENE BRIEFING",
                "A car has left the road and struck a barrier. Five people are injured or shaken. " +
                "You are first on scene and emergency services are on the way.\n\n" +
                "<b>Your task</b>\n" +
                "1. Walk to the scene and find all five people.\n" +
                "2. Examine each person (look at them and press <b>E</b>).\n" +
                "3. Assign a triage priority: RED (immediate), YELLOW (delayed) or GREEN (minor).\n" +
                "4. Open the first-aid kit, choose a suitable supply and complete the simulated aid step.\n\n" +
                "The timer pauses while a panel is open. Accuracy matters far more than speed.\n\n" +
                "<color=#CDD3D8><i>" + Disclaimer + "</i></color>",
                new Vector2(900f, 560f), 100f, false);
            UiFactory.Button(modal.Panel, "BEGIN SCENARIO", new Vector2(0f, modal.RowY(0)),
                new Vector2(300f, 58f), BeginSimulation);
        }

        public void BeginSimulation()
        {
            if (HasStarted) return;
            HasStarted = true;
            modal.Close();
            hud.SetVisible(true);
            if (gameManager != null) gameManager.StartScenario();
            if (player != null) player.SetControlsEnabled(true);
            hud.SetStatus("Walk toward the crash past the barriers ahead.");
            RefreshHud();
        }

        /// <summary>Raycasts from the camera and interacts with a person or the kit in range.</summary>
        public bool TryInteract()
        {
            if (playerCamera == null) return false;
            object target = FindTarget();
            if (target is PatientActor patient)
            {
                if (audioManager != null) audioManager.PlayPatient();
                OpenExamination(patient);
                return true;
            }

            if (target is FirstAidKitInteractable kit)
            {
                return OpenKit(kit);
            }

            hud.SetStatus("Nothing in reach. Look directly at a person or the first-aid kit.");
            return false;
        }

        private object FindTarget()
        {
            Ray ray = new Ray(playerCamera.position, playerCamera.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, InteractionRange,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return null;
            }

            PatientActor patient = hit.collider.GetComponentInParent<PatientActor>();
            if (patient != null) return patient;
            return hit.collider.GetComponentInParent<FirstAidKitInteractable>();
        }

        private void UpdatePrompt()
        {
            object target = playerCamera != null ? FindTarget() : null;
            if (target is PatientActor patient)
            {
                hud.SetPrompt("[E]  Examine " + patient.Data.patientName);
            }
            else if (target is FirstAidKitInteractable)
            {
                hud.SetPrompt("[E]  Open first-aid kit");
            }
            else
            {
                hud.SetPrompt(string.Empty);
            }
        }

        public void OpenExamination(PatientActor patient)
        {
            if (patient.MarkExamined() && scoreManager != null) scoreManager.MarkExamined();
            UpdateObjectives();
            PatientData data = patient.Data;
            string previous = patient.HasBeenTriaged
                ? "\n<color=#F6D171>Previously assigned: " + Palette.Label(patient.AssignedCategory) +
                  ". Reassess and choose again if needed.</color>"
                : string.Empty;
            OpenModal("PATIENT EXAMINATION  |  " + data.patientId,
                "<b>" + data.patientName + "</b>, age " + data.age + "\n\n" +
                "<b>Visible injury:</b> " + data.injuryDescription + "\n" +
                "<b>Reported / observed:</b> " + data.symptoms + "\n\n" +
                "<b>Walking:</b> " + (data.canWalk ? "Able to walk" : "Unable to walk") + "\n" +
                "<b>Breathing:</b> " + (data.breathing ? "Breathing observed" : "No breathing observed") + "\n" +
                "<b>Responsiveness:</b> " + data.responsiveness + "\n" +
                "<b>Bleeding:</b> " + (data.severeBleeding ? "Severe, visible bleeding" : "No severe bleeding seen") + "\n" +
                previous + "\n\nChoose a triage priority for this person:",
                new Vector2(920f, 620f), 180f, true);
            Transform panel = modal.Panel;
            float y = modal.RowY(1);
            UiFactory.Button(panel, "RED  |  IMMEDIATE", new Vector2(-285f, y), new Vector2(265f, 58f),
                () => AssignTriage(patient, TriageCategory.Red), Palette.TriageRed);
            UiFactory.Button(panel, "YELLOW  |  DELAYED", new Vector2(0f, y), new Vector2(265f, 58f),
                () => AssignTriage(patient, TriageCategory.Yellow), Palette.TriageYellow);
            UiFactory.Button(panel, "GREEN  |  MINOR", new Vector2(285f, y), new Vector2(265f, 58f),
                () => AssignTriage(patient, TriageCategory.Green), Palette.TriageGreen);
            UiFactory.Button(panel, "CLOSE", new Vector2(0f, modal.RowY(0)), new Vector2(200f, 48f),
                ResumeSimulation, Palette.Slate);
        }

        public void AssignTriage(PatientActor patient, TriageCategory category)
        {
            bool previouslyTriaged = patient.HasBeenTriaged;
            bool previousCorrect = patient.LastTriageWasCorrect;
            patient.SetTriage(category);
            bool correct = patient.LastTriageWasCorrect;
            if (scoreManager != null) scoreManager.SetTriageResult(previouslyTriaged, previousCorrect, correct);

            StringBuilder text = new StringBuilder();
            text.Append("You assigned: <b>").Append(Palette.Label(category)).Append("</b>\n");
            text.Append("Scenario key: <b>").Append(Palette.Label(patient.Data.correctTriage)).Append("</b>\n\n");
            text.Append(correct
                ? "<color=#7FE0A6><b>Correct.</b> +" + ScoreManager.CorrectTriagePoints + " points.</color>"
                : "<color=#FF8A8A><b>Not the expected priority.</b> -" + ScoreManager.WrongTriagePenalty +
                  " points. You can reassess this person.</color>");
            text.Append("\n\n<b>Why:</b> ").Append(patient.Data.reasoning);
            text.Append("\n\n<size=85%><color=#CDD3D8>Simplified START-style rule used in this game: " +
                        "able to walk = GREEN; not breathing, severe bleeding or unresponsive = RED; " +
                        "otherwise YELLOW.</color></size>");
            OpenModal("TRIAGE FEEDBACK  |  " + patient.Data.patientId, text.ToString(),
                new Vector2(860f, 520f), 100f, true);
            UiFactory.Button(modal.Panel, "REASSESS", new Vector2(-140f, modal.RowY(0)),
                new Vector2(240f, 54f), () => OpenExamination(patient), Palette.Slate);
            UiFactory.Button(modal.Panel, "CONTINUE", new Vector2(140f, modal.RowY(0)),
                new Vector2(240f, 54f), ResumeSimulation);
            UpdateObjectives();
        }

        public bool OpenKit(FirstAidKitInteractable kit)
        {
            if (!patients.Exists(item => item.HasBeenTriaged))
            {
                hud.SetStatus("Examine and triage at least one person before using the kit.");
                return false;
            }

            if (!kit.IsOpen && audioManager != null) audioManager.PlayKitOpen();
            kit.Open();
            ShowInventory();
            return true;
        }

        private void ShowInventory()
        {
            List<PatientActor> waiting = patients.FindAll(item => item.HasBeenTriaged && !item.HasReceivedAid);
            waiting.Sort((a, b) => Priority(a.AssignedCategory).CompareTo(Priority(b.AssignedCategory)));
            int untriaged = patients.FindAll(item => !item.HasBeenTriaged).Count;
            OpenModal("FIRST-AID KIT  |  EDUCATIONAL SIMULATION",
                "Choose a triaged person (highest priority listed first), then select one supply. " +
                "A mismatched item gives feedback but no credit.\n" +
                (untriaged > 0 ? "<color=#F6D171>" + untriaged + " person(s) still need triage.</color>" : string.Empty),
                new Vector2(940f, 680f), 470f, true);
            Transform panel = modal.Panel;
            if (waiting.Count == 0)
            {
                UiFactory.Text(panel, "No triaged person is waiting for aid.", 22,
                    new Vector2(760f, 60f), new Vector2(0f, 40f), TextAlignmentOptions.Center);
            }

            float y = 130f;
            foreach (PatientActor actor in waiting)
            {
                PatientActor current = actor;
                UiFactory.Button(panel, current.Data.patientId + "  |  " + current.Data.patientName +
                                        "  |  " + Palette.Label(current.AssignedCategory),
                    new Vector2(0f, y), new Vector2(720f, 54f), () => ShowSupplyChoices(current),
                    Palette.ForCategory(current.AssignedCategory));
                y -= 64f;
            }

            UiFactory.Button(panel, "CLOSE KIT", new Vector2(0f, modal.RowY(0)), new Vector2(240f, 50f),
                ResumeSimulation, Palette.Slate);
        }

        private void ShowSupplyChoices(PatientActor patient, string feedback = null)
        {
            OpenModal("SELECT A SUPPLY  |  " + patient.Data.patientId + "  " + patient.Data.patientName,
                "<b>Injury:</b> " + patient.Data.injuryDescription + "\n" +
                "Select the item that best fits this person's simulated aid step." +
                (feedback != null ? "\n<color=#F6D171>" + feedback + "</color>" : string.Empty),
                new Vector2(980f, 660f), 500f, true);
            Transform panel = modal.Panel;
            for (int i = 0; i < supplies.Count; i++)
            {
                SupplyData supply = supplies[i];
                float x = i % 2 == 0 ? -230f : 230f;
                if (i == supplies.Count - 1 && supplies.Count % 2 == 1) x = 0f;
                float y = 120f - (i / 2) * 104f;
                UiFactory.Button(panel, supply.displayName + "\n<size=70%>" + supply.description + "</size>",
                    new Vector2(x, y), new Vector2(430f, 90f), () => ApplySupply(patient, supply),
                    supply.displayColor);
            }

            UiFactory.Button(panel, "BACK", new Vector2(0f, modal.RowY(0)), new Vector2(220f, 50f),
                ShowInventory, Palette.Slate);
        }

        /// <summary>Applies a supply. Returns true when it matched the scenario's recommended item.</summary>
        public bool ApplySupply(PatientActor patient, SupplyData supply)
        {
            if (patient.HasReceivedAid) return false;
            if (patient.Data.recommendedSupply != supply)
            {
                if (scoreManager != null) scoreManager.RecordWrongSupply();
                ShowSupplyChoices(patient, supply.displayName + " does not fit this scenario step. No credit (-" +
                                           ScoreManager.WrongSupplyPenalty + "). Try another item.");
                return false;
            }

            patient.MarkAidCompleted();
            if (scoreManager != null) scoreManager.MarkTreatmentCompleted();
            objectives.Set(ObjectiveTracker.SelectSupply, true);
            OpenModal("SIMULATED AID COMPLETE  |  " + patient.Data.patientId,
                "<b>Item:</b> " + supply.displayName + "  (+" + ScoreManager.AidPoints + " points)\n\n" +
                "<b>Simulated step:</b> " + patient.Data.aidAction + "\n\n" +
                "<b>Why this item:</b> " + supply.educationalPurpose + "\n\n" +
                "<color=#CDD3D8><i>This simplified activity is not a real-world treatment instruction.</i></color>",
                new Vector2(860f, 500f), 100f, true);
            UiFactory.Button(modal.Panel, "CONTINUE", new Vector2(0f, modal.RowY(0)), new Vector2(260f, 54f),
                ResumeSimulation);
            UpdateObjectives();
            return true;
        }

        private void ShowPauseMenu()
        {
            paused = true;
            OpenModal("PAUSED", "The scenario timer is paused.\n\nMaster volume", new Vector2(600f, 560f), 330f, true);
            Transform panel = modal.Panel;
            UiFactory.VolumeSlider(panel, new Vector2(0f, 90f), new Vector2(420f, 34f));
            UiFactory.Button(panel, "RESUME", new Vector2(0f, modal.RowY(2)), new Vector2(300f, 56f), ResumeSimulation);
            UiFactory.Button(panel, "RESTART", new Vector2(0f, modal.RowY(1)), new Vector2(300f, 56f),
                RestartScenario, Palette.Slate);
            UiFactory.Button(panel, "MAIN MENU", new Vector2(0f, modal.RowY(0)), new Vector2(300f, 56f),
                ReturnToMenu, Palette.Slate);
        }

        public void ResumeSimulation()
        {
            paused = false;
            modal.Close();
            if (IsFinished) return;
            if (gameManager != null) gameManager.ResumeScenario();
            if (player != null) player.SetControlsEnabled(true);
            RefreshHud();
            if (patients.Count > 0 && patients.TrueForAll(item => item.HasReceivedAid)) FinishScenario();
        }

        public void RestartScenario()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void ReturnToMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(MainMenuScene);
        }

        private void OpenModal(string title, string body, Vector2 size, float reservedBottom, bool closable)
        {
            if (player != null) player.SetControlsEnabled(false);
            if (gameManager != null && HasStarted && !IsFinished) gameManager.PauseScenario();
            hud.SetPrompt(string.Empty);
            modal.Show(title, body, size, reservedBottom, closable);
        }

        public void FinishScenario()
        {
            if (IsFinished) return;
            UpdateObjectives();
            IsFinished = true;
            paused = false;
            if (gameManager != null) gameManager.EndScenario();
            if (player != null) player.SetControlsEnabled(false);
            float remaining = gameManager != null ? gameManager.GetRemainingTime() : 0f;
            bool allAided = patients.TrueForAll(item => item.HasReceivedAid);
            int timeBonus = scoreManager != null && allAided ? scoreManager.AddTimeBonus(remaining) : 0;
            int score = scoreManager != null ? scoreManager.score : 0;
            int maximum = ScoreManager.MaximumScore(patients.Count, objectives.Count);

            StringBuilder report = new StringBuilder();
            report.Append("<b>Total score: ").Append(score).Append(" / ").Append(maximum).Append("</b>   ");
            report.Append("Rating: <b>").Append(Rating(score, maximum)).Append("</b>\n");
            report.Append(allAided ? "All simulated aid completed with " + FormatTime(remaining) + " remaining."
                                   : "<color=#F6D171>Time expired before every step was completed.</color>");
            report.Append("\n\n");
            if (scoreManager != null)
            {
                report.Append("Examined: ").Append(scoreManager.examinedPatients).Append("/").Append(patients.Count);
                report.Append("    Correct triage: ").Append(scoreManager.correctClassifications).Append("/").Append(patients.Count);
                report.Append("    Simulated aid: ").Append(scoreManager.completedTreatments).Append("/").Append(patients.Count);
                report.Append("\nObjectives: ").Append(objectives.CompletedCount).Append("/").Append(objectives.Count);
                report.Append("    Triage changes needed: ").Append(scoreManager.triageMistakes);
                report.Append("    Wrong supplies: ").Append(scoreManager.supplyMistakes);
                report.Append("    Time bonus: +").Append(timeBonus).Append(" (max ").Append(ScoreManager.MaxTimeBonus).Append(")\n\n");
            }

            foreach (PatientActor patient in patients)
            {
                string assigned = patient.HasBeenTriaged ? patient.AssignedCategory.ToString().ToUpperInvariant() : "NONE";
                string mark = patient.HasBeenTriaged && patient.LastTriageWasCorrect
                    ? "<color=#7FE0A6>correct</color>" : "<color=#FF8A8A>expected " +
                      patient.Data.correctTriage.ToString().ToUpperInvariant() + "</color>";
                report.Append(patient.Data.patientId).Append("  ").Append(patient.Data.patientName)
                    .Append(":  ").Append(assigned).Append(" (").Append(mark).Append(")  |  aid ")
                    .Append(patient.HasReceivedAid ? "done" : "not done").Append("\n");
            }

            report.Append("\n<size=85%><color=#CDD3D8><i>").Append(Disclaimer).Append("</i></color></size>");
            FinalReport = report.ToString();
            hud.SetVisible(false);
            modal.Show("FINAL SCENARIO REPORT", FinalReport, new Vector2(1000f, 640f), 100f, false);
            UiFactory.Button(modal.Panel, "RESTART", new Vector2(-150f, modal.RowY(0)), new Vector2(260f, 56f),
                RestartScenario, Palette.Slate);
            UiFactory.Button(modal.Panel, "MAIN MENU", new Vector2(150f, modal.RowY(0)), new Vector2(260f, 56f),
                ReturnToMenu);
            if (audioManager != null) audioManager.PlayObjective();
            if (scoreManager != null) scoreManager.PrintSummary();
        }

        // ------------------------------------------------------------- helpers

        private void UpdateObjectives()
        {
            objectives.Set(ObjectiveTracker.ExamineAll, patients.Count > 0 && patients.TrueForAll(p => p.HasBeenExamined));
            objectives.Set(ObjectiveTracker.TriageAll, patients.Count > 0 && patients.TrueForAll(p => p.HasBeenTriaged));
            objectives.Set(ObjectiveTracker.CompleteAid, patients.Count > 0 && patients.TrueForAll(p => p.HasReceivedAid));
            RefreshHud();
        }

        private void OnObjectiveCompleted(int index)
        {
            if (scoreManager != null) scoreManager.AwardObjective();
            if (audioManager != null) audioManager.PlayObjective();
            if (hud != null) hud.SetStatus("Objective complete! +" + ScoreManager.ObjectivePoints + " points.", 4f);
        }

        private void RefreshHud()
        {
            if (hud == null) return;
            hud.SetObjectives(objectives.Describe());
            hud.SetScore(scoreManager != null ? scoreManager.score : 0,
                gameManager != null ? gameManager.GetFormattedTime() : "--:--");
        }

        private static int Priority(TriageCategory category)
        {
            return category == TriageCategory.Red ? 0 : category == TriageCategory.Yellow ? 1 : 2;
        }

        private static string FormatTime(float seconds)
        {
            return Mathf.FloorToInt(seconds / 60f).ToString("00") + ":" + Mathf.FloorToInt(seconds % 60f).ToString("00");
        }

        private static string Rating(int score, int maximum)
        {
            float ratio = maximum > 0 ? (float)score / maximum : 0f;
            if (ratio >= 0.9f) return "Excellent prioritisation";
            if (ratio >= 0.75f) return "Strong assessment";
            if (ratio >= 0.5f) return "Developing";
            return "Keep practising";
        }

        private static Transform FindChild(Transform parent, string childName)
        {
            if (parent.name == childName) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform result = FindChild(parent.GetChild(i), childName);
                if (result != null) return result;
            }

            return null;
        }
    }
}
