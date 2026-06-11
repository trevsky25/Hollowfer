# Hollowfen Story Card Image Brief

This document is the single source of truth for generating story-card illustrations for **Hollowfen — The Failing Village**. It is intended to be handed directly to the Codex image-generation tool.

There are **30 story cards** total. **7 are already illustrated** (listed under "Already Illustrated" below — do not regenerate). **23 still need art** and are listed under "Cards Needing Art" with full prompts. Each card needing art currently shows a parchment "ART PENDING" placeholder; generating the image with the correct filename below will replace that placeholder automatically.

---

## How to use this file (Codex instructions)

1. For each entry under **"Cards Needing Art"**, generate one image.
2. Save each image to `public/story/cards/<filename>.png`, overwriting the existing placeholder PNG of the same name. **Do not change the filename.** The game's data file (`src/data/StoryCards.js`) already references these exact filenames.
3. Format every image as **16:9 cinematic, ~1920×1080 PNG, no text, no logos, no watermarks, no borders, no captions, no UI overlays**.
4. Match the **shared art direction** and re-use the **character & setting bibles** so the cards feel like a single illustrated novel rather than a collection.
5. The "Already Illustrated" section is provided as visual / tonal reference only — match those images' palette, character likenesses, and texture.

---

## Shared art direction

- **Genre:** grounded folk-tale realism. Low fantasy. Think a hand-painted illustrated novel for adults, not a video-game key art frame.
- **Style:** painterly but representational. Visible brushwork in skies and cloth, clean drawing in faces and hands. Avoid digital airbrush smoothness. Avoid stylized cartoon proportions. No anime, no comic-book ink lines, no high-saturation fantasy poster look.
- **Color palette:** mossy greens, oat / parchment cream, weathered timber browns, hearth-orange firelight, river-stone grey, and twilight blues for night. Reds are reserved for blood, hearth, and a single wax seal late in the story — use sparingly.
- **Lighting:** naturalistic. Time-of-day matters. Dawn is cold and pale-pink. Dusk is amber and long-shadowed. Night interiors are lit by hearth, candle, or oil lamp — never floodlit. Avoid lens flares, godrays, and cinematic backlight unless the scene specifies them.
- **Composition:** cinematic 16:9. Characters are usually small or mid-frame against an environment that tells half the story. The Hollowfen world is a co-protagonist. Avoid centered hero portraits unless the entry specifies one.
- **Texture:** lived-in. Wood is grained, stone is mossed, cloth is mended, kettles are blackened. Nothing in Hollowfen is new.
- **Magic:** there is no overt fantasy magic in this story. No glowing runes, no spell effects, no floating particles, no obvious spirits. The strangeness is in the woods themselves, in mushrooms that shouldn't be there, in a river course that changed. If a mushroom "glows" (Brightspore, Wendlight), it is a faint biological luminescence — pale, almost gas-flame green or watery white, never neon.
- **Anachronisms to avoid:** no plate armor, no firearms, no printed paper, no electric light, no machine-uniform clothing. This is a low-medieval / early-modern rural England feel. Wool, linen, leather, iron, tallow, beeswax.
- **What never appears in any card:** text of any kind (signs, books-with-readable-pages, banners, captions), logos, watermarks, UI elements, brand marks, or any in-image typography.

---

## Character bible

Match likenesses across all cards. If a character appears in multiple scenes, they are the same person.

