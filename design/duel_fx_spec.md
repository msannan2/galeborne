# Duel hit feedback: timing spec

Assets (Sprite, 256×256 RGBA, straight alpha, black removed):
- `Resources.Load<Sprite>("Art/FX/duel_hit_flash")`: brass hit burst
- `Resources.Load<Sprite>("Art/FX/duel_splinter")`: teak splinter puff

All times in seconds from the moment the hit lands (t = 0). Use normal alpha blending (UI default). **No bloom, no glow, no additive blending.**

## Hit
| Element | Start | Duration | Animation |
|---|---|---|---|
| Hit flash | 0.00 | 0.25 | Random Z rotation (0–360°) at spawn. Scale 0.6 → 1.1 over 0–0.12 (ease-out). Alpha 1 → 0, fully gone by 0.25. |
| Splinter puff | 0.05 | 0.35 | Scale 0.8 → 1.3 (ease-out) while alpha 1 → 0 over 0.35 (ends at 0.40). |
| Card shake | 0.00 | 0.15 | Struck card shakes ±6 px (random offset each frame, damped to 0 by 0.15). |
| Striker lunge | 0.00 | 0.20 | Striker's card moves 18 px toward the opponent and back (out 0–0.10, back 0.10–0.20). |

Both FX are centred on the impact point, drawn above the cards, and destroyed after they fade.

## Miss
- No flash and no splinter.
- Show the ring's real position as a faint outline (ink/brass, alpha ~0.35) for 0.4 s, fading out, so the player sees how close they were.

## Not allowed
- Bloom, glow, light shafts, additive sprites or screen flashes.
