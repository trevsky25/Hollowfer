// Dialog scripts. Each entry in `lines` is one speaker turn — consecutive
// sentences from the same speaker are merged into a single dialog box so the
// player advances at speaker boundaries, not sentence boundaries. IDs match
// `Dialogue ID:` markers in docs/story.md.
//
// Trigger shape: { atLine: <0-based turn index>, action: 'handoff' | 'idle' | ...,
// target: 'bram' | 'marra' | 'wren' }
//
// Outcome shape: { unlockCard?, giveItem?, completeQuest?, nextDialog? }

export const DIALOGS = {
  'act1.homecoming.bram.recognition': {
    speaker: 'Bram',
    title: "Old Bram",
    lines: [
      { speaker: 'Wren', text: "Old Bram! How I've missed you. It's been so long. I thought of the Pintle every winter in Veyrwick — your fire, Marra's oatcakes, Dad pretending not to notice when I stayed past supper.", shot: 'closeup' },
      { speaker: 'Bram', text: "Wren? Lord help me, it is you. Come here and let an old innkeeper look at you. Three winters, and you've come home with your father's height and your mother's eyes. I only wish the village had kept more of itself to welcome you.", shot: 'closeup' }
    ],
    triggers: [
      { atLine: 0, action: 'talking', target: 'wren' }
    ],
    outcome: {
      unlockCard: 'homecoming',
      nextDialog: 'act1.crooked_pintle.bram.key'
    }
  },

  'act1.crooked_pintle.bram.key': {
    speaker: 'Bram',
    title: "Old Bram",
    lines: [
      { speaker: 'Wren', text: "Dad's letters grew shorter, then stopped. He wrote about the flooded fields, never how bad it was. I should have come sooner, Bram. Tell me what happened — and why you have the mill key.", shot: 'closeup' },
      // Bram holds the key in both palms before placing it in Wren's hand.
      { speaker: 'Bram', text: 'Your father made every hardship sound like weather. The Wend changed course three winters ago and left the wheel over dry stones. When he took poorly, he brought me this key and asked me to keep the damp out until you came home. I did what I could. He was a good man, Wren, and a dear friend. I am sorry.', shot: 'closeup' },
      { speaker: 'Wren', text: "Thank you for keeping faith with him. I'll take the key now — and whatever waits at the mill. I've spent long enough being absent.", shot: 'closeup' }
    ],
    triggers: [
      { atLine: 0, action: 'talking', target: 'wren' },
      { atLine: 1, action: 'handoff', target: 'bram' }
    ],
    outcome: {
      unlockCard: 'crooked_pintle',
      giveItem: 'item.mill_key',
      completeQuest: 'speakBram'
    }
  },

  'act1.crooked_pintle.bram.repeat': {
    speaker: 'Bram',
    title: "Old Bram",
    lines: [
      { speaker: 'Bram', text: "Mill lane runs east from the well. You'll know the turn. Of course you will — old habit, Wren. Forgive me." }
    ],
    triggers: [
      { atLine: 0, action: 'talking', target: 'bram' }
    ]
  }
};

// Per-NPC dialog selector. Returns the next dialog ID for a given NPC based
// on world state. main.js owns world state; pass it in here.
export function pickDialogForBram(state) {
  if (!state.bramTalkedRecognition) return 'act1.homecoming.bram.recognition';
  if (!state.bramKeyGiven) return 'act1.crooked_pintle.bram.key';
  return 'act1.crooked_pintle.bram.repeat';
}
