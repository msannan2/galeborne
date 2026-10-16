# v0.6j Journal & Quest Menu Data

Ship-ready copy and rules for Game Developer. Job offers themselves come from Game Researcher's JSON (`v0j_jobs.json` once posted). This file owns the journal UI, quest menu flow, and story-goal sync.

## Journal

**Open from:** pause card, town header, or a "Journal" button on the dock HUD.

**Tabs**
- Active — accepted, not finished
- Done — completed or failed (last 10)

**Entry fields (every job)**
| Field | Example |
| --- | --- |
| title | "Rumour for Yusuf" |
| giver | "Debal tavern" |
| destination | "Gwadar tavern" |
| reward | "40 coins" |
| risk | "" or "Customs may seize cargo" |
| deadline | "" or "Within 2 moons" |
| status | active / done / failed |

**Hint button** (active jobs only): one line of soft help, no map pin. Examples:
- "Ask at Debal's temple."
- "Muscat's guild knows the shortage."
- "Farida still waits in the alley."

**Cap:** 3 active jobs. Story long-goal ("Find out what House Qarin is hoarding.") does not count.

**Declined offers:** leave the keeper's list until the next visit to that building; then rotate to a different offer from the same pool.

## Quest menu (talk to keeper)

1. Portrait + short greeting.
2. List of 1–3 offers matching building + Honor/Fear/Sultan standing.
3. Each row: title, one-line summary, reward, risk if any.
4. Accept → add to journal, close menu, short confirm line.
5. Decline → no penalty, offer rotates next visit.
6. If at job cap: show "Journal is full (3/3). Finish a job first." and disable Accept.

## Opening vs post-opening

| Phase | Map markers | Three-goal HUD | Journal |
| --- | --- | --- | --- |
| Opening (until Muscat sale + Yusuf seal) | On | Short / medium / long as now | Hidden or empty |
| After opening | Off | Top active job title + long story goal | Full |

Customs gunpowder rule from h4 stays: do not advance medium/short past "Get the gunpowder to Muscat" / buy gunpowder until a successful Muscat sale.

## First-wave keepers to wire (copy stubs)

Use these until the full JSON loads. Each Accept adds the matching journal entry.

### Debal tavern — "Rumour for Yusuf"
- Summary: Carry a tip about Qarin buyers to Gwadar's tavern.
- Reward: 40 coins
- Risk: none
- Hint: "Gwadar's tavern keeper will pay for the tip."
- Greeting: "Word travels if you carry it. Interested?"

### Muscat palace — "Escort to Gwadar"
- Summary: Guard a tribute dhow home to Gwadar.
- Reward: 120 coins + Sultan standing
- Risk: ambush chance raised on that leg
- Hint: "Set sail for Gwadar with the tribute boat."
- Gate: Sultan standing ≥ 1 or after opening Muscat sale

### Debal temple — "Alms for the quay"
- Summary: Deliver a small cargo of cloth to Debal's poor quay (local, no voyage).
- Reward: 25 coins + Honor
- Risk: none
- Hint: "The quay is past the temple steps."
- Gate: always

### Debal alley — Farida (existing)
- Ally: banned rug run (prices −20%, log `PAPERS -20%`)
- One-shot turn-in: 500 coins + Sultan standing → rival
- Rival: sabotage job only

### Gwadar harbour — "Timber for the yard"
- Summary: Bring teak from Debal to Gwadar's shipwright.
- Reward: 60 coins + repair discount once
- Risk: none
- Hint: "Debal's market sells teak cheap."
- Gate: Gwadar shipwright present (repair-only towns still offer timber)

## Hull limp rule (Muscat dead-end fix)

- Always leave port at ≥ 25% hull (slower sails).
- Muscat dock: pay to patch to 50%; if broke, free emergency patch to 25% + small harbour debt.

## Out of scope here

Full job pool numbers and unlock curves → Researcher's JSON. Landmark art and free-model visuals → art track.
