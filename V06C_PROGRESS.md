# v0.6c progress (updated 01:47 PKT Oct 15)
- DONE: settings merged (opening, goodWantBonus, portGoods; portGoodsV06 dropped per Jeff). Design/v06_settings.txt = latest.
- DONE: opening v2 (prologue duel -> Gwadar -> Debal sell+rumor -> Debal alley gunpowder [openingDebalAlleyStocksGunpowder] -> Muscat fog -> sale -> unlocks). StDone=8.
- DONE: first-run customs always clears, real risk from voyage 4; ads hidden until !InOpening && voyage>=4; warehouse/bank/lender/tithe hidden until sale; goal chip tap + 45s nudge; coin floor (Yusuf loan) in Debal.
- DONE: chart v2 (chart_map_v2 look, ChartGeo real geography, map top 3/4, route card bottom).
- DONE: prologue lighting (moon, fire point light, UI embers).
- DONE: playthrough branches main/customs/copper; log format; IDLE/STUCK/CHEAT flags; abort after 12 stuck.
- Linux build green 01:40.
- Balance checks run 01:2x: only portGoodsV06 MISMATCH (deleted on purpose) + hardcoded LIVE text lines.
- TODO: run 3 branches -> shots/v06c/playthrough_<branch>/; shots -gbshots -> shots/v06c/1080x2340 (+duel); sim.txt; BuildAndroidV06c (code 11) -> /workspace/Galeborne_v0.6c.apk <24,000,000.
Run: cd Builds/Linux && DISPLAY=:5 ./Galeborne.x86_64 -screen-fullscreen 0 -screen-width 540 -screen-height 1170 -gbsafe 55,30 -gbplaythrough <dir> [main|customs|copper]
- 01:55 three branches ran clean (0 stuck). main x2.01 coins (sale 1873 for 5 gp), customs: first run cleared despite forced roll, 2nd run caught fined 45; copper x1.54. Next: -gbshots, sim, APK.
- 02:2x all 3 branches clean after rumor fix (step2 rumor). Next: APK.
- 02:21 APK 23,732,553 signed (code 11).
- 02:23 sim.txt + balance written. Report next.

# v0.6d (started 02:30 PKT Oct 15) - Linux -> Builds/Linux_v06d, shots -> shots/v06d (do NOT touch Builds/Linux or shots/v06c)
- code: texts (letter, rumor2; tavern line computed in OpenBuilding), fog needs sails>=3 + prologue counts as voyage 1, PrologueDuel 3 taps + slowmo, duel strip bottom third, chart clean bg + new ChartGeo, folk/props/stalls snapped z>=0.5, upper town ochre/plaster.
- Builder: BuildLinuxV06d, BuildAndroidV06d (code 12).
- 02:48 v0.6d DONE: Linux_v06d green, 3 branches 0 stuck, APK /workspace/Galeborne_v0.6d.apk 23,717,565 signed code 12, shots/v06d/1080x2340 (44), sim.txt, balance.txt.
- 03:0x ambush addCargoValue fix; APK rebuilt; branches rerun clean.

