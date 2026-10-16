# Voyage events: variants, 5 new events, Hail payoffs
Source read: `origin/gamedev/endgame-fixes` @ 1467a0b, `Game.cs:563-614` (`VoyageDecision`, `Encounter`). Machine-readable: `voyage_events.json`. Tester report: `shots/fun_t19/fun_report.md`. Game Designer sign-off changes applied (lighthouse auto-take, customs Heave to, families).

## Important: there are no text keys today
All voyage card text is hardcoded C# string literals in `Game.cs` (only the ambush cards and rumours live in `v06_events_content.txt`). The JSON is keyed by event id with fields named after each `Ask(title, body, options)` call and its `msg` outcome lines, so Game Developer needs a small loader (pick a random variant per card) to drop it in. Placeholders `{good}`, `{goods}` (= `GoodCount`), `{n}`, `{unit}`, `{cost}`, `{port}`, `{lost}`, `{fee}` match the values those lines already use.

## Rules for Game Developer
- **Never the same event family twice in a row** (Game Designer). Every event has a `family` field; compare families, not ids, so Storm front, Squall and Monsoon squall line (all `weather`) never follow each other, nor Wreckage and Wreckage adrift (`salvage`). Today `voyageKindsUsed` only blocks repeats *within one voyage*; keep a `lastFamily` across voyages (save it) and reroll on a match.
- **Wreckage with a full hold:** show the card with Salvage greyed and labelled "Hold full" (Game Designer), or skip the event; never a live dead button.
- **Hail her always pays:** replace the 35% goodwill roll with a weighted outcome card: rumour 50, trade 30, warning 20.
- **Story cargo:** every loss uses `LosableGoods()`, including the customs search and the lighthouse supplies.
- **Lighthouse supplies are automatic:** take 1 unit of the cheapest good in `LosableGoods()` (no picker). With no losable goods, grey the option with "Nothing to spare".
- Variants change wording only; outcomes stay exactly as coded.
- Merchant: dropped the false "cheaper than any quay" claim.

## Families
| Event | Family |
|---|---|
| horizon | horizon |
| storm | weather |
| wreckage | salvage |
| wreckAdrift | salvage |
| merchant | merchant |
| squall | weather |
| rumor | rumour |
| pearlDiver | pearlDiver |
| leakyDhow | leakyDhow |
| customsLaunch | customsLaunch |
| squallLine | weather |
| lighthouse | lighthouse |

## (a) Existing events: variant counts
- `horizon` (Game.cs:572 VoyageDecision kind 0): 3 variants
- `storm` (Game.cs:579-583 kind 1): 3 variants
- `wreckage` (Game.cs:585-588 kind 2): 3 variants
- `wreckAdrift` (Game.cs:598-601 Encounter wreck): 2 variants
- `merchant` (Game.cs:602-607 Encounter merchant): 3 variants
- `squall` (Game.cs:608-610 Encounter squall): 2 variants
- `rumor` (Game.cs:611-613 Encounter rumour): 3 variants

## (c) Hail her payoffs
| Outcome | Weight | Mechanic |
|---|---|---|
| rumour | 50 | pick port p + good g from p.want (shortage, price[p,g]*=rumorSpike) or p.cheap (glut, existing glut effect); add to news |
| trade | 30 | same as merchant offer: good from the hailing ship's home port cheap list, unit=basePrice[dest,g]*0.8, q<=min(4, free hold, coins cap); Decline always allowed |
| warning | 20 | lower this voyage's ambush chance by half ('you steer wide'); text names the real danger |

Rumour goods must come from the target port's real lists (shortage: `want`; glut: `cheap`), so every tip can actually be acted on.

## (b) 5 new events

### Diver in trouble (`pearlDiver`, family `pearlDiver`)
When: any voyage touching Gwadar or Muscat (pearl ports)

- Body v1: A pearl diver clings to a broken float, waving. His boat is gone.
- Body v2: A diver drifts on a spar, shouting for help. A pouch hangs from his neck.

