# LinkedIn post — Hollowfen: the illustrated story

**Attach:** `Hollowfen-The-Story.pdf` (8-slide landscape document post)

---

## Caption

Last week I shared *how* I'm building Hollowfen — Claude wired into my editor, the whole AI-enablement loop.

This week, the *why*.

Hollowfen is a foraging game about Wren — a young woman who comes home to a dying village and slowly brings it back by learning to read the forest: which mushrooms feed people, and which kill them.

Swipe through the story so far 🍄
→ A father's hidden journal, left half-blank
→ A first forage watched by a silent girl
→ The first goldfoot cooked in the inn in twenty years
→ A knock at the door from someone who knew her grandmother
→ A cottage deep in the wood that remembers more than she does

Four acts. Four endings. One village deciding what it wants to become.

Every frame here is AI-generated concept art; the story and design are mine. That's the honest line I keep drawing in this project — let the model carry what it's good at, and keep my hands on what only I can decide.

Still building. Follow along if you want to watch a village come back to life.

#buildinpublic #gamedev #AI #indiedev #storytelling

---

## Notes
- Slides are built from the project's real story cards (`public/story/cards/*.png`) and the
  narrative in `src/data/StoryCards.js` (28-card spine, four acts + four endings).
- Cover uses `homecoming.png`. The 6 beats: father's mill, first forage, Marra's kitchen,
  Almy's doorway, Theo's trade, the Witch's cottage. Slide 8 is a themed closing/CTA card.
- Disclosure line on the final slide + in the caption notes the art is AI-generated — on-brand
  for the build-in-public / AI-enablement theme, and honest.
- To re-render: `chrome --headless --print-to-pdf=Hollowfen-The-Story.pdf file://…/hollowfen-story-carousel.html`
