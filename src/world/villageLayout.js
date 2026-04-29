// Hollowfen village layout — designed for actual gameplay flow.
//
// Story flow (from STORY_BIBLE.md):
//   1. Wren returns at dusk from the south road. Her first sight is the
//      MILL on her left — childhood home, where her grandmother raised her.
//   2. She continues north into the VILLAGE SQUARE, anchored by the WELL
//      (plot-relevant — "the well cap is rotten"). Market stalls line the
//      south side; the Crooked Pintle Inn faces the square from the east.
//   3. The SMITHY (Joren) sits east on the road, where tool upgrades happen.
//   4. The CHAPEL (Father Calden) stands at the north edge by the woods.
//      Act III unlocks the chapel garden and the path through to Edge Woods.
//   5. Cottages cluster in three areas — west lane (Mill side), north field
//      (around the chapel path), south road (entrance) — many boarded.
//
// Coordinate system: +X east, +Z south, -Z north, Y up. rotY in radians.
//
// Spatial scale: ~150m N-S × ~140m E-W. Wren walks ~70m north from spawn
// to reach the well, passing the Mill on her left at ~30m in. From the
// square, Inn is ~25m east, Smithy ~45m east, Chapel ~55m north.

const buildings = [
  // ============ SOUTH ROAD APPROACH (Wren's first sight as she walks in) ============
  // The Mill — Wren's childhood home — sits west of the road, broadside so
  // she sees its full timber frame and water-wheel side as she approaches.
  { id: 'mill', asset: 'BasicBuilding5_4.glb',
    x: -28, z: 30, rotY: 1.55,
    role: 'mill', enterable: true,
    doorway: { side: '+x', center: 0, width: 1.7 },
    interior: 'mill_interior' },

  // A boarded cottage opposite the mill — first signal of the "failing" tone
  { id: 'boarded_a', asset: 'BasicBuilding1_3.glb', x: 22, z: 36, rotY: -2.1, role: 'cottage' },

  // Two more cottages further south along the road in (mostly empty)
  { id: 'boarded_b', asset: 'BasicBuilding1_6.glb', x: -32, z: 56, rotY:  0.7, role: 'cottage' },
  { id: 'boarded_c', asset: 'BasicBuilding1_2.glb', x:  30, z: 58, rotY: -0.6, role: 'cottage' },

  // ============ VILLAGE SQUARE — the heart of Hollowfen ============
  // Two merchant/storage buildings flank the square, framing the well.
  // ExteriorBuilding1 has a long balcony — good for the south-east corner
  // facing the road in.
  { id: 'merchant_se', asset: 'ExteriorBuilding1.glb',   x:  18, z: 12, rotY: -1.4, role: 'building' },
  { id: 'merchant_sw', asset: 'ExteriorBuilding3.glb',   x: -16, z: 14, rotY:  1.6, role: 'building' },

  // ============ INN DISTRICT — east of the square ============
  // The Crooked Pintle Inn (Old Bram). Door faces west onto the square so
  // Wren sees the entrance, the hanging sign, the lanterns on approach.
  { id: 'inn', asset: 'ExteriorBuilding_Tavern2_1.glb',
    x: 38, z: -4, rotY: -1.55,
    role: 'inn', enterable: true,
    doorway: { side: '-x', center: 0, width: 1.8 },
    interior: 'inn_interior' },

  // Second tavern (Tavern1_1) further east along the inn row — out-of-business,
  // signals the failing village. Faces same direction as the inn.
  { id: 'old_tavern', asset: 'ExteriorBuilding_Tavern1_1.glb',
    x: 56, z: -8, rotY: -1.55, role: 'building' },

  // A large merchant house north of the inn (stables / warehouse feel)
  { id: 'inn_warehouse', asset: 'ExteriorBuilding5.glb',
    x: 50, z: 14, rotY: -1.55, role: 'building' },

  // ============ SMITHY YARD — east of inn district, on the road bend ============
  // Joren's smithy. Door faces west toward the village.
  { id: 'smithy', asset: 'BasicBuilding2_1.glb',
    x: 60, z: 30, rotY: -1.55,
    role: 'smithy', enterable: true,
    doorway: { side: '-x', center: 0, width: 1.6 },
    interior: 'smithy_interior' },

  // A storage shed for the smithy
  { id: 'smithy_shed', asset: 'Building1c.glb', x: 70, z: 26, rotY: -1.55 },

  // ============ CHAPEL GROUNDS — north edge, path leads to Edge Woods ============
  // Father Calden's chapel. Door faces south toward the village square.
  { id: 'chapel', asset: 'BasicBuilding3_2.glb',
    x: -10, z: -54, rotY: 0,
    role: 'chapel', enterable: true,
    doorway: { side: '-z', center: 0, width: 1.7 },
    interior: 'chapel_interior' },

  // Calden's small living quarters next to the chapel (sleeping shed)
  { id: 'chapel_quarters', asset: 'Building1d.glb', x: -22, z: -52, rotY: 1.55 },

  // ============ WATCHTOWER — far NE landmark, suggests former importance ============
  { id: 'watchtower', asset: 'TowerGuard.glb', x: 50, z: -60, rotY: -0.6, role: 'tower' },

  // ============ COTTAGE LANE — WEST (working families' homes) ============
  // North-south row west of the square. These are LIVED-IN (Wenmar family,
  // Edda's grandfather, etc.). Variety of BasicBuilding1 styles.
  { id: 'cottage_w1', asset: 'BasicBuilding1_5.glb', x: -50, z:  10, rotY:  1.55, role: 'cottage' },
  { id: 'cottage_w2', asset: 'BasicBuilding1_7.glb', x: -52, z: -10, rotY:  1.6,  role: 'cottage' },
  { id: 'cottage_w3', asset: 'BasicBuilding1_4.glb', x: -50, z: -28, rotY:  1.5,  role: 'cottage' },
  { id: 'cottage_w4', asset: 'BasicBuilding2_3.glb', x: -34, z:   2, rotY:  0.0,  role: 'cottage' },
  // Edda's grandfather's cottage — slightly larger, set back
  { id: 'cottage_edda', asset: 'BasicBuilding2_5.glb', x: -36, z: -20, rotY:  0.2, role: 'cottage' },

  // ============ COTTAGE CLUSTER — NORTH (along the chapel path) ============
  // Cottages between the square and the chapel — a winding path passes them
  { id: 'cottage_n1', asset: 'BasicBuilding1_1.glb', x: -32, z: -36, rotY:  0.3,  role: 'cottage' },
  { id: 'cottage_n2', asset: 'BasicBuilding3_4.glb', x:  10, z: -32, rotY: -0.4,  role: 'cottage' },
  { id: 'cottage_n3', asset: 'BasicBuilding3_3.glb', x:  22, z: -42, rotY: -0.7,  role: 'cottage' },
  { id: 'cottage_n4', asset: 'BasicBuilding4_3.glb', x: -22, z: -42, rotY:  0.6,  role: 'cottage' },

  // ============ COTTAGE CLUSTER — EAST (behind the inn) ============
  { id: 'cottage_e1', asset: 'BasicBuilding6_2.glb', x:  56, z: 30, rotY: -1.55, role: 'cottage' },
  { id: 'cottage_e2', asset: 'BasicBuilding6_4.glb', x:  62, z: 50, rotY: -1.6,  role: 'cottage' },
  { id: 'cottage_e3', asset: 'BasicBuilding4_1.glb', x:  46, z: 50, rotY:  0.2,  role: 'cottage' },

  // ============ FAR-WEST OUTBUILDINGS (mill paddock, animal sheds) ============
  { id: 'mill_paddock_a', asset: 'Building1a.glb', x: -42, z: 28, rotY: 1.55 },
  { id: 'mill_paddock_b', asset: 'Building2a.glb', x: -16, z: 38, rotY: 0 },

  // ============ FAR-NORTH OUTBUILDING (storage near chapel) ============
  { id: 'chapel_storage', asset: 'Building2b.glb', x:  6, z: -50, rotY:  0.0  }
];

