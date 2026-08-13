# Modular architecture generation contract

Generate a production-ready 2D isometric modular architecture sprite sheet for a Unity dark-fantasy dungeon game. Use the matching project biome decor atlas as the authoritative reference for palette, pixel-art rendering, material language, scale, lighting direction, and detail density. The mood may evoke the architectural richness of classic isometric gothic action RPGs, but every asset must be original.

Output one `4 x 3` contact sheet containing exactly twelve isolated modules in this order:

1. `wall-left`, `wall-right`, `corner-outer`, `corner-inner`
2. `arch-open`, `door-closed`, `wall-broken`, `wall-niche`
3. `column`, `arcade`, `stairs`, `landmark`

Use the mandatory projection in `ISOMETRIC_ART_CONTRACT.md`; do not infer projection from a concept
reference. It is a fixed `2:1` dimetric camera: floor edges run only at `+26.565°` or `-26.565°`,
screen verticals remain exactly vertical, and one logical diamond is `1.44 x 0.72` world units.
Match the measured wall socket, ground baseline and pivot instead of hiding joins with overlap.
Use real vertical volume, readable textured surfaces, coherent relative scale, generous padding, and
a perfectly uniform `#00ff00` chroma background. Do not add outlines, strokes, diagram lines, cell
borders, labels, text, UI, grids, frames, separators, baked floor, cast shadows, or silhouette glow.
Use crisp high-resolution hand-painted pixel art with upper-left lighting and no blur.

Before runtime export, every module must pass the overlay and close Game-view acceptance gate in
`ISOMETRIC_ART_CONTRACT.md`. A standalone contact sheet is not proof of a valid tile.

Biome material clauses:

- `ashen-catacombs`: cracked charcoal-gray cathedral masonry, worn carved stone, restrained rusted iron, subtle warm grime.
- `ember-vaults`: soot-black basalt, burnt brick, heat-cracked masonry, restrained ember seams inside carvings, scorched bronze and iron.
- `drowned-crypt`: damp blue-gray stone, green oxidation, drainage channels, worn aquatic motifs, barnacles, sparse moss, tarnished bronze.
- `charnel-gardens`: bone-inlaid weathered stone, structural petrified roots, restrained fungal growth, ossuary arches, dark earthen grime.
- `obsidian-sanctum`: faceted black volcanic glass, dark carved stone, restrained violet crystal insets, arcane engravings, monumental ritual geometry.

Keep these generated sheets under `ArtSource/Architecture/FacadeReferences`. They are facade references only and must not be exported to `Resources` or painted along dungeon contours. Runtime wall art requires a separate fixed-size microtile contract with aligned left/right halves, vertical bands, tile properties, and special-overlay sprites.
