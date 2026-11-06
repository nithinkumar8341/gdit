using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GoldenHour
{
    public sealed class GameplayController : MonoBehaviour
    {
        private const string MainMenuScene = "MainMenu";
        private static readonly Color Red = new Color32(194, 43, 52, 255);
        private static readonly Color Yellow = new Color32(229, 171, 49, 255);
        private static readonly Color Green = new Color32(53, 145, 88, 255);
        private static readonly Color Charcoal = new Color32(24, 32, 41, 250);
        private static readonly Color Muted = new Color32(64, 77, 89, 255);

        public GameManager gameManager;
        public ScoreManager scoreManager;
        public FirstPersonPlayer player;
        public Transform playerCamera;
        public List<PatientData> patientProfiles = new List<PatientData>();
        public List<SupplyData> supplies = new List<SupplyData>();
        public GameObject mannequinPrefab;
        public GameObject kitPrefab;
        public GameObject roadBarrierPrefab;
        public AudioClip ambience;
        public AudioClip buttonClick;
        public AudioClip kitOpen;
        public AudioClip objectiveComplete;
        public AudioClip patientInteraction;

        private readonly List<PatientActor> patients = new List<PatientActor>();
        private readonly HashSet<int> completedObjectives = new HashSet<int>();
        private Canvas canvas;
        private GoldenHourAudio audioManager;
        private TMP_Text promptText;
        private TMP_Text objectivesText;
        private TMP_Text statusText;
        private TMP_Text scoreText;
        private bool simulationStarted;
        private bool simulationFinished;
        private PatientActor inventoryPatient;
        private FirstAidKitInteractable firstAidKit;

        private void Start()
        {
            ResolveSceneReferences();
            BuildEnvironment();
            BuildPatients();
            BuildKit();
            BuildHud();
            audioManager = GetComponent<GoldenHourAudio>();
            if (audioManager == null)
            {
                audioManager = gameObject.AddComponent<GoldenHourAudio>();
                audioManager.ambience = ambience;
                audioManager.buttonClick = buttonClick;
                audioManager.kitOpen = kitOpen;
                audioManager.objectiveComplete = objectiveComplete;
                audioManager.patientInteraction = patientInteraction;
            }
            UiFactory.ButtonPressed = audioManager.PlayButton;
            player.SetControlsEnabled(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ShowBriefing();
            UpdateObjectives();
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

        private void BuildEnvironment()
        {
            GameObject environment = new GameObject("Accident Scene");
            Material ground = MakeMaterial(new Color32(96, 116, 95, 255));
            Material asphalt = MakeMaterial(new Color32(52, 58, 64, 255));
            Material concrete = MakeMaterial(new Color32(157, 161, 160, 255));
            Material paint = MakeMaterial(new Color32(239, 222, 164, 255));
            Material orange = MakeMaterial(new Color32(225, 107, 46, 255));

            CreateBox(environment.transform, "Ground", new Vector3(0f, -0.24f, 4f),
                new Vector3(70f, 0.35f, 68f), ground);
            CreateBox(environment.transform, "Road", new Vector3(0f, -0.02f, 4f),
                new Vector3(11f, 0.12f, 42f), asphalt);
            CreateBox(environment.transform, "West Sidewalk", new Vector3(-6.3f, 0.03f, 4f),
                new Vector3(1.6f, 0.2f, 42f), concrete);
            CreateBox(environment.transform, "East Sidewalk", new Vector3(6.3f, 0.03f, 4f),
                new Vector3(1.6f, 0.2f, 42f), concrete);

            for (int z = -14; z <= 22; z += 5)
            {
                CreateBox(environment.transform, "Road Centre Marking",
                    new Vector3(0f, 0.05f, z), new Vector3(0.12f, 0.025f, 2.2f), paint);
            }

            if (roadBarrierPrefab != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    GameObject barrier = Instantiate(roadBarrierPrefab,
                        new Vector3(i % 2 == 0 ? -5.5f : 5.5f, 0f, -1f + i * 5f),
                        Quaternion.identity, environment.transform);
                    barrier.name = "Road Barrier " + (i + 1);
                }
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    GameObject cone = CreatePrimitive(environment.transform, "Road Cone",
                        PrimitiveType.Cylinder, new Vector3(i % 2 == 0 ? -5.3f : 5.3f,
                            0.48f, -1f + i * 5f), new Vector3(0.38f, 0.48f, 0.38f), orange);
                }
            }

            BuildDamagedVehicle(environment.transform, asphalt, concrete, orange);
            BuildRoadside(environment.transform, ground, concrete);
            ConfigurePlayer();

            RenderSettings.ambientLight = new Color32(166, 174, 184, 255);
            RenderSettings.ambientIntensity = 1.15f;
        }

        private void BuildDamagedVehicle(Transform parent, Material dark, Material glass,
            Material accent)
        {
            Transform car = new GameObject("Damaged Vehicle").transform;
            car.SetParent(parent, false);
            car.position = new Vector3(-2.2f, 0f, 3f);
            car.rotation = Quaternion.Euler(0f, -18f, 0f);
            CreateBox(car, "Body", new Vector3(0f, 0.62f, 0f),
                new Vector3(3.2f, 0.72f, 1.6f), MakeMaterial(new Color32(113, 69, 57, 255)));
            CreateBox(car, "Roof", new Vector3(-0.2f, 1.2f, 0f),
                new Vector3(1.55f, 0.55f, 1.4f), glass);
            CreateBox(car, "Hood", new Vector3(1.05f, 0.86f, 0.08f),
                new Vector3(1f, 0.18f, 1.35f), accent);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int axle = -1; axle <= 1; axle += 2)
                {
                    GameObject wheel = CreatePrimitive(car, "Wheel", PrimitiveType.Cylinder,
                        new Vector3(axle * 1.05f, 0.32f, side * 0.83f),
                        new Vector3(0.62f, 0.22f, 0.62f), dark);
                    wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }
            }

            CreateBox(parent, "Scattered Debris", new Vector3(-4.6f, 0.16f, 5.7f),
                new Vector3(0.65f, 0.28f, 0.46f), dark).transform.rotation =
                Quaternion.Euler(0f, 28f, 13f);
        }

        private void BuildRoadside(Transform parent, Material foliage, Material building)
        {
            Material trunk = MakeMaterial(new Color32(105, 78, 57, 255));
            Material wall = MakeMaterial(new Color32(196, 198, 187, 255));
            for (int i = 0; i < 4; i++)
            {
                float z = -12f + i * 10f;
                CreateBox(parent, "Building", new Vector3(i % 2 == 0 ? -13f : 13f, 3f, z),
                    new Vector3(9f, 6f, 9f), wall);
                CreateBox(parent, "Building Roof", new Vector3(i % 2 == 0 ? -13f : 13f, 6.15f, z),
                    new Vector3(9.4f, 0.3f, 9.4f), building);
                CreateBox(parent, "Window", new Vector3(i % 2 == 0 ? -13f : 13f, 3.2f, z + 4.56f),
                    new Vector3(2f, 1.7f, 0.08f), MakeMaterial(new Color32(123, 164, 185, 255)));
            }

            for (int i = 0; i < 5; i++)
            {
                float x = i % 2 == 0 ? -8f : 8f;
                float z = -10f + i * 8f;
                CreatePrimitive(parent, "Tree Trunk", PrimitiveType.Cylinder,
                    new Vector3(x, 1.35f, z), new Vector3(0.28f, 1.35f, 0.28f), trunk);
                CreatePrimitive(parent, "Tree Crown", PrimitiveType.Sphere,
                    new Vector3(x, 3.2f, z), new Vector3(2f, 2.25f, 2f), foliage);
            }
        }

        private void ConfigurePlayer()
        {
            if (player == null)
            {
                Debug.LogError("Golden Hour scene requires the existing Player object.");
                return;
            }

            player.transform.SetPositionAndRotation(new Vector3(0f, 0.15f, -14f),
                Quaternion.identity);
            if (playerCamera == null)
            {
                Camera foundCamera = player.GetComponentInChildren<Camera>();
                if (foundCamera != null) playerCamera = foundCamera.transform;
            }

            if (playerCamera != null)
            {
                playerCamera.localPosition = new Vector3(0f, 1.52f, 0f);
                playerCamera.localRotation = Quaternion.identity;
                playerCamera.gameObject.tag = "MainCamera";
                player.Configure(playerCamera);
            }
        }

        private void BuildPatients()
        {
            if (patientProfiles == null || patientProfiles.Count != 5)
            {
                Debug.LogError("Five PatientData assets must be assigned in the scene.");
                return;
            }

            Vector3[] positions =
            {
                new Vector3(-2.8f, 0f, -3f),
                new Vector3(2.8f, 0f, 0f),
                new Vector3(-2.7f, 0f, 4f),
                new Vector3(2.8f, 0f, 8f),
                new Vector3(-2.5f, 0f, 12f)
            };
            Transform group = new GameObject("Patients").transform;

            for (int i = 0; i < patientProfiles.Count; i++)
            {
                GameObject patient = new GameObject(patientProfiles[i].patientId);
                patient.transform.SetParent(group, false);
                patient.transform.position = positions[i];
                patient.transform.rotation = Quaternion.Euler(0f, (i % 2 == 0 ? -15f : 15f), 0f);
                PatientActor actor = patient.AddComponent<PatientActor>();
                actor.Initialize(patientProfiles[i], mannequinPrefab);
                patients.Add(actor);
            }
        }

        private void BuildKit()
        {
            GameObject kit = new GameObject("First Aid Kit");
            kit.transform.position = new Vector3(4.1f, 0f, 13.1f);
            kit.transform.rotation = Quaternion.Euler(0f, -20f, 0f);
            BoxCollider collider = kit.AddComponent<BoxCollider>();
            collider.size = new Vector3(1.4f, 0.72f, 1.1f);
            collider.center = new Vector3(0f, 0.38f, 0f);

            Transform lid;
            if (kitPrefab != null)
            {
                GameObject model = Instantiate(kitPrefab, kit.transform);
                model.name = "Kit Model";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;
                lid = FindChild(model.transform, "KitHinge") ?? FindChild(model.transform, "Lid");
            }
            else
            {
                lid = BuildFallbackKit(kit.transform);
            }

            firstAidKit = kit.AddComponent<FirstAidKitInteractable>();
            firstAidKit.Initialize(lid);
        }

        private Transform BuildFallbackKit(Transform parent)
        {
            Material red = MakeMaterial(new Color32(194, 43, 52, 255));
            Material white = MakeMaterial(Color.white);
            Material steel = MakeMaterial(new Color32(122, 132, 140, 255), 0.65f);
            CreateBox(parent, "Kit Base", new Vector3(0f, 0.27f, 0f),
                new Vector3(1.25f, 0.5f, 0.92f), red);
            Transform hinge = new GameObject("KitHinge").transform;
            hinge.SetParent(parent, false);
            hinge.localPosition = new Vector3(0f, 0.51f, -0.45f);
            CreateBox(hinge, "Lid", new Vector3(0f, 0.08f, 0.46f),
                new Vector3(1.25f, 0.16f, 0.92f), red);
            CreateBox(hinge, "Cross Vertical", new Vector3(0f, 0.17f, 0.46f),
                new Vector3(0.22f, 0.035f, 0.58f), white);
            CreateBox(hinge, "Cross Horizontal", new Vector3(0f, 0.17f, 0.46f),
                new Vector3(0.55f, 0.035f, 0.2f), white);
            for (int side = -1; side <= 1; side += 2)
            {
                CreateBox(parent, "Kit Latch", new Vector3(side * 0.42f, 0.5f, 0.48f),
                    new Vector3(0.13f, 0.22f, 0.09f), steel);
            }

            for (int i = 0; i < 3; i++)
            {
                CreateBox(parent, "Visible Supply", new Vector3(-0.32f + i * 0.32f, 0.6f, 0.02f),
                    new Vector3(0.24f, 0.1f, 0.28f),
                    MakeMaterial(i == 0 ? new Color32(229, 171, 49, 255) :
                        i == 1 ? new Color32(235, 231, 217, 255) :
                        new Color32(78, 143, 111, 255)));
            }

            return hinge;
        }

        private void BuildHud()
        {
            canvas = UiFactory.CreateCanvas("Golden Hour Gameplay UI");
            GameObject hud = UiFactory.Panel(canvas.transform, "HUD",
                Vector2.zero, Vector2.zero, Color.clear, true);
            hud.GetComponent<Image>().raycastTarget = false;
            objectivesText = UiFactory.Text(hud.transform, string.Empty, 18,
                new Vector2(410f, 244f), new Vector2(-560f, 300f), TextAlignmentOptions.TopLeft);
            SetRectAnchor(objectivesText.rectTransform, new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(24f, -24f));
            scoreText = UiFactory.Text(hud.transform, string.Empty, 20,
                new Vector2(320f, 70f), new Vector2(0f, 0f), TextAlignmentOptions.TopRight);
            SetRectAnchor(scoreText.rectTransform, new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-24f, -24f));
            promptText = UiFactory.Text(hud.transform, string.Empty, 22,
                new Vector2(700f, 54f), new Vector2(0f, 82f), TextAlignmentOptions.Center,
                Color.white);
            SetRectAnchor(promptText.rectTransform, new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 82f));
            UiFactory.Text(hud.transform, "WASD MOVE    MOUSE LOOK    E INTERACT    ESC PAUSE",
                16, new Vector2(650f, 38f), Vector2.zero, TextAlignmentOptions.Center,
                new Color32(225, 229, 232, 255));
            TMP_Text controls = hud.transform.GetChild(hud.transform.childCount - 1)
                .GetComponent<TMP_Text>();
            SetRectAnchor(controls.rectTransform, new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 26f));
            statusText = UiFactory.Text(hud.transform, string.Empty, 17,
                new Vector2(850f, 44f), Vector2.zero, TextAlignmentOptions.Center,
                new Color32(246, 209, 113, 255));
            SetRectAnchor(statusText.rectTransform, new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 126f));
            GameObject crosshair = UiFactory.Panel(hud.transform, "Crosshair",
                new Vector2(8f, 8f), Vector2.zero, new Color32(255, 255, 255, 225));
            crosshair.GetComponent<Image>().raycastTarget = false;
            UpdateScoreDisplay();
        }

        private void Update()
        {
            if (!simulationStarted || simulationFinished || gameManager == null)
            {
                return;
            }

            UpdatePrompt();

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (IsModalOpen())
                {
                    ResumeSimulation();
                    return;
                }
                else
                {
                    ShowPauseMenu();
                    return;
                }
            }

            if (IsModalOpen())
            {
                UpdateScoreDisplay();
                return;
            }

            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                TryInteract();
            }

            UpdateScoreDisplay();
            if (!gameManager.scenarioRunning && !gameManager.scenarioPaused)
            {
                FinishScenario();
            }
        }

        private void ShowBriefing()
        {
            ShowModal("SCENE BRIEFING",
                "A road accident has occurred. Several people are injured. Assess the victims, prioritise their conditions and provide appropriate basic first aid.\n\n" +
                "Your objectives: reach the scene, examine all five people, assign triage priorities, choose suitable supplies and complete the simulated aid steps.\n\n" +
                "Controls: WASD to move, mouse to look, E to interact, Esc to pause.\n\n" +
                "This is an educational simulation, not medical training or advice.",
                new Vector2(880f, 620f));
            UiFactory.Button(CurrentModal(), "BEGIN SCENARIO",
                new Vector2(0f, -220f), new Vector2(290f, 58f), BeginSimulation);
        }

        private void BeginSimulation()
        {
            simulationStarted = true;
            CloseModal();
            if (gameManager != null) gameManager.StartScenario();
            if (player != null) player.SetControlsEnabled(true);
            CompleteObjective(0);
            UpdateObjectives();
        }

        private void TryInteract()
        {
            if (playerCamera == null || player == null || !player.enabled)
            {
                return;
            }

            Ray ray = new Ray(playerCamera.position, playerCamera.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, 3.8f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                SetStatus("Nothing within reach. Look at a person or the first-aid kit.");
                return;
            }

            PatientActor patient = hit.collider.GetComponentInParent<PatientActor>();
            if (patient != null)
            {
                if (audioManager != null) audioManager.PlayPatient();
                OpenExamination(patient);
                return;
            }

            FirstAidKitInteractable kit = hit.collider.GetComponentInParent<FirstAidKitInteractable>();
            if (kit != null)
            {
                if (patients.Exists(item => !item.HasBeenTriaged))
                {
                    SetStatus("Examine and assign a triage priority to each person first.");
                    return;
                }

                kit.Open();
                if (audioManager != null) audioManager.PlayKitOpen();
                OpenInventory();
                return;
            }

            SetStatus("Look directly at a person or the first-aid kit and press E.");
        }

        private void OpenExamination(PatientActor patient)
        {
            if (patient.MarkExamined() && scoreManager != null)
            {
                scoreManager.MarkExamined();
            }
            UpdateObjectives();
            ShowModal("PATIENT EXAMINATION",
                "PATIENT " + patient.Data.patientId + "  |  " + patient.Data.patientName +
                "  |  Age " + patient.Data.age + "\n\n" +
                "Visible injury: " + patient.Data.injuryDescription + "\n\n" +
                "Reported symptoms: " + patient.Data.symptoms + "\n\n" +
                "Responsiveness: " + patient.Data.responsiveness + "\n" +
                "Breathing: " + (patient.Data.breathing ? "Present" : "Not observed") + "\n" +
                "Visible severe bleeding: " + (patient.Data.severeBleeding ? "Yes" : "No") + "\n" +
                "Can walk: " + (patient.Data.canWalk ? "Yes" : "No") +
                "\n\nChoose a triage category. Your choice is not revealed until after you decide.",
                new Vector2(880f, 700f));

            Transform panel = CurrentModal();
            UiFactory.Button(panel, "RED  |  IMMEDIATE", new Vector2(-270f, -220f),
                new Vector2(250f, 58f), () => AssignTriage(patient, TriageCategory.Red), Red);
            UiFactory.Button(panel, "YELLOW  |  DELAYED", new Vector2(0f, -220f),
                new Vector2(250f, 58f), () => AssignTriage(patient, TriageCategory.Yellow), Yellow);
            UiFactory.Button(panel, "GREEN  |  MINOR", new Vector2(270f, -220f),
                new Vector2(250f, 58f), () => AssignTriage(patient, TriageCategory.Green), Green);
            UiFactory.Button(panel, "CLOSE", new Vector2(0f, -292f),
                new Vector2(180f, 44f), ResumeSimulation, Muted);
        }

        private void AssignTriage(PatientActor patient, TriageCategory category)
        {
            bool previouslyTriaged = patient.HasBeenTriaged;
            bool previousCorrect = patient.LastTriageWasCorrect;
            patient.SetTriage(category);
            if (scoreManager != null)
            {
                scoreManager.SetTriageResult(previouslyTriaged, previousCorrect,
                    patient.LastTriageWasCorrect);
            }

            StringBuilder result = new StringBuilder();
            result.Append("Assigned triage: ").Append(CategoryLabel(category)).Append("\n\n");
            result.Append(patient.LastTriageWasCorrect
                ? "This matches the simplified scenario key."
                : "This does not match the simplified scenario key.");
            result.Append("\n\nReasoning: ").Append(patient.Data.reasoning);
            result.Append("\n\nYou may reassess this person at any time.");
            ShowModal("TRIAGE FEEDBACK", result.ToString(), new Vector2(760f, 500f));
            Transform panel = CurrentModal();
            UiFactory.Button(panel, "REASSESS", new Vector2(-130f, -180f),
                new Vector2(220f, 52f), () => OpenExamination(patient), Muted);
            UiFactory.Button(panel, "CONTINUE", new Vector2(130f, -180f),
                new Vector2(220f, 52f), ResumeSimulation);
            UpdateObjectives();
        }

        private void OpenInventory()
        {
            List<PatientActor> available = patients.FindAll(item =>
                item.HasBeenTriaged && !item.HasReceivedAid);
            ShowModal("FIRST-AID KIT  |  EDUCATIONAL SIMULATION",
                "Choose a triaged person, then select a basic supply for the described scenario action.\n" +
                "A mismatched item receives feedback but no aid credit. These simplified actions are not medical instructions.",
                new Vector2(940f, 660f));
            Transform panel = CurrentModal();

            if (available.Count == 0)
            {
                UiFactory.Text(panel, "All simulated aid steps are complete.",
                    20, new Vector2(760f, 80f), new Vector2(0f, -30f),
                    TextAlignmentOptions.Center);
            }
            else
            {
                float y = 110f;
                foreach (PatientActor actor in available)
                {
                    PatientActor current = actor;
                    UiFactory.Button(panel,
                        current.Data.patientId + "  |  " + current.Data.patientName +
                        "  |  " + CategoryLabel(current.AssignedCategory),
                        new Vector2(0f, y), new Vector2(690f, 52f),
                        () => ShowSupplyChoices(current), Muted);
                    y -= 62f;
                }
            }

            UiFactory.Button(panel, "CLOSE KIT", new Vector2(0f, -282f),
                new Vector2(220f, 48f), ResumeSimulation, Muted);
        }

        private void ShowSupplyChoices(PatientActor patient)
        {
            inventoryPatient = patient;
            ShowModal("SELECT A SUPPLY  |  " + patient.Data.patientId,
                "Scenario note: " + patient.Data.aidAction +
                "\n\nSelect one item. Correct actions are educational feedback only.",
                new Vector2(940f, 660f));
            Transform panel = CurrentModal();
            for (int i = 0; i < supplies.Count; i++)
            {
                SupplyData supply = supplies[i];
                float x = i % 2 == 0 ? -220f : 220f;
                float y = 100f - (i / 2) * 112f;
                string text = supply.displayName + "\n" + supply.educationalPurpose;
                UiFactory.Button(panel, text, new Vector2(x, y), new Vector2(390f, 88f),
                    () => ApplySupply(patient, supply), supply.displayColor);
            }

            UiFactory.Button(panel, "BACK TO PATIENTS", new Vector2(0f, -282f),
                new Vector2(260f, 48f), OpenInventory, Muted);
        }

        private void ApplySupply(PatientActor patient, SupplyData supply)
        {
            if (patient.Data.recommendedSupply != supply)
            {
                SetStatus("Not the scenario's recommended item. No aid credit awarded; you may try again.");
                ShowSupplyChoices(patient);
                UiFactory.Text(CurrentModal(),
                    "That item does not match this scenario action. No reward was awarded.",
                    18, new Vector2(760f, 42f), new Vector2(0f, -235f),
                    TextAlignmentOptions.Center, Yellow);
                return;
            }

            patient.MarkAidCompleted();
            if (scoreManager != null) scoreManager.MarkTreatmentCompleted();
            ShowModal("SIMULATED AID COMPLETE",
                "Selected item: " + supply.displayName + "\n\n" +
                "Scenario action: " + patient.Data.aidAction +
                "\n\nThis simplified activity is not a real-world treatment instruction.",
                new Vector2(760f, 470f));
            UiFactory.Button(CurrentModal(), "CONTINUE",
                new Vector2(0f, -165f), new Vector2(240f, 52f), ResumeSimulation);
            UpdateObjectives();
        }

        private void ShowPauseMenu()
        {
            if (gameManager != null) gameManager.PauseScenario();
            if (player != null) player.SetControlsEnabled(false);
            ShowModal("PAUSED", "The scenario timer is paused.", new Vector2(570f, 440f));
            Transform panel = CurrentModal();
            UiFactory.Button(panel, "RESUME", new Vector2(0f, 85f),
                new Vector2(280f, 56f), ResumeSimulation);
            UiFactory.Button(panel, "RESTART", new Vector2(0f, 5f),
                new Vector2(280f, 56f), RestartScenario, Muted);
            UiFactory.Button(panel, "MAIN MENU", new Vector2(0f, -75f),
                new Vector2(280f, 56f), ReturnToMenu, Muted);
        }

        private void ResumeSimulation()
        {
            CloseModal();
            if (gameManager != null) gameManager.ResumeScenario();
            if (player != null) player.SetControlsEnabled(true);
            UpdateObjectives();
        }

        private void RestartScenario()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void ReturnToMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(MainMenuScene);
        }

        private void FinishScenario()
        {
            if (simulationFinished) return;
            simulationFinished = true;
            if (player != null) player.SetControlsEnabled(false);
            if (gameManager != null) gameManager.EndScenario();
            int timeBonus = scoreManager != null
                ? scoreManager.AddTimeBonus(gameManager != null ? gameManager.GetRemainingTime() : 0f)
                : 0;
            ShowModal("FINAL SCENARIO REPORT",
                "TOTAL SCORE  " + (scoreManager != null ? scoreManager.score : 0) +
                "  (includes +" + timeBonus + " time bonus)\n\n" +
                "People examined: " + (scoreManager != null ? scoreManager.examinedPatients : 0) +
                " / " + patients.Count + "\n" +
                "Correct triage: " + (scoreManager != null ? scoreManager.correctClassifications : 0) +
                "   |   Incorrect: " + (scoreManager != null ? scoreManager.incorrectClassifications : 0) +
                "\nSimulated aid steps: " + (scoreManager != null ? scoreManager.completedTreatments : 0) +
                " / " + patients.Count + "\nObjectives completed: " +
                (scoreManager != null ? scoreManager.completedObjectives : 0) + " / 5\n\n" +
                "Overall rating: " + GetRating(scoreManager != null ? scoreManager.score : 0) +
                "\n\nThis is an educational simulation, not medical training or advice. It is not a substitute for emergency services or qualified professional guidance.",
                new Vector2(900f, 700f));
            Transform panel = CurrentModal();
            UiFactory.Button(panel, "RESTART", new Vector2(-140f, -300f),
                new Vector2(220f, 54f), RestartScenario, Muted);
            UiFactory.Button(panel, "MAIN MENU", new Vector2(140f, -300f),
                new Vector2(220f, 54f), ReturnToMenu);
        }

        private void UpdateObjectives()
        {
            bool allExamined = patients.Count > 0 && patients.TrueForAll(item => item.HasBeenExamined);
            bool allTriaged = patients.Count > 0 && patients.TrueForAll(item => item.HasBeenTriaged);
            bool allTreated = patients.Count > 0 && patients.TrueForAll(item => item.HasReceivedAid);
            SetObjective(1, allExamined);
            SetObjective(2, allTriaged);
            SetObjective(3, allTreated);
            SetObjective(4, allTreated);

            if (objectivesText != null)
            {
                objectivesText.text =
                    "<b>SCENE OBJECTIVES</b>\n" +
                    ObjectiveLine(0, "Reach the accident scene") + "\n" +
                    ObjectiveLine(1, "Examine every person") + "\n" +
                    ObjectiveLine(2, "Assign triage to everyone") + "\n" +
                    ObjectiveLine(3, "Select suitable supplies") + "\n" +
                    ObjectiveLine(4, "Complete simulated aid");
            }

            if (allTreated && simulationStarted)
            {
                FinishScenario();
            }

            UpdateScoreDisplay();
        }

        private void SetObjective(int index, bool complete)
        {
            if (complete) CompleteObjective(index);
        }

        private void CompleteObjective(int index)
        {
            if (completedObjectives.Add(index))
            {
                if (scoreManager != null) scoreManager.AwardObjective();
                if (audioManager != null) audioManager.PlayObjective();
            }
        }

        private string ObjectiveLine(int index, string text)
        {
            return (completedObjectives.Contains(index) ? "[x] " : "[ ] ") + text;
        }

        private void UpdateScoreDisplay()
        {
            if (scoreText == null) return;
            scoreText.text = "SCORE  " + (scoreManager != null ? scoreManager.score : 0) +
                             "\nTIME  " + (gameManager != null ? gameManager.GetFormattedTime() : "--:--");
        }

        private void UpdatePrompt()
        {
            if (promptText == null || playerCamera == null || IsModalOpen())
            {
                if (promptText != null) promptText.text = string.Empty;
                return;
            }

            Ray ray = new Ray(playerCamera.position, playerCamera.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, 3.8f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                PatientActor patient = hit.collider.GetComponentInParent<PatientActor>();
                if (patient != null)
                {
                    promptText.text = "Press E to examine " + patient.Data.patientName;
                    return;
                }

                if (hit.collider.GetComponentInParent<FirstAidKitInteractable>() != null)
                {
                    promptText.text = "Press E to open the first-aid kit";
                    return;
                }
            }

            promptText.text = string.Empty;
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
            Debug.Log(message);
        }

        private void ShowModal(string title, string body, Vector2 size)
        {
            if (player != null) player.SetControlsEnabled(false);
            if (gameManager != null && simulationStarted && !simulationFinished)
            {
                gameManager.PauseScenario();
            }

            CloseModal();
            GameObject backdrop = UiFactory.Panel(canvas.transform, "Modal Backdrop",
                Vector2.zero, Vector2.zero, new Color32(5, 9, 14, 175), true);
            backdrop.transform.SetAsLastSibling();
            GameObject panel = UiFactory.Panel(backdrop.transform, "Modal Panel",
                size, Vector2.zero, Charcoal);
            UiFactory.Text(panel.transform, title, 30, new Vector2(size.x - 70f, 60f),
                new Vector2(0f, size.y * 0.5f - 54f), TextAlignmentOptions.Center,
                new Color32(255, 255, 255, 255)).fontStyle = FontStyles.Bold;
            TMP_Text bodyText = UiFactory.Text(panel.transform, body, 19,
                new Vector2(size.x - 96f, size.y - 320f),
                new Vector2(0f, 0f), TextAlignmentOptions.TopLeft);
            bodyText.margin = new Vector4(12f, 5f, 12f, 5f);
            if (canvas.transform.childCount > 0)
            {
                canvas.transform.SetAsLastSibling();
            }

        }

        private Transform CurrentModal()
        {
            Transform backdrop = canvas.transform.Find("Modal Backdrop");
            return backdrop != null ? backdrop.Find("Modal Panel") : canvas.transform;
        }

        private bool IsModalOpen()
        {
            return canvas != null && canvas.transform.Find("Modal Backdrop") != null;
        }

        private void CloseModal()
        {
            if (canvas == null) return;
            Transform existing = canvas.transform.Find("Modal Backdrop");
            if (existing != null) Destroy(existing.gameObject);
        }

        private static void SetRectAnchor(RectTransform rect, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 position)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(anchorMin.x == 1f ? 1f : 0f,
                anchorMin.y == 1f ? 1f : 0f);
            rect.anchoredPosition = position;
        }

        private static string CategoryLabel(TriageCategory category)
        {
            switch (category)
            {
                case TriageCategory.Red: return "RED | IMMEDIATE";
                case TriageCategory.Yellow: return "YELLOW | DELAYED";
                case TriageCategory.Green: return "GREEN | MINOR";
                default: return category.ToString().ToUpperInvariant();
            }
        }

        private static string GetRating(int value)
        {
            if (value >= 650) return "Excellent prioritisation";
            if (value >= 400) return "Strong assessment";
            if (value >= 200) return "Developing";
            return "Keep practicing the scenario";
        }

        private static Material MakeMaterial(Color color, float metallic = 0f)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material material = new Material(shader);
            material.color = color;
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            return material;
        }

        private static GameObject CreateBox(Transform parent, string name, Vector3 position,
            Vector3 scale, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }

        private static GameObject CreatePrimitive(Transform parent, string name,
            PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            GameObject item = GameObject.CreatePrimitive(type);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = material;
            return item;
        }

        private static Transform FindChild(Transform parent, string childName)
        {
            if (parent.name.Contains(childName)) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform result = FindChild(parent.GetChild(i), childName);
                if (result != null) return result;
            }

            return null;
        }
    }
}
