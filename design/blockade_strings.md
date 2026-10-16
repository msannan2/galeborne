# Muscat blockade strings

Data for the blockade warning (Game Designer wording, Jeff approved). Machine copy: `design/blockade_strings.json`.

## Tavern rumour (guaranteed)
`rumors[]` entries with `type: "blockade"` in `ProjectFiles/design/v06_events_content.json` (mirrored to `Assets/Resources/Design/v06_events_content.txt`):
1. Sultan's soldiers are searching every hold at Muscat. Copper is being seized.
2. Soldiers board every dhow at Muscat. Any copper aboard is taken.
3. Muscat's quay is crawling with soldiers. They seize copper from every hold.

**Rule:** while a blockade is active at Muscat (`condition: blockadeActive:Muscat`), one of these always appears in the tavern of the port you're leaving (`shownIn: departurePort`, `guaranteed/alwaysShow: true`). It is never rolled; only the wording varies. Give it at least one voyage of warning.

## Other strings
- Hail her warning outcome: "Soldiers search copper at Muscat"
- Sea map plaque (crossed spears on Muscat ring): "Blockade: copper seized"
- Route preview (amber, copper in hold + Muscat destination): "Blockade at Muscat: your copper will be seized."
- Arrival card: title "Blockade at Muscat", body "Soldiers board to seize your copper.", options "Pay them off ({fee} coins)" / "Let them take it", disabled "Not enough coins".
- Fee: about 30% of the copper's value, `ceil(0.30 * qty * price[Muscat,copper])`.

## Hook-in
`GameV06.Rumor()` currently maps `blockade` to `shortage` rumours; change to pick `type=="blockade"`, and don't leave it to the random roll: on departure while blockade is active, insert one random variant into news/tavern talk.
