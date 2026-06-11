// Hollowfen — full story spine. 28 cards: four acts plus four endings.
// Voice and beats sourced from STORY_BIBLE.md (Act spine §6, Opening 30
// Minutes §10, NPC voices §11). Each card has:
//   id, act, scene, title, subtitle, image, body (2–4 sentences), wrenNote
//   (Wren's italicized first-person voice, 1–2 lines), beats (3–4 plot bullets).
// `unlockAt` is the storyProgress index that unlocks this card. The UI shows
// locked cards as "Locked Memory" in greyscale.

export const STORY_CARDS = [
  // ============== ACT I — ARRIVAL ==============
  {
    id: 'homecoming',
    act: 'Act I',
    scene: 'Scene 1',
    title: 'Homecoming',
    subtitle: 'Wren walks the path back to Hollowfen at dusk.',
    image: '/story/cards/homecoming.png',
    questId: 'arrive',
    unlockAt: 0,
    body: "It had been three years since she left for the kitchens at Veyrwick. Her father's letters got shorter, then stopped. She comes home at dusk with a pack on her back and a knife at her hip, to a village that almost doesn't recognize her — fewer chimneys smoking than she remembers, more boards across windows than she can count.",
    wrenNote: "I thought coming home would feel like stepping backward. It feels more like opening a door no one has touched in years.",
    beats: [
      "Wren returns to Hollowfen after three winters away.",
      "The village is smaller than she remembers, and quieter.",
      "Old Bram sweeping the inn doorstep is the first face she sees."
    ]
  },
  {
    id: 'crooked_pintle',
    act: 'Act I',
    scene: 'Scene 2',
    title: 'The Crooked Pintle',
    subtitle: 'Old Bram hands Wren the mill key wrapped in a rag.',
    image: '/story/cards/crooked-pintle.png',
    questId: 'speakBram',
    unlockAt: 1,
    body: "The inn is half-dark and half-warm. Bram has the mill key wrapped in a rag like it might bite, and apologises before saying anything important. Tobin's daughter, he calls her — twice — as if saying the name twice could bring her father back to the room.",
    wrenNote: "Bram apologised four times before he handed me the key. He used to laugh more than that. Or maybe I just remember him laughing because he was the loudest in any room.",
    beats: [
      "Bram tells Wren her father was a good, quiet man.",
      "The Wend changed course three winters ago and killed the mill wheel.",
      "Wren takes the key and turns toward the long lane to the mill."
    ]
  },
  {
    id: 'fathers_mill',
    act: 'Act I',
    scene: 'Scene 3',
    title: "Your Father's Mill",
    subtitle: 'The wheel is still. Flour dust lies in the corners like ash.',
    image: '/story/cards/fathers-mill.png',
    questId: 'searchMill',
    unlockAt: 2,
    body: "The mill is the same building and a stranger's house at once. The big wheel hangs over a streambed that no longer carries water. Inside, Tobin's ledgers are folded just so, his coat hangs on the same peg, and the kettle is on the hearth as if someone meant to put it back on the fire and forgot.",
    wrenNote: "Da wrote less every winter. I used to be cross about it. Standing in his kitchen now, I think he was saving words for the wrong pages.",
    beats: [
      "Wren enters her childhood home for the first time in three years.",
      "The mill wheel hasn't turned since the river left.",
      "Wren finds her father's things untouched, waiting."
    ]
  },
  {
    id: 'hidden_journal',
    act: 'Act I',
    scene: 'Scene 4',
    title: 'The Hidden Journal',
    subtitle: 'A worn leather book, half its pages still blank.',
    image: '/story/cards/hidden-journal.png',
    questId: 'findJournal',
    unlockAt: 3,
    body: "It was in the bottom drawer, wrapped in oilcloth under a stack of his old receipts. Tobin's hand changes as the pages go on — careful at the start, hurried at the end. Three mushrooms drawn in soft pencil: Field Cap, Wood Ear, Pinecrest. And a confession in the margins about a family secret no one was supposed to write down.",
    wrenNote: "\"If you're reading this, I never told you.\" Da, you were waiting for me to be old enough. I was old enough. I was just gone.",
    beats: [
      "Wren finds Tobin's hidden foraging journal.",
      "The first three mushrooms are sketched and named.",
      "A note at the back reveals the gift runs in her family."
    ]
  },
  {
    id: 'first_forage',
    act: 'Act I',
    scene: 'Scene 5',
    title: 'The First Forage',
    subtitle: 'Edge Woods, morning. A girl watches from the path.',
    image: '/story/cards/first-forage.png',
    questId: 'firstForage',
    unlockAt: 4,
    body: "The Edge Woods have always been at the village's back door, but most folk never walked into them. Wren finds the Field Caps in a fairy ring, a Wood Ear soft as a folded scrap on a fallen birch, and a Pinecrest under the third pine she touches. A small girl watches her work the whole time, and never says her name.",
    wrenNote: "I felt my father's hand on my shoulder twice this morning. He wasn't there. I don't mind that he wasn't there.",
    beats: [
      "Wren forages her first three mushrooms.",
      "Edda watches in silence from the path's edge.",
      "The Edge Woods feel less haunted than the village said."
    ]
  },
  {
    id: 'marra_kitchen',
    act: 'Act I',
    scene: 'Scene 6',
    title: "Marra's Kitchen",
    subtitle: 'The first goldfoot in the inn kitchen in twenty years.',
    image: '/story/cards/marra-kitchen.png',
    questId: 'firstSale',
    unlockAt: 5,
    body: "Bram looks at the basket and breathes a word that isn't a curse. Marra washes them in cold water, not hot — she will tell Wren twice and not three times — and the kitchen smells like Wren's mother for the first time in a decade. The first coin earned weighs more than its silver.",
    wrenNote: "Marra didn't hug me. She told me how to wash mushrooms properly and asked, almost like an afterthought, if my mother ever taught me to sing while I worked. I said yes.",
    beats: [
      "Bram pays Wren her first three coins.",
      "Marra cooks a stew that travelers stop for.",
      "The inn kitchen feels like a kitchen again, briefly."
    ]
  },
  {
    id: 'almy_doorway',
    act: 'Act I',
    scene: 'Scene 7 — Act break',
    title: 'A Knock at the Door',
    subtitle: 'Sister Almy stands at the mill threshold the next morning.',
    image: '/story/cards/almy-doorway.png',
    questId: 'meetAlmy',
    unlockAt: 6,
    body: "Sister Almy doesn't make small talk. She watched Wren forage yesterday — picked the goldfoots and didn't pick the false ones beside them — and her father couldn't have taught her that, because he didn't know it. There is a chair pulled out at the kitchen table before Wren has time to say good morning.",
    wrenNote: "She said my grandmother's name like a key turning. I don't remember my grandmother. Apparently my hands do.",
    beats: [
      "Sister Almy arrives unannounced at the mill.",
      "She tells Wren her grandmother knew the old foraging lore.",
      "Wren's personal hook becomes a generational one."
    ]
  },

  // ============== ACT II — BUILDING ==============
  {
    id: 'almy_lessons',
    act: 'Act II',
    scene: 'Scene 1',
    title: "The Vine-Tender's Lessons",
    subtitle: 'Almy teaches what was meant for an apprentice she never had.',
    image: '/story/cards/almy-lessons.png',
    questId: 'almyTeach',
    unlockAt: 7,
    body: "They sit in Almy's back garden among raised beds of unfamiliar herbs, and Almy talks about her own mentor — the last hedge-witch of Hollowfen, dead a generation. Three things her grandmother taught her, three things she was forbidden to pass on to anyone but Wren. By the end of the morning, Wren has put her first wood-ear log to soak.",
    wrenNote: "I asked Almy why she was teaching me, all at once, after a generation of silence. She said: because you came back. Because nobody else has, in twenty-six years.",
    beats: [
      "Sister Almy reveals she was the unfinished apprentice of the old hedge-witch.",
      "She teaches Wren the basics of mushroom cultivation.",
      "Wren plants her first crop in the mill's back yard."
    ]
  },
  {
    id: 'jorens_forge',
    act: 'Act II',
    scene: 'Scene 2',
    title: "Joren's Forge",
    subtitle: 'Smithy of three villages, empty commission books.',
    image: '/story/cards/jorens-forge.png',
    questId: 'forgeKnife',
    unlockAt: 8,
    body: "Joren resents Wren before he respects her. A peddler's knife would do, he tells her — half the price, twice the speed. Then he asks what she's foraging that needs a proper edge, and the next morning the foraging knife is on his anvil with a horn handle, and he refuses to let her call it charity.",
    wrenNote: "Joren wrapped my knife in a kerchief and told me not to tell anyone he made it like a gift. Then he let me see him smile, just for a second, before the door shut.",
    beats: [
      "Wren commissions a proper foraging knife from Joren.",
      "He starts asking what comes back in her basket.",
      "The smithy fire is lit two days a week instead of one."
    ]
  },
  {
    id: 'voss_first_visit',
    act: 'Act II',
    scene: 'Scene 3',
    title: 'Twelve Silver by Yule',
    subtitle: 'Master Voss, tax collector, comes through Hollowfen.',
    image: '/story/cards/voss-first-visit.png',
    questId: 'firstTax',
    unlockAt: 9,
    body: "Voss is bureaucratic and bloodless — never raises his voice, never has to. Twelve silver per quarter for Lord Aldric, regardless of yields, regardless of mill or Wend or weather. The Wenmar family sit in the inn looking at their boots, certain they will lose the cottage they were born in. Wren does the maths and pays it in goldfoots and lacewigs.",
    wrenNote: "Voss doesn't enjoy the work. He told me as much, in a sentence. Then he counted the coins.",
    beats: [
      "Voss arrives demanding twelve silver from a village that has none.",
      "Wren's foraging earnings save the Wenmar family's cottage.",
      "The village notices for the first time that Wren's arrival changed an outcome."
    ]
  },
  {
    id: 'theo_trade',
    act: 'Act II',
    scene: 'Scene 4',
    title: "The Trader's Ledger",
    subtitle: "Theo's wagon comes through every five to seven days.",
    image: '/story/cards/theo-trade.png',
    questId: 'theoTrade',
    unlockAt: 10,
    body: "Theo is cheerful, worldly, slightly too smooth. He pays well and he talks too easily, and he keeps a ledger of which mushrooms fetch what price in which town. The first time Wren shows him a goldfoot he buys the lot before she can name a price, and tells her not to sell to peddlers — they'll cheat her, and he won't.",
    wrenNote: "Theo is the first person who has looked at me as if I were a business and not a homecoming. I am not sure how I feel about that yet. I know it is useful.",
    beats: [
      "Theo establishes a regular trade route through Hollowfen.",
      "Wren learns market prices in Veyrwick and the Capital.",
      "Mushroom income overtakes the village's field income for the first time in years."
    ]
  },
  {
    id: 'edda_grandfather',
    act: 'Act II',
    scene: 'Scene 5',
    title: 'Brightspore at the Bedside',
    subtitle: 'A real person, saved.',
    image: '/story/cards/edda-grandfather.png',
    questId: 'edsGrandfather',
    unlockAt: 11,
    body: "Edda's grandfather had not eaten a full bowl in three weeks. The Brightspore tonic — bitter, faintly luminous, found at the foot of one specific birch — sets him up against his pillows by the second evening. He says the soup tastes like when he was small. Edda doesn't know what to do with that, and so she stands at the door with a strangely heavy face.",
    wrenNote: "Edda told me her grandfather ate today, the whole bowl. She said it like she was reporting weather. Then she walked away. I had to put my forehead against the doorframe.",
    beats: [
      "Edda's grandfather recovers thanks to a Brightspore tonic.",
      "The village sees a foraged mushroom save a life.",
      "Edda speaks her first full sentence to Wren."
    ]
  },
  {
    id: 'hollin_arrives',
    act: 'Act II',
    scene: 'Scene 6',
    title: 'A Stranger at the Inn',
    subtitle: 'A traveler asks for the goldfoot girl by name.',
    image: '/story/cards/hollin-arrives.png',
    questId: 'meetHollin',
    unlockAt: 12,
    body: "Hollin doesn't look like a buyer or a peddler or a Lord's man. She is quiet and careful with her words and asked at three villages before someone said Wren's name. She isn't selling and she isn't buying. She would like to sit. She would like to talk about the wood, if Wren has a moment.",
    wrenNote: "Hollin uses fewer words than I do. That has never happened to me before. We sat for half an hour without saying much. It was easier than most conversations I have had this year.",
    beats: [
      "A traveler named Hollin arrives in Hollowfen.",
      "She does not say what she has come for, only that she has come back.",
      "Wren recognises something in her without yet knowing what."
    ]
  },
  {
    id: 'cottages_reopen',
    act: 'Act II',
    scene: 'Scene 7',
    title: 'Two Boards Come Down',
    subtitle: 'Cottages that were boarded for two winters reopen.',
    image: '/story/cards/cottages-reopen.png',
    questId: 'cottagesReopen',
    unlockAt: 13,
    body: "Two of the boarded cottages have new shutters by the autumn equinox. A traveler asks at the inn if there is work in Hollowfen — there hasn't been such a question in years. Elder Pell takes a long look at his ledger and writes one line, in pencil, then traces over it again in ink.",
    wrenNote: "Pell stopped me on the lane and said, very slowly, that he was not yet ready to be hopeful. He didn't mean it the way it sounded.",
    beats: [
      "Two boarded cottages reopen as families return.",
      "A traveler asks about work in Hollowfen for the first time in years.",
      "Pell begins recording the season as a turning point."
    ]
  },
  {
    id: 'caldens_doubt',
    act: 'Act II',
    scene: 'Scene 8 — Act break',
    title: "Father Calden's Doubt",
    subtitle: 'The priest will not yet bless what he does not understand.',
    image: '/story/cards/caldens-doubt.png',
    questId: 'caldenWarning',
    unlockAt: 14,
    body: "Calden visits the mill privately, and is formal even in a kitchen. He baptized Wren. He knew her father. He says, kindly, that the deep wood is forgotten on purpose, and that the old ways did not die without help. He will not yet stand against her. He will not yet stand with her.",
    wrenNote: "Calden doesn't hate me. He is afraid for me, and I think a little afraid of me. I noticed he didn't cross himself when he left.",
    beats: [
      "Father Calden privately warns Wren away from the deeper wood.",
      "He admits he knew her father and her family well.",
      "He locks the chapel garden, denying Wren a prime cultivation site."
    ]
  },

  // ============== ACT III — DISCOVERY ==============
  {
    id: 'hollin_inheritance',
    act: 'Act III',
    scene: 'Scene 1',
    title: "Hollin's Inheritance",
    subtitle: 'The wood remembers, and so does she.',
    image: '/story/cards/hollin-inheritance.png',
    questId: 'hollinReveals',
    unlockAt: 15,
    body: "Hollin tells Wren her own grandmother's name on a damp morning under a leaking roof. The old hedge-witch — Almy's mentor — was Hollin's grandmother, and Hollin came back because her own pages were also half blank. Two granddaughters of two women who knew the same forest, sitting in a kitchen that smells of wet wool and woodsmoke.",
    wrenNote: "Hollin and I have inherited different halves of the same broken thing. I do not yet know if we are meant to put it back together or if the trying will tell us we cannot.",
    beats: [
      "Hollin reveals she is the granddaughter of the last hedge-witch.",
      "She and Wren share the same severed lineage, from opposite directions.",
      "Sister Almy, hearing this, weeps for the first time in years."
    ]
  },
  {
    id: 'witch_cottage',
    act: 'Act III',
    scene: 'Scene 2',
    title: "The Witch's Cottage",
    subtitle: 'Deep in the Old Wood, a roof still holds.',
    image: '/story/cards/witch-cottage.png',
    questId: 'findWitchCottage',
    unlockAt: 16,
    body: "Hollin walks ahead. Almy walks behind. Wren walks in the middle and the path opens like a remembered sentence. The cottage is small, the moss is thick, and the spring at the back of it is still cold and still clean. The seedbook on the table is older than any of them, and someone left it open to a page about Witchwell.",
    wrenNote: "It looked like the kind of place I always imagined when my mother said \"deep in the wood.\" It looked like I had been there before. I don't think I had.",
    beats: [
      "Wren, Hollin, and Sister Almy find the Witch's Cottage together.",
      "The seedbook of the old hedge-witch is recovered.",
      "Tier 4 mushrooms — Moonring, Hollowheart, Wendlight — become identifiable."
    ]
  },
  {
    id: 'wend_truth',
    act: 'Act III',
    scene: 'Scene 3',
    title: "The Wend's True Course",
    subtitle: 'Wendlight grows where the river used to flow.',
    image: '/story/cards/wend-truth.png',
    questId: 'wendlightFound',
    unlockAt: 17,
    body: "The journal in the cottage names a mushroom that fruits only on land where clean water once ran. Wren walks the dry bed of the old Wend at dawn and the Wendlight is already there — pale, brittle, glowing faintly in the half-light. The Wend's course-shift was not weather. The deforestation upstream was the work of Lord Aldric's woodcutters.",
    wrenNote: "For three winters this village has called the river's leaving an act of God. It was an act of accounting. I do not know yet what to do with that.",
    beats: [
      "Wendlight mushrooms reveal the river's old course.",
      "The course-shift was caused by deforestation upstream.",
      "Lord Aldric's name enters the story for the first time as a cause, not a backdrop."
    ]
  },
  {
    id: 'chapel_garden',
    act: 'Act III',
    scene: 'Scene 4',
    title: 'The Chapel Garden Opens',
    subtitle: "Father Calden's confession.",
    image: '/story/cards/chapel-garden.png',
    questId: 'caldenReconcile',
    unlockAt: 18,
    body: "Calden has been reading old church records. He comes to the mill with a brown envelope and a face he doesn't want to show anyone. The church helped suppress the old foraging knowledge centuries ago — not as fairy-story heresy, but as policy. He has unlocked the chapel garden. He will not bless what he does not understand. He will no longer stand in its way.",
    wrenNote: "Calden gave me the chapel garden key and apologised for the wrong his order had done. He didn't look me in the eye. I wish he had. I would have told him the apology was enough.",
    beats: [
      "Father Calden uncovers the church's historical role in suppressing forage lore.",
      "He unlocks the chapel garden as a cultivation site.",
      "He stops short of full reconciliation, and Wren accepts the half-distance."
    ]
  },
  {
    id: 'edda_apprentice',
    act: 'Act III',
    scene: 'Scene 5',
    title: 'Edda Asks',
    subtitle: 'A formal apprenticeship, asked for in a single sentence.',
    image: '/story/cards/edda-apprentice.png',
    questId: 'eddaApprentice',
    unlockAt: 19,
    body: "Edda waits at the mill door at first light, hat in hand, and asks if Wren will take her on as an apprentice. She has thought about it. She has practiced the sentence. She would like to learn properly, please. Her grandfather is well enough now. She would like a future here.",
    wrenNote: "Edda asked me a question she had practiced. I tried to answer like a person who deserved to be asked. I am not sure I did.",
    beats: [
      "Edda asks to be Wren's formal apprentice.",
      "She begins learning identification and the journal's discipline.",
      "Wren gains a junior forager and the village gains a future."
    ]
  },
  {
    id: 'theo_capital_offer',
    act: 'Act III',
    scene: 'Scene 6',
    title: "Theo's Capital",
    subtitle: 'The trader names the temptation aloud.',
    image: '/story/cards/theo-capital-offer.png',
    questId: 'theoCapitalOffer',
    unlockAt: 20,
    body: "Theo is more careful than usual. He waits until the inn is empty. He has a buyer in the Capital who would set Wren up properly — kitchen, name, signage, the works. He won't lie about what it would cost: it would cost Hollowfen Wren. He says the offer with the steady voice of a friend who isn't sure he wants to win.",
    wrenNote: "Theo offered me a life I had been told to want for sixteen years. I sat with it. I did not give him an answer that night.",
    beats: [
      "Theo formally offers Wren a partnership in the Capital.",
      "The offer is not greedy or cruel — Theo means it kindly.",
      "Wren begins to weigh leaving against everything that would not leave with her."
    ]
  },
  {
    id: 'first_festival',
    act: 'Act III',
    scene: 'Scene 7',
    title: 'The First Festival in Three Years',
    subtitle: 'Lanterns, four signature dishes, the village square full.',
    image: '/story/cards/first-festival.png',
    questId: 'festivalHosted',
    unlockAt: 21,
    body: "Marra cooks four signature dishes by sundown. The square fills before the lanterns are even lit. Pell records the year as the year the Hollow turned, in ink, with Wren's name written in his thinnest hand. Bram weeps at his own bar without trying to hide it. Wren is the guest of honor and spends most of the night washing bowls.",
    wrenNote: "I went outside halfway through the festival because there were too many people thanking me at once. Edda found me. She didn't say anything. She just stood with me for a while. That was the right thing.",
    beats: [
      "Hollowfen holds its first proper festival in three years.",
      "Pell formally records the season as a turning point.",
      "The village publicly recognises Wren as the cause."
    ]
  },
  {
    id: 'sealed_letter',
    act: 'Act III',
    scene: 'Scene 8 — Act break',
    title: 'A Sealed Letter',
    subtitle: 'Master Voss arrives unannounced, between visits.',
    image: '/story/cards/sealed-letter.png',
    questId: 'aldricLetter',
    unlockAt: 22,
    body: "Voss is dressed plainly and looks tired. He didn't come for taxes. He has a sealed letter from Lord Aldric himself, addressed to Miss Tobin. The wax is the colour of old wine. He says — almost gently — that whatever the letter contains, he would like Wren to read it before the rumour reaches the inn. Then he leaves without his usual line.",
    wrenNote: "Voss has carried a hundred letters for Lord Aldric. He brought this one to my door personally. I think he is afraid of what it says. I think he is also afraid of becoming irrelevant if I answer it.",
    beats: [
      "Lord Aldric writes to Wren personally.",
      "Voss's posture shifts — he may not be on the Lord's side anymore.",
      "Act III closes on a letter Wren has not yet opened."
    ]
  },

  // ============== ACT IV — THE CHOICE ==============
  {
    id: 'aldric_offer',
    act: 'Act IV',
    scene: 'Scene 1',
    title: "The Lord's Offer",
    subtitle: 'A velvet glove, a written hand, an iron underneath.',
    image: '/story/cards/aldric-offer.png',
    questId: 'aldricOfferRead',
    unlockAt: 23,
    body: "Lord Aldric writes well. He had presumed, speaking honestly, that Hollowfen was finished. He has been informed otherwise. He would like to make Miss Tobin's acquaintance, and he would like Hollowfen's mushroom trade under his banner — generous terms, written in graceful prose, with an exit clause that is graceful only on the page.",
    wrenNote: "I read the letter twice. I read it a third time. The third time I tried to read it as if it were addressed to my grandmother. I think she would have laughed at the second paragraph and refused at the fourth.",
    beats: [
      "Lord Aldric formally proposes a trade monopoly.",
      "The terms are generous — and the village would no longer be its own.",
      "Wren must decide what kind of place Hollowfen becomes."
    ]
  },
  {
    id: 'wend_source',
    act: 'Act IV',
    scene: 'Scene 2',
    title: 'The Source of the Wend',
    subtitle: 'Upstream, where the woodcutters worked.',
    image: '/story/cards/wend-source.png',
    questId: 'wendSource',
    unlockAt: 24,
    body: "A day's walk upstream the wood opens onto a hillside that has been shaved bare. Stumps in rows. Drag-trails. A camp, abandoned mid-season, the cookpots still rusting on the cold fires. Wren stands at the lip of the cleared slope and watches a thin trickle that should have been a river. The Wend left because there was nothing left to slow it down.",
    wrenNote: "I have never been so quietly angry. The kind of anger that doesn't move your face. I think this is what my father would have looked like at this view. I am glad he was spared it.",
    beats: [
      "Wren walks the upstream clear-cut for herself.",
      "The scale of the deforestation is undeniable.",
      "Aldermark mushrooms grow at the edge of the cleared slope — leverage for a negotiation."
    ]
  },
  {
    id: 'meeting_aldric',
    act: 'Act IV',
    scene: 'Scene 3',
    title: 'The Meeting',
    subtitle: 'Lord Aldric across a polished table, eight miles away.',
    image: '/story/cards/meeting-aldric.png',
    questId: 'meetAldric',
    unlockAt: 25,
    body: "The manor is smaller than the rumours. Lord Aldric is educated, smooth, condescending without realizing it. He treats Wren with surprising respect once they meet. He offers her tea. He offers her terms. He waits, with the patience of a man who has never been told no by someone who could not be replaced. Wren takes the tea, and answers slowly.",
    wrenNote: "Aldric is not a wicked man. He is a careful one. That, I think, is worse to say no to. Easier, perhaps, to forgive afterward.",
    beats: [
      "Wren meets Lord Aldric at his manor.",
      "He offers a partnership at his terms.",
      "Wren's answer determines which ending follows."
    ]
  },

  // ============== ENDINGS ==============
  {
    id: 'ending_free_hollow',
    act: 'Endings',
    scene: 'Ending A',
    title: 'The Free Hollow',
    subtitle: 'Wren refuses. Hollowfen stays its own.',
    image: '/story/cards/ending-free-hollow.png',
    questId: 'endingFreeHollow',
    unlockAt: 26,
    body: "Wren says no, and the saying takes a long time. Hollowfen stays its own — independent, smaller than Aldric's offer would have made it, prouder than any neighbouring village dares be. Foragers come to learn from Sister Almy. Travellers stop for Marra's stew. The Wend does not return, but the village no longer needs it to. Wren writes the next page of her father's journal in her own hand.",
    wrenNote: "I told Lord Aldric no. He smiled, and I think he meant it. I do not know if I have ruined us. I know I have not ruined us today.",
    beats: [
      "Wren refuses Aldric's offer.",
      "Hollowfen becomes a free-trade hub for foragers and herbalists.",
      "Hardest path; requires the highest community score."
    ]
  },
  {
    id: 'ending_lordly_patronage',
    act: 'Endings',
    scene: 'Ending B',
    title: 'The Lordly Patronage',
    subtitle: 'Wren accepts. The village prospers, and changes.',
    image: '/story/cards/ending-lordly-patronage.png',
    questId: 'endingPatronage',
    unlockAt: 26,
    body: "Wren signs. The terms are generous. The village swells: roofs are repaired, the well is rebuilt with stone, the mill wheel turns again on a paid stipend. Theo leaves quietly for the Capital alone. Edda asks if she can leave with him. Wren tells her yes, and stands at the door until Edda is out of sight, and then a little longer.",
    wrenNote: "I made the bargain my grandmother would not have made. The village will eat. I will live with that. So will the village.",
    beats: [
      "Wren accepts Lord Aldric's offer.",
      "Hollowfen prospers under his banner — and loses some of its soul.",
      "Several villagers, including Theo and Edda, eventually leave."
    ]
  },
  {
    id: 'ending_capital',
    act: 'Endings',
    scene: 'Ending C',
    title: 'The Capital',
    subtitle: 'Wren leaves with Theo. Hollowfen becomes legend.',
    image: '/story/cards/ending-capital.png',
    questId: 'endingCapital',
    unlockAt: 26,
    body: "Wren goes with Theo. Her kitchen in the Capital is famous within a year, infamous within three. The Hollow is a place she talks about quietly to a cook over wine, the place where, briefly, things were saved. Hollin stays and takes Almy's mantle. Edda writes letters. Wren writes back, less every winter — she catches herself doing what her father did, and stops, and starts again.",
    wrenNote: "I left because there was a door open. I am writing this on a table that does not belong to my family. I cooked the goldfoot stew tonight. The chef across from me asked where the recipe came from. I said: a place I am from.",
    beats: [
      "Wren leaves with Theo for the Capital.",
      "Her name becomes synonymous with a moment Hollowfen had.",
      "Hollin remains and continues the foraging lineage."
    ]
  },
  {
    id: 'ending_witchs_path',
    act: 'Endings',
    scene: 'Ending D',
    title: "The Witch's Path",
    subtitle: 'Wren and Hollin take up the old mantle together.',
    image: '/story/cards/ending-witchs-path.png',
    questId: 'endingWitch',
    unlockAt: 26,
    body: "Wren turns down Aldric and turns down the Capital. The Witch's Cottage is repaired by the time the first snow falls. Wren and Hollin spend most of their days walking the deep wood, naming what is there, writing what is not yet written. Hollowfen prospers, more quietly than the other paths. People come from Veyrwick to ask questions and leave more thoughtful than they came.",
    wrenNote: "I do not know what to call what we are doing. We are not witches. We are not herbalists. We are reading a book that was almost erased, with hands that were almost forgotten. That is enough of a name for now.",
    beats: [
      "Wren takes up Sister Almy's mantle alongside Hollin.",
      "Hollowfen becomes a place of quiet wisdom rather than commercial recovery.",
      "The most introspective ending. Requires the highest knowledge score."
    ]
  }
];
