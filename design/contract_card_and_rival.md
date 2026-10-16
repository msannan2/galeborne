# Contract card and rival spec (v0.7 first slice)
This builds on `endgame_sketch.md` and Jeff's calls from 17:17 PKT.

## Contract card (tavern board)
The board shows 3 cards stacked in the existing parchment frame. Tapping one opens the full card.

**List row**, one line each:
`[port crest] Delivery · Muscat · 2 moons · 340 coins` with the risk pip on the right (green, amber or red).

**Full card**, top to bottom:
1. **Title:** a short verb phrase in the light brass #C9A55A heading, for example "Dates for the Sultan's kitchens".
2. **Giver line:** an italic one-liner in the giver's voice. Each port has its own voice: Gwadar is plain-spoken fishers, Debal is fast-talking spice brokers, and Muscat is guarded and formal.
3. **Terms block**, three rows with icons, the same layout as the market rows:
   - **Cargo:** "10 sacks of dates" (through `GoodCount`).
   - **Deliver to:** "Muscat".
   - **Deadline:** "2 moons left". This turns amber at 1 moon and red in the last week.
4. **Reward block:** coins and Renown on one line, "340 coins · +25 Renown". The deposit, if there is one, goes below it in smaller text: "Deposit 50 coins, returned on delivery".
5. **Risk line:** one plain sentence, for example "Customs in Muscat search every third ship." It never shows a bare percentage.
6. **Buttons:** Accept (primary) and Not now. Back counts as Not now, the same as the job offer.

**Rules**
- Accepted contracts appear in the Journal under the goal line, with the moons left.
- On the sea map, the destination port gets a small brass flag showing the moons left.
- Delivering shows a result card: "Delivered. +340 coins, +25 Renown." It counts up, which matches the reward count-up idea.
- Failing shows: "The buyer found another captain. −10 Renown, deposit lost."
- You can hold at most 2 active contracts in the first slice.

**Copy rules:** sentence case, a full stop on result lines, plurals through `GoodCount`, and no numbers above 9,999 without a comma.

## Rival (light version, v0.7)
- **Who:** Captain Rashid of Sur. He's a Muscat-born trader who's flashy, polite on the surface and cutting underneath. Chief Art Designer can design his portrait to sit apart from the three merchants: a red and black turban, a gold earring and a sharp grin.
- **Intro:** at the rank-2 promotion (Trader), he appears on the rank-up card: "So the harbour talks about you now. Enjoy it while it lasts."
- **Contract stealing:** each moon, there's a 1-in-4 chance one board contract at your current port shows "Taken by Rashid." The card stays on the board, greyed out, so the player sees what they lost. He never takes a contract you've already accepted.
- **Bounty duel:** from Captain rank, a bounty "Rashid's luck runs out" appears once. It's a duel with one extra round and the capped duel window, with no growth bonus. Winning gives +100 Renown and a unique title in the status bar: "Rashid's bane".
- **Losing the duel:** costs coins only, never cargo, and Rashid stays active with a taunt. You can try again after 2 moons.
- **Rule:** he never touches story cargo, never sinks the ship, and never blocks progress. He creates pressure, not walls.

## For Game Researcher's data pass
Please key all text by port so each port's voice stays distinct. I'll review the Renown numbers against this target pace: Trader after about 4 contracts, and Merchant after about 12.
