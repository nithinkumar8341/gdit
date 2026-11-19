using System.IO;
using UnityEditor;

namespace GoldenHour.EditorTools
{
    /// <summary>
    /// v2 Blender exports keep their object hierarchy (rig joints, vehicle parts), so Unity bakes the
    /// Blender-to-Unity axis conversion into that hierarchy on import. Roots stay at identity rotation
    /// and joint frames are consistent.
    /// </summary>
    public sealed class V2ModelPostprocessor : AssetPostprocessor
    {
        public static readonly string[] Models =
        {
            "RiggedHuman", "RiggedChild", "Ambulance", "PoliceCar", "FireTruck", "Bus", "Motorbike", "Stretcher", "V2Props",
            "TreatmentProps", "FirstPersonHands"
        };

        /// <summary>Diagnostics: logs the imported hierarchy of a v2 model (batch: -executeMethod ...Dump).</summary>
        public static void Dump()
        {
            foreach (string model in new[] { "RiggedHuman", "Ambulance" })
            {
                UnityEngine.GameObject prefab = AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/GoldenHour/Blender/Exports/" + model + ".fbx");
                foreach (UnityEngine.Transform t in prefab.GetComponentsInChildren<UnityEngine.Transform>(true))
                {
                    UnityEngine.Debug.Log("DUMP " + model + " " + t.name + " local " + t.localPosition.ToString("0.00") + " rot " + t.localEulerAngles.ToString("0") +
                                          " scale " + t.localScale.ToString("0.00") + " world " + t.position.ToString("0.00"));
                }
            }
        }

        /// <summary>Moves the root's axis-conversion rotation into its children so the root is identity.</summary>
        private void OnPostprocessModel(UnityEngine.GameObject root)
        {
            if (!assetPath.Replace('\\', '/').Contains("GoldenHour/Blender/Exports/")) return;
            if (System.Array.IndexOf(Models, Path.GetFileNameWithoutExtension(assetPath)) < 0) return;
            UnityEngine.Quaternion rotation = root.transform.localRotation;
            if (UnityEngine.Quaternion.Angle(rotation, UnityEngine.Quaternion.identity) < 0.01f) return;
            foreach (UnityEngine.Transform child in root.transform)
            {
                child.localPosition = rotation * child.localPosition;
                child.localRotation = rotation * child.localRotation;
            }

            root.transform.localRotation = UnityEngine.Quaternion.identity;
        }

        private void OnPreprocessModel()
        {
            if (!assetPath.Replace('\\', '/').Contains("GoldenHour/Blender/Exports/")) return;
            if (Path.GetFileNameWithoutExtension(assetPath) == "PlayerCharacter")
            {
                // Skinned, animated main character: axis conversion baked, legacy clips driven by PlayerAvatar.
                ModelImporter character = (ModelImporter)assetImporter;
                character.bakeAxisConversion = true;
                character.globalScale = 1f;
                character.animationType = ModelImporterAnimationType.Legacy;
                character.importAnimation = true;
                character.importCameras = false;
                character.importLights = false;
                return;
            }
            if (System.Array.IndexOf(Models, Path.GetFileNameWithoutExtension(assetPath)) < 0) return;
            ModelImporter importer = (ModelImporter)assetImporter;
            importer.bakeAxisConversion = true;
            importer.globalScale = 1f;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
        }
    }
}
