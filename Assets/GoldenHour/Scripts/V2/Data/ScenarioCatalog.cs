using System.Collections.Generic;
using UnityEngine;

namespace GoldenHour
{
    /// <summary>The campaign: scenarios in order.</summary>
    [CreateAssetMenu(menuName = "Golden Hour/Scenario Catalog")]
    public sealed class ScenarioCatalog : ScriptableObject
    {
        public List<ScenarioData> scenarios = new List<ScenarioData>();

        public ScenarioData Find(string scenarioId)
        {
            foreach (ScenarioData scenario in scenarios)
            {
                if (scenario != null && scenario.scenarioId == scenarioId) return scenario;
            }

            return scenarios.Count > 0 ? scenarios[0] : null;
        }
    }
}
