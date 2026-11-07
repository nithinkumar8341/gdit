using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace GoldenHour
{
    public enum CutsceneKind
    {
        Opening,
        Ending
    }

    /// <summary>
    /// Opening fly-in and ending orbit, filmed by a separate presentation camera with letterbox
    /// bars and captions. Any key (Esc, E, Space) skips. Returns false when disabled.
    /// </summary>
    public static class Cutscene
    {
        public const float OpeningSeconds = 7f;
        public const float EndingSeconds = 5f;

        private static Runner runner;

        public static bool Enabled { get; set; } = true;
        public static bool Playing => runner != null;
        public static CutsceneKind Current { get; private set; }

        public static bool Play(IncidentDirector director, CutsceneKind kind, Action finished)
        {
            if (!Enabled || director == null) return false;
            Stop();
            Current = kind;
            runner = new GameObject("Cutscene").AddComponent<Runner>();
            runner.Begin(director, kind, finished);
            return true;
        }

        public static void Skip()
        {
            if (runner != null) runner.Finish();
        }

        private static void Stop()
        {
            if (runner != null) UnityEngine.Object.Destroy(runner.gameObject);
            runner = null;
        }

        private sealed class Runner : MonoBehaviour
        {
            private Camera shot;
            private Camera playerView;
            private Action done;
            private bool finished;
            private TMP_Text caption;
            private TMP_Text subCaption;
            private CanvasGroup fade;

            public void Begin(IncidentDirector director, CutsceneKind kind, Action finished)
            {
                done = finished;
                playerView = director.playerCamera != null ? director.playerCamera.GetComponent<Camera>() : Camera.main;
                shot = new GameObject("Cutscene Camera").AddComponent<Camera>();
                shot.transform.SetParent(transform, false);
                if (playerView != null)
                {
                    shot.CopyFrom(playerView);
                    shot.depth = playerView.depth + 5f;
                }

                UniversalAdditionalCameraData data = shot.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                BuildOverlay(director, kind);
                StartCoroutine(kind == CutsceneKind.Opening ? Opening(director) : Ending(director));
            }

            private void BuildOverlay(IncidentDirector director, CutsceneKind kind)
            {
                Canvas canvas = UiFactory.CreateCanvas("Cutscene Overlay");
                canvas.sortingOrder = 70;
                canvas.transform.SetParent(transform, false);
                Bar(canvas.transform, true);
                Bar(canvas.transform, false);
                string title = director.Scenario != null ? director.Scenario.title.ToUpperInvariant() : "GOLDEN HOUR";
                string line = kind == CutsceneKind.Opening
                    ? (director.Scenario != null && director.Scenario.night ? "Night. " : "") + "You are the first trained person on scene."
                    : director.EndReason;
                caption = UiFactory.Text(canvas.transform, kind == CutsceneKind.Opening ? title : "SCENE " + (director.AllResolved() ? "CLEARED" : "HANDED OVER"),
                    40, new Vector2(1400f, 60f), new Vector2(0f, -360f), TextAlignmentOptions.Center);
                caption.fontStyle = FontStyles.Bold;
                subCaption = UiFactory.Text(canvas.transform, line, 22, new Vector2(1400f, 40f), new Vector2(0f, -410f), TextAlignmentOptions.Center,
                    Palette.LightGrey);
                UiFactory.Text(canvas.transform, "Press Esc, E or Space to skip", 15, new Vector2(500f, 30f), new Vector2(690f, 470f),
                    TextAlignmentOptions.Right, Palette.LightGrey);
                GameObject fadeObject = UiFactory.Panel(canvas.transform, "Fade", Vector2.zero, Vector2.zero, Color.black, true);
                fade = fadeObject.AddComponent<CanvasGroup>();
                fade.alpha = kind == CutsceneKind.Opening ? 1f : 0f;
                fade.blocksRaycasts = false;
            }

            private static void Bar(Transform canvas, bool top)
            {
                GameObject bar = UiFactory.Panel(canvas, top ? "Letterbox Top" : "Letterbox Bottom", Vector2.zero, Vector2.zero, Color.black);
                RectTransform rect = bar.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, top ? 0.88f : 0f);
                rect.anchorMax = new Vector2(1f, top ? 1f : 0.12f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            private IEnumerator Opening(IncidentDirector director)
            {
                Vector3 centre = director.SceneCentre();
                Vector3 end = playerView != null ? playerView.transform.position : centre + new Vector3(0f, 1.6f, -12f);
                Quaternion endRotation = playerView != null ? playerView.transform.rotation : Quaternion.LookRotation(centre - end);
                float time = 0f;
                while (time < OpeningSeconds)
                {
                    time += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(time / OpeningSeconds);
                    float eased = t * t * (3f - 2f * t);
                    float angle = Mathf.Lerp(-70f, 0f, eased);
                    Vector3 orbit = centre + Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, Mathf.Lerp(16f, 6f, eased), -Mathf.Lerp(26f, 18f, eased));
                    Vector3 position = t < 0.75f ? orbit : Vector3.Lerp(orbit, end, (t - 0.75f) / 0.25f);
                    Quaternion look = Quaternion.LookRotation((centre + Vector3.up) - position);
                    shot.transform.SetPositionAndRotation(position, t < 0.75f ? look : Quaternion.Slerp(look, endRotation, (t - 0.75f) / 0.25f));
                    fade.alpha = Mathf.Clamp01(1f - time / 1.2f) + Mathf.Clamp01((time - (OpeningSeconds - 0.5f)) / 0.5f) * 0.6f;
                    caption.alpha = subCaption.alpha = Mathf.Clamp01((time - 0.8f) / 0.8f) * Mathf.Clamp01((OpeningSeconds - 1f - time) / 0.8f);
                    yield return null;
                }

                Finish();
            }

            private IEnumerator Ending(IncidentDirector director)
            {
                Vector3 centre = director.SceneCentre();
                float time = 0f;
                while (time < EndingSeconds)
                {
                    time += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(time / EndingSeconds);
                    Vector3 position = centre + Quaternion.Euler(0f, 30f + t * 50f, 0f) * new Vector3(0f, 7f + t * 6f, -16f - t * 6f);
                    shot.transform.SetPositionAndRotation(position, Quaternion.LookRotation(centre + Vector3.up - position));
                    caption.alpha = subCaption.alpha = Mathf.Clamp01(time / 0.8f);
                    fade.alpha = Mathf.Clamp01((time - (EndingSeconds - 0.8f)) / 0.8f) * 0.85f;
                    yield return null;
                }

                Finish();
            }

            public void Finish()
            {
                if (finished) return;
                finished = true;
                Action callback = done;
                runner = null;
                Destroy(gameObject);
                callback?.Invoke();
            }
        }
    }
}
