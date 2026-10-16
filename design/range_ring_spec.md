# Naval range ring: spec

## Assets (`Assets/Resources/Art/FX/`, Sprite, RGBA, transparent)
| Load path | Size | Notes |
|---|---|---|
| `Resources.Load<Sprite>("Art/FX/range_ring")` | 512×512 | Solid double ring (6 px outer, 2 px inner, 24 ticks, 4 larger cardinals). **Pure white + alpha: tint in code.** |
| `Art/FX/range_ring_dashed` | 512×512 | Same radius, 48 dashes. White, tint in code. |
| `Art/FX/range_pulse` | 512×512 | Single soft ring ~10 px, strongest at the outer edge, fading inward. White, tint in code. |
| `Art/FX/range_marker` | 64×64 | Short bold brass notch, dark #3A2618 outline, points **up** (= outward). Not tinted. |
| `Art/FX/range_label_plaque` | 192×64 | Parchment plaque, 1 px brass border, **9-slice, 16 px border** (Image.type = Sliced). Text colour #3A2618. |

Ring stroke radius in the 512 sprites is ~250 px (outer edge of outer stroke), so world scale = `cannonRange / (250/512 · spriteWorldSize)`; simplest: set the sprite's world size so that 500 px = 2 × cannon range.

## Tints
- Out of range: **#C8A060** (brass) at **60% alpha**.
- In range: **#E6CFA0** (pale) at 100%.
- Pulse: #E6CFA0.

## Placement
- The ring lies **flat on the water** (horizontal plane), centred on the pirate, scaled to the cannon range radius in world units.
- **Render below ship, on water plane.** It must never draw over the pirate's hull (or yours): sorting/queue just above the water, below all ships and wakes.
- `range_marker` sits **on the ring's edge**, at the point on the ring nearest the player's ship: position = pirate + normalize(playerShip − pirate) × rangeRadius. Rotate it around the ring so its "up" points outward toward the player's ship. It replaces any screen-edge pointer. Flat on the water like the ring.

## States
States must read by **dashed vs solid** even without colour (colour-blind safe).

**Out of range (closing / opening)**
- `range_ring_dashed`, brass 60%, rotating slowly at **6°/s** around its centre.
- `range_marker` on the ring edge facing your ship.
- Plaque: `Range 340 m · Closing` or `Range 340 m · Opening` (distance updates live; Closing/Opening from the sign of the distance change).

**On entering range**
- Swap to solid `range_ring`, pale #E6CFA0, no rotation.
- Play **one** `range_pulse`: scale 1.0 → 1.25 and alpha 1 → 0 over **0.4 s**, then destroy.
- Plaque reads `In range`.
- The FIRE button gets a brass (#C8A060) border while in range.
- Marker stays on the edge facing your ship.

On leaving range, swap back to the dashed brass state (no pulse).

## Readability
- Must read at **1080×2340 portrait**: at default camera zoom the outer stroke should be ≥ 3 screen px and the marker ≥ 40 screen px; plaque text ≥ 28 px.

## Rendering rules
- Normal alpha blending only. **No additive blend, no bloom, no glow.**
