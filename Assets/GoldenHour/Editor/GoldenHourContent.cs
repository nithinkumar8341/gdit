using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GoldenHour.EditorTools
{
    /// <summary>
    /// Creates the v2 campaign data: casualty profiles, four scenarios and the catalog. Values are a
    /// simplified educational model (START thresholds: breathing over 30/min, capillary refill over 2 s,
    /// not following commands). Not medically reviewed.
    /// </summary>
    public static class GoldenHourContent
    {
        public const string Root = "Assets/GoldenHour/ScriptableObjects/V2/";
        public const string CatalogPath = Root + "Campaign.asset";

        private static readonly string[] Ids = { "tutorial", "highway", "bus", "building" };

        /// <summary>
        /// When false (Build Project), existing casualty and scenario assets are left exactly as they are, so
        /// Inspector edits survive; only missing assets are created from the defaults below. When true
        /// (menu "GoldenHour/Regenerate Content"), every asset is reset to these defaults.
        /// </summary>
        private static bool overwrite;

        /// <summary>Assets that already existed this run, keyed by the throwaway instance the builders filled in.</summary>
        private static readonly Dictionary<Object, Object> Existing = new Dictionary<Object, Object>();

        /// <summary>Number of assets created or overwritten in the last run (logged for evidence).</summary>
        public static int Written { get; private set; }

        /// <summary>Non-destructive: creates missing content only (used by Build Project).</summary>
        public static ScenarioCatalog CreateAll() => Generate(false);

        [MenuItem("GoldenHour/Regenerate Content (overwrites scenario and casualty assets)")]
        public static void RegenerateContent()
        {
            Generate(true);
            Debug.Log("Golden Hour content regenerated from defaults (" + Written + " assets written).");
        }

        private static ScenarioCatalog Generate(bool overwriteExisting)
        {
            overwrite = overwriteExisting;
            Existing.Clear();
            Written = 0;
            Directory.CreateDirectory(Root + "Casualties");
            Directory.CreateDirectory(Root + "Scenarios");
            AssetDatabase.Refresh();
            ScenarioCatalog catalog = AssetDatabase.LoadAssetAtPath<ScenarioCatalog>(CatalogPath);
            bool newCatalog = catalog == null;
            if (newCatalog)
            {
                catalog = ScriptableObject.CreateInstance<ScenarioCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            List<ScenarioData> scenarios = new List<ScenarioData> { Tutorial(), Highway(), Bus(), Building() };
            if (overwrite || newCatalog || catalog.scenarios == null || catalog.scenarios.Count == 0)
            {
                catalog.scenarios = scenarios;
                EditorUtility.SetDirty(catalog);
                Written++;
            }

            foreach (Object transient in Existing.Keys) Object.DestroyImmediate(transient);
            Existing.Clear();
            overwrite = false;
            AssetDatabase.SaveAssets();
            Debug.Log("Golden Hour content: " + Written + " asset(s) created or overwritten" + (overwriteExisting ? " (regenerate)." : "; existing assets preserved."));
            return catalog;
        }

        /// <summary>Returns the saved asset for a builder-filled instance (the existing asset when it was preserved).</summary>
        private static T Commit<T>(T built) where T : Object
        {
            if (Existing.TryGetValue(built, out Object asset)) return (T)asset;
            EditorUtility.SetDirty(built);
            Written++;
            return built;
        }

        public static ScenarioCatalog LoadCatalog() => AssetDatabase.LoadAssetAtPath<ScenarioCatalog>(CatalogPath);

        // ----------------------------------------------------------------- types

        private static PhysiologyState Walker(int rate = 18) => new PhysiologyState { canWalk = true, respiratoryRate = rate, capillaryRefill = 1.5f };

        private static PhysiologyState Yellow(int rate = 20, float refill = 1.6f) =>
            new PhysiologyState { respiratoryRate = rate, capillaryRefill = refill };

        private static PhysiologyState Bleeding() =>
            new PhysiologyState { respiratoryRate = 24, capillaryRefill = 2.6f, bleeding = BleedingLevel.Severe };

        private static PhysiologyState Airway() =>
            new PhysiologyState { airwayObstructed = true, respiratoryRate = 12, capillaryRefill = 2f, followsCommands = false };

        private static PhysiologyState Unresponsive() =>
            new PhysiologyState { respiratoryRate = 14, capillaryRefill = 1.8f, followsCommands = false };

        private static PhysiologyState Fast(int rate = 34) => new PhysiologyState { respiratoryRate = rate, capillaryRefill = 1.8f };

        private static PhysiologyState Shock() =>
            new PhysiologyState { respiratoryRate = 24, capillaryRefill = 3.2f, radialPulse = false };

        private static PhysiologyState Expectant() =>
            new PhysiologyState { respiratoryRate = 0, capillaryRefill = 4f, radialPulse = false, followsCommands = false };

        private static CasualtyProfile Profile(string id, string name, int age, BodyVariant variant, BodyPose pose, Color clothing,
            string appearance, string injury, PhysiologyState state, Treatment indicated, string aidNote, params string[] barks)
        {
            string path = Root + "Casualties/" + id + ".asset";
            CasualtyProfile profile = AssetDatabase.LoadAssetAtPath<CasualtyProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<CasualtyProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            else if (!overwrite)
            {
                CasualtyProfile transient = ScriptableObject.CreateInstance<CasualtyProfile>();
                Existing[transient] = profile;
                profile = transient;
            }

            profile.casualtyId = id;
            profile.displayName = name;
            profile.age = age;
            profile.variant = variant;
            profile.pose = pose;
            profile.clothingColor = clothing;
            profile.appearance = appearance;
            profile.injurySummary = injury;
            profile.initial = state;
            profile.indicatedTreatments = indicated;
            profile.aidNote = aidNote;
            profile.barks = barks;
            profile.condition = CriticalCondition.None;
            profile.deathMinute = 0f;
            profile.worsenMinute = 0f;
            profile.worsenedRespiratoryRate = 0;
            profile.worsenedCapillaryRefill = 0f;
            profile.worsenedLosesCommands = false;
            profile.capillaryRefillRisePerMinute = 0f;
            profile.stabilisedBy = Treatment.None;
            profile.airwayOpeningDelayMinutes = 8f;
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static CasualtyProfile Haemorrhage(CasualtyProfile profile, float death)
        {
            profile.condition = CriticalCondition.Haemorrhage;
            profile.deathMinute = death;
            profile.capillaryRefillRisePerMinute = 0.12f;
            profile.stabilisedBy = Treatment.PressureDressing;
            return profile;
        }

        private static CasualtyProfile AirwayRisk(CasualtyProfile profile, float death)
        {
            profile.condition = CriticalCondition.Airway;
            profile.deathMinute = death;
            profile.stabilisedBy = Treatment.RecoveryPosition;
            return profile;
        }

        private static CasualtyProfile NeedsTransport(CasualtyProfile profile, CriticalCondition condition, float death)
        {
            profile.condition = condition;
            profile.deathMinute = death;
            return profile;
        }

        private static CasualtyProfile Worsens(CasualtyProfile profile, float minute, int rate, float refill, bool losesCommands, Treatment stabilisedBy)
        {
            profile.condition = losesCommands ? CriticalCondition.HeadInjury : CriticalCondition.Shock;
            profile.worsenMinute = minute;
            profile.worsenedRespiratoryRate = rate;
            profile.worsenedCapillaryRefill = refill;
            profile.worsenedLosesCommands = losesCommands;
            profile.stabilisedBy = stabilisedBy;
            return profile;
        }

        private static readonly Color Blue = new Color32(70, 110, 160, 255);
        private static readonly Color Red = new Color32(160, 60, 55, 255);
        private static readonly Color Green = new Color32(80, 120, 80, 255);
        private static readonly Color Purple = new Color32(120, 90, 150, 255);
        private static readonly Color Grey = new Color32(110, 110, 115, 255);
        private static readonly Color Orange = new Color32(200, 120, 50, 255);
        private static readonly Color Teal = new Color32(50, 130, 130, 255);
        private static readonly Color Brown = new Color32(120, 90, 60, 255);

        private static ScenarioData Scenario(string id)
        {
            string path = Root + "Scenarios/" + id + ".asset";
            ScenarioData scenario = AssetDatabase.LoadAssetAtPath<ScenarioData>(path);
            if (scenario == null)
            {
                scenario = ScriptableObject.CreateInstance<ScenarioData>();
                AssetDatabase.CreateAsset(scenario, path);
            }
            else if (!overwrite)
            {
                ScenarioData transient = ScriptableObject.CreateInstance<ScenarioData>();
                Existing[transient] = scenario;
                scenario = transient;
            }

            scenario.scenarioId = id;
            scenario.casualties = new List<CasualtyPlacement>();
            scenario.hazards = new List<HazardPlacement>();
            scenario.waves = new List<AmbulanceWave>();
            scenario.radio = new List<RadioCue>();
            int index = System.Array.IndexOf(Ids, id);
            scenario.order = index;
            scenario.nextScenarioId = index >= 0 && index < Ids.Length - 1 ? Ids[index + 1] : string.Empty;
            return scenario;
        }

        private static void Place(ScenarioData scenario, CasualtyProfile profile, float x, float z, float yaw)
        {
            scenario.casualties.Add(new CasualtyPlacement { profile = Commit(profile), position = new Vector3(x, 0f, z), yaw = yaw });
        }

        private static void Hazard(ScenarioData scenario, HazardType type, float x, float z, float radius, float yaw = 0f)
        {
            scenario.hazards.Add(new HazardPlacement { type = type, position = new Vector3(x, 0f, z), radius = radius, yaw = yaw });
        }

        private static void Wave(ScenarioData scenario, float minute, int capacity)
        {
            scenario.waves.Add(new AmbulanceWave { minute = minute, capacity = capacity });
        }

        private static void Cue(ScenarioData scenario, float minute, string text)
        {
            scenario.radio.Add(new RadioCue { minute = minute, text = text });
        }

        private static readonly string[] CommonTips =
        {
            "START takes about 30-60 seconds per person: walking, breathing, circulation, commands.",
            "Shout \"if you can walk, go to the green zone\" first - it sorts the walking wounded in seconds.",
            "Capillary refill: press a fingernail and count how long the colour takes to return. Over 2 seconds is poor circulation.",
            "Open the airway of anyone not breathing. If they start breathing, they are RED. If not, BLACK.",
            "Gloves first. Every hands-on check without gloves costs points.",
            "Conditions change. Re-check people - a justified re-tag earns credit.",
            "Hand over RED before YELLOW, YELLOW before GREEN. Never send someone tagged BLACK.",
            "This is an educational model, not medical training. Check real procedures with a qualified first-aid trainer."
        };

        // -------------------------------------------------------------- tutorial

        private static ScenarioData Tutorial()
        {
            ScenarioData s = Scenario("tutorial");
            s.title = "Training Yard";
            s.description = "Guided introduction: a low-speed collision in the training centre yard with three casualties.";
            s.tutorial = true;
            s.environment = EnvironmentKind.TrainingYard;
            s.night = false;
            s.rain = false;
            s.goldenHourMinutes = 60f;
            s.simMinutesPerSecond = 0.05f;
            s.seed = 101;
            s.playerStart = new Vector3(0f, 0.1f, -9f);
            s.playerYaw = 0f;
            s.kitPosition = new Vector3(-2f, 0.02f, -6f);
            s.greenZone = new Vector3(-8f, 0f, -3f);
            s.ambulanceSpawn = new Vector3(6f, 0f, -36f);
            s.ambulanceBay = new Vector3(6f, 0f, -10f);
            s.flythrough = new[] { new Vector3(-12f, 9f, -14f), new Vector3(-4f, 6f, -4f), new Vector3(6f, 4f, 2f), new Vector3(0f, 2.2f, -9.5f) };
            Place(s, Profile("T01", "Robin Hale", 34, BodyVariant.Adult, BodyPose.Standing, Blue, "standing by the car, holding their wrist.",
                "Sore wrist, shaken.", Walker(), Treatment.None, "Directed to the green zone.", "I'm okay, I think... my wrist hurts.",
                "Is everyone alright?"), 5.5f, 1.5f, 200f);
            Place(s, Haemorrhage(Profile("T02", "Mo Clarke", 45, BodyVariant.Adult, BodyPose.Lying, Red, "lying on the ground, pale and anxious.",
                "A deep cut on the lower leg that is bleeding heavily (no graphic detail shown).", Bleeding(), Treatment.PressureDressing,
                "A pressure dressing with firm pressure controls heavy bleeding.", "My leg... it won't stop.", "Please hurry..."), 22f), -1.8f, 3.5f, 100f);
            Place(s, AirwayRisk(Profile("T03", "Sam Ito", 52, BodyVariant.Adult, BodyPose.Lying, Grey, "lying still on their back by the wall.",
                "Hit their head. Breathing but not responding.", Unresponsive(), Treatment.RecoveryPosition,
                "An unresponsive person who is breathing goes into the recovery position to keep the airway open."), 26f), 0.5f, 9f, -70f);
            Hazard(s, HazardType.FuelLeak, 2.6f, 2.6f, 1.5f);
            Wave(s, 6f, 2);
            Wave(s, 12f, 1);
            s.bystanders = 0;
            s.locationOptions = new[] { "Training yard, First Responder Centre", "Car park, City Hospital", "Ring road, Junction 4" };
            s.correctLocation = 0;
            s.gloves = 2;
            s.dressings = 2;
            s.blankets = 2;
            s.briefing = "Welcome to the Training Yard. A car has hit a bollard at low speed. There are three people involved.\n\n" +
                         "This guided scenario teaches the full loop: scene safety, the 112 dispatch report, START triage by examining, " +
                         "first aid, and handing over to the ambulance crew. Hints are always on here.";
            Cue(s, 0f, "Training control: this is an exercise. Treat it like the real thing. Scene safety first.");
            Cue(s, 9f, "Training control: remember to re-check anyone you have not looked at for a while.");
            s.tutorialSteps = new[]
            {
                "Walk to the red first-aid bag and collect it ({Interact}).",
                "Put on gloves ({Gloves}) before you touch anyone.",
                "Fuel is leaking by the car: look at it and mark the hazard ({Interact}).",
                "Press {Radio} and send the dispatch report to 112: number, location, casualties, hazards.",
                "Shout for the walking wounded ({Shout}): anyone who can walk goes to the green zone.",
                "Examine and tag the others ({Interact}): check breathing, circulation and commands, then choose a tag.",
                "Give the aid each person needs: open the SUPPLY BAG and take a pressure dressing for heavy bleeding (hold {Primary}); recovery position for an unresponsive person who is breathing.",
                "When the ambulance arrives, talk to the paramedic ({Interact}) and hand over the most urgent casualty first."
            };
            s.loadingTips = CommonTips;
            return Commit(s);
        }

        // --------------------------------------------------------------- highway

        private static ScenarioData Highway()
        {
            ScenarioData s = Scenario("highway");
            s.title = "Highway Collision";
            s.description = "A car and a motorcycle have collided on a dual carriageway. Six casualties, fuel and fire.";
            s.tutorial = false;
            s.environment = EnvironmentKind.Highway;
            s.night = false;
            s.rain = false;
            s.goldenHourMinutes = 60f;
            s.simMinutesPerSecond = 0.08f;
            s.seed = 202;
            s.playerStart = new Vector3(0f, 0.1f, -13.5f);
            s.playerYaw = 0f;
            s.kitPosition = new Vector3(2.4f, 0.02f, -7.2f);
            s.greenZone = new Vector3(5f, 0f, -19f);
            s.ambulanceSpawn = new Vector3(-2.6f, 0f, -48f);
            s.ambulanceBay = new Vector3(-2.6f, 0f, -16.5f);
            s.flythrough = new[] { new Vector3(14f, 12f, 30f), new Vector3(6f, 7f, 14f), new Vector3(-4f, 4f, 2f), new Vector3(0f, 2.2f, -14f) };
            Place(s, Profile("H01", "Dana Okafor", 41, BodyVariant.Adult, BodyPose.Standing, Blue, "standing by the verge, phone in hand.",
                "Bruised shoulder, walking about.", Walker(), Treatment.None, "Directed to the green zone.", "I swerved... he came out of nowhere.",
                "Is the biker okay?"), 4.4f, -2.5f, 180f);
            Place(s, Haemorrhage(Profile("H02", "Lee Novak", 29, BodyVariant.Adult, BodyPose.Lying, Red, "lying on the road, grey-faced and scared.",
                "Heavy bleeding from a wound on the thigh (no graphic detail shown).", Bleeding(), Treatment.PressureDressing,
                "Firm pressure with a dressing controls heavy bleeding.", "So much blood...", "I'm going to be sick..."), 16f), -2.8f, 1.2f, 70f);
            Place(s, AirwayRisk(Profile("H03", "Ari Mensah", 58, BodyVariant.Adult, BodyPose.Lying, Grey, "slumped on their back, making snoring noises then silence.",
                "Head injury. Not responding.", Airway(), Treatment.AirwayOpening | Treatment.RecoveryPosition,
                "Open the airway; if breathing starts, the recovery position keeps it open."), 10f), 3.4f, 9.2f, -150f);
            Place(s, NeedsTransport(Profile("H04", "Jo Brennan", 36, BodyVariant.Adult, BodyPose.Lying, Purple, "lying on their side, breathing fast and shallow.",
                "Chest hurts after hitting the steering wheel.", Fast(34), Treatment.Reassure, "Calm reassurance; needs urgent transport.",
                "Can't... get... air.", "It hurts to breathe."), CriticalCondition.Breathing, 30f), -3.6f, 9.6f, 160f);
            Place(s, Worsens(Profile("H05", "Priya Shah", 63, BodyVariant.Elderly, BodyPose.Lying, Teal, "lying awkwardly, says their hip hurts.",
                "Cannot stand; hip pain. Talking clearly.", Yellow(22, 1.9f), Treatment.Blanket, "Keep warm with a blanket; reassure.",
                "My hip... I can't get up.", "I'm so cold."), 18f, 26, 3.2f, false, Treatment.Blanket), 0.6f, 13.6f, 95f);
            Place(s, Profile("H06", "Kit Romero", 24, BodyVariant.Motorcyclist, BodyPose.Lying, new Color32(40, 40, 46, 255),
                "the motorcyclist, helmet on, lying near the bike, eyes open but confused.",
                "Thrown from the bike. Breathing, but not following instructions.", Unresponsive(), Treatment.RecoveryPosition,
                "Keep the helmet on in this model; recovery position protects the airway.", "Where... where's my bike?"), -1.6f, -5.2f, 30f);
            CasualtyProfile biker = s.casualties[s.casualties.Count - 1].profile;
            biker.condition = CriticalCondition.HeadInjury;
            biker.deathMinute = 34f;
            biker.stabilisedBy = Treatment.RecoveryPosition;
            Hazard(s, HazardType.Traffic, 0f, 22f, 2f, 180f);
            Hazard(s, HazardType.FuelLeak, 0.2f, 7.8f, 1.5f);
            Hazard(s, HazardType.Fire, 2.5f, 6.8f, 2.2f);
            Wave(s, 9f, 2);
            Wave(s, 17f, 2);
            Wave(s, 25f, 2);
            s.bystanders = 2;
            s.locationOptions = new[] { "A31 dual carriageway, northbound near Exit 12", "Market Street, city centre", "B4210 Moor Road" };
            s.correctLocation = 0;
            s.gloves = 2;
            s.dressings = 2;
            s.blankets = 2;
            s.briefing = "You are an off-duty first aider driving home. A car and a motorcycle have just collided ahead of you on the A31 northbound near Exit 12. " +
                         "Fuel is leaking and the engine is smoking. Six people are involved, and drivers are stopping to look.";
            Cue(s, 0f, "Incident command: you are first on scene. Make it safe, then send the report to 112.");
            Cue(s, 7f, "Police: the northbound carriageway is closed behind you.");
            Cue(s, 20f, "Incident command: fire service four minutes out. Keep everyone clear of the fire.");
            s.loadingTips = CommonTips;
            return Commit(s);
        }

        // ------------------------------------------------------------------- bus

        private static ScenarioData Bus()
        {
            ScenarioData s = Scenario("bus");
            s.title = "Night Bus Rollover";
            s.description = "A coach has rolled onto its side on a wet country road at night. Ten casualties, fuel leak and fire.";
            s.tutorial = false;
            s.environment = EnvironmentKind.BusRollover;
            s.night = true;
            s.rain = true;
            s.goldenHourMinutes = 60f;
            s.simMinutesPerSecond = 0.08f;
            s.seed = 303;
            s.playerStart = new Vector3(0f, 0.1f, -10f);
            s.playerYaw = 0f;
            s.kitPosition = new Vector3(-2f, 0.02f, -6.5f);
            s.greenZone = new Vector3(-8.5f, 0f, 10f);
            s.ambulanceSpawn = new Vector3(-2.5f, 0f, -52f);
            s.ambulanceBay = new Vector3(-2.5f, 0f, -14f);
            s.flythrough = new[] { new Vector3(-10f, 10f, 34f), new Vector3(-8f, 6f, 18f), new Vector3(-4f, 4f, 2f), new Vector3(0f, 2.2f, -10.5f) };
            Place(s, Profile("B01", "Tom Fisher", 22, BodyVariant.Adult, BodyPose.Standing, Blue, "standing in the rain, dazed.", "Scratches on the hands.",
                Walker(), Treatment.None, "Green zone.", "We just... rolled over.", "My girlfriend's still in there!"), -3.5f, 4f, 160f);
            Place(s, Profile("B02", "Rosa Lind", 67, BodyVariant.Elderly, BodyPose.Sitting, Purple, "sitting on the verge clutching her bag.", "Bruised and shaken.",
                Walker(16), Treatment.None, "Green zone.", "I'm fine, dear, help the others."), -4f, 8f, 120f);
            Place(s, Worsens(Profile("B03", "Callum Reid", 31, BodyVariant.Adult, BodyPose.Standing, Green, "walking around in circles, holding his head.",
                "Bumped his head. Repeating questions.", Walker(), Treatment.None, "Watch for a worsening head injury.",
                "What happened? What happened?", "My head's pounding."), 15f, 0, 0f, true, Treatment.None), -2.5f, 14f, 200f);
            Place(s, Profile("B04", "Mia Kowalski", 19, BodyVariant.Adult, BodyPose.Lying, Orange, "lying on the road, arm held across her chest.",
                "Painful forearm, cannot get up because of dizziness. Talking.", Yellow(18, 1.5f), Treatment.Blanket, "Blanket and reassurance.",
                "My arm's broken, I know it is.", "I'm freezing."), 0f, 3f, 90f);
            Place(s, Worsens(Profile("B05", "Hassan Ali", 48, BodyVariant.Adult, BodyPose.Lying, Brown, "lying by the bus, shivering, talking clearly.",
                "Cannot stand; back pain. Cold and wet.", Yellow(22, 1.9f), Treatment.Blanket, "Keep warm with a blanket.",
                "I can't feel warm.", "Is the driver okay?"), 16f, 28, 3.0f, false, Treatment.Blanket), -1.5f, 19f, 200f);
            Place(s, Haemorrhage(Profile("B06", "Ellie Grant", 27, BodyVariant.Adult, BodyPose.Lying, Red, "lying in the wet grass beyond the rail.",
                "Heavy bleeding from the upper arm (no graphic detail shown).", Bleeding(), Treatment.PressureDressing,
                "Pressure dressing.", "Please, it's bleeding a lot.", "Help me..."), 15f), 6.5f, 6f, 0f);
            Place(s, AirwayRisk(Profile("B07", "Driver", 55, BodyVariant.Adult, BodyPose.Lying, new Color32(30, 50, 90, 255), "the coach driver, thrown clear, on his back.",
                "Not responding; noisy then silent breathing.", Airway(), Treatment.AirwayOpening | Treatment.RecoveryPosition,
                "Open the airway, then recovery position."), 11f), 7f, 14f, 180f);
            Place(s, NeedsTransport(Profile("B08", "Nina Petrova", 38, BodyVariant.Adult, BodyPose.Lying, Teal, "lying still, breathing very fast.",
                "Was crushed against a seat; chest pain.", Fast(36), Treatment.Reassure, "Reassure; urgent transport.", "Can't breathe properly."),
                CriticalCondition.Breathing, 28f), -0.5f, 9f, 60f);
            Place(s, NeedsTransport(Profile("B09", "Owen Price", 45, BodyVariant.Adult, BodyPose.Lying, Grey, "lying pale and sweaty, quiet.",
                "Says his belly hurts; skin cold and clammy.", Shock(), Treatment.Blanket, "Blanket; urgent transport.", "Feel... faint..."),
                CriticalCondition.Shock, 30f), 1f, 21.5f, 120f);
            Place(s, Profile("B10", "Unknown passenger", 70, BodyVariant.Elderly, BodyPose.Lying, Grey, "lying motionless by the rear of the bus.",
                "No movement. (No graphic detail shown.)", Expectant(), Treatment.None, "No breathing after airway opening: expectant."), 5.5f, 22.5f, 170f);
            Hazard(s, HazardType.Traffic, 0f, -20f, 2f, 0f);
            Hazard(s, HazardType.FuelLeak, 2.2f, 5f, 1.6f);
            Hazard(s, HazardType.Fire, 4.4f, 18.4f, 2.5f);
            Wave(s, 8f, 2);
            Wave(s, 15f, 3);
            Wave(s, 22f, 3);
            Wave(s, 30f, 2);
            s.bystanders = 2;
            s.locationOptions = new[] { "B4210 Moor Road, by the reservoir", "A31 Exit 12 roundabout", "Central bus station" };
            s.correctLocation = 0;
            s.gloves = 2;
            s.dressings = 1;
            s.blankets = 2;
            s.briefing = "11:40 pm, heavy rain. A coach has rolled onto its side on the B4210 Moor Road by the reservoir. You stopped your car behind it. " +
                         "Diesel is leaking, the engine compartment is burning, and passengers are climbing out. Supplies are limited: " +
                         "one pressure dressing and two blankets. Choose carefully.";
            Cue(s, 0f, "Incident command: multiple casualties reported on Moor Road. You are first on scene. Safety, then report.");
            Cue(s, 12f, "Incident command: more units are coming but the road is flooding. Use the green zone for anyone who can walk.");
            Cue(s, 26f, "Fire service: we're on scene at the bus fire. Keep casualties clear of the rear.");
            s.loadingTips = CommonTips;
            return Commit(s);
        }

        // -------------------------------------------------------------- building

        private static ScenarioData Building()
        {
            ScenarioData s = Scenario("building");
            s.title = "Collapsed Community Hall";
            s.description = "The roof of a community hall has collapsed during a children's event. Twelve casualties and a crowd of walking wounded.";
            s.tutorial = false;
            s.environment = EnvironmentKind.BuildingSite;
            s.night = false;
            s.rain = false;
            s.goldenHourMinutes = 60f;
            s.simMinutesPerSecond = 0.08f;
            s.seed = 404;
            s.playerStart = new Vector3(0f, 0.1f, -12f);
            s.playerYaw = 0f;
            s.kitPosition = new Vector3(-2f, 0.02f, -9f);
            s.greenZone = new Vector3(-15f, 0f, -6f);
            s.ambulanceSpawn = new Vector3(-48f, 0f, -20f);
            s.ambulanceBay = new Vector3(-6f, 0f, -20f);
            s.flythrough = new[] { new Vector3(16f, 14f, 28f), new Vector3(10f, 8f, 14f), new Vector3(-6f, 4f, 0f), new Vector3(0f, 2.2f, -12.5f) };
            Place(s, Profile("K01", "Jamie Cole", 38, BodyVariant.Adult, BodyPose.Standing, Blue, "a parent, dusty, calling for their child.",
                "Small cuts, walking.", Walker(), Treatment.None, "Green zone.", "Lily? LILY?"), -6f, -2f, 150f);
            Place(s, Profile("K02", "Lily Cole", 8, BodyVariant.Child, BodyPose.Standing, Orange, "a child, crying, covered in dust.",
                "Grazed knees, walking.", Walker(22), Treatment.None, "Green zone with a parent.", "I want my mum!"), -4.5f, -1f, 200f);
            Place(s, Profile("K03", "Arthur Bell", 76, BodyVariant.Elderly, BodyPose.Sitting, Grey, "sitting on a bench, coughing from the dust.",
                "Coughing, no injury seen.", Walker(20), Treatment.None, "Green zone.", "This dust... cough..."), 5f, -1f, 210f);
            Place(s, Profile("K04", "Sid Malik", 44, BodyVariant.Worker, BodyPose.Standing, Orange, "a site worker in hi-vis, holding his ribs.",
                "Sore ribs; walking.", Walker(), Treatment.None, "Green zone.", "The roof just went."), 7f, 0f, 190f);
            Place(s, Profile("K05", "Noah Brooks", 9, BodyVariant.Child, BodyPose.Lying, Green, "a child lying on the floor, holding his leg.",
                "Leg pinned briefly; cannot stand. Crying, talking.", Yellow(24, 1.6f), Treatment.Blanket | Treatment.Reassure,
                "Reassure and keep warm. (Real practice uses JumpSTART for children - simplified here.)", "It hurts!", "Where's my dad?"), -3f, 4f, 90f);
            Place(s, NeedsTransport(Profile("K06", "Ava Brooks", 7, BodyVariant.Child, BodyPose.Lying, Purple, "a child, very pale, breathing quickly.",
                "Chest hit by falling debris. (Real practice uses JumpSTART for children.)", Fast(38), Treatment.Reassure, "Reassure; urgent transport.",
                "Can't... breathe..."), CriticalCondition.Breathing, 26f), 3f, 5f, -90f);
            Place(s, Haemorrhage(Profile("K07", "Grace Hill", 33, BodyVariant.Adult, BodyPose.Lying, Red, "lying by a fallen slab, conscious.",
                "Heavy bleeding from the lower leg (no graphic detail shown).", Bleeding(), Treatment.PressureDressing,
                "Pressure dressing.", "My leg... please."), 14f), -6f, 8f, 0f);
            Place(s, AirwayRisk(Profile("K08", "Dev Patel", 51, BodyVariant.Worker, BodyPose.Lying, Orange, "a worker lying on his back, helmet off.",
                "Not responding; breathing is noisy and stops.", Airway(), Treatment.AirwayOpening | Treatment.RecoveryPosition,
                "Open the airway, then recovery position."), 11f), 6f, 9f, 180f);
            Place(s, Worsens(Profile("K09", "Edna Moss", 81, BodyVariant.Elderly, BodyPose.Lying, Teal, "an elderly woman lying near the doorway.",
                "Cannot get up; hip pain; talking.", Yellow(20, 1.8f), Treatment.Blanket, "Blanket and reassurance.", "Is everyone out?"),
                17f, 24, 3.1f, false, Treatment.Blanket), 0f, 6.5f, 90f);
            Place(s, NeedsTransport(Profile("K10", "Ben Fraser", 40, BodyVariant.Adult, BodyPose.Lying, Brown, "lying quietly, sweaty and grey.",
                "Pinned by the legs for a while; now free. Belly pain.", Shock(), Treatment.Blanket, "Blanket; urgent transport.", "Feel faint..."),
                CriticalCondition.Shock, 30f), -1f, 10.5f, 45f);
            Place(s, Profile("K11", "Unknown adult", 55, BodyVariant.Adult, BodyPose.Lying, Grey, "partly under a fallen slab, not moving.",
                "No movement. (No graphic detail shown.)", Expectant(), Treatment.None, "Expectant."), 3.5f, 12.5f, 200f);
            Place(s, Profile("K12", "Chloe Adams", 29, BodyVariant.Adult, BodyPose.Lying, Blue, "lying on the grass, ankle at an odd angle.",
                "Cannot walk on the ankle; otherwise well.", Yellow(18, 1.5f), Treatment.Reassure, "Reassure.", "I'm okay, just my ankle."), -8f, 2f, 120f);
            Hazard(s, HazardType.UnstableStructure, 9f, 15f, 3f);
            Hazard(s, HazardType.GasCylinder, -12f, 10f, 2f);
            Hazard(s, HazardType.LiveWire, 12f, -3f, 2.5f);
            Wave(s, 8f, 2);
            Wave(s, 14f, 3);
            Wave(s, 21f, 3);
            Wave(s, 28f, 3);
            s.bystanders = 3;
            s.locationOptions = new[] { "Riverside Community Hall, Mill Lane", "Mill Lane Primary School", "Riverside retail park" };
            s.correctLocation = 0;
            s.gloves = 3;
            s.dressings = 1;
            s.blankets = 2;
            s.briefing = "Saturday afternoon. Part of the roof of Riverside Community Hall on Mill Lane has collapsed during a children's party next to a building site. " +
                         "Parents, children and workers are everywhere. A power line is down, a gas cylinder is hissing, and a wall is leaning. " +
                         "Use the walking-wounded command early - there are many people who can walk.";
            Cue(s, 0f, "Incident command: you are the first trained responder at the hall. Safety, then a report.");
            Cue(s, 10f, "Incident command: the electricity company is isolating the power line. Keep everyone clear.");
            Cue(s, 24f, "Incident command: urban search and rescue is ten minutes out for anyone trapped.");
            s.loadingTips = CommonTips;
            return Commit(s);
        }
    }
}