| Option | Cost | Gain | Outcome |
|---|---|---|---|
| Haul him aboard | +1 week | 1 pearl (if hold has room, else +60 coins) | He presses a pearl into your hand. A week lost. |
| Toss him a float | 10 coins | rumour: pearls dear in Debal | He shouts thanks: Debal pays well for pearls. |
| Sail on (safe) | - | - | You leave him to the next boat. |

### Leaky date dhow (`leakyDhow`, family `leakyDhow`)
When: route to/from Gwadar or Muscat (date ports)

- Body v1: A sinking dhow full of dates. Her nakhuda sells cheap before she goes.
- Body v2: A dhow lists hard, bailing. 'Dates, half price, take them!'

| Option | Cost | Gain | Outcome |
|---|---|---|---|
| Buy {n} dates ({cost}) | coins (0.5 x Gwadar date price) | up to 5 dates | Bought {goods} as she settles. · full: Hold full |
| Take her crew off | +1 week | rumour: salt dear in Debal | Her crew tell you salt sells high in Debal. A week lost. |
| Sail on (safe) | - | - | You leave them bailing. |

### Customs launch (`customsLaunch`, family `customsLaunch`)
When: only when destination is Muscat

- Body v1: A Muscat customs launch signals you to heave to for search.
- Body v2: The Sultan's launch closes in, a clerk waving his ledger.

| Option | Cost | Gain | Outcome |
|---|---|---|---|
| Heave to, they search the hold | +1 week; banned non-story goods seized (LosableGoods, never story cargo) | - | They search, stamp, and wave you on. |
| Pay the fee (safe) | 5% of coins, min 20, max 150 | - | The clerk pockets {fee} coins and looks away. |
| Run for it | 55%: nothing; 45%: hull -10 and fee x2 | - | You outrun her. / A shot holes your hull. They fine you {fee}. |

### Monsoon squall line (`squallLine`, family `weather`)
When: any; more likely moons 6-9 (monsoon) if a season exists, else flat weight

- Body v1: A long grey line of squalls lies across your course.
- Body v2: The monsoon throws a wall of cloud across the horizon.

| Option | Cost | Gain | Outcome |
|---|---|---|---|
| Sail around it (safe) | +1 week | - | A long detour, but dry decks. |
| Cut through | 40%: lose 1-2 losable goods | - | Soaked but whole. / The squalls take {lost}. |
| Heave to | hull -5 | - | You wait it out. The rigging groans. |

### Lighthouse keeper (`lighthouse`, family `lighthouse`)
When: route via Ras al Hadd (Gwadar-Muscat, Debal-Muscat)

- Body v1: The keeper at Ras al Hadd waves a lamp. He trades news for supplies.
- Body v2: A lone keeper rows out from his tower. 'News for coin, captain?'

| Option | Cost | Gain | Outcome |
|---|---|---|---|
| Buy news (15) | 15 coins | real rumour (shortage at a want port) | He says {port} is short of {good}. |
| Give him supplies | 1 unit of your cheapest losable good | pirate warning: next voyage ambush chance halved | He takes {goods} and warns you where the raiders wait. · greyed with no losable goods: Nothing to spare |
| Sail by (safe) | - | - | You dip your flag and sail past. |

## Self-check
Limits: title <=19 chars, body <=90 chars after filling placeholders with worst-case values ("3 baskets of dates", "Incense", "Gwadar", 3-digit prices), <=3 options.

