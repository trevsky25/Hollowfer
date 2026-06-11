export const CHARACTER_IDENTITIES = [
  {
    id: 'wren-tobin',
    name: 'Wren Tobin',
    role: 'Protagonist / forager',
    priority: 'Player model',
    age: 'Late twenties',
    silhouette: 'Tall, lean, practical, slightly travel-worn. Long braid or low knot, layered clothes, belt tools.',
    palette: ['warm linen', 'deep rust', 'olive grey', 'dark leather', 'mushroom gold'],
    heroImage: '/ui/wren-profile.png',
    modelSheet: '/concept/wren-model-sheet/wren-full-character-sheet.png',
    storyRefs: [
      '/story/cards/homecoming.png',
      '/story/cards/first-forage.png',
      '/story/cards/hidden-journal.png'
    ],
    props: ['Wicker basket', 'Tobin field journal', 'Horn-handled foraging knife', 'Belt pouch', 'Mushroom specimens'],
    views: ['front/back/side game model turnaround', 'foraging crouch', 'journal-reading close-up', 'basket carry'],
    modelNotes: 'Wren is the visual anchor for the whole project. Keep her face steady and observant, never glamorous. Dirt under nails, weather-marked skin, practical layers, visible tool belt.'
  },
  {
    id: 'old-bram',
    name: 'Old Bram',
    role: 'Innkeeper / first buyer',
    priority: 'Unique NPC',
    age: 'Sixties',
    silhouette: 'Broad shoulders, white beard, ruddy cheeks, slight stoop, apron and waistcoat.',
    palette: ['warm hearth brown', 'faded cream linen', 'stained apron grey', 'old oak', 'beer amber'],
    heroImage: '/story/cards/crooked-pintle.png',
    modelSheet: '/concept/character-identity/old-bram-character-sheet.png',
    storyRefs: [
      '/story/cards/homecoming.png',
      '/story/cards/crooked-pintle.png',
      '/story/cards/first-festival.png'
    ],
    props: ['Mill key wrapped in rag', 'Broom', 'Bar cloth', 'Inn ledger', 'Clay mug'],
    views: ['bar stance', 'doorstep sweeping pose', 'key handoff', 'festival emotional beat'],
    modelNotes: 'Bram should feel physically large but emotionally careful. His face needs kindness, apology, and old volume held back.'
  },
  {
    id: 'marra',
    name: 'Marra',
    role: 'Cook / recipe mentor',
    priority: 'Unique NPC',
    age: 'Mid-fifties',
    silhouette: 'Strong arms, sleeves rolled, kerchief, practical apron, kitchen authority.',
    palette: ['flour white', 'smoked umber', 'kerchief red-brown', 'copper pot', 'stew gold'],
    heroImage: '/story/cards/marra-kitchen.png',
    modelSheet: '/concept/character-identity/marra-character-sheet.png',
    storyRefs: [
      '/story/cards/marra-kitchen.png',
      '/story/cards/first-festival.png'
    ],
    props: ['Copper pot', 'Wooden spoon', 'Wash basin', 'Kitchen knife', 'Serving ladle'],
    views: ['washing mushrooms', 'ladling festival stew', 'arms-crossed kitchen stance', 'recipe instruction pose'],
    modelNotes: 'Marra is compact force. She should read as someone who can run a kitchen, a room, and Bram with one look.'
  },
  {
    id: 'sister-almy',
    name: 'Sister Almy',
    role: 'Vine-tender / cultivation mentor',
    priority: 'Unique NPC',
    age: 'Late fifties to early sixties',
    silhouette: 'Slight, wiry, direct gaze, dark dress, grey kerchief or wimple, herb satchel.',
    palette: ['garden black', 'weathered grey', 'dry sage', 'seed-paper tan', 'dried herb green'],
    heroImage: '/story/cards/almy-doorway.png',
    modelSheet: '/concept/character-identity/sister-almy-character-sheet.png',
    storyRefs: [
      '/story/cards/almy-doorway.png',
      '/story/cards/almy-lessons.png',
      '/story/cards/hollin-inheritance.png'
    ],
    props: ['Seedbook', 'Dried herb bundles', 'Leather satchel', 'Cultivation log', 'Chapel garden key'],
    views: ['doorway reveal', 'kneeling garden lesson', 'seedbook inspection', 'quiet grief profile'],
    modelNotes: 'Almy should feel severe until she moves. Her hands matter: weathered, precise, teacherly.'
  },
  {
    id: 'edda',
    name: 'Edda',
    role: 'Apprentice / village future',
    priority: 'Unique NPC',
    age: 'Fourteen',
    silhouette: 'Small, watchful, edge-of-frame posture, brown homespun, untidy braid or headscarf.',
    palette: ['homespun brown', 'mud grey', 'washed linen', 'candle amber', 'young birch green'],
    heroImage: '/story/cards/edda-apprentice.png',
    modelSheet: '/concept/character-identity/edda-character-sheet.png',
    storyRefs: [
      '/story/cards/first-forage.png',
      '/story/cards/edda-grandfather.png',
      '/story/cards/edda-apprentice.png'
    ],
    props: ['Small basket', 'Empty bucket', 'Hat held in both hands', 'Apprentice notes', 'Tonic bottle'],
    views: ['watching from path', 'bedside shadow stance', 'formal apprentice request', 'basket carry'],
    modelNotes: 'Edda is quiet intensity. Avoid making her cute. She is serious, observant, and older inside than she should be.'
  },
  {
    id: 'joren',
    name: 'Joren',
    role: 'Smith / tool upgrades',
    priority: 'Unique NPC',
    age: 'Forties',
    silhouette: 'Broad as a doorframe, soot-black forearms, cut-off sleeves, leather apron.',
    palette: ['forge black', 'coal grey', 'leather brown', 'iron blue', 'fire orange'],
    heroImage: '/story/cards/jorens-forge.png',
    modelSheet: '/concept/character-identity/joren-character-sheet.png',
    storyRefs: [
      '/story/cards/jorens-forge.png',
      '/story/cards/cottages-reopen.png'
    ],
    props: ['Horn-handled knife', 'Hammer', 'Tongs', 'Anvil', 'Hinge set'],
    views: ['anvil stance', 'knife presentation', 'hammering idle', 'resentful half-smile close-up'],
    modelNotes: 'Joren should look gruff and useful, with pride he tries to hide. His hands and forearms are a character feature.'
  },
  {
    id: 'master-voss',
    name: 'Master Voss',
    role: 'Tax collector / Aldric messenger',
    priority: 'Unique NPC',
    age: 'Late forties',
    silhouette: 'Gaunt, plain, tired, drab coat, satchel held close, controlled posture.',
    palette: ['drab grey-brown', 'paper cream', 'ink black', 'cold pewter', 'worn leather'],
    heroImage: '/story/cards/voss-first-visit.png',
    modelSheet: '/concept/character-identity/master-voss-character-sheet.png',
    storyRefs: [
      '/story/cards/voss-first-visit.png',
      '/story/cards/sealed-letter.png'
    ],
    props: ['Tax ledger', 'Coin rows', 'Paper satchel', 'Sealed Aldric letter', 'Small pencil'],
    views: ['seated ledger count', 'doorstep letter handoff', 'traveler silhouette', 'professional neutral close-up'],
    modelNotes: 'Voss is not a villain costume. He should look like a tired professional trapped inside a cruel system.'
  },
  {
    id: 'theo',
    name: 'Theo',
    role: 'Trader / Capital temptation',
    priority: 'Unique NPC',
    age: 'Late thirties',
    silhouette: 'Well-groomed traveler, trim beard, better coat, ledger and money pouch.',
    palette: ['good dark wool', 'brass clasp', 'wagon leather', 'ledger tan', 'wine red'],
    heroImage: '/story/cards/theo-trade.png',
    modelSheet: '/concept/character-identity/theo-character-sheet.png',
    storyRefs: [
      '/story/cards/theo-trade.png',
      '/story/cards/theo-capital-offer.png',
      '/story/cards/ending-capital.png'
    ],
    props: ['Trade ledger', 'Money pouch', 'Wagon crates', 'Clay wine cup', 'Wrapped sample bundle'],
    views: ['wagon-side trade pose', 'seated offer pose', 'ledger writing', 'friendly but measuring close-up'],
    modelNotes: 'Theo should feel cleaner and more worldly than Hollowfen without looking noble. His smile is useful, not false.'
  },
  {
    id: 'hollin',
    name: 'Hollin',
    role: 'Traveler / deep-wood companion',
    priority: 'Unique NPC',
    age: 'Late twenties',
    silhouette: 'Slight, pale, dark-haired, contained posture, travels light, earth-toned clothes.',
    palette: ['wet bark', 'soft charcoal', 'moss grey', 'faded umber', 'cold cream'],
    heroImage: '/story/cards/hollin-arrives.png',
    modelSheet: '/concept/character-identity/hollin-character-sheet.png',
    storyRefs: [
      '/story/cards/hollin-arrives.png',
      '/story/cards/hollin-inheritance.png',
      '/story/cards/witch-cottage.png'
    ],
    props: ['Single satchel', 'Old pages', 'Plain cloak', 'Travel mug', 'Seedbook touchpoint'],
    views: ['quiet inn table pose', 'deep-wood walking companion', 'satchel carry', 'still listening close-up'],
    modelNotes: 'Hollin resembles Wren like a cousin, never a twin. She is quieter, paler, more inward, and less physically rooted.'
  },
  {
    id: 'father-calden',
    name: 'Father Calden',
    role: 'Priest / skeptic turned ally',
    priority: 'Unique NPC',
    age: 'Sixties',
    silhouette: 'Lean, white-haired, dark cassock, wooden cross, formal posture.',
    palette: ['cassock black', 'aged linen', 'chapel stone', 'old paper brown', 'apple-leaf green'],
    heroImage: '/story/cards/caldens-doubt.png',
    modelSheet: '/concept/character-identity/father-calden-character-sheet.png',
    storyRefs: [
      '/story/cards/caldens-doubt.png',
      '/story/cards/chapel-garden.png'
    ],
    props: ['Wooden cross', 'Hat in hand', 'Brown church records envelope', 'Chapel garden key'],
    views: ['formal kitchen warning', 'key handoff at gate', 'downcast apology', 'records-reading pose'],
    modelNotes: 'Calden carries doubt in his face. He should look kind, rigid, and ashamed before he looks brave.'
  },
  {
    id: 'elder-pell',
    name: 'Elder Pell',
    role: 'Village elder / record keeper',
    priority: 'Semi-unique NPC',
    age: 'Seventies',
    silhouette: 'Stooped, narrow, slow-moving, ledger tucked under arm, pencil behind ear.',
    palette: ['old ink', 'ledger brown', 'dust grey', 'muted green', 'lantern amber'],
    heroImage: '/story/cards/cottages-reopen.png',
    modelSheet: '/concept/character-identity/elder-pell-character-sheet.png',
    storyRefs: [
      '/story/cards/cottages-reopen.png',
      '/story/cards/first-festival.png'
    ],
    props: ['Leather-bound ledger', 'Stub pencil', 'Spectacles optional', 'Village notice sheet'],
    views: ['walking and writing', 'festival note-taking', 'ledger close-up', 'stooped idle'],
    modelNotes: 'Pell is the memory of the village made visible. His ledger should be instantly readable in silhouette.'
  },
  {
    id: 'lord-aldric',
    name: 'Lord Aldric',
    role: 'Regional lord / final negotiator',
    priority: 'Unique NPC',
    age: 'Fifties',
    silhouette: 'Well-fed, educated, dark wool, velvet collar, trim grey beard, signet ring.',
    palette: ['black wool', 'wine velvet', 'polished oak', 'old gold', 'seal wax red'],
    heroImage: '/story/cards/meeting-aldric.png',
    modelSheet: '/concept/character-identity/lord-aldric-character-sheet.png',
    storyRefs: [
      '/story/cards/aldric-offer.png',
      '/story/cards/meeting-aldric.png',
      '/story/cards/ending-lordly-patronage.png'
    ],
    props: ['Signet ring', 'Sealed letter', 'Tea service', 'Polished negotiation table', 'Manor documents'],
    views: ['seated negotiation', 'letter-signing hand close-up', 'polite smile close-up', 'standing manor silhouette'],
    modelNotes: 'Aldric should not look cartoon-cruel. His danger is polish, entitlement, and calm certainty.'
  },
  {
    id: 'eddas-grandfather',
    name: "Edda's Grandfather",
    role: 'Bedridden elder / first life saved',
    priority: 'Quest-specific NPC',
    age: 'Late seventies',
    silhouette: 'Thin, white-haired, fragile, mostly seen propped in bed under a quilt.',
    palette: ['candle gold', 'quilt blue-grey', 'bone white', 'soup brown', 'tonic green'],
    heroImage: '/story/cards/edda-grandfather.png',
    modelSheet: '/concept/character-identity/eddas-grandfather-character-sheet.png',
    storyRefs: [
      '/story/cards/edda-grandfather.png'
    ],
    props: ['Wooden broth bowl', 'Wool quilt', 'Pillow stack', 'Brightspore tonic bottle'],
    views: ['bedridden before state', 'recovering broth sip', 'hand holding bowl', 'warm candle profile'],
    modelNotes: 'He can be a limited animation NPC, but the face and hands matter. The quest only works if he feels like a person.'
  },
  {
    id: 'wenmar-family',
    name: 'Wenmar Family',
    role: 'Tax stakes / cottage saved or lost',
    priority: 'Three villager variants',
    age: 'Adult couple and teenage son',
    silhouette: 'Worn rural family, clasped hands, guarded posture, boots watched too closely.',
    palette: ['worn wool', 'boot brown', 'ashen hearth', 'washed blue', 'dull brass'],
    heroImage: '/story/cards/voss-first-visit.png',
    modelSheet: '/concept/character-identity/wenmar-family-character-sheet.png',
    storyRefs: [
      '/story/cards/voss-first-visit.png',
      '/story/cards/cottages-reopen.png'
    ],
    props: ['Cottage key', 'Tax notice', 'Small coin purse', 'Mended shawl', 'Iron pot'],
    views: ['inn bench tax scene', 'saved cottage threshold', 'family grouping', 'teen son variant'],
    modelNotes: 'Design as modular villagers with a specific family read. They need poverty without caricature.'
  },
  {
    id: 'villager-set',
    name: 'Hollowfen Villager Set',
    role: 'Ambient crowd / labor / festival',
    priority: 'Modular NPC set',
    age: 'Children, adults, elders, travelers',
    silhouette: 'Simple rural body-shape variety with shared clothing system and color discipline.',
    palette: ['field brown', 'linen cream', 'dull green', 'weathered grey', 'lantern gold'],
    heroImage: '/story/cards/first-festival.png',
    modelSheet: '/concept/character-identity/villager-set-character-sheet.png',
    storyRefs: [
      '/story/cards/first-festival.png',
      '/story/cards/cottages-reopen.png',
      '/story/cards/ending-free-hollow.png'
    ],
    props: ['Wooden bowls', 'Lanterns', 'Bundles', 'Market baskets', 'Simple tools'],
    views: ['festival crowd variants', 'laborer repair pose', 'traveler arrival', 'child scale reference'],
    modelNotes: 'Build as a modular kit. Keep clothing humble and grounded; festival changes should come from accessories and lighting, not fancy costumes.'
  }
];
