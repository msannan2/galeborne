# v0.6j Job Reference — Building Keepers

Short design reference for Game Designer’s journal / quest-menu pass.  
Galeborne: Arabian Sea / Indian Ocean trade (Debal, Gwadar, Muscat). Stats that gate jobs: **Honor**, **Fear**, **Sultan standing**.

## How jobs work (v0.6j)

- After the opening tutorial, **map quest markers go away**. Missions live in the **journal**.
- Keepers offer jobs from a **quest menu**; player **accepts or declines**. Rewards are listed **up front** before accept.
- Accepted jobs appear in the journal with destination, deadline (if any), and reward.
- Optional **hint** button is the only navigation help after the opening.
- **Towns differ by service mix**: some have no shipwright; some repair only; only a few sell ships. Job lists should respect which buildings exist in that town.

### Genre precedent (confident only)

- **Tradewinds Legends**: building keepers hand out delivery / fetch jobs; accept puts the job on your ledger.
- **Uncharted Waters**: governors issue escort and bounty work; taverns tip smuggling and rumour jobs; temples and guilds match their role.
- **Sid Meier’s Pirates!**: governors give named missions (escort, hunt pirate, deliver); taverns give leads; reward is stated when you take the job.

No invented numeric thresholds below — only which stat **gates or unlocks** the offer. Exact cutoffs are for Game Designer / balance pass.

---

## 1. Governor / Sultan’s palace

**Keeper:** Governor (town) or Sultan’s court (capital / Muscat-tier).

| Job type | What it is | Reward type | Gate / unlock |
| --- | --- | --- | --- |
| Escort convoy | Guard a merchant vessel or caravan to a named port | Coins + Sultan standing | Unlocks with **positive or rising Sultan standing**; better escorts at higher standing |
| Bounty / pirate hunt | Hunt a named raider or report a wreck | Coins + Sultan standing; small **Fear** bump if you take the violent option | Available early; higher bounties prefer **Fear** or proven combat (post-ambush) |
| Official delivery | Carry sealed papers or tribute under deadline | Coins + Sultan standing | **Sultan standing** unlocks better payloads; failure hurts standing |
| Harbour inspection | Spot-check a rival’s hold or report contraband | Coins + Sultan standing | Mid **Sultan standing**; conflicts with Farida ally path if target is her goods |
| Court favour | One-off: attend, gift, or speak for a house | Sultan standing (primary); modest coins | High **Honor** or high **Sultan standing** |

**Notes:** Palace jobs should never ask you to openly smuggle. That stays with the alley / tavern. Precedent: governors in Pirates! / Uncharted Waters.

---

## 2. Tavern

**Keeper:** Innkeeper / information broker.

| Job type | What it is | Reward type | Gate / unlock |
| --- | --- | --- | --- |
| Rumour delivery | Carry a tip or letter to another tavern | Coins; sometimes a map hint | Always available; no gate |
| Smuggling tip | Off-books cargo run (small hold) | High coins; **Fear** up or **Honor** down on success | Unlocks with **Fear** or after meeting Farida; high **Honor**: fewer offers, better terms, bigger reputation hit if caught |
| Debtor collection | Collect or forgive a debt in another town | Coins; **Honor** if you forgive, **Fear** if you lean hard | Mid game; choice splits Honor/Fear |
| Crew hire lead | Find a specialist sailor for your dhow | Crew perk (not coins) | After first ambush / first voyage |
| Quiet passage | Move a passenger who does not want papers checked | Coins; slight **Fear** | **Fear** gate; conflicts with high **Sultan standing** jobs |

**Notes:** Tavern is the soft entry to grey-market work. Precedent: Uncharted Waters tavern tips; Pirates! tavern leads.

---

## 3. Merchant guild / market

**Keeper:** Guild factor or master merchant.

| Job type | What it is | Reward type | Gate / unlock |
| --- | --- | --- | --- |
| Bulk delivery | Haul a listed good to a named market under deadline | Coins (markup stated upfront) | Always; better contracts with **Honor** or guild reputation |
| Shortage run | Fill a sudden shortage (spice, copper, dates) | High coins for speed | Time-limited; no heavy gate |
| Price arbitration | Buy low here, sell at agreed floor there | Fixed coin fee + your trading profit | **Honor** preferred (honest broker) |
| Rival undercut | Deliver before a named rival | Coins; small **Fear** if you sabotage | **Fear** unlocks the dirty variant |
| Warehouse bond | Store and later release guild goods | Coins + warehouse access flavour | After warehouse unlock in story |

