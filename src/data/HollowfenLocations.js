export const LOCATION_PIN_STORAGE_KEY = 'hollowfen.locationPins.v1';

export const HOLLOWFEN_LOCATION_OPTIONS = [
  {
    group: 'Act I Core',
    items: [
      { id: 'arrival_road', name: 'Arrival Road', type: 'path', description: 'Where Wren first approaches Hollowfen.' },
      { id: 'village_square', name: 'Village Square', type: 'hub', description: 'Central hub around the well.' },
      { id: 'village_well', name: 'Village Well', type: 'landmark', description: 'Square landmark and restoration object.' },
      { id: 'crooked_pintle', name: 'The Crooked Pintle', type: 'inn', description: 'Bram and Marra\'s inn.' },
      { id: 'bram_start', name: 'Old Bram Start Position', type: 'npc', description: 'Bram sweeping or waiting near the inn.' },
      { id: 'tobins_mill', name: 'Tobin\'s Mill', type: 'home', description: 'Wren\'s home base and journal discovery location.' },
      { id: 'mill_doorway', name: 'Mill Doorway', type: 'interaction', description: 'Door/threshold used by Almy and Voss scenes.' },
      { id: 'mill_yard', name: 'Mill Yard', type: 'gameplay', description: 'Cultivation logs and home exterior tasks.' },
      { id: 'edge_woods_path', name: 'Edge Woods Path', type: 'path', description: 'First foraging route.' },
      { id: 'first_forage_grove', name: 'First Forage Grove', type: 'forage', description: 'Field Cap, Wood Ear, Pinecrest, Goldfoot tutorial zone.' }
    ]
  },
  {
    group: 'Act II Village',
    items: [
      { id: 'almy_garden', name: 'Almy\'s Garden', type: 'garden', description: 'Cultivation lesson location.' },
      { id: 'jorens_forge', name: 'Joren\'s Forge', type: 'smithy', description: 'Tool upgrades.' },
      { id: 'theo_wagon_stop', name: 'Theo\'s Wagon Stop', type: 'trade', description: 'Recurring trader arrival point.' },
      { id: 'voss_tax_table', name: 'Voss Tax Table', type: 'interior', description: 'Tax scene position inside/near inn.' },
      { id: 'wenmar_cottage', name: 'Wenmar Cottage', type: 'cottage', description: 'First tax consequence cottage.' },
      { id: 'edda_cottage', name: 'Edda\'s Cottage', type: 'cottage', description: 'Brightspore recovery quest.' },
      { id: 'reopened_cottage_a', name: 'Reopened Cottage A', type: 'cottage', description: 'First visible recovery cottage.' },
      { id: 'reopened_cottage_b', name: 'Reopened Cottage B', type: 'cottage', description: 'Second visible recovery cottage.' },
      { id: 'old_tavern_closed', name: 'Closed Tavern / Failed Business', type: 'building', description: 'Environmental sign of village failure.' }
    ]
  },
  {
    group: 'Faith and Old Lore',
    items: [
      { id: 'chapel', name: 'Chapel', type: 'chapel', description: 'Father Calden\'s primary location.' },
      { id: 'chapel_garden_gate', name: 'Chapel Garden Gate', type: 'gate', description: 'Locked/unlocked garden interaction.' },
      { id: 'chapel_garden_beds', name: 'Chapel Garden Beds', type: 'garden', description: 'Act III cultivation site.' },
      { id: 'deep_wood_entry', name: 'Deep Wood Entry', type: 'path', description: 'Route toward Witch\'s Cottage.' },
      { id: 'witch_cottage', name: 'Witch\'s Cottage', type: 'lore', description: 'Sable\'s cottage in the Deep Wood.' },
      { id: 'witchwell_spring', name: 'Witchwell Spring', type: 'lore', description: 'Spring behind the Witch\'s Cottage.' }
    ]
  },
  {
    group: 'River and Endgame',
    items: [
      { id: 'dry_wend_bed', name: 'Dry Wend Riverbed', type: 'river', description: 'Wendlight reveal route.' },
      { id: 'wendlight_cluster', name: 'Wendlight Cluster', type: 'forage', description: 'Old river-course clue.' },
      { id: 'upstream_clearcut', name: 'Upstream Clear-cut', type: 'endgame', description: 'Aldric logging evidence.' },
      { id: 'aldermark_patch', name: 'Aldermark Patch', type: 'forage', description: 'Negotiation leverage mushroom.' },
      { id: 'aldric_manor', name: 'Lord Aldric\'s Manor', type: 'manor', description: 'Final meeting location.' }
    ]
  },
  {
    group: 'Crowd and Event Staging',
    items: [
      { id: 'festival_table', name: 'Festival Trestle Table', type: 'event', description: 'Four-dish festival table.' },
      { id: 'festival_musicians', name: 'Festival Musicians', type: 'event', description: 'Musician staging spot.' },
      { id: 'pell_ledger_spot', name: 'Elder Pell Ledger Spot', type: 'npc', description: 'Where Pell records village changes.' },
      { id: 'market_stalls', name: 'Market Stalls', type: 'trade', description: 'Village square stalls.' },
      { id: 'traveler_arrival_spot', name: 'Traveler Arrival Spot', type: 'npc', description: 'Generic traveler/refugee entry point.' }
    ]
  }
];

