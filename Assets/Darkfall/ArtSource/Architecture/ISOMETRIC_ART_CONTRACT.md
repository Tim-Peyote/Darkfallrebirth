# Darkfall Depths — mandatory isometric art contract

This document is the source of truth for every world sprite. Concept references may define
silhouette, ornament or mood; they never define projection. Runtime projection is defined only by
`IsoWorld` and the machine-readable `isometric-art-contract.json` beside this file.

## Projection

- Projection type: `2:1 dimetric` (the common game-art form usually called isometric).
- Logical X basis: `(+0.72, -0.36)` world units.
- Logical Y basis: `(-0.72, -0.36)` world units.
- One floor diamond: `1.44 x 0.72` world units, exactly `2:1`.
- Every receding ground edge is therefore `+26.565°` or `-26.565°` from screen horizontal.
- World verticals remain exactly vertical (`90°` on screen). Vertical architecture must never
  converge toward a vanishing point.
- Standard wall rise above its floor baseline: `0.82` world units.

Unity documents the default isometric grid as a dimetric `1 : 0.5` cell. Darkfall uses the same
ratio, scaled by `1.44`, and performs projection explicitly in `IsoWorld.Project`.

## Runtime architecture socket

- Canonical straight-wall canvas: `362 x 362 px`.
- Canonical architecture PPU: `230`.
- Canonical pivot: `(0.5, 0.08)` unless a measured per-role baseline overrides it.
- A module must be authored for one of the two projected wall planes. It is forbidden to use a
  front-facing facade and rotate or shear it by eye afterward.
- Left/right connection sockets must meet the measured plinth, frieze and cap bands of the
  neighbouring straight-wall role. Decorative overlap is not a substitute for a socket.
- A threshold replaces wall geometry inside its reserved span. Do not paint a second continuous
  wall behind a door or cover bad joins with additional masonry.

## Door contract

- Silhouette: a tall, narrow central arch, clearly higher than both low side pylons.
- The central opening is centred on the logical threshold.
- The skull is the keystone at the exact crown of the arch.
- Side pylons end below the spring line/crown and terminate at the two measured wall sockets.
- Closed, intermediate and open frames share one canvas, one baseline, one pivot and one opaque
  masonry silhouette. Only the wooden leaf changes.
- Door animation is composited from one immutable open-arch master plus leaf-only frames. Generating
  or redrawing the masonry independently per animation frame is forbidden.
- The top cap of both side pylons must land on the measured top-cap socket of the canonical straight
  wall at runtime scale. Only the central arch and skull may rise above ordinary wall height.
- The final open frame contains no leaf and no black fill. The opening is true alpha so the player,
  floor, fog and lighting can render through it.
- No baked floor, contact shadow, neighbouring wall, UI, glow or free-standing perspective base.

## Acceptance gate for every world sprite

1. Overlay both floor-basis guide lines: all ground-plane edges must be parallel to one of them.
2. Overlay the correct canonical wall role: cap, plinth and connection sockets must coincide.
3. Compare all animation frames: canvas, pivot, baseline and static masonry bounds must not move.
4. Capture close Game-view screenshots for both wall orientations and every final state.
5. Reject the asset for any gap, double cap, floating plinth, black opening, baked floor, scale jump
   or line that does not follow `±26.565°`/vertical.

An asset is not complete because its standalone PNG looks plausible. It is complete only after it
passes the socket overlay and close in-game screenshot checks.
