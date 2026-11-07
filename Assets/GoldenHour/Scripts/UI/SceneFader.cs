using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GoldenHour
{
    /// <summary>
    /// Persistent full-screen fade used for every scene change: fades to black, loads the scene,
    /// then fades back in. Runs on unscaled time so it also works while the game is paused.
    /// </summary>
    public sealed class SceneFader : MonoBehaviour
    {
        public const float FadeDuration = 0.35f;

        private static SceneFader instance;
        private CanvasGroup group;
        private TMPro.TMP_Text loadingLabel;

        /// <summary>Title and tip shown on the loading screen during the next scene change.</summary>
        public static string LoadingText { get; set; } = string.Empty;
        private bool loading;

        public static bool IsLoading => instance != null && instance.loading;

        /// <summary>Current overlay opacity (0 = clear, 1 = black).</summary>
        public static float Alpha => instance != null ? instance.group.alpha : 0f;

        public static void LoadScene(string sceneName)
        {
            Ensure().StartLoad(sceneName);
        }

        /// <summary>Fades in from black when a scene opens (called by scene controllers on start).</summary>
        public static void FadeIn()
        {
            SceneFader fader = Ensure();
            if (!fader.loading) fader.StartCoroutine(fader.Fade(1f, 0f));
        }

        private static SceneFader Ensure()
        {
            if (instance != null) return instance;
            GameObject root = new GameObject("Scene Fader", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasGroup));
            DontDestroyOnLoad(root);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            GameObject image = new GameObject("Fade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            image.transform.SetParent(root.transform, false);
            RectTransform rect = image.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            image.GetComponent<Image>().color = new Color32(6, 8, 11, 255);
            TMPro.TMP_Text label = UiFactory.Text(root.transform, string.Empty, 24, new Vector2(1100f, 240f), new Vector2(0f, -60f),
                TMPro.TextAlignmentOptions.Center);
            root.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            root.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1600f, 900f);
            instance = root.AddComponent<SceneFader>();
            instance.group = root.GetComponent<CanvasGroup>();
            instance.loadingLabel = label;
            instance.group.alpha = 0f;
            instance.group.blocksRaycasts = false;
            instance.group.interactable = false;
            return instance;
        }

        private void StartLoad(string sceneName)
        {
            if (loading) return;
            StopAllCoroutines();
            StartCoroutine(LoadRoutine(sceneName));
        }

        private IEnumerator LoadRoutine(string sceneName)
        {
            loading = true;
            group.blocksRaycasts = true;
            loadingLabel.text = LoadingText;
            yield return Fade(group.alpha, 1f);
            Time.timeScale = 1f;
            float shownUntil = Time.realtimeSinceStartup + (string.IsNullOrEmpty(LoadingText) ? 0f : 1.2f);
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
            while (operation != null && !operation.isDone) yield return null;
            while (Time.realtimeSinceStartup < shownUntil) yield return null;
            yield return null;
            LoadingText = string.Empty;
            loading = false;
            yield return Fade(1f, 0f);
            loadingLabel.text = string.Empty;
        }

        private IEnumerator Fade(float from, float to)
        {
            group.blocksRaycasts = true;
            float elapsed = 0f;
            while (elapsed < FadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / FadeDuration));
                yield return null;
            }

            group.alpha = to;
            group.blocksRaycasts = to > 0.01f;
        }
    }
}
