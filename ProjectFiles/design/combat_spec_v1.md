# Galeborne Combat Spec v1 (first slice: Hasan's dhow vs one pirate dhow)
All values go in galeborne_settings.json under "combat".

## Flow (target 1-3 min)
1. Encounter card: Fight / Flee (Seamanship check vs wind) / Pay toll (10% cargo).
2. Maneuver + fire (real time). 3. Finish: Sink, Disable, or Board.

## Controls (portrait, one thumb)
- Swipe left/right on lower half = turn rudder. Sail button toggles full/half sail.
- Tap enemy part (sails, guns, hull, crew) to set aim target; gold marker shows it.
- Ammo tray (right edge): Round, Chain, Grape, Fire. Fire button + reload ring.

## Wind
- Speed multiplier by angle to wind: running 1.0, broad reach 1.1, beam 0.9, close-hauled 0.5, into wind 0.1.
- Upwind ship gets +15% accuracy.

## Ships (first slice)
| | Hull | Sails | Guns | Crew | Speed | Reload |
|---|---|---|---|---|---|---|
| Hasan's dhow | 100 | 60 | 4 per side | 20 | 8 | 4 s |
| Pirate dhow | 80 | 50 | 3 per side | 16 | 8 | 5 s |
Broadside fires only if target within 50° of beam and range <= 120 m.

## Ammo (damage per gun hit)
| Ammo | Hull | Sails | Guns | Crew | Range | Notes |
|---|---|---|---|---|---|---|
| Round | 6 | 1 | 2 | 1 | 120 m | default |
| Chain | 1 | 8 | 0 | 0 | 80 m | each 10 sail dmg = -10% speed |
| Grape | 0 | 1 | 0 | 3 | 50 m | crew loss lowers enemy duel and reload |
| Fire | 3 + burn 1/s for 8 s | 2 | 0 | 1 | 60 m | 1 per battle in slice, crew douse on 3rd tick 50% |
Base accuracy 60%, +15% upwind, +10% aimed part matches ammo, -1% per 10 m past half range.
Guns at 0 = enemy can't fire. Hull at 0 = sinks.

## Finishes and loot
- Sink (hull 0): 30% of enemy cargo as floating wreckage, 50-150 coins. Safe.
- Disable (sails <= 10 or guns 0, hull > 0): flee/surrender prompt; full cargo + 20% chance rare good/charm.
- Board (needs range <= 15 m and enemy speed < yours or disabled): duel. Full cargo + capture ship + charm roll + captain (ransom 300 / recruit / bounty). Risky: lose duel = lose 30% cargo, 25% crew injured (−reload speed until healed in port).

## Boarding duel (timing)
- Best of 3 rounds. A ring closes; tap in the gold zone = hit, early/late = parry fails.
- Gold zone size: base 20%, +5% per Combat point above enemy, +5% for Hasan's Blade of the Dunes.
- Enemy crew lower than yours: zone +5%.

## Enemy AI (slice)
- Tries to get upwind, fires when in arc, switches to chain if your speed > its speed.
- Flees at hull < 25% if its speed >= yours.

## Encounter safety
- First 2 sails pirate-free (existing). Max 2 adversaries hunting at once (later).
- After losing a fight: 3 sails with no pirate encounters.

## Tuning targets for Game Tester
- Average fight 90-150 s. Hasan wins about 75% vs pirate dhow with good play.
- Boarding win rate about 60% for an average player; sinking always available as safe option.

## Addendum (Oct 14, 2026)
- Pirate buff (approved): +30% accuracy, x1.3 damage.
- Results: WIN = enemy sunk, disabled and surrendered, or boarding duel won. LOSS = sunk, duel lost, or toll paid. DRAW = escape (run, bluff, bait).
- Ambush chance per voyage: base 33%; first 2 voyages pirate-free; +1% per 1,000 coins of cargo, +1% per 1,000 coins carried aboard (banked coins don't count); +5% if carrying contraband; fame adds; risky routes x2; cap of 50% applied LAST.
- Pre-fight options: Fight, Run (wind check vs. pirate speed), Pay toll (counts as a loss), Bluff (false flag, Cunning check), Dump cargo as bait (at least 10% of hold value).
- Intimidation: if your fleet strength is at least 2x theirs, they flee; between 1x and 2x the flee chance scales with your advantage. Named adversaries and desperate crews never flee. Fleeing pirates drop no loot, but you can give chase.
- A beaten crew may return tougher as the same crew. Never promoted into a new adversary; rivals come only from the story and the fixed roster.
- Ambush types rotate with no repeat twice in a row: fog, false distress, reef trap, night raid, storm chase, named adversary.
- Surrender: the enemy surrenders when its crew drops below 40%, or when it's disabled and you have more crew. Reward: the ship and all its cargo, and some of its crew may join you. Only boarding gives the captain, his strongbox, and the charm roll, and boarding is usually the faster finish.
- Tuning (Oct 14): enemies start fleeing at 15% hull; an escape succeeds 20% of the time; escape range is 400; a chain shot to a fleeing ship's sails almost always stops it. Target fight length is 60-150s. At 150s the pirate pays you a toll, which counts as a WIN with half the loot.
- Hull weak spot (v0.6): once enemy hull is under 50%, a glowing breach appears at the waterline on the side facing you for 6s, every 15s. Round shot aimed at Hull while it glows does 2x hull damage. Sinking adds a short cinematic. Sinking gives sinkCargoPct loot; a ship that sinks while its breach is glowing also drops floating crates you can sail through to collect.