const props = [
  // ============ VILLAGE SQUARE — the heart ============
  // Well dead center. WellSmoke variant has subtle smoke for "lived-in" feel.
  { id: 'well',          asset: 'WellSmoke.glb',     x:  0, z:  0, rotY: 0 },
  // A warming brazier next to the well — gathering point on cold mornings
  { id: 'brazier_well',  asset: 'Brazier.glb',       x:  4, z:  3, rotY: 0 },
  // Hanging village flag/banner on the merchant building
  { id: 'flag_square',   asset: 'HangingFlag.glb',   x: 18, z: 10, rotY: -1.55, yOffset: 5 },

  // ============ MARKET ROW — south of the well, where Wren sells mushrooms ============
  // Five stalls in a curving line, facing north toward the well
  { id: 'stall_1', asset: 'MarketStall01.glb',  x: -12, z: 8, rotY: 0 },
  { id: 'stall_2', asset: 'MarketStall02.glb',  x:  -6, z: 8, rotY: 0 },
  { id: 'stall_3', asset: 'MarketStall03.glb',  x:   0, z: 9, rotY: 0 },
  { id: 'stall_4', asset: 'MarketStall02b.glb', x:   6, z: 8, rotY: 0 },
  { id: 'stall_5', asset: 'MarketStall03b.glb', x:  12, z: 8, rotY: 0 },
  // Goods displayed in front of the stalls
  { id: 'apples_a', asset: 'ApplePile.glb',          x: -11, z: 6.5, rotY: 0 },
  { id: 'apples_b', asset: 'ApplePile.glb',          x:  -5, z: 6.5, rotY: 0 },
  { id: 'fcrate_a', asset: 'FoodCrate_Apple.glb',    x:  -6, z:  9.5, rotY: 0 },
  { id: 'fish_pile', asset: 'FishPile.glb',          x:   0, z: 6.5, rotY: 0 },
  { id: 'fcrate_f', asset: 'FoodCrate_Fish.glb',     x:   0, z:  9.5, rotY: 0 },
  { id: 'fcrate_g', asset: 'FoodCrate_Garlic.glb',   x:   6, z:  9.5, rotY: 0 },
  { id: 'chain_fish', asset: 'ChainFish.glb',        x:   0, z: 7.5, rotY: 0, yOffset: 2.0 },
  // A merchant's cart parked beside the stalls
  { id: 'cart_market', asset: 'Cart_Cargo_1.glb', x: 18, z: 5, rotY: 0.7 },

  // ============ MILL YARD — west of the road, Wren's home ============
  // Sign hangs above mill door, visible from road
  { id: 'sign_mill',  asset: 'HangingSign.glb',   x: -25, z: 30, rotY: 1.57, yOffset: 3.4 },
  { id: 'lantern_mill', asset: 'WallCandleB_Flame.glb', x: -25, z: 28, rotY: 1.57, yOffset: 2.4 },
  // Hay piles + farm tools — implies grain milling, lived-in
  { id: 'mill_hay_a',  asset: 'HayPile1.glb',  x: -36, z: 28, rotY: 0 },
  { id: 'mill_hay_b',  asset: 'HayPile2.glb',  x: -38, z: 30, rotY: 0.5 },
  { id: 'mill_haybale_a', asset: 'Haybale.glb', x: -34, z: 32, rotY: 0.3 },
  { id: 'mill_haybale_b', asset: 'Haybale.glb', x: -32, z: 32, rotY: 0.3 },
  { id: 'mill_cart',   asset: 'Cart1.glb',    x: -22, z: 38, rotY: 1.1 },
  { id: 'mill_sacks',  asset: 'SackPileA.glb', x: -25, z: 36, rotY: 0 },
  { id: 'mill_sack_a', asset: 'SackFullA.glb', x: -23, z: 35, rotY: 0 },
  { id: 'mill_sack_b', asset: 'SackFullB.glb', x: -22, z: 33, rotY: 0.3 },
  { id: 'mill_firewood', asset: 'FirewoodSingle.glb', x: -27, z: 34, rotY: 0 },
  { id: 'mill_pitchfork', asset: 'PitchFork.glb', x: -38, z: 32, rotY: 1.4 },
  { id: 'mill_hayfork',   asset: 'HayFork.glb',   x: -39, z: 30, rotY: 1.0 },
  { id: 'mill_rake',      asset: 'Rake.glb',      x: -26, z: 36, rotY: 1.2 },
  { id: 'mill_ladder',    asset: 'Ladder.glb',    x: -23, z: 31, rotY: 0 },
  // Mill paddock fence (defines the yard)
  { id: 'fence_mill_a', asset: 'FenceWood1.glb', x: -16, z: 30, rotY: 0 },
  { id: 'fence_mill_b', asset: 'FenceWood1.glb', x: -19, z: 30, rotY: 0 },
  { id: 'fence_mill_c', asset: 'FenceWood2.glb', x: -22, z: 30, rotY: 0 },
  { id: 'fence_mill_d', asset: 'Fence02.glb',    x: -42, z: 32, rotY: 0 },
  { id: 'fence_mill_e', asset: 'Fence02.glb',    x: -45, z: 32, rotY: 0 },
  { id: 'fence_mill_f', asset: 'Fencepost.glb',  x: -16, z: 35, rotY: 0 },

  // ============ INN DISTRICT — east of square ============
  { id: 'sign_inn',     asset: 'HangingSign.glb',     x: 35, z: -4, rotY: 0,    yOffset: 3.4 },
  { id: 'lantern_inn_l', asset: 'WallCandle_Flame.glb', x: 35, z: -1, rotY: -1.57, yOffset: 2.4 },
  { id: 'lantern_inn_r', asset: 'WallCandle_Flame.glb', x: 35, z: -7, rotY: -1.57, yOffset: 2.4 },
  // Cart parked outside the inn (a traveler's wagon)
  { id: 'inn_cart',     asset: 'Cart2.glb',         x: 30, z:  3, rotY:  0.6 },
  { id: 'inn_barrel_rack', asset: 'BarrelRack.glb', x: 32, z: -8, rotY:  0   },
  { id: 'inn_barrel_a', asset: 'Barrel.glb',        x: 30, z: -8, rotY:  0 },
  { id: 'inn_barrel_b', asset: 'Barrel.glb',        x: 30, z: -10, rotY: 0 },
  { id: 'inn_crate',    asset: 'CratePile1.glb',    x: 33, z: -10, rotY: 0.5 },
  { id: 'inn_chest',    asset: 'Chest.glb',         x: 32, z: -2,  rotY: 0 },
  // The closed second tavern — empty cart and abandoned barrels signal it's shut
  { id: 'old_tavern_sign', asset: 'SignHanging.glb', x: 53, z: -8, rotY: 0,    yOffset: 3.0 },
  { id: 'old_tavern_cart', asset: 'Cart3.glb',       x: 60, z: -2, rotY: 0.4 },
  { id: 'old_tavern_barrel', asset: 'Barrel.glb',    x: 58, z: -10, rotY: 0 },

  // ============ SMITHY YARD — east, on the road ============
  { id: 'sign_smithy',   asset: 'SignHanging.glb', x: 57, z: 30, rotY: 0,    yOffset: 3.0 },
  { id: 'lantern_smithy', asset: 'WallCandleB_Flame.glb', x: 57, z: 32, rotY: -1.57, yOffset: 2.4 },
  // Forge fire outside (where Joren works hot iron)
  { id: 'smithy_forge',  asset: 'HearthFire.glb',  x: 54, z: 32, rotY: 0 },
  { id: 'smithy_anvil_a', asset: 'Crate1.glb',     x: 54, z: 34, rotY: 0 },
  { id: 'smithy_firewood', asset: 'FirewoodSingle.glb', x: 56, z: 34, rotY: 0 },
  { id: 'smithy_firewood_b', asset: 'FirewoodBasket.glb', x: 58, z: 34, rotY: 0 },
  // Smithy tools
  { id: 'smithy_shovel', asset: 'Shovel.glb', x: 59, z: 32, rotY: 0.8 },
  { id: 'smithy_hoe',    asset: 'Hoe.glb',    x: 60, z: 33, rotY: 1.2 },
  { id: 'smithy_barrels', asset: 'BarrelRack.glb', x: 64, z: 32, rotY: 1.55 },
  { id: 'smithy_sack',   asset: 'SackTiedA.glb', x: 64, z: 30, rotY: 0.4 },
  // Smithy work yard fence
  { id: 'fence_smithy_a', asset: 'FenceRopes.glb', x: 52, z: 36, rotY: 0 },
  { id: 'fence_smithy_b', asset: 'FenceRopes.glb', x: 55, z: 36, rotY: 0 },
  { id: 'fence_smithy_c', asset: 'FenceRopes.glb', x: 58, z: 36, rotY: 0 },

  // ============ CHAPEL GROUNDS — north, where Wren exits to Edge Woods ============
  // Two large candle posts flank the chapel entrance
  { id: 'chapel_candle_l', asset: 'CandleLarge_Flame.glb', x: -12, z: -50, rotY: 0 },
  { id: 'chapel_candle_r', asset: 'CandleLarge_Flame.glb', x:  -8, z: -50, rotY: 0 },
  // Wall lanterns on the chapel facade
  { id: 'chapel_lantern_l', asset: 'WallCandle_Flame.glb', x: -12, z: -52, rotY: 0, yOffset: 2.4 },
  { id: 'chapel_lantern_r', asset: 'WallCandle_Flame.glb', x:  -8, z: -52, rotY: 0, yOffset: 2.4 },
  // Chapel garden fence — locked in Act I-II per story
  { id: 'fence_chapel_a', asset: 'Fence05.glb', x: -16, z: -48, rotY: 1.57 },
  { id: 'fence_chapel_b', asset: 'Fence05.glb', x: -16, z: -45, rotY: 1.57 },
  { id: 'fence_chapel_c', asset: 'Fence06.glb', x:  -4, z: -48, rotY: 1.57 },
  { id: 'fence_chapel_d', asset: 'Fence06.glb', x:  -4, z: -45, rotY: 1.57 },
  { id: 'fence_chapel_e', asset: 'Fence05.glb', x: -10, z: -42, rotY: 0 },
  { id: 'fence_chapel_f', asset: 'Fence05.glb', x:  -7, z: -42, rotY: 0 },
  { id: 'fence_chapel_g', asset: 'Fence06.glb', x: -13, z: -42, rotY: 0 },

  // ============ WEST COTTAGE LANE — fences mark property lines ============
  // Fences between cottage_w1 and cottage_w2
  { id: 'fence_w1', asset: 'FenceWood1.glb', x: -50, z:  0, rotY: 0 },
  { id: 'fence_w2', asset: 'FenceWood1.glb', x: -47, z:  0, rotY: 0 },
  { id: 'fence_w3', asset: 'FenceWood2.glb', x: -50, z: -18, rotY: 0 },
  { id: 'fence_w4', asset: 'FenceWood2.glb', x: -47, z: -18, rotY: 0 },
  // Lived-in details: hay, sacks, firewood near cottages
  { id: 'cottage_w1_hay',     asset: 'Haybale.glb',         x: -45, z: 12, rotY: 0.3 },
  { id: 'cottage_w1_firewood', asset: 'FirewoodSingle.glb', x: -45, z:  8, rotY: 0   },
  { id: 'cottage_w2_sacks',   asset: 'SackFullC.glb',       x: -47, z: -8, rotY: 0.4 },
  { id: 'cottage_w2_barrel',  asset: 'Barrel.glb',          x: -47, z: -12, rotY: 0  },
  { id: 'cottage_w3_haybale', asset: 'Haybale.glb',         x: -45, z: -28, rotY: 0  },
  { id: 'cottage_w3_bucket',  asset: 'Bucket.glb',          x: -45, z: -25, rotY: 0  },
  { id: 'cottage_edda_hay',   asset: 'HayPile3.glb',        x: -32, z: -22, rotY: 0  },
  { id: 'cottage_edda_chair', asset: 'Chair.glb',           x: -32, z: -16, rotY: 0  },
  { id: 'cottage_edda_basket', asset: 'FirewoodBasket.glb', x: -34, z: -18, rotY: 0  },

  // ============ NORTH COTTAGE CLUSTER (along chapel path) ============
  { id: 'fence_n1', asset: 'Fence01.glb', x: -28, z: -32, rotY: 0 },
  { id: 'fence_n2', asset: 'Fence01.glb', x: -25, z: -32, rotY: 0 },
  { id: 'fence_n3', asset: 'Fence01.glb', x:  14, z: -32, rotY: 0 },
  { id: 'fence_n4', asset: 'Fence01.glb', x:  17, z: -32, rotY: 0 },
  // Lived-in detail at north cottages
  { id: 'cottage_n1_firewood', asset: 'FirewoodSingle.glb', x: -28, z: -38, rotY: 0 },
  { id: 'cottage_n2_haybale',  asset: 'Haybale.glb',        x:  14, z: -34, rotY: 0.3 },
  { id: 'cottage_n2_barrel',   asset: 'Barrel.glb',         x:  10, z: -36, rotY: 0 },

  // ============ SOUTH ROAD APPROACH details (Wren's first sight) ============
  // A barrel + sack at the boarded south cottage suggests it WAS lived-in
  { id: 'south_cottage_barrel', asset: 'Barrel.glb',     x:  26, z: 38, rotY: 0 },
  { id: 'south_cottage_sack',   asset: 'SackPileB.glb',  x:  24, z: 36, rotY: 0 },
  // Roadside firewood pile
  { id: 'road_firewood',        asset: 'FirewoodSingle.glb', x: -22, z: 56, rotY: 0 },
  { id: 'road_haypile',         asset: 'HayPile1.glb',   x:  22, z: 50, rotY: 0.4 },
  // Empty cart abandoned by the road
  { id: 'road_cart_abandoned',  asset: 'Cart3.glb',      x:  -4, z: 50, rotY: 0.7 },

  // ============ WATCHTOWER details (north-east) ============
  { id: 'tower_barrel_a',  asset: 'Barrel.glb',         x: 48, z: -56, rotY: 0 },
  { id: 'tower_crate',     asset: 'CrateEmpty.glb',     x: 50, z: -54, rotY: 0.4 },
  { id: 'tower_firewood',  asset: 'FirewoodSingle.glb', x: 46, z: -58, rotY: 0 },
  { id: 'tower_brazier',   asset: 'Brazier.glb',        x: 50, z: -62, rotY: 0 },

  // ============ MISCELLANEOUS DRESSING (atmosphere) ============
  // BBQ pit at the inn district — outdoor cooking
  { id: 'bbq_inn',         asset: 'BBQPit.glb',          x:  44, z:   2, rotY: 0 },
  // A few buckets and tools scattered around the square
  { id: 'square_bucket',   asset: 'Bucket.glb',          x:  -2, z:  -4, rotY: 0 },
  { id: 'square_broom',    asset: 'Broom.glb',           x:  -3, z:   2, rotY: 1.5 },
  // Empty crate at south-east merchant
  { id: 'merchant_se_crate', asset: 'Crate2.glb',        x:  18, z:  16, rotY: 0.4 },
  { id: 'merchant_se_sack',  asset: 'SackFullC.glb',     x:  20, z:  14, rotY: 0   },
  // Storage near south-west merchant
  { id: 'merchant_sw_chest', asset: 'ChestItems.glb',    x: -14, z:  16, rotY: 0   },
  { id: 'merchant_sw_crate', asset: 'CratePile1.glb',    x: -18, z:  18, rotY: 0.5 }
];

