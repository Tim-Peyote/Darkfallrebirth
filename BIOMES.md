# Darkfall Depths — visual chapters

The run has no hard final depth. A boss arena appears every 10 levels. Defeating its guardian and continuing starts the next visual chapter.

| Depths | Biome | Visual identity |
|---|---|---|
| 1–10 | Ashen Catacombs | worn neutral stone, ash, warm fire |
| 11–20 | Ember Vaults | scorched basalt, iron ribs, ember fire |
| 21–30 | Drowned Crypt | wet blue-grey slate, verdigris, cold witchlight |
| 31–40 | Charnel Gardens | roots, mineral remains, moss and sickly green light |
| 41–50 | Obsidian Sanctum | ritual obsidian, dark metal, muted violet flame |

After depth 50 the five visual chapters repeat while gameplay scaling continues. Boss definitions currently cycle through the three catalogued bosses.

## Extension contract

Biome selection lives in `DungeonVisualProfile.ForDepth`. Each profile owns its floor and wall resource paths, palette, contact shadow, fire colour, decor tint, decor pools, light spacing and decor density. `DungeonView` consumes the profile without biome-specific branches.

New floor and wall textures belong under `Assets/Darkfall/Resources/Textures/Biomes`. Keep them square, tileable, neutral-lit and free of props or baked directional shadows. New standalone decor belongs under `Assets/Darkfall/Resources/Sprites/Environment/Props`; add its index to the corresponding profile pools. Blocking props must remain away from start and exit cells.

`DarkfallBuildTools.Validate Project` verifies that every registered biome texture exists and that all five chapters resolve to different profiles.
