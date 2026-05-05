using System.Collections.Generic;

namespace Hollowfen
{
    // Placeholder LUT — looks up player-facing strings by ID. Real translation
    // (English + Simplified Chinese for EA) replaces this dictionary later.
    public static class Localization
    {
        private static readonly Dictionary<string, string> _table = new Dictionary<string, string>
        {
            // Main menu
            { "ui.menu.quit_title",   "Quit Hollowfen?" },
            { "ui.menu.quit_message", "Any unsaved progress will be lost." },

            // Pause menu
            { "ui.pause.quit_title",   "Quit to Main Menu?" },
            { "ui.pause.quit_message", "Any unsaved progress will be lost. Save before quitting if you want to keep where you are." },

            // Foraging prompts
            { "prompt.forage.verb",    "Forage" },
            { "prompt.inspect.verb",   "Inspect" },

            // Inspect screen
            { "inspect.unknown.eyebrow", "UNKNOWN" },
            { "inspect.unknown.title",   "?" },
            { "inspect.unknown.body",    "A mushroom you haven't catalogued. Forage one to learn more." },
            { "inspect.btn.forage",      "Forage" },
            { "inspect.btn.leave",       "Leave" },

            // Inventory screen
            { "inventory.empty",     "Your pouch is empty. Forage some mushrooms in the woods." },
            { "inventory.btn.close", "Close" },
        };

        public static string Get(string stringId)
        {
            if (string.IsNullOrEmpty(stringId)) return stringId;
            return _table.TryGetValue(stringId, out var s) ? s : stringId;
        }
    }
}
