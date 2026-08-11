# Darkfall Depths — project baseline

Baseline date: 2026-08-11.

This document records reproducible project facts before the staged roadmap work. It is not a
feature wish list; planned work belongs in `DEVELOPMENT_ROADMAP.md`.

## Runtime and build

- Unity: 6000.1.4f1.
- Render pipeline: Universal Render Pipeline 17.1.0 with 2D lighting support.
- UI: Unity UI 2.0.0.
- Main and only enabled gameplay scene: `Assets/Scenes/Main.unity`.
- Desktop reference resolution: 1920×1080, fullscreen window.
- Main `CanvasScaler` reference resolution: 1920×1080.
- Supported visual-QA matrix: 1280×720, 1920×1080, 1920×1200, 2560×1080 and 3440×1440.
- Target standalone builders currently exist for macOS and Windows 64-bit.

## Runtime ownership

| Area | Primary source |
| --- | --- |
| Session, depth and scene bootstrap | `Assets/Darkfall/Scripts/Core/GameManager.cs` |
| Balance and definitions | `Assets/Darkfall/Scripts/Core/GameDefinitions.cs` |
| Legacy content catalog | `Assets/Darkfall/Resources/Data/legacy-catalog.json` |
| Dungeon topology and semantics | `Assets/Darkfall/Scripts/World/DungeonGenerator.cs` |
| Dungeon state, collision and visibility | `Assets/Darkfall/Scripts/World/DungeonData.cs` |
| Dungeon rendering and decor | `Assets/Darkfall/Scripts/World/DungeonView.cs` |
| Biome visual recipe | `Assets/Darkfall/Scripts/World/DungeonVisualProfile.cs` |
| Lighting and fog | `DungeonLighting.cs`, `FogOfWarView.cs` |
| Player and enemy runtime | `PlayerController.cs`, `EnemyController.cs` |
| Runtime UI root | `Assets/Darkfall/Scripts/UI/RuntimeUI.cs` |
| Inventory UI and interactions | `Assets/Darkfall/Scripts/UI/InventoryUI.cs` |
| Build and fast validation | `Assets/Darkfall/Scripts/Editor/DarkfallBuildTools.cs` |
| Exhaustive seed validation | `Assets/Darkfall/Scripts/Editor/DungeonSeedAudit.cs` |

## Confirmed content baseline

- Three playable heroes.
- Seventeen regular enemy definitions: twelve shared and five biome inhabitants.
- Three bosses, cycled by boss floors.
- Forty-six base item definitions, nine affixes and eight shop upgrades.
- Forty-two backpack cells, nine equipment cells and three quick slots.
- Five biome profiles, one chapter per ten depths; boss arena every tenth depth.
- Separate biome tracks 1–5, universal boss track and three alternating tavern tracks.
- One main scene; runtime UI and world are constructed from code.

## Reproducible seed suite

These seeds are permanent manual/visual regression cases. Do not replace a failing seed with a
more convenient one; fix the defect or add a documented exception.

| Purpose | Depth | Seed |
| --- | ---: | ---: |
| Early arrival and basic movement | 1 | 4242 |
| Late Ashen growth | 9 | 73009 |
| First boss arena | 10 | 1010 |
| Ember biome entry | 11 | 73011 |
| Drowned biome entry | 21 | 73021 |
| Charnel biome entry | 31 | 73031 |
| Obsidian biome entry | 41 | 73041 |
| Late progression before biome cycle | 49 | 73049 |
| Biome cycle restart | 51 | 73051 |

`Darkfall > Capture Biome Visual Audit` renders the five chapter entry depths into
`work/visual-audit`. The checked-in images form the current visual baseline, even where they expose
known defects.

## Validation commands

Fast editor validation:

```text
Darkfall > Validate Project
```

Exhaustive editor validation:

```text
Darkfall > Validation > Audit 5000 Dungeon Seeds
```

Headless exhaustive validation:

```bash
/Applications/Unity/Hub/Editor/6000.1.4f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath "/path/to/Unity" \
  -executeMethod Darkfall.Editor.DungeonSeedAudit.AuditDungeonSeedsBatch
```

The exhaustive report is written to `work/validation/dungeon-seed-audit.json`. It records seed,
depth, biome, failures and aggregate topology metrics. Generated reports are intentionally ignored
by Git.

The audit also projects all four screen-space movement directions into the logical isometric grid
and verifies that a free arrival room preserves the requested visual direction after collision
resolution.

## Known baseline risks

- All regular biomes still share one base room-and-corridor layout algorithm.
- Lighting has reported hard triangle, stripe and edge artefacts around occlusion boundaries.
- Directional character animation has reported gait, frame-size and grounding inconsistencies.
- Runtime UI requires a complete multi-resolution interaction and layout pass.
- Procedural decor and colliders require broad seed validation, especially at doors and corridors.
- A standalone player smoke test has not yet been recorded for the current workspace state.

## Baseline completion rule

Stage 0 is complete only after fast validation compiles and passes, the exhaustive audit has been
run, its result is recorded in the roadmap, and the visual reference suite has been reviewed.