- **Wren Tobin** — the protagonist, late 20s, returning daughter. Tall and lean, weathered from three years working in another town's kitchens. Dark hair pulled back in a practical braid or low knot, falling loose late in the day. Tan complexion, sun-marked across the nose. Calm, observant face — not pretty, not plain; the kind of face that gets described as "steady." Wears layered traveling clothes: a worn linen shirt, dark wool overdress or jerkin, leather belt with a small foraging knife at the hip, ankle boots, sometimes a long olive-grey wool cloak. Often carries a wicker basket or a journal.
- **Old Bram** — innkeeper of The Crooked Pintle. Sixties, broad-shouldered, white beard, ruddy cheeks, a slight stoop from years of leaning over the bar. Wears a loose linen shirt, wool waistcoat, an apron with old stains. Apologizes a lot. Loud when he laughs, quiet when he doesn't.
- **Marra** — Bram's wife and the inn's cook. Mid-fifties, strong-armed, hair tied up in a kerchief. Practical apron, sleeves rolled. Carries the authority of someone who knows exactly how a kitchen runs. Doesn't smile often; when she does it lands.
- **Sister Almy** — the village's vine-tender / gardener / unfinished hedge-witch apprentice. Late fifties to early sixties. Slight, wiry, weathered hands. Plain dark dress, a simple linen wimple or kerchief over grey hair, a leather satchel of seedbooks and dried herbs at her hip. Quiet authority. Looks at people too directly.
- **Edda** — village girl, around fourteen. Quiet, observant, often with a basket or an empty bucket. Wears a brown homespun smock, headscarf or untidy braid, dirty bare feet in summer or worn ankle boots in cooler months. Stands at the edges of frames. Speaks rarely.
- **Joren** — village smith. Forties, broad as a doorframe, blackened hands and forearms, sweat-flat hair. Leather apron over a sooty shirt, sleeves cut off. Resentful smile that isn't actually unkind.
- **Theo** — traveling trader. Late thirties, well-groomed for someone living out of a wagon. Dark trim beard, slight smile lines, traveling coat of better cloth than anyone else in Hollowfen wears. Always carries a small ledger and a leather money-pouch. Reads people as he reads ledgers.
- **Master Voss** — Lord Aldric's tax collector. Late forties. Gaunt, plain, tired. Drab grey-brown traveling clothes, no insignia, no flourish. Carries a satchel of papers and a small ledger. Looks like a man who doesn't enjoy his work and is too professional to say so.
- **Lord Aldric** — the regional lord. Fifties. Educated, well-fed, well-dressed in dark wool with a velvet collar and a single signet ring. Trim grey beard. Polite, condescending without realizing it. Never appears outdoors in muddy boots. Never raises his voice.
- **Father Calden** — the village priest. Sixties. Lean, white-haired, tonsured or close-cropped. Plain dark cassock, simple wooden cross, no jewelry. A face that has been carrying a doubt for a long time.
- **Hollin** — a quiet traveler in her late twenties, granddaughter of the last hedge-witch. Pale, dark-haired, slight, quietly self-possessed. Travels light: a single satchel, plain travel clothes in the same earth tones as Wren. Resembles Wren without being a mirror — they look like cousins, never sisters. Speaks rarely.
- **Elder Pell** — the village elder and informal record-keeper. Seventies, stooped, perpetually with a leather-bound ledger and a stub of a pencil tucked behind one ear.

---

## Setting bible