**Notes:** Pure trade jobs. Fits Tradewinds-style ledger work. Do not duplicate Farida’s banned rugs here — those stay alley.

---

## 4. Temple (priests)

**Keeper:** Priest / temple elder. (Every town with a temple gets a talkable keeper.)

| Job type | What it is | Reward type | Gate / unlock |
| --- | --- | --- | --- |
| Pilgrim passage | Carry pilgrims safely to another temple town | Coins + **Honor** | **Honor** unlocks; higher Honor = more pilgrims / better pay |
| Relic run | Deliver a relic or sacred gift under escort rules (no violence preferred) | Coins + **Honor**; rare cosmetic / blessing | Mid–high **Honor** |
| Alms cargo | Distribute food or cloth to a poor quay | Modest coins + **Honor** | Always for low Honor players as a climb path |
| Sanctuary escort | Protect a refugee from bounty hunters | **Honor** strong; coins modest | High **Honor**; conflicts with active bounty jobs |
| Offering voyage | Sail an offering to a festival on a fixed date | Coins + **Honor** + Sultan standing if court-sponsored | Calendar / festival beat; **Honor** or **Sultan standing** |

**Notes:** Temple jobs should feel clean and reputation-heavy. Precedent: Uncharted Waters-style temple / church missions (pilgrim / sacred cargo), adapted to Arabian Sea setting.

---

## 5. Shipwright / harbour master

**Keeper:** Shipwright and/or harbour master.  
**Town variance:** Some towns have **no shipwright**; some **repair only**; few **sell ships**. Only offer jobs that match services present.

| Job type | What it is | Reward type | Gate / unlock |
| --- | --- | --- | --- |
| Timber / fittings run | Fetch teak, rope, or sailcloth from another port | Coins + repair discount | Always where a shipwright exists |
| Dry-dock escort | Guard a vessel under tow to the yard | Coins | After player owns a ship upgrade path |
| Harbour clearance | Clear wreckage or report a hazard on a route | Coins + safer sailing on that route (flavour) | Harbour master present |
| Trial sail | Test a new hull / lateen for the yard | Coins or free minor repair | Shipwright that **sells** ships |
| Smuggled fittings | Off-books parts (faster, riskier) | Cheap upgrade; **Fear** up / **Honor** down | **Fear** or Farida ally; not offered in high-security ports |

**Notes:** If the town only repairs, skip “trial sail” and ship-sale jobs. If no shipwright, harbour master can still offer clearance / hazard jobs.

---

## 6. Back alley (Farida)

**Keeper:** Farida (Debal alley; later Muscat alley when present). Ally vs rival changes the menu.

| Job type | What it is | Reward type | Gate / unlock |
| --- | --- | --- | --- |
| Banned rug run | Buy her rugs, sell in Muscat (~3×) | High coins; ally loyalty | Ally path; prices show **−20%** with `PAPERS -20%` in log when ally |
| Grey delivery | Move a sealed crate past customs | High coins; **Fear** | Ally; confiscation risk |
| Fence goods | Offload hot cargo she names | Coins; **Fear** | Ally; blocked if rival |
| Turn-in (one-shot) | Report her to the authorities | Flat coins (e.g. 500) + **Sultan standing**; she becomes **rival** | Available once; locks ally jobs |
| Rival sabotage | After turn-in: undercut or expose her network | Coins + **Sultan standing**; **Fear** | **Rival Farida** portrait / cold stare state |

**Notes:** Alley is the Honor/Fear mirror of the palace. Do not offer palace-clean jobs here. Journal should show risk (customs, standing loss) before accept.

---

## Journal & quest menu checklist

- [ ] Accept / decline UI with reward and risk listed upfront  
- [ ] Journal lists active, completed, failed jobs  
- [ ] Clear medium/short goals still sync with story (customs gunpowder fix from h4 stays)  
- [ ] Markers off after opening; optional hint only  
- [ ] Job pool filtered by town buildings and Honor / Fear / Sultan standing  
- [ ] Farida ally vs rival swaps alley job list and portrait  

## Open for Game Designer

- Exact standing / Honor / Fear thresholds  
- How many active journal jobs at once  
- Whether declined jobs return next visit or rotate out  
