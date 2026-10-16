# Profit coin + sale text (Chief Art Designer)

## Sprites
- `Assets/Resources/Art/UI/profit_coin.png` — 64×64, coin ~60 px, transparent edge
- `Assets/Resources/Art/UI/profit_coin_128.png` — 128×128 (hi-DPI)
- Import: Sprite (2D and UI), single, alphaIsTransparency, no mipmaps, bilinear.
- Load: `Resources.Load<Sprite>("Art/UI/profit_coin")` (or `"Art/UI/profit_coin_128"`).

## Text
- Profit: `+N profit`, `#E0B85A`, 2 px outline `#3A2618`.
- Loss: `−N`, `#A8584A` at 85% alpha, with a 2 px `#3A2618` outline (same as profit). No coins, no sound.

## Coin fly (profit only)
- 5–8 coins, 24–32 px on screen.
- Arc from the market sale point to the HUD coin counter over ~0.6 s, ease-in.
- Slight random spin per coin; stagger launches by 0.05 s.
- Counter ticks up as coins land.
- No glow, no sparkle.
