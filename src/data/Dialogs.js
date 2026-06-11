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
      { speaker: 'Bram', text: 'Wren?' },
      { speaker: 'Wren', text: 'Evening, Bram.' },
      { speaker: 'Bram', text: "Lord help me. You've got your father's height." },
      { speaker: 'Wren', text: 'I came as soon as I could.', shot: 'closeup' },
      { speaker: 'Bram', text: "Aye. I know.\n\nI've got the key inside." }
    ],
    triggers: [
      { atLine: 0, action: 'talking', target: 'bram' }
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
      { speaker: 'Bram', text: "Marra's in back. She'll want to see you. Only she won't say so in a way you'll enjoy." },
      { speaker: 'Wren', text: 'That sounds like Marra.' },
      // Bram retrieves the key from below the bar — handoff plays here.
      // He holds it in both palms before placing it on the bar between them.
      { speaker: 'Bram', text: 'Your da left this with me when he took poorly.' },
      { speaker: 'Wren', text: 'He knew?' },
      { speaker: 'Bram', text: "He knew enough.\n\nYou'll find the mill in a state. I went by when I could.\n\nWheel hasn't turned since the Wend went and changed its mind about where it was going." },
      { speaker: 'Wren', text: 'Three winters ago?' },
      { speaker: 'Bram', text: 'Aye.' },
      { speaker: 'Wren', text: 'Father wrote that the lower fields flooded.' },
      { speaker: 'Bram', text: "He would have written it tidy.\n\nHe was a good man, your da." },
      { speaker: 'Wren', text: 'That sounds like him.' },
      { speaker: 'Bram', text: "I'm sorry, Wren.", shot: 'closeup' },
      { speaker: 'Wren', text: 'So am I.', shot: 'closeup' }
    ],
    triggers: [
      { atLine: 0, action: 'talking', target: 'bram' },
      { atLine: 2, action: 'handoff', target: 'bram' }
    ],
    outcome: {
      unlockCard: 'crooked_pintle',
      giveItem: 'item.mill_key',
      completeQuest: 'speakBram',
      nextDialog: 'act1.crooked_pintle.bram.repeat'
    }
  },

  'act1.crooked_pintle.bram.repeat': {
    speaker: 'Bram',
    title: "Old Bram",
    lines: [
      { speaker: 'Bram', text: "Mill lane's east of the well. You'll know the turn, I expect." },
      { speaker: 'Wren', text: 'I know it.' },
      { speaker: 'Bram', text: 'Course you do. Sorry.' }
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
