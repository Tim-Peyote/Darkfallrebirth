# Darkfall Depths — UI design system

## Direction

Dark medieval fantasy presented with modern restraint. Depth comes from translucent layers,
soft vertical gradients and hierarchy—not from large ornamental frames. Character and item art
must remain the visual focus.

## Core rules

- Use the procedural 9-slice primitives in `DarkFantasySkin`; never stretch a raster frame.
- One panel equals one information group. Do not nest visible borders unless the child is interactive.
- Use one accent at a time: warm gold for focus and progression, red for danger/health.
- Keep corners consistent: 14 px modal, 12 px card, 10 px button, 8 px item slot.
- Keep a minimum 8 px internal rhythm; primary gaps are 16, 24 and 32 px.
- Do not place decorative sigils beside ordinary labels. Brand marks belong to branding surfaces only.
- Use `PTSans-Regular` and `PTSans-Bold` for all Cyrillic UI. Do not mix font families inside a screen.
- Text is never baked into a texture. Icons and portraits use `preserveAspect`.

## Palette

- Ink: `#050607` — modal dim and deepest background.
- Surface: `#090B0C` to `#161717` — gradient panels.
- Raised: `#0E0F0F` to `#272319` — buttons and selected surfaces.
- Hairline: muted bronze/graphite at 35–70% opacity.
- Primary text: warm white `#E8E3D6`.
- Secondary text: neutral grey `#9E9F9B`.
- Focus: gold `#D19F4C`.
- Health: dark crimson to ember red.

## Layouts

### HUD

- Player state: compact bottom-left card with portrait, one metadata line and two bars.
- Quick access: one bottom-centre rail; three consumables plus ability, equal sizing and baseline.
- Minimap and system actions: compact top-right cluster.
- Do not expose empty decorative containers.

### Inventory

- Left: backpack and item actions.
- Centre: full character preview, equipment arranged around the silhouette, quick access below.
- Right: selected item details; chest grid occupies its upper section only while a chest is open.
- Drag targets use a temporary gold focus line. Persistent heavy outlines are prohibited.

### Menus

- Main menu navigation is left-aligned over environmental art.
- Character selection uses three equal cards with breathing previews and a single selected state.
- Modal screens use one centred rounded card and no full-screen ornamental frame.

## Interaction states

- Normal: quiet graphite/bronze edge.
- Hover: small luminance lift; geometry does not move.
- Selected/drop target: thin gold focus.
- Pressed: reduced luminance and warmth.
- Disabled: lower contrast; never hide the label.
- Destructive actions require confirmation and use red only in the confirmation state.