export const VILLAGE_LAYOUT = {
  // Wren spawns south of the village on the road in, facing north toward the
  // mill. Her first 30m of walking takes her past the mill on her left.
  spawn: { x: 0, z: 70, facing: 0 },
  buildings,
  props
};

// Interior layouts. Local space inside each building (before the building's rotY).
// localX is east-west in building space, localZ is north-south, localY is up.
// Items are decorative — collisions are handled by the building's wall colliders.
export const INTERIORS = {
  mill_interior: {
    items: [
      // Wren's bedroom corner
      { asset: 'SingleBed.glb',  localX:  1.6, localZ: -1.5, rotY: 0 },
      { asset: 'Dresser.glb',    localX: -1.8, localZ: -1.8, rotY: 0 },
      // Working area: table, chair, cooking pot by hearth
      { asset: 'TableA.glb',     localX: -1.0, localZ:  0.5, rotY: 0 },
      { asset: 'Chair.glb',      localX: -1.6, localZ:  0.5, rotY: 1.57 },
      { asset: 'HearthFire.glb', localX:  0.0, localZ: -2.4, rotY: 0 },
      { asset: 'CookingPot.glb', localX:  0.4, localZ: -2.0, rotY: 0 },
      // Storage shelf for grain
      { asset: 'Shelf.glb',      localX:  1.6, localZ:  1.5, rotY: 0 },
      { asset: 'SackFullA.glb',  localX:  1.4, localZ:  1.0, rotY: 0 },
      // Rug in the middle
      { asset: 'RugA.glb',       localX:  0.0, localZ:  0.0, rotY: 0 }
    ]
  },
  inn_interior: {
    items: [
      // Bar along one wall (Old Bram's spot)
      { asset: 'TavernBar.glb', localX: -1.5, localZ: -2.0, rotY: 0 },
      { asset: 'Barstool.glb',  localX: -2.0, localZ: -1.0, rotY: 0 },
      { asset: 'Barstool.glb',  localX: -1.0, localZ: -1.0, rotY: 0 },
      { asset: 'Barstool.glb',  localX:  0.0, localZ: -1.0, rotY: 0 },
      // Bar drinks
      { asset: 'Flagon.glb',    localX: -1.5, localZ: -1.6, rotY: 0, localY: 1.0 },
      { asset: 'Bottle.glb',    localX: -1.0, localZ: -1.6, rotY: 0, localY: 1.0 },
      { asset: 'Amphora.glb',   localX:  2.6, localZ: -1.5, rotY: 0 },
      // Tavern tables
      { asset: 'TableB.glb',    localX:  1.5, localZ:  0.5, rotY: 0 },
      { asset: 'Bench.glb',     localX:  1.5, localZ:  1.5, rotY: 0 },
      { asset: 'Bench.glb',     localX:  1.5, localZ: -0.5, rotY: 0 },
      { asset: 'TableC.glb',    localX: -1.5, localZ:  1.0, rotY: 0 },
      { asset: 'BowlStack.glb', localX: -1.5, localZ:  1.0, rotY: 0, localY: 0.9 },
      // Hearth + chandelier overhead
      { asset: 'HearthFire.glb',       localX:  2.5, localZ: -2.4, rotY: 0 },
      { asset: 'Chandelier_Flame.glb', localX:  0.5, localZ:  0.5, rotY: 0, localY: 2.6 },
      { asset: 'RugB.glb',             localX:  0.5, localZ:  0.5, rotY: 0 }
    ]
  },
  chapel_interior: {
    items: [
      // Pews facing the altar
      { asset: 'Bench.glb',            localX: -1.0, localZ: -1.0, rotY: 0 },
      { asset: 'Bench.glb',            localX:  1.0, localZ: -1.0, rotY: 0 },
      { asset: 'Bench.glb',            localX: -1.0, localZ:  0.5, rotY: 0 },
      { asset: 'Bench.glb',            localX:  1.0, localZ:  0.5, rotY: 0 },
      // Altar table at the back
      { asset: 'TableA.glb',           localX:  0.0, localZ:  2.0, rotY: 0 },
      { asset: 'Candleholder_Lit.glb', localX: -0.6, localZ:  2.0, rotY: 0 },
      { asset: 'Candleholder_Lit.glb', localX:  0.6, localZ:  2.0, rotY: 0 },
      { asset: 'BowlStack.glb',        localX:  0.0, localZ:  2.0, rotY: 0, localY: 0.9 }
    ]
  },
  smithy_interior: {
    items: [
      // Forge fire dominates the interior
      { asset: 'HearthFire.glb', localX:  1.5, localZ: -1.8, rotY: 0 },
      { asset: 'CookingPot.glb', localX:  1.5, localZ: -1.4, rotY: 0, localY: 0.8 },
      // Work table for cold work
      { asset: 'TableA.glb',     localX: -1.0, localZ:  0.5, rotY: 0 },
      // Material storage
      { asset: 'Barrel.glb',     localX: -2.0, localZ: -1.5, rotY: 0 },
      { asset: 'Crate1.glb',     localX: -1.8, localZ: -2.0, rotY: 0 },
      { asset: 'Shelf.glb',      localX:  2.0, localZ:  1.0, rotY: 0 }
    ]
  }
};
