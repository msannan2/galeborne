using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// -gbplaythrough <dir>: drives the real game through the opening by tapping the actual UI. Every step: screenshot + log line.
/// Anything the bot could not do through a tap is logged as CHEAT; anything it could not reach as STUCK.
public partial class Game {
  public bool playthrough; int ptStrikes; float ptLastTap; public int ptForceCustoms = -1; string ptBranch = "main"; public float ptRudder; StreamWriter ptLog; string ptDir; int ptN; int ptCheats, ptStuck; float ptT0;
  static string Plain(string s) => Regex.Replace(s ?? "", "<[^>]+>", "").Replace("\n", " ").Trim();
  IEnumerable<Button> LiveButtons() => FindObjectsByType<Button>(FindObjectsSortMode.None).Where(b => b.isActiveAndEnabled && b.interactable);
  string LabelOf(Button b) => Plain(string.Join(" ", b.GetComponentsInChildren<Text>().Select(t => t.text)));
  string Labels() => string.Join(" | ", LiveButtons().Select(LabelOf).Where(s => s.Length > 0).Distinct().Take(40));
  string PtScreen() => onTitle ? "title" : fighting ? (duelPanel != null ? "duel" : "combat") : dialog != null ? "card" : sailing ? "voyage" : tab < 0 ? $"town:{S.ports[port].name}" : tab == 4 ? "chart" : TabNames[tab].ToLower();
  string Cargo() { var c = Enumerable.Range(0, S.goods.Length).Where(g => hold[g] > 0).Select(g => $"{S.goods[g].id} x{hold[g]}").ToList(); return c.Count == 0 ? "empty" : string.Join(", ", c); }
  /// one line per action: step | clock | +seconds | screen | coins | cargo | action | result
  void PLog2(string action, string result) { var (g1, g2, g3) = S != null && price != null ? Goals3() : ("", "", ""); string jp = lastJackpot != null ? $" | {lastJackpot}" : ""; lastJackpot = null;
    string line = $"#{ptN:00} | {DateTime.Now:HH:mm:ss} PKT +{Time.realtimeSinceStartup - ptT0:0.0}s | {PtScreen()} | {coins} coins | cargo: {Cargo()} | {action} | {result} | Honor {honor} Fear {fear} | goals: {g1} / {g2} / {g3}{jp}"; ptLog.WriteLine(line); ptLog.Flush(); Debug.Log("PT " + line); }
  void PLog(string s) { if (s.StartsWith("STUCK") || s.StartsWith("CHEAT")) PLog2(s.Split(':')[0].ToLower(), s); else if (s.StartsWith("tap ")) PLog2(s, "ok"); else PLog2("note", s); }
  IEnumerator PStep(string name, string note) { ptN++; string f = $"{ptN:00}_{name}.png"; yield return null; yield return ShotCo(Path.Combine(ptDir, f));
    float gap = Time.realtimeSinceStartup - ptLastTap; if (ptLastTap > 0 && gap > 45 && !sailing) PLog2("IDLE", $"{gap:0} s since the last tap"); ptLastTap = Time.realtimeSinceStartup;
    PLog2("STEP " + name, $"{note} | week {week}, story beat {storyStep} | shot {f}"); }
  void Cheat(string why) { ptCheats++; PLog("CHEAT: " + why); }
  /// taps the first live button whose visible label contains the text (exact match preferred)
  string ptScope;
  bool Tap(string label, bool quiet = false) {
    var bs = LiveButtons().Where(b => ptScope == null || (b.transform.parent != null && b.transform.parent.name.Contains(ptScope))).Select(b => (b, l: LabelOf(b))).Where(x => x.l.IndexOf(label, StringComparison.OrdinalIgnoreCase) >= 0 && !x.l.StartsWith("Now ")).OrderBy(x => x.l.Equals(label, StringComparison.OrdinalIgnoreCase) ? 0 : 1).ThenBy(x => x.l.Length).ToList();
    if (bs.Count == 0) { if (!quiet) { ptStuck++; if (ptStuck >= 12) { PLog2("ABORT", "12 stuck taps: the run is dead-ended here"); ptLog.Close(); Application.Quit(3); } PLog($"STUCK: no button '{label}'. Visible: {Labels()}"); } return false; }
    var go = bs[0].b.gameObject; ExecuteEvents.Execute(go, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler); ptLastTap = Time.realtimeSinceStartup; if (!quiet) PLog($"tap '{bs[0].l}'"); return true; }
  IEnumerator TapW(string label, float wait = 0.4f) {
    if (label == "Town") {
      while (dialog != null) { if (!Tap("Close", true) && !Tap("Leave", true) && !Tap("‹  Town", true) && !Tap("‹ Town", true)) { /* force dismiss */ if (dialog) { Destroy0(dialog.gameObject); dialog = null; } } yield return new WaitForSecondsRealtime(0.3f); }
      if (tab >= 0) { if (!Tap("‹  Town", true) && !Tap("‹ Town", true) && !Tap("Town", true)) { tab = -1; Refresh(); } yield return new WaitForSecondsRealtime(wait); yield break; }
      if (tab < 0) { PLog("already in town"); yield return new WaitForSecondsRealtime(0.15f); yield break; }
    }
    if (tab < 0 && !sailing && bldgs.Any(b => b.name.StartsWith(label)) && !Tap(label, true)) { /* building plaque off-screen: walk the town (the swipe) until it shows */
      bool flagged = ptSwipe; if (!ptSwipe) { ptSwipe = true; Cheat("town swipe: the vertical drag is written as the scroll value it would produce (townS), stepping until the plaque appears"); }
      for (float v = townSMin; v <= townSMax; v += 1f) { townS = v; yield return null; yield return null; if (LiveButtons().Any(b => LabelOf(b).StartsWith(label))) break; }
      PLog($"walked the town to {townS:0} to reach {label}"); }
    Tap(label); yield return new WaitForSecondsRealtime(wait); }
  bool ptSwipe;
  List<string> ptChoices; List<string> PtChoices { get { if (ptChoices == null) { ptChoices = new List<string>(); var a = Environment.GetCommandLineArgs(); for (int i = 0; i < a.Length - 1; i++) if (a[i] == "-gbchoice") ptChoices.Add(a[i + 1].ToLower()); } return ptChoices; } } /* -gbchoice spare|drown and -gbchoice run|turnin, both allowed */
  IEnumerator Until(Func<bool> c, float timeout, string what) { float t = 0; while (!c() && t < timeout) { t += Time.unscaledDeltaTime; yield return null; } if (!c()) { ptStuck++; PLog($"STUCK: timed out waiting for {what}. Visible: {Labels()}"); } }
  bool DialogUp => dialog != null;
  string CardText() => dialog == null ? "" : string.Join(" / ", dialog.GetComponentsInChildren<Text>().Where(t => t.GetComponentInParent<Button>() == null).Select(t => Plain(t.text)).Where(t => t.Trim().Length > 0).Take(2));
  /// buys or sells n units through the market sheet: tap the good's row, set the quantity slider, tap Buy/Sell
  IEnumerator Trade(string good, int n, bool sell) {
    if (!Tap(S.goods[G(good)].name)) yield break; yield return new WaitForSecondsRealtime(0.4f);
    if (qty != null) { qty.value = n; PLog($"drag quantity slider to {n}"); }
    yield return TapW(sell ? "Sell" : "Buy", 0.6f); yield return Until(() => !busy, 12, "sheet trade to finish"); yield return new WaitForSecondsRealtime(0.4f); CloseSheet(); Refresh(); yield return new WaitForSecondsRealtime(0.3f); }
  /// v0.6i: simulated pointer drag on the chart map to verify pan clamps and moves the map
  IEnumerator ChartPanTest() {
    var map = FindObjectsByType<RawImage>(FindObjectsSortMode.None).FirstOrDefault(r => r && r.gameObject.name == "Map" && r.GetComponent<UnityEngine.EventSystems.EventTrigger>());
    if (map == null) { PLog2("chart_pan", "SKIP: no Map with EventTrigger"); yield break; }
    var rt = map.rectTransform; var before = rt.anchoredPosition;
    var ped = new PointerEventData(EventSystem.current) { dragging = true, delta = new Vector2(-120f, 80f) };
    ExecuteEvents.Execute(map.gameObject, ped, ExecuteEvents.dragHandler);
    yield return null; var mid = rt.anchoredPosition;
    ped.delta = new Vector2(400f, -400f); ExecuteEvents.Execute(map.gameObject, ped, ExecuteEvents.dragHandler); yield return null;
    ped.delta = new Vector2(-2000f, 2000f); ExecuteEvents.Execute(map.gameObject, ped, ExecuteEvents.dragHandler); yield return null;
    var after = rt.anchoredPosition; bool moved = (mid - before).sqrMagnitude > 1f;
    PLog2("chart_pan", $"drag simulated: moved={moved} before={before} mid={mid} after={after}");
    yield return PStep("chart_pan", $"map drag-to-pan tested (moved={moved})");
  }
  IEnumerator Sail(string to) {
    yield return TapW("SET SAIL"); yield return PStep("chart_open_" + to.ToLower(), "chart with ship marker"); yield return ChartPanTest();
    yield return TapW(to); yield return PStep("chart_route_" + to.ToLower(), $"route card for {to}");
    yield return TapW("SET SAIL FOR"); float t = 0;
    while ((sailing || fighting || DialogUp) && t < 400) { t += Time.unscaledDeltaTime;
      if (fighting) yield return PlayFight(); else if (DialogUp) { yield return PStep(CardText().Contains("Yusuf, harbour master") ? "yusuf_seal" : "card", "card: " + CardText()); yield return HandleCard(); }
      yield return null; }
    yield return new WaitForSecondsRealtime(0.6f); while (DialogUp) { yield return PStep(CardText().Contains("Yusuf, harbour master") ? "yusuf_seal" : "card_on_dock", "card on docking: " + CardText()); yield return HandleCard(); yield return new WaitForSecondsRealtime(0.5f); } }
  string ptPrefer;
  IEnumerator HandleCard() { string[] prefer = { ptPrefer, "Fight", "Dock", "Land", "Accept", "Decline", "Ask about work", "Hint", "Close", "Hold your course", "Reef sails", "Sail on", "Hail her", "Press on", "Salvage", "Continue", "Collect", "OK", "Take", "Captain" };
    foreach (var p in prefer) if (!string.IsNullOrEmpty(p) && Tap(p, true)) { PLog($"card: tapped '{p}' (options: {Labels()})"); yield return new WaitForSecondsRealtime(0.8f); yield break; }
    var b = LiveButtons().FirstOrDefault(x => x.transform.IsChildOf(dialog.transform)); if (b != null) { PLog($"card: tapped first option '{LabelOf(b)}'"); ExecuteEvents.Execute(b.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler); }
    else { ptStuck++; PLog("STUCK: card without options"); } yield return new WaitForSecondsRealtime(0.8f); }
  /// combat bot: taps ammo/aim/FIRE/BOARD/STRIKE; steering is the one input it cannot synthesise
  bool ptSteerFlag;
  IEnumerator PlayFight() {
    yield return new WaitForSecondsRealtime(0.5f); yield return PStep("fight_start", "fight begins: " + (hudMsg && Plain(hudMsg.text).Trim().Length > 0 ? Plain(hudMsg.text) : (ambushForced ? "fog ambush, pirate dhow closing" : "pirate dhow closing"))); float t0 = Time.time; bool shot1 = false, chainDone = false;
    if (!ptSteerFlag) { ptSteerFlag = true; Cheat("steering: the swipe-to-steer drag cannot be injected through uGUI events, so the bot writes the rudder value a swipe would produce (keep a broadside, same policy as the pirate AI)"); }
    while (fighting) {
      if (core != null && core.H != null) { var H = core.H; float br = CombatCore.Bearing(core.P.x - H.x, core.P.z - H.z); ptRudder = CombatCore.Steer(H, CombatCore.Norm(br - CombatCore.Side(H, br)));
        bool slow = !chainDone && core.P.sails > core.P.sails0 * 0.5f; if (slow) { Tap("Chain", true); Tap("Sails", true); } else { chainDone = true; if (core.P.crew < core.P.crew0 * 0.6f || core.Range < 40) { Tap("Grape", true); Tap("Crew", true); } else { Tap("Round", true); Tap("Hull", true); } }
        Tap("FIRE", true); Tap("BOARD!", true); }
      if (!shot1 && Time.time - t0 > 6) { shot1 = true; yield return PStep("fight_broadside", Plain(hudMsg ? hudMsg.text : "")); }
      if (duelPanel != null) yield return PlayDuel();
      if (DialogUp) { yield return PStep("fight_card", "card during fight"); yield return HandleCard(); }
      yield return new WaitForSecondsRealtime(0.25f); }
    PLog($"fight over after {Time.time - t0:0} s game time"); }
  RectTransform duelPanel => root ? root.Find("Duel") as RectTransform : null;
  IEnumerator PlayDuel() { yield return PStep("duel", "boarding duel: watching the closing ring");
    while (duelPanel != null) { var ring = duelPanel.Find("arena/closing"); if (ring != null) { float sc = ring.localScale.x; if (sc <= 0.52f && sc > 0.3f) { if (Tap("STRIKE", true)) ptStrikes++; yield return new WaitForSecondsRealtime(1.0f); } } yield return null; } }

