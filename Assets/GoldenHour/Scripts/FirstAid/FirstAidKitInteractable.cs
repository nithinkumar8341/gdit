using System.Collections;
using UnityEngine;

namespace GoldenHour
{
    public sealed class FirstAidKitInteractable : MonoBehaviour
    {
        private Transform lid;
        private bool isOpen;
        private Coroutine animationRoutine;

        public void Initialize(Transform lidTransform)
        {
            lid = lidTransform;
        }

        public void Open()
        {
            if (isOpen || lid == null)
            {
                isOpen = true;
                return;
            }

            isOpen = true;
            if (animationRoutine != null)
            {
                StopCoroutine(animationRoutine);
            }

            animationRoutine = StartCoroutine(AnimateLid(100f));
        }

        private IEnumerator AnimateLid(float angle)
        {
            Quaternion start = lid.localRotation;
            Quaternion end = start * Quaternion.Euler(-angle, 0f, 0f);
            const float duration = 0.45f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                lid.localRotation = Quaternion.Slerp(start, end, elapsed / duration);
                yield return null;
            }

            lid.localRotation = end;
        }
    }
}
