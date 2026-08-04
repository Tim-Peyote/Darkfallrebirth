# Character Select Idle Animation

- Runtime frames: `Assets/Darkfall/Resources/Sprites/UIHeroIdle/<hero>/idle_1..4.png`.
- Every frame is exactly 256×256 with at least a 2 px transparent gutter.
- The silhouette is centered horizontally and placed on the same bottom baseline.
- Playback sequence is `1 → 2 → 3 → 4 → 3 → 2` at 3 frames per second.
- Breathing is authored in the pixels. Never animate `RectTransform.localScale` or rotation.
- UI pivot stays fixed at `(0.5, 0.08)` and `preserveAspect` remains enabled.
- Feet, staff tips, sword tips and dagger tips must not move vertically between frames.
- New frames must be validated with `Tools/ArtPipeline/ValidateSprite.swift` before use.

The generated chroma strips are preserved beside this guide. Runtime PNGs are produced by
chroma removal followed by `ExtractConnectedGrid.swift`, which isolates each frame and
normalizes it into a clean 256×256 cell.
