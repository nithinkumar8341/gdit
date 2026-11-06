using System;
using System.Collections.Generic;
using System.Text;

namespace GoldenHour
{
    /// <summary>Ordered scene objectives with one-time completion callbacks.</summary>
    public sealed class ObjectiveTracker
    {
        public const int ReachScene = 0;
        public const int ExamineAll = 1;
        public const int TriageAll = 2;
        public const int SelectSupply = 3;
        public const int CompleteAid = 4;

        private static readonly string[] Descriptions =
        {
            "Reach the accident scene",
            "Examine every injured person",
            "Assign a triage priority to everyone",
            "Open the kit and select a suitable supply",
            "Complete simulated aid for everyone"
        };

        private readonly HashSet<int> completed = new HashSet<int>();

        public event Action<int> Completed;

        public int Count => Descriptions.Length;
        public int CompletedCount => completed.Count;

        public bool IsComplete(int index) => completed.Contains(index);

        public void Set(int index, bool isComplete)
        {
            if (isComplete && completed.Add(index))
            {
                Completed?.Invoke(index);
            }
        }

        public string Describe()
        {
            StringBuilder text = new StringBuilder("<b>SCENE OBJECTIVES</b>\n");
            for (int i = 0; i < Descriptions.Length; i++)
            {
                text.Append(completed.Contains(i)
                    ? "<color=#5CC98A><b>[X]</b></color> <color=#AEB7BF>"
                    : "<color=#F6F7F8>[   ] ");
                text.Append(Descriptions[i]).Append("</color>\n");
            }

            return text.ToString();
        }
    }
}