- **Hollowfen** — small failing village in a wooded river valley. Roughly twenty-thirty cottages of timber-frame and wattle, mossy thatched roofs, a stone-and-timber inn (The Crooked Pintle) at the center, a small chapel with a walled garden, and Tobin's mill at the east end. Several cottages are visibly boarded up at story start. The village square is unpaved, with a stone well in the middle.
- **Tobin's mill** — Wren's family home and her father's old water-mill. Two-story timber-and-fieldstone building. A large wooden water wheel hangs over a stream-bed that no longer has water in it. Inside: stone floor dusted with old flour, a hearth, a kitchen with a long worn table, a back drawer-room with ledgers, a sleeping loft above. Quiet, half-lived-in, kettle still on the hearth.
- **The Crooked Pintle** — village inn. Low-beamed common room with a long bar, mismatched stools and benches, a big stone hearth, copper pots hanging by the kitchen door. Slightly too dark inside for the time of day. Outside: a hand-painted hanging sign (do not include readable text), a worn doorstep, an iron boot-scraper.
- **Edge Woods** — the gentle wooded edge of the village. Birch, beech, pine. Bracken, fern, leaf litter. Soft morning light filtering through. Mushrooms grow at the bases of trees, in fairy rings, on fallen logs. Feels safe.
- **Deep Wood** — older forest further out. Thicker trunks, moss-cloaked, less light. Path narrows. Feels watched without being threatening.
- **The Witch's Cottage** — a small one-room stone-and-timber cottage deep in the Old Wood. Steeply pitched mossed thatch, ivy climbing one wall, small stone chimney, single shuttered window. A spring at the back trickles into a stone basin. Inside: a heavy oak table, a hearth, a shelf of clay jars, an open seedbook on the table.
- **Wend's dry riverbed** — a wide bed of pale rounded stones where a river used to run. Some pools remain. Pale grass-fingers and unusual mushrooms creeping into the bed.
- **Chapel garden** — a small walled herb and vegetable garden behind the village chapel. Low stone wall, wooden gate, raised beds gone to seed, a single old apple tree.
- **Upstream clear-cut** — a wide hillside one day's walk upstream that has been industrially logged. Stumps in rough rows, drag-trails of dragged timber, an abandoned woodcutters' camp with rusting cookpots and a collapsed canvas shelter.
- **Lord Aldric's manor** — a modest stone manor house eight miles from Hollowfen. Gravel drive, slate roof, two stories. Inside: oak-paneled study, polished long table, leaded windows, a fireplace, a few painted portraits (faces turned away from camera so they read as decoration not characters).

---

## Already illustrated (visual reference only — DO NOT regenerate)

These seven images are canon and set the tone. New images should match their character likenesses, palette, and painterly grain.

- `homecoming.png` — Wren returns to Hollowfen at dusk; Old Bram gives her the mill key outside The Crooked Pintle.
- `fathers-mill.png` — Wren opens a drawer in her father's abandoned mill and finds the hidden mushroom journal.
- `first-forage.png` — Wren kneels among mushrooms in the Edge Woods while Edda watches quietly from the path.
- `marra-kitchen.png` — Marra washes golden mushrooms in the inn kitchen while Wren learns beside her.
- `almy-doorway.png` — Sister Almy appears at Wren's mill-house doorway at dawn with a seedbook and dried mushrooms.
- `theo-trade.png` — Theo examines a basket of goldfoot chanterelles by his trader wagon at Hollowfen's edge.
- `witch-cottage.png` — Wren and Hollin discover the ivy-covered Witch's Cottage in the Deep Wood at blue twilight.

---

## Cards needing art (23)

Each entry: target filename → act/scene → one-sentence subject → full prompt. **Use the prompt verbatim or as a starting brief — every detail listed is intentional.**

### ACT I — ARRIVAL

#### `crooked-pintle.png` — Act I, Scene 2 — "The Crooked Pintle"
*Old Bram hands Wren the mill key wrapped in a rag inside the half-dark inn.*

Interior of The Crooked Pintle inn, late evening, lit only by hearthfire and a single tallow candle on the bar. Old Bram (sixties, white beard, wool waistcoat, apron) stands behind a worn oak bar, both hands extended, holding out a small iron mill key wrapped in a rough grey rag like it might bite. Wren (late 20s, traveling cloak still on her shoulders, pack at her feet, dark braid) stands across the bar receiving it, her hand half-extended. Their eyes meet. The common room behind them is mostly empty — two unoccupied stools, one shadowy figure at a far table not facing camera. Copper pots hang in shadow above the kitchen door. Warm orange firelight on Bram's right side, cool blue twilight bleeding through the small window behind Wren. Quiet, apologetic mood. Match Bram's likeness from `homecoming.png`. No text, no signage readable.

#### `hidden-journal.png` — Act I, Scene 4 — "The Hidden Journal"
*Wren finds her father's hidden mushroom journal in a drawer of his mill.*