# v0.6e (03:05 PKT) -> Builds/Linux_v06e, shots/v06e, /workspace/Galeborne_v0.6e.apk (code 13). Do NOT touch Builds/Linux_v06d (Tester). Dates bug owned by Tester (bot change reverted).
- duel: deck camera, Hasan(kind 8)+raider(kind 9) figures, night tint 0.42, fire particles (Mats/Fire additive), no UI embers/orange smear. Town: ambient (0.34,0.34,0.37) + warm fog in town.
- 03:40 PKT: 3 branches DONE stuck 0; APK built
- 04:00 PKT v0.6e Tester items: duel exactly 3 taps (STRIKE+ring removed after 3rd; bot counts only landed taps); duel not a voyage (sails=0), Debal->Muscat opening = calm Ras al Hadd leg (sailing 2) + fog (sailing 3); letter label measured (no overlap); squall no cargo loss + no defeat (toll 10%, min 20) in opening; second rumor appended on first Debal tavern visit; "Dockside bonus" msg/log for Yusuf coin floor; fight label fallback. Fire = own mesh particle sim (ParticleSystem module cost +590KB). APK 23,804,565 B. 3 branches DONE stuck 0.
- 04:40 PKT v0.6e2: gift card, Yusuf line on gunpowder buy, fog cap 60s, -gbforce squall|ambush|customs_caught|customs_pass, boats/folk snap, additive fire shader+rim light. APK 23,801,589.
- 05:10 PKT v0.6e3: night duel (navy bg/fog, ship night tint, overlay covers safe gap), flames along sail foot, turban sphere (Mats/Turban) + raider red sash (Mats/Sash), squall card text logged, -gbforce customs_caught overrides first-run pass. APK 24015421.
- APK final 23,830,861 B (chart+port backdrops ASTC_10x10 on Android to fit).
- 05:30 PKT v0.6e3_hq: HQPass (ASTC_4x4 2048, no crunch, TownTex full RGB24) for _hq builds only; APK 31,864,985 B code 16; main 3 taps/0 stuck.
- 05:40 PKT v0.6e3_mq: chart ASTC4x4 2048, TownTex RGB24 full, normals 512 ASTC8x8, port backdrops 1024 ASTC12x12, ship atlas ASTC8x8. APK 24,239,237 B code 17.
- 05:40 PKT v0.6e3_mq: MQPass chart ASTC4x4 2048, TownTex RGB24 full, normals 512 ASTC8x8, port backdrops 1024 ASTC12x12, ship atlas ASTC8x8. APK 24,239,237 B code 17.
# v0.6f (05:50 PKT): fine cap, Banned for good, 60s cap all ambushes (Escaped outside opening), DangerLabel unified + Calm waters, sea3D off in town (Muscat water), chart full height w/o route, stray rowboat removed, FighterMesh per turnaround sheet, beam camera, fire climbs sail, moon rim. Build target Linux_v06f, APK via _mq recipe (0.6f_mq -> copied to 0.6f.apk), code 18.
- 06:30 PKT v0.6f built: APK 24245089 code 18; town water own material _Amp 0.3 (Muscat flood fix); label free also honours safeUntilSail. Balance checks crash at section 3 (designer's partial customs block in v06_settings.json; Tester's tool).
- 06:40 PKT v0.6f2: fighters split into flat materials Mats/F_* (warm skin, brown jaw beard, plain kurta), fewer sky sparks + flame strip on yard (out of frame). APK 24246049 code 19.
- 07:20 PKT v0.6f3: ROOT CAUSE title freeze = Application.runInBackground false; under bare Xvfb (xvfb-run fallback in runrest.sh when :5 was down after reboot) the window has no focus and the player loop pauses. Fix: runInBackground=true (code+PlayerSettings). Corrupt save -> backed up as .corrupt-<ts> and deleted. Route wording: Safe only when Calm waters, else 'Coastal route, calm seas'. APK 24251125 code 20.

## v0.6f4 (vc21) — done 15 Oct 2026 ~08:20 PKT
- runInBackground: Game.cs wrapped in #if UNITY_STANDALONE||UNITY_EDITOR; ProjectSettings 0; BuildLinuxV06f4 sets true then restores; BuildAndroidV06f4 forces false (log: runInBackground=False).
- Opening route card: DangerLabel + fog hint below; "Open water" label hidden on fog card. "banned for good" lowercase. New Muscat gunpowder rumor. Header v0.6f4.
- APK /workspace/Galeborne_v0.6f4.apk 24,249,485 B, vc21/0.6f4. Linux Builds/Linux_v06f4. main: 3 taps, stuck 0. Shots shots/v06f4/.

## v0.6f5 (vc22) — 15 Oct 2026 ~08:25 PKT
- Opening fog card danger line fixed: "Moderate pirate danger (25%)" (gold/Moderate), fog hint beneath. Header v0.6f5.
- APK /workspace/Galeborne_v0.6f5.apk 24,249,405 B vc22, runInBackground=False. Linux Builds/Linux_v06f5. main stuck 0. Shots shots/v06f5/.

