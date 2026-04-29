import { buildChanterelle, CHANTERELLE_INFO } from './Chanterelle.js';
import { buildDeathCap, DEATH_CAP_INFO } from './DeathCap.js';
import { buildFlyAgaric, FLY_AGARIC_INFO } from './FlyAgaric.js';
import { buildPorcini, PORCINI_INFO } from './Porcini.js';
import { buildLibertyCap, LIBERTY_CAP_INFO } from './LibertyCap.js';
import { buildDestroyingAngel, DESTROYING_ANGEL_INFO } from './DestroyingAngel.js';
import { buildFieldMushroom, FIELD_MUSHROOM_INFO } from './FieldMushroom.js';
import { buildDeadlyGalerina, DEADLY_GALERINA_INFO } from './DeadlyGalerina.js';
import {
  FIELD_CAP_INFO, WOOD_EAR_INFO, PINECREST_INFO, GOLDFOOT_INFO,
  COPPERCUP_INFO, LACEWIG_INFO, BONEPALE_INFO, BRIGHTSPORE_INFO
} from './StoryMushrooms.js';

// Each mushroom species exports a procedural geometry builder and a field-guide info object.
// To add a species: create a new file, export both, register here.

export const MUSHROOM_BUILDERS = {
  chanterelle: buildChanterelle,
  deathCap: buildDeathCap,
  flyAgaric: buildFlyAgaric,
  porcini: buildPorcini,
  libertyCap: buildLibertyCap,
  destroyingAngel: buildDestroyingAngel,
  fieldMushroom: buildFieldMushroom,
  deadlyGalerina: buildDeadlyGalerina,
  fieldCap: buildFieldMushroom,
  woodEar: buildPorcini,
  pinecrest: buildDeadlyGalerina,
  goldfoot: buildChanterelle,
  coppercup: buildFlyAgaric,
  lacewig: buildPorcini,
  bonepale: buildDestroyingAngel,
  brightspore: buildChanterelle
};

export const MUSHROOM_INFO = {
  chanterelle: CHANTERELLE_INFO,
  deathCap: DEATH_CAP_INFO,
  flyAgaric: FLY_AGARIC_INFO,
  porcini: PORCINI_INFO,
  libertyCap: LIBERTY_CAP_INFO,
  destroyingAngel: DESTROYING_ANGEL_INFO,
  fieldMushroom: FIELD_MUSHROOM_INFO,
  deadlyGalerina: DEADLY_GALERINA_INFO,
  fieldCap: FIELD_CAP_INFO,
  woodEar: WOOD_EAR_INFO,
  pinecrest: PINECREST_INFO,
  goldfoot: GOLDFOOT_INFO,
  coppercup: COPPERCUP_INFO,
  lacewig: LACEWIG_INFO,
  bonepale: BONEPALE_INFO,
  brightspore: BRIGHTSPORE_INFO
};