Interior of Tobin's mill kitchen, mid-morning. Wren kneels on the stone floor in front of an open bottom drawer of a heavy wooden chest, both hands holding a worn brown leather journal, oilcloth wrapping fallen open in her lap. The journal is open to a page with three pencil sketches of mushrooms — only loose impressions, never readable text. Pale gold light through a single small window catches floating dust. The hearth is unlit. A coat hangs on a peg behind her. Old receipts and ledgers stack beside the open drawer. Her face is half-lit, expression caught between recognition and grief, lips slightly parted. Camera over her shoulder, slightly above. Painterly stillness. Match the mill's interior wood and stone from `fathers-mill.png`.

### ACT II — BUILDING

#### `almy-lessons.png` — Act II, Scene 1 — "The Vine-Tender's Lessons"
*Almy teaches Wren mushroom cultivation in her back garden.*

Sister Almy's small walled back garden, mid-morning, late summer. Raised wooden beds full of unfamiliar herbs (sage, comfrey, mugwort) and a low wattle frame with damp wood-ear logs propped against it. Sister Almy (late 50s, dark dress, grey kerchief, leather satchel) crouches at one of the beds with both hands in the soil, gesturing toward a small clutch of pale mushrooms. Wren kneels beside her, sleeves rolled, hands also in the dirt, leaning in to listen, an open notebook on a folded cloth at her knee. Soft slanting morning light, dappled through an apple tree at the garden's edge. The chapel wall rises beyond. Mood: companionable, quiet teaching. No glowing magic; the mushrooms look ordinary. Match Almy's likeness from `almy-doorway.png`.

#### `jorens-forge.png` — Act II, Scene 2 — "Joren's Forge"
*Joren shows Wren the foraging knife he made for her.*

Interior of a small village smithy at midday. Joren (forties, broad, leather apron, sooty arms, sleeves cut off) stands at the anvil holding up a slim foraging knife with a horn handle, the blade catching warm orange firelight from the forge behind him. Wren stands across the anvil, both hands flat on its edge, looking at the knife with an unguarded smile. Tools and a hanging row of half-finished iron hooks frame the back wall. A single rectangle of midday sunlight falls through the open smithy door at left, cool against the orange forgelight. Coal smoke softens the air. Joren's expression resists his own pride. Mood: gruff warmth.

#### `voss-first-visit.png` — Act II, Scene 3 — "Twelve Silver by Yule"
*Master Voss collects taxes inside the inn while villagers watch.*

Interior of The Crooked Pintle, mid-afternoon, full common room. Master Voss (late 40s, gaunt, drab grey-brown coat, no insignia, leather satchel) sits at a long bench-table facing camera, a small ledger and a row of coins in front of him, hands folded. Wren stands at the table's near end, a leather coin-pouch open in one hand, counting silver onto the wood. To one side, the Wenmar family — a worn-looking man, his wife, and a teenage son — sit on the next bench, looking down at their boots, hands clasped. Other villagers sit in the background, watching without speaking. Bram stands behind the bar with a cloth, mid-wipe, frozen. Light is grey-flat from the windows; no fire is lit despite the season. Mood: bureaucratic dread, partial relief. Match Bram and the inn interior to `homecoming.png` and `crooked-pintle.png`.

#### `edda-grandfather.png` — Act II, Scene 5 — "Brightspore at the Bedside"
*Edda's grandfather, recovering, sips broth in his bed.*

Small dim cottage bedroom, evening. An elderly man (late 70s, thin, white-haired, propped up on a stack of wool pillows in a low rope-bed under a quilt) holds a wooden bowl in both hands and is sipping broth, eyes closed in quiet appreciation. Edda (about 14, brown smock, untidy braid) stands at the foot of the bed in shadow, one hand resting on the bedpost, watching with a strangely heavy expression — relief warring with something older than relief. Wren stands further back at the doorway, half in the corridor, holding a small glass bottle of pale, faintly luminous Brightspore tonic — barely glowing, gas-flame green, almost imperceptible. Single candle on a stool beside the bed gives the only warm light. Window shutters closed. Mood: hushed, sacred. Faint glow on the tonic only — no other magic.