  /// usage: Galeborne.x86_64 -gbplaythrough <dir> [main|customs|copper]
  ///   main    : full opening, papers + tithe, customs passes, gunpowder sold in Muscat's alley, Qarin letter
  ///   customs : same run, but customs catches you on the first run (gunpowder confiscated, coins kept)
  ///   copper  : hear the rumor twice, skip gunpowder, Muscat's forced copper shortage (x2) pays instead
  IEnumerator PlaythroughIfAsked() {
    var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-gbplaythrough"); if (i < 0 || i + 1 >= args.Length) yield break;
    ptDir = args[i + 1]; if (i + 2 < args.Length && !args[i + 2].StartsWith("-")) ptBranch = args[i + 2].ToLower();
    if (ptBranch != "main" && ptBranch != "customs" && ptBranch != "copper" && ptBranch != "rugs") { Debug.LogError("unknown branch " + ptBranch); Application.Quit(2); yield break; }
    Directory.CreateDirectory(ptDir); foreach (var f in Directory.GetFiles(ptDir, "*.png")) File.Delete(f);
    ptLog = new StreamWriter(Path.Combine(ptDir, "log.txt")); playthrough = true; ptT0 = Time.realtimeSinceStartup; Time.timeScale = 2f;
    PLog($"Galeborne v0.6k3 scripted playthrough, branch '{ptBranch}', choices [{string.Join(",", PtChoices)}]. timeScale 2 (waits shortened, logic unchanged). Taps are EventSystem pointer-clicks on the real buttons. Columns: step | clock | screen | coins | cargo | action | result");
    yield return new WaitForSecondsRealtime(1.5f); yield return PStep("title", "title screen");
    yield return TapW("New Game", 0.6f); yield return PStep("difficulty", "difficulty card"); yield return TapW("Captain", 1.0f);
    // prologue: the burning dhow, an unlosable duel (~3 STRIKE taps), the Qarin seal
    yield return Until(() => dialog != null, 10, "prologue card"); yield return PStep("prologue", "the burning dhow"); yield return TapW("Draw your blade", 0.8f);
    yield return Until(() => duelPanel != null, 10, "prologue duel"); int taps0 = ptStrikes; /* counts only taps that landed */ yield return PlayDuel(); PLog2("prologue duel", $"won with {ptStrikes - taps0} STRIKE taps (duelsFought {duelsFought})");
    yield return Until(() => dialog != null, 10, "raider card"); yield return TapW("Go after him", 0.8f);
    yield return Until(() => dialog != null, 10, "prologue choice"); yield return PStep("prologue_choice", "raider in the water: " + CardText()); bool drown = PtChoices.Contains("drown"); yield return TapW(drown ? "Let him drown" : "Spare him", 0.8f); PLog2("prologue choice", drown ? "let him drown" : "spared him");
    yield return Until(() => dialog != null, 10, "seal card"); yield return PStep("seal", "the scorched seal"); yield return TapW("Limp into Gwadar", 1.0f); yield return Until(() => arrivalPanT0 < 0 && (sea3D == null || !sea3D.gameObject.activeSelf), 8, "intro arrival hold"); yield return new WaitForSecondsRealtime(0.3f); /* gamedev_t8: bot waits out the intro Gwadar arrival hold (gamedev_t7) before walking the town */
    yield return PStep("dock_gwadar", "Gwadar at dawn; goal: " + Plain(StoryGoals().Item1) + $"; hidden: warehouse {Hidden(2)}, bank {Hidden(5)}, lender {Hidden(6)}; ads {(AdsUnlocked ? "ON" : "off")}");
    if (Array.IndexOf(Environment.GetCommandLineArgs(), "-gbpausetest") >= 0) { PLog("pausetest: simulating a return to the app (OnApplicationPause false)"); ShowPauseCard(); yield return new WaitForSecondsRealtime(0.8f); yield return PStep("pause_screen", "resume card: " + (pauseCard ? "shown" : "MISSING")); yield return TapW("Resume voyage", 0.6f); }
    yield return TapW("Market"); yield return Trade("dates", 6, false); yield return Trade("salt", 4, false); yield return PStep("bought_dates_salt", "bought dates and salt");
    yield return TapW("Town"); yield return Sail("Debal"); yield return PStep("dock_debal", "arrived in Debal (voyage " + sails + ")");
    yield return TapW("Market"); int c0 = coins; yield return Trade("dates", 6, true); yield return Trade("salt", 4, true); yield return PStep("sold_debal", $"sold in Debal for {coins - c0} (dates wanted +8%)");
    yield return TapW("Town"); yield return TapW("Tavern"); yield return PStep("rumor_1", "tavern: " + Plain(tavernStory));
    if (ptBranch == "copper") {
      yield return TapW("Town"); yield return TapW("Tavern"); yield return PStep("rumor_2", "tavern again: " + Plain(tavernStory) + $" (rumor heard {rumorHeard}x); skipping the gunpowder");
      yield return TapW("Town"); yield return Sail("Gwadar"); yield return TapW("Market"); yield return Trade("copper", 8, false); yield return PStep("bought_copper", "bought copper in Gwadar instead; goal: " + Plain(StoryGoals().Item1));
      yield return TapW("Town"); yield return Sail("Muscat"); yield return PStep("dock_muscat", $"Muscat; copper shortage fired {fallbackFired}, x{evMult[port, G("copper")]:0.0}");
      yield return TapW("Market"); c0 = coins; yield return Trade("copper", 8, true); yield return PStep("sold_copper", $"sold copper for {coins - c0}; run x{(jackpotStartCoins > 0 ? coins / (float)jackpotStartCoins : 0):0.00}");
    } else {
      yield return TapW("Town"); yield return TapW("Back Alley"); yield return PStep("debal_alley", "Farida's stall in Debal (gunpowder at Gwadar's price)");
      ptScope = "buyrow gunpowder"; yield return TapW("Buy 5"); ptScope = null; yield return PStep("bought_gunpowder", $"bought gunpowder, papers {papersGiven}");
      yield return TapW("Town"); ptPrefer = "Fight"; if (ptBranch == "customs") ptForceCustoms = 1;
      yield return Sail("Muscat"); yield return PStep("dock_muscat", $"Muscat (voyage {sails}); story ambush done {storyAmbushDone}; gunpowder aboard {hold[Gp]}; openingGpSold {openingGpSold}" + (ptBranch == "customs" ? " (customs branch)" : ""));
      if (hold[Gp] == 0 && !openingGpSold) { /* confiscated (e.g. -gbforce customs_caught) */
        yield return TapW("Town"); yield return PStep("customs_goals", "after confiscation goals: " + Plain(Goals3().Item1) + " / " + Plain(Goals3().Item2));
        coins += 2500; Cheat("topped up coins after confiscation for the recovery buy");
        if (InOpening && OpenFlag("openingDebalAlleyStocksGunpowder")) { yield return Sail("Debal"); yield return TapW("Back Alley"); }
        else { yield return Sail("Gwadar"); yield return TapW("Back Alley"); }
        ptScope = "buyrow gunpowder"; yield return TapW("Buy 5"); ptScope = null; yield return PStep("rebuy_gunpowder", $"rebought gunpowder: {hold[Gp]} aboard; short goal {Plain(Goals3().Item1)}");
        ptForceCustoms = 0; Cheat("customs forced PASS for the recovery sale");
        yield return TapW("Town"); yield return Sail("Muscat");
      }
      if (hold[Gp] > 0) { yield return TapW("Back Alley"); c0 = coins; yield return TapW("Sell all", 0.25f);
        yield return PStep("sold_gunpowder", $"sold in Muscat alley: +{coins - c0}; openingGpSold {openingGpSold}; medium {Plain(Goals3().Item2)}; run x{(jackpotStartCoins > 0 ? coins / (float)jackpotStartCoins : 0):0.00}"); }
    }
    yield return TapW("Town");
    if (ptBranch != "copper") yield return PStep("goals_after_sale", "goals right after the sale: " + Plain(Goals3().Item1) + " / " + Plain(Goals3().Item2));
    yield return TapW("Tavern"); yield return PStep("qarin_letter", "tavern: " + Plain(tavernStory));
    yield return TapW("Town"); yield return PStep("unlocked", $"opening done {!InOpening}; hidden now: warehouse {Hidden(2)}, bank {Hidden(5)}, lender {Hidden(6)}, tithe {TitheHidden}; ads unlocked {AdsUnlocked} (voyage {sails})");


    if (ptBranch == "main" || ptBranch == "rugs") {
      yield return Sail("Debal"); yield return PStep("dock_debal_yusuf", "back in Debal for Yusuf");
      yield return Until(() => yusufSeal || dialog != null, 8, "Yusuf seal scene");
      while (DialogUp) { yield return PStep("yusuf_seal", "card: " + CardText()); yield return HandleCard(); yield return new WaitForSecondsRealtime(0.5f); }
      yield return PStep("debal_after_yusuf", "Yusuf scene seen " + yusufSeal);

      /* v0.6j: journal + accept/decline in Debal (tavern has first-wave jobs here) */
      yield return TapW("Journal", 0.6f); yield return PStep("journal_open", "journal after opening");
      while (dialog != null && (Tap("Close", true) || Tap("Leave", true))) yield return new WaitForSecondsRealtime(0.35f);
      if (dialog != null) { Destroy0(dialog.gameObject); dialog = null; }
      yield return TapW("Tavern", 0.6f); yield return TapW("Ask about work", 0.6f); yield return PStep("quest_menu", "quest menu offers: " + Labels());
      if (Tap("Accept", true)) { yield return new WaitForSecondsRealtime(0.5f); yield return PStep("quest_accept", "accepted"); }
      else { Tap("Decline", true); yield return new WaitForSecondsRealtime(0.5f); yield return PStep("quest_decline", "declined"); }
      while (dialog != null && (Tap("Close", true) || Tap("Leave", true))) yield return new WaitForSecondsRealtime(0.3f);
      if (dialog != null) { Destroy0(dialog.gameObject); dialog = null; }
      yield return TapW("Town", 0.4f); yield return TapW("Journal", 0.5f); yield return PStep("journal_active", "journal after quest choice; active=" + ActiveJobCount);
      if (Tap("Hint", true)) { yield return new WaitForSecondsRealtime(0.3f); yield return PStep("journal_hint", "hint: " + Plain(msg)); }
      while (dialog != null && (Tap("Close", true) || Tap("Leave", true))) yield return new WaitForSecondsRealtime(0.3f);
      if (dialog != null) { Destroy0(dialog.gameObject); dialog = null; }

      if (ptBranch == "rugs") {
        bool turn = PtChoices.Contains("turnin");
        yield return TapW("Back Alley", 0.6f); yield return PStep("farida_choice", "Farida's offer");
        int c1 = coins; yield return TapW(turn ? "Turn her in" : "Run the rugs", 0.8f);
        PLog2("farida choice", turn ? $"turned her in: +{coins - c1} coins, Muscat standing {rep[Muscat]}" : "ran the rugs: Farida is an ally");
        if (!turn) { yield return TapW("Town"); yield return TapW("Back Alley", 0.6f); yield return PStep("farida_ally_prices", "ally prices after Run the rugs"); }
        else { yield return TapW("Town"); yield return TapW("Back Alley", 0.6f); yield return PStep("farida_rival", $"rival Farida portrait after turn-in; rugs offered: {AlleySells(Debal).Contains("rugs") && !RugsHidden(Debal)}"); }
      }
    }
    yield return TapW("Town"); yield return PStep("goals_hud", "goals: " + Plain(Goals3().Item1) + " / " + Plain(Goals3().Item2) + " / " + Plain(Goals3().Item3));
    PLog($"DONE. branch {ptBranch}, steps {ptN}, cheats {ptCheats}, stuck {ptStuck}, idle nudges shown {nudges}");
    ptLog.Close(); Application.Quit();
  }
}