export const LOCATION_OPTION_BY_ID = new Map(
  HOLLOWFEN_LOCATION_OPTIONS.flatMap((group) => group.items).map((item) => [item.id, item])
);

export const HOLLOWFEN_CANON_LOCATIONS = [
  {
    id: 'aldermark_patch',
    name: 'Aldermark Patch',
    type: 'forage',
    description: 'Negotiation leverage mushroom.',
    act: 'Act IV',
    position: { x: -134.65, y: 43.66, z: 118.19 }
  },
  {
    id: 'almy_garden',
    name: 'Almy\'s Garden',
    type: 'garden',
    description: 'Cultivation lesson location.',
    act: 'Act II',
    position: { x: -203.13, y: 38.20, z: 299.65 }
  },
  {
    id: 'arrival_road',
    name: 'Arrival Road',
    type: 'path',
    description: 'Where Wren first approaches Hollowfen.',
    act: 'Act I',
    position: { x: -218.20, y: 32.96, z: 232.01 }
  },
  {
    id: 'chapel',
    name: 'Chapel',
    type: 'chapel',
    description: 'Father Calden\'s primary location.',
    act: 'Act II',
    position: { x: -313.21, y: 37.23, z: 68.04 }
  },
  {
    id: 'chapel_garden_beds',
    name: 'Chapel Garden Beds',
    type: 'garden',
    description: 'Act III cultivation site.',
    act: 'Act III',
    position: { x: -330.59, y: 37.23, z: 75.40 }
  },
  {
    id: 'chapel_garden_gate',
    name: 'Chapel Garden Gate',
    type: 'gate',
    description: 'Locked/unlocked garden interaction.',
    act: 'Act II',
    position: { x: -330.33, y: 37.23, z: 83.78 }
  },
  {
    id: 'old_tavern_closed',
    name: 'Closed Tavern / Failed Business',
    type: 'building',
    description: 'Environmental sign of village failure.',
    act: 'Act II',
    position: { x: -230.23, y: 38.21, z: 284.10 }
  },
  {
    id: 'dry_wend_bed',
    name: 'Dry Wend Riverbed',
    type: 'river',
    description: 'Wendlight reveal route.',
    act: 'Act III',
    position: { x: -234.46, y: 32.29, z: 220.21 }
  },
  {
    id: 'edda_cottage',
    name: 'Edda\'s Cottage',
    type: 'cottage',
    description: 'Brightspore recovery quest.',
    act: 'Act II',
    position: { x: -260.32, y: 35.18, z: 204.45 }
  },
  {
    id: 'edge_woods_path',
    name: 'Edge Woods Path',
    type: 'path',
    description: 'First route from Hollowfen toward the Edge Woods.',
    act: 'Act I',
    position: { x: -145.14, y: 31.31, z: 256.65 }
  },
  {
    id: 'pell_ledger_spot',
    name: 'Elder Pell Ledger Spot',
    type: 'npc',
    description: 'Where Pell records village changes.',
    act: 'Act II',
    position: { x: -284.16, y: 35.18, z: 69.02 }
  },
  {
    id: 'festival_musicians',
    name: 'Festival Musicians',
    type: 'event',
    description: 'Musician staging spot.',
    act: 'Act III',
    position: { x: -286.54, y: 35.22, z: 103.23 }
  },
  {
    id: 'festival_table',
    name: 'Festival Trestle Table',
    type: 'event',
    description: 'Four-dish festival table.',
    act: 'Act III',
    position: { x: -277.51, y: 35.18, z: 100.46 }
  },
  {
    id: 'first_forage_grove',
    name: 'First Forage Grove',
    type: 'forage',
    description: 'The tutorial grove for Field Cap, Wood Ear, Pinecrest, and Goldfoot.',
    act: 'Act I',
    position: { x: -92.12, y: 30.98, z: 320.34 }
  },
  {
    id: 'jorens_forge',
    name: 'Joren\'s Forge',
    type: 'smithy',
    description: 'Tool upgrades.',
    act: 'Act II',
    position: { x: -247.55, y: 35.18, z: 187.50 }
  },
  {
    id: 'aldric_manor',
    name: 'Lord Aldric\'s Manor',
    type: 'manor',
    description: 'Final meeting location.',
    act: 'Act IV',
    position: { x: -322.71, y: 47.33, z: 288.92 }
  },
  {
    id: 'market_stalls',
    name: 'Market Stalls',
    type: 'trade',
    description: 'Village square stalls.',
    act: 'Act III',
    position: { x: -203.26, y: 32.65, z: 125.86 }
  },
  {
    id: 'mill_doorway',
    name: 'Mill Doorway',
    type: 'interaction',
    description: 'The threshold used for Almy and Voss doorway scenes.',
    act: 'Act I',
    position: { x: -297.37, y: 35.18, z: 112.62 }
  },
  {
    id: 'bram_start',
    name: 'Old Bram Start Position',
    type: 'npc',
    description: 'Bram sweeping or waiting near The Crooked Pintle.',
    act: 'Act I',
    position: { x: -226.196, y: 32.690, z: 95.797 }
  },
  {
    id: 'reopened_cottage_a',
    name: 'Reopened Cottage A',
    type: 'cottage',
    description: 'First visible recovery cottage.',
    act: 'Act II',
    position: { x: -205.03, y: 32.65, z: 196.12 }
  },
  {
    id: 'reopened_cottage_b',
    name: 'Reopened Cottage B',
    type: 'cottage',
    description: 'Second visible recovery cottage.',
    act: 'Act II',
    position: { x: -222.09, y: 33.15, z: 144.84 }
  },
  {
    id: 'crooked_pintle',
    name: 'The Crooked Pintle',
    type: 'inn',
    description: 'Bram and Marra\'s inn, first buyer and cooking hub.',
    act: 'Act I',
    position: { x: -226.826, y: 32.654, z: 112.598 }
  },
  {
    id: 'theo_wagon_stop',
    name: 'Theo\'s Wagon Stop',
    type: 'trade',
    description: 'Recurring trader arrival point.',
    act: 'Act II',
    position: { x: -251.92, y: 35.18, z: 142.71 }
  },
  {
    id: 'tobins_mill',
    name: 'Tobin\'s Mill',
    type: 'home',
    description: 'Wren\'s family mill and home base.',
    act: 'Act I',
    position: { x: -290.66, y: 35.18, z: 126.05 }
  },
  {
    id: 'traveler_arrival_spot',
    name: 'Traveler Arrival Spot',
    type: 'npc',
    description: 'Generic traveler/refugee entry point.',
    act: 'Act III',
    position: { x: -212.26, y: 32.65, z: 67.01 }
  },
  {
    id: 'upstream_clearcut',
    name: 'Upstream Clear-cut',
    type: 'endgame',
    description: 'Aldric logging evidence.',
    act: 'Act IV',
    position: { x: -60.71, y: 35.76, z: 227.69 }
  },
  {
    id: 'village_square',
    name: 'Village Square',
    type: 'hub',
    description: 'The social hub around Hollowfen\'s central buildings.',
    act: 'Act I',
    position: { x: -278.56, y: 35.17, z: 107.34 }
  },
  {
    id: 'village_well',
    name: 'Village Well',
    type: 'landmark',
    description: 'The well landmark and future restoration object.',
    act: 'Act I',
    position: { x: -286.31, y: 37.30, z: 159.32 }
  },
  {
    id: 'voss_tax_table',
    name: 'Voss Tax Table',
    type: 'interior',
    description: 'Tax scene position inside/near inn.',
    act: 'Act II',
    position: { x: -268.02, y: 39.04, z: 112.38 }
  },
  {
    id: 'wendlight_cluster',
    name: 'Wendlight Cluster',
    type: 'forage',
    description: 'Old river-course clue.',
    act: 'Act III',
    position: { x: -71.39, y: 29.47, z: 294.36 }
  },
  {
    id: 'wenmar_cottage',
    name: 'Wenmar Cottage',
    type: 'cottage',
    description: 'First tax consequence cottage.',
    act: 'Act II',
    position: { x: -333.60, y: 35.18, z: 131.92 }
  },
  {
    id: 'witch_cottage',
    name: 'Witch\'s Cottage',
    type: 'lore',
    description: 'Sable\'s cottage in the Deep Wood.',
    act: 'Act III',
    position: { x: -231.09, y: 33.89, z: 309.79 }
  },
  {
    id: 'witchwell_spring',
    name: 'Witchwell Spring',
    type: 'lore',
    description: 'Spring behind the Witch\'s Cottage.',
    act: 'Act III',
    position: { x: -237.05, y: 31.44, z: 327.91 }
  }
];

export function mergeLocationPins(savedPins = []) {
  const byId = new Map(HOLLOWFEN_CANON_LOCATIONS.map((pin) => [pin.id, { ...pin, source: 'canon' }]));
  for (const pin of savedPins) {
    const canon = byId.get(pin.id);
    if (canon && samePosition(canon.position, pin.position)) continue;
    byId.set(pin.id, { ...pin, source: 'local' });
  }
  return Array.from(byId.values()).sort((a, b) => a.name.localeCompare(b.name));
}

function samePosition(a, b) {
  if (!a || !b) return false;
  return Math.abs(a.x - b.x) < 0.01
    && Math.abs(a.y - b.y) < 0.01
    && Math.abs(a.z - b.z) < 0.01;
}