#### `hollin-arrives.png` — Act II, Scene 6 — "A Stranger at the Inn"
*A quiet traveler named Hollin sits with Wren in the inn.*

Interior of The Crooked Pintle, late afternoon. Hollin (late 20s, pale, dark-haired, plain travel clothes in earth tones, single satchel on the bench beside her) and Wren sit across from each other at a small table near the window, two clay mugs between them. Both women are leaning slightly inward, mid-conversation, but neither is speaking — a comfortable silence. Hollin's posture is contained, careful. Wren's posture mirrors hers without copying it. Soft afternoon light through the small window picks out the side of Hollin's face. Background: Bram pretending not to watch from behind the bar, two other villagers at a far table. The two women resemble cousins, not sisters — same era, same kind of face, different lineage. Mood: quiet recognition. Match the inn interior to `homecoming.png` and `crooked-pintle.png`.

#### `cottages-reopen.png` — Act II, Scene 7 — "Two Boards Come Down"
*A reopened cottage with new shutters at the autumn equinox.*

Hollowfen village lane, autumn afternoon, low golden sun. A small stone-and-timber cottage center frame: two of its windows have brand-new pale-wood shutters, distinctly lighter than the weathered timber around them. The boards that had covered them are stacked neatly to one side. A man on a short ladder hammers the last shutter hinge in place. A woman crouches at the threshold, sweeping out three years of dust into the lane. A small boy carries an iron pot toward the doorway. In the middle distance, Elder Pell (seventies, stooped, leather ledger under his arm) walks past, pencil in hand, glancing at the cottage and writing as he walks. Long autumn shadows, leaves blowing along the lane. A second cottage further back also shows new shutters. Mood: cautious hope, quiet labor. No fanfare.

#### `caldens-doubt.png` — Act II, Scene 8 (Act break) — "Father Calden's Doubt"
*Father Calden visits Wren privately at her mill kitchen.*

Interior of Tobin's mill kitchen, late morning, grey overcast light through the window. Father Calden (sixties, lean, white-haired, plain dark cassock, simple wooden cross) stands inside the door but has not sat down, hat in hand, posture formal. Wren sits at the long kitchen table with a half-empty cup of tea, looking up at him steadily. A second untouched cup sits across from her at the chair he has refused. The hearth is lit but low. A single foraging basket with three goldfoot mushrooms sits on the end of the table. Calden's face is troubled, kind, tired. He doesn't make eye contact. Mood: a difficult kindness. Cool grey window light contrasts with the small warm fire. Match the mill interior to `fathers-mill.png` and `hidden-journal.png`.

### ACT III — DISCOVERY

#### `hollin-inheritance.png` — Act III, Scene 1 — "Hollin's Inheritance"
*Wren and Hollin sit in the mill kitchen as Hollin reveals her lineage; Almy weeps quietly.*

Interior of Tobin's mill kitchen, damp grey morning, faint rain audible. Wren and Hollin sit across from each other at the long kitchen table, both with their hands wrapped around mugs that are no longer steaming. Sister Almy stands at the back of the room near the hearth, half-turned toward the wall, one hand pressed flat against the stones, the other against her face — weeping quietly, with dignity, in profile. Light is soft and watery from the window. The hearth fire is small but warm. The wet smell of wool is implied by faintly damp cloaks hung on the door pegs. Three women across three generations of the same severed lineage. No one is centered. Mood: a long-postponed honesty.

#### `wend-truth.png` — Act III, Scene 3 — "The Wend's True Course"
*Wren walks the dry riverbed at dawn where Wendlight glows faintly.*

