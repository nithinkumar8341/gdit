using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GoldenHour
{
    /// <summary>Keeps a UI button selected while a gamepad is connected so menus can be driven with the D-pad.</summary>
    public sealed class GamepadSelector : MonoBehaviour
    {
        private void Update()
        {
            if (Gamepad.current == null || EventSystem.current == null) return;
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null && selected.activeInHierarchy) return;
            Button[] buttons = GetComponentsInChildren<Button>();
            for (int i = buttons.Length - 1; i >= 0; i--)
            {
                if (buttons[i].interactable && buttons[i].gameObject.activeInHierarchy)
                {
                    EventSystem.current.SetSelectedGameObject(buttons[i].gameObject);
                    return;
                }
            }
        }
    }
}
