# Darkfall Depths — UI System V3

## Brand idea

Darkfall is not ornate high fantasy. Its interface is dungeon equipment: forged, worn,
precise and readable. Obsidian steel carries structure, aged bronze separates layers,
and the amber Depth Fire marks focus, danger and player agency.

The descending ember sigil is the primary mark. Use it once per major screen and never
as repeating wallpaper.

## Palette

| Role | Hex | Use |
|---|---:|---|
| Void | `#08090B` | modal shade and world separation |
| Obsidian | `#17181B` | primary surfaces |
| Iron | `#303136` | structural frame |
| Aged bronze | `#80643A` | separators and passive emphasis |
| Depth fire | `#E47A18` | focus, active ability and important state |
| Bone | `#E4DDCF` | primary text |
| Ash | `#9D978D` | secondary text |
| Blood | `#9E241F` | health, destructive confirmation |

Do not use blue as the generic interactive color. Rarity colors belong to item borders
and comparison data, not entire panels.

## Typography

- Display and major screen titles: Cormorant Garamond, uppercase, restrained tracking.
- UI labels and data: PT Sans, regular or bold.
- Minimum desktop body size: 16 px at the 1920×1080 reference canvas.
- Minimum mobile body size: 18 px. Do not place text directly on the world.
- Use no more than three text weights/sizes inside one card.

## Layout

- Base spacing unit: 8 px. Preferred gaps: 8, 16, 24 and 32 px.
- Interactive targets: at least 48×48 desktop and 64×64 touch.
- Important information follows three stable zones: player state bottom-left, action state
  bottom-center, navigation and system actions top-right.
- Equipment, backpack and inspection remain separate columns. Details never cover the grid.
- Panels use 9-slice sprites. Never stretch a painted full-resolution mockup.
- Keep content at least 24 px inside panel borders and 12 px inside slot borders.

## Component rules

- `panel-plate`: screens, modal cards, HUD groups and minimap.
- `button-plate`: text actions, boss bar and short interaction prompts.
- `slot-plate`: inventory cells, quick slots and compact icon actions.
- Depth Fire signals selected/focused/ready. White or bronze signals neutral.
- Hover brightens metal slightly; press darkens it. Components must not jump or resize.
- Destructive actions remain structurally identical and change only accent/text to Blood.

## Adaptation

The canvas reference is 1920×1080 with equal width/height matching. All outer HUD groups
must pass through `SafeAreaFitter`. Inventory uses normalized anchors and never fixed screen
coordinates. At ultrawide ratios, columns retain their proportions and the world receives
the extra width. At narrow ratios, touch controls grow while decorative elements may hide;
content and labels may not shrink below their minimum sizes.

## Asset sources

The `*-chroma.png` files are editable generated masters. Runtime-ready transparent PNGs are
stored under `Assets/Darkfall/Resources/UI/BrandV3`. Chroma removal and sprite extraction are
reproducible through `Tools/ArtPipeline`.
