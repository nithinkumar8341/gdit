using System;

namespace GoldenHour
{
    public enum CutsceneKind
    {
        Opening,
        Ending
    }

    /// <summary>Opening fly-through and end cutscenes. Returns false when no cutscene is played.</summary>
    public static class Cutscene
    {
        public static bool Enabled { get; set; } = true;

        public static bool Play(IncidentDirector director, CutsceneKind kind, Action finished)
        {
            return false;
        }

        public static void Skip()
        {
        }
    }
}
