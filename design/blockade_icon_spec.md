# Sea map blockade icon: spec

- Asset: `Resources.Load<Sprite>("Art/UI/blockade_icon")`, 128×128 RGBA Sprite (Android max 128). Brass plaque, parchment centre, two crossed dark spears (leaf heads up-left and up-right, butt caps, parchment gap at the crossing). Not tinted.
- **Placement:** on the sea map, at the **top-right of the port ring**, **44 px on a 1080 px-wide screen** (scale with screen width: 44 × width / 1080). Draw above the port ring, below any open popups.
- **When shown:** as soon as the player **knows** about a blockade at that port, from a **rumour** or a **Hail warning**. Hidden blockades the player hasn't heard of show nothing.
- **How long:** stays until the blockade ends (no fade/timer before that). Remove it when the blockade ends.
- **Tap:** tapping the port shows the blockade warning text (the rumour or Hail line that revealed it).
- No red, no glow, no pulsing; normal alpha blending.