The Wend's dry riverbed at dawn, pale pink-grey light. Wide flat stretch of pale rounded river-stones running diagonally through frame from upper right to lower left. Wren walks the bed alone, mid-frame, small in the composition, wrapped in her wool cloak, one hand trailing at her side. Scattered across the dry stones at her feet and continuing into the distance: small pale mushrooms (Wendlight) glowing very faintly with a cool watery light — barely-there, like the last embers of a candle, never neon. The forest closes in on either bank. Mist clings low. No animals, no other figures. Mood: anger held quietly, the kind that does not move the face. Camera low and slightly behind Wren so the riverbed leads the eye into the distance.

#### `chapel-garden.png` — Act III, Scene 4 — "The Chapel Garden Opens"
*Father Calden hands Wren the chapel garden key at the gate.*

Exterior, the wooden gate of the chapel garden, late afternoon. A low fieldstone wall, an old apple tree visible behind, raised beds gone to seed inside. Father Calden stands just inside the open gate, holding out a heavy iron key on a leather thong. Wren stands just outside the gate, accepting it with her right hand. A brown paper envelope is tucked under Calden's left arm. He is not looking her in the eye — his gaze is downcast, jaw set. Hers is steady, kind. Soft warm late-day light gilds the chapel wall behind them. The chapel itself rises beyond the garden wall, plain stone with a small cross at the gable. Mood: an apology that doesn't quite finish itself. Match Calden's likeness from `caldens-doubt.png`.

#### `edda-apprentice.png` — Act III, Scene 5 — "Edda Asks"
*Edda stands at Wren's mill door at first light, asking to be apprenticed.*

Exterior, the threshold of Tobin's mill at first light. Edda (about 14, brown smock, scrubbed face, hair freshly braided, hat held in both hands at her waist, posture rigid with rehearsed nerve) stands one step below the door. Wren stands in the open doorway, surprised but composed, in a simple linen shirt and dark wool overdress, hair still loose from sleep. Their eyes are level despite the step. Cold pale dawn light from the east casts long shadows across the worn wooden threshold. The mill wheel is visible at the right edge of frame, motionless. Mist curling along the dry stream-bed below. Mood: a serious child making a serious request. Match Wren and the mill exterior to `homecoming.png` and `almy-doorway.png`.

#### `theo-capital-offer.png` — Act III, Scene 6 — "Theo's Capital"
*Theo makes his Capital offer to Wren in an empty inn.*

Interior of The Crooked Pintle, late evening, the room emptied. Theo (late 30s, well-trimmed beard, traveling coat, ledger closed at his elbow) and Wren sit across from each other at a small table near the dying hearth. Two cups of dark wine sit between them, one untouched. Theo is leaning slightly back, hands open on the table, mid-sentence — not pitching, telling. Wren is leaning slightly forward, both hands wrapped around her cup, listening with a still face. The bar behind them is unattended; the kitchen door is closed; no other patrons. The hearth's coals throw warm low light up onto their faces from below. Outside the window: full dark. Mood: a kind man making a kind offer that costs something. Match Theo's likeness from `theo-trade.png`.

#### `first-festival.png` — Act III, Scene 7 — "The First Festival in Three Years"
*The village square at lantern-lighting, full of people, four signature dishes on a long table.*

The village square of Hollowfen at the moment lanterns are being lit, blue-hour evening. The square is full of villagers — adults talking in clusters, children weaving through, three musicians with a fiddle and a small drum at one corner. A long trestle table down the center carries four large clay serving pots and stacked wooden bowls. Marra is at the table mid-ladle, sleeves rolled, kerchief on. Bram is behind the makeshift bar at the inn doorway, weeping openly without trying to hide it, a clean cloth pressed to his cheek. Wren is in the middle distance, half-turned away from the camera, carrying a stack of empty bowls toward the kitchen — caught not as the guest of honor but as the dish-washer. Lanterns hang on cords strung between cottage eaves; their light is warm and just lit, contrasting with the cool blue of the sky. Elder Pell stands at the inn step writing in his ledger by lantern-light. Mood: a quiet triumph held by people who don't quite trust it yet. Match Bram, Marra, and Wren to existing cards.