| Card | Title | Body | Options | Result |
|---|---|---|---|---|
| horizon v1 body | 19 | 80 | 2 | PASS |
| horizon v2 body | 16 | 58 | 2 | PASS |
| horizon v3 body | 17 | 55 | 2 | PASS |
| storm v1 body | 11 | 74 | 2 | PASS |
| storm v2 body | 13 | 67 | 2 | PASS |
| storm v3 body | 13 | 62 | 2 | PASS |
| wreckage v1 body | 8 | 85 | 2 | PASS |
| wreckage v1 bodyFull | 8 | 38 | 2 | PASS |
| wreckage v2 body | 7 | 72 | 2 | PASS |
| wreckage v2 bodyFull | 7 | 51 | 2 | PASS |
| wreckage v3 body | 14 | 80 | 2 | PASS |
| wreckage v3 bodyFull | 14 | 50 | 2 | PASS |
| wreckAdrift v1 body | 15 | 49 | 1 | PASS |
| wreckAdrift v1 bodyFull | 15 | 42 | 1 | PASS |
| wreckAdrift v2 body | 12 | 60 | 1 | PASS |
| wreckAdrift v2 bodyFull | 12 | 44 | 1 | PASS |
| merchant v1 body | 16 | 52 | 2 | PASS |
| merchant v2 body | 12 | 74 | 2 | PASS |
| merchant v3 body | 17 | 77 | 2 | PASS |
| squall v1 bodyLoss | 6 | 70 | 1 | PASS |
| squall v1 bodySafe | 6 | 65 | 1 | PASS |
| squall v1 bodyEmpty | 6 | 69 | 1 | PASS |
| squall v2 bodyLoss | 13 | 63 | 1 | PASS |
| squall v2 bodySafe | 13 | 47 | 1 | PASS |
| squall v2 bodyEmpty | 13 | 71 | 1 | PASS |
| rumor v1 body | 17 | 75 | 1 | PASS |
| rumor v2 body | 18 | 61 | 1 | PASS |
| rumor v3 body | 17 | 62 | 1 | PASS |
| hail rumour v1 bodyShort | 18 | 61 | - | PASS |
| hail rumour v1 bodyGlut | 18 | 69 | - | PASS |
| hail rumour v2 bodyShort | 15 | 71 | - | PASS |
| hail rumour v2 bodyGlut | 15 | 67 | - | PASS |
| hail trade v1 body | 16 | 63 | 2 | PASS |
| hail trade v2 body | 14 | 64 | 2 | PASS |
| hail warning v1 body | 9 | 65 | - | PASS |
| hail warning v2 body | 14 | 64 | - | PASS |
| NEW pearlDiver v1 | 16 | 65 | 3 | PASS |
| NEW pearlDiver v2 | 16 | 73 | 3 | PASS |
| NEW leakyDhow v1 | 15 | 70 | 3 | PASS |
| NEW leakyDhow v2 | 15 | 59 | 3 | PASS |
| NEW customsLaunch v1 | 14 | 59 | 3 | PASS |
| NEW customsLaunch v2 | 14 | 57 | 3 | PASS |
| NEW squallLine v1 | 19 | 52 | 3 | PASS |
| NEW squallLine v2 | 19 | 54 | 3 | PASS |
| NEW lighthouse v1 | 17 | 68 | 3 | PASS |
| NEW lighthouse v2 | 17 | 64 | 3 | PASS |

**Overall: ALL PASS** (46 card bodies checked). Longest option label: "Heave to, they search the hold" (30 chars, 3-option card).

### Trade-off check (new events)
| Event | Every option costs/gains? | Safe dull option |
|---|---|---|
| pearlDiver | week / 10 coins / nothing | Sail on |
| leakyDhow | coins / week / nothing | Sail on |
| customsLaunch | week + search / fee / risk hull + fee | Pay the fee |
| squallLine | week / cargo risk / hull | Sail around it |
| lighthouse | coins / 1 cheapest losable good (auto) / nothing | Sail by |

### Goods validity (fixed goods named in text)
| Text | Good | Port | Code list | OK |
|---|---|---|---|---|
| pearlDiver gift | pearls | from Gwadar/Muscat waters | Gwadar, Muscat `cheap` | yes |
| pearlDiver rumour | pearls dear in Debal | Debal | Debal `want` | yes |
| leakyDhow | dates | Gwadar/Muscat routes | Gwadar, Muscat `cheap` | yes |
| leakyDhow rumour | salt dear in Debal | Debal | Debal `want` | yes |
| Hail/lighthouse/rumour | {good} at {port} | runtime | must draw from that port's `want` (shortage) or `cheap` (glut) | rule |

The old rumour card (`Game.cs:612`) already draws from `want`, so it passes.
