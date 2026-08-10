# Darkfall dungeon art grammar

The generator owns topology and semantics. Biome art only supplies compatible visual modules.
This keeps every chapter on one set of architectural rules while allowing a different material,
silhouette and narrative language.

## Order of generation

1. Carve connected rooms and corridors.
2. Assign room purpose from geometry and chapter rules.
3. Place structural thresholds: walls, real passages, doors and elevation stairs.
4. Reserve walkable combat and transition lanes.
5. Fit one coherent room miniset into the remaining region.
6. Generate connected hazard fields and validate their crossings.
7. Dress the miniset with subordinate props, lights, decals and ambient effects.

This follows the useful part of Diablo's theme-room model: an object is only selected after its
required footprint, region and solid-tile constraints pass. Decorative randomness never decides
whether a door, stair or traversable route exists.

## Room purposes

- Arrival: small protected room, one real unlocked door, biome vigil and offering. Not on boss floors.
- Shrine: wall-backed icon, offerings, candles or equivalent biome lights.
- Reliquary: guarded central object with open circulation on both diagonals.
- Ossuary: bones/tombs arranged as bays, never scattered through the centre lane.
- Armory: wall racks and one broken equipment cluster.
- Ritual: radial or axial composition with a deliberately readable centre.
- Cistern: drowned-crypt exclusive reservoir, drains and brine channels.
- Forge: ember-vault exclusive furnace, slag gutter and lava feeder.
- Garden: charnel-garden exclusive fungal bed, bile pool and bone trellis.
- Observatory: obsidian-sanctum exclusive lens, void fissure and rune axis.

## Hazard topology

Every hazard cell stores a four-neighbour mask. Art files are separate PNG modules, not a runtime
slice of one opaque atlas.

- `isolated`: no neighbours; small puddle, crack or vent.
- `end-n/e/s/w`: one neighbour.
- `straight-ns/ew`: two opposite neighbours.
- `bend-ne/nw/se/sw`: two adjacent neighbours.
- `tee-*`: three neighbours.
- `body`: four neighbours.
- `bank-*`: dry border module selected from the inverse mask.
- `bridge-ns/ew`: explicit safe crossing reserved by topology.
- `source` and `mouth`: rare landmark variants, never substituted for an arbitrary body tile.

Gameplay is separate from the sprite: a pool may slow, poison, burn, pulse, erupt or remain purely
decorative. Visual connectivity therefore cannot silently change collision or damage.

## Biome matrix

| Biome | Hazard families | Room signatures | Material language |
| --- | --- | --- | --- |
| Ashen Catacombs | ember seep, blood channel, ash vent | ossuary, reliquary, funeral shrine | worn limestone, soot, iron, red wax |
| Ember Vaults | lava river, slag pool, flame jet | forge, sacrifice furnace, armour vault | basalt, brass, orange heat cracks |
| Drowned Crypt | brine channel, flooded pool, gas vent | cistern, drowned chapel, embalming room | wet green stone, copper, pale corpse light |
| Charnel Gardens | bile/acid pool, spore bed, grasping root | garden, vivarium, bone orchard | porous bone, roots, fungus, sickly amber |
| Obsidian Sanctum | void fissure, rune field, crystal discharge | observatory, ritual court, forbidden archive | black glass, silver gothic metal, violet light |

## Art constraints

- Match the current isometric projection and hero scale before detail painting.
- Anchor every module to its physical contact line; no baked broad black drop shadow.
- Use contact occlusion only at the plinth, with a narrow tinted value rather than pure black.
- Keep silhouettes chunky and theatrical: miniature-set architecture, stop-motion fantasy props,
  restrained late-1990s pre-rendered texture noise.
- Reserve the brightest values for interaction, fire, poison and magic; walls remain readable but
  do not compete with actors.
- Each new set must include both wall-axis orientations and every required corner/threshold state.

## Required validation

- All five biome profiles use the same room/hazard semantics.
- Hazard neighbour masks match generated adjacency.
- No hazard occupies arrival, exit, door or stair landing cells.
- At least one safe route remains between all room centres.
- A bridge is the only dry crossing through a river that spans a complete room axis.
- Wall plinths touch floor; raised-floor fascia is visible only on screen-facing sides.
- Multi-seed screenshots cover all room purposes and every hazard mask before a set is accepted.