#### `sealed-letter.png` — Act III, Scene 8 (Act break) — "A Sealed Letter"
*Master Voss hands Wren a sealed letter at her mill door.*

Exterior, the mill doorway, mid-afternoon, overcast. Master Voss stands one step below the door, no horse visible, traveling cloak dust-touched, looking unusually tired. He extends a folded letter sealed with a thick disc of dark wine-red wax. The wax catches the only saturated color in the frame. Wren stands in the doorway, taking the letter with both hands, a small frown pulling between her brows. Voss's posture is plain, unguarded — for once not the bureaucrat. The mill wheel, motionless, is at the right edge of frame. The dry stream-bed runs behind him into the trees. No insignia on Voss's clothing. Mood: a man delivering something he wishes weren't his to deliver. Match Voss to `voss-first-visit.png` and Wren to `homecoming.png`.

### ACT IV — THE CHOICE

#### `aldric-offer.png` — Act IV, Scene 1 — "The Lord's Offer"
*Wren reads Lord Aldric's letter alone at her father's kitchen table.*

Interior of Tobin's mill kitchen, evening. Wren sits at the long kitchen table, alone, the unfolded letter spread flat in front of her. The broken dark-red wax seal sits on the table beside it. A single tallow candle lights the page from the left, warm and close, leaving most of the room in shadow. Wren's elbows rest on the table, fingers laced, chin resting on her thumbs, eyes on the page — she is reading it for the third time. The hearth at the back of the kitchen is lit but low. The hidden mushroom journal is closed at the corner of the table beside an inkpot and a quill. Outside the window: full dark. Mood: a private weighing. Camera at the table's height, slightly to one side. Match Wren and the mill kitchen to `hidden-journal.png`, `caldens-doubt.png`, `hollin-inheritance.png`.

#### `wend-source.png` — Act IV, Scene 2 — "The Source of the Wend"
*Wren stands at the lip of the upstream clear-cut and sees the scale of the deforestation.*

A wide hillside one day's walk upstream, midday, harsh flat light. The hillside has been industrially clear-cut: stumps in rough rows running into the distance, drag-trails of dragged timber gouged into the bare red-brown earth, a scattering of slash and broken branches. An abandoned woodcutters' camp mid-distance: a collapsed canvas shelter, a rusting cookpot beside a cold fire-ring, a single tipped saw-horse. A thin trickle of water — what should be the Wend — runs down a shallow gully. Wren stands at the lip of the cleared slope in the foreground, back three-quarters turned to camera, small in the composition, looking out over it all. Her hands are loose at her sides. At the very edge of the cleared slope at her feet, a small clutch of pale Aldermark mushrooms grows defiantly. The wood behind her is dense and dark; the wood in front of her is gone. Mood: unmoving, quiet anger. Cold. Bird-empty.

#### `meeting-aldric.png` — Act IV, Scene 3 — "The Meeting"
*Wren meets Lord Aldric across the polished table of his manor study.*

Interior of Lord Aldric's oak-paneled manor study, late morning. A polished long table runs across the frame. Lord Aldric (fifties, dark wool with velvet collar, signet ring, trim grey beard, seated in a high-backed chair) sits on the far side, hands folded on the table, an open ledger and an untouched cup of tea before him. Wren sits opposite him in a simpler chair, traveling clothes plainly out of place against the polish, both hands wrapped around her own cup of tea. She is mid-sip, eyes on Aldric over the rim. Tall leaded windows at the right pour cold north light across the table; a small fire in a stone fireplace at the left throws warm orange counter-light. Two painted portraits on the back wall, faces turned away or shadowed so they read as decoration. The room is enormous around them. Mood: a careful negotiation. Aldric is not menacing — he is patient. Wren is not cowed — she is also patient.

