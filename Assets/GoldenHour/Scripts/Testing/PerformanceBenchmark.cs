using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace GoldenHour
{
    /// <summary>
    /// Frame-time benchmark of the player build. Inactive unless launched with
    /// <c>-goldenhour-benchmark &lt;outputDirectory&gt; [-goldenhour-preset Low|Medium|High]</c>.
    /// For each scenario: loads it, skips the intro, starts the incident, warms up for 2 s, then turns
    /// the player 360 degrees over 10 s while recording frame times. Writes benchmark.txt (GPU,
    /// resolution, preset, average FPS, 1% low, worst frame) and exits with code 0.
    /// </summary>
    public sealed class PerformanceBenchmark : MonoBehaviour
    {
        private const string Argument = "-goldenhour-benchmark";
        private const string PresetArgument = "-goldenhour-preset";
        private string outputDirectory;
        private static GraphicsPreset savedPreset;
        private readonly List<string> lines = new List<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, Argument);
            if (index < 0 || index + 1 >= args.Length) return;
            GameObject runner = new GameObject("Performance Benchmark");
            DontDestroyOnLoad(runner);
            PerformanceBenchmark benchmark = runner.AddComponent<PerformanceBenchmark>();
            benchmark.outputDirectory = Path.GetFullPath(args[index + 1]);
            int preset = Array.IndexOf(args, PresetArgument);
            savedPreset = GameSettings.Graphics;
            GraphicsPreset chosen = GraphicsPreset.Medium;
            if (preset >= 0 && preset + 1 < args.Length) Enum.TryParse(args[preset + 1], true, out chosen);
            GameSettings.Graphics = chosen;
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(outputDirectory);
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Session.SeedOverride = 4242;
            Session.DifficultyOverride = Difficulty.Normal;
            ProfileStore.OverrideDirectory = Path.Combine(outputDirectory, "profile");
            Cutscene.Enabled = false;
            Write("Golden Hour benchmark | " + DateTime.Now.ToString("u") + " | Unity " + Application.unityVersion);
            Write("GPU: " + SystemInfo.graphicsDeviceName + " (" + SystemInfo.graphicsDeviceType + ", " + SystemInfo.graphicsMemorySize + " MB)");
            Write("CPU: " + SystemInfo.processorType + " | RAM " + SystemInfo.systemMemorySize + " MB");
            yield return new WaitForSecondsRealtime(1f);
            Write("Resolution: " + Screen.width + "x" + Screen.height + " " + Screen.fullScreenMode + " | preset " + GameSettings.Graphics +
                  " | vSync off, uncapped");

            foreach (string scenario in new[] { "tutorial", "highway", "bus", "building" })
            {
                Session.ScenarioId = scenario;
                SceneFader.LoadScene("Samplespace");
                float timeout = Time.realtimeSinceStartup + 40f;
                while (Time.realtimeSinceStartup < timeout && (SceneFader.IsLoading || IncidentDirector.Instance == null ||
                                                              IncidentDirector.Instance.Phase != IncidentPhase.Briefing))
                {
                    yield return null;
                }

                IncidentDirector director = IncidentDirector.Instance;
                if (director == null)
                {
                    Write(scenario + ": FAILED TO LOAD");
                    continue;
                }

                director.BeginScenario();
                director.CollectKit();
                yield return new WaitForSecondsRealtime(2f);
                List<float> frames = new List<float>();
                Transform body = director.player.transform;
                float start = Time.realtimeSinceStartup;
                float startYaw = body.eulerAngles.y;
                while (Time.realtimeSinceStartup - start < 10f)
                {
                    float t = (Time.realtimeSinceStartup - start) / 10f;
                    director.player.Teleport(body.position, startYaw + t * 360f, 8f);
                    yield return null;
                    frames.Add(Time.unscaledDeltaTime);
                }

                frames.Sort();
                float average = frames.Count / frames.Sum();
                int lowCount = Mathf.Max(1, frames.Count / 100);
                float onePercentLow = lowCount / frames.Skip(frames.Count - lowCount).Sum();
                Write(scenario + ": average " + average.ToString("0.0") + " fps | 1% low " + onePercentLow.ToString("0.0") + " fps | worst frame " +
                      (frames[frames.Count - 1] * 1000f).ToString("0.0") + " ms | " + frames.Count + " frames | draw distance " +
                      director.playerCamera.GetComponent<Camera>().farClipPlane.ToString("0") + " m");
            }

            File.WriteAllLines(Path.Combine(outputDirectory, "benchmark.txt"), lines);
            GameSettings.Graphics = savedPreset;
            Application.Quit(0);
        }

        private void Write(string line)
        {
            lines.Add(line);
            Debug.Log("[Benchmark] " + line);
        }
    }
}
