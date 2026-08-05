# Darkfall asset slicing guide

## Non-negotiable rules

- Runtime hero animation uses one PNG per frame. Generic enemies use the documented directional sheet layout.
- Runtime inventory art uses one PNG per `baseId`. Source atlases are import material only.
- Every exported cell must keep its full transparent gutter. Do not trim a frame independently: trimming changes the pivot and makes animation jitter.
- If generated art touches a cell edge, apply the same inset to every frame in the whole character set. Current hero safe-area is `16 px`.
- All frames in one animation set must have identical dimensions, pivot and pixels-per-unit.
- Heroes use `right` as the canonical horizontal animation. Unity mirrors it for movement to the
  left with `SpriteRenderer.flipX`; do not create an independently timed left set. A dedicated left
  set is allowed only when handedness or asymmetric equipment changes gameplay and it has been
  reviewed frame-for-frame against the right set.
- Atlas coordinates are measured from the top-left corner. The slicer rejects rectangles outside the source image.
- Unity imports runtime sprites with mipmaps disabled, bilinear filtering and clamp wrapping. A transparent gutter prevents adjacent-frame bleeding.
- `DarkfallSpriteImportRules` enforces these settings automatically. Characters use point filtering; inventory art uses bilinear filtering. Both are uncompressed `256×256` textures with mipmaps disabled.

## Current grids

- Equipment: `1536×768`, `6×3`, cell `256×256`.
- Potions: `1536×512`, `6×2`, cell `256×256`.
- Scrolls: `1536×512`, `6×2`, cell `256×256`.
- Heroes: final export is separate `256×256` files under
  `Resources/Sprites/Characters/<hero>/<direction>/<state>.png`.
- Directional enemies: `1774×887`, `8×4`. Rows are `down`, `left`, `right`, `up`.
  Columns are `idle_1`, `idle_2`, `walk_1`, `walk_2`, `attack_1`, `attack_2`, `attack_3`, `hurt`.
  Runtime treats `right` as the canonical horizontal row and mirrors it for left. Keep the legacy
  left row only as an authoring reference; gameplay must not mix independently timed side rows.
- Mimic: `Resources/Sprites/Directional/enemy-mimic-v1.png` follows the directional enemy grid.
  Keep the complete transparent gutter around every silhouette; `DirectionalSpriteAtlas` slices it at runtime.

For a grid cell `(column, row)`, use `x = column * 256`, `y = row * 256`.

```bash
swift Tools/ArtPipeline/SliceAtlas.swift source.png x y 256 256 output.png
```

## Required validation

1. Output dimensions are exactly `256×256`.
2. Corners and cell borders are transparent.
3. No pixels from adjacent cells are visible.
4. Character feet remain on the same baseline in every state and direction.
5. Every catalog `baseId` resolves to exactly one individual item PNG.
6. Inspect first, middle and last cells visually before deleting or quarantining a source atlas.

Run the automated size and border check on every final set:

```bash
swiftc Tools/ArtPipeline/ValidateSprite.swift -o /tmp/darkfall-validate-sprite
/tmp/darkfall-validate-sprite path/to/frame.png path/to/next-frame.png
```

The runtime hero set contains `idle`, `walk_1..4`, `attack_1..3`, and `hurt_1..2` for `down`, `up`,
and canonical `right`. The four walking images must alternate neutral/contact poses; the current
generated sets play in `1, 2, 4, 3` order so opposite leg contacts are separated by a neutral pose.
Runtime still consumes individual `256×256` frames.

Generated hero grids use a uniform `#ff00ff` matte. Convert it before slicing:

```bash
swift Tools/ArtPipeline/RemoveChroma.swift hero-grid-chroma.png hero-grid-alpha.png
```
## Cleaning generated animation cells

Generated atlases may visually resemble a regular grid while a weapon or foot from an adjacent
pose crosses the mathematical cell boundary. Never ship the raw crop. After `SliceAtlas.swift`,
run `KeepMainSprite.swift` on each 256x256 cell to retain the centered connected silhouette, then
run `InsetSprite.swift` with an 8-12 pixel inset. Finish with `ValidateSprite.swift`; every frame
must report a clean 2px alpha gutter before it is placed under `Resources/Sprites`.

When poses are not centered consistently enough for mathematical crops, use
`ExtractConnectedGrid.swift <alpha-sheet> <columns> <rows> <output-directory>` instead. It detects
complete silhouettes on the full sheet before assigning and centering them, so a pose crossing a
nominal cell boundary is not cut in half.
