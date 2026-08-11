# Darkfall Depths — visual chapters

The run has no hard final depth. A boss arena appears every 10 levels. Defeating its guardian and continuing starts the next visual chapter.

| Depths | Biome | Visual identity |
|---|---|---|
| 1–10 | Ashen Catacombs | worn neutral stone, ash, warm fire |
| 11–20 | Ember Vaults | scorched basalt, iron ribs, ember fire |
| 21–30 | Drowned Crypt | wet blue-grey slate, verdigris, cold witchlight |
| 31–40 | Charnel Gardens | roots, mineral remains, moss and sickly green light |
| 41–50 | Obsidian Sanctum | ritual obsidian, dark metal, muted violet flame |

## Signature inhabitants and decor

Each chapter keeps the shared underground enemy pool and adds a local inhabitant with a 42% selection weight whenever that biome is active:

- Ashen Catacombs — Ash Warden.
- Ember Vaults — Ember Revenant.
- Drowned Crypt — Drowned Sentinel.
- Charnel Gardens — Spore Stalker.
- Obsidian Sanctum — Obsidian Acolyte.

The four late-game chapters each own a 12-prop atlas: lantern, altar, urn, crystal, rack, container, conduit, statue, basin, post, obelisk and rubble. Ashen Catacombs retain the original 12-prop dungeon set. Pools use both clutter and structural placements, and biome lantern/crystal slots register matching dynamic light sources.

After depth 50 the five visual chapters repeat while gameplay scaling continues. Boss definitions currently cycle through the three catalogued bosses.

## Extension contract

Biome selection for layout lives in `DungeonLayoutStrategies.ForDepth`; visual selection lives in
`DungeonVisualProfile.ForDepth`. A layout strategy emits the logical plan before shared repair,
set-piece, tile, population and validation passes. Ashen Catacombs already use their own branching
crypt strategy; later chapters remain on the compatible room/corridor strategy until their roadmap
stage is reached.

Each visual profile owns its floor and wall resource paths, palette, contact shadow, fire colour,
decor tint, decor pools, light spacing and decor density. `DungeonView` consumes the profile without
owning macro layout decisions.

Authored room-scale content is emitted by `DungeonSetPieceFitter`; local 3×3/5×5 substitutions are
emitted by `DungeonMiniSetMatcher`. Both reserve semantic masks before population. Runtime portal,
chests and elite placement consume those records instead of reconstructing intent from decor indices.

New floor and wall textures belong under `Assets/Darkfall/Resources/Textures/Biomes`. Keep them square, tileable, neutral-lit and free of props or baked directional shadows. New standalone decor belongs under `Assets/Darkfall/Resources/Sprites/Environment/Props`; add its index to the corresponding profile pools. Blocking props must remain away from start and exit cells.

`DarkfallBuildTools.Validate Project` verifies that every registered biome texture exists and that all five chapters resolve to different profiles.
