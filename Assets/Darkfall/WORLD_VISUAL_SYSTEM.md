# Darkfall Depths — world visual system

## Findings from the August visual audit

- The thick room border was not part of the wall texture. `DungeonView.BuildWallEdges` drew a
  continuous 0.12-cell opaque strip over every floor/wall boundary.
- Environment props and four fire frames were stored only inside shared atlases, making additions
  and biome replacement unnecessarily fragile.
- Player lighting combined a ray-clipped freeform light with a second shadowed point light. Their
  independent boundaries produced doubled seams and hard wedges while moving.
- Temporary gameplay effects had no remaining-time data for UI; slow, weakness, vulnerability and
  damage-over-time were therefore invisible in the HUD.
- Minimap and system buttons occupied the same top-right anchor region.

## Implemented architecture

### Biome profiles

`DungeonVisualProfile.ForDepth` chooses a chapter profile every ten depths. A profile owns:

- floor and wall resource paths;
- floor/wall/contact-shadow palette;
- light colour and placement cadence;
- structural and clutter prop sets.

Current chapters are Ashen Catacombs (1–10), Ember Vaults (11–20) and Drowned Crypt (21–30),
then the sequence repeats. Replacing a texture path or prop list changes the biome without touching
generation, collision or gameplay.

### Modular decoration

- `Resources/Sprites/Environment/Props/prop-0..11.png` are independent prop assets.
- `Resources/Sprites/Environment/Flames/flame-0..3.png` are independent animation frames.
- Generated hierarchy is split into `Structural`, `Light Sources` and `Clutter` groups.
- Medium rooms receive deterministic secondary clutter; very large rooms may receive an additional
  wall accent. Placement is derived from room coordinates, so the same seed remains visually stable.
- Blocking props register obstacles and shadow casters; light props register lights independently.
- Props inside the protected start/exit radius are forced non-blocking so decoration cannot break a run.
- The original atlases remain only as compatibility fallbacks.

### Exit portal

- A single portal is created at the farthest room when the level is built, initially sealed.
- Clearing the enemy budget unlocks it; a fallback runtime check also handles non-standard enemy removal.
- The unlocked portal receives emissive ground glow, stronger native 2D light and a cross-shaped minimap marker.
- Close interaction uses an explicit `[E]` prompt. The sealed prompt reports the remaining enemy count.

### Lighting

- Static flames use native shadowed Point Light 2D sources; no concave ray polygon is generated.
- Player peripheral light is a convex shifted ellipse instead of a triangular or ray-clipped cone.
- Walls and blocking props clip both systems through `ShadowCaster2D` with soft engine falloff.
- The local warm core is intentionally small and shadowless; it cannot produce a second wall seam.
- A very low global floor prevents crushed-black texture detail without revealing unexplored rooms.
- Fog visibility and the minimap use the same facing radius and line-of-sight rules.

### Runtime status visibility

`PlayerController.GetStatusSnapshots` exposes positive/negative state, label and remaining time.
The HUD shows up to four timed effects in a top-centre container and hides the container when empty.
Inventory displays final health, damage, defence, critical chance, fire resistance and ice resistance.

## Adding a biome

1. Add floor/wall textures under `Resources/Textures`.
2. Add individual prop PNGs under a biome-specific Resources folder.
3. Add a `DungeonVisualProfile` recipe with texture paths, palettes and prop indices.
4. Extend `ForDepth` chapter selection.
5. Run `Darkfall/Validate Project` and the visual audit at depths 1, 11 and 21.

## Recommended next visual upgrades

1. Author dedicated floor/wall texture sets for Ember Vaults and Drowned Crypt; profiles currently
   reuse the base masonry with different grading.
2. Add wall-facing variants, corner caps and doorway trims as independent sprites.
3. Replace rectangular obstacle shadow casters with sprite-silhouette physics shapes for large props.
4. Add status icons and an expandable effect inspector for runs with more than three simultaneous effects.
5. Add a biome preview scene and automated screenshots at 16:9, 16:10 and ultrawide resolutions.
