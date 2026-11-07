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
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace GoldenHour.EditorTools
{
    /// <summary>
    /// Editor automation for Golden Hour v2: creates data assets and scenes, wires references,
    /// configures build settings, validates the project and builds the Windows player.
    /// All entry points work from the menu and from -batchmode -executeMethod.
    /// </summary>
    public static class GoldenHourProjectBuilder
    {
        private const string GameScenePath = "Assets/Scenes/Samplespace.unity";
        private const string MenuScenePath = "Assets/GoldenHour/Scenes/MainMenu.unity";
        private const string AudioPath = "Assets/GoldenHour/Audio/";
        private const string ModelPath = "Assets/GoldenHour/Blender/Exports/";
        private const string BaseMaterialPath = "Assets/GoldenHour/Materials/GH_RuntimeBase.mat";
        private const string UnlitMaterialPath = "Assets/GoldenHour/Materials/GH_RuntimeUnlit.mat";
        private const string OutlineMaterialPath = "Assets/GoldenHour/Materials/GH_Outline.mat";
        private const string SmokeMaterialPath = "Assets/GoldenHour/Materials/GH_Smoke.mat";
        private const string RainMaterialPath = "Assets/GoldenHour/Materials/GH_Rain.mat";
        private const string SmokeTexturePath = "Assets/GoldenHour/Materials/GH_SmokePuff.png";
        private const string SkyMaterialPath = "Assets/GoldenHour/Materials/GH_LateAfternoonSky.mat";
        private const string PostProfilePath = "Assets/GoldenHour/Settings/GH_PostProcessing.asset";
        private const string PcPipelinePath = "Assets/Settings/PC_RPAsset.asset";
        private const string BuildPath = "Builds/GoldenHour/GoldenHour.exe";

        private static readonly string[] AudioClips =
        {
            "Roadside_Ambience", "UI_Click", "UI_Hover", "Footstep", "Kit_Open", "Objective_Complete", "Patient_Interaction",
            "Siren", "Radio_Static", "Voice_Murmur", "Heartbeat", "Breathing", "Rain_Loop", "Wind_Loop", "Music_Pad", "Music_Pulse",
            "Music_Drums", "Metronome", "Compression"
        };

        [MenuItem("GoldenHour/Build Project")]
        /// <summary>
        /// Evidence for the non-destructive content rule: edits a casualty and a scenario field as a designer would in the
        /// Inspector, runs the Build Project content step, checks both edits survived, then restores the originals.
        /// Batch: -executeMethod GoldenHour.EditorTools.GoldenHourProjectBuilder.CheckContentPreservation
        /// </summary>
        public static void CheckContentPreservation()
        {
            GoldenHourContent.CreateAll();
            CasualtyProfile profile = AssetDatabase.LoadAssetAtPath<CasualtyProfile>(GoldenHourContent.Root + "Casualties/T01.asset");
            ScenarioData scenario = AssetDatabase.LoadAssetAtPath<ScenarioData>(GoldenHourContent.Root + "Scenarios/highway.asset");
            Require(profile != null && scenario != null, "Content assets are missing.");
            string originalNote = profile.aidNote;
            float originalDeath = profile.deathMinute;
            string originalBriefing = scenario.briefing;
            int originalDressings = scenario.dressings;
            try
            {
                profile.aidNote = "INSPECTOR EDIT MARKER";
                profile.deathMinute = 33.3f;
                scenario.briefing = "INSPECTOR EDIT MARKER";
                scenario.dressings = 7;
                EditorUtility.SetDirty(profile);
                EditorUtility.SetDirty(scenario);
                AssetDatabase.SaveAssets();
                GoldenHourContent.CreateAll();
                profile = AssetDatabase.LoadAssetAtPath<CasualtyProfile>(GoldenHourContent.Root + "Casualties/T01.asset");
                scenario = AssetDatabase.LoadAssetAtPath<ScenarioData>(GoldenHourContent.Root + "Scenarios/highway.asset");
                bool kept = profile.aidNote == "INSPECTOR EDIT MARKER" && Mathf.Approximately(profile.deathMinute, 33.3f) &&
                            scenario.briefing == "INSPECTOR EDIT MARKER" && scenario.dressings == 7 && GoldenHourContent.Written == 0;
                Require(kept, "Build Project overwrote Inspector edits (written " + GoldenHourContent.Written + ").");
                Debug.Log("Content preservation check passed: Inspector edits to T01 and highway survived Build Project; 0 assets rewritten.");
            }
            finally
            {
                profile.aidNote = originalNote;
                profile.deathMinute = originalDeath;
                scenario.briefing = originalBriefing;
                scenario.dressings = originalDressings;
                EditorUtility.SetDirty(profile);
                EditorUtility.SetDirty(scenario);
                AssetDatabase.SaveAssets();
            }
        }

        public static void BuildProject()
        {
            EnsureDirectories();
            CreateBaseMaterial();
            CreateVisualAssets();
            ScenarioCatalog catalog = GoldenHourContent.CreateAll();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            CreateMainMenuScene(catalog);
            ConfigureGameplayScene(catalog);
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
            ScenarioCatalog catalog = GoldenHourContent.LoadCatalog();
            Require(catalog != null && catalog.scenarios.Count == 4, "The campaign must contain four scenarios.");
            string[] expectedIds = { "tutorial", "highway", "bus", "building" };
            for (int i = 0; i < expectedIds.Length; i++)
            {
                Require(catalog.scenarios[i] != null && catalog.scenarios[i].scenarioId == expectedIds[i], "Scenario " + i + " must be " + expectedIds[i] + ".");
                ValidateScenario(catalog.scenarios[i]);
            }

            Require(catalog.scenarios[0].casualties.Count == 3 && catalog.scenarios[0].tutorial && catalog.scenarios[0].tutorialSteps.Length >= 6,
                "Tutorial needs 3 casualties and guided steps.");
            Require(catalog.scenarios[2].casualties.Count >= 9 && catalog.scenarios[2].night && catalog.scenarios[2].rain,
                "Bus rollover needs ~10 casualties at night in the rain.");
            Require(catalog.scenarios[2].hazards.Exists(h => h.type == HazardType.FuelLeak) && catalog.scenarios[2].hazards.Exists(h => h.type == HazardType.Fire),
                "Bus rollover needs fuel-leak and fire hazards.");
            Require(catalog.scenarios[3].casualties.Count >= 11, "Building collapse needs ~12 casualties.");
            Require(catalog.scenarios[3].casualties.Count(c => c.profile.variant == BodyVariant.Child) >= 2, "Building collapse needs children.");
            Require(catalog.scenarios[3].casualties.Count(c => c.profile.initial.canWalk) >= 4, "Building collapse needs a crowd of walking wounded.");
            Require(catalog.scenarios.SelectMany(s => s.casualties).Any(c => c.profile.variant == BodyVariant.Motorcyclist),
                "A helmeted motorcyclist casualty is required.");
            HashSet<TriageTag> categories = new HashSet<TriageTag>(catalog.scenarios.SelectMany(s => s.casualties)
                .Select(c => StartTriage.Evaluate(c.profile.initial)));
            Require(categories.SetEquals(new[] { TriageTag.Red, TriageTag.Yellow, TriageTag.Green, TriageTag.Black }),
                "The campaign must use all four START categories.");

            Require(File.Exists(GameScenePath) && File.Exists(MenuScenePath), "Game and menu scenes are required.");
            Require(EditorBuildSettings.scenes.Length >= 2 && EditorBuildSettings.scenes[0].path == MenuScenePath &&
                    EditorBuildSettings.scenes.Any(s => s.enabled && s.path == GameScenePath),
                "Build Settings must list MainMenu first and the gameplay scene.");
            foreach (string model in new[] { "FirstAidKit", "RoadBarrier", "AccidentProps", "DamagedCar" })
            {
                Require(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath + model + ".fbx") != null, "Blender export " + model + ".fbx is missing.");
            }

            foreach (string rigName in new[] { "RiggedHuman", "RiggedChild" }) ValidateRig(rigName);
            foreach (string vehicle in new[] { "Ambulance", "PoliceCar", "FireTruck", "Bus" }) ValidateVehicle(vehicle);
            foreach (string model in new[] { "Motorbike", "Stretcher", "V2Props" })
            {
                Require(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath + model + ".fbx") != null, "Blender export " + model + ".fbx is missing.");
            }

            ValidateTreatmentModels();

            foreach (string clip in AudioClips) LoadClip(clip + ".wav");

            Scene game = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            Require(GameObject.Find("Player") != null && GameObject.Find("EventSystem") != null, "Player and EventSystem are required.");
            IncidentDirector director = UnityEngine.Object.FindAnyObjectByType<IncidentDirector>();
            Require(director != null, "IncidentDirector is missing from the gameplay scene.");
            Require(director.treatmentPropsPrefab != null, "IncidentDirector needs the TreatmentProps model.");
            Viewmodel sceneViewmodel = UnityEngine.Object.FindAnyObjectByType<Viewmodel>();
            Require(sceneViewmodel != null && sceneViewmodel.handsModel != null, "The player camera's Viewmodel needs the FirstPersonHands model.");
            Require(director.catalog == catalog && director.baseMaterial != null && director.unlitMaterial != null && director.kitPrefab != null &&
                    director.outlineMaterial != null && director.environmentAssets.smokeMaterial != null && director.environmentAssets.carPrefab != null,
                "IncidentDirector references are not fully assigned.");
            Require(director.GetComponent<AccidentEnvironment>() != null && director.GetComponent<GoldenHourAudio>() != null,
                "The director needs AccidentEnvironment and GoldenHourAudio.");
            GoldenHourAudio gameAudio = director.GetComponent<GoldenHourAudio>();
            Require(gameAudio.siren != null && gameAudio.radioStatic != null && gameAudio.musicPad != null && gameAudio.heartbeat != null,
                "Gameplay audio clips are not assigned.");
            Require(UnityEngine.Object.FindAnyObjectByType<InputSystemUIInputModule>() != null &&
                    UnityEngine.Object.FindAnyObjectByType<StandaloneInputModule>() == null, "Gameplay must use the Input System UI module only.");
            Require(RenderSettings.fog && RenderSettings.skybox != null, "Gameplay scene needs fog and a skybox.");
            Camera view = UnityEngine.Object.FindAnyObjectByType<FirstPersonPlayer>().GetComponentInChildren<Camera>();
            Require(view != null && view.GetUniversalAdditionalCameraData().renderPostProcessing, "Player camera must render post-processing.");
            Volume volume = UnityEngine.Object.FindAnyObjectByType<Volume>();
            Require(volume != null && volume.sharedProfile != null && volume.sharedProfile.Has<Bloom>() && volume.sharedProfile.Has<Vignette>(),
                "Global Volume must use the post-processing profile.");
            Require(UnityEngine.Object.FindAnyObjectByType<IncidentDirector>() != null, "The gameplay scene needs an IncidentDirector.");
            EditorSceneManager.CloseScene(game, true);

            Scene menu = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            MainMenuController menuController = UnityEngine.Object.FindAnyObjectByType<MainMenuController>();
            Require(menuController != null && menuController.catalog == catalog, "MainMenuController with the campaign catalog is missing.");
            Require(UnityEngine.Object.FindAnyObjectByType<InputSystemUIInputModule>() != null, "Main menu does not use the Input System UI module.");
            Require(UnityEngine.Object.FindAnyObjectByType<Camera>() != null && UnityEngine.Object.FindAnyObjectByType<AudioListener>() != null,
                "Main menu needs a camera and an AudioListener.");
            EditorSceneManager.CloseScene(menu, true);
            Require(!AssetDatabase.IsValidFolder("Assets/TutorialInfo") && AssetDatabase.LoadMainAssetAtPath("Assets/Readme.asset") == null,
                "Template leftovers are still present.");
            Debug.Log("Golden Hour project validation passed.");
        }

        private static void ValidateScenario(ScenarioData scenario)
        {
            Require(!string.IsNullOrEmpty(scenario.title) && !string.IsNullOrEmpty(scenario.briefing), scenario.scenarioId + " needs a title and briefing.");
            Require(scenario.casualties.Count > 0 && scenario.casualties.All(c => c.profile != null), scenario.scenarioId + " has missing casualty profiles.");
            Require(scenario.casualties.Select(c => c.profile.casualtyId).Distinct().Count() == scenario.casualties.Count,
                scenario.scenarioId + " casualty IDs must be unique.");
            Require(scenario.locationOptions.Length >= 2 && scenario.correctLocation >= 0 && scenario.correctLocation < scenario.locationOptions.Length,
                scenario.scenarioId + " needs dispatch location options.");
            Require(scenario.waves.Count > 0 && scenario.hazards.Count > 0, scenario.scenarioId + " needs ambulance waves and hazards.");
            int stretcherCases = scenario.casualties.Count(c => StartTriage.Evaluate(c.profile.initial) != TriageTag.Black && !c.profile.initial.canWalk);
            Require(scenario.TotalAmbulanceCapacity >= stretcherCases, scenario.scenarioId + ": ambulance capacity is below the stretcher cases.");
            Require(scenario.simMinutesPerSecond > 0f && scenario.goldenHourMinutes > 0f, scenario.scenarioId + " needs a valid clock.");
            foreach (CasualtyPlacement placement in scenario.casualties)
            {
                CasualtyProfile profile = placement.profile;
                Require(!string.IsNullOrEmpty(profile.appearance) && !string.IsNullOrEmpty(profile.injurySummary),
                    profile.casualtyId + " needs appearance and injury text.");
                if (profile.deathMinute > 0f && profile.condition == CriticalCondition.Haemorrhage)
                {
                    Require((profile.stabilisedBy & Treatment.PressureDressing) != 0, profile.casualtyId + " bleeding must be stabilised by a dressing.");
                }
            }
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
                throw new BuildFailedException("Windows build failed: " + report.summary.result + " (" + report.summary.totalErrors + " errors).");
            }

            Debug.Log("Windows build created at " + outputPath + " | bytes: " + report.summary.totalSize + " | warnings: " + report.summary.totalWarnings);
        }

        // ---------------------------------------------------------------- assets

        private static void EnsureDirectories()
        {
            foreach (string directory in new[]
                     {
                         "Assets/GoldenHour/Scenes", "Assets/GoldenHour/ScriptableObjects", "Assets/GoldenHour/Materials", "Assets/GoldenHour/Audio",
                         "Assets/GoldenHour/Settings"
                     })
            {
                Directory.CreateDirectory(directory);
            }

            AssetDatabase.Refresh();
        }

        private static void CreateBaseMaterial()
        {
            Material material = LoadOrCreateMaterial(BaseMaterialPath, "Universal Render Pipeline/Lit");
            material.color = Color.white;
            EditorUtility.SetDirty(material);
        }

        private static void CreateVisualAssets()
        {
            Material unlit = LoadOrCreateMaterial(UnlitMaterialPath, "Universal Render Pipeline/Unlit");
            unlit.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(unlit);

            Material outline = LoadOrCreateMaterial(OutlineMaterialPath, "Universal Render Pipeline/Unlit");
            outline.SetColor("_BaseColor", new Color(1f, 0.85f, 0.35f));
            outline.SetFloat("_Cull", (float)CullMode.Front);
            EditorUtility.SetDirty(outline);

            Texture2D puff = CreateSmokeTexture();
            Material smoke = LoadOrCreateMaterial(SmokeMaterialPath, "Universal Render Pipeline/Particles/Unlit");
            ConfigureTransparentParticle(smoke, puff, Color.white);
            Material rain = LoadOrCreateMaterial(RainMaterialPath, "Universal Render Pipeline/Particles/Unlit");
            ConfigureTransparentParticle(rain, puff, new Color(0.75f, 0.82f, 0.95f, 0.6f));

            Material sky = LoadOrCreateMaterial(SkyMaterialPath, "Skybox/Procedural");
            sky.SetFloat("_SunSize", 0.05f);
            sky.SetFloat("_SunSizeConvergence", 6f);
            sky.SetFloat("_AtmosphereThickness", 1.35f);
            sky.SetColor("_SkyTint", new Color(0.62f, 0.56f, 0.52f));
            sky.SetColor("_GroundColor", new Color(0.42f, 0.37f, 0.33f));
            sky.SetFloat("_Exposure", 1.15f);
            EditorUtility.SetDirty(sky);

            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, PostProfilePath);
            }

            Bloom bloom = ProfileComponent<Bloom>(profile);
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.55f);
            bloom.scatter.Override(0.62f);
            bloom.tint.Override(new Color(1f, 0.9f, 0.78f));
            Vignette vignette = ProfileComponent<Vignette>(profile);
            vignette.intensity.Override(0.27f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(new Color(0.08f, 0.05f, 0.04f));
            ColorAdjustments grading = ProfileComponent<ColorAdjustments>(profile);
            grading.postExposure.Override(0.12f);
            grading.contrast.Override(12f);
            grading.saturation.Override(6f);
            grading.colorFilter.Override(new Color(1f, 0.96f, 0.9f));
            WhiteBalance balance = ProfileComponent<WhiteBalance>(profile);
            balance.temperature.Override(10f);
            balance.tint.Override(2f);
            Tonemapping tonemapping = ProfileComponent<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.Neutral);
            EditorUtility.SetDirty(profile);

            UniversalRenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PcPipelinePath);
            if (pipeline != null)
            {
                pipeline.shadowDistance = 45f;
                pipeline.shadowCascadeCount = 2;
                EditorUtility.SetDirty(pipeline);
            }
        }

        private static void ConfigureTransparentParticle(Material material, Texture2D texture, Color color)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
        }

        private static T ProfileComponent<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T existing)) return existing;
            T component = profile.Add<T>();
            component.name = typeof(T).Name;
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        private static Material LoadOrCreateMaterial(string path, string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            Require(shader != null, shaderName + " shader not found.");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            return material;
        }

        private static Texture2D CreateSmokeTexture()
        {
            if (!File.Exists(SmokeTexturePath))
            {
                const int size = 64;
                Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f) / size * 2f - 1f;
                        float dy = (y + 0.5f) / size * 2f - 1f;
                        float alpha = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                        alpha = alpha * alpha * (3f - 2f * alpha);
                        texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                }

                File.WriteAllBytes(SmokeTexturePath, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(SmokeTexturePath);
            }

            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(SmokeTexturePath);
            if (importer != null && (!importer.alphaIsTransparency || importer.mipmapEnabled))
            {
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(SmokeTexturePath);
        }

        // ---------------------------------------------------------------- scenes

        private static void CreateMainMenuScene(ScenarioCatalog catalog)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject cameraObject = new GameObject("Menu Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = new Color32(14, 19, 25, 255);
            camera.farClipPlane = 160f;
            camera.transform.SetPositionAndRotation(new Vector3(9f, 4f, -6f), Quaternion.Euler(14f, -40f, 0f));
            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            GameObject sun = new GameObject("Directional Light", typeof(Light));
            Light light = sun.GetComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            RenderSettings.sun = light;
            AccidentEnvironment.ApplyLighting();
            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            Volume volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProfilePath);

            GameObject menu = new GameObject("Main Menu");
            MainMenuController controller = menu.AddComponent<MainMenuController>();
            controller.catalog = catalog;
            controller.baseMaterial = AssetDatabase.LoadAssetAtPath<Material>(BaseMaterialPath);
            controller.unlitMaterial = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath);
            controller.menuCamera = camera;
            AccidentEnvironment backdrop = menu.AddComponent<AccidentEnvironment>();
            backdrop.carPrefab = LoadModel("DamagedCar");
            backdrop.barrierPrefab = LoadModel("RoadBarrier");
            backdrop.conePrefab = LoadModel("AccidentProps");
            backdrop.smokeMaterial = AssetDatabase.LoadAssetAtPath<Material>(SmokeMaterialPath);
            AssignAudio(menu.AddComponent<GoldenHourAudio>());
            CreateEventSystem();
            EditorSceneManager.SaveScene(scene, MenuScenePath);
        }

        private static void ConfigureGameplayScene(ScenarioCatalog catalog)
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            GameObject playerObject = GameObject.Find("Player");
            GameObject managerObject = GameObject.Find("GameManager");
            GameObject eventSystem = GameObject.Find("EventSystem");
            Require(playerObject != null && managerObject != null && eventSystem != null, "Player, GameManager and EventSystem objects are required.");

            DeleteNamedObject("Patient_Test");
            DeleteNamedObject("TestGround");
            DeleteNamedObject("GoldenHourUI");
            // Legacy v1 components (scripts removed in the cleanup phase) leave missing-script entries; strip them.
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(playerObject);

            Camera camera = playerObject.GetComponentInChildren<Camera>();
            Require(camera != null, "The existing Player Camera child is required.");
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(camera.gameObject);
            camera.transform.localPosition = new Vector3(0f, FirstPersonPlayer.StandingEyeHeight, 0f);
            camera.transform.localRotation = Quaternion.identity;
            camera.nearClipPlane = 0.05f;
            camera.fieldOfView = 70f;
            camera.farClipPlane = 160f;
            camera.gameObject.tag = "MainCamera";
            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            EditorUtility.SetDirty(cameraData);
            if (camera.GetComponent<AudioListener>() == null) camera.gameObject.AddComponent<AudioListener>();
            Viewmodel viewmodel = camera.GetComponent<Viewmodel>();
            if (viewmodel == null) viewmodel = camera.gameObject.AddComponent<Viewmodel>();
            viewmodel.handsModel = OptionalModel("FirstPersonHands");
            EditorUtility.SetDirty(viewmodel);

            CharacterController characterController = playerObject.GetComponent<CharacterController>();
            if (characterController == null) characterController = playerObject.AddComponent<CharacterController>();
            characterController.height = 1.8f;
            characterController.radius = 0.3f;
            characterController.center = new Vector3(0f, 0.9f, 0f);
            characterController.stepOffset = 0.3f;
            characterController.skinWidth = 0.04f;
            FirstPersonPlayer controller = playerObject.GetComponent<FirstPersonPlayer>();
            if (controller == null) controller = playerObject.AddComponent<FirstPersonPlayer>();
            SerializedObject serializedPlayer = new SerializedObject(controller);
            serializedPlayer.FindProperty("cameraTransform").objectReferenceValue = camera.transform;
            serializedPlayer.ApplyModifiedPropertiesWithoutUndo();

            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(managerObject);

            IncidentDirector director = managerObject.GetComponent<IncidentDirector>();
            if (director == null) director = managerObject.AddComponent<IncidentDirector>();
            director.catalog = catalog;
            director.fallbackScenario = catalog.scenarios[0];
            director.player = controller;
            director.playerCamera = camera.transform;
            director.baseMaterial = AssetDatabase.LoadAssetAtPath<Material>(BaseMaterialPath);
            director.unlitMaterial = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath);
            director.outlineMaterial = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
            director.kitPrefab = LoadModel("FirstAidKit");
            director.humanRigPrefab = OptionalModel("RiggedHuman");
            director.childRigPrefab = OptionalModel("RiggedChild");
            director.stretcherPrefab = OptionalModel("Stretcher");
            director.treatmentPropsPrefab = OptionalModel("TreatmentProps");
            director.ambulancePrefab = OptionalModel("Ambulance");
            director.environmentAssets.carPrefab = LoadModel("DamagedCar");
            director.environmentAssets.barrierPrefab = LoadModel("RoadBarrier");
            director.environmentAssets.conePrefab = LoadModel("AccidentProps");
            director.environmentAssets.busPrefab = OptionalModel("Bus");
            director.environmentAssets.policeCarPrefab = OptionalModel("PoliceCar");
            director.environmentAssets.fireTruckPrefab = OptionalModel("FireTruck");
            director.environmentAssets.motorbikePrefab = OptionalModel("Motorbike");
            director.environmentAssets.smokeMaterial = AssetDatabase.LoadAssetAtPath<Material>(SmokeMaterialPath);
            director.environmentAssets.rainMaterial = AssetDatabase.LoadAssetAtPath<Material>(RainMaterialPath);
            EditorUtility.SetDirty(director);

            AccidentEnvironment environment = managerObject.GetComponent<AccidentEnvironment>();
            if (environment == null) environment = managerObject.AddComponent<AccidentEnvironment>();
            environment.carPrefab = director.environmentAssets.carPrefab;
            environment.barrierPrefab = director.environmentAssets.barrierPrefab;
            environment.conePrefab = director.environmentAssets.conePrefab;
            environment.smokeMaterial = director.environmentAssets.smokeMaterial;
            EditorUtility.SetDirty(environment);

            GoldenHourAudio gameAudio = managerObject.GetComponent<GoldenHourAudio>();
            if (gameAudio == null) gameAudio = managerObject.AddComponent<GoldenHourAudio>();
            AssignAudio(gameAudio);
            ConfigureEventSystem(eventSystem);

            AccidentEnvironment.ApplyLighting();
            if (RenderSettings.sun != null) EditorUtility.SetDirty(RenderSettings.sun);
            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            Volume volume = UnityEngine.Object.FindAnyObjectByType<Volume>();
            if (volume == null) volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProfilePath);
            EditorUtility.SetDirty(volume);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MenuScenePath, true), new EditorBuildSettingsScene(GameScenePath, true) };
            PlayerSettings.companyName = "Golden Hour Learning";
            PlayerSettings.productName = "Golden Hour";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
        }

        private static void CreateEventSystem()
        {
            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
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
            audio.footstep = LoadClip("Footstep.wav");
            audio.buttonHover = LoadClip("UI_Hover.wav");
            audio.siren = LoadClip("Siren.wav");
            audio.radioStatic = LoadClip("Radio_Static.wav");
            audio.voiceMurmur = LoadClip("Voice_Murmur.wav");
            audio.heartbeat = LoadClip("Heartbeat.wav");
            audio.breathing = LoadClip("Breathing.wav");
            audio.rainLoop = LoadClip("Rain_Loop.wav");
            audio.windLoop = LoadClip("Wind_Loop.wav");
            audio.musicPad = LoadClip("Music_Pad.wav");
            audio.musicPulse = LoadClip("Music_Pulse.wav");
            audio.musicDrums = LoadClip("Music_Drums.wav");
            audio.metronome = LoadClip("Metronome.wav");
            audio.compression = LoadClip("Compression.wav");
            EditorUtility.SetDirty(audio);
        }

        // --------------------------------------------------------------- helpers

        private static GameObject LoadModel(string name)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath + name + ".fbx");
            Require(model != null, "Missing Blender export " + name + ".fbx");
            return model;
        }

        private static GameObject OptionalModel(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath + name + ".fbx");

        private static AudioClip LoadClip(string filename)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath + filename);
            Require(clip != null, "Missing generated audio asset " + filename);
            return clip;
        }

        private static void ValidateTriageRules()
        {
            PhysiologyState state = new PhysiologyState { canWalk = true, respiratoryRate = 40 };
            Require(StartTriage.Evaluate(state) == TriageTag.Green, "Walking rule failed.");
            state.canWalk = false;
            Require(StartTriage.Evaluate(state) == TriageTag.Red, "Breathing-rate rule failed.");
            state.respiratoryRate = 0;
            Require(StartTriage.Evaluate(state) == TriageTag.Black, "Apnoea rule failed.");
            state.respiratoryRate = 16;
            state.capillaryRefill = 3f;
            Require(StartTriage.Evaluate(state) == TriageTag.Red, "Circulation rule failed.");
            state.capillaryRefill = 1.5f;
            state.followsCommands = false;
            Require(StartTriage.Evaluate(state) == TriageTag.Red, "Commands rule failed.");
            state.followsCommands = true;
            Require(StartTriage.Evaluate(state) == TriageTag.Yellow, "Delayed rule failed.");
            Debug.Log("Triage rule validation passed.");
        }

        private static void DeleteNamedObject(string objectName)
        {
            GameObject target = GameObject.Find(objectName);
            if (target != null) UnityEngine.Object.DestroyImmediate(target);
        }

        private static void DeleteTemplateLeftovers()
        {
            if (AssetDatabase.IsValidFolder("Assets/TutorialInfo")) AssetDatabase.DeleteAsset("Assets/TutorialInfo");
            if (File.Exists("Assets/Readme.asset")) AssetDatabase.DeleteAsset("Assets/Readme.asset");
        }

        /// <summary>Bounds of a model part (all its meshes) in the model root's space, which is how it is attached at runtime.</summary>
        private static Bounds PartBounds(GameObject model, string partName)
        {
            Transform part = TreatmentVisuals.FindDeep(model.transform, partName);
            Require(part != null, model.name + " has no part " + partName + ".");
            Matrix4x4 toRoot = model.transform.worldToLocalMatrix;
            bool any = false;
            Bounds bounds = new Bounds();
            foreach (MeshFilter filter in part.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                Bounds local = filter.sharedMesh.bounds;
                Matrix4x4 matrix = toRoot * filter.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 point = matrix.MultiplyPoint3x4(corner);
                    if (!any) bounds = new Bounds(point, Vector3.zero);
                    else bounds.Encapsulate(point);
                    any = true;
                }
            }

            Require(any, model.name + "/" + partName + " has no meshes.");
            return bounds;
        }

        /// <summary>Phase 5 Blender exports: treatment props and first-person gloved hands (shape, orientation, detail).</summary>
        private static void ValidateTreatmentModels()
        {
            GameObject props = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath + "TreatmentProps.fbx");
            Require(props != null, "Blender export TreatmentProps.fbx is missing.");
            Bounds limb = PartBounds(props, "Dressing_Limb");
            Require(limb.center.magnitude < 0.05f && limb.extents.x > 0.05f && limb.extents.x < 0.1f && limb.extents.z > 0.05f && limb.extents.y < 0.1f,
                "Dressing_Limb must be a band of radius ~0.07 m round the joint's Y axis: " + limb);
            Require(PartBounds(props, "Dressing_Limb_Pad").center.z > 0.04f, "The limb dressing pad must face the front (+Z).");
            Bounds head = PartBounds(props, "Dressing_Head");
            Require(head.extents.x > 0.1f && head.extents.x < 0.16f && head.center.y > 0.1f && head.center.y < 0.2f, "Dressing_Head must ring the head: " + head);
            Require(PartBounds(props, "Dressing_Torso").center.z > 0.08f, "Dressing_Torso must sit on the front of the trunk.");
            Bounds lying = PartBounds(props, "Blanket_Lying");
            Require(lying.size.y > 1.3f && lying.size.x > 0.55f && lying.size.x < 0.95f && lying.center.z > 0f,
                "Blanket_Lying must run along the body (+Y) on its front (+Z): " + lying);
            Bounds shawl = PartBounds(props, "Blanket_Shawl");
            Require(shawl.center.z < 0f && shawl.size.y > 0.5f, "Blanket_Shawl must wrap the back and shoulders: " + shawl);

            GameObject hands = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath + "FirstPersonHands.fbx");
            Require(hands != null, "Blender export FirstPersonHands.fbx is missing.");
            foreach (string side in new[] { "L", "R" })
            {
                Transform hand = TreatmentVisuals.FindDeep(hands.transform, "Hand." + side);
                Require(hand != null, "FirstPersonHands has no Hand." + side + ".");
                int segments = hand.GetComponentsInChildren<MeshFilter>(true)
                    .Count(f => (f.name.StartsWith("Glove_Finger") || f.name.StartsWith("Glove_Thumb")) && !f.name.Contains("_Tip"));
                Require(segments == 10, "Hand." + side + " needs 4 two-segment fingers and a two-segment thumb (found " + segments + ").");
                Bounds fingers = PartBounds(hands, "Glove_Finger3_B_" + side);
                Bounds thumb = PartBounds(hands, "Glove_Thumb_B_" + side);
                Bounds sleeve = PartBounds(hands, "Sleeve_" + side);
                Require(fingers.center.z > 0.12f && sleeve.center.z < -0.1f, "Hand." + side + " must point forward (+Z) from the sleeve.");
                Require(side == "L" ? thumb.center.x > 0.02f : thumb.center.x < -0.02f, "Hand." + side + " thumb must be on the inner side.");
                Debug.Log("Phase 5 model check: Hand." + side + " finger segments " + segments + ", fingertip z " + fingers.max.z.ToString("0.000") +
                          ", thumb x " + thumb.center.x.ToString("0.000"));
            }

            Debug.Log("Phase 5 model check: limb dressing " + limb.size.ToString("0.000") + ", head dressing " + head.size.ToString("0.000") +
                      ", lying blanket " + lying.size.ToString("0.000") + " centre " + lying.center.ToString("0.000") + ", shawl " + shawl.size.ToString("0.000"));
        }

        private static void ValidateRig(string rigName)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath + rigName + ".fbx");
            Require(prefab != null, "Blender export " + rigName + ".fbx is missing.");
            GameObject holder = new GameObject("Rig Check");
            GameObject rig = UnityEngine.Object.Instantiate(prefab, holder.transform);
            rig.transform.localPosition = Vector3.zero;
            rig.transform.localRotation = Quaternion.identity;
            rig.transform.localScale = Vector3.one;
            foreach (string joint in HumanoidBody.JointNames)
            {
                Require(rig.GetComponentsInChildren<Transform>(true).Any(t => t.name == joint), rigName + " is missing joint " + joint);
            }

            bool normalised = HumanoidBody.NormaliseRig(rig.transform, 1f);
            Transform Find(string n) => rig.GetComponentsInChildren<Transform>(true).First(t => t.name == n || t.name.EndsWith("_" + n));
            Vector3 head = Find("Head").position;
            Vector3 nose = Find("Nose").position;
            Vector3 armL = Find("UpperArm.L").position;
            Vector3 hips = Find("Hips").position;
            Debug.Log(rigName + " rig: normalised=" + normalised + " hips y=" + hips.y.ToString("0.00") + " head y=" + head.y.ToString("0.00") +
                      " nose dz=" + (nose.z - head.z).ToString("0.00") + " left arm x=" + armL.x.ToString("0.00") +
                      " renderers=" + rig.GetComponentsInChildren<Renderer>(true).Length);
            Require(normalised, rigName + " has implausible proportions after import.");
            Require(nose.z > head.z, rigName + " does not face +Z after normalising.");
            Require(armL.x < hips.x, rigName + " has its left arm on the right side (mirrored export).");
            UnityEngine.Object.DestroyImmediate(holder);
        }

        private static void ValidateVehicle(string vehicle)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath + vehicle + ".fbx");
            Require(prefab != null, "Blender export " + vehicle + ".fbx is missing.");
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            Bounds bounds = new Bounds(instance.transform.position, Vector3.zero);
            foreach (Renderer part in instance.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(part.bounds);
            Transform front = instance.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == vehicle + "_Front");
            Debug.Log(vehicle + " model: size " + bounds.size.ToString("0.00") + " min y " + bounds.min.y.ToString("0.00") +
                      (front != null ? " front z " + front.position.z.ToString("0.00") : " (no front marker)"));
            Require(bounds.size.z > bounds.size.x && Mathf.Abs(bounds.min.y) < 0.1f, vehicle + " should be ground-standing and longest along Z.");
            Require(front == null || front.position.z > 0f, vehicle + " should face +Z.");
            UnityEngine.Object.DestroyImmediate(instance);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
