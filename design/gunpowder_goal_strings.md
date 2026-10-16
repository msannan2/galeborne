# Gunpowder goal: corrected strings (for Game Developer)
Source read: `origin/gamedev/endgame-fixes` @ 1467a0b (read only). Report: `shots/fun_t19/fun_report.md` issue 1.

## What the code actually does (differs slightly from the report)
- **Buy:** in the opening, gunpowder is sold only by **Farida in Debal's Back Alley** (`opening.buyPort = "Debal"`, `openingDebalAlleyStocksGunpowder`; `GameV06.cs:23-24`; priced at Gwadar's price, `GameV06.cs:89`). After the opening the data has Gwadar's alley selling it (`portGoods._backAlley.sells.Gwadar`, `gunpowderGwadarOnlyAfterOpening`).
- **Sell:** only **Muscat's Back Alley** buys it (`_backAlley.buysBanned.Muscat`). The sale sets `openingGpSold` and clears the goal (`Game.cs:106`).
- **Farida** is the Back Alley keeper (keeper key `farida`); her rug offer fires only in Debal (`GameAct1.cs:77`).
- **Why the tester went to Gwadar:** the ban rumour says "Farida in the back alley has some, **at Gwadar's price**" and names no port for Farida. The fallback Now goal literally says "Buy gunpowder in Gwadar" (`GameAct1.cs:30`). The Journal line "Buy gunpowder before the ban bites, then make for Muscat." names no seller at all.
- **Why the goal vanished:** once `!InOpening && JournalUnlocked`, `Goals3()` returns `JournalGoalsSummary()` first (`GameAct1.cs:23`), so the gunpowder lines are never reached. `needGpAgain` also needs `jackpotStartCoins > 0`.

## String changes
| # | file:line (1467a0b) | key | current | new |
|---|---|---|---|---|
| 1 | GameV06j.cs:217 | JournalOpeningGoals[4] | Buy gunpowder before the ban bites, then make for Muscat. | Buy gunpowder from Farida in Debal's Back Alley, then sail for Muscat. |
| 2 | GameV06j.cs:218 | JournalOpeningGoals[5] | Sail to Muscat with your gunpowder. | Sail to Muscat with the gunpowder. Keep it from customs. |
| 3 | GameV06j.cs:219 | JournalOpeningGoals[6] | Sell your gunpowder in Muscat. | Sell the gunpowder in Muscat's Back Alley, not the market. |
| 4 | GameV06j.cs:228-230 | OpeningMediumGoals[4-6] | Make a fortune running gunpowder to Muscat. | (keep) |
| 5 | GameAct1.cs:30 | Now goal, opening branch | Buy gunpowder in Debal | Buy gunpowder: Farida, Debal's Back Alley |
| 6 | GameAct1.cs:30 | Now goal, after-opening branch | Buy gunpowder in Gwadar | Buy gunpowder in Gwadar's Back Alley (only if it really stocks it; see code note C) |
| 7 | GameAct1.cs:27 / v06_settings.txt:231 | goalMediumOpening | Get the gunpowder to Muscat. | Sell the gunpowder in Muscat's Back Alley. |
| 8 | GameV06.cs:229 | StoryGoals case 4 | Buy gunpowder from Farida in Debal's back alley | same, "Back Alley" capitalised |
| 9 | GameV06.cs:230-231 | StoryGoals case 5/6 | Sell the gunpowder in Muscat's back alley | same, "Back Alley" capitalised |
| 10 | GameV06.cs:253 | ban rumour | …Farida in the back alley has some, at Gwadar's price. | …Farida in Debal's Back Alley sells it cheap. Sell it in Muscat's Back Alley. |
| 11 | GameV06.cs:250 | Rumor2 | They say Muscat's garrison is starved for powder… | (keep; "here" is the Debal tavern, correct) |
| 12 | GameQa.cs:53 | QA copy of the ban rumour | (same as #10) | match #10 |
| 13 | new | completion toast where `openingGpSold = true` (Game.cs:106) | (none; log line only) | Gunpowder sold. Muscat's garrison won't forget you. |
| 14 | new | Now goal + Journal line while gunpowder is held, after the opening | (goal disappears) | Gunpowder: sell it in Muscat's Back Alley. |
| 15 | keeper_lines.json farida/buyContraband | — | Gunpowder? I see only... very angry sand. | (keep) |

"Back Alley" is capitalised to match the tab name (`Game.cs:281`, `GameTown.cs:22`).

## Repeated rumour in the Muscat tavern (Chief Art Designer's egfix shots)
- **The 5 copies come from the QA script, not the game.** `GameQa.cs:53` does `for (int k = 0; k < 5; k++) news.Insert(0, "Rumor (Week 2 · Moon 1 · Year 1): …Farida in the back alley has some, at Gwadar's price.")` for each of Gwadar, Debal and Muscat, then shoots `egfix_tavern_<port>` (line 54). It's a deliberate layout stress fill. `news` is restored afterwards (`GameQa.cs:50` saves `news0`, `GameQa.cs:60` restores it).
- **In real play it's added once:** `GameV06.cs:254`, only on the storyStep 3→4 transition (that sets storyStep = 4, so it can't fire again). Save/load restores `news` as saved; it doesn't re-add it.
- **No dedupe exists on `news.Insert`** anywhere, so if the egfix shots are used for art review, change the QA fill to 1 copy (or 5 different lines) so it doesn't read as a bug.

## Code changes Game Developer must make
A. **Keep the goal up:** in `Goals3()` (GameAct1.cs:23), check `!openingGpSold && !fallbackFired && (hold[Gp] > 0 || needGpAgain)` *before* `JournalGoalsSummary()` and return #14 (held) or #5/#6 (not held). Add the same line to the Journal goals until `openingGpSold`.
B. **Point at Muscat to sell:** with gunpowder held, the sea-map flag goes on Muscat.
C. **Gwadar buy source:** check `AlleySells(Gwadar)` really lists gunpowder after the opening (the tester saw none in Gwadar). If not, point #6 at Debal's Back Alley or drop that branch.
D. Show toast #13 where `openingGpSold = true` (Game.cs:106).
E. (Optional) GameQa.cs:53: 1 copy instead of 5.