### ENDINGS

#### `ending-free-hollow.png` — Ending A — "The Free Hollow"
*Hollowfen as a small free-trade hub of foragers and herbalists, a year on.*

Hollowfen village square, late summer afternoon, a year after the events of Act IV. The square is busier than it has ever been — three small trestle tables under linen awnings where foragers from neighboring villages have come to learn from Sister Almy, who is mid-demonstration at one of them, a basket of wild mushrooms laid out before her. Wren stands at the inn doorway in the middle distance, looking out at it all with a calm small smile. Edda crosses the square carrying a stack of wood-ear logs, taller and more grown-up. The boarded cottages from Act I now have shutters, smoking chimneys, washing lines. The mill wheel in the far distance is still motionless — the river has not returned — but the village around it is clearly alive. The mood is quiet pride, not triumph. Warm afternoon light, long soft shadows.

#### `ending-lordly-patronage.png` — Ending B — "The Lordly Patronage"
*Hollowfen prosperous under Aldric's banner; Wren watches Theo and Edda leave at the village edge.*

The lane out of Hollowfen at the village edge, mid-morning. In the foreground at the right edge of frame, Wren stands alone in the lane, one hand at her chest, watching. In the middle distance, Theo's loaded trade wagon recedes down the lane toward the horizon — Theo on the driver's bench, and beside him a young woman in traveling clothes carrying a small bundle: Edda, looking back over her shoulder. Behind Wren, the village is visibly more prosperous: the mill wheel is turning slowly on a paid stipend (a thin sluice has been engineered to feed it), the well has been rebuilt in clean cut stone, several rooflines have new thatch, a new sign hangs over the inn. A small banner with a stylized device (no readable heraldry, just a colored pennant) flies from the chapel. The light is bright but cool. Mood: a fair bargain made, and the cost of it leaving down the lane. Match Wren, Theo, Edda to existing cards.

#### `ending-capital.png` — Ending C — "The Capital"
*Wren in her own kitchen in the Capital, years later, alone after service.*

Interior of a busy professional kitchen in the Capital, very late at night, after the last service. Wren stands alone at a long copper-topped prep table in a clean white apron over dark clothes, sleeves rolled, hands flat on the table, looking down at a single steaming bowl of golden mushroom stew between her palms. Her hair is shorter than in Hollowfen, pinned back. She is older — late thirties — and the same face. Behind her, the kitchen is dim except for two oil lamps and the dying coals of a much larger range than anything in Hollowfen. Copper pots in disciplined rows on the back wall. A folded letter, unopened, sits at the edge of the table — return address implied to be from home. Mood: success and the price of it. The bowl of stew is the same recipe she cooked in `marra-kitchen.png`. Camera at slight three-quarter angle, soft warm pool of lamp-light on Wren and the bowl, the rest in shadow.

#### `ending-witchs-path.png` — Ending D — "The Witch's Path"
*Wren and Hollin walk a snowy Deep Wood path together at first snow.*

The Deep Wood at first snowfall, late afternoon, blue-hour light. A narrow path winds through old moss-cloaked trees now dusted in fresh fine snow. Wren and Hollin walk together along the path, mid-frame, three-quarters away from camera, neither leading nor following, both wrapped in heavy wool cloaks, both with foraging baskets at their hips. They are mid-conversation, walking slowly, Hollin gesturing toward something off to the left. The Witch's Cottage is visible in the middle distance ahead of them — repaired now: ivy trimmed back, fresh thatch patched into the old roof, a thin curl of smoke rising from the stone chimney. Soft cold blue light, a few last flakes of snow still falling. Mood: quiet shared purpose. Two women carrying forward a thing that was almost erased. Match Wren to existing cards and Hollin to `hollin-arrives.png` and `witch-cottage.png`.

---

*End of brief. Generate, save with the exact filenames above, and the game will pick the new images up automatically on next refresh.*
