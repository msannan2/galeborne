# Galeborne endgame data, slice 1 (draft for Game Designer review)

Sources: endgame_sketch.md (1a0477a, 4d0b0d1), contract_card_and_rival.md (0082a5a), galeborne_settings.json. Machine-readable twin: `endgame_data_slice1.json`.

## Ranks
| Rank | Renown | Unlocks | Card heading | Button | Promotion line |
|---|---|---|---|---|---|
| Deckhand | 0 | One starter contract on each board. | - | - | - |
| Trader | 100 | Full board: 3 contracts, rush jobs open. | Known on the quay | Onward | The harbour knows your name now. Trader. |
| Merchant | 300 | Second cargo deck at the Shipwright. Tier 2 contracts. | The guilds take note | To sea | Your word is good in every port. Merchant. |

Rashid on the Trader card: "So the harbour talks about you now. Enjoy it while it lasts."

**Open question for Game Designer:** the sketch puts the contract board at Trader, but the pace target needs contracts to reach Trader. Proposal: Deckhand boards show 1 starter delivery; Trader opens the full 3-card board and rush jobs.

## Renown values
| Source | Renown |
|---|---|
| Delivery done | +20 |
| Rush done | +30 |
| Fight won | +5 |
| Bounty (Captain+, later) | +40 |
| First sale of a good at a port | +3 (once each, max 39 total) |
| Selling otherwise | 0 |
| Delivery failed | -10 + deposit |
| Rush failed | -15 + deposit |

Renown never drops below your current rank's threshold (no demotion).

## Pace math
Pirate encounters: chance 0.333 per sail after 2 free sails, win chance ~0.55, so ~1 won fight every 5-6 sails. A contract run is ~2 sails (out and back).

- **Trader (100):** 4 contracts = 3 delivery + 1 rush = 90, + ~1-2 fights won (8 sails) = 5-10, + ~2 first sales = 6. Total ~101-106. Contracts alone: 4 deliveries = 80, so a pure-delivery player needs a 5th; a rush-heavy player gets there in 4.
- **Merchant (300):** 12 contracts = 8 delivery + 4 rush = 280, + ~4 fights won (24 sails) = 20, + ~5 first sales = 15, - one failed delivery = -10. Total ~305.
- First-sale bonus is capped at 39 lifetime, so exploring helps but can't carry a rank (<14% of Merchant).

## Coin scale
Reward caps (Game Designer): delivery 900, rush 1,200 coins. Over-cap templates use smaller quantities, never clamped rewards.
Reward = cargo value (qty × mid price × 1.25 want-mult) + premium (50% of cargo mid value for delivery, 100% for rush). Rush = half time, about double the premium. Deposit 15% (delivery) / 20% (rush), rounded to 10. Start coins 1,000, hold 20, max 2 active contracts, so qty stays 3-12.

## Card strings
- `rankUpTitle`: Raised in rank
- `rankUpButton`: Onward
- `accept`: Accept
- `notNow`: Not now
- `moonsLeft1`: 1 moon left
- `moonsLeftN`: {n} moons left
- `lastWeek`: Last week
- `taken`: Taken by Rashid
- `cargo`: Cargo
- `deliverTo`: Deliver to
- `deadline`: Deadline
- `reward`: {coins} coins · +{renown} Renown
- `deposit`: Deposit {d} coins, returned on delivery
- `delivered`: Delivered. +{coins} coins, +{renown} Renown.
- `failed`: The buyer found another captain. {renown} Renown, deposit lost.
- `full`: You can hold 2 contracts.
- `nextRank`: Next rank: {rank}, {cur} / {need} Renown
- `firstSale`: First {good} sold here. +{renown} Renown.
- `listRow`: {type} · {dest} · {moons} · {coins} coins

## Gwadar — Plain-spoken fishers and salt men

### Delivery (2 moons)
| Title | Giver line | Cargo | To | Coins | Renown | Deposit | Risk |
|---|---|---|---|---|---|---|---|
| Salt for pickling | *Salt's salt. Get it there dry.* | 11 salt | Debal | 870 | +20 | 130 | Rain at sea can spoil an open hold. |
| Dates for Debal | *My cousin waits. Don't make him wait long.* | 12 dates | Debal | 840 | +20 | 130 | Pirates watch the open water past the coast. |
| Copper for smiths | *Heavy load. Honest pay. That's all.* | 5 copper | Muscat | 790 | +20 | 120 | Customs in Muscat search some ships. |
| A jeweller's pearl | *A diver bled for this one. Mind it.* | 1 pearls | Debal | 660 | +20 | 100 | One pearl draws pirates like gulls to bait. |

