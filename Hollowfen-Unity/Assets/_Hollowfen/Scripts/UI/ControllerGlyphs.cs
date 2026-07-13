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

        // The physical face button for a logical position on the CURRENT pad, as display text.
        // Keyboard fallback is the caller's job (they know their own binding).
        public static string For(Face face)
        {
            var pad = Gamepad.current;
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
    }
}
