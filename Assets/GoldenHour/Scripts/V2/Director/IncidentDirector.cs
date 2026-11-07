using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GoldenHour
{
    public enum IncidentPhase
    {
        Loading,
        Intro,
        Briefing,
        Safety,
        Response,
        Ending,
        Debrief
    }

    /// <summary>
    /// v2 game director: runs one scenario. The golden-hour clock never pauses while panels are open
    /// (only the pause menu stops it). Every decision is logged to the score ledger and telemetry.
    /// </summary>
    public sealed partial class IncidentDirector : MonoBehaviour
    {
        public const string MainMenuScene = "MainMenu";
        public const float InteractionRange = 3.4f;

        public const string Disclaimer =
            "Educational simulation only - not medical training or medical advice. The content has NOT been medically " +
            "reviewed and should be checked by a qualified first-aid trainer. In a real emergency call your local emergency number.";

        public static IncidentDirector Instance { get; private set; }

        [Header("Data")]
        public ScenarioCatalog catalog;
        public ScenarioData fallbackScenario;

        [Header("Scene references")]
        public FirstPersonPlayer player;
        public Transform playerCamera;

        [Header("Assets")]
        public Material baseMaterial;
        public Material unlitMaterial;
        public Material outlineMaterial;
        public GameObject kitPrefab;
        public GameObject humanRigPrefab;
        public GameObject childRigPrefab;
        public GameObject ambulancePrefab;
        public GameObject stretcherPrefab;
        public EnvironmentAssets environmentAssets = new EnvironmentAssets();

        private readonly List<Casualty> casualties = new List<Casualty>();
        private readonly List<Hazard> hazards = new List<Hazard>();
        private readonly List<Ambulance> ambulances = new List<Ambulance>();
        private readonly List<Bystander> bystanders = new List<Bystander>();
        private readonly ScoreLedger ledger = new ScoreLedger();
        private readonly RunRecord run = new RunRecord();
        private readonly HashSet<int> spawnedWaves = new HashSet<int>();
        private readonly HashSet<int> warnedWaves = new HashSet<int>();
        private readonly HashSet<int> playedCues = new HashSet<int>();
        private readonly Dictionary<Hazard, float> exposure = new Dictionary<Hazard, float>();
        private GoldenHourAudio audioManager;
        private IncidentHud hud;
        private ModalWindow modal;
        private FirstAidKitInteractable kit;
        private float nextPathSample;
        private float realStart;
        private bool paused;
        private bool shoutCredited;
        private int goldenHourWarnings;
        private int tutorialStep = -1;

        public ScenarioData Scenario { get; private set; }
        public IncidentPhase Phase { get; private set; } = IncidentPhase.Loading;
        public float Minute { get; private set; }
        public float RemainingMinutes => Mathf.Max(0f, Scenario.goldenHourMinutes - Minute);
        public float DispatchMinute { get; private set; } = -1f;
        public bool Dispatched => DispatchMinute >= 0f;
        public bool KitCollected { get; private set; }
        public bool GlovesOn { get; private set; }
        public int Gloves { get; private set; }
        public int Dressings { get; private set; }
        public int Blankets { get; private set; }
        public int MaximumScore { get; private set; }
        public float DeteriorationScale { get; private set; } = 1f;
        public string DifficultyBand { get; private set; } = "Standard";
        public bool HintsEnabled { get; private set; }
        public int Seed { get; private set; }
        public string EndReason { get; private set; } = string.Empty;
        public IReadOnlyList<Casualty> Casualties => casualties;
        public IReadOnlyList<Hazard> Hazards => hazards;
        public IReadOnlyList<Ambulance> Ambulances => ambulances;
        public IReadOnlyList<Bystander> Bystanders => bystanders;
        public ScoreLedger Ledger => ledger;
        public RunRecord Run => run;
        public IncidentHud Hud => hud;
        public ModalWindow Modal => modal;
        public FirstAidKitInteractable Kit => kit;
        public bool Paused => paused;
        public bool Finished => Phase == IncidentPhase.Ending || Phase == IncidentPhase.Debrief;

        /// <summary>Test hook: timed actions complete at once but still cost their simulated time.</summary>
        public bool InstantActions { get; set; }

        /// <summary>Raised when a run ends (after the record is saved).</summary>
        public event Action<RunRecord> RunFinished;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SpeechBark.Spoken -= OnSpeech;
            if (player != null && audioManager != null) player.Stepped -= audioManager.PlayFootstep;
        }

        private void Start()
        {
            Time.timeScale = Session.TimeScaleOverride > 0f ? Session.TimeScaleOverride : 1f;
            GameInput.Ensure();
            MaterialFactory.Template = baseMaterial;
            MaterialFactory.UnlitTemplate = unlitMaterial;
            InteractionOutline.OutlineMaterial = outlineMaterial;
            Scenario = catalog != null ? catalog.Find(Session.ScenarioId) : null;
            if (Scenario == null) Scenario = fallbackScenario;
            if (player == null) player = FindAnyObjectByType<FirstPersonPlayer>();
            if (player != null && playerCamera == null && player.PlayerCamera != null) playerCamera = player.PlayerCamera.transform;
            audioManager = GetComponent<GoldenHourAudio>();
            UiFactory.ButtonPressed = audioManager != null ? audioManager.PlayButton : (Action)null;
            ButtonFeedback.Hovered = audioManager != null ? audioManager.PlayHover : (Action)null;
            if (player != null && audioManager != null) player.Stepped += audioManager.PlayFootstep;
            if (GetComponent<StressSystem>() == null) gameObject.AddComponent<StressSystem>();
            if (GetComponent<PhotoMode>() == null) gameObject.AddComponent<PhotoMode>();
            SpeechBark.Spoken += OnSpeech;

            ConfigureDifficulty();
            Seed = Session.SeedOverride ?? Environment.TickCount;
            ScenarioEnvironment.Build(Scenario, GetComponent<AccidentEnvironment>(), environmentAssets,
                playerCamera != null ? playerCamera.GetComponent<Camera>() : null);
            SpawnKit();
            SpawnCasualties();
            SpawnHazards();
            SpawnBystanders();
            if (player != null) player.Teleport(Scenario.playerStart, Scenario.playerYaw, 4f);

            GraphicsApplier.Apply();
            if (audioManager != null)
            {
                audioManager.StartMusic();
                audioManager.StartWeather(Scenario.rain, Scenario.environment != EnvironmentKind.TrainingYard);
            }

            Canvas canvas = UiFactory.CreateCanvas("Incident UI");
            canvas.gameObject.AddComponent<GamepadSelector>();
            hud = new IncidentHud(canvas.transform);
            modal = new ModalWindow(canvas.transform);
            MaximumScore = ComputeMaximumScore();
            realStart = Time.realtimeSinceStartup;
            InitialiseRun();
            OnStartPresentation();
            SceneFader.FadeIn();
        }

        private void ConfigureDifficulty()
        {
            Difficulty mode = Session.DifficultyOverride ?? GameSettings.Difficulty;
            if (mode == Difficulty.Easy)
            {
                DeteriorationScale = 0.8f;
                DifficultyBand = "Easy";
                HintsEnabled = true;
            }
            else if (mode == Difficulty.Normal)
            {
                DeteriorationScale = 1f;
                DifficultyBand = "Normal";
                HintsEnabled = false;
            }
            else
            {
                DeteriorationScale = AdaptiveDifficulty.DeteriorationScale(ProfileStore.Load().recentRatios, out string band);
                DifficultyBand = "Adaptive (" + band + ")";
                HintsEnabled = band == "Support";
            }

            if (Scenario != null && Scenario.tutorial)
            {
                DeteriorationScale = Mathf.Min(DeteriorationScale, 0.8f);
                HintsEnabled = true;
            }
        }

        // ------------------------------------------------------------ spawning

        private void SpawnKit()
        {
            GameObject kitObject = new GameObject("First Aid Bag");
            kitObject.transform.SetPositionAndRotation(Scenario.kitPosition, Quaternion.Euler(0f, -20f, 0f));
            Transform lid = null;
            if (kitPrefab != null)
            {
                GameObject model = Instantiate(kitPrefab, kitObject.transform);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                foreach (Transform child in model.GetComponentsInChildren<Transform>())
                {
                    if (child.name == "KitHinge") lid = child;
                }
            }

            BoxCollider box = kitObject.AddComponent<BoxCollider>();
            box.size = new Vector3(1.3f, 0.75f, 1f);
            box.center = new Vector3(0f, 0.37f, 0f);
            kit = kitObject.AddComponent<FirstAidKitInteractable>();
            kit.Initialize(lid);
            kitObject.AddComponent<KitPickup>();
        }

        private void SpawnCasualties()
        {
            Transform group = new GameObject("Casualties").transform;
            List<CasualtyPlacement> placements = new List<CasualtyPlacement>(Scenario.casualties);
            // Replayability: outside the tutorial and fixed-seed tests, people swap positions between runs.
            int[] order = new int[placements.Count];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            if (!Scenario.tutorial && Session.SeedOverride == null)
            {
                System.Random shuffle = new System.Random(Seed);
                for (int i = order.Length - 1; i > 0; i--)
                {
                    int j = shuffle.Next(i + 1);
                    (order[i], order[j]) = (order[j], order[i]);
                }
            }

            for (int i = 0; i < placements.Count; i++)
            {
                CasualtyPlacement slot = placements[order[i]];
                CasualtyPlacement placement = new CasualtyPlacement
                {
                    profile = placements[i].profile, position = slot.position, yaw = slot.yaw
                };
                Casualty casualty = new GameObject(placement.profile.casualtyId).AddComponent<Casualty>();
                casualty.transform.SetParent(group, false);
                GameObject rig = placement.profile.variant == BodyVariant.Child && childRigPrefab != null ? childRigPrefab : humanRigPrefab;
                casualty.Initialize(placement, Seed * 31 + i * 7919, DeteriorationScale, rig);
                casualties.Add(casualty);
            }
        }

        private void SpawnHazards()
        {
            foreach (HazardPlacement placement in Scenario.hazards)
            {
                Hazard hazard = new GameObject("Hazard").AddComponent<Hazard>();
                hazard.Initialize(placement, environmentAssets.smokeMaterial, unlitMaterial);
                hazards.Add(hazard);
            }
        }

        private void SpawnBystanders()
        {
            Vector3 centre = SceneCentre();
            for (int i = 0; i < Scenario.bystanders; i++)
            {
                Bystander bystander = new GameObject("Bystander " + (i + 1)).AddComponent<Bystander>();
                Vector3 start = centre + new Vector3(-5f + i * 4f, 0f, -4f + (i % 2) * 3f);
                bystander.Initialize(start, centre, Scenario.greenZone, Seed + 500 + i, humanRigPrefab);
                bystanders.Add(bystander);
            }
        }

        public Vector3 SceneCentre()
        {
            if (Scenario.casualties.Count == 0) return Scenario.playerStart;
            Vector3 sum = Vector3.zero;
            foreach (CasualtyPlacement placement in Scenario.casualties) sum += placement.position;
            Vector3 centre = sum / Scenario.casualties.Count;
            centre.y = 0f;
            return centre;
        }

        // ---------------------------------------------------------------- update

        private void Update()
        {
            if (hud == null) return;
            hud.Tick();
            UpdateAction();
            bool minigameWasActive = FirstAidMinigame.Active;
            FirstAidMinigame.Tick();
            if (Phase == IncidentPhase.Intro) UpdateIntro();
            if (Phase == IncidentPhase.Ending && Cutscene.Playing &&
                (GameInput.Pressed(GameInput.Pause) || GameInput.Pressed(GameInput.Interact) || GameInput.Pressed(GameInput.Primary))) Cutscene.Skip();
            if (PhotoMode.Instance != null && (PhotoMode.Instance.Active || PhotoMode.Instance.ExitFrame == Time.frameCount)) return;
            if (Phase != IncidentPhase.Safety && Phase != IncidentPhase.Response) return;

            if (GameInput.Pressed(GameInput.Pause) && !minigameWasActive)
            {
                if (paused) ResumeFromPause();
                else if (modal.IsOpen && modal.Closable && !ActionRunning) CloseModal();
                else if (!modal.IsOpen) ShowPauseMenu();
                return;
            }

            if (paused) return;
            AdvanceSimulation(Time.deltaTime * Scenario.simMinutesPerSecond);
            if (Finished) return;
            UpdateWaves();
            UpdateRadioCues();
            UpdateDanger();
            SamplePath();
            UpdateTutorial();

            if (!modal.IsOpen && !ActionRunning)
            {
                UpdatePrompt();
                UpdateCompass();
                if (GameInput.Pressed(GameInput.Interact)) TryInteract();
                else if (GameInput.Pressed(GameInput.Radio)) OpenRadio();
                else if (GameInput.Pressed(GameInput.Shout)) ShoutWalkingWounded();
                else if (GameInput.Pressed(GameInput.Gloves)) PutOnGloves();
                else if (GameInput.Pressed(GameInput.Tablet)) OpenTablet();
            }
            else if (modal.IsOpen && modal.Title == TabletTitle && GameInput.Pressed(GameInput.Tablet))
            {
                CloseModal();
            }

            RefreshHud();
            if (Phase == IncidentPhase.Response && AllResolved()) EndScenario("Scene cleared - every casualty is transported, in the green zone or expectant.");
        }

        /// <summary>Advances the golden-hour clock and every casualty's physiology.</summary>
        public void AdvanceSimulation(float minutes)
        {
            if (minutes <= 0f || Finished) return;
            Minute += minutes;
            foreach (Casualty casualty in casualties)
            {
                bool wasDead = casualty.Sim.State.deceased;
                bool wasWorse = casualty.Sim.Worsened;
                casualty.Sim.Advance(minutes);
                if (!wasDead && casualty.Sim.State.deceased) OnDeath(casualty);
                else if (!wasWorse && casualty.Sim.Worsened)
                {
                    Log("worsened", casualty.Id, casualty.Sim.Events[casualty.Sim.Events.Count - 1].text);
                    casualty.Say(casualty.Sim.State.followsCommands ? "I... I feel worse..." : string.Empty);
                }

                casualty.Tick();
            }

            if (Minute >= Scenario.goldenHourMinutes) EndScenario("The golden hour has run out.");
            else if (goldenHourWarnings == 0 && RemainingMinutes <= 10f)
            {
                goldenHourWarnings = 1;
                Radio("Incident command: ten minutes left in the golden hour. Prioritise RED casualties for transport.");
            }
        }

        private void OnDeath(Casualty casualty)
        {
            casualty.Body.Limp = true;
            Log("death", casualty.Id, casualty.Sim.Events.Count > 0 ? casualty.Sim.Events[casualty.Sim.Events.Count - 1].text : "died");
            if (casualty.Sim.PreventableDeath)
            {
                Score("death", ScoreRules.PreventableDeath, casualty.Id + " died (" + casualty.Profile.condition + " not controlled in time)",
                    casualty.Id);
                Announce(casualty.Id + " has stopped breathing and died.");
            }
        }

        private void UpdateWaves()
        {
            if (!Dispatched) return;
            for (int i = 0; i < Scenario.waves.Count; i++)
            {
                float arrival = DispatchMinute + Scenario.waves[i].minute;
                if (!warnedWaves.Contains(i) && Minute >= arrival - 2f)
                {
                    warnedWaves.Add(i);
                    Radio("Control: Ambulance " + (i + 1) + " is two minutes out with " + Scenario.waves[i].capacity +
                          " stretcher place" + (Scenario.waves[i].capacity == 1 ? "" : "s") + ".");
                }

                if (!spawnedWaves.Contains(i) && Minute >= arrival) SpawnAmbulance(i);
            }
        }

        /// <summary>Spawns ambulance wave <paramref name="index"/> (tests may call this to skip waiting).</summary>
        public Ambulance SpawnAmbulance(int index)
        {
            if (spawnedWaves.Contains(index)) return ambulances.Find(a => a.Number == index + 1);
            spawnedWaves.Add(index);
            Ambulance.StretcherModel = stretcherPrefab;
            Ambulance ambulance = new GameObject("Ambulance").AddComponent<Ambulance>();
            Vector3 road = Scenario.ambulanceBay - Scenario.ambulanceSpawn;
            road.y = 0f;
            Vector3 right = Vector3.Cross(Vector3.up, road.normalized);
            Vector3 offset = right * (index % 2 == 0 ? 0f : 3.1f);
            ambulance.Initialize(index + 1, Scenario.waves[index].capacity, Scenario.ambulanceSpawn + offset,
                Scenario.ambulanceBay + offset, SceneCentre(), ambulancePrefab, humanRigPrefab);
            ambulances.Add(ambulance);
            if (audioManager != null) audioManager.PlaySiren(ambulance.transform);
            Log("ambulance", string.Empty, "Ambulance " + (index + 1) + " dispatched to the scene");
            return ambulance;
        }

        public void OnAmbulanceArrived(Ambulance ambulance)
        {
            Radio("Ambulance " + ambulance.Number + " on scene. Hand over your highest-priority casualties to the crew.");
            Log("ambulance", string.Empty, "Ambulance " + ambulance.Number + " arrived");
        }

        public void OnCasualtyLoaded(Ambulance ambulance, Casualty casualty)
        {
            casualty.Sim.Transport();
            Log("transport", casualty.Id, "Loaded into ambulance " + ambulance.Number);
            hud.SetStatus(casualty.Id + " is on the way to hospital in ambulance " + ambulance.Number + ".");
        }

        public void OnAmbulanceDeparted(Ambulance ambulance)
        {
            Log("ambulance", string.Empty, "Ambulance " + ambulance.Number + " departed with " + ambulance.Loaded.Count + " casualties");
        }

        private void UpdateRadioCues()
        {
            for (int i = 0; i < Scenario.radio.Count; i++)
            {
                if (playedCues.Contains(i) || Minute < Scenario.radio[i].minute) continue;
                playedCues.Add(i);
                Radio(Scenario.radio[i].text);
            }
        }

        private void UpdateDanger()
        {
            if (player == null) return;
            string warning = string.Empty;
            foreach (Hazard hazard in hazards)
            {
                if (!hazard.Dangerous) continue;
                if (hazard.DistanceTo(player.transform.position) < hazard.Radius)
                {
                    warning = "DANGER: too close to the " + hazard.DisplayName.ToLowerInvariant() + " - step back";
                    exposure.TryGetValue(hazard, out float seconds);
                    seconds += Time.deltaTime;
                    exposure[hazard] = seconds;
                    if (seconds > 2f && !hazard.ExposurePenaltyApplied)
                    {
                        hazard.ExposurePenaltyApplied = true;
                        Score("safety", ScoreRules.DangerZone, "Stayed in the danger zone of the " + hazard.DisplayName.ToLowerInvariant());
                    }
                }
                else
                {
                    exposure[hazard] = 0f;
                }
            }

            InDanger = warning.Length > 0;
            hud.SetDanger(warning);
        }

        /// <summary>True while the player stands inside an unmarked or dangerous hazard radius.</summary>
        public bool InDanger { get; private set; }

        private void SamplePath()
        {
            if (player == null || Time.realtimeSinceStartup < nextPathSample) return;
            nextPathSample = Time.realtimeSinceStartup + 0.5f;
            run.path.Add(new PathSample { minute = Minute, x = player.transform.position.x, z = player.transform.position.z });
        }

        // ----------------------------------------------------------- interaction

        private IInteractable FindTarget()
        {
            if (playerCamera == null) return null;
            Ray ray = new Ray(playerCamera.position, playerCamera.forward);
            RaycastHit[] hits = Physics.SphereCastAll(ray, 0.12f, InteractionRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (player != null && hit.collider.transform.IsChildOf(player.transform)) continue;
                IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
                if (interactable != null && interactable.CanInteract) return interactable;
                if (!hit.collider.isTrigger) break;
            }

            return null;
        }

        private void UpdatePrompt()
        {
            IInteractable target = FindTarget();
            hud.SetPrompt(target != null ? "[" + GameInput.KeyName(GameInput.Interact) + "]  " + target.Prompt : string.Empty);
            if (target is MonoBehaviour behaviour) InteractionOutline.Show(behaviour.gameObject);
            else InteractionOutline.Show(null);
        }

        public bool TryInteract()
        {
            IInteractable target = FindTarget();
            if (target == null) return false;
            target.Interact();
            return true;
        }

        private void UpdateCompass()
        {
            if (playerCamera == null) return;
            Vector3 origin = playerCamera.position;
            Casualty nearest = null;
            float best = float.MaxValue;
            if (Dispatched)
            {
                foreach (Casualty casualty in casualties)
                {
                    if (casualty.Record.HasTag || !casualty.CanInteract || !casualty.gameObject.activeInHierarchy) continue;
                    float distance = (casualty.FocusPoint - origin).sqrMagnitude;
                    if (distance < best)
                    {
                        best = distance;
                        nearest = casualty;
                    }
                }
            }

            string label;
            Vector3 target;
            if (!KitCollected)
            {
                target = kit.transform.position;
                label = "First-aid bag";
            }
            else if (nearest != null)
            {
                target = nearest.FocusPoint;
                label = "Untagged  <b>" + nearest.Id + "</b>";
            }
            else
            {
                Ambulance waiting = ambulances.Find(a => a.State == AmbulanceState.Waiting && a.FreeSpaces > 0);
                if (waiting == null || waiting.Lead == null)
                {
                    hud.SetCompass(false, 0f, string.Empty);
                    return;
                }

                target = waiting.Lead.transform.position;
                label = "Ambulance " + waiting.Number;
            }

            Vector3 toTarget = target - origin;
            toTarget.y = 0f;
            Vector3 forward = playerCamera.forward;
            forward.y = 0f;
            float angle = forward.sqrMagnitude > 0.0001f && toTarget.sqrMagnitude > 0.0001f
                ? Vector3.SignedAngle(forward, toTarget, Vector3.up) : 0f;
            hud.SetCompass(true, angle, label + "   " + Mathf.RoundToInt(toTarget.magnitude) + " m");
        }

        // ------------------------------------------------------ player actions

        public void CollectKit()
        {
            if (KitCollected) return;
            KitCollected = true;
            kit.Open();
            if (audioManager != null) audioManager.PlayKitOpen();
            Gloves = Scenario.gloves;
            Dressings = Scenario.dressings;
            Blankets = Scenario.blankets;
            Log("kit", string.Empty, "Collected the first-aid bag");
            hud.SetStatus("First-aid bag collected: " + Gloves + " pairs of gloves, " + Dressings + " pressure dressings, " + Blankets +
                          " blankets. Put gloves on before touching anyone (" + GameInput.KeyName(GameInput.Gloves) + ").", 7f);
        }

        public void PutOnGloves()
        {
            if (GlovesOn || ActionRunning) return;
            if (!KitCollected)
            {
                hud.SetStatus("The gloves are in the first-aid bag. Collect it first.");
                return;
            }

            if (Gloves <= 0)
            {
                hud.SetStatus("No gloves left.");
                return;
            }

            RunAction("Putting on gloves", 2f, false, () =>
            {
                Gloves--;
                GlovesOn = true;
                Log("gloves", string.Empty, "Put on gloves");
                hud.SetStatus("Gloves on.");
                if (Viewmodel.Instance != null) Viewmodel.Instance.SetGloves(true);
            });
        }

        public void MarkHazard(Hazard hazard)
        {
            if (hazard.Marked) return;
            hazard.Mark();
            Score("safety", ScoreRules.HazardMarked, "Marked hazard: " + hazard.DisplayName);
        }

        public void CalmBystander(Bystander bystander)
        {
            if (bystander.Calmed) return;
            bystander.Calm();
            Score("bystander", ScoreRules.BystanderCalmed, "Calmed a bystander and sent them to the green zone");
        }

        /// <summary>"If you can walk, go to the green zone": every casualty who can walk heads there.</summary>
        public int ShoutWalkingWounded()
        {
            if (!Dispatched)
            {
                hud.SetStatus("Make the scene safe and send the dispatch report first (" + GameInput.KeyName(GameInput.Radio) + ").");
                return 0;
            }

            if (player != null && player.transform != null) hud.ShowSubtitle("You: \"If you can walk, go to the green zone by the flag!\"", 3.5f);
            int moving = 0;
            foreach (Casualty casualty in casualties)
            {
                if (!casualty.Sim.State.canWalk || casualty.Sim.State.deceased || casualty.InGreenZone || casualty.HeadingToGreenZone ||
                    casualty.OnStretcher || casualty.Sim.Transported) continue;
                moving++;
                casualty.HeadingToGreenZone = true;
                casualty.Say("Okay, I can walk...");
                Vector3 spot = Scenario.greenZone + new Vector3((moving % 3 - 1) * 1.3f, 0f, (moving / 3 - 0.5f) * 1.2f);
                Casualty current = casualty;
                casualty.Walker.GoTo(spot, () => ArriveGreenZone(current));
            }

            Log("command", string.Empty, "Shouted for the walking wounded: " + moving + " moving to the green zone");
            if (moving > 0 && !shoutCredited)
            {
                shoutCredited = true;
                Score("command", ScoreRules.WalkingCommand, "Directed the walking wounded to the green zone (START step 1)");
            }
            else if (moving == 0)
            {
                hud.SetStatus("Nobody else can walk to the green zone.");
            }

            return moving;
        }

        public void ArriveGreenZone(Casualty casualty)
        {
            casualty.HeadingToGreenZone = false;
            casualty.InGreenZone = true;
            casualty.ApplyRestPose();
            if (!casualty.Record.HasTag && !casualty.Sim.State.deceased)
            {
                casualty.Record.RecordExamination(Minute, "walked to the green zone when asked");
                TriageTag truth = casualty.Sim.Evaluate(out StartStep step);
                int points = casualty.Record.ApplyTag(Minute, TriageTag.Green, truth, step, out _);
                casualty.AutoTaggedGreen = true;
                casualty.SetTag(TriageTag.Green);
                Score("triage", points, casualty.Id + " walked to the green zone: GREEN (START step 1)", casualty.Id);
            }
        }

        // ------------------------------------------------------- timed actions

        private string actionLabel;
        private float actionDuration;
        private float actionProgress;
        private bool actionHold;
        private Action actionDone;

        public bool ActionRunning => actionDone != null || FirstAidMinigame.Active;

        /// <summary>
        /// Runs a timed action while the clock keeps running. Hold actions only progress while the
        /// hold input is pressed. With <see cref="InstantActions"/>, completes at once but still costs the time.
        /// </summary>
        public void RunAction(string label, float seconds, bool hold, Action done)
        {
            if (ActionRunning) return;
            if (InstantActions)
            {
                AdvanceSimulation(seconds * Scenario.simMinutesPerSecond);
                done?.Invoke();
                return;
            }

            actionLabel = label;
            actionDuration = Mathf.Max(0.1f, seconds);
            actionProgress = 0f;
            actionHold = hold;
            actionDone = done;
            if (Viewmodel.Instance != null) Viewmodel.Instance.Hold(Viewmodel.ItemFor(label));
        }

        private void UpdateAction()
        {
            if (actionDone == null)
            {
                hud?.SetProgress(string.Empty, 0f, false);
                return;
            }

            if (paused) return;
            bool advancing = !actionHold || GameInput.Held(GameInput.Primary);
            actionProgress = advancing ? actionProgress + Time.deltaTime : Mathf.Max(0f, actionProgress - Time.deltaTime * 0.5f);
            string label = actionHold && !advancing
                ? actionLabel + "  -  HOLD " + GameInput.KeyName(GameInput.Primary) + " to keep pressure"
                : actionLabel;
            hud.SetProgress(label, actionProgress / actionDuration, true);
            if (actionProgress >= actionDuration)
            {
                Action done = actionDone;
                actionDone = null;
                hud.SetProgress(string.Empty, 0f, false);
                if (Viewmodel.Instance != null) Viewmodel.Instance.Hold(null);
                done?.Invoke();
            }
        }

        // ----------------------------------------------------------- scoring

        public ScoreEntry Score(string category, int points, string reason, string casualtyId = "")
        {
            ScoreEntry entry = ledger.Add(Minute, category, points, reason, casualtyId);
            Log("score", casualtyId, (points >= 0 ? "+" : "") + points + " " + reason);
            if (points != 0 && hud != null) hud.Toast(points, reason);
            return entry;
        }

        public void Log(string type, string casualtyId, string detail)
        {
            run.events.Add(new TelemetryEvent
            {
                minute = Minute, realSeconds = Time.realtimeSinceStartup - realStart, type = type, casualtyId = casualtyId ?? string.Empty,
                detail = detail
            });
        }

        public void Announce(string text)
        {
            if (hud != null) hud.SetStatus(text, 6f);
            Log("announce", string.Empty, text);
        }

        public void Radio(string text)
        {
            if (hud != null) hud.ShowRadio(text);
            if (audioManager != null) audioManager.PlayRadio();
            Log("radio", string.Empty, text);
        }

        private void OnSpeech(string text, Vector3 position)
        {
            if (player == null || hud == null) return;
            if (Vector3.Distance(position, player.transform.position) < 9f) hud.ShowSubtitle("\"" + text + "\"");
            if (audioManager != null) audioManager.PlayVoice(position);
        }

        private int ComputeMaximumScore()
        {
            int total = 0;
            int dressingsNeeded = 0;
            int blanketsNeeded = 0;
            int walkers = 0;
            int stretcherCases = 0;
            foreach (CasualtyPlacement placement in Scenario.casualties)
            {
                CasualtyProfile profile = placement.profile;
                total += ScoreRules.TriageCorrect;
                if (profile.initial.canWalk) walkers++;
                bool expectant = profile.initial.respiratoryRate <= 0 || profile.initial.deceased;
                if (!expectant && !profile.initial.canWalk) stretcherCases++;
                if (expectant) continue;
                Treatment indicated = profile.indicatedTreatments;
                if ((indicated & Treatment.PressureDressing) != 0) dressingsNeeded++;
                if ((indicated & Treatment.Blanket) != 0) blanketsNeeded++;
                if ((indicated & Treatment.AirwayOpening) != 0) total += ScoreRules.TreatmentEffective;
                if ((indicated & Treatment.RecoveryPosition) != 0) total += ScoreRules.TreatmentEffective;
                if ((indicated & Treatment.Reassure) != 0) total += ScoreRules.TreatmentEffective;
            }

            total += Mathf.Min(dressingsNeeded, Scenario.dressings) * ScoreRules.TreatmentEffective;
            total += Mathf.Min(blanketsNeeded, Scenario.blankets) * ScoreRules.TreatmentEffective;
            total += Mathf.Min(stretcherCases, Scenario.TotalAmbulanceCapacity) * ScoreRules.HandoverCorrect;
            total += Scenario.hazards.Count * ScoreRules.HazardMarked;
            total += ScoreRules.DispatchFields * ScoreRules.DispatchField;
            if (walkers > 0) total += ScoreRules.WalkingCommand;
            total += Scenario.bystanders * ScoreRules.BystanderCalmed;
            total += ScoreRules.MaxTimeBonus;
            return total;
        }

        /// <summary>True when every casualty is transported, in the green zone, dead, or tagged expectant.</summary>
        public bool AllResolved()
        {
            if (casualties.Count == 0) return false;
            foreach (Casualty casualty in casualties)
            {
                if (casualty.Sim.Transported || casualty.Sim.State.deceased) continue;
                if (casualty.InGreenZone && casualty.Record.HasTag) continue;
                if (casualty.Record.HasTag && casualty.Record.CurrentTag == TriageTag.Black) continue;
                return false;
            }

            return true;
        }

        // -------------------------------------------------------------- ending

        public void EndScenario(string reason)
        {
            if (Finished) return;
            Phase = IncidentPhase.Ending;
            EndReason = reason;
            CloseModal();
            if (player != null) player.SetControlsEnabled(false);
            Log("end", string.Empty, reason);
            bool cleared = AllResolved();
            if (cleared) Score("time", ScoreRules.TimeBonus(RemainingMinutes), "Time bonus: " + Mathf.FloorToInt(RemainingMinutes) + " min of the golden hour left");
            FinaliseRun();
            OnEndPresentation();
        }

        private void InitialiseRun()
        {
            run.scenarioId = Scenario.scenarioId;
            run.scenarioTitle = Scenario.title;
            run.seed = Seed;
            run.difficultyBand = DifficultyBand;
            run.deteriorationScale = DeteriorationScale;
            run.startedUtc = DateTime.UtcNow.ToString("o");
            run.maximumScore = MaximumScore;
        }

        private void FinaliseRun()
        {
            run.durationMinutes = Minute;
            run.realSeconds = Time.realtimeSinceStartup - realStart;
            run.score = ledger.Total;
            run.ledger = new List<ScoreEntry>(ledger.Entries);
            run.casualties.Clear();
            int preventable = 0;
            int survivors = 0;
            foreach (Casualty casualty in casualties)
            {
                CasualtySim sim = casualty.Sim;
                if (sim.PreventableDeath) preventable++;
                if (!sim.State.deceased) survivors++;
                TriageTag finalTruth = sim.Evaluate(out _);
                CasualtyOutcome outcome = new CasualtyOutcome
                {
                    casualtyId = casualty.Id,
                    displayName = casualty.Profile.displayName,
                    firstTag = casualty.Record.FirstTag.ToString(),
                    startTruthAtFirstTag = casualty.Record.HasTag ? casualty.Record.FirstTruth.ToString() : "-",
                    startStep = casualty.Record.HasTag ? casualty.Record.FirstTruthStep.ToString() : sim.Evaluate(out StartStep step).ToString() + "/" + step,
                    finalTag = casualty.Record.CurrentTag.ToString(),
                    finalTruth = finalTruth.ToString(),
                    outcome = OutcomeText(casualty),
                    decisionMinutes = casualty.Record.DecisionMinutes,
                    retags = casualty.Record.Retags,
                    treatments = sim.Applied.ToString()
                };
                outcome.history.AddRange(casualty.Record.Log);
                foreach (SimEvent simEvent in sim.Events) outcome.history.Add(simEvent.minute.ToString("0.0") + " min: " + simEvent.text);
                run.casualties.Add(outcome);
            }

            run.preventableDeaths = preventable;
            run.survivors = survivors;
            run.stars = ScoreRules.Stars(ledger.Total, MaximumScore, preventable);
            run.feedback = DebriefFeedback.Build(this);

            PlayerProfileData profile = ProfileStore.Load();
            profile.RecordRun(Scenario.scenarioId, ledger.Total, MaximumScore, run.stars, Scenario.nextScenarioId);
            ProfileStore.Save();
            Session.LastRun = run;
            Session.LastRunPath = ProfileStore.SaveRun(run);
            RunFinished?.Invoke(run);
        }

        public string OutcomeText(Casualty casualty)
        {
            CasualtySim sim = casualty.Sim;
            if (sim.State.deceased)
            {
                return sim.InitiallyExpectant ? "Expectant - not breathing after airway opening" : "Died - preventable in this model";
            }

            if (sim.Transported) return "Transported to hospital alive";
            if (casualty.InGreenZone) return "Walked to the green zone (minor)";
            return "Alive on scene at the end";
        }

        // ------------------------------------------------------------ navigation

        public void RestartScenario()
        {
            Time.timeScale = 1f;
            SceneFader.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void ReturnToMenu()
        {
            Time.timeScale = 1f;
            SceneFader.LoadScene(MainMenuScene);
        }

        /// <summary>True when a next scenario exists and the saved profile has unlocked it.</summary>
        public bool HasNextScenario => Scenario != null && !string.IsNullOrEmpty(Scenario.nextScenarioId) &&
                                       ProfileStore.Load().Get(Scenario.nextScenarioId).unlocked;

        public void PlayNextScenario()
        {
            if (!HasNextScenario) return;
            if (!string.IsNullOrEmpty(Scenario.nextScenarioId)) Session.ScenarioId = Scenario.nextScenarioId;
            RestartScenario();
        }

        /// <summary>Closes the open panel and returns control to the player.</summary>
        public void ClosePanel()
        {
            if (Phase == IncidentPhase.Debrief) return;
            CloseModal();
        }

        private void CloseModal()
        {
            modal.Close();
            examTarget = null;
            if (player != null)
            {
                player.ForcedKneel = false;
                if (!Finished && !paused && Phase != IncidentPhase.Briefing && Phase != IncidentPhase.Intro) player.SetControlsEnabled(true);
            }
        }

        private void OpenPanel(string title, string body, Vector2 size, float reserved, bool closable)
        {
            if (player != null) player.SetControlsEnabled(false);
            hud.SetPrompt(string.Empty);
            hud.SetCompass(false, 0f, string.Empty);
            InteractionOutline.Show(null);
            modal.Show(title, body, size, reserved, closable);
        }

        private void RefreshHud()
        {
            StringBuilder objectives = new StringBuilder("<b>" + Scenario.title.ToUpperInvariant() + "</b>\n");
            int marked = hazards.FindAll(h => h.Marked).Count;
            if (Phase == IncidentPhase.Safety)
            {
                objectives.Append(Check(KitCollected)).Append("Collect the first-aid bag\n");
                objectives.Append(Check(marked == hazards.Count)).Append("Spot and mark hazards (").Append(marked).Append(")\n");
                objectives.Append(Check(Dispatched)).Append("Radio the dispatch report to 112 (").Append(GameInput.KeyName(GameInput.Radio)).Append(")\n");
                objectives.Append(Check(GlovesOn)).Append("Put on gloves (").Append(GameInput.KeyName(GameInput.Gloves)).Append(")");
            }
            else
            {
                int tagged = casualties.FindAll(c => c.Record.HasTag).Count;
                int transported = casualties.FindAll(c => c.Sim.Transported).Count;
                int dead = casualties.FindAll(c => c.Sim.State.deceased).Count;
                objectives.Append(Check(shoutCredited)).Append("Shout for the walking wounded (").Append(GameInput.KeyName(GameInput.Shout)).Append(")\n");
                objectives.Append(Check(tagged == casualties.Count)).Append("Examine and tag everyone (").Append(tagged).Append("/").Append(casualties.Count).Append(")\n");
                objectives.Append(Check(false)).Append("Give indicated aid; re-check people over time\n");
                objectives.Append(Check(false)).Append("Hand over to ambulances by priority (").Append(transported).Append(" sent)");
                if (dead > 0) objectives.Append("\n<color=#FF8A8A>Deaths: ").Append(dead).Append("</color>");
            }

            hud.SetObjectives(objectives.ToString());
            float remaining = RemainingMinutes;
            string clockColor = remaining < 10f ? "#FF8A8A" : remaining < 25f ? "#F6D171" : "#F6F7F8";
            hud.SetClock("<color=" + clockColor + ">GOLDEN HOUR  " + Mathf.FloorToInt(remaining).ToString("00") + ":" +
                         Mathf.FloorToInt(remaining % 1f * 60f).ToString("00") + "</color>\nSCORE  " + ledger.Total +
                         "\n<size=70%>" + DifficultyBand + "</size>");
            hud.SetSupplies(KitCollected
                ? "<b>BAG</b>  Gloves " + (GlovesOn ? "<color=#7FE0A6>ON</color>" : "<color=#FF8A8A>OFF</color>") + " (" + Gloves + " spare)\n" +
                  "Pressure dressings  " + Dressings + "\nBlankets  " + Blankets
                : "<b>BAG</b>  not collected\n<size=85%>Find the red first-aid bag</size>");
        }

        private static string Check(bool done) => done ? "<color=#7FE0A6>[X]</color> " : "[  ] ";
    }
}