## v0.6g (vc23) — 16 Oct 2026 ~02:00 PKT
- New FighterArt.cs sculpted fighters (smooth heads with nose/brow/sockets/lips/chin, eyes+lids, band beard/hair/turban shells, fingered hands, scimitar); painted face texture at runtime; indigo turban per store banner.
- FireSim2 flame tongues + embers + smoke; duel cam reframed and tilted up 10 deg.
- Opening card top-right "Open sea route". Pause card (OnApplicationPause/Focus resume, mobile) + -gbpausetest. Two rotating save slots + voyage backup + fallback; -gbsavetest PASS.
- APK /workspace/Galeborne_v0.6g.apk 24,299,225 B vc23 runInBackground=False. Linux Builds/Linux_v06g. main stuck 0.

## v0.6g2 (vc24) — 16 Oct 2026 ~02:15 PKT
- Tavern/alley news lines measured (Flow), broken-save text, pause card parchment = ledger Box. APK 0.6g2. main stuck 0, savetest PASS.

## v0.6g3 (vc25) — 16 Oct 2026 ~02:15 PKT
- Parchment art (ui_parch_wide/tall, Android 512 ASTC_6x6) stretched on tavern/Qarin panel and pause card. main stuck 0, savetest PASS.

## v0.6g4 (vc26) — 16 Oct 2026 ~02:45 PKT
- Fire emitted from Hasan sail surface (Dhow.HasanSail/SailPoint), clew+foot, FireCut opaque cutout shader, soft smoke, deck + sail orange point lights. -gbsavedir / GALEBORNE_SAVEDIR isolation; savetest refuses without it. All runs isolated + pgrep-gated. main stuck 0.

## v0.6g5 (vc27) — 16 Oct 2026 ~03:00 PKT
- Duel cam swung aft/widened (DuelCamPos -8.4,2.8,-1.5 look 0,2.35,-2.1 FOV 56). Fire climbs the leech from the clew (lowest visible point is the stern-cabin roof). Sail fire-lit by strong orange point light. Varied/leaning/mirrored tongues. main stuck 0.

## v0.6g6 (vc28) — 16 Oct 2026 ~03:10 PKT
- g3 duel framing restored (pos -6.2,2.3,-1.2 look 0,1.05,-1.2 FOV 42 tilt 10). Fire burn band depth 0.22 in from leech so a strip shows top-right. Sail sun emblem covered for the duel by SunPatch. main stuck 0.

## v0.6g7 (vc29) — 16 Oct 2026 ~04:05 PKT
- Duel FOV 42->35, tilt 10->8 (keeps feet above panel). #FF8A3D rim point light high right behind fighters. Fire untouched. Fighters ~40% px (soles-crown) vs ~34% same method in g6. main stuck 0.

## v0.6h (versionCode 30), Oct 16 04:35 PKT
Act 1 pass: GameAct1.cs (3 goals HUD, prologue spare/drown -> Honor/Fear, Yusuf seal scene, Farida rugs run/turnin, jackpot spill), saves persist honor/fear/yusufSeal/faridaState. Real art: Resources/Art/Portraits/{yusuf,farida}.png (512 ASTC6x6), Art/alley_debal.jpg (1024 ASTC8x8, Debal alley backdrop), Art/coin_spill.png (512 ASTC6x6). New settings: act1.*. Flags: -gbchoice spare|drown -gbchoice run|turnin; playthrough branch 'rugs'. APK 24,889,353 B (+430,588 vs g7). Runs: shots/v06h, v06h_drown, v06h_rugs_run, v06h_rugs_turnin all 0 stuck; savetest PASS.


