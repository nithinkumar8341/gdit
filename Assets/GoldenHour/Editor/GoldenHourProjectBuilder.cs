using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GoldenHour;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace GoldenHour.EditorTools
{
    /// <summary>
    /// Editor automation for Golden Hour: creates data assets and scenes, wires references,
    /// configures build settings, validates the project and builds the Windows player.
    /// All entry points work from the menu and from -batchmode -executeMethod.
    /// </summary>
    public static class GoldenHourProjectBuilder
    {
        private const string GameScenePath = "Assets/Scenes/Samplespace.unity";
        private const string MenuScenePath = "Assets/GoldenHour/Scenes/MainMenu.unity";
        private const string DataPath = "Assets/GoldenHour/ScriptableObjects/";
        private const string AudioPath = "Assets/GoldenHour/Audio/";
        private const string ModelPath = "Assets/GoldenHour/Blender/Exports/";
        private const string BaseMaterialPath = "Assets/GoldenHour/Materials/GH_RuntimeBase.mat";
        private const string BuildPath = "Builds/GoldenHour/GoldenHour.exe";

        private static readonly string[] PatientIds = { "P01", "P02", "P03", "P04", "P05" };
        private static readonly string[] SupplyIds = { "Gloves", "Dressing", "Gauze", "Bandage", "EmergencyBlanket" };

        [MenuItem("GoldenHour/Build Project")]
        public static void BuildProject()
        {
            EnsureDirectories();
            CreateBaseMaterial();
            CreateGameData();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            CreateMainMenuScene();
            ConfigureGameplayScene();
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();
            DeleteTemplateLeftovers();
            AssetDatabase.Refresh();
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
            foreach (PatientData patient in patients)
            {
                Require(TriageEvaluator.Evaluate(patient) == patient.correctTriage,
                    patient.patientId + " observations do not match its expected triage under the game rule.");
                Require(!string.IsNullOrWhiteSpace(patient.patientName) && patient.age > 0 &&
                        !string.IsNullOrWhiteSpace(patient.injuryDescription) &&
                        !string.IsNullOrWhiteSpace(patient.symptoms) &&
                        !string.IsNullOrWhiteSpace(patient.responsiveness) &&
                        !string.IsNullOrWhiteSpace(patient.reasoning) &&
                        !string.IsNullOrWhiteSpace(patient.aidAction),
                    patient.patientId + " is missing identity, observation, reasoning or aid text.");
                Require(patient.recommendedSupply != null, patient.patientId + " needs a recommended supply.");
            }

            int red = patients.Count(item => item.correctTriage == TriageCategory.Red);
            int yellow = patients.Count(item => item.correctTriage == TriageCategory.Yellow);
            int green = patients.Count(item => item.correctTriage == TriageCategory.Green);
            Require(red >= 1 && red <= 2, "Expected 1-2 RED patients, found " + red + ".");
            Require(yellow == 2, "Expected 2 YELLOW patients, found " + yellow + ".");
            Require(green >= 1 && green <= 2, "Expected 1-2 GREEN patients, found " + green + ".");

            List<SupplyData> supplies = LoadSupplies();
            Require(supplies.Count == 5, "Expected five SupplyData assets.");
            Require(File.Exists(GameScenePath), "Gameplay scene is missing.");
            Require(File.Exists(MenuScenePath), "Main menu scene is missing.");
            Require(EditorBuildSettings.scenes.Length >= 2 &&
                    EditorBuildSettings.scenes[0].path == MenuScenePath &&
                    HasSceneInBuildSettings(GameScenePath),
                "Build Settings must list MainMenu first and the gameplay scene.");
            foreach (string model in new[] { "LowPolyMannequin", "FirstAidKit", "RoadBarrier", "AccidentProps", "DamagedCar" })
            {
                Require(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath + model + ".fbx") != null,
                    "Blender export " + model + ".fbx is missing or not imported.");
            }

            GameObject kit = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath + "FirstAidKit.fbx");
            Require(kit.GetComponentsInChildren<Transform>().Any(item => item.name == "KitHinge"),
                "FirstAidKit.fbx must contain the KitHinge lid pivot.");
            foreach (string clip in new[] { "Roadside_Ambience", "UI_Click", "Kit_Open", "Objective_Complete", "Patient_Interaction" })
            {
                LoadClip(clip + ".wav");
            }

            Scene game = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            Require(GameObject.Find("Player") != null, "Existing Player object is missing.");
            Require(GameObject.Find("GameManager") != null, "Existing GameManager is missing.");
            Require(GameObject.Find("ScoreManager") != null, "Existing ScoreManager is missing.");
            Require(GameObject.Find("EventSystem") != null, "EventSystem is missing.");
            GameplayController gameplay = UnityEngine.Object.FindAnyObjectByType<GameplayController>();
            Require(gameplay != null, "GameplayController is missing.");
            Require(gameplay.patientProfiles.Count == 5 && gameplay.supplies.Count == 5 &&
                    gameplay.baseMaterial != null && gameplay.mannequinPrefab != null && gameplay.kitPrefab != null,
                "GameplayController references are not fully assigned.");
            AccidentEnvironment environment = gameplay.GetComponent<AccidentEnvironment>();
            Require(environment != null && environment.carPrefab != null && environment.barrierPrefab != null &&
                    environment.conePrefab != null, "AccidentEnvironment prefab references are not assigned.");
            Require(UnityEngine.Object.FindAnyObjectByType<FirstPersonPlayer>() != null, "FirstPersonPlayer is missing.");
            Require(UnityEngine.Object.FindAnyObjectByType<InputSystemUIInputModule>() != null,
                "Gameplay EventSystem does not use the Input System UI module.");
            Require(UnityEngine.Object.FindAnyObjectByType<StandaloneInputModule>() == null,
                "Legacy StandaloneInputModule must not be present.");
            Require(UnityEngine.Object.FindAnyObjectByType<AudioListener>() != null, "Gameplay AudioListener is missing.");
            EditorSceneManager.CloseScene(game, true);

            Scene menu = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            Require(UnityEngine.Object.FindAnyObjectByType<MainMenuController>() != null, "MainMenuController is missing.");
            Require(UnityEngine.Object.FindAnyObjectByType<InputSystemUIInputModule>() != null,
                "Main menu does not use the Input System UI module.");
            Require(UnityEngine.Object.FindAnyObjectByType<Camera>() != null &&
                    UnityEngine.Object.FindAnyObjectByType<AudioListener>() != null,
                "Main menu needs a camera and an AudioListener.");
            EditorSceneManager.CloseScene(menu, true);
            Require(!AssetDatabase.IsValidFolder("Assets/TutorialInfo") &&
                    AssetDatabase.LoadMainAssetAtPath("Assets/Readme.asset") == null,
                "Template leftovers are still present.");
            Debug.Log("Golden Hour project validation passed.");
        }

        [MenuItem("GoldenHour/Build Windows Player")]
        public static void BuildWindowsPlayer()
        {
            string outputPath = Path.GetFullPath(BuildPath);
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

            Debug.Log("Windows build created at " + outputPath + " | bytes: " + report.summary.totalSize +
                      " | warnings: " + report.summary.totalWarnings);
        }

        // ------------------------------------------------------------------ data

        private static void EnsureDirectories()
        {
            foreach (string directory in new[]
                     {
                         "Assets/GoldenHour/Scenes", "Assets/GoldenHour/ScriptableObjects",
                         "Assets/GoldenHour/Materials", "Assets/GoldenHour/Audio"
                     })
            {
                Directory.CreateDirectory(directory);
            }

            AssetDatabase.Refresh();
        }

        private static void CreateBaseMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(BaseMaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                Require(shader != null, "URP Lit shader not found.");
                material = new Material(shader) { name = "GH_RuntimeBase" };
                AssetDatabase.CreateAsset(material, BaseMaterialPath);
            }

            material.color = Color.white;
            EditorUtility.SetDirty(material);
        }

        private static void CreateGameData()
        {
            SupplyData gloves = CreateSupply("Gloves", "Disposable Gloves",
                "Protective barrier for the rescuer",
                "Gloves protect you and the person from infection before any contact - the first step in every scenario.",
                new Color32(70, 130, 180, 255));
            SupplyData dressing = CreateSupply("Dressing", "Trauma Dressing",
                "Large pad for heavy bleeding",
                "A large dressing pad covers a heavily bleeding wound so firm pressure can be held over it.",
                new Color32(60, 140, 96, 255));
            SupplyData gauze = CreateSupply("Gauze", "Sterile Gauze Pads",
                "Clean cover for small cuts",
                "Sterile gauze keeps a small cut or graze clean and covered.",
                new Color32(225, 225, 214, 255));
            SupplyData bandage = CreateSupply("Bandage", "Triangular Bandage",
                "Support for an injured limb",
                "A triangular bandage can form a sling or padding to support and steady an injured limb.",
                new Color32(214, 182, 112, 255));
            SupplyData blanket = CreateSupply("EmergencyBlanket", "Emergency Blanket",
                "Keeps a person warm and reassured",
                "A foil blanket helps keep a shaken person warm while they wait for help.",
                new Color32(212, 170, 60, 255));

            CreatePatient("P01", "Alex Morgan", 28, new Color32(70, 120, 160, 255),
                "Small graze on the forearm; standing beside the road.",
                "Walked over to you unaided. Says they feel shaken and cold.",
                "Alert, talking and follows instructions.",
                breathing: true, severeBleeding: false, canWalk: true, followsCommands: true,
                TriageCategory.Green,
                "Alex can walk. In this simplified START-style model, anyone who can walk is GREEN (minor) and is re-checked later.",
                blanket, "Wrap the emergency blanket around Alex's shoulders and keep them company away from traffic.");
            CreatePatient("P02", "Jordan Lee", 42, new Color32(150, 70, 60, 255),
                "Deep cut on the upper arm with heavy, continuous bleeding.",
                "Lying on the road, pale and anxious. Says they feel dizzy.",
                "Awake and follows simple instructions.",
                breathing: true, severeBleeding: true, canWalk: false, followsCommands: true,
                TriageCategory.Red,
                "Severe visible bleeding is a RED sign in this model, even though Jordan is awake and talking.",
                dressing, "Place the trauma dressing over the wound and simulate holding firm pressure until help arrives.");
            CreatePatient("P03", "Sam Rivera", 36, new Color32(90, 90, 120, 255),
                "No visible major wound. Lying still near the car.",
                "Does not respond to voice or a gentle shoulder tap. No breathing observed during the check.",
                "Unresponsive.",
                breathing: false, severeBleeding: false, canWalk: false, followsCommands: false,
                TriageCategory.Red,
                "Not breathing and unresponsive are RED signs in this model; this person needs immediate professional help.",
                gloves, "Put on gloves before contact and call loudly for the arriving paramedics - this scenario marks Sam for immediate professional care.");
            CreatePatient("P04", "Taylor Kim", 51, new Color32(80, 120, 80, 255),
                "Painful, swollen lower leg; cannot put weight on it. No heavy bleeding.",
                "Lying on the road. Breathing normally and answering questions clearly.",
                "Alert and follows instructions.",
                breathing: true, severeBleeding: false, canWalk: false, followsCommands: true,
                TriageCategory.Yellow,
                "Taylor cannot walk but shows none of the RED signs, so the model assigns YELLOW (delayed).",
                bandage, "Use the triangular bandage as padding to steady the leg in the position found, without moving it.");
            CreatePatient("P05", "Casey Patel", 19, new Color32(130, 100, 150, 255),
                "Small cut above the eyebrow and bruised ribs; minor bleeding only.",
                "Lying down, says it hurts to stand. Breathing normally and talking.",
                "Alert and follows instructions.",
                breathing: true, severeBleeding: false, canWalk: false, followsCommands: true,
                TriageCategory.Yellow,
                "Casey cannot walk but is breathing, responsive and has no severe bleeding, so the model assigns YELLOW.",
                gauze, "Cover the small cut with a sterile gauze pad and keep Casey still and reassured.");
        }

        private static SupplyData CreateSupply(string id, string displayName, string description,
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
            asset.displayName = displayName;
            asset.description = description;
            asset.educationalPurpose = purpose;
            asset.displayColor = color;
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void CreatePatient(string id, string patientName, int age, Color clothing, string injury,
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
            asset.patientName = patientName;
            asset.age = age;
            asset.clothingColor = clothing;
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

        // ---------------------------------------------------------------- scenes

        private static void CreateMainMenuScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject cameraObject = new GameObject("Menu Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(14, 19, 25, 255);

            GameObject menu = new GameObject("Main Menu");
            menu.AddComponent<MainMenuController>();
            AssignAudio(menu.AddComponent<GoldenHourAudio>());
            CreateEventSystem();
            EditorSceneManager.SaveScene(scene, MenuScenePath);
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

            // Placeholder prototype content replaced by the generated scenario.
            DeleteNamedObject("Patient_Test");
            DeleteNamedObject("TestGround");
            DeleteNamedObject("GoldenHourUI");
            RemoveComponentIfPresent<global::PlayerController>(playerObject);
            RemoveComponentIfPresent<global::PlayerLook>(playerObject);
            RemoveComponentIfPresent<global::PlayerInteraction>(playerObject);

            Camera camera = playerObject.GetComponentInChildren<Camera>();
            Require(camera != null, "The existing Player Camera child is required.");
            RemoveComponentIfPresent<global::PlayerLook>(camera.gameObject);
            camera.transform.localPosition = new Vector3(0f, 1.52f, 0f);
            camera.transform.localRotation = Quaternion.identity;
            camera.nearClipPlane = 0.05f;
            camera.fieldOfView = 70f;
            camera.gameObject.tag = "MainCamera";
            if (camera.GetComponent<AudioListener>() == null) camera.gameObject.AddComponent<AudioListener>();

            CharacterController characterController = playerObject.GetComponent<CharacterController>();
            if (characterController == null) characterController = playerObject.AddComponent<CharacterController>();
            characterController.height = 1.8f;
            characterController.radius = 0.3f;
            characterController.center = new Vector3(0f, 0.9f, 0f);
            characterController.stepOffset = 0.3f;
            characterController.skinWidth = 0.04f;
            playerObject.transform.SetPositionAndRotation(AccidentEnvironment.PlayerStart, Quaternion.identity);
            FirstPersonPlayer controller = playerObject.GetComponent<FirstPersonPlayer>();
            if (controller == null) controller = playerObject.AddComponent<FirstPersonPlayer>();
            SerializedObject serializedPlayer = new SerializedObject(controller);
            serializedPlayer.FindProperty("cameraTransform").objectReferenceValue = camera.transform;
            serializedPlayer.ApplyModifiedPropertiesWithoutUndo();

            GameManager gameManager = managerObject.GetComponent<GameManager>();
            ScoreManager scoreManager = scoreObject.GetComponent<ScoreManager>();
            Require(gameManager != null && scoreManager != null, "Existing manager scripts are required.");
            gameManager.scenarioDuration = 900f;
            scoreManager.ResetScore();
            EditorUtility.SetDirty(gameManager);
            EditorUtility.SetDirty(scoreManager);

            GameplayController gameplay = managerObject.GetComponent<GameplayController>();
            if (gameplay == null) gameplay = managerObject.AddComponent<GameplayController>();
            gameplay.gameManager = gameManager;
            gameplay.scoreManager = scoreManager;
            gameplay.player = controller;
            gameplay.playerCamera = camera.transform;
            gameplay.patientProfiles = LoadPatients();
            gameplay.supplies = LoadSupplies();
            gameplay.baseMaterial = AssetDatabase.LoadAssetAtPath<Material>(BaseMaterialPath);
            gameplay.mannequinPrefab = LoadModel("LowPolyMannequin");
            gameplay.kitPrefab = LoadModel("FirstAidKit");
            Require(gameplay.patientProfiles.Count == 5, "Patient data assets were not created.");
            Require(gameplay.supplies.Count == 5, "Supply data assets were not created.");
            EditorUtility.SetDirty(gameplay);

            AccidentEnvironment environment = managerObject.GetComponent<AccidentEnvironment>();
            if (environment == null) environment = managerObject.AddComponent<AccidentEnvironment>();
            environment.carPrefab = LoadModel("DamagedCar");
            environment.barrierPrefab = LoadModel("RoadBarrier");
            environment.conePrefab = LoadModel("AccidentProps");
            EditorUtility.SetDirty(environment);

            GoldenHourAudio gameAudio = managerObject.GetComponent<GoldenHourAudio>();
            if (gameAudio == null) gameAudio = managerObject.AddComponent<GoldenHourAudio>();
            AssignAudio(gameAudio);
            ConfigureEventSystem(eventSystem);

            Light sun = UnityEngine.Object.FindObjectsByType<Light>()
                .FirstOrDefault(item => item.type == LightType.Directional);
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
                sun.intensity = 1.6f;
                sun.color = new Color32(255, 236, 205, 255);
                sun.shadows = LightShadows.Soft;
                EditorUtility.SetDirty(sun);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true)
            };
            PlayerSettings.companyName = "Golden Hour Learning";
            PlayerSettings.productName = "Golden Hour";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
        }

        private static void CreateEventSystem()
        {
            GameObject eventSystemObject = new GameObject("EventSystem",
                typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystemObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        private static void ConfigureEventSystem(GameObject eventSystemObject)
        {
            if (eventSystemObject.GetComponent<EventSystem>() == null) eventSystemObject.AddComponent<EventSystem>();
            StandaloneInputModule legacyModule = eventSystemObject.GetComponent<StandaloneInputModule>();
            if (legacyModule != null) UnityEngine.Object.DestroyImmediate(legacyModule);
            InputSystemUIInputModule module = eventSystemObject.GetComponent<InputSystemUIInputModule>();
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

        // --------------------------------------------------------------- helpers

        private static GameObject LoadModel(string name)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath + name + ".fbx");
            Require(model != null, "Missing Blender export " + name + ".fbx");
            return model;
        }

        private static AudioClip LoadClip(string filename)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath + filename);
            Require(clip != null, "Missing generated audio asset " + filename);
            return clip;
        }

        private static List<PatientData> LoadPatients()
        {
            return PatientIds
                .Select(id => AssetDatabase.LoadAssetAtPath<PatientData>(DataPath + "Patient_" + id + ".asset"))
                .Where(asset => asset != null)
                .ToList();
        }

        private static List<SupplyData> LoadSupplies()
        {
            return SupplyIds
                .Select(id => AssetDatabase.LoadAssetAtPath<SupplyData>(DataPath + "Supply_" + id + ".asset"))
                .Where(asset => asset != null)
                .ToList();
        }

        private static void ValidateTriageRules()
        {
            PatientData sample = ScriptableObject.CreateInstance<PatientData>();
            try
            {
                sample.canWalk = true;
                sample.breathing = false;
                sample.severeBleeding = true;
                Require(TriageEvaluator.Evaluate(sample) == TriageCategory.Green, "Walking-patient rule failed.");

                sample.canWalk = false;
                sample.breathing = false;
                sample.severeBleeding = false;
                sample.followsCommands = true;
                Require(TriageEvaluator.Evaluate(sample) == TriageCategory.Red, "Not-breathing rule failed.");

                sample.breathing = true;
                sample.severeBleeding = true;
                Require(TriageEvaluator.Evaluate(sample) == TriageCategory.Red, "Severe-bleeding rule failed.");

                sample.severeBleeding = false;
                sample.followsCommands = false;
                Require(TriageEvaluator.Evaluate(sample) == TriageCategory.Red, "Unresponsive rule failed.");

                sample.followsCommands = true;
                Require(TriageEvaluator.Evaluate(sample) == TriageCategory.Yellow, "Stable non-walking rule failed.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sample);
            }

            Debug.Log("Triage rule validation passed.");
        }

        private static bool HasSceneInBuildSettings(string scenePath)
        {
            return EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path == scenePath);
        }

        /// <summary>Destroys a root-level scene object by name, if present.</summary>
        private static void DeleteNamedObject(string objectName)
        {
            GameObject target = GameObject.Find(objectName);
            if (target != null) UnityEngine.Object.DestroyImmediate(target);
        }

        private static void RemoveComponentIfPresent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            if (component != null) UnityEngine.Object.DestroyImmediate(component);
        }

        private static void DeleteTemplateLeftovers()
        {
            if (AssetDatabase.IsValidFolder("Assets/TutorialInfo")) AssetDatabase.DeleteAsset("Assets/TutorialInfo");
            if (File.Exists("Assets/Readme.asset")) AssetDatabase.DeleteAsset("Assets/Readme.asset");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
