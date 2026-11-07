using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoldenHour
{
    /// <summary>
    /// End-of-scenario debrief with tabs: Summary (score, stars, feedback), Timeline (key decisions and
    /// decision times), Casualties (first tag vs START truth and the START step), Map (top-down movement
    /// heatmap with casualty and hazard markers) and Stats (score breakdown, accuracy, difficulty).
    /// The educational disclaimer is shown on every tab.
    /// </summary>
    public static class DebriefScreen
    {
        public const string Title = "DEBRIEF";
        public static readonly string[] Tabs = { "Summary", "Timeline", "Casualties", "Map", "Stats" };
        public static bool HasTabs => true;
        public static string CurrentTab { get; private set; } = "Summary";
        public static Texture2D HeatmapTexture { get; private set; }
        public static int HeatmapCells { get; private set; }

        private const int HeatmapSize = 64;
        private static ModalWindow modal;

        public static void Show(IncidentDirector director, ModalWindow window)
        {
            modal = window;
            ShowTab(director, "Summary");
        }

        public static void ShowTab(IncidentDirector director, string tab)
        {
            if (modal == null || director == null || director.Run == null) return;
            CurrentTab = tab;
            RunRecord run = director.Run;
            Vector2 size = new Vector2(1240f, 860f);
            modal.Show(Title + "  |  " + run.scenarioTitle.ToUpperInvariant(), string.Empty, size, 100f, false);
            Transform panel = modal.Panel;
            for (int i = 0; i < Tabs.Length; i++)
            {
                string name = Tabs[i];
                UiFactory.Button(panel, name.ToUpperInvariant(), new Vector2(-480f + i * 240f, 320f), new Vector2(220f, 44f),
                    () => ShowTab(director, name), name == tab ? Palette.EmergencyRed : Palette.Slate);
            }

            TMP_Text content = UiFactory.Text(panel, string.Empty, 18, new Vector2(1160f, 560f), new Vector2(0f, -10f), TextAlignmentOptions.TopLeft);
            content.enableAutoSizing = true;
            content.fontSizeMin = 12f;
            content.fontSizeMax = 18f;
            content.overflowMode = TextOverflowModes.Truncate;
            switch (tab)
            {
                case "Timeline":
                    content.text = TimelineText(director, run);
                    break;
                case "Casualties":
                    content.text = CasualtiesText(run);
                    break;
                case "Map":
                    content.rectTransform.sizeDelta = new Vector2(560f, 560f);
                    content.rectTransform.anchoredPosition = new Vector2(300f, -10f);
                    content.text = MapLegend(director, run);
                    BuildMap(panel, director, run);
                    break;
                case "Stats":
                    content.text = StatsText(director, run);
                    break;
                default:
                    content.text = SummaryText(director, run);
                    break;
            }

            TMP_Text disclaimer = UiFactory.Text(panel, "<i>" + IncidentDirector.Disclaimer + "</i>", 13, new Vector2(1160f, 40f), new Vector2(0f, -318f),
                TextAlignmentOptions.Center, new Color32(205, 211, 216, 255));
            disclaimer.enableAutoSizing = true;
            disclaimer.fontSizeMin = 10f;
            disclaimer.fontSizeMax = 13f;
            float row = modal.RowY(0);
            UiFactory.Button(panel, "RETRY", new Vector2(-300f, row), new Vector2(260f, 54f), director.RestartScenario, Palette.Slate);
            Button next = UiFactory.Button(panel, "NEXT SCENARIO", new Vector2(0f, row), new Vector2(260f, 54f), director.PlayNextScenario);
            next.interactable = director.HasNextScenario;
            UiFactory.Button(panel, "MAIN MENU", new Vector2(300f, row), new Vector2(260f, 54f), director.ReturnToMenu, Palette.Slate);
        }

        // ------------------------------------------------------------- tabs

        private static string SummaryText(IncidentDirector director, RunRecord run)
        {
            StringBuilder body = new StringBuilder();
            body.Append("<size=130%>Score <b>").Append(run.score).Append(" / ").Append(run.maximumScore).Append("</b>   ").Append(Stars(run.stars))
                .Append("</size>\n");
            body.Append(director.EndReason).Append("\n");
            body.Append("Survivors <b>").Append(run.survivors).Append("</b>   Preventable deaths <b>").Append(run.preventableDeaths)
                .Append("</b>   Golden-hour time used <b>").Append(run.durationMinutes.ToString("0.0")).Append(" min</b>   Difficulty <b>")
                .Append(run.difficultyBand).Append("</b>\n\n");
            body.Append("<b>WHAT TO WORK ON</b>\n");
            foreach (string line in run.feedback) body.Append("- ").Append(line).Append("\n");
            body.Append("\n<color=#9AA4AD>Stars: 1 = 40% of the maximum, 2 = 65%, 3 = 85% with no preventable deaths.</color>");
            return body.ToString();
        }

        private static readonly HashSet<string> TimelineTypes = new HashSet<string>
        {
            "phase", "hazard", "dispatch", "command", "tag", "treat", "cpr", "handover", "transport", "death", "deterioration", "ambulance", "end"
        };

        private static string TimelineText(IncidentDirector director, RunRecord run)
        {
            StringBuilder body = new StringBuilder();
            List<CasualtyOutcome> decided = run.casualties.Where(c => c.decisionMinutes > 0f).ToList();
            if (decided.Count > 0)
            {
                CasualtyOutcome slowest = decided.OrderByDescending(c => c.decisionMinutes).First();
                body.Append("<b>Decision time</b> (first examination to first tag): average <b>")
                    .Append(decided.Average(c => c.decisionMinutes).ToString("0.0")).Append(" min</b>, slowest <b>").Append(slowest.casualtyId)
                    .Append("</b> ").Append(slowest.decisionMinutes.ToString("0.0")).Append(" min. START aims for under a minute per person.\n\n");
            }
            else
            {
                body.Append("<b>Decision time</b>: no casualty was tagged.\n\n");
            }

            List<TelemetryEvent> events = run.events.Where(e => TimelineTypes.Contains(e.type)).ToList();
            int skip = Mathf.Max(0, events.Count - 26);
            if (skip > 0) body.Append("<color=#9AA4AD>... ").Append(skip).Append(" earlier events (full log in the run JSON)</color>\n");
            foreach (TelemetryEvent e in events.Skip(skip))
            {
                body.Append("<color=#F6D171>").Append(e.minute.ToString("00.0")).Append(" min</color>  <b>").Append(e.type.ToUpperInvariant()).Append("</b>  ");
                if (!string.IsNullOrEmpty(e.casualtyId)) body.Append(e.casualtyId).Append(": ");
                body.Append(e.detail).Append("\n");
            }

            return body.ToString();
        }

        private static string CasualtiesText(RunRecord run)
        {
            StringBuilder body = new StringBuilder("<b>Each casualty: your first tag against START at that moment, the START step that applied, and the outcome.</b>\n");
            foreach (CasualtyOutcome c in run.casualties)
            {
                bool correct = c.firstTag == c.startTruthAtFirstTag;
                string mark = c.firstTag == "None" ? "<color=#F6D171>untagged</color>" : correct ? "<color=#7FE0A6>correct</color>" : "<color=#FF8A8A>" +
                    (Priority(c.firstTag) > Priority(c.startTruthAtFirstTag) ? "under-triage" : "over-triage") + "</color>";
                body.Append("<b>").Append(c.casualtyId).Append("</b> ").Append(c.displayName).Append("  first tag <b>").Append(c.firstTag).Append("</b> vs START <b>")
                    .Append(c.startTruthAtFirstTag).Append("</b> (").Append(mark).Append(")  <color=#9AA4AD>START step:</color> ").Append(Humanize(c.startStep))
                    .Append("\n      ").Append(c.outcome);
                if (c.decisionMinutes > 0f) body.Append("   decision ").Append(c.decisionMinutes.ToString("0.0")).Append(" min");
                if (c.retags > 0) body.Append("   retags ").Append(c.retags);
                if (!string.IsNullOrEmpty(c.treatments)) body.Append("   aid: ").Append(c.treatments);
                body.Append("\n");
            }

            return body.ToString();
        }

        /// <summary>"RespiratoryRateOver30" -> "respiratory rate over 30".</summary>
        public static string Humanize(string identifier)
        {
            if (string.IsNullOrEmpty(identifier)) return "-";
            string spaced = System.Text.RegularExpressions.Regex.Replace(identifier, "(?<=[a-z])(?=[A-Z0-9])", " ");
            return spaced.Substring(0, 1).ToUpperInvariant() + spaced.Substring(1).ToLowerInvariant();
        }

        private static int Priority(string tag)
        {
            switch (tag)
            {
                case "Red": return 0;
                case "Yellow": return 1;
                case "Green": return 2;
                case "Black": return 3;
                default: return 4;
            }
        }

        private static string StatsText(IncidentDirector director, RunRecord run)
        {
            StringBuilder body = new StringBuilder("<b>SCORE BREAKDOWN</b>\n");
            foreach (IGrouping<string, ScoreEntry> group in run.ledger.GroupBy(e => e.category).OrderByDescending(g => g.Sum(e => e.points)))
            {
                int sum = group.Sum(e => e.points);
                body.Append(Label(group.Key)).Append(": <b>").Append(sum >= 0 ? "+" : "").Append(sum).Append("</b>  <color=#9AA4AD>(")
                    .Append(group.Count()).Append(group.Count() == 1 ? " entry" : " entries").Append(")</color>\n");
            }

            int tagged = run.casualties.Count(c => c.firstTag != "None");
            int correct = run.casualties.Count(c => c.firstTag != "None" && c.firstTag == c.startTruthAtFirstTag);
            int under = run.casualties.Count(c => c.firstTag != "None" && Priority(c.firstTag) > Priority(c.startTruthAtFirstTag));
            int over = run.casualties.Count(c => c.firstTag != "None" && Priority(c.firstTag) < Priority(c.startTruthAtFirstTag));
            body.Append("\n<b>TRIAGE</b>\nFirst-tag accuracy <b>").Append(tagged > 0 ? Mathf.RoundToInt(100f * correct / tagged) : 0).Append("%</b> (")
                .Append(correct).Append("/").Append(tagged).Append(")   under-triage ").Append(under).Append("   over-triage ").Append(over).Append("\n");
            body.Append("\n<b>TIME</b>\nGolden-hour minutes used ").Append(run.durationMinutes.ToString("0.0")).Append("   real time ")
                .Append(Mathf.FloorToInt(run.realSeconds / 60f)).Append(":").Append(Mathf.FloorToInt(run.realSeconds % 60f).ToString("00")).Append("\n");
            body.Append("\n<b>DIFFICULTY</b>\nThis run: ").Append(run.difficultyBand).Append(" (deterioration x").Append(run.deteriorationScale.ToString("0.00"))
                .Append(", seed ").Append(run.seed).Append(")\nAdaptive difficulty uses your last three score ratios: 80%+ makes casualties deteriorate faster; ")
                .Append("45% or less slows them and turns hints on.");
            return body.ToString();
        }

        private static string Label(string category)
        {
            switch (category)
            {
                case "triage": return "Triage (first tags)";
                case "retriage": return "Re-triage";
                case "aid": return "First aid";
                case "handover": return "Handover order";
                case "dispatch": return "112 dispatch report";
                case "command": return "Walking-wounded command";
                case "bystander": return "Bystanders calmed";
                case "safety": return "Scene safety (hazards, danger zones)";
                case "gloves": return "Gloves";
                case "death": return "Preventable deaths";
                case "time": return "Time bonus";
                default: return category;
            }
        }

        // -------------------------------------------------------------- map

        private static string MapLegend(IncidentDirector director, RunRecord run)
        {
            StringBuilder body = new StringBuilder("<b>WHERE YOU WENT</b>\nTop-down view, north up. Brighter = more time spent there (")
                .Append(run.path.Count).Append(" position samples).\n\n");
            body.Append("Markers: <color=#FF5A5A><b>RED</b></color>  <color=#FFD34D><b>YELLOW</b></color>  <color=#5FD38A><b>GREEN</b></color>  ")
                .Append("<color=#9AA4AD><b>BLACK / untagged</b></color>  <color=#FF9A2E><b>! hazard</b></color>\n\n");
            body.Append("Good scene management keeps your path short: hazards first, then a sweep from the nearest casualty outwards, ")
                .Append("not back-and-forth across the scene.");
            return body.ToString();
        }

        private static void BuildMap(Transform panel, IncidentDirector director, RunRecord run)
        {
            Rect bounds = MapBounds(director, run);
            HeatmapTexture = BuildHeatmap(run, bounds, out int cells);
            HeatmapCells = cells;
            GameObject mapObject = new GameObject("Heatmap", typeof(RectTransform), typeof(RawImage));
            mapObject.transform.SetParent(panel, false);
            RectTransform rect = mapObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(520f, 520f);
            rect.anchoredPosition = new Vector2(-300f, -20f);
            RawImage image = mapObject.GetComponent<RawImage>();
            image.texture = HeatmapTexture;
            image.raycastTarget = false;
            foreach (Casualty casualty in director.Casualties)
            {
                Vector3 p = casualty.Sim.Transported ? casualty.SpawnPosition : casualty.transform.position;
                Marker(rect, bounds, p, TriageTagVisual.ColorFor(casualty.Record.HasTag ? casualty.Record.CurrentTag : TriageTag.Black), 12f, casualty.Id);
            }

            foreach (Hazard hazard in director.Hazards) Marker(rect, bounds, hazard.transform.position, new Color32(255, 154, 46, 255), 14f, "!");
        }

        private static void Marker(RectTransform map, Rect bounds, Vector3 world, Color color, float size, string label)
        {
            Vector2 uv = new Vector2((world.x - bounds.xMin) / bounds.width, (world.z - bounds.yMin) / bounds.height);
            Vector2 position = new Vector2((uv.x - 0.5f) * map.sizeDelta.x, (uv.y - 0.5f) * map.sizeDelta.y);
            GameObject dot = UiFactory.Panel(map, "Marker " + label, new Vector2(size, size), position, color);
            dot.GetComponent<Image>().raycastTarget = false;
            TMP_Text text = UiFactory.Text(map, label, 11, new Vector2(60f, 16f), position + new Vector2(0f, 12f), TextAlignmentOptions.Center);
            text.raycastTarget = false;
        }

        private static Rect MapBounds(IncidentDirector director, RunRecord run)
        {
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            void Add(float x, float z)
            {
                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
                minZ = Mathf.Min(minZ, z);
                maxZ = Mathf.Max(maxZ, z);
            }

            foreach (PathSample sample in run.path) Add(sample.x, sample.z);
            foreach (Casualty casualty in director.Casualties) Add(casualty.SpawnPosition.x, casualty.SpawnPosition.z);
            foreach (Hazard hazard in director.Hazards) Add(hazard.transform.position.x, hazard.transform.position.z);
            if (minX > maxX) return new Rect(-20f, -20f, 40f, 40f);
            float span = Mathf.Max(maxX - minX, maxZ - minZ) + 8f;
            Vector2 centre = new Vector2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
            return new Rect(centre.x - span * 0.5f, centre.y - span * 0.5f, span, span);
        }

        /// <summary>Accumulates path samples into a blurred grid and colours it with a dark-to-amber ramp.</summary>
        public static Texture2D BuildHeatmap(RunRecord run, Rect bounds, out int cells)
        {
            float[,] grid = new float[HeatmapSize, HeatmapSize];
            foreach (PathSample sample in run.path)
            {
                int gx = Mathf.Clamp(Mathf.FloorToInt((sample.x - bounds.xMin) / bounds.width * HeatmapSize), 0, HeatmapSize - 1);
                int gz = Mathf.Clamp(Mathf.FloorToInt((sample.z - bounds.yMin) / bounds.height * HeatmapSize), 0, HeatmapSize - 1);
                for (int dx = -2; dx <= 2; dx++)
                {
                    for (int dz = -2; dz <= 2; dz++)
                    {
                        int x = gx + dx, z = gz + dz;
                        if (x < 0 || z < 0 || x >= HeatmapSize || z >= HeatmapSize) continue;
                        grid[x, z] += 1f / (1f + dx * dx + dz * dz);
                    }
                }
            }

            float max = 0.0001f;
            foreach (float value in grid) max = Mathf.Max(max, value);
            Texture2D texture = new Texture2D(HeatmapSize, HeatmapSize, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            Color low = new Color32(22, 28, 36, 255);
            Color mid = new Color32(150, 40, 40, 255);
            Color high = new Color32(255, 210, 90, 255);
            cells = 0;
            for (int x = 0; x < HeatmapSize; x++)
            {
                for (int z = 0; z < HeatmapSize; z++)
                {
                    float t = Mathf.Sqrt(grid[x, z] / max);
                    if (grid[x, z] > 0f) cells++;
                    Color color = t < 0.5f ? Color.Lerp(low, mid, t * 2f) : Color.Lerp(mid, high, (t - 0.5f) * 2f);
                    bool gridLine = x % 8 == 0 || z % 8 == 0;
                    texture.SetPixel(x, z, gridLine && t < 0.05f ? new Color32(34, 42, 52, 255) : color);
                }
            }

            texture.Apply();
            return texture;
        }

        public static string Stars(int stars)
        {
            return "<color=#F6D171>" + new string('*', Mathf.Clamp(stars, 0, 3)) + "</color><color=#59626B>" +
                   new string('*', 3 - Mathf.Clamp(stars, 0, 3)) + "</color>";
        }
    }
}
