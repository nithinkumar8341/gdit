using System.Text;
using UnityEngine;

namespace GoldenHour
{
    /// <summary>End-of-scenario debrief: score, stars, outcomes and feedback.</summary>
    public static class DebriefScreen
    {
        public const string Title = "DEBRIEF";
        public static bool HasTabs => false;
        public static readonly string[] Tabs = new string[0];
        public static Texture2D HeatmapTexture { get; private set; }
        public static int HeatmapCells { get; private set; }

        public static void ShowTab(IncidentDirector director, string tab)
        {
        }

        public static void Show(IncidentDirector director, ModalWindow modal)
        {
            RunRecord run = director.Run;
            StringBuilder body = new StringBuilder();
            body.Append("<b>").Append(run.scenarioTitle).Append("</b>   Score <b>").Append(run.score).Append(" / ").Append(run.maximumScore)
                .Append("</b>   ").Append(Stars(run.stars)).Append("\n");
            body.Append(director.EndReason).Append("\n\n");
            foreach (CasualtyOutcome outcome in run.casualties)
            {
                body.Append(outcome.casualtyId).Append(": first tag ").Append(outcome.firstTag).Append(" (START ").Append(outcome.startTruthAtFirstTag)
                    .Append(") - ").Append(outcome.outcome).Append("\n");
            }

            body.Append("\n");
            foreach (string line in run.feedback) body.Append("- ").Append(line).Append("\n");
            body.Append("\n<size=85%><color=#CDD3D8><i>").Append(IncidentDirector.Disclaimer).Append("</i></color></size>");
            modal.Show(Title, body.ToString(), new Vector2(1240f, 840f), 100f, false);
            UiFactory.Button(modal.Panel, "RETRY", new Vector2(-300f, modal.RowY(0)), new Vector2(260f, 54f), director.RestartScenario, Palette.Slate);
            UiFactory.Button(modal.Panel, "NEXT SCENARIO", new Vector2(0f, modal.RowY(0)), new Vector2(260f, 54f), director.PlayNextScenario);
            UiFactory.Button(modal.Panel, "MAIN MENU", new Vector2(300f, modal.RowY(0)), new Vector2(260f, 54f), director.ReturnToMenu, Palette.Slate);
        }

        public static string Stars(int stars)
        {
            return "<color=#F6D171>" + new string('*', Mathf.Clamp(stars, 0, 3)) + "</color><color=#59626B>" +
                   new string('*', 3 - Mathf.Clamp(stars, 0, 3)) + "</color>";
        }
    }
}