## v0.6h3 (versionCode 32), Oct 16 ~05:35 PKT
Chief Art + Game Designer pass on h2:
1. Back Alley KeeperHeader Farida → Art/Portraits/farida.png (same as dialogue; no more Keepers/k2_* purple-veil).
2. Re-imported Chief Art Designer's re-cut coin_spill.png (art/ → Resources/Art/).
3. debal_back_alley (Art/alley_debal.jpg) sits behind the parchment on Debal's Back Alley (AlleyBg under inset translucent parchment; lighter dimmer).
4. Medium goals: opening "Get the gunpowder to Muscat." → after Muscat "Run Farida's rugs." → after Farida choice "Save for a bigger ship." Warehouse unlock timing unchanged (Hidden(2) still storyStep 7). act1.goalMediumOpening updated in design/v06_settings.json + Resources/Design/v06_settings.txt.
APK /workspace/Galeborne_v0.6h3.apk 24,890,325 B vc32/0.6h3 runInBackground=False. Linux Builds/Linux_v06h3.
Runs: shots/v06h3 (main spare), v06h3_drown, v06h3_rugs_run, v06h3_rugs_turnin all stuck 0; shots/v06h3_savetest PASS.
Key shots: v06h3/14_debal_alley.png (header+alley bg), v06h3/07_dock_gwadar.png + 28_goals_hud.png (opening/after Muscat goals), v06h3_rugs_run/39_farida_choice.png (matching portraits), v06h3_rugs_run/48_jackpot_rugs.png (spill).
Rugs jackpot log: JACKPOT rugs sold at 454 (2.79x buy 163).

## v0.6h2 (versionCode 31), Oct 16 04:55 PKT
Rugs banned sale in Muscat = Debal price x act1.rugsMuscatBannedSellMult (no Muscat markup stacked; gunpowder formula unchanged). Jackpot gasp on dark strip with brass trim. Medium goal: warehouse (while Hidden(2)) -> Farida's rugs -> act1.goalMediumAfterFarida "Save for a bigger ship.". Yusuf shots named yusuf_seal. Alley art kept at 1024. APK 24,889,781 B. Runs shots/v06h2{,_drown,_rugs_run,_rugs_turnin} 0 stuck; savetest PASS. Balance checks run from a /tmp copy (Tester files untouched).

## v0.6k (2026-10-16 ~07:15 PKT) — art fixes + landmarks

**Priority art fixes (then landmarks):**
1. Debal dock: goals under town name; Story HUD shortens to "What is Qarin hiding?"; dark swipe hint; Journal/Options labeled teak chips; SignMaxY raised so plaques clear goals.
2. Quest menu: ink panel sized to job cards, title padded inside (no teak 9-slice clip); covers keeper header.
3. Sea fight: backdrop clear so 3D sea shows; zenith cam/fog + top sky strip (no parchment); Wind thin-border plaque; camera pulled back + Hasan scale 1.55×; Aim row lowered.

**Landmarks:** Gwadar/Debal/Muscat town+sea meshes in GameV06k.cs; shots under shots/v06k_*_{town,sea}.png and v06k_fix_*.png.

**Verify:** main/customs/copper/rugs/force 0 stuck; savetest PASS; APK /workspace/Galeborne_v0.6k.apk (j untouched). Linux Builds/Linux_v06k.

## v0.6k2 (2026-10-16 ~07:51 PKT) — art rejection fixes

**APK:** /workspace/Galeborne_v0.6k2.apk (versionCode 36). Does not overwrite k or j.

1. **Sea fight:** Camera reverted to exact v0.6j high three-quarter (`d*0.55+16` / `d*0.75+15`). Hasan scale ×1.45 only. Aim row lowered. Health plates separated. Sky-strip hack removed (parchment edge may still show from skyHorizon clear color).
2. **Landmarks:** Solid-color primitives per port (Debal stepped spire+RED pennant+teak house; Muscat white crenellated fort+gatehouse; Gwadar watchtower+dome+nets). Sea landmarks placed in front of coastline ridge. Towns still share terrace layout; skyline props now differ.
3. **Kenney dhow:** FBX length was on Y — fixed with Euler(-90,180,0) + scale 0.55; URP Lit + Galeborne colormap. Visible on Debal sea approach.
4. **Journal chip:** ink panel + cream "Journal"/"Options" labels (no empty teal).

**Verify:** main/copper/rugs/force 0 stuck; customs flaky on polluted title (re-run); savetest PASS.
**Shots:** shots/v06k2_{gwadar,debal,muscat}_{town,sea}.png, v06k2_fight.png, v06k2_dock.png, v06k2_sailing.png.

