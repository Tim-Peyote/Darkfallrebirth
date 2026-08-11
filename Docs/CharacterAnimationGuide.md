# Character animation guide

## Runtime contract

- Every authored hero frame is an independent 256×256 transparent PNG.
- Runtime uses three canonical rows: `down`, `up` and `right`; left mirrors `right`.
- Enemy 8×4 atlases register their reviewed right-facing row explicitly; generated sheets do not
  share one universal side-row order.
- Mirroring is allowed only while the pose remains readable with swapped weapon handedness.
- Every direction owns one stable foot pivot shared by idle, walk, attack and hurt.
- Directional silhouette scale is normalized in `DirectionalSpriteAtlas`; gameplay colliders never scale.
- File order is `idle_1..4`, `walk_1..4`, `attack_1..3`, `hurt_1..2`.

## Walk-cycle contract

1. `walk_1` — first contact pose.
2. `walk_2` — first passing pose.
3. `walk_3` — opposite contact pose.
4. `walk_4` — opposite passing pose.

Frames 1 and 3 must visibly use opposite leading legs. Frames 2 and 4 must transfer body weight
between those contacts. The head and torso may bob, but the foot-contact line must not move.

## Asset checks

- No neighbouring animation cell may leak into an exported PNG.
- Canvas, PPU, filter mode and alpha treatment are identical for the complete character set.
- Transparent gutter differences are handled by authored layout metadata, not by moving colliders.
- Attack flourishes, weapons and coat tails are not used to calculate the foot pivot.
- Switching direction on the same logical position must not make the actor float or sink.
- Spawn scale and animated scale must be identical; animation code never silently resizes actors.
- Idle includes breathing or secondary motion without changing the contact foot.

## Acceptance pass

For every hero, enemy and boss, review idle, walk, attack and hit while facing down, up, right and
mirrored left. Test direction changes while moving, movement into walls, attack during movement and
return from hit to idle. A clip is accepted only when scale, pivot, phase and gameplay hit direction
remain stable.
