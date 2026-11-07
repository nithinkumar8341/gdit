using System.Collections.Generic;
using UnityEngine;

namespace GoldenHour
{
    /// <summary>
    /// Highlights the object under the crosshair with an inverted-hull outline (front-face culled unlit
    /// copies of its meshes, slightly enlarged). One target at a time; copies are cached per target.
    /// </summary>
    public static class InteractionOutline
    {
        private static readonly List<GameObject> shells = new List<GameObject>();
        private static GameObject current;

        public static Material OutlineMaterial { get; set; }
        public static GameObject Current => current;

        public static void Show(GameObject target)
        {
            if (target == current) return;
            Clear();
            current = target;
            if (target == null || OutlineMaterial == null) return;
            foreach (MeshFilter filter in target.GetComponentsInChildren<MeshFilter>())
            {
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || !renderer.enabled || filter.sharedMesh == null) continue;
                if (filter.gameObject.name.StartsWith("Outline")) continue;
                if (filter.GetComponentInParent<TMPro.TMP_Text>() != null) continue;
                GameObject shell = new GameObject("Outline Shell");
                shell.transform.SetParent(filter.transform, false);
                shell.transform.localScale = Vector3.one * 1.06f;
                shell.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                MeshRenderer shellRenderer = shell.AddComponent<MeshRenderer>();
                shellRenderer.sharedMaterial = OutlineMaterial;
                shellRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                shellRenderer.receiveShadows = false;
                shells.Add(shell);
                if (shells.Count > 60) break;
            }
        }

        public static void Clear()
        {
            foreach (GameObject shell in shells)
            {
                if (shell != null) Object.Destroy(shell);
            }

            shells.Clear();
            current = null;
        }
    }
}
