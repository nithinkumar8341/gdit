using UnityEngine;

namespace GoldenHour
{
    /// <summary>
    /// Creates coloured runtime materials by cloning a serialized template material, so the
    /// render-pipeline shader is always included in player builds (no Shader.Find at runtime).
    /// </summary>
    public static class MaterialFactory
    {
        public static Material Template { get; set; }

        public static Material Create(Color color, float metallic = 0f, float smoothness = 0.25f)
        {
            Material material = Template != null
                ? new Material(Template)
                : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            return material;
        }

        public static void Tint(Renderer renderer, Color color)
        {
            Material material = renderer.material;
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        }
    }
}
