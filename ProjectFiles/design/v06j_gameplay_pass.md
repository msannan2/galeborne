# v0.6j Gameplay Pass — Journal, Quests, Towns

Leads from Sannan's feedback and `v0j_job_reference.md`. Ship after v0.6i.

## 1. Journal (replaces map markers after opening)

- Opening keeps the existing goal markers / three-goal HUD until Muscat sale + Yusuf seal.
- After opening: **map quest markers off**. All missions live in a **Journal** (parchment card, teak-and-brass frame).
- Journal tabs: **Active**, **Available** (optional), **Done**.
- Each entry shows: title, giver, destination, reward (coins / standing / Honor / Fear), deadline if any, risk line for contraband.
- Optional **Hint** button on an active job: one soft nudge (e.g. "Ask at Debal's temple"), never a map pin.
- Three-goal HUD stays as a short summary of the top active job + long story goal; detail is in the journal.

## 2. Quest menu (accept / decline)

- Talking to a keeper opens a **quest menu**, not an auto-accept.
- List 1–3 offers that match that building and the player's Honor / Fear / Sultan standing (see job reference).
- Rewards and risks are listed **before** Accept. Decline closes with no penalty.
- Max **3 active jobs** at once (story long-goal does not count toward the cap).

## 3. Building keepers (every building has someone)

Ship the first wave of talkable keepers with at least one job type each:

| Building | Keeper | First jobs to ship |
| --- | --- | --- |
| Palace / Governor | Governor or Sultan | Escort, bounty, official delivery |
| Tavern | Innkeeper | Rumour delivery, smuggling tip (Honor = fewer / better terms / bigger catch penalty) |
| Market / Guild | Guild factor | Bulk delivery, shortage run |
| Temple | Priest | Pilgrim passage, alms cargo |
| Shipwright / Harbour | Shipwright or harbour master | Timber run; harbour clearance if no yard |
| Back alley | Farida | Banned rugs (ally) / turn-in once / rival sabotage |

Exact job tables: `v0j_job_reference.md`.

## 4. Town service mix (layouts can wait)

Towns feel different by **services**, not full custom maps in j:

| Town | Shipwright | Temple | Alley | Notes |
| --- | --- | --- | --- | --- |
| Gwadar | Repair only | Yes | No | Home port; no ship sales |
| Debal | Full (repair + sell small hulls) | Yes | Farida | Tutorial / smuggling hub |
| Muscat | None (harbour master only) | Yes | Later | Court / customs capital |

Landmark art from Chief Art Designer sits at the quay so towns still read apart while layouts stay shared.

## 5. Honor rules (Jeff's change)

- High Honor never locks smuggling.
- Honourable captains get **fewer** shady offers, at **better coin terms**, and take a **bigger standing / Honor hit** if customs catches them.

## 6. Out of scope for j (later)

- Full unique town layouts / tap-around streets
- Peacock mission, Omar's beads, Qarin warehouse ending
- Free-model visual upgrade (ships / townsfolk) — parallel art track, not gated on this pass

## Build order for Game Developer

1. Journal UI + move post-opening missions into it; strip map markers after opening.
2. Quest menu accept/decline on one keeper (tavern rumour) as the template.
3. Roll the menu to palace, market, temple, shipwright/harbour, Farida.
4. Apply town service mix table so missing buildings simply aren't there.
5. Wire Honor fewer/better/riskier smuggling terms.

Settings keys and numbers can live in `design/v06_settings.json` once the skeleton is in.
