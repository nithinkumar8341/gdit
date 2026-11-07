using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoldenHour
{
    [Serializable]
    public sealed class FeedbackAnswer
    {
        public string question;
        public int rating;
    }

    [Serializable]
    public sealed class FeedbackResponse
    {
        public string savedUtc;
        public string lastScenario;
        public int lastScore;
        public int lastStars;
        public List<FeedbackAnswer> answers = new List<FeedbackAnswer>();
        public string comment;
    }

    /// <summary>
    /// In-game playtest questionnaire (PLAYTEST_PLAN.md): eight 1-5 Likert items plus a comment,
    /// saved as JSON under persistentDataPath/Feedback.
    /// </summary>
    public static class FeedbackForm
    {
        public static readonly string[] Questions =
        {
            "I understood the START triage steps after playing.",
            "Examining by actions (breathing, circulation, commands) felt meaningful.",
            "The golden-hour clock created real time pressure.",
            "My decisions had clear consequences.",
            "The debrief explained why my decisions were right or wrong.",
            "I felt immersed in the scene (sound, lighting, characters).",
            "The controls and interface were easy to use.",
            "I would like to replay a scenario to improve."
        };

        private static readonly int[] Ratings = new int[Questions.Length];
        private static TMP_InputField comment;
        private static string commentText = string.Empty;

        public static string LastSavedPath { get; private set; }

        public static void Build(Transform view, Action back)
        {
            UiFactory.Text(view, "PLAYTEST FEEDBACK", 32, new Vector2(800f, 50f), new Vector2(0f, 368f), TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
            UiFactory.Text(view, "1 = strongly disagree, 5 = strongly agree. Answers are saved on this computer only.", 15, new Vector2(1000f, 30f),
                new Vector2(0f, 330f), TextAlignmentOptions.Center, Palette.LightGrey);
            for (int q = 0; q < Questions.Length; q++)
            {
                float y = 280f - q * 56f;
                UiFactory.Text(view, (q + 1) + ". " + Questions[q], 16, new Vector2(640f, 50f), new Vector2(-230f, y), TextAlignmentOptions.MidlineLeft);
                for (int r = 1; r <= 5; r++)
                {
                    int question = q;
                    int rating = r;
                    UiFactory.Button(view, r.ToString(), new Vector2(170f + r * 64f, y), new Vector2(56f, 42f), () =>
                    {
                        Ratings[question] = rating;
                        if (comment != null) commentText = comment.text;
                        Build(Clear(view), back);
                    }, Ratings[q] == r ? Palette.EmergencyRed : Palette.Slate);
                }
            }

            comment = InputField(view, new Vector2(0f, -200f), new Vector2(1000f, 90f), "Optional comment: what confused you, what would you change?");
            comment.text = commentText;
            UiFactory.Button(view, "SAVE ANSWERS", new Vector2(-150f, -320f), new Vector2(260f, 52f), () =>
            {
                Save(comment != null ? comment.text : string.Empty);
                back();
            });
            UiFactory.Button(view, "BACK", new Vector2(150f, -320f), new Vector2(220f, 52f), back, Palette.Slate);
        }

        private static Transform Clear(Transform view)
        {
            for (int i = view.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(view.GetChild(i).gameObject);
            return view;
        }

        public static void SetRating(int question, int rating)
        {
            Ratings[question] = Mathf.Clamp(rating, 0, 5);
        }

        public static string Save(string text)
        {
            FeedbackResponse response = new FeedbackResponse
            {
                savedUtc = DateTime.UtcNow.ToString("o"),
                lastScenario = Session.LastRun != null ? Session.LastRun.scenarioId : string.Empty,
                lastScore = Session.LastRun != null ? Session.LastRun.score : 0,
                lastStars = Session.LastRun != null ? Session.LastRun.stars : 0,
                comment = text ?? string.Empty
            };
            for (int i = 0; i < Questions.Length; i++) response.answers.Add(new FeedbackAnswer { question = Questions[i], rating = Ratings[i] });
            string folder = Path.Combine(ProfileStore.Directory, "Feedback");
            Directory.CreateDirectory(folder);
            LastSavedPath = Path.Combine(folder, "feedback_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".json");
            File.WriteAllText(LastSavedPath, JsonUtility.ToJson(response, true));
            return LastSavedPath;
        }

        private static TMP_InputField InputField(Transform parent, Vector2 position, Vector2 size, string placeholder)
        {
            GameObject root = UiFactory.Panel(parent, "Comment Field", size, position, new Color32(40, 48, 58, 255));
            TMP_InputField field = root.AddComponent<TMP_InputField>();
            GameObject area = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            area.transform.SetParent(root.transform, false);
            RectTransform areaRect = area.GetComponent<RectTransform>();
            areaRect.anchorMin = Vector2.zero;
            areaRect.anchorMax = Vector2.one;
            areaRect.offsetMin = new Vector2(10f, 6f);
            areaRect.offsetMax = new Vector2(-10f, -6f);
            TMP_Text text = UiFactory.Text(area.transform, string.Empty, 17, size - new Vector2(20f, 12f), Vector2.zero, TextAlignmentOptions.TopLeft);
            Stretch(text.rectTransform);
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Overflow;
            TMP_Text hint = UiFactory.Text(area.transform, placeholder, 17, size - new Vector2(20f, 12f), Vector2.zero, TextAlignmentOptions.TopLeft,
                new Color32(150, 158, 166, 255));
            Stretch(hint.rectTransform);
            hint.fontStyle = FontStyles.Italic;
            field.textViewport = areaRect;
            field.textComponent = text;
            field.placeholder = hint;
            field.lineType = TMP_InputField.LineType.MultiLineNewline;
            field.characterLimit = 600;
            field.targetGraphic = root.GetComponent<Image>();
            return field;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
