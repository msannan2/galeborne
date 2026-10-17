# Galeborne debug flags (desktop / test builds)

Pass these to the player on the command line, for example
`DISPLAY=:5 Builds/Linux_encounter_balance/Galeborne.x86_64 -gbsavedir /tmp/sv_test -gbforcehail warning -logFile /tmp/play.log`

| Flag | Env var | Effect | Log line |
|---|---|---|---|
| `-gbforceevent <id>` | `GB_FORCE_EVENT` | The next voyage gets this encounter (one voyage only). ids: `pirate wreckAdrift merchant squall rumor pearlDiver leakyDhow customsLaunch squallLine lighthouse` | `GB VEVENTS force next event: <id>` then `GB VEVENTS forced event this voyage: <id>` |
| `-gbforceevent hailWarning` / `hailTrade` / `hailRumour` | `GB_FORCE_EVENT` | Alias for `-gbforcehail warning|trade|rumour` | as below |
| `-gbforcehail warning|trade|rumour` (`rumor` accepted) | `GB_FORCE_HAIL` | The next voyage's first decision card is "Sail on the horizon". Tap **Hail her** and the Hail pays that kind (one voyage only). | `GB HAIL force next voyage: <kind> ...`, `GB HAIL forced this voyage: <kind>`, `GB HAIL forced: horizon card first`, `GB HAIL payoff <kind>` |

Notes
- The Hail **warning** names a port with ambush risk ahead. On a leg into Gwadar there is none, so it falls back to rumour (`GB HAIL warning: no ambush risk anywhere -> rumour`). Test it on e.g. Debal -> Muscat.
- After a warning, a pirate pick on that voyage is steered wide 50% of the time (`GB HAIL warning: steered wide, no ambush`).
- A blockade hail reveal (`GB BLOCKADE heard hail ...`) takes precedence over the payoff when a blockade is active on the route.

## Encounter roll (v0.7, gamedev/encounter-balance)
One roll per voyage: `encounterChance` (0.5 in `galeborne_settings.json`). If it hits, one pick from the weighted pool of 10
(pirate 35, wreck 20, merchant 20, squall 15, rumor 10, plus the route-gated new events). The family just shown is not drawn again,
and pirates never come twice in a row (`lastFamily` + saved `lastEncFamily`). Only the scripted story fog ambush bypasses the pool.
Each voyage logs `GB VEVENTS weights ...` (an `x` after a weight = excluded by no-repeat) and, when the encounter shows, `GB VEVENTS pick <id> ...`.