### Rush (1 moon)
| Title | Giver line | Cargo | To | Coins | Renown | Deposit | Risk |
|---|---|---|---|---|---|---|---|
| Salt for the feast | *Feast's soon. Sail now, not tomorrow.* | 10 salt | Debal | 1,010 | +30 | 200 | A storm could eat the days you have. |
| Copper, fast | *Forge is cold. They pay double, quick.* | 5 copper | Muscat | 1,010 | +30 | 200 | Head winds can cost a week. |
| Wedding dates | *No dates, no wedding. Hurry, friend.* | 10 dates | Debal | 900 | +30 | 180 | One bad storm and the wedding is over. |
| A pearl, in haste | *He sails home soon with or without it. Catch him.* | 1 pearls | Debal | 840 | +30 | 170 | Pirates and storms both slow a rush run. |

## Debal — Fast-talking spice and cloth brokers

### Delivery (2 moons)
| Title | Giver line | Cargo | To | Coins | Renown | Deposit | Risk |
|---|---|---|---|---|---|---|---|
| Pepper for kitchens | *Finest pepper, finest price, go go!* | 4 pepper | Gwadar | 800 | +20 | 120 | Pirates like a hold that smells of spice. |
| Cotton for weavers | *Bolts of cloud, my friend. Muscat loves it.* | 8 cotton | Muscat | 840 | +20 | 130 | Customs in Muscat search some ships. |
| Indigo for a dyer | *Blue like the sea, worth more than the sea!* | 3 indigo | Gwadar | 740 | +20 | 110 | Rain can stain a careless cargo. |
| Tea for the court | *Court tea! Court pay! You'll thank me.* | 4 tea | Muscat | 740 | +20 | 110 | Open water past the coast draws pirates. |

### Rush (1 moon)
| Title | Giver line | Cargo | To | Coins | Renown | Deposit | Risk |
|---|---|---|---|---|---|---|---|
| Pepper, sell high | *Quick, quick! The price won't wait.* | 4 pepper | Gwadar | 1,040 | +30 | 210 | A storm could cost you the margin. |
| Cotton, rush order | *Muscat wants it yesterday. Can you?* | 8 cotton | Muscat | 1,080 | +30 | 220 | Head winds can cost a week. |
| Tea for a caravan | *Caravan leaves at dawn. Be there!* | 5 tea | Muscat | 1,180 | +30 | 240 | Storms and searches both eat time. |
| Festival indigo | *Festival blue! Late is worthless!* | 3 indigo | Gwadar | 940 | +30 | 190 | One storm and the festival is gone. |

## Muscat — Guarded and formal

### Delivery (2 moons)
| Title | Giver line | Cargo | To | Coins | Renown | Deposit | Risk |
|---|---|---|---|---|---|---|---|
| Temple incense | *The temples expect this. Do not fail them.* | 2 incense | Debal | 770 | +20 | 120 | Pirates know what incense is worth. |
| Coffee for traders | *A gift of good faith. Treat it so.* | 5 coffee | Gwadar | 880 | +20 | 130 | The coastal run is calm, but storms come. |
| A pearl, discreetly | *A single pearl, captain. Speak of it to no one.* | 1 pearls | Debal | 660 | +20 | 100 | Even one pearl draws pirates on open water. |
| Dates for the poor | *The Sultan's charity. Carry it well.* | 12 dates | Debal | 840 | +20 | 130 | Rain at sea can spoil an open hold. |

### Rush (1 moon)
| Title | Giver line | Cargo | To | Coins | Renown | Deposit | Risk |
|---|---|---|---|---|---|---|---|
| Funeral incense | *There is no second day for this. Go.* | 2 incense | Debal | 990 | +30 | 200 | A storm would make you late for the dead. |
| Coffee for a guest | *The guest arrives soon. So must you.* | 5 coffee | Gwadar | 1,120 | +30 | 220 | Head winds can cost a week. |
| A betrothal pearl | *The bride's family awaits this pearl. Do not keep them.* | 1 pearls | Debal | 840 | +30 | 170 | Pirates and storms both slow a rush run. |
| Dates to break fast | *When the fast ends, they eat. Be there.* | 10 dates | Debal | 900 | +30 | 180 | One storm could cost the whole run. |
