using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace GoldenHour
{
    /// <summary>
    /// Applies the Low / Medium / High graphics preset at runtime: render scale, shadow distance and
    /// post-processing; and the UI text scale. Medium is the integrated-graphics target.
    /// </summary>
    public static class GraphicsApplier
    {
        public static float RenderScale { get; private set; } = 1f;

        /// <summary>Benchmark only: no vSync and no frame cap.</summary>
        public static bool Uncapped { get; set; }

        /// <summary>True when the screen-space ambient occlusion renderer feature is enabled (High only).</summary>
        public static bool AmbientOcclusionActive { get; private set; }

        /// <summary>Turns a renderer feature (matched by type name) on or off on every renderer of the pipeline.</summary>
        private static void SetFeatureActive(UniversalRenderPipelineAsset pipeline, string typeName, bool active)
        {
            System.Reflection.FieldInfo field = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (!(field?.GetValue(pipeline) is ScriptableRendererData[] list)) return;
            foreach (ScriptableRendererData data in list)
            {
                if (data == null) continue;
                foreach (ScriptableRendererFeature feature in data.rendererFeatures)
                {
                    if (feature == null || !feature.GetType().Name.Contains(typeName)) continue;
                    feature.SetActive(active);
                    AmbientOcclusionActive = active;
                }
            }
        }

        public static void Apply()
        {
            UniversalRenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            GraphicsPreset preset = GameSettings.Graphics;
            float scale = preset == GraphicsPreset.Low ? 0.75f : preset == GraphicsPreset.Medium ? 0.9f : 1f;
            float shadows = preset == GraphicsPreset.Low ? 20f : preset == GraphicsPreset.Medium ? 30f : 60f;
            if (pipeline != null)
            {
                pipeline.renderScale = scale;
                pipeline.shadowDistance = shadows;
                pipeline.shadowCascadeCount = preset == GraphicsPreset.High ? 2 : 1;
                SetFeatureActive(pipeline, "ScreenSpaceAmbientOcclusion", preset == GraphicsPreset.High);
            }

            RenderScale = scale;
            QualitySettings.vSyncCount = Uncapped ? 0 : 1;
            Application.targetFrameRate = Uncapped ? -1 : 60;
            foreach (Camera camera in Camera.allCameras)
            {
                UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
                if (data != null) data.renderPostProcessing = preset != GraphicsPreset.Low;
            }

            foreach (CanvasScaler scaler in Object.FindObjectsByType<CanvasScaler>())
            {
                if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize) scaler.referenceResolution = UiFactory.ReferenceResolution;
            }
        }
    }
}
