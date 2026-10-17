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

## v0.7: Captain Rashid of the Red Dhow + grand contracts (gamedev/v07-rival-contracts)
| Flag | Env var | Effect | Log line |
|---|---|---|---|
| `-gbredDhowNext` (also `-gbreddhownext`) | `GB_REDDHOW_NEXT=1` | The Red Dhow is in the next port you dock at (one arrival; ignores the moon-3 gate and the 1-in-4 roll). He buys that port's cheapest good (stock halved, price +20% until the next week), the ticker says "The Red Dhow is in port.", his taunt card shows, the tavern has his in-port line and the rumour of where he heads next. | `GB RIVAL force: ...`, `GB RIVAL arrival <port> ... forced=True ... -> IN PORT`, `GB RIVAL bought the cheapest good ...`, `GB RIVAL heads for <port>` |
| `-gbracecontract` | `GB_RACE_CONTRACT=1` | The first time you are in a board port (Gwadar, Debal, Muscat), one of its contract notices becomes a race (red-sail icon; Rashid chases it). The forced race is re-posted on board refreshes that moon until a race is taken, so it survives the Merchant re-post when combined with `-gbendgamecontract`. Also opens the board during the opening. | `GB RIVAL race contract posted (forced): ...`; on accept `GB RIVAL race on: <id> to <port>, Rashid arrives week N`; then `GB RIVAL race won by the player` / `GB RIVAL race lost by the player` |
| `-gbendgamecontract` | `GB_ENDGAME_CONTRACT=1` | Jumps to the first grand contract: sets Merchant rank (300 Renown), starts the 3-contract sequence and opens the board. | `GB GRAND force applied: rank Merchant, offer eg_big1`, `GB GRAND offer 1/3 ...`, `GB GRAND accept/delivered/failed ...`, `GB GRAND run complete` |
| `-gbforce v07_test` | | Scripted v0.7 self-test (needs `-gbsavedir` under /tmp); also runs inside `-gbforce qa_run`. Screenshots in `<savedir>/v07test`. | `GB V07TEST PASS|FAIL ...` |

Other v0.7 log lines: `GB RIVAL sea: the Red Dhow is on the <port> leg` / `GB RIVAL sea show` (red-sailed dhow at sea on the leg to his next port), `GB RIVAL visit over`, `GB RIVAL moved on`.
Tunables: `Assets/Resources/rival_data.json` (rival) and `endgame_data.json` → `endgameContracts` (grand contracts).

## v0.7.1 (dock watchdog, load testing)

| Flag | Env | Effect |
|---|---|---|
| `-gbstall <ms>` | `GB_STALL=<ms>` | Simulated machine load: about 30% of frames sleep 0..ms real ms (fixed seed). Logs `GB STALL on`. For qa under load only. |

Log lines: `GB DOCK RECOVER (<why>): stage <n>, at <port>, bound for <port>`: the dock watchdog finished a stuck arrival. This happens when the player is `sailing` with no voyage running (or a voyage with no heartbeat for 30 s) and no card, modal, fight, pause card or title for 8 s real time. A normal qa run must log none. `GB COROUTINE Voyage failed: <exception>`: an exception stopped the voyage coroutine. The watchdog then completes the dock. `GB ARRIVAL hold: time frozen with no pause card, ending the hold`.
