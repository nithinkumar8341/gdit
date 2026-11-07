using System.Collections.Generic;

namespace GoldenHour
{
    public enum PatientTaskKind
    {
        OpenAirway = 0,
        ControlBleeding = 1,
        Recheck = 2,
        RecoveryPosition = 3,
        Tag = 4,
        KeepWarm = 5
    }

    /// <summary>What the player currently knows about one casualty (findings, tag, aid given).</summary>
    public struct PatientTaskInput
    {
        public string casualtyId;
        /// <summary>Care the casualty needs (indicated treatments without reassurance and CPR).</summary>
        public Treatment careNeeded;
        public Treatment applied;
        /// <summary>False once the casualty is transported, in the green zone or deceased.</summary>
        public bool active;
        public bool examined;
        public bool tagged;
        public bool breathingChecked;
        public bool bleedingChecked;
        public bool commandsChecked;
        /// <summary>The condition changed after the last examination (the casualty said they feel worse).</summary>
        public bool worsenedSinceExam;
    }

    public sealed class PatientTask
    {
        public string casualtyId;
        public PatientTaskKind kind;
        public bool done;

        public string Key => casualtyId + ":" + kind;
        public string Text => PatientTasks.Describe(kind, casualtyId);
    }

    /// <summary>
    /// Live per-casualty objectives. A need appears only after the finding that would reveal it (bleeding after the
    /// bleeding check, airway after "not breathing" ...), so the list never gives away hidden physiology, and it only
    /// asks for care that is indicated (the same source as the "give indicated aid" objective).
    /// </summary>
    public static class PatientTasks
    {
        public static string Describe(PatientTaskKind kind, string id)
        {
            switch (kind)
            {
                case PatientTaskKind.OpenAirway: return "Open the airway of " + id;
                case PatientTaskKind.ControlBleeding: return "Control bleeding on " + id;
                case PatientTaskKind.Recheck: return "Re-check " + id + " (condition changed)";
                case PatientTaskKind.RecoveryPosition: return "Recovery position for " + id;
                case PatientTaskKind.Tag: return "Tag " + id;
                default: return "Keep " + id + " warm (blanket)";
            }
        }

        /// <summary>Every revealed task (pending and done), pending first, then by priority and input order.</summary>
        public static List<PatientTask> Build(IEnumerable<PatientTaskInput> casualties)
        {
            List<(PatientTask task, int order)> found = new List<(PatientTask, int)>();
            int order = 0;
            foreach (PatientTaskInput input in casualties)
            {
                order++;
                if (!input.active) continue;
                void Need(Treatment care, bool revealed, PatientTaskKind kind)
                {
                    if ((input.careNeeded & care) == 0 || !revealed) return;
                    found.Add((new PatientTask { casualtyId = input.casualtyId, kind = kind, done = (input.applied & care) != 0 }, order));
                }

                Need(Treatment.AirwayOpening, input.breathingChecked, PatientTaskKind.OpenAirway);
                Need(Treatment.PressureDressing, input.bleedingChecked, PatientTaskKind.ControlBleeding);
                Need(Treatment.RecoveryPosition, input.commandsChecked, PatientTaskKind.RecoveryPosition);
                Need(Treatment.Blanket, input.tagged, PatientTaskKind.KeepWarm);
                if (input.examined) found.Add((new PatientTask { casualtyId = input.casualtyId, kind = PatientTaskKind.Tag, done = input.tagged }, order));
                if (input.tagged && input.worsenedSinceExam)
                {
                    found.Add((new PatientTask { casualtyId = input.casualtyId, kind = PatientTaskKind.Recheck }, order));
                }
            }

            found.Sort((a, b) =>
            {
                if (a.task.done != b.task.done) return a.task.done ? 1 : -1;
                int kind = a.task.kind.CompareTo(b.task.kind);
                return kind != 0 ? kind : a.order.CompareTo(b.order);
            });
            List<PatientTask> tasks = new List<PatientTask>(found.Count);
            foreach ((PatientTask task, int _) in found) tasks.Add(task);
            return tasks;
        }
    }
}
