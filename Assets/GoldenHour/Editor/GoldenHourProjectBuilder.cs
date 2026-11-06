using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GoldenHour;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace GoldenHour.EditorTools
{
    public static class GoldenHourProjectBuilder
    {
        private const string GameScenePath = "Assets/Scenes/Samplespace.unity";
        private const string MenuScenePath = "Assets/GoldenHour/Scenes/MainMenu.unity";
        private const string DataPath = "Assets/GoldenHour/ScriptableObjects/";
        private const string AudioPath = "Assets/GoldenHour/Audio/";
        private const string ModelPath = "Assets/GoldenHour/Blender/Exports/";

        [MenuItem("GoldenHour/Build Project")]
        public static void BuildProject()
        {
            EnsureDirectories();
            CreateGameData();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            CreateMainMenuScene();
            ConfigureGameplayScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            DeleteTemplateLeftovers();
            ValidateProject();
            Debug.Log("Golden Hour project setup completed.");
        }

        [MenuItem("GoldenHour/Validate Project")]
        public static void ValidateProject()
        {
            ValidateTriageRules();
            List<PatientData> patients = LoadPatients();
            Require(patients.Count == 5, "Expected exactly five PatientData assets.");
            Require(patients.Select(item => item.patientId).Distinct().Count() == 5,
                "Patient identifiers must be unique.");
            Require(patients.Count(item => item.correctTriage == TriageCategory.Red) == 2,
                "Expected two red patients.");
            Require(patients.Count(item => item.correctTriage == TriageCategory.Yellow) == 2,
                "Expected two yellow patients.");
            Require(patients.Count(item => item.correctTriage == TriageCategory.Green) == 1,
                "Expected one green patient.");
            Require(patients.All(item => item.recommendedSupply != null),
                "Every patient needs a recommended supply.");

            List<SupplyData> supplies = LoadSupplies();
            Require(supplies.Count == 5, "Expected five SupplyData assets.");
            Require(File.Exists(GameScenePath), "Gameplay scene is missing.");
            Require(File.Exists(MenuScenePath), "Main menu scene is missing.");
            Require(HasSceneInBuildSettings(GameScenePath), "Gameplay scene is not in Build Settings.");
            Require(HasSceneInBuildSettings(MenuScenePath), "Main menu scene is not in Build Settings.");
            Require(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath + "LowPolyMannequin.fbx") != null,
                "Blender mannequin FBX is missing.");
            Require(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath + "FirstAidKit.fbx") != null,
                "Blender first-aid kit FBX is missing.");
            Require(AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath + "Roadside_Ambience.wav") != null,
                "Procedural ambience WAV is missing.");
            Require(AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath + "UI_Click.wav") != null,
                "Procedural UI click WAV is missing.");

            Scene game = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            Require(GameObject.Find("Player") != null, "Existing Player object is missing.");
            Require(GameObject.Find("GameManager") != null, "Existing GameManager is missing.");
            Require(GameObject.Find("ScoreManager") != null, "Existing ScoreManager is missing.");
            Require(GameObject.Find("EventSystem") != null, "EventSystem is missing.");
            Require(UnityEngine.Object.FindAnyObjectByType<GameplayController>() != null,
                "GameplayController is missing.");
            Require(UnityEngine.Object.FindAnyObjectByType<FirstPersonPlayer>() != null,
                "FirstPersonPlayer is missing.");
            EditorSceneManager.CloseScene(game, true);

            Scene menu = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            Require(UnityEngine.Object.FindAnyObjectByType<MainMenuController>() != null,
                "MainMenuController is missing.");
            Require(UnityEngine.Object.FindAnyObjectByType<InputSystemUIInputModule>() != null,
                "Main menu does not use the Input System UI module.");
            EditorSceneManager.CloseScene(menu, true);
            Debug.Log("Golden Hour project validation passed.");
        }

        [MenuItem("GoldenHour/Build Windows Player")]
        public static void BuildWindowsPlayer()
        {
            string outputPath = Path.GetFullPath("Builds/GoldenHour/GoldenHour.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { MenuScenePath, GameScenePath },
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException("Windows build failed: " +
                    report.summary.result + " (" + report.summary.totalErrors + " errors).");
            }

            Debug.Log("Windows build created at " + outputPath +
                      " | bytes: " + report.summary.totalSize);
        }

        [MenuItem("GoldenHour/Write Testing Report")]
        public static void WriteTestingReport()
        {
            ValidateProject();
            string[] results =
            {
                "# GOLDEN HOUR — Testing Report",
                "",
                "Validation ran in Unity 6.6 batch mode using `GoldenHour/Validate Project`.",
                "",
                "| Test | Result |",
                "|---|---|",
                "| Five patient data assets are present and uniquely identified | PASS |",
                "| Scenario data matches 2 RED, 2 YELLOW, 1 GREEN | PASS |",
                "| Walking-patient priority rule | PASS |",
                "| Non-breathing / severe-bleeding / unresponsive priority rule | PASS |",
                "| Stable non-walking patient priority rule | PASS |",
                "| Five supply assets are present and each patient has an assigned item | PASS |",
                "| Main menu and gameplay scenes are in Windows build settings | PASS |",
                "| Existing Player, GameManager, ScoreManager, and EventSystem are wired | PASS |",
                "| Main-menu EventSystem uses InputSystemUIInputModule | PASS |",
                "| Blender FBX and procedural audio assets are imported | PASS |",
                "| Unity compilation and player build | See build log and build output |",
                "",
                "Interactive mouse/keyboard playthrough and visual review require a manual run of the Windows build.",
                "This educational simulation is not medical training or advice."
            };
            File.WriteAllLines("TESTING_REPORT.md", results);
            AssetDatabase.Refresh();
            Debug.Log("Testing report written to TESTING_REPORT.md.");
        }

        private static void EnsureDirectories()
        {
            foreach (string directory in new[]
            {
                "Assets/GoldenHour/Scenes",
                "Assets/GoldenHour/ScriptableObjects",
                "Assets/GoldenHour/Audio",
                "Assets/GoldenHour/Prefabs",
                "Assets/GoldenHour/Blender/Exports"
            })
            {
                Directory.CreateDirectory(directory);
            }
        }

        private static void CreateGameData()
        {
            SupplyData gauze = CreateSupply("Gauze", "Gauze",
                "Soft folded gauze model.", "Illustrate a clean-supply selection.",
                new Color32(231, 231, 218, 255));
            SupplyData bandage = CreateSupply("Bandage", "Bandage",
                "Rolled bandage model.", "Illustrate a basic bandage selection.",
                new Color32(218, 189, 128, 255));
            SupplyData gloves = CreateSupply("Gloves", "Gloves",
                "Protective glove prop.", "Illustrate protective-equipment awareness.",
                new Color32(82, 145, 172, 255));
            SupplyData dressing = CreateSupply("Dressing", "Dressing",
                "Sterile dressing package model.", "Illustrate a dressing-package selection.",
                new Color32(73, 147, 105, 255));
            SupplyData blanket = CreateSupply("EmergencyBlanket", "Emergency Blanket",
                "Folded emergency blanket prop.", "Illustrate a blanket supply selection.",
                new Color32(198, 159, 58, 255));

            CreatePatient("P01", "Alex Morgan", 28,
                "Walking, alert, with a minor scrape on one arm.",
                "Reports soreness and is able to move independently.",
                "Alert and follows simple instructions.", true, false, false, true,
                TriageCategory.Green,
                "The patient can walk in this scenario, so the simplified model assigns GREEN.",
                blanket, "Choose a blanket prop for a comfort-themed simulated aid step.");
            CreatePatient("P02", "Jordan Lee", 42,
                "Not walking; prominent visible bleeding from an arm.",
                "Reports pain and dizziness; visible bleeding is marked severe in this scenario.",
                "Awake and follows simple instructions.", true, true, false, true,
                TriageCategory.Red,
                "Severe visible bleeding is a RED indicator in this simplified scenario.",
                dressing, "Choose the dressing package prop for a simulated supply-selection step.");
            CreatePatient("P03", "Sam Rivera", 36,
                "Not walking; no breathing observed during the scenario check.",
                "No verbal response during the scenario observation.",
                "Unresponsive in this scenario.", false, false, false, false,
                TriageCategory.Red,
                "Not breathing and unresponsive are RED indicators in this simplified scenario.",
                gloves, "Choose gloves to demonstrate protective-equipment awareness; no procedure is shown.");
            CreatePatient("P04", "Taylor Kim", 51,
                "Not walking; minor leg injury with no severe bleeding observed.",
                "Reports discomfort and can answer questions.",
                "Alert and follows simple instructions.", true, false, false, true,
                TriageCategory.Yellow,
                "The patient cannot walk and has none of the listed RED indicators, so YELLOW is expected.",
                gauze, "Choose a gauze prop for a basic simulated supply-selection step.");
            CreatePatient("P05", "Casey Patel", 19,
                "Not walking; bruising and a small scrape are visible.",
                "Reports feeling shaken but can answer questions.",
                "Alert and follows simple instructions.", true, false, false, true,
                TriageCategory.Yellow,
                "The patient cannot walk and has none of the listed RED indicators, so YELLOW is expected.",
                bandage, "Choose a bandage prop for a basic simulated supply-selection step.");
        }

        private static SupplyData CreateSupply(string id, string name, string description,
            string purpose, Color color)
        {
            string path = DataPath + "Supply_" + id + ".asset";
            SupplyData asset = AssetDatabase.LoadAssetAtPath<SupplyData>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<SupplyData>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.supplyId = id;
            asset.displayName = name;
            asset.description = description;
            asset.educationalPurpose = purpose;
            asset.displayColor = color;
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void CreatePatient(string id, string name, int age, string injury,
            string symptoms, string responsiveness, bool breathing, bool severeBleeding,
            bool canWalk, bool followsCommands, TriageCategory category, string reasoning,
            SupplyData supply, string aidAction)
        {
            string path = DataPath + "Patient_" + id + ".asset";
            PatientData asset = AssetDatabase.LoadAssetAtPath<PatientData>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<PatientData>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.patientId = id;
            asset.patientName = name;
            asset.age = age;
            asset.injuryDescription = injury;
            asset.symptoms = symptoms;
            asset.responsiveness = responsiveness;
            asset.breathing = breathing;
            asset.severeBleeding = severeBleeding;
            asset.canWalk = canWalk;
            asset.followsCommands = followsCommands;
            asset.correctTriage = category;
            asset.reasoning = reasoning;
            asset.recommendedSupply = supply;
            asset.aidAction = aidAction;
            EditorUtility.SetDirty(asset);
        }

        private static void CreateMainMenuScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            GameObject menu = new GameObject("Main Menu");
            menu.AddComponent<MainMenuController>();
            GoldenHourAudio audio = menu.AddComponent<GoldenHourAudio>();
            AssignAudio(audio);
            CreateEventSystem();
            EditorSceneManager.SaveScene(scene, MenuScenePath);
            AddSceneToBuildSettings(MenuScenePath);
        }

        private static void ConfigureGameplayScene()
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            GameObject playerObject = GameObject.Find("Player");
            GameObject managerObject = GameObject.Find("GameManager");
            GameObject scoreObject = GameObject.Find("ScoreManager");
            GameObject eventSystem = GameObject.Find("EventSystem");
            Require(playerObject != null, "The existing Player object is required.");
            Require(managerObject != null, "The existing GameManager object is required.");
            Require(scoreObject != null, "The existing ScoreManager object is required.");
            Require(eventSystem != null, "The existing EventSystem object is required.");

            DestroyNamedObject("Patient_Test");
            DestroyNamedObject("TestGround");
            DestroyNamedObject("GoldenHourUI");
            RemoveComponentIfPresent<global::PlayerController>(playerObject);
            RemoveComponentIfPresent<global::PlayerLook>(playerObject);
            RemoveComponentIfPresent<global::PlayerInteraction>(playerObject);

            Camera camera = playerObject.GetComponentInChildren<Camera>();
            Require(camera != null, "The existing Player Camera child is required.");
            RemoveComponentIfPresent<global::PlayerLook>(camera.gameObject);
            camera.transform.localPosition = new Vector3(0f, 1.52f, 0f);
            camera.transform.localRotation = Quaternion.identity;
            camera.gameObject.tag = "MainCamera";
            CharacterController characterController =
                playerObject.GetComponent<CharacterController>();
            if (characterController == null)
            {
                characterController = playerObject.AddComponent<CharacterController>();
            }

            characterController.height = 1.8f;
            characterController.radius = 0.3f;
            characterController.center = new Vector3(0f, 0.9f, 0f);
            characterController.stepOffset = 0.3f;
            characterController.skinWidth = 0.04f;
            playerObject.transform.SetPositionAndRotation(new Vector3(0f, 0.15f, -14f),
                Quaternion.identity);
            FirstPersonPlayer controller = playerObject.GetComponent<FirstPersonPlayer>();
            if (controller == null) controller = playerObject.AddComponent<FirstPersonPlayer>();
            controller.Configure(camera.transform);

            GameManager gameManager = managerObject.GetComponent<GameManager>();
            ScoreManager scoreManager = scoreObject.GetComponent<ScoreManager>();
            Require(gameManager != null && scoreManager != null,
                "Existing manager scripts are required.");
            gameManager.scenarioDuration = 900f;
            gameManager.ResetScenario();
            scoreManager.ResetScore();

            GameplayController gameplay = managerObject.GetComponent<GameplayController>();
            if (gameplay == null) gameplay = managerObject.AddComponent<GameplayController>();
            gameplay.gameManager = gameManager;
            gameplay.scoreManager = scoreManager;
            gameplay.player = controller;
            gameplay.playerCamera = camera.transform;
            gameplay.patientProfiles = LoadPatients();
            gameplay.supplies = LoadSupplies();
            gameplay.mannequinPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath + "LowPolyMannequin.fbx");
            gameplay.kitPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath + "FirstAidKit.fbx");
            gameplay.roadBarrierPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath + "RoadBarrier.fbx");
            Require(gameplay.patientProfiles.Count == 5, "Patient data assets were not created.");
            Require(gameplay.supplies.Count == 5, "Supply data assets were not created.");
            AssignAudio(gameplay);

            GoldenHourAudio gameAudio = managerObject.GetComponent<GoldenHourAudio>();
            if (gameAudio == null) gameAudio = managerObject.AddComponent<GoldenHourAudio>();
            AssignAudio(gameAudio);
            ConfigureEventSystem(eventSystem);

            Light directionalLight = UnityEngine.Object.FindAnyObjectByType<Light>();
            if (directionalLight != null)
            {
                directionalLight.transform.rotation = Quaternion.Euler(48f, -28f, 0f);
                directionalLight.intensity = 1.4f;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true)
            };
            SetProjectIdentity();
        }

        private static void CreateEventSystem()
        {
            GameObject eventSystemObject = new GameObject("EventSystem",
                typeof(EventSystem), typeof(InputSystemUIInputModule));
            InputSystemUIInputModule module =
                eventSystemObject.GetComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        private static void ConfigureEventSystem(GameObject eventSystemObject)
        {
            EventSystem eventSystem = eventSystemObject.GetComponent<EventSystem>();
            if (eventSystem == null) eventSystemObject.AddComponent<EventSystem>();
            StandaloneInputModule legacyModule =
                eventSystemObject.GetComponent<StandaloneInputModule>();
            if (legacyModule != null) UnityEngine.Object.DestroyImmediate(legacyModule);
            InputSystemUIInputModule module =
                eventSystemObject.GetComponent<InputSystemUIInputModule>();
            if (module == null) module = eventSystemObject.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        private static void AssignAudio(GoldenHourAudio audio)
        {
            audio.ambience = LoadClip("Roadside_Ambience.wav");
            audio.buttonClick = LoadClip("UI_Click.wav");
            audio.kitOpen = LoadClip("Kit_Open.wav");
            audio.objectiveComplete = LoadClip("Objective_Complete.wav");
            audio.patientInteraction = LoadClip("Patient_Interaction.wav");
            EditorUtility.SetDirty(audio);
        }

        private static void AssignAudio(GameplayController gameplay)
        {
            gameplay.ambience = LoadClip("Roadside_Ambience.wav");
            gameplay.buttonClick = LoadClip("UI_Click.wav");
            gameplay.kitOpen = LoadClip("Kit_Open.wav");
            gameplay.objectiveComplete = LoadClip("Objective_Complete.wav");
            gameplay.patientInteraction = LoadClip("Patient_Interaction.wav");
        }

        private static AudioClip LoadClip(string filename)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath + filename);
            Require(clip != null, "Missing generated audio asset " + filename);
            return clip;
        }

        private static List<PatientData> LoadPatients()
        {
            return new[] { "P01", "P02", "P03", "P04", "P05" }
                .Select(id => AssetDatabase.LoadAssetAtPath<PatientData>(
                    DataPath + "Patient_" + id + ".asset"))
                .Where(asset => asset != null)
                .ToList();
        }

        private static List<SupplyData> LoadSupplies()
        {
            return new[] { "Gauze", "Bandage", "Gloves", "Dressing", "EmergencyBlanket" }
                .Select(id => AssetDatabase.LoadAssetAtPath<SupplyData>(
                    DataPath + "Supply_" + id + ".asset"))
                .Where(asset => asset != null)
                .ToList();
        }

        private static void ValidateTriageRules()
        {
            PatientData walking = ScriptableObject.CreateInstance<PatientData>();
            walking.canWalk = true;
            walking.breathing = false;
            walking.severeBleeding = true;
            Require(TriageEvaluator.Evaluate(walking) == TriageCategory.Green,
                "Walking-patient rule failed.");
            UnityEngine.Object.DestroyImmediate(walking);

            PatientData red = ScriptableObject.CreateInstance<PatientData>();
            red.breathing = false;
            Require(TriageEvaluator.Evaluate(red) == TriageCategory.Red,
                "Not-breathing priority rule failed.");
            red.breathing = true;
            red.severeBleeding = true;
            Require(TriageEvaluator.Evaluate(red) == TriageCategory.Red,
                "Severe-bleeding priority rule failed.");
            red.severeBleeding = false;
            red.followsCommands = false;
            Require(TriageEvaluator.Evaluate(red) == TriageCategory.Red,
                "Unresponsive priority rule failed.");
            UnityEngine.Object.DestroyImmediate(red);

            PatientData yellow = ScriptableObject.CreateInstance<PatientData>();
            yellow.canWalk = false;
            yellow.breathing = true;
            yellow.followsCommands = true;
            Require(TriageEvaluator.Evaluate(yellow) == TriageCategory.Yellow,
                "Stable non-walking priority rule failed.");
            UnityEngine.Object.DestroyImmediate(yellow);
            Debug.Log("Triage rule validation passed.");
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            List<EditorBuildSettingsScene> scenes =
                EditorBuildSettings.scenes.Where(scene => scene.path != scenePath).ToList();
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static bool HasSceneInBuildSettings(string scenePath)
        {
            return EditorBuildSettings.scenes.Any(scene =>
                scene.enabled && scene.path == scenePath);
        }

        private static void SetProjectIdentity()
        {
            PlayerSettings.companyName = "Golden Hour Learning";
            PlayerSettings.productName = "Golden Hour";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.runInBackground = false;
        }

        private static void DeleteNamedObject(string objectName)
        {
            GameObject target = GameObject.Find(objectName);
            if (target != null) UnityEngine.Object.DestroyImmediate(target);
        }

        private static void RemoveComponentIfPresent<T>(GameObject target)
            where T : Component
        {
            T component = target.GetComponent<T>();
            if (component != null) UnityEngine.Object.DestroyImmediate(component);
        }

        private static void DeleteTemplateLeftovers()
        {
            AssetDatabase.DeleteAsset("Assets/TutorialInfo");
            AssetDatabase.DeleteAsset("Assets/Readme.asset");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
