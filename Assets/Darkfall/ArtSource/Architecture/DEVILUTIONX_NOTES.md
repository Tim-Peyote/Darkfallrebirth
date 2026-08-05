# DevilutionX dungeon rendering notes

Source studied: `diasurgical/DevilutionX`, especially `levels/gendung.cpp`, `levels/gendung.h`, `levels/dun_tile.hpp`, `engine/render/scrollrt.cpp`, and `diablo.cpp`.

- A logical `.TIL` `MegaTile` contains four dungeon-piece identifiers (`micro1..micro4`) and expands to a `2 x 2` region in `dPiece`.
- A dungeon-piece is not one facade sprite. `.MIN` maps it to a vertical stack of paired CEL blocks (`MICROS.mt`): normally ten blocks, twelve for Hell, and sixteen for town storage.
- Rendering works in `64 x 32` tile space, using 32-pixel halves. CEL blocks explicitly encode square, transparent-square, left/right triangle, and left/right trapezoid shapes.
- The first left/right pair forms the floor or lower tile shape. Remaining pairs render upward one 32-pixel band at a time.
- Tile collision, light blocking, missile blocking, and transparency are data properties loaded from `.SOL`; they are not inferred from sprite names.
- Occluding architecture uses transparency regions (`dTransVal` plus `TransList`) and left/right transparency properties.
- Arches and doors use `dSpecial` and a separate special-CEL overlay pass. They are not random replacements for ordinary walls.

Darkfall consequence: architecture must be selected from topology and composed in stable draw passes. A complete facade cannot be stretched or repeated as a mesh texture; it can, however, be used as one independently anchored Unity module when its role is explicit.

## Exact original layout contract

- A level CEL frame is exactly `32 x 32` pixels.
- Frame type `0` is an opaque upper-wall square; type `1` is an RLE transparent square.
- Types `2/3` are the left/right transparent floor shapes; types `4/5` are the left/right wall-bottom trapezoids.
- A normal dungeon subtile is `64 x 160`: ten CEL references arranged as five left/right pairs. Town and Hell use sixteen references and reach `64 x 256`.
- A TIL tile contains exactly four subtile indices. Their pixel origins are `(32,0)`, `(64,16)`, `(0,16)`, and `(32,32)`, producing a `128 x 192` dungeon tile.
- DUN base-layer entries select TIL tiles; they do not directly select arbitrary sprites.
- Special arch files contain eight `64 x 160` frames. The base level CEL is drawn first, entities are drawn next, and the selected special frame is drawn last.
- SOL is one property byte per subtile: collision, light blocking, missile blocking, transparency, left/right floor transparency, and trap/background behavior.
- TMI adds rendering flags per subtile, including full second-pass redraw for walls/high objects and partial second-pass redraw for foliage protrusions.

Binary compatibility with Diablo's original renderer would require three separate authored products per biome:

1. `CEL`: fixed `32 x 32` microframes with explicit shape type.
2. `MIN/TIL`: data-only recipes composing microframes into subtiles and four-subtile tiles.
3. `SPECIAL`: a small fixed set of aligned `64 x 160` overlays drawn after characters.

Darkfall does not decode the original CEL/MIN/TIL files at runtime. Its current equivalent keeps the useful composition rules in a Unity-native form: split role files, one shared pivot, deterministic biome selection, topology-selected straight/inner/outer pieces, a separate architecture draw group and independent lighting passes. The large master sheets remain source references; the 60 split role files are the runtime modules.
