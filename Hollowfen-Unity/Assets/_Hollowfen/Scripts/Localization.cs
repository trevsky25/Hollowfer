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

            // NPC interaction prompts
            { "prompt.npc.talk",       "Talk to" },

            // NPC display names
            { "npc.bram.name",         "Bram" },
            { "npc.marra.name",        "Marra" },
            { "npc.edda.name",         "Edda" },
            { "npc.almy.name",         "Sister Almy" },
            { "npc.joren.name",        "Joren" },
            { "npc.voss.name",         "Voss" },
            { "npc.theo.name",         "Theo" },
            { "npc.calden.name",       "Father Calden" },
            { "npc.hollin.name",       "Hollin" },
            { "npc.aldric.name",       "Lord Aldric" },

            // Quest objective + display strings (Act I)
            { "quest.arrive.name",       "Homecoming" },
            { "quest.arrive.objective",  "Walk the road into Hollowfen." },
            { "quest.speakBram.name",       "The Crooked Pintle" },
            { "quest.speakBram.objective",  "Speak with Bram outside the inn." },

            // Inspect screen
            { "inspect.unknown.eyebrow", "UNKNOWN" },
            { "inspect.unknown.title",   "?" },
            { "inspect.unknown.body",    "A mushroom you haven't catalogued. Forage one to learn more." },
            { "inspect.btn.forage",      "Forage" },
            { "inspect.btn.leave",       "Leave" },

            // Inventory screen
            { "inventory.empty",     "Your pouch is empty. Forage some mushrooms in the woods." },
            { "inventory.btn.close", "Close" },

            // Map locations (POIs)
            { "loc.fathers_mill.name",   "Father's Mill" },
            { "loc.village_well.name",   "Village Well" },
            { "loc.crooked_pintle.name", "The Crooked Pintle" },
            { "loc.marra_kitchen.name",  "Marra's Kitchen" },
            { "loc.almy_doorway.name",   "Almy's Doorway" },
            { "loc.jorens_forge.name",   "Joren's Forge" },
            { "loc.chapel.name",         "Chapel" },
            { "loc.old_wood_edge.name",  "Old Wood Edge" },

            // Map location short descriptions (side panel copy)
            { "loc.fathers_mill.desc",   "The old water mill where Wren grew up. The wheel still turns; the stones lie idle." },
            { "loc.village_well.desc",   "The heart of the village. Buckets at dawn, gossip at dusk." },
            { "loc.crooked_pintle.desc", "Hollowfen's only tavern. Cheap ale, warmer fires." },
            { "loc.marra_kitchen.desc",  "Marra cooks for everyone. The smell of broth never quite leaves the air." },
            { "loc.almy_doorway.desc",   "Sister Almy's threshold. The seedbook lives somewhere within." },
            { "loc.jorens_forge.desc",   "Joren's hammer rings before dawn. He shapes more than iron." },
            { "loc.chapel.desc",         "Father Calden tends the chapel. Quiet, mostly empty, always open." },
            { "loc.old_wood_edge.desc",  "Where the village footpath ends and the Old Wood begins." },

            // Map side panel labels
            { "map.eyebrow.landmark",     "LANDMARK" },
            { "map.eyebrow.waypost",      "WAYPOST" },
            { "map.eyebrow.unknown",      "UNDISCOVERED" },
            { "map.unknown.title",        "An unfamiliar place" },
            { "map.unknown.body",         "You've heard of this place, but haven't been there. Visit to learn more." },
            { "map.placeholder.title",    "Hollowfen" },
            { "map.placeholder.body",     "Select a landmark to read its entry. Use the arrow keys, D-pad, or click a pin." },
            { "map.btn.waypoint",         "Set Waypoint" },
            { "map.btn.waypoint_clear",   "Clear Waypoint" },
            { "map.btn.waypoint_disabled","Visit first to set waypoint" },
            { "map.label.distance",       "DISTANCE" },
            { "map.label.region",         "REGION" },
        };

        public static string Get(string stringId)
        {
            if (string.IsNullOrEmpty(stringId)) return stringId;
            return _table.TryGetValue(stringId, out var s) ? s : stringId;
        }
    }
}
