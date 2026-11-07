using System;
using System.Collections.Generic;
using UnityEngine;

namespace GoldenHour
{
    public enum EnvironmentKind
    {
        TrainingYard,
        Highway,
        BusRollover,
        BuildingSite
    }

    /// <summary>Hazard types; the value is the bit used in the dispatch report's hazard mask.</summary>
    public enum HazardType
    {
        Traffic = 0,
        FuelLeak = 1,
        Fire = 2,
        LiveWire = 3,
        UnstableStructure = 4,
        GasCylinder = 5
    }

    [Serializable]
    public sealed class CasualtyPlacement
    {
        public CasualtyProfile profile;
        public Vector3 position;
        public float yaw;
    }

    [Serializable]
    public sealed class HazardPlacement
    {
        public HazardType type;
        public Vector3 position;
        public float yaw;
        public float radius = 2.5f;
    }

    [Serializable]
    public sealed class AmbulanceWave
    {
        public float minute;
        public int capacity = 2;
    }

    [Serializable]
    public sealed class RadioCue
    {
        public float minute;
        [TextArea] public string text;
    }

    /// <summary>One playable incident: layout, casualties, hazards, resources, timing and radio script.</summary>
    [CreateAssetMenu(menuName = "Golden Hour/Scenario")]
    public sealed class ScenarioData : ScriptableObject
    {
        [Header("Identity")]
        public string scenarioId = "scenario";
        public string title = "Scenario";
        [TextArea] public string description;
        public int order;
        public string nextScenarioId;
        public bool tutorial;

        [Header("Environment")]
        public EnvironmentKind environment;
        public bool night;
        public bool rain;

        [Header("Clock")]
        public float goldenHourMinutes = 60f;
        [Tooltip("Simulated minutes per real second.")] public float simMinutesPerSecond = 0.1f;
        public int seed = 1;

        [Header("Layout")]
        public Vector3 playerStart;
        public float playerYaw;
        public Vector3 kitPosition;
        public Vector3 greenZone;
        public Vector3 ambulanceBay;
        public Vector3 ambulanceSpawn;
        public Vector3[] flythrough = new Vector3[0];

        [Header("Content")]
        public List<CasualtyPlacement> casualties = new List<CasualtyPlacement>();
        public List<HazardPlacement> hazards = new List<HazardPlacement>();
        public List<AmbulanceWave> waves = new List<AmbulanceWave>();
        public List<RadioCue> radio = new List<RadioCue>();
        public int bystanders;

        [Header("Dispatch report")]
        public string[] locationOptions = new string[0];
        public int correctLocation;

        [Header("Supplies in the first-aid bag")]
        public int gloves = 2;
        public int dressings = 3;
        public int blankets = 3;

        [Header("Text")]
        [TextArea(4, 10)] public string briefing;
        public string[] loadingTips = new string[0];
        [TextArea] public string[] tutorialSteps = new string[0];

        public int HazardMask
        {
            get
            {
                int mask = 0;
                foreach (HazardPlacement hazard in hazards) mask |= 1 << (int)hazard.type;
                return mask;
            }
        }

        public int TotalAmbulanceCapacity
        {
            get
            {
                int total = 0;
                foreach (AmbulanceWave wave in waves) total += wave.capacity;
                return total;
            }
        }

        public static string HazardName(HazardType type)
        {
            switch (type)
            {
                case HazardType.Traffic: return "Moving traffic";
                case HazardType.FuelLeak: return "Fuel leak";
                case HazardType.Fire: return "Fire";
                case HazardType.LiveWire: return "Fallen live wire";
                case HazardType.UnstableStructure: return "Unstable structure";
                case HazardType.GasCylinder: return "Leaking gas cylinder";
                default: return type.ToString();
            }
        }
    }
}
