using System;
using UnityEngine;

namespace GoldenHour
{
    /// <summary>
    /// Simplified casualty physiology used by the START model.
    /// <see cref="respiratoryRate"/> is the rate when the airway is open; 0 means no breathing even after
    /// airway opening. <see cref="airwayObstructed"/> means the person breathes only once the airway is opened
    /// (this stays true for START purposes; <see cref="airwayOpened"/> records the rescuer's action).
    /// </summary>
    [Serializable]
    public sealed class PhysiologyState
    {
        public bool canWalk;
        public bool airwayObstructed;
        public bool airwayOpened;
        [Range(0, 60)] public int respiratoryRate = 16;
        [Range(0.5f, 8f)] public float capillaryRefill = 1.5f;
        public bool radialPulse = true;
        public bool followsCommands = true;
        public BleedingLevel bleeding;
        public bool bleedingControlled;
        public bool deceased;

        /// <summary>Visible chest movement right now.</summary>
        public bool Breathing => !deceased && respiratoryRate > 0 && (!airwayObstructed || airwayOpened);

        public bool PoorCirculation => capillaryRefill > StartTriage.CapillaryRefillLimit || !radialPulse;

        public PhysiologyState Clone()
        {
            return (PhysiologyState)MemberwiseClone();
        }
    }

    [Flags]
    public enum Treatment
    {
        None = 0,
        PressureDressing = 1,
        AirwayOpening = 2,
        RecoveryPosition = 4,
        Blanket = 8,
        Cpr = 16,
        Reassure = 32
    }

    public enum BodyVariant
    {
        Adult,
        Child,
        Motorcyclist,
        Elderly,
        Worker
    }

    public enum BodyPose
    {
        Standing,
        Sitting,
        Lying
    }

    /// <summary>Why a casualty deteriorates if left untreated.</summary>
    public enum CriticalCondition
    {
        None,
        Haemorrhage,
        Airway,
        Breathing,
        Shock,
        HeadInjury
    }
}
