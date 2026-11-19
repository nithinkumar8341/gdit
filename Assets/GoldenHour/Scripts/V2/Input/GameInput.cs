using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GoldenHour
{
    /// <summary>
    /// All gameplay input as Input System actions (keyboard/mouse and gamepad), with interactive
    /// rebinding saved to PlayerPrefs. Built in code so no asset is needed.
    /// </summary>
    public static class GameInput
    {
        private const string OverridesKey = "GoldenHour.Bindings";
        private static InputActionAsset asset;

        public static InputAction Move { get; private set; }
        public static InputAction LookMouse { get; private set; }
        public static InputAction LookStick { get; private set; }
        public static InputAction Interact { get; private set; }
        public static InputAction Pause { get; private set; }
        public static InputAction Tablet { get; private set; }
        public static InputAction Radio { get; private set; }
        public static InputAction Crouch { get; private set; }
        public static InputAction Sprint { get; private set; }
        public static InputAction Shout { get; private set; }
        public static InputAction Gloves { get; private set; }
        public static InputAction PhotoMode { get; private set; }
        public static InputAction ToggleView { get; private set; }
        public static InputAction Primary { get; private set; }
        public static InputAction Rotate { get; private set; }
        public static InputAction Rise { get; private set; }

        /// <summary>Actions the player may rebind (first keyboard binding of each).</summary>
        public static IReadOnlyList<InputAction> Rebindable
        {
            get
            {
                Ensure();
                return new[] { Interact, Shout, Radio, Tablet, Gloves, Crouch, Sprint, Primary, PhotoMode };
            }
        }

        public static void Ensure()
        {
            if (asset != null) return;
            asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "Golden Hour Input";
            InputActionMap map = asset.AddActionMap("Gameplay");

            Move = map.AddAction("Move", InputActionType.Value);
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            Move.AddBinding("<Gamepad>/leftStick");

            LookMouse = map.AddAction("Look Mouse", InputActionType.Value, "<Mouse>/delta");
            LookStick = map.AddAction("Look Stick", InputActionType.Value, "<Gamepad>/rightStick");

            Interact = Button(map, "Interact", "<Keyboard>/e", "<Gamepad>/buttonSouth");
            Pause = Button(map, "Pause", "<Keyboard>/escape", "<Gamepad>/start");
            Tablet = Button(map, "Casualty Tablet", "<Keyboard>/tab", "<Gamepad>/select");
            Radio = Button(map, "Radio / Dispatch", "<Keyboard>/r", "<Gamepad>/dpad/up");
            Crouch = Button(map, "Kneel", "<Keyboard>/c", "<Gamepad>/buttonEast");
            Sprint = Button(map, "Walk Faster", "<Keyboard>/leftShift", "<Gamepad>/leftStickPress");
            Shout = Button(map, "Shout to Walking Wounded", "<Keyboard>/q", "<Gamepad>/dpad/left");
            Gloves = Button(map, "Put On Gloves", "<Keyboard>/g", "<Gamepad>/dpad/right");
            PhotoMode = Button(map, "Photo Mode", "<Keyboard>/f10", "<Gamepad>/rightStickPress");
            ToggleView = Button(map, "Toggle 1st / 3rd Person", "<Keyboard>/v", "<Gamepad>/leftShoulder");
            Primary = Button(map, "Hold / Compress", "<Keyboard>/space", "<Gamepad>/rightTrigger");
            Primary.AddBinding("<Mouse>/leftButton");

            Rotate = map.AddAction("Rotate", InputActionType.Value);
            Rotate.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
            Rotate.AddBinding("<Gamepad>/leftStick/x");
            Rise = map.AddAction("Rise", InputActionType.Value);
            Rise.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/q").With("Positive", "<Keyboard>/e");

            string overrides = PlayerPrefs.GetString(OverridesKey, string.Empty);
            if (!string.IsNullOrEmpty(overrides))
            {
                try
                {
                    asset.LoadBindingOverridesFromJson(overrides);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("Ignoring invalid saved key bindings: " + exception.Message);
                }
            }

            asset.Enable();
        }

        private static InputAction Button(InputActionMap map, string name, string keyboard, string gamepad)
        {
            InputAction action = map.AddAction(name, InputActionType.Button);
            action.AddBinding(keyboard);
            action.AddBinding(gamepad);
            return action;
        }

        public static bool Pressed(InputAction action)
        {
            Ensure();
            return action != null && action.WasPressedThisFrame();
        }

        public static bool Held(InputAction action)
        {
            Ensure();
            return action != null && action.IsPressed();
        }

        /// <summary>Display name of the first (keyboard/mouse) binding, e.g. "E".</summary>
        public static string KeyName(InputAction action)
        {
            Ensure();
            if (action == null || action.bindings.Count == 0) return "?";
            int index = KeyboardBindingIndex(action);
            return action.GetBindingDisplayString(index);
        }

        public static int KeyboardBindingIndex(InputAction action)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                string path = action.bindings[i].effectivePath ?? string.Empty;
                if (!action.bindings[i].isComposite && (path.StartsWith("<Keyboard>") || path.StartsWith("<Mouse>"))) return i;
            }

            return 0;
        }

        /// <summary>Starts an interactive rebind of the action's keyboard binding. Esc cancels.</summary>
        public static void Rebind(InputAction action, Action<bool> finished)
        {
            Ensure();
            int index = KeyboardBindingIndex(action);
            action.Disable();
            InputActionRebindingExtensions.RebindingOperation operation = null;
            operation = action.PerformInteractiveRebinding(index)
                .WithControlsHavingToMatchPath("<Keyboard>")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnComplete(op =>
                {
                    op.Dispose();
                    action.Enable();
                    SaveOverrides();
                    finished?.Invoke(true);
                })
                .OnCancel(op =>
                {
                    op.Dispose();
                    action.Enable();
                    finished?.Invoke(false);
                });
            operation.Start();
        }

        /// <summary>Applies a keyboard override directly (used by tests and by "reset").</summary>
        public static void SetKeyboardBinding(InputAction action, string path)
        {
            Ensure();
            action.ApplyBindingOverride(KeyboardBindingIndex(action), path);
            SaveOverrides();
        }

        public static void ResetBindings()
        {
            Ensure();
            asset.RemoveAllBindingOverrides();
            SaveOverrides();
        }

        private static void SaveOverrides()
        {
            PlayerPrefs.SetString(OverridesKey, asset.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }
    }
}
