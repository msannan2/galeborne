# Rival art spec: Captain Rashid of the Red Dhow (v0.7)

Owner: Chief Art Designer. Branch `art/rival-sail`. Game Designer's v0.7 spec says "Qasim"; read **Rashid** everywhere.

## 1. Race-contract icon (red sail on brass plaque)

| File | Size | Use |
|---|---|---|
| `Assets/Resources/Art/UI/rival_sail_icon.png` | 128x128 | default, scales down cleanly to 24-32 px |
| `Assets/Resources/Art/UI/rival_sail_icon_64.png` | 64x64 | optional, for low-res lists |

- Load: `Resources.Load<Sprite>("Art/UI/rival_sail_icon")` (or `"Art/UI/rival_sail_icon_64"`).
- Import: Sprite (2D and UI), single, pivot centre, alpha is transparency, no mipmaps, Clamp, Bilinear. Same importer settings as `profit_coin`.
- Alpha: round plaque with an anti-aliased circular edge, fully opaque inside, transparent outside, no dark fringe. It works on the dark card `#2A1D14` and on parchment `#D9C08A`.
- Placement: on the contract board, **28-32 px**, vertically centred, **left of the title** of every race contract (a contract Rashid is also chasing). Leave about 6 px between the icon and the title. Use `preserveAspect = true` and no tint (white).
- Don't put the icon on normal contracts. It means "Rashid is racing you for this one".

## 2. Red Dhow sail

- Texture: `Assets/Resources/Art/Ships/red_sail_canvas.png`, 512x512, seamlessly tileable red canvas with horizontal panel seams. Load: `Resources.Load<Texture2D>("Art/Ships/red_sail_canvas")`.
- Import: Default texture type, wrap **Repeat**, mipmaps on, sRGB on, Bilinear, aniso 2.
- Material: URP **Lit**, `_BaseMap` = red_sail_canvas, `_BaseColor` white, `_Smoothness` **0.1**, metallic 0, **no emission** (don't copy the cream lateen's `_EMISSION`/`_EmissionColor` from `FreeModels.FixLateenSail`). Keep `_Cull` = 0 (double-sided) like the player's sail.
- Tiling: sail UVs span 0..1 (see the note in `FixLateenSail`). Start at tiling (1,1). If the weave looks too coarse, use (2,2). The texture repeats cleanly at any whole number.
- Fallback: if the sail mesh is untextured or has no usable UVs, use a plain Lit material with `_BaseColor` **#8E2F25** (0.557, 0.184, 0.145), smoothness 0.1, no emission.
- Only the sail goes red. The hull and yard stay teak like the player's dhow, so the red sail is what tells it apart at sea. Per Game Designer, the sail colour doesn't count as a town look.

## 3. Rashid's portraits

- Branch `art/rashid-portrait`, commit `bac7f0a`. Keeper row 7: `Keepers/k7_0..3`.
  - `k7_0` neutral
  - `k7_1` delighted (he wins a race)
  - `k7_2` offended (he loses a race)
  - `k7_3` suspicious (tavern rumour)
- Framing matches Salim's keeper portraits, so use the same UI slot and size.

## Preview

`/workspace/galeborne/art/rival_art_preview.png` on the box, outside the repo. It shows the icon at 64/32/24 px on `#2A1D14` and `#D9C08A`, a mock 30 px board row, and the canvas tiled 2x2.
