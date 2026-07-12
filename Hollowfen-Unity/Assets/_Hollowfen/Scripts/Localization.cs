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
            { "npc.pell.name",         "Elder Pell" },
            { "npc.aldric.name",       "Lord Aldric" },

            // Quest objective + display strings (Act I)
            { "quest.arrive.name",       "Homecoming" },
            { "quest.arrive.objective",  "Walk the road into Hollowfen." },
            { "quest.speakBram.name",       "The Crooked Pintle" },
            { "quest.speakBram.objective",  "Speak with Bram outside the inn." },
            { "quest.searchMill.name",      "Your Father's Mill" },
            { "quest.searchMill.objective", "Enter the mill and search the house." },
            { "quest.findJournal.name",      "The Hidden Journal" },
            { "quest.findJournal.objective", "Find Tobin's mushroom journal." },
            { "quest.firstForage.name",      "The First Forage" },
            { "quest.firstForage.objective", "Gather the three safe basics and the gold-stemmed find at the Old Wood edge." },
            { "quest.firstSale.name",      "Marra's Kitchen" },
            { "quest.firstSale.objective", "Bring your first basket to The Crooked Pintle and see if Hollowfen still has an appetite." },
            { "quest.meetAlmy.name",      "A Knock at the Door" },
            { "quest.meetAlmy.objective", "Someone is waiting at the mill door." },

            // Quest objective + display strings (Act II)
            { "quest.almyTeach.name",       "The Vine-Tender's Lessons" },
            { "quest.almyTeach.objective",  "Learn cultivation from Sister Almy, then plant Wood Ear spawn in a mill-yard grow bed." },
            { "quest.forgeKnife.name",      "Joren's Forge" },
            { "quest.forgeKnife.objective", "Commission a proper foraging knife from Joren in the walled town — and come back for it tomorrow." },
            { "quest.firstTax.name",        "Twelve Silver by Yule" },
            { "quest.firstTax.objective",   "Earn twelve silver and pay Master Voss at the market square before sundown." },
            { "quest.theoTrade.name",       "The Trader's Ledger" },
            { "quest.theoTrade.objective",  "Theo's wagon comes with the dawn. Bring him a basket worth a ledger line." },
            { "quest.edsGrandfather.name",      "Brightspore at the Bedside" },
            { "quest.edsGrandfather.objective", "Edda waits by the mill. Find Brightspore at the old birch, have Marra make the tonic, and carry it to her grandfather." },
            { "quest.meetHollin.name",       "A Stranger at the Inn" },
            { "quest.meetHollin.objective",  "A traveler at The Crooked Pintle asked for the goldfoot girl by name. Sit with her." },
            { "quest.cottagesReopen.name",       "Two Boards Come Down" },
            { "quest.cottagesReopen.objective",  "Elder Pell keeps the ledger at the village well. Fund the shutters and see two cold cottages open their windows." },
            { "quest.caldenWarning.name",       "Father Calden's Doubt" },
            { "quest.caldenWarning.objective",  "Father Calden has come to the mill and refused tea. Hear him out, then see what he has done to the chapel garden gate." },

            // Quest objective + display strings (Act III)
            { "quest.hollinReveals.name",       "Hollin's Inheritance" },
            { "quest.hollinReveals.objective",  "Hollin waits at the mill with a page folded into oilcloth. Hear the name her grandmother left her." },
            { "quest.findWitchCottage.name",       "The Witch's Cottage" },
            { "quest.findWitchCottage.objective",  "Follow the old path into the Deep Wood and find Sable's cottage. Read what she left on the table." },
            { "quest.wendlightFound.name",       "The Wend's True Course" },
            { "quest.wendlightFound.objective",  "Wendlight grows where water was betrayed. Walk the dry riverbed, follow the pale shine, and find what waits at the bend." },
            { "quest.caldenReconcile.name",      "The Chapel Garden Opens" },
            { "quest.caldenReconcile.objective", "Bring the Wendlight truth to Father Calden. Give him a day with the old registers, then meet him at the chapel garden gate." },
            { "quest.eddaApprentice.name",       "Edda Asks" },
            { "quest.eddaApprentice.objective",  "Edda is waiting at the mill door with a speech she has practiced. Hear her out." },
            { "quest.theoCapitalOffer.name",     "Theo's Capital" },
            { "quest.theoCapitalOffer.objective","Theo has an offer to make. Find him at his wagon, hear him out, ask what you like, and leave when you are ready." },
            { "quest.festivalHosted.name",       "The First Festival in Three Years" },
            { "quest.festivalHosted.objective",  "The village is ready to celebrate. Find Marra to set the festival in motion." },
            { "quest.aldricLetter.name",         "A Sealed Letter" },
            { "quest.aldricLetter.objective",    "Voss is waiting with something he was told to deliver in person. Hear him out." },
            { "quest.aldricOfferRead.name",      "The Lord's Offer" },
            { "quest.aldricOfferRead.objective", "Lord Aldric's letter is still sealed on the mill table. Read it by candlelight." },
            { "quest.wendSource.name",           "The Source of the Wend" },
            { "quest.wendSource.objective",      "The Wendlight traced a line upstream. Follow it past the Old Wend to whatever broke the river." },
            { "quest.meetAldric.name",           "The Meeting" },
            { "quest.meetAldric.objective",      "Take the Aldermark and the truth east, to Lord Aldric's manor. Hear his offer, and answer it." },
            { "item.aldric_letter.name",         "Lord Aldric's Letter" },

            // Mushroom tier display names (locked draft 2026-07-11 — QUESTIONS.md Q1; not yet rendered anywhere)
            { "tier.t1.name", "Basket Common" },
            { "tier.t2.name", "Knifework" },
            { "tier.t3.name", "Yard-Grown" },
            { "tier.t4.name", "Deepwood" },

            // Settings screen (batch-28 rebuild — every label localized)
            { "settings.eyebrow",              "Options" },
            { "settings.title",                "Settings" },
            { "settings.tab.audio",            "Audio" },
            { "settings.tab.graphics",         "Graphics" },
            { "settings.tab.controls",         "Controls" },
            { "settings.tab.credits",          "Credits" },
            { "settings.audio.master",         "Master" },
            { "settings.audio.music",          "Music" },
            { "settings.audio.sfx",            "SFX" },
            { "settings.graphics.fullscreen",  "Fullscreen" },
            { "settings.graphics.resolution",  "Resolution" },
            { "settings.graphics.quality",     "Quality" },
            { "settings.value.on",             "On" },
            { "settings.value.off",            "Off" },
            { "settings.controls.sensitivity", "Look Sensitivity" },
            { "settings.controls.action",      "Action" },
            { "settings.controls.gamepad",     "Gamepad" },
            { "settings.controls.keyboard",    "Keyboard" },
            { "settings.controls.section.ui",       "UI" },
            { "settings.controls.section.player",   "Player" },
            { "settings.controls.section.dialogue", "Dialogue" },
            { "settings.hint",                 "Q · E  or  LB · RB — switch tabs        Esc / B — back" },
            { "settings.quality.pc",           "PC" },   // display name for the project's sole quality level (row hidden until a 2nd exists)

            // Binding reference table (copy preserved from the shipped controls tab)
            { "settings.bind.navigate",     "Navigate" },
            { "settings.bind.navigate.pad", "D-Pad / Left Stick" },
            { "settings.bind.navigate.kb",  "W A S D / Arrows" },
            { "settings.bind.submit",       "Submit" },
            { "settings.bind.submit.pad",   "A / Cross" },
            { "settings.bind.submit.kb",    "Enter / Space" },
            { "settings.bind.cancel",       "Cancel" },
            { "settings.bind.cancel.pad",   "B / Circle" },
            { "settings.bind.cancel.kb",    "Esc" },
            { "settings.bind.tabLeft",      "Tab Left" },
            { "settings.bind.tabLeft.pad",  "LB / L1" },
            { "settings.bind.tabLeft.kb",   "Q" },
            { "settings.bind.tabRight",     "Tab Right" },
            { "settings.bind.tabRight.pad", "RB / R1" },
            { "settings.bind.tabRight.kb",  "E" },
            { "settings.bind.delete",       "Delete" },
            { "settings.bind.delete.pad",   "X / Square" },
            { "settings.bind.delete.kb",    "Delete" },
            { "settings.bind.move",         "Move" },
            { "settings.bind.move.pad",     "Left Stick" },
            { "settings.bind.move.kb",      "W A S D" },
            { "settings.bind.look",         "Look" },
            { "settings.bind.look.pad",     "Right Stick" },
            { "settings.bind.look.kb",      "Mouse" },
            { "settings.bind.interact",     "Interact" },
            { "settings.bind.interact.pad", "X / Square" },
            { "settings.bind.interact.kb",  "E" },
            { "settings.bind.jump",         "Jump" },
            { "settings.bind.jump.pad",     "A / Cross" },
            { "settings.bind.jump.kb",      "Space" },
            { "settings.bind.journal",      "Journal" },
            { "settings.bind.journal.pad",  "Y / Triangle" },
            { "settings.bind.journal.kb",   "J" },
            { "settings.bind.pause",        "Pause" },
            { "settings.bind.pause.pad",    "Start / Options" },
            { "settings.bind.pause.kb",     "Esc" },
            { "settings.bind.advance",      "Advance" },
            { "settings.bind.advance.pad",  "A / Cross" },
            { "settings.bind.advance.kb",   "Space / Enter" },
            { "settings.bind.skip",         "Skip" },
            { "settings.bind.skip.pad",     "B / Circle" },
            { "settings.bind.skip.kb",      "Esc" },
            { "settings.bind.choices",      "Choices 1-4" },
            { "settings.bind.choices.pad",  "D-Pad up/right/down/left" },
            { "settings.bind.choices.kb",   "1 / 2 / 3 / 4" },

            // Credits (shipped copy verbatim — final credits copy is Trevor's open backlog item)
            { "credits.heading",   "HOLLOWFEN — THE FAILING VILLAGE" },
            { "credits.sub",       "An exploration & foraging story by Trevor Kist." },
            { "credits.build",     "Built with Unity 6 · ScriptableObject-driven content · Steam Deck-ready" },
            { "credits.copyright", "Story, dialogue, characters © Trevor Kist" },
            { "credits.photos",    "Mushroom photographs courtesy Wikimedia Commons under CC licenses." },
            { "credits.wren",      "Wren character art generated for production use." },
            { "credits.engine",    "Engine built on Unity Starter Assets, Magic Pig Games, NatureManufacture." },
            { "credits.thanks",    "Thanks for walking back into Hollowfen with Wren." },

            // Cultivation
            { "prompt.plant.verb", "Plant" },
            { "growbed.name",      "Grow Bed" },

            // Interaction verbs (doors, props)
            { "prompt.door.unlock",  "Unlock" },
            { "prompt.examine.verb", "Examine" },
            { "prompt.journal.read", "Read" },
            { "prompt.deliver.verb", "Deliver tonic to" },

            // Inspect screen
            { "inspect.unknown.eyebrow", "UNKNOWN" },
            { "inspect.unknown.title",   "?" },
            { "inspect.unknown.body",    "A mushroom you haven't catalogued. Forage one to learn more." },
            { "inspect.btn.forage",      "Forage" },
            { "inspect.btn.leave",       "Leave" },

            // Inventory screen
            { "inventory.empty",     "Your pouch is empty. Forage some mushrooms in the woods." },
            { "inventory.btn.close", "Close" },
            { "inventory.keepsakes", "KEEPSAKES" },

            // Key items
            { "toast.received",             "Received" },
            { "item.mill_key.name",         "Mill Key" },
            { "item.fathers_journal.name",  "Father's Journal" },
            { "item.sealed_letter.name",    "Sealed Letter" },
            { "item.forage_knife.name",     "Horn-Handled Foraging Knife" },
            { "item.brightspore_tonic.name", "Brightspore Tonic" },
            { "item.seedbook.name",          "Sable's Seedbook" },

            // Map locations (POIs)
            { "loc.fathers_mill.name",   "Father's Mill" },
            { "loc.village_well.name",   "Village Well" },
            { "loc.crooked_pintle.name", "The Crooked Pintle" },
            { "loc.marra_kitchen.name",  "Marra's Kitchen" },
            { "loc.almy_doorway.name",   "Almy's Doorway" },
            { "loc.jorens_forge.name",   "Joren's Forge" },
            { "loc.chapel.name",         "Chapel" },
            { "loc.old_wood_edge.name",  "Old Wood Edge" },
            { "loc.theos_wagon.name",    "Theo's Wagon" },
            { "loc.eddas_cottage.name",  "Edda's Cottage" },
            { "loc.witchs_cottage.name", "The Witch's Cottage" },
            { "loc.old_wend.name",       "The Old Wend" },
            { "loc.clear_cut.name",      "The Clear-Cut" },
            { "loc.manor.name",          "Aldric's Manor" },

            // Map location short descriptions (side panel copy)
            { "loc.fathers_mill.desc",   "The old water mill where Wren grew up. The wheel still turns; the stones lie idle." },
            { "loc.clear_cut.desc",      "Upstream of the Old Wend: stumps, drag-trails, a cold camp, and the throttled spring. This is what broke the river." },
            { "loc.village_well.desc",   "The heart of the village. Buckets at dawn, gossip at dusk." },
            { "loc.crooked_pintle.desc", "Hollowfen's only tavern. Cheap ale, warmer fires." },
            { "loc.marra_kitchen.desc",  "Marra cooks for everyone. The smell of broth never quite leaves the air." },
            { "loc.almy_doorway.desc",   "Sister Almy's threshold. The seedbook lives somewhere within." },
            { "loc.jorens_forge.desc",   "Joren's hammer rings before dawn. He shapes more than iron." },
            { "loc.chapel.desc",         "Father Calden tends the chapel. Quiet, mostly empty, always open." },
            { "loc.old_wood_edge.desc",  "Where the village footpath ends and the Old Wood begins." },
            { "loc.theos_wagon.desc",    "Theo's board and ledger. He pays more than Bram could, less than the Capital would, and says both plainly." },
            { "loc.eddas_cottage.desc",  "A low cottage near the mill. Edda's grandfather keeps to his bed by the window." },
            { "loc.witchs_cottage.desc", "One room where the Deep Wood gathers around a spring. Sable's door opens for those who enter kindly." },
            { "loc.old_wend.desc",       "The river's abandoned course — rounded stones, grass fingers, and a thin watery shine at dawn." },
            { "loc.manor.desc",          "Lord Aldric's estate, east of the village: oak panelling, poured tea, and ledgers where other people keep guilt." },

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
