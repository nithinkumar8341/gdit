using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GoldenHour
{
    /// <summary>Hover/press feedback for UI buttons: a small scale pop and an optional hover sound.</summary>
    public sealed class ButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        public static Action Hovered;

        private const float HoverScale = 1.05f;
        private const float PressScale = 0.95f;
        private bool hovered;
        private bool pressed;

        public bool IsHovered => hovered;

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            Hovered?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            pressed = false;
        }

        public void OnPointerDown(PointerEventData eventData) => pressed = true;

        public void OnPointerUp(PointerEventData eventData) => pressed = false;

        private void OnDisable()
        {
            hovered = false;
            pressed = false;
            transform.localScale = Vector3.one;
        }

        private void Update()
        {
            float target = pressed ? PressScale : hovered ? HoverScale : 1f;
            float current = transform.localScale.x;
            float next = Mathf.MoveTowards(current, target, Time.unscaledDeltaTime * 1.2f);
            if (!Mathf.Approximately(current, next)) transform.localScale = new Vector3(next, next, 1f);
        }
    }
}
