using UnityEngine;
using UnityEngine.InputSystem;

namespace Hollowfen.UI
{
    // Brand-aware controller button glyphs (batch-48, closes Q11). PlayStation pads get the
    // real shape icons via the ControllerGlyphs TMP sprite sheet (<sprite name=...> resolves
    // through TMP's default sprite asset); Xbox/Switch get their letter legends as text.
    // One resolver so the prompt HUD, inspect, and inventory never drift apart.
    public static class ControllerGlyphs
    {
        public enum Face { South, East, West, North }

        private static Gamepad _preferredGamepad;
        private static Gamepad _heldGamepad;
        private static bool _deviceSelectionHeld;

        /// <summary>
        /// Controller prompts are authoritative whenever an enabled gamepad is connected. Keyboard
        /// prompts are the fallback only when no gamepad is available.
        /// </summary>
        public static bool IsGamepadActive
        {
            get { return ResolveConnectedGamepad() != null; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetTracking()
        {
            _preferredGamepad = null;
            _heldGamepad = null;
            _deviceSelectionHeld = false;
        }

        /// <summary>
        /// Records the gamepad behind a semantic action so mixed-controller setups use the correct
        /// brand. Keyboard and mouse do not suppress a connected controller's prompts.
        /// </summary>
        public static void NoteUsedDevice(InputDevice device)
        {
            var gamepad = device as Gamepad;
            if (IsAvailable(gamepad)) _preferredGamepad = gamepad;
        }

        /// <summary>
        /// Pins the control scheme used to submit a cinematic scene transition. Entering gameplay
        /// locks/warps the cursor and can emit synthetic mouse state changes; those must not replace
        /// the controller prompt before the first narration frame is visible.
        /// </summary>
        public static void BeginTransitionDeviceHold(InputDevice device)
        {
            NoteUsedDevice(device);
            _heldGamepad = ResolveConnectedGamepad();
            _deviceSelectionHeld = true;
        }

        public static void EndTransitionDeviceHold()
        {
            if (!_deviceSelectionHeld) return;
            if (IsAvailable(_heldGamepad)) _preferredGamepad = _heldGamepad;
            _heldGamepad = null;
            _deviceSelectionHeld = false;
        }

        // The physical face button for a logical position on the selected connected pad, as display text.
        // Keyboard fallback is the caller's job (they know their own binding).
        public static string For(Face face)
        {
            var pad = ResolveConnectedGamepad();
            if (pad == null) return "";

            string n = pad.GetType().Name;
            string product = pad.description.product != null ? pad.description.product.ToLowerInvariant() : "";

            bool isPS = n.Contains("DualSense") || n.Contains("DualShock") || n.Contains("PS4") || n.Contains("PS5")
                     || product.Contains("dualsense") || product.Contains("dualshock") || product.Contains("playstation")
                     || product.Contains("wireless controller"); // Sony's marketing name on macOS HID
            bool isSwitch = n.Contains("Switch") || n.Contains("Joy")
                         || product.Contains("nintendo") || product.Contains("pro controller");

            if (isPS)
            {
                switch (face)
                {
                    case Face.South: return "<sprite name=\"ps_cross\">";
                    case Face.East:  return "<sprite name=\"ps_circle\">";
                    case Face.West:  return "<sprite name=\"ps_square\">";
                    default:         return "<sprite name=\"ps_triangle\">";
                }
            }
            if (isSwitch)
            {
                switch (face)
                {
                    case Face.South: return "B";
                    case Face.East:  return "A";
                    case Face.West:  return "Y";
                    default:         return "X";
                }
            }
            // Xbox and unknown pads: XInput letters.
            switch (face)
            {
                case Face.South: return "A";
                case Face.East:  return "B";
                case Face.West:  return "X";
                default:         return "Y";
            }
        }

        private static Gamepad ResolveConnectedGamepad()
        {
            if (_deviceSelectionHeld && IsAvailable(_heldGamepad)) return _heldGamepad;
            if (IsAvailable(Gamepad.current)) return Gamepad.current;
            if (IsAvailable(_preferredGamepad)) return _preferredGamepad;

            foreach (Gamepad gamepad in Gamepad.all)
                if (IsAvailable(gamepad)) return gamepad;
            return null;
        }

        private static bool IsAvailable(Gamepad gamepad)
        {
            return gamepad != null && gamepad.added && gamepad.enabled;
        }
    }
}
