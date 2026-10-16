# Galeborne v0.6: first-20-minutes gap analysis v1 (Game Researcher, 2026-10-14)

Compared: `v06_first_20_minutes.md`, plus `hasan_act1_dialogue_v1.md`, `v06_settings.json` and `player_feedback_v1.md`.
How the sources were checked: every quote below is word for word from the page text that WebSearch returned for that URL on 2026-10-14. I didn't open the pages separately with WebFetch, so re-check a quote before using it outside the team. There are no invented numbers. The Steam and app-store review totals in `player_feedback_v1.md` are not repeated here.
Ranking: most likely to make a new player quit in session one comes first.

---

## 1. The opening doesn't match the story script, so the first 2 minutes are undefined
**Problem.** The 20-minute plan opens with "Hasan docks at Gwadar at dawn." The Act 1 dialogue opens with a burning-dhow duel (Beat 1), then the market tutorial with Yusuf in **Debal** (Beat 2). Right now we have two different first scenes, a market tutorial in two places, and an unlosable duel that comes before the sea-combat tutorial at minutes 13-16. Players judge the hook in the first minute, so whichever version ships by accident is a risk.
**Evidence.** Good openings throw you into the world and then teach in place. Uncharted Waters Origin's two intros were "quite prompt placing you into the world efficiently, leaving you informed and ready to play" (https://derekdeck.wordpress.com/2023/01/23/uncharted-waters-origin-expanded-review/). Dredge's wreck-then-repay-the-debt opening gives you a motive before any lesson (https://www.rockpapershotgun.com/dredge-review).
**Fix.** Put it in writing: minute 0-1.5 is the burning-dhow duel (unlosable, about 3 taps, and it doubles as the first combat lesson), cut to Hasan limping into Gwadar at dawn. Move Yusuf's "buy where it's piled" lesson to the Gwadar bazaar, or make the Debal visit at minutes 6-9 the selling lesson. Update both docs to the same timeline.

## 2. Tutorial wall: too many systems before the payoff
**Problem.** In 20 minutes we teach buying, selling, the map, rumors, warehouses, contraband, heat and forged papers, the tithe, customs odds, ambushes, two ammo types, boarding and the story letter. That's 10+ systems. Players and critics say long, dry instruction is what makes trading games feel like homework.
**Evidence.** Port Royale 4's tutorial is "about an hour long and painfully dry … I legitimately felt like I was taking some sort of expedited college course" (https://www.thegamer.com/port-royale-4-review/). Its tutorials "demonstrate the mechanics … but they almost never land the concept or reason for doing it" (https://bigbossbattle.com/port-royale-4-is-no-caribbean-dream-but-it-sure-kills-a-lot-of-time/). Merchant of the Skies is "menus, trading posts, resources, upgrades, energy, quests … all these little systems thrown at you almost immediately" (https://gamingonphone.com/reviews/merchant-of-the-skies-review-a-cozy-trading-sim-that-grows-on-you/).
**Fix.** Every lesson must pay off within 60 seconds, with no text boxes over 2 lines. Hide the temple tithe and the warehouse lease until after the Muscat jackpot: Farida's crate can ride in the hold and the warehouse can come in session 2. Teach one ammo type in the first ambush, chain shot (grape comes in ambush 2). Explain each mechanic in one sentence that says why it matters ("Papers lower the customs odds"), not how to use the menu.

## 3. One punishing loss can wipe out the first session
**Problem.** We protect the ambush (gunpowder is spared) but not customs. With the papers, the doc puts customs odds at about 15-20%, and on the first run "the gunpowder is confiscated, coins are kept." So roughly 1 in 5 new players spend 15 minutes building toward the jackpot and get nothing. Game Tester's math also says the expected return is about 2×, not 3×. The sailing genre's biggest quit story is "slow start, then lose everything."
**Evidence.** Sunless Sea: "got tired of all the slow starts only to lose everything in an instant, uninstalled" (https://www.reddit.com/r/patientgamers/comments/1hpr9un/sunless_sea_sunless_skies_so_much_potential_but/). Also "I hate the early stages … so punishing considering I've done it now four times" (https://www.reddit.com/r/sunlesssea/comments/4th7y4/ive_given_up_trying_to_play_on_default/). TechRaptor: "very, very easy to lose the game at any point … it's more frustrating than immersive" (https://techraptor.net/gaming/reviews/sunless-sea-review).
**Fix.** First run only: show the real odds (about 18%) and play the slow-motion check, but script it to pass. Or, if caught, the officer takes a bribe and you keep half the gunpowder to sell at 2×. Flagged runs stay random. Save the real loss for voyage 4+, after the player has felt one win. Add a `firstRunCustomsAlwaysPass` flag to `v06_settings.json`.

## 4. The economy is opaque at the moment you need to read it
**Problem.** The jackpot depends on the player understanding that a rumor means a price spike, that "3× with a 1% drop per unit" means don't dump everything, and that heat means customs odds. If those numbers live in menus, players won't connect them.
**Evidence.** Port Royale 4: you "have to click on each one to determine the prices … you need a spreadsheet and a calculator" (https://www.thegamer.com/port-royale-4-review/). Merchant of the Skies: "It took me three starts to understand where was the fuel gauge… Trade tax needs more explanation" (https://steamcommunity.com/app/1040070/discussions/0/1643170269568540658/), and it "never takes the time to properly explain its core mechanics" (https://goombastomp.com/merchant-of-the-skies-review/).
**Fix.** At the market slider, show a live "You'll make +X coins in Muscat (rumored)" line and a falling-price curve as the slider passes the 1%-per-unit drop. Show customs odds as one coloured badge on the route card. Never require a second screen to make the opening decision.

## 5. A stuck point or an unclear next step
**Problem.** We list short, medium and long goals, but the doc doesn't say what happens if the player wanders: sails to the wrong port, sells the gunpowder in Debal, or runs out of coins after buying dates.
**Evidence.** Dredge: "More than once we knew what we were supposed to do but had no idea how to do it. A more generous helping hand from a hint-dropping NPC wouldn't have gone amiss" (https://www.nintendolife.com/reviews/switch-eshop/dredge). Port Royale 4 preview: "I also hope the main mission or goal becomes clearer, as aimlessly sailing … may not be everyone's cup of tea" (https://www.thegamer.com/port-royale-4-steam-preview-management/).
**Fix.** Pin the short goal as a tappable chip that highlights the next building or route. After 45 seconds idle, a keeper nudges ("Muscat pays triple for that powder, Captain"). Add guards: a floor of starting coins so the player can always afford the gunpowder, and a warning when selling banned goods anywhere but Muscat on run one. Add "idle more than 45 seconds" and "coins below the gunpowder price" as stuck flags in the v0.6c playthrough log.

## 6. Ads or monetisation showing up in the first session
**Problem.** `adPlacements` includes `earlyRumor` and `skipVoyage`, and the caps allow 3 ads per session. A prompt to "watch an ad for an early rumor" during the scripted rumor beat, or one to skip the 30-45 second voyage, would land inside the first 20 minutes and cheapen the jackpot. Players are quick to call mobile trading games "pay-to-win."
**Evidence.** Uncharted Waters Origin, a top-rated Steam review: "The item shop is... extensive … there's around 12 of them at the moment, all at once" and "the game has this 'you need to go there... BUT NOT YET' vibe" (https://steamcommunity.com/app/1574360/reviews/?browsefilter=toprated). Prior pay-to-win evidence is in `player_feedback_v1.md`, item 3.
**Fix.** Add `adFreeUntilVoyage: 4` (or until the Muscat payout) to the settings. Show no ad buttons at all in the first session, and don't use `earlyRumor` while the scripted rumor is active.

## 7. Slow sailing and dead time
**Problem.** Sailing takes 30-45 seconds per leg, with 2× fast-forward. We have three legs plus the ambush in 20 minutes. That's fine if something happens on the way, but empty water is the most common sailing-game complaint.
**Evidence.** Sunless Sea: "how SLOW it was going from place to place. Completely ruined the mood" (https://www.reddit.com/r/patientgamers/comments/1hpr9un/sunless_sea_sunless_skies_so_much_potential_but/). Also "each life starts off with 15-20 minutes of 100% repeat tasks … The pace is absolutely glacial" (https://www.reddit.com/r/Games/comments/2v88x8/lets_talk_about_sunless_sea_is_it_overhyped/).
**Fix.** Cap the first-session legs at about 20 seconds, and allow fast-forward from the start. Fill every leg with one beat: a crew joke, a dolphin or a distant ship, or the keeper's next hint. Never make the player sail the same leg twice in the first 20 minutes. The Debal round trip is the risk.

## 8. The same opening on every replay
**Problem.** The rumor and ban are scripted on the first playthrough. If a player restarts, which is common after a bad first session, they replay the exact same 20 minutes. That's fine for run one, but a second play-through of identical tutorial is a known turn-off.
**Evidence.** Sunless Sea: "each time I restarted I felt like I was going through basically the same details again" (https://www.reddit.com/r/patientgamers/comments/1hpr9un/sunless_sea_sunless_skies_so_much_potential_but/). Tradewinds Legends: "every character's story is almost the same" (in `player_feedback_v1.md`).
**Fix.** After the first full playthrough, offer "Skip the opening": start at Gwadar with the first-run rewards and a random rumor. Make all tutorial prompts skippable.

---

## What good openings do (to keep)
- **The fiction gives you a reason to earn.** Dredge's debt and our burned ship and Qarin seal are good. Keep the seal in minute 1.
- **An early, safe, repeatable loop.** Guides for Dredge recommend short daylight loops "without overextending" (https://casualgameguides.com/walkthroughs/dredge/first-steps-the-marrows). Our pirate-free voyages 1-2 match this.
- **Forgiving early economy.** Potion Craft's first hour is described as forgiving, with no deadlines, and failed sales only cost reputation (https://techraptor.net/gaming/reviews/potion-craft-alchemist-simulator-review).
- **Get sailing fast.** Pirates! starts you on a sloop outside a port with an easy first run (https://www.gamespot.com/reviews/sid-meiers-pirates-review/1900-6114066/).
