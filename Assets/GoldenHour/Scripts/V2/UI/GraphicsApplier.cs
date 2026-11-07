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

        public static void Apply()
        {
            UniversalRenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            GraphicsPreset preset = GameSettings.Graphics;
            float scale = preset == GraphicsPreset.Low ? 0.75f : preset == GraphicsPreset.Medium ? 0.9f : 1f;
            float shadows = preset == GraphicsPreset.Low ? 20f : preset == GraphicsPreset.Medium ? 40f : 60f;
            if (pipeline != null)
            {
                pipeline.renderScale = scale;
                pipeline.shadowDistance = shadows;
            }

            RenderScale = scale;
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = 60;
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
