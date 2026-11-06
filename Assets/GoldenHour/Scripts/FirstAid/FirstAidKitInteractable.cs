using System.Collections;
using UnityEngine;

namespace GoldenHour
{
    /// <summary>First-aid kit with a hinged lid that animates open on first use.</summary>
    public sealed class FirstAidKitInteractable : MonoBehaviour
    {
        /// <summary>How far the lid swings open, in degrees.</summary>
        public float openAngle = 105f;

        private Transform lid;
        private bool isOpen;
        private Coroutine animationRoutine;

        public bool IsOpen => isOpen;
        public Transform Lid => lid;

        public void Initialize(Transform lidPivot)
        {
            lid = lidPivot;
        }

        public void Open()
        {
            if (isOpen) return;
            isOpen = true;
            if (lid == null) return;
            if (animationRoutine != null) StopCoroutine(animationRoutine);
            animationRoutine = StartCoroutine(AnimateLid());
        }

        private IEnumerator AnimateLid()
        {
            // The hinge axis is derived from where the lid geometry actually sits relative to the
            // pivot, so the free edge always swings upward regardless of model axis conventions.
            Vector3 toLid = LidCentre() - lid.position;
            toLid.y = 0f;
            Vector3 axis = toLid.sqrMagnitude > 0.0001f
                ? Vector3.Cross(toLid.normalized, Vector3.up)
                : lid.right;
            Quaternion start = lid.rotation;
            Quaternion end = Quaternion.AngleAxis(openAngle, axis) * start;
            const float duration = 0.6f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                lid.rotation = Quaternion.Slerp(start, end, Mathf.SmoothStep(0f, 1f, elapsed / duration));
                yield return null;
            }

            lid.rotation = end;
        }

        private Vector3 LidCentre()
        {
            Renderer[] renderers = lid.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return lid.position;
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer item in renderers) bounds.Encapsulate(item.bounds);
            return bounds.center;
        }
    }
}
