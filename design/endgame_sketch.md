# Galeborne: Endgame sketch (rank milestones and contracts)
First sketch for Jeff, due 9 PM PKT. Design only, no code yet.

## Goal
Give players a reason to keep sailing after the opening ends (StDone). Each session should have a short goal (a contract), a medium goal (the next rank), and a long goal (the top rank, then Free Play).

## 1. Rank milestones
Rank goes up with **Renown**, which you earn from contracts, profitable voyages, won fights and bounties. Coins alone don't raise your rank, so you can't just grind one trade route.

| Rank | Renown | Unlocks |
|---|---|---|
| Deckhand | 0 | Start of the game after the opening |
| Trader | 100 | Contract board in every tavern, tier 1 contracts |
| Merchant | 300 | Second cargo deck at the Shipwright, tier 2 contracts |
| Captain | 700 | Bounty board, escort contracts, better duel gear |
| Merchant Prince | 1500 | Tier 3 contracts, a named rival who competes for the same contracts |
| Lord of the Monsoon | 3000 | A final contract chain, then Free Play |

- Each rank-up gets its own full card with a brass seal and the new title, and the title shows in the status bar.
- The Journal shows "Next rank: Merchant, 180 / 300 Renown" in place of the opening goal line.

## 2. Contracts
Each tavern board offers 3 contracts, and they refresh every moon. Every contract shows its reward, its deadline in moons, and its risk up front.

| Type | Example | Risk |
|---|---|---|
| Delivery | "Bring 10 sacks of dates to Muscat within 2 moons." | Low |
| Rush | Same, with half the time and double the pay | Storms cost time |
| Smuggling | Carry goods that are banned at their destination | Customs catch |
| Escort | Protect a merchant ship on the next voyage | Forced fight |
| Bounty (Captain and up) | Hunt a named pirate seen near a port | A harder duel |

- **Each port has its own contract flavour:** Gwadar deals in fish and salt, Debal in spice and cloth, and Muscat in smuggling and gunpowder. That keeps routes a real decision and backs up the "no two towns alike" rule.
- **Failing** costs Renown and the deposit, never story cargo or the ship.
- **Streaks:** completing 3 contracts in a row gives a bonus card. It's a small reason to play "one more voyage".

## 3. Free Play (after Lord of the Monsoon)
- An endless contract board, with monthly rival leaderboards against your own best runs.
- Optional modifiers for higher Renown multipliers: harsher storms, stricter customs, tougher pirates.

## Fit with tonight's fun work
- The duel cap (Jeff's call) pairs with bounties: late-game difficulty should come from named pirates, not from a hit zone that keeps growing.
- Researcher's point about event variety: each contract type brings its own voyage events, like an escort ambush or a customs sweep.

## Jeff's calls (17:17 PKT)
1. **Renown:** none from selling goods. It comes only from contracts, fights and bounties, plus a small bonus for the first sale of a new good at each port.
2. **Named rival:** in v0.7, as a light version. One rival appears at the rank-2 promotion, steals a contract now and then, and can be beaten in a bounty duel.
3. **Deadlines:** counted in moons. The moons left show on the contract card and on the sea map.

## First slice (approved, after the duel window)
Renown plus the first 3 ranks, plus delivery and rush contracts at the 3 existing ports. That's enough to test whether players chase the next rank.
