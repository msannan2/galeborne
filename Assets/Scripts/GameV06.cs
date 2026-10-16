using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Rnd = UnityEngine.Random;

/// v0.6 systems driven by the design data files: port goods and back alleys, market events and stock, the foreign ship,
/// cargo- and coin-scaled ambushes with moods and responses, the scripted gunpowder opening, repairs, ads (stub) and quality tiers.
public partial class Game {
  public Design DZ;
  // ---- story / opening ----
  public int storyStep; public bool firstRun = true; public int rumorHeard, gpBanStart = -1; public bool fallbackFired, storyAmbushDone, qarinLetter, boardedStoryPirate, spareGunpowder; public int jackpotStartCoins = -1;
  // ---- market ----
  public int[,] stock, stockMax; public float[,] evMult; public int[,] evEnd; string[] lastEv; public string[] townHint; public int[] hintEnd; public int[] blockGood, blockEnd; public int[] blockStart; public bool[] blockKnown; public string[] blockHeard; public int blockPending = -1; int chartCenterOn = -1; /* gamedev_blockade_warning */
  public int foreignPort = -1, foreignGood = -1; List<(int p, Dictionary<string, object> e, int g, int start)> pendingEv = new List<(int, Dictionary<string, object>, int, int)>();
  // ---- ship damage ----
  public float hullNow = -1, sailsNow = -1;
  string lastAmbush = ""; public string ambushMood = "";

  public int PortIdx(string n) => Array.FindIndex(S.ports, p => p.name == n);
  List<string> AlleySells(int p) { var l = Design.SL(DZ?.Path(DZ.settings, "portGoods._backAlley.sells." + S.ports[p].name)).ToList(); string gp = Design.Path0(DZ, "opening.good", "gunpowder");
    l.Remove(gp); if (!openingGpSold && (OpenFlag("openingDebalAlleyStocksGunpowder") || !InOpening) && p == BuyPort) l.Insert(0, gp); /* research_gp2: Farida (Debal alley) sells gunpowder until the Muscat sale, then never again */ return l; }
  List<string> AlleyBuys(int p) => Design.SL(DZ?.Path(DZ.settings, "portGoods._backAlley.buysBanned." + S.ports[p].name));
  float WantBonus(int g) => Design.F(DZ?.Path(DZ.settings, "goodWantBonus." + S.goods[g].id), 1f);

  /// called right after settings load: port goods straight from v06_settings.portGoods, contraband numbers merged
  void ApplyDesign() {
    DZ = Design.Load(); InitV06j();
    foreach (var pd in S.ports) { var pg = Design.D(DZ.Path(DZ.settings, "portGoods." + pd.name)); if (pg.Count == 0) continue; pd.cheap = Design.SL(pg["cheap"]).ToArray(); pd.want = Design.SL(pg["want"]).ToArray(); }
    var cb = Design.D(DZ.Path(DZ.settings, "contraband")); if (cb.ContainsKey("bannedSellMult")) S.contraband.bannedSellMult = Design.F(cb["bannedSellMult"]); if (cb.ContainsKey("bannedPerUnitSellDrop")) S.contraband.bannedPerUnitSellDrop = Design.F(cb["bannedPerUnitSellDrop"]);
  }
  /// price multiplier used when base prices are rolled (cheap / want / alley-sold, with the per-good want bonus)
  float PortMult(int p, int g) { var id = S.goods[g].id; if (S.ports[p].cheap.Contains(id) || (DZ != null && AlleySells(p).Contains(id))) return S.cheapMult;
    if (S.ports[p].want.Contains(id)) return S.wantMult * WantBonus(g); return 1f; }

  void InitV06() {
    int np = S.ports.Length, ng = S.goods.Length; bans.Clear(); storyStep = 0; firstRun = true; rumorHeard = 0; gpBanStart = -1; fallbackFired = storyAmbushDone = qarinLetter = boardedStoryPirate = false; jackpotStartCoins = -1; openingGpSold = false;
    stock = new int[np, ng]; stockMax = new int[np, ng]; evMult = new float[np, ng]; evEnd = new int[np, ng]; lastEv = new string[np]; townHint = new string[np]; hintEnd = new int[np]; blockGood = Enumerable.Repeat(-1, np).ToArray(); blockEnd = new int[np]; blockStart = new int[np]; blockKnown = new bool[np]; blockHeard = new string[np]; blockPending = -1;
    for (int p = 0; p < np; p++) for (int g = 0; g < ng; g++) { evMult[p, g] = 1; var id = S.goods[g].id; stockMax[p, g] = S.ports[p].cheap.Contains(id) ? 40 : S.ports[p].want.Contains(id) ? 12 : AlleySells(p).Contains(id) ? 15 : 20; stock[p, g] = stockMax[p, g]; }
    foreignPort = foreignGood = -1; pendingEv.Clear(); hullNow = sailsNow = -1; EnsureDesignBans();
  }
  /// bans come from the back-alley data: each buysBanned port bans those goods; the opening's gunpowder ban starts after the rumor (always Muscat)
  void EnsureDesignBans() {
    for (int p = 0; p < S.ports.Length; p++) foreach (var id in AlleyBuys(p)) { int g = G(id); if (g < 0) continue; bool gp = id == Design.Path0(DZ, "opening.good", "gunpowder");
      int start = gp ? (gpBanStart >= 0 ? gpBanStart : int.MaxValue / 2) : 0; var b = bans.FirstOrDefault(x => x.good == g && x.port == p);
      if (b == null) bans.Add(new Ban { good = g, port = p, start = start, end = int.MaxValue / 2 }); else b.start = start; }
  }

  // ---------------- market: list, stock, events, foreign ship ----------------
  public List<int> MarketGoods(int p) {
    var l = new List<int>(); for (int g = 0; g < S.goods.Length; g++) { var id = S.goods[g].id; if (S.goods[g].contraband && S.market.contrabandOnlyInBackAlley) continue;
      if (S.ports[p].cheap.Contains(id) || S.ports[p].want.Contains(id) || (S.market.alwaysListCarriedGoods && hold[g] > 0) || (foreignPort == p && foreignGood == g)) l.Add(g); }
    return l; }
  public int StockOf(int g) => stock == null ? 999 : stock[port, g];
  float EvMult(int p, int g) => evMult == null ? 1f : evMult[p, g];
  void TickMarketV06(bool newMoon) {
    if (stock == null) return; int np = S.ports.Length, ng = S.goods.Length;
    for (int p = 0; p < np; p++) { for (int g = 0; g < ng; g++) if (evMult[p, g] != 1 && week >= evEnd[p, g]) evMult[p, g] = 1; if (blockGood[p] >= 0 && week >= blockEnd[p]) { blockGood[p] = -1; blockKnown[p] = false; blockHeard[p] = null; } if (week >= hintEnd[p]) townHint[p] = null; }
    int refill = (int)Design.F(DZ.Path(DZ.market, "priceDrift.restockWeeks"), 3); if (week % Mathf.Max(1, refill) == 0) for (int p = 0; p < np; p++) for (int g = 0; g < ng; g++) stock[p, g] = Mathf.Max(stock[p, g], stockMax[p, g]);
    foreach (var pe in pendingEv.Where(x => x.start <= week).ToList()) { StartEvent(pe.p, pe.e, pe.g); pendingEv.Remove(pe); }
    if (newMoon) { int rolls = (int)Design.F(DZ.Path(DZ.market, "rollsPerPortPerMoon"), 1); int lead = (int)Design.F(DZ.Path(DZ.market, "rumorLeadWeeks"), 1);
      for (int p = 0; p < np; p++) for (int k = 0; k < rolls; k++) { var e = RollEvent(p); if (e == null) continue; int g = EventGood(p, e); pendingEv.Add((p, e, g, week + lead)); Rumor(p, (string)e["id"], g); } }
  }
  Dictionary<string, object> RollEvent(int p) {
    var all = ((List<object>)DZ.market["events"]).Select(Design.D).Where(e => !e.ContainsKey("weightIsChancePerDock") && (!e.ContainsKey("onlyPort") || (string)e["onlyPort"] == S.ports[p].name)
      && !((bool)(DZ.market.ContainsKey("noSameEventInPortTwiceInARow") ? DZ.market["noSameEventInPortTwiceInARow"] : false) && (string)e["id"] == lastEv[p])).ToList();
    float tot = all.Sum(e => Design.F(e["weight"])), r = Rnd.value * tot; foreach (var e in all) { r -= Design.F(e["weight"]); if (r <= 0) { lastEv[p] = (string)e["id"]; return e; } } return null; }
  int EventGood(int p, Dictionary<string, object> e) { var t = (string)e["target"]; var pool = t == "oneGoodFromPortWant" ? S.ports[p].want : t == "oneGoodFromPortCheap" ? S.ports[p].cheap : null;
    return pool == null ? -1 : G(pool[Rnd.Range(0, pool.Length)]); }
  Dictionary<string, object> Content(string id) => ((List<object>)DZ.content["marketEvents"]).Select(Design.D).FirstOrDefault(x => (string)x["id"] == id) ?? new Dictionary<string, object>();
  string Fill(string s, int p, int g, int n = 0) => (s ?? "").Replace("{port}", p >= 0 ? S.ports[p].name : "").Replace("{good}", g >= 0 ? S.goods[g].name.ToLower() : "goods").Replace("{n}", n.ToString());
  /// vague tavern talk a week ahead of the event (rumorsAreVague)
  void Rumor(int p, string id, int g) { var rs = ((List<object>)DZ.content["rumors"]).Select(Design.D).Where(x => (string)x["type"] == (id == "glut" ? "glut" : id == "shortage" ? "shortage" : id == "blockade" ? "blockade" /* gamedev_blockade_warning: own rumours, not shortage */ : "x")).ToList();
    string t = rs.Count > 0 ? (id == "blockade" ? BlkFill((string)rs[Rnd.Range(0, rs.Count)]["text"], p, g) : Fill((string)rs[Rnd.Range(0, rs.Count)]["text"], p, g)) : id == "festival" || id == "sultanWedding" ? $"Lantern-makers in {S.ports[p].name} are working through the night." : $"Something is stirring in {S.ports[p].name}.";
    news.Insert(0, $"Rumor ({DateStr}): {t}"); }
  public void StartEvent(int p, Dictionary<string, object> e, int g) {
    string id = (string)e["id"]; int dur = (int)Design.F(e.ContainsKey("durationWeeks") ? e["durationWeeks"] : null, 2); var c = Content(id);
    if (id == "blockade") { blockGood[p] = g; blockEnd[p] = week + dur; blockStart[p] = week; blockKnown[p] = false; blockHeard[p] = null; } /* gamedev_blockade_warning: seizure only from the week after the start, so the departure-port rumour exists one voyage ahead */
    else if ((string)e["target"] == "luxuries") { float m = Design.F(e["priceMult"], 1.5f); foreach (var lid in Design.SL(e["luxuries"])) { int lg = G(lid); if (lg < 0) continue; SetEv(p, lg, m, week + dur); } }
    else if (g >= 0) { var pm = e["priceMult"]; float m = pm is Dictionary<string, object> d ? (d.ContainsKey("min") ? Rnd.Range(Design.F(d["min"]), Design.F(d["max"])) : Design.F(d["max"])) : Design.F(pm, 1);
      SetEv(p, g, m, week + dur); if (e.ContainsKey("stockMult")) stock[p, g] = Mathf.RoundToInt(stockMax[p, g] * Design.F(e["stockMult"])); }
    townHint[p] = c.ContainsKey("townHint") ? (string)c["townHint"] : null; hintEnd[p] = week + dur;
    news.Insert(0, $"{Fill((string)(c.ContainsKey("headline") ? c["headline"] : id), p, g)} {Fill((string)(c.ContainsKey("text") ? c["text"] : ""), p, g)}"); }
  void SetEv(int p, int g, float m, int end) { evMult[p, g] = m; evEnd[p, g] = end; price[p, g] = basePrice[p, g] * m; lastPrice[p, g] = basePrice[p, g]; }
  /// on docking: foreign ship roll (chance per dock), blockade seizure, the forced fallback shortage
  void OnDockV06() {
    if (InOpening && port == BuyPort && Gp >= 0) { int gw = GwadarIdx; price[port, Gp] = price[gw, Gp] * Design.F(DZ?.Path(DZ.settings, "opening.openingDebalGunpowderBuyMult"), 1f); /* Farida sells at Gwadar's price during the opening */
      int need = 5 * Mathf.CeilToInt(price[port, Gp]); if (OpenFlag("gunpowderCoinFloor") && storyStep <= 4 && hold[Gp] == 0 && coins < need) { giftPending = need - coins; news.Insert(0, $"Yusuf's gift: {need - coins} coins."); msg = $"A gift from Yusuf: {need - coins} coins."; if (playthrough) PLog2("dockside gift", $"+{need - coins} coins from Yusuf (coin floor: 5 gunpowder)"); coins = need; } }
    if (stock == null) return; foreignPort = -1;
    var fe = ((List<object>)DZ.market["events"]).Select(Design.D).FirstOrDefault(x => (string)x["id"] == "foreignShip");
    if (fe != null && Rnd.value < Design.F(fe["weight"]) && !InOpening) { var pool = Design.SL(fe["pool"]); int g = G(pool[Rnd.Range(0, pool.Count)]);
      foreignPort = port; foreignGood = g; stock[port, g] = (int)Design.F(fe["stockUnits"], 20); price[port, g] = lastPrice[port, g] = basePrice[port, g] * Design.F(fe["buyPriceMult"], 0.6f);
      var c = Content("foreignShip"); townHint[port] = "foreignShipInHarbor"; hintEnd[port] = week + 1; news.Insert(0, Fill((string)c["headline"], port, g) + ". " + Fill((string)c["text"], port, g)); }
    if (blockGood[port] >= 0 && week > blockStart[port] && hold[blockGood[port]] > 0 && !StoryGoods().Contains(blockGood[port]) /* gamedev_t16_seizesafe */) { blockPending = port; Debug.Log($"GB DOCK blockade at {S.ports[port].name}: {hold[blockGood[port]]}x{S.goods[blockGood[port]].id} aboard, fee {BlkFee(port)}"); } /* gamedev_blockade_warning: the arrival card (pay off / let them take it) decides; see BlockadeCard */
  }

  // ---------------- ambush ----------------
  bool RiskyRoute(int a, int b) => !(a == GwadarIdx || b == GwadarIdx);
  /// v06_ambush_data.rollOrder, cargo counted by coin value at the departure port, cap applied last
  public float AmbushChanceFor(int to) {
    float p = 0; float cargoVal = Enumerable.Range(0, S.goods.Length).Sum(g => hold[g] * price[port, g]);
    foreach (var st in ((List<object>)DZ.ambush["rollOrder"]).Select(Design.D)) switch ((string)st["step"]) {
      case "base": p = Design.F(st["value"]); break;
      case "addCargo": case "addCargoValue": p += Design.F(st.ContainsKey("per1000CoinsOfCargoValue") ? st["per1000CoinsOfCargoValue"] : st.ContainsKey("per1000Units") ? st["per1000Units"] : 0) * cargoVal / 1000f; break; /* data renamed the step to addCargoValue (coins of cargo value) */
      case "addCoinsAboard": p += Design.F(st["per1000Coins"]) * coins / 1000f; break;
      case "addContraband": if (Enumerable.Range(0, S.goods.Length).Any(g => hold[g] > 0 && S.goods[g].contraband)) p += Design.F(st["value"]); break;
      case "addFame": p += Design.F(st["perLevel"]) * fame; break;
      case "multiplyRiskyRoute": if (RiskyRoute(port, to)) p *= Design.F(st["value"], 1); break;
      case "cap": p = Mathf.Min(p, Design.F(st["value"], 1)); break; }
    return p; }
  string PickAmbushType() {
    var w = Design.D(DZ.Path(DZ.ambush, "typePick.weights")); bool nr = DZ.Path(DZ.ambush, "typePick.noRepeatInARow") is bool b && b;
    var pool = w.Keys.Where(k => !(nr && k == lastAmbush)).ToList(); float tot = pool.Sum(k => Design.F(w[k])), r = Rnd.value * tot;
    foreach (var k in pool) { r -= Design.F(w[k]); if (r <= 0) return lastAmbush = k; } return lastAmbush = pool[0]; }
  float Power(ShipStats s) => s.hull + s.sails * 0.5f + s.gunsPerSide * 15 + s.crew * 3;
  public float StrengthRatio => Power(S.combat.hasan) / Power(S.combat.pirate);
  float Cunning => Mathf.Min(0.6f, 0.3f + 0.04f * fame);
  float HoldValue => Enumerable.Range(0, S.goods.Length).Sum(g => hold[g] * price[port, g]);

  public IEnumerator AmbushEncounter(string type) {
    var card = ((List<object>)DZ.content["ambushCards"]).Select(Design.D).First(x => (string)x["type"] == type);
    lastAmbushOutcome = ""; /* ambush_customs_t6 */ ambushMood = type; ApplyMood(type); yield return AmbushApproach();
    var ir = Design.D(DZ.Path(DZ.ambush, "playerResponses.intimidate")); float fleeAt = Design.F(ir["fleeAtStrengthRatio"], 2), from = Design.F(ir["chanceScalesFromRatio"], 1);
    bool desperate = type == "nightRaid" && ir["namedAndDesperateNeverFlee"] is bool nd && nd; float ratio = StrengthRatio;
    float scare = desperate || ambushForced ? 0 : ratio >= fleeAt ? 1 : Mathf.Clamp01((ratio - from) / (fleeAt - from));
    if (Rnd.value < scare) { yield return AskCard("They turn away", (string)DZ.content["intimidated"], ("Sail on", null)); msg = "Intimidated, the pirate sheers off."; lastAmbushOutcome = "turned"; storyAmbushDone = true; Save(); /* ambush_customs_t6 */ StartCoroutine(PirateSailsOff()); /* gamedev_t7: runs alongside the result card */ EndMood(); yield break; }
    int cargo = HoldUsed, toll = Mathf.CeilToInt(cargo * S.combat.tollCargoPct); var tollSg = StoryGoods(); int tollable = LosableGoods().Sum(g => hold[g]); /* gamedev_t15_storysafe */ bool canToll = tollable > 0 && tollable >= toll; /* gamedev_t18_notoll */ /* gamedev_t8: toll only from non-story goods; hidden if they cannot cover it (no coin fallback) */ float wind = Rnd.value;
    float flee = Mathf.Clamp01((S.combat.encounterFleeBase + S.combat.fleeWindWeight * wind) * (type == "fog" ? 0.7f : type == "nightRaid" ? 1.2f : 1f));
    float baitPct = Design.F(DZ.Path(DZ.ambush, "playerResponses.bait.minCostPctOfHold"), 0.1f); int baitCost = Mathf.CeilToInt(BaitableValue * baitPct); /* gamedev_t7: story cargo excluded; 0 → no Bait button */
    bool canBluff = DZ.Path(DZ.ambush, "playerResponses.bluff.enabled") is bool bb && bb;
    int choice = 0; var odds = new List<string> { $"Run: {flee * 100:0}%" }; if (canToll) odds.Add($"Toll: {toll} of {tollable} cargo"); /* gamedev_t11_toll: count only cargo the toll can take (excl. StoryGoods) */ if (canBluff) odds.Add($"Bluff: {Cunning * 100:0}%"); /* gamedev_t9: odds line rebuilt from the options actually offered - no gaps when Toll/Bait are hidden */
    string body = $"<i>{card["warning"]}</i>\n{card["reveal"]}\n\n" + string.Join("   ·   ", odds) + (baitCost > 0 ? $"\nBait: dump goods worth {baitCost} coins and slip away while they fish them out." : ""); /* gamedev_t9 */
    yield return AskCard(type == "fog" ? "Ambush in the fog" : type == "nightRaid" ? "Night raid" : "False distress", body,
      ("Fight", (Action)(() => choice = 0)), ($"Run {flee * 100:0}%", (Action)(() => choice = 1)), canToll ? (cargo > 0 ? "Pay toll" : "Pay nothing", (Action)(() => choice = 2)) : ((string, Action))(null, null), /* gamedev_t8 */ canBluff ? ($"Bluff {Cunning * 100:0}%", (Action)(() => choice = 3)) : ((string, Action))(null, null), baitCost > 0 ? ("Bait", (Action)(() => choice = 4)) : ((string, Action))(null, null));
    lastAmbushChoice = choice;
    if (choice == 2) { PayCargo(toll, false, tollSg); /* gamedev_t8 */ msg = toll > 0 ? $"You hand over {toll} cargo. The pirates sheer off." : "Your hold is empty; they jeer and sheer off."; lastAmbushOutcome = "toll"; storyAmbushDone = true; Save(); /* ambush_customs_t6 */ StartCoroutine(PirateSailsOff()); /* gamedev_t7: runs alongside the result card */ EndMood(); yield break; }
    if (choice == 1 && Rnd.value < flee) { msg = "Hasan catches the gust and the pirate falls astern."; lastAmbushOutcome = "run"; storyAmbushDone = true; Save(); /* ambush_customs_t6 */ StartCoroutine(PirateSailsOff()); /* gamedev_t7: runs alongside the result card */ EndMood(); yield break; }
    if (choice == 3 && Rnd.value < Cunning) { msg = "Hasan roars about the navy squadron just over the horizon. They believe him and bear away."; lastAmbushOutcome = "bluff"; storyAmbushDone = true; Save(); /* ambush_customs_t6 */ StartCoroutine(PirateSailsOff()); /* gamedev_t7: runs alongside the result card */ EndMood(); yield break; }
    if (choice == 4) { DumpForBait(baitCost); msg = "Bales hit the water and the pirates turn to fish them out. You slip away."; lastAmbushOutcome = "bait"; storyAmbushDone = true; Save(); /* ambush_customs_t6 */ StartCoroutine(PirateSailsOff()); /* gamedev_t7: bait result card shows at once while the pirate turns away */ EndMood(); yield break; }
    if (choice == 1 || choice == 3) msg = "It doesn't work. They close to fight!";
    spareGunpowder = firstRun && !storyAmbushDone && DZ.Path(DZ.settings, "opening.spareGunpowderOnFirstAmbushLoss") is bool sg && sg;
    lastAmbushOutcome = choice == 1 || choice == 3 ? "fled_fail" : "fight"; /* ambush_customs_t6 */ yield return Fight(); spareGunpowder = false; storyAmbushDone = true; Save(); /* ambush_customs_t6: also covers fight after failed Run/Bluff */ EndMood();
  }
  public int lastAmbushChoice; public bool ambushForced;
  /* ambush_customs_t6: explicit outcome of the last AmbushEncounter, read by AmbushWithResult (designer_ambush_customs) instead of msg text */
  public string lastAmbushOutcome = ""; /* ambush_customs_t6 */
  IEnumerator PirateSailsOff() { if (pirateShip == null || !pirateShip.gameObject.activeSelf) yield break; Vector3 p0 = pirateShip.position; float y0 = pirateShip.eulerAngles.y; /* ambush_customs_t6 */
    for (float t = 0; t < 2.4f; t += Time.deltaTime) { float k = Mathf.SmoothStep(0, 1, t / 2.4f); pirateShip.rotation = Quaternion.Euler(0, Mathf.LerpAngle(y0, 60f, Mathf.Clamp01(t / 0.8f)), Mathf.Sin(t * 1.1f) * 4); pirateShip.position = p0 + new Vector3(70f, 0, 45f) * k; yield return null; } /* ambush_customs_t6 */
    pirateShip.gameObject.SetActive(false); } /* ambush_customs_t6 */
  /* gamedev_t7: goods the story or an active job still needs - never thrown to pirates. Opening good while its Muscat sale is pending; any active job objective naming a good. */
  HashSet<int> StoryGoods() { var r = new HashSet<int>(); /* gamedev_t7 */
    if (InOpening && !openingGpSold) { int og = G(Design.Path0(DZ, "opening.good", "gunpowder")); if (og >= 0) r.Add(og); } /* gamedev_t7 */
    foreach (var a in journal) { if (a.status != "active" || a.objective == null) continue; foreach (var k in new[] { "good", "goodId", "cargo" }) if (JGet(a.objective, k) is string gid) { int g = G(gid); if (g >= 0) r.Add(g); } } /* gamedev_t7 */
    return r; } /* gamedev_t7 */
  List<int> LosableGoods() { var sg = StoryGoods(); return Enumerable.Range(0, hold.Length).Where(g => hold[g] > 0 && !sg.Contains(g)).ToList(); } /* gamedev_t15_storysafe */
  List<int> SquallLosableGoods() { int og = G(Design.Path0(DZ, "opening.good", "gunpowder")); var l = LosableGoods(); if (!openingGpSold && og >= 0) l.Remove(og); return l; } /* gamedev_squall_storysafe: until openingGpSold, squalls never take the opening gunpowder (or other story cargo); trade goods or hull instead */
  public const float SquallStoryHullLoss = 5f; /* gamedev_squall_storysafe */
  float BaitableValue { get { return LosableGoods().Sum(/* gamedev_t15_storysafe */ g => hold[g] * price[port, g]); } } /* gamedev_t7 */
  void DumpForBait(int worth) { int gp = G("gunpowder"); var sg = StoryGoods(); /* gamedev_t7 */ while (worth > 0 && HoldUsed > 0) { var held = LosableGoods().Where(g => !(spareGunpowder && g == gp)) /* gamedev_t15_storysafe */.OrderBy(g => price[port, g]).ToList(); if (held.Count == 0) break; hold[held[0]]--; worth -= Mathf.CeilToInt(price[port, held[0]]); } }

  /// card sits in the bottom third so the pirate stays visible above it
  IEnumerator AskCard(string title, string body, params (string, Action)[] opts) { PasserHide("askcard:" + title); /* gamedev_t11_passer */
    opts = opts.Where(o => o.Item1 != null).ToArray(); bool done = false;
    while (CardBusy) yield return null; /* final modal_queue_back: queue (never waits on itself) */
    int perRow = opts.Length > 3 ? (opts.Length + 1) / 2 : opts.Length, /* gamedev_t9: 4 → 2+2, 5 → 3+2 (was 3+1 / 3+2 left-packed) */ rows = Mathf.Max(1, (opts.Length + perRow - 1) / Mathf.Max(1, perRow)); /* final: card sized to its measured body (was a fixed 330 px body box, leaving a gap above the buttons) */
    float bodyH = Mathf.Min(330f, CardBodyH(body, 800f)) /* art_t4_fixes: measured at the inset width */, btnY = 132 + bodyH + 24 + CardTitleFit(title, 740, T.fontSizeTitle).extra /* gamedev_title1 */ /* art_t12_cards: body moved to y 132 under the lowered rule */, cardH = Mathf.Max(420f, btnY + rows * 124 - 16 + 110); /* art_t5_fixes: ~110 px under the last button row, clear of the bottom brackets */
    var myDim = ModalDim("CardDim"); modalDepth++; cardSeq++; /* final ask_modal: own full-screen dim on root, above Content (whole HUD: SET SAIL, Journal, Options, top bar) and just below this card */
    var my = R(root, "Card", 0, 0, 1, 0); dialog = my; my.pivot = new Vector2(0.5f, 0); my.offsetMin = new Vector2(30, 30); my.offsetMax = new Vector2(-30, 30 + cardH); Box(my, T.panel, T.gold); var be = PushBack(title, () => my != null, null); /* final modal_queue_back: AskCard is always FORCED */
    var inner = R(my, "in", 0.5f, 1, 0.5f, 1); inner.sizeDelta = new Vector2(1000, cardH); inner.pivot = new Vector2(0.5f, 1); inner.anchoredPosition = Vector2.zero;
    var tf = CardTitle(inner, title, 130, 740, T.fontSizeTitle, T.parchment, TextAnchor.MiddleLeft, CardTitleFit(title, 740, T.fontSizeTitle)); /* gamedev_title1 */ /* art_t12_cards: off the top rail, between the corner brackets (Ask geometry) */ /* art_t12_cards: rule 8 px under the title descenders, between brackets */
    Label(inner, body, 100, 132 + tf.extra /* gamedev_title1 */, /* art_t12_cards: was y 104 */ 800, bodyH, /* art_t4_fixes: x 100–900, clear of the bracket arms */ T.fontSize, T.text, TextAnchor.UpperLeft);
    float w = (800f - (perRow - 1) * 16) / Mathf.Max(1, perRow); /* art_t5_fixes: buttons x 100–900 inside the 1000-wide card */ /* final2: title and buttons inset to x 95–905 so no button corner sits on a brass bracket (5-button ambush) */
    Debug.Log("GB CARD show " + title); /* gamedev_t9 */
    for (int i = 0; i < opts.Length; i++) { var o = opts[i]; int row = i / perRow, col = i % perRow; int inRow = Mathf.Min(perRow, opts.Length - row * perRow); float rowX = 100 + (800f - (inRow * w + (inRow - 1) * 16)) / 2f; /* gamedev_t9: a short last row is centred, same button width and 16 px gaps */ Btn(inner, o.Item1, rowX + col * (w + 16) /* art_t5_fixes */, btnY + row * 124, w, 108, () => { Debug.Log("GB CARD choose " + title + " " + o.Item1); /* gamedev_t9 */ o.Item2?.Invoke(); done = true; }, true, i == 0); }
    try { while (!done) yield return null; }
    finally { backStack.Remove(be); if (my) Destroy0(my.gameObject); if (myDim) Destroy0(myDim.gameObject); if (dialog == my) dialog = null; modalDepth = Mathf.Max(0, modalDepth - 1); } }
  int routeLogged = -1; /* gamedev_t9: route card logs show once per destination (it rebuilds on every Refresh) */
  int cardSeq; /* final modal_queue_back: bumped when an AskCard actually builds its card (PortraitCard waits for it) */ /* final ask_modal: own panel + own dim only (was: destroyed whatever dialog pointed at) */

  Light seaSun; Color sunCol0; float sunInt0; Color amb0, fog0; float fogS0, fogE0;
  void ApplyMood(string type) {
    if (seaSun == null) seaSun = sea3D.Find("Sun")?.GetComponent<Light>(); if (seaSun == null) return; sunCol0 = seaSun.color; sunInt0 = seaSun.intensity; amb0 = RenderSettings.ambientLight; fog0 = RenderSettings.fogColor; fogS0 = RenderSettings.fogStartDistance; fogE0 = RenderSettings.fogEndDistance;
    if (type == "fog") { RenderSettings.fogColor = new Color(0.62f, 0.66f, 0.68f); RenderSettings.fogStartDistance = 4; RenderSettings.fogEndDistance = 95; seaSun.intensity = 0.6f; RenderSettings.ambientLight = new Color(0.5f, 0.53f, 0.56f); }
    else if (type == "nightRaid") { RenderSettings.fogColor = new Color(0.07f, 0.08f, 0.18f); RenderSettings.fogStartDistance = 20; RenderSettings.fogEndDistance = 160; seaSun.color = new Color(0.55f, 0.62f, 1f); seaSun.intensity = 0.35f; RenderSettings.ambientLight = new Color(0.12f, 0.14f, 0.3f); }
    else { seaSun.color = new Color(1f, 0.78f, 0.45f); seaSun.intensity = 1.25f; RenderSettings.fogColor = new Color(0.95f, 0.78f, 0.5f); }
    cam.backgroundColor = RenderSettings.fogColor; }
  void EndMood() { ambushMood = ""; if (seaSun == null) return; seaSun.color = sunCol0; seaSun.intensity = sunInt0; RenderSettings.ambientLight = amb0; RenderSettings.fogColor = fog0; RenderSettings.fogStartDistance = fogS0; RenderSettings.fogEndDistance = fogE0; cam.backgroundColor = UITheme.C(T.skyHorizon); }
  /// the camera shows the pirate dhow turning toward Hasan before the card appears
  IEnumerator AmbushApproach() {
    if (pirateShip == null) pirateShip = Dhow.Build(sea3D, T, true, "PirateDhow"); pirateShip.gameObject.SetActive(true); sea3D.gameObject.SetActive(true); backdrop.color = Color.clear;
    fighting = true; /* holds the sailing camera still */ var hp = new Vector3(0, 0.15f, 0); ship.position = hp; ship.rotation = Quaternion.Euler(0, -8, 0);
    var pp = new Vector3(26, 0.1f, 42); pirateShip.position = pp; float y0 = 100, y1 = SafeRot.Look(hp - pp, Quaternion.identity).eulerAngles.y; /* gamedev_t8 */
    for (float t = 0; t < 2.2f; t += Time.deltaTime) { float k = Mathf.SmoothStep(0, 1, t / 2.2f);
      pirateShip.rotation = Quaternion.Euler(Mathf.Sin(t * 1.6f) * 2, Mathf.LerpAngle(y0, y1, k), Mathf.Sin(t * 1.1f) * 4 - k * 6); pirateShip.position = pp + (hp - pp).normalized * k * 6;
      cam.transform.position = hp + new Vector3(-9, 6.5f, -16); cam.transform.LookAt(Vector3.Lerp(hp, pirateShip.position, 0.72f) + Vector3.up * 5f); yield return null; }
    approachDone = true; fighting = false; }
  public bool approachDone;

  // ---------------- story: the gunpowder opening (v06_first_20_minutes.md) ----------------
  /* opening v2 (v06_first_20_minutes.md, Oct 15): prologue duel -> Gwadar dates/salt -> Debal sell + rumor -> Debal alley gunpowder -> Muscat (fog ambush, voyage 3) -> sale -> unlocks */
  public const int StDone = 8;
  public bool InOpening => firstRun && storyStep < StDone;
  bool OpenFlag(string k) => DZ != null && DZ.Path(DZ.settings, "opening." + k) is bool b && b;
  int OpenInt(string k, int d) => (int)Design.F(DZ?.Path(DZ.settings, "opening." + k), d);
  int Gp => G("gunpowder");
  int Muscat => PortIdx("Muscat"); int Debal => PortIdx("Debal");
  int BuyPort => Math.Max(0, PortIdx(Design.Path0(DZ, "opening.buyPort", "Debal")));
  /// warehouse, tithe, bank and lender stay hidden until the Muscat sale
  public bool Hidden(int tab) {
    if (InOpening && storyStep < 7 && ((tab == 2 && OpenFlag("hideWarehouseUntilAfterMuscat")) || tab == 5 || tab == 6)) return true;
    return !BuildingAvailable(tab); /* v0.6j town service mix */
  }
  public bool TitheHidden => InOpening && storyStep < 7 && OpenFlag("hideTitheUntilAfterMuscat");
  public bool AdsUnlocked => !InOpening && sails >= OpenInt("adsUnlockAfterVoyage", 4);
  /// returns the building tab the tutorial points at (-2 = chart table button, -1 = none)
  public int TutorialTarget() {
    if (!InOpening || sailing || fighting) return -1;
    switch (storyStep) {
      case 0: return port == GwadarIdx ? 0 : -2; case 1: return -2; case 2: return 0; case 3: return 8; case 4: return port == BuyPort && hold[Gp] == 0 && !fallbackFired ? 1 : -2; case 5: return -2; case 6: return fallbackFired ? 0 : 1; case 7: return 8; }
    return -1; }
  public string QarinPowderLine => openingGpSold ? "The garrison has its powder. My thanks, captain." : "Bring powder."; /* research_gp3: Game Designer ruling, thank-you once sold */
  (string, string, string) StoryGoals() {
    string l = qarinLetter ? "The Qarin seal: a letter names a harbour master in Muscat" : "Find who burned your ship: the Qarin seal";
    string m = "Make a fortune on one daring run";
    switch (storyStep) {
      case 0: return ("Buy cheap dates and salt at the bazaar (Market)", m, l);
      case 1: return ("Set sail for Debal on the chart table (safe coastal route)", m, l);
      case 2: return ("Sell your dates and salt in Debal's market", m, l);
      case 3: return ("Hear the harbour talk at Debal's tavern", m, l);
      case 4: return rumorHeard >= 2 && hold[Gp] == 0 ? ("No gunpowder? Copper is short in Muscat: buy copper in Gwadar", "Sell it in Muscat", l) : (GpBuyGoal, GpSellGoal, l) /* research_gp3 */;
      case 5: return ("Set sail for Muscat on the chart table", fallbackFired || hold[Gp] == 0 ? "Sell copper in Muscat's market" : GpSellGoal, l);
      case 6: return fallbackFired ? ("Sell your copper in Muscat's market (shortage, x2)", "Then: warehouse, bank and lender open up", l) : (GpSellGoal, "Then: warehouse, bank and lender open up", l);
      case 7: return ("Ask at the tavern about the Qarin seal", "Save up for a bigger ship", l);
    } return ("", "", l); }
  /// advances the opening from the real game state; called after every Refresh
  public void StoryTick() {
    if (!InOpening) return; int cp = G("copper");
    if (storyStep == 0 && (hold[G("dates")] > 0 || hold[G("salt")] > 0)) storyStep = 1;
    if (storyStep == 1 && port == Debal) storyStep = 2;
    if (storyStep == 2 && port == Debal && hold[G("dates")] == 0 && hold[G("salt")] == 0) storyStep = 3;
    if (storyStep == 4 && hold[Gp] > 0) storyStep = 5;
    if (storyStep == 4 && rumorHeard >= 2 && hold[cp] > 0) storyStep = 5;
    if (storyStep == 5 && port == Muscat) storyStep = 6;
    if (storyStep == 4 && port == Muscat) storyStep = 6;
    /* v0.6h4: advance only after a real sale — confiscation leaves hold empty but openingGpSold stays false */
    if (storyStep == 6 && port == Muscat && hold[Gp] == 0 && (jackpotStartCoins > 0 || fallbackFired)
        && (fallbackFired ? hold[cp] == 0 : openingGpSold)) { storyStep = 7;
      float mult = jackpotStartCoins > 0 ? coins / (float)jackpotStartCoins : 0; news.Insert(0, $"The {(fallbackFired ? "copper" : "gunpowder")} run: {jackpotStartCoins} coins before, {coins} after ({mult:0.00}x). The warehouse, tithe, bank and lender are open to you now."); }
  }
  /// tavern beats: the scripted ban rumor (Debal), its one repeat, and the Qarin letter at the end
  const string Rumor2 = "They say Muscat's garrison is starved for powder. A crate that costs a sack of dates here fetches a chest of silver there, if customs doesn't catch you.";
  string TavernStory() {
    if (!InOpening) return null; var rs = ((List<object>)DZ.content["rumors"]).Select(Design.D).First(x => (string)x["type"] == "ban");
    string ban = "Muscat's customs seize gunpowder on sight, but the garrison still pays a fortune for it under the table. Farida in Debal's Back Alley sells it cheap. Sell it in Muscat's Back Alley."; /* v0.6f4 wording; rs kept for the data check */ _ = rs;
    if (storyStep == 3 || (storyStep == 2 && port == Debal)) { /* the rumor can't be missed: hearing it before selling everything still moves the opening on */ storyStep = 4; rumorHeard = 1; gpBanStart = week + 1; EnsureDesignBans(); news.Insert(0, $"Rumor ({DateStr}): {ban}"); return OpenFlag("secondRumorOnFirstTavernVisit") ? ban + " " + Rumor2 : ban; }
    if (storyStep == 4 && hold[Gp] == 0 && rumorHeard < 2) { rumorHeard++; return Rumor2; }
    if (storyStep == 7) { storyStep = StDone; qarinLetter = true; return (boardedStoryPirate ? "A letter from the fog pirate's strongbox, sealed with the Qarin mark: " : "The tavern keeper slides you a letter sealed with the Qarin mark: ") + "\"To the captain who carries this seal: the Sultan's customs in Muscat look away for those who bear the Qarin mark. " + QarinPowderLine + " Ask for the man in the blue doorway. – Q.\""; }
    return null; }
  /// a player who skipped the gunpowder after the rumor repeats gets the backup: a forced copper shortage in Muscat
  void FallbackCheck() {
    if (!InOpening || fallbackFired || port != Muscat) return; var fb = Design.D(DZ.Path(DZ.settings, "opening.fallbackShortage")); int need = 1 + (int)Design.F(fb["triggerIfNoGunpowderAfterRumorRepeats"], 1);
    if (rumorHeard >= need && hold[Gp] == 0 && !wh[GwadarIdx].items.Any(b => b.good == Gp)) { fallbackFired = true; if (jackpotStartCoins < 0) jackpotStartCoins = coins + hold[G((string)fb["good"])] * Mathf.CeilToInt(AvgCost[G((string)fb["good"])]); int g = G((string)fb["good"]); SetEv(port, g, Design.F(fb["mult"], 2), week + 2);
      var c = Content("shortage"); news.Insert(0, Fill((string)c["headline"], port, g) + " " + Fill((string)c["text"], port, g)); msg = $"Muscat is short of {S.goods[g].name.ToLower()}: it sells at twice the usual price."; storyStep = 6; }
  }

  // ---------------- repairs ----------------
  public void ApplyDamageToFight() { if (core == null) return; if (hullNow >= 0) core.H.hull = Mathf.Min(hullNow, core.H.hull0); if (sailsNow >= 0) core.H.sails = Mathf.Min(sailsNow, core.H.sails0); }
  public void StoreDamageFromFight(Outcome o) { if (core == null) return; hullNow = o == Outcome.HasanSunk ? core.H.hull0 * 0.15f : Mathf.Max(1, core.H.hull); sailsNow = Mathf.Max(core.H.sails0 * 0.1f, core.H.sails); }
  float HullMax => S.combat.hasan.hull; float SailsMax => S.combat.hasan.sails;
  float HullCur => hullNow < 0 ? HullMax : Mathf.Min(hullNow, HullMax); float SailsCur => sailsNow < 0 ? SailsMax : Mathf.Min(sailsNow, SailsMax);
  int RepairCost => Mathf.CeilToInt(((HullMax - HullCur) + (SailsMax - SailsCur) * 0.5f) * S.shipwright.repairPerHullPoint);
  void Repair() { hullNow = HullMax; sailsNow = SailsMax; }

  // ---------------- ads (stub) ----------------
  int adsThisVisit, adsThisSession; float lastAdT = -9999;
  public string AdBlockReason() { var a = S.ads; if (!AdsUnlocked) return "ads open after the Muscat run"; if (adsThisVisit >= a.maxPerPortVisit) return "one per port visit"; if (adsThisSession >= a.maxPerSession) return "session limit"; if (AdsToday() >= a.maxPerDay) return "daily limit";
    if (Time.realtimeSinceStartup - lastAdT < a.minSecondsApart) return $"wait {Mathf.CeilToInt(a.minSecondsApart - (Time.realtimeSinceStartup - lastAdT))} s"; return null; }
  int AdsToday() => PlayerPrefs.GetString("gbAdDay") == DateTime.Now.ToString("yyyyMMdd") ? PlayerPrefs.GetInt("gbAdCount") : 0;
  public IEnumerator ShowAd(string placement, Action reward) {
    if (AdBlockReason() != null) { msg = "No ad available: " + AdBlockReason() + "."; Refresh(); yield break; }
    var ov = R(root, "AdStub", 0, 0, 1, 1); ov.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.85f); var t = Txt(R(ov, "t", 0.1f, 0.4f, 0.9f, 0.6f), "", T.fontSizeTitle, T.parchment, TextAnchor.MiddleCenter, true);
    for (int s = 3; s > 0; s--) { t.text = $"Sponsored message (stub)\n<size={T.fontSizeSmall}>{placement} · reward in {s}</size>"; yield return new WaitForSeconds(shotMode || playthrough ? 0.3f : 1f); }
    Destroy0(ov.gameObject); adsThisVisit++; adsThisSession++; lastAdT = Time.realtimeSinceStartup; PlayerPrefs.SetString("gbAdDay", DateTime.Now.ToString("yyyyMMdd")); PlayerPrefs.SetInt("gbAdCount", AdsToday() + 1);
    reward?.Invoke(); Refresh(); }

  // ---------------- quality tiers ----------------
  public int quality = -1; static readonly string[] QNames = { "Low", "Medium", "High" };
  public void ApplyQuality(int q) {
    quality = q; PlayerPrefs.SetInt("gbQuality", q);
    QualitySettings.SetQualityLevel(Mathf.Clamp(q, 0, QualitySettings.names.Length - 1), true); /* k4: switch level FIRST, then edit the active URP asset */
    var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
    if (urp) { urp.renderScale = q == 0 ? 0.7f : q == 1 ? 0.85f : 1f; urp.msaaSampleCount = q == 0 ? 1 : q == 1 ? 2 : 4; urp.shadowDistance = q == 0 ? 0 : q == 1 ? 40 : 70; }
    QualitySettings.shadows = q == 0 ? ShadowQuality.Disable : ShadowQuality.All; if (urp) urp.renderScale = q == 0 ? 0.7f : q == 1 ? 0.85f : 1f; Application.targetFrameRate = q == 2 ? 60 : 30; if (cam) { cam.allowMSAA = q > 0; var cd = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); if (cd) { cd.renderShadows = q > 0; cd.renderPostProcessing = q == 2; } } msg = $"Graphics: {QNames[q]}."; Debug.Log($"GB QUALITY {q} renderScale {(urp ? urp.renderScale : -1)} shadows {QualitySettings.shadows}"); if (town3D) { var h = town3D.Find("Hills"); if (h) h.gameObject.SetActive(q > 0); foreach (var f in folk) if (f.t) f.t.gameObject.SetActive(q > 0 || f.idx % 2 == 0); } }
  int DefaultQuality() => SystemInfo.systemMemorySize < 3000 ? 0 : SystemInfo.systemMemorySize < 6000 ? 1 : 2;
  void OptionsMenu() { StartCoroutine(AskD("Options", $"Difficulty: {S.combat.difficulties[difficulty].name}\nGraphics: {QNames[Mathf.Max(0, quality)]}", ("Difficulty", () => OptionsDialog()), ("Graphics", () => StartCoroutine(AskD("Graphics quality", "High: full resolution, shadows, 60 fps.\nMedium: 85% resolution, short shadows.\nLow: 70% resolution, no shadows, fewer townsfolk.",
      ("Low", () => ApplyQuality(0)), ("Medium", () => ApplyQuality(1)), ("High", () => ApplyQuality(2))))), ("Credits", () => ShowCredits(true)), ("Close", null))); } /* back_market_t6: Credits from Options returns to Options */
  /* final: Credits gets its own card (same look as Ask) with a centred 600 px Close, so the button ends clear the bottom corner brackets (~95 px each).
     Ask() is untouched; this panel owns and closes only itself. */
  RectTransform creditsCard, creditsDim; /* final credits_modal: Credits owns its panel AND its dimmer (same pattern as the job-offer myDim fix) */
  bool creditsFromOptions; /* back_market_t6 */
  void CloseCredits() {
    var cp = creditsCard; if (cp) Destroy0(cp.gameObject); if (creditsDim) Destroy0(creditsDim.gameObject);
    if (cp != null && dialog == cp) dialog = null; creditsCard = null; creditsDim = null;
    if (cp != null && creditsFromOptions && !onTitle) { creditsFromOptions = false; OptionsMenu(); } creditsFromOptions = false; } /* back_market_t6: reopen Options (its own fresh dim + Back entry; the old Options card already closed) */
  void ShowCredits(bool fromOptions = false) { /* back_market_t6 */
    if (creditsCard != null) return; creditsFromOptions = fromOptions; /* back_market_t6 */ /* final credits_modal: a second open while one is showing is ignored (double tap stacked two panels) */
    const string body = "Galeborne\n\nShip models: Watercraft Kit by Kenney (kenney.nl)\nCharacter models: KayKit Adventurers by Kay Lousberg (kaylousberg.com)\nAll models CC0.\n\nThank you for sailing with us.";
    float bodyH = CardBodyH(body, 700f), by = 106 + bodyH + 20, panelH = by + 84 + 110; /* art_t6_fixes: body measured at x 100–800; Close lifted ~110 px off the bottom brackets */ /* final credits_modal: 780 like Ask() */
    float top = Mathf.Max(180f, (1920f - panelH) * 0.38f);
    var dim = ModalDim("CreditsDim"); creditsDim = dim; /* final modal_queue_back: shared ModalDimColor */ /* blocks Options/Journal taps behind the card */
    var cp = Px(root, "Dialog", 90, top, 900, panelH); creditsCard = cp; dialog = cp; Box(cp, T.panel, T.gold); dim.SetAsLastSibling(); cp.SetAsLastSibling();
    Label(cp, "Credits", 130, 22, 640, 52, /* art_t8_brackets: x 130, clear of the top-left bracket */ /* final2: clear of the top-left bracket */ T.fontSizeTitle, T.parchment, TextAnchor.MiddleLeft, true); Box(Px(cp, "rule", 30, 72, 840, 2), T.brass);
    Label(cp, body, 100, 106, 700, bodyH, /* art_t6_fixes: like Ask() */ T.fontSize, T.text, TextAnchor.UpperLeft); /* final credits_modal: 30 px inset each side (x 60-840) */
    Btn(cp, "Close", 250, by, 400, 84, /* art_t6_fixes: centred, x 250–650 */ () => CloseCredits(), true, true); PushBack("Credits", () => creditsCard != null, CloseCredits); /* final modal_queue_back: DISMISSABLE */
  }
}
public partial class Game {
  /// v0.6i SET SAIL art button: cream serif caps, press-down on tap, short send-off
  Button SetSailBtn(Transform p, float x, float y, float w, float h, string label, Action on) {
    var r = Px(p, "SetSail", x, y, w, h); var tex = Resources.Load<Texture2D>("Art/ui_setsail_button");
    if (tex) { var ri = r.gameObject.AddComponent<RawImage>(); ri.texture = tex; ri.raycastTarget = true; }
    else Box(r, T.panelAlt, T.gold);
    var b = r.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None;
    var et = r.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
    void Add(UnityEngine.EventSystems.EventTriggerType ty, System.Action<UnityEngine.EventSystems.BaseEventData> fn) { var e = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = ty }; e.callback.AddListener(ed => fn(ed)); et.triggers.Add(e); }
    Add(UnityEngine.EventSystems.EventTriggerType.PointerDown, _ => { r.localScale = new Vector3(0.97f, 0.94f, 1f); r.anchoredPosition += new Vector2(0, -4); });
    Add(UnityEngine.EventSystems.EventTriggerType.PointerUp, _ => { r.localScale = Vector3.one; });
    Add(UnityEngine.EventSystems.EventTriggerType.PointerExit, _ => { r.localScale = Vector3.one; });
    b.onClick.AddListener(() => { msg = "Fair winds and a following sea, Captain."; on(); });
    var cream = "#F5ECD8"; Txt(R(r, "L", 0, 0, 1, 1), string.IsNullOrEmpty(label) ? "SET SAIL" : label, T.fontSizeTitle, cream, TextAnchor.MiddleCenter, true);
    return b; }
    float chartScroll = -1;
  Vector2 chartPanPx; bool chartCentered; /* pixel offset of the oversized chart inside the viewport; clamped */
  const float ChartScale = 1.55f; /* map larger than viewport so drag-to-pan has room */
  /// approximate lon/lat → portrait map (0..1)
  static readonly Dictionary<string, Vector2> ChartGeo = new Dictionary<string, Vector2> { { "Hormuz", new Vector2(0.17f, 0.22f) }, { "Gwadar", new Vector2(0.72f, 0.33f) }, { "Debal", new Vector2(0.77f, 0.51f) }, { "Thatta", new Vector2(0.86f, 0.64f) }, { "Sohar", new Vector2(0.28f, 0.53f) }, { "Muscat", new Vector2(0.49f, 0.66f) } };
  /// full-screen chart with ship marker + drag-to-pan (touch and mouse), markers move with the map
  void ChartFull() {
    if (town3D) town3D.gameObject.SetActive(false); sea3D.gameObject.SetActive(false); backdrop.color = new Color(0.16f, 0.12f, 0.08f); dimmer.gameObject.SetActive(false); TopBar();
    var tex = Resources.Load<Texture2D>("Art/chart"); float top = 172, full = Mathf.Max(1800, root.rect.height), cardH = chartDest >= 0 ? Mathf.Round((full - top) * 0.25f) : 210, H = full - top - cardH - 16, W = 1080;
    float mapW = W * ChartScale, mapH = H * ChartScale;
    if (!chartCentered) { var g0 = ChartGeo.TryGetValue(S.ports[chartCenterOn >= 0 ? chartCenterOn : port].name, out var gg) ? gg : new Vector2(0.5f, 0.5f); chartPanPx = new Vector2(W * 0.5f - g0.x * mapW, g0.y * mapH - H * 0.5f); chartCentered = true; } /* v0.6k3: open centred on current port */
    chartPanPx.x = Mathf.Clamp(chartPanPx.x, W - mapW, 0f); chartPanPx.y = Mathf.Clamp(chartPanPx.y, 0f, mapH - H); /* pivot top: +y reveals the bottom (Muscat) */
    var viewport = Px(content, "MapView", 0, top, W, H); viewport.gameObject.AddComponent<RectMask2D>();
    var map = Px(viewport, "Map", 0, 0, mapW, mapH); map.anchoredPosition = chartPanPx; /* pivot (0,1): clamp keeps map covering the viewport */
    var ri = map.gameObject.AddComponent<RawImage>(); ri.texture = tex; ri.color = new Color(1, 0.97f, 0.9f); ri.raycastTarget = true;
    /* drag-to-pan: pointer drag (mouse + touch), clamped to bounds */
    var et = map.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
    var entry = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.Drag };
    entry.callback.AddListener(ed => { var d = (UnityEngine.EventSystems.PointerEventData)ed;
      chartPanPx.x = Mathf.Clamp(chartPanPx.x + d.delta.x, W - mapW, 0f); chartPanPx.y = Mathf.Clamp(chartPanPx.y + d.delta.y, 0f, mapH - H);
      map.anchoredPosition = chartPanPx; });
    et.triggers.Add(entry);
    Vector2 P(string n) { var v = ChartGeo.TryGetValue(n, out var g) ? g : new Vector2(0.5f, 0.5f); return new Vector2(v.x * mapW, v.y * mapH); }
    if (chartDest >= 0 && chartDest != port) { var a = P(S.ports[port].name); var b = P(S.ports[chartDest].name); var mid = (a + b) / 2 + new Vector2(0, Vector2.Distance(a, b) * 0.18f);
      for (int i = 1; i < 40; i++) { float tt = i / 40f; if (i % 3 == 2) continue; var p0 = (1 - tt) * (1 - tt) * a + 2 * (1 - tt) * tt * mid + tt * tt * b; var d = Px(map, "ink", p0.x - 7, p0.y - 7, 14, 14).gameObject.AddComponent<Image>(); d.sprite = Dot(); d.color = UITheme.C("#4A2A12"); d.raycastTarget = false; } }
    foreach (var kv in ChartGeo) { int pi = PortIdx(kv.Key); var p = P(kv.Key); bool known = pi >= 0;
      var m = Px(map, "town " + kv.Key, p.x - 46, p.y - 46, 92, 92); var mi = m.gameObject.AddComponent<Image>(); mi.sprite = Ring(0.0f); mi.color = UITheme.C(pi == port ? T.marker : pi == chartDest ? T.gold : known ? "#5A3A16" : "#9A8A70");
      var mc = Px(m, "keep", 26, 22, 40, 48).gameObject.AddComponent<Image>(); mc.sprite = Tri(); mc.color = UITheme.C(known ? "#E6C27A" : "#C8BBA0"); mc.raycastTarget = false;
      var nb = Px(map, "plaque " + kv.Key, Mathf.Clamp(p.x - 140, 8, mapW - 288), p.y + 50, 280, 64); Box(nb, known ? "#C9A050" : "#B0A080", "#4A2A10"); var nin = Stretch(nb, "in", 5, 5, 5, 5); Box(nin, known ? "#E2BE6E" : "#CDBF9E", "#8A6224");
      Txt(R(nin, "t", 0, 0, 1, 1), kv.Key + (pi == port ? " (here)" : known ? "" : " · uncharted"), T.fontSizeSmall - 2, "#1A120A", TextAnchor.MiddleCenter, true);
      { var fl = known ? (EgMapFlag(pi) ?? GpMapFlag(pi)) : null; /* research_gp1 */ if (fl != null) { var fb = Px(map, "egflag " + kv.Key, p.x - 120, p.y - 104, 240, 52); Box(fb, "#2A1E14", "#C9A050"); Txt(R(fb, "t", 0, 0, 1, 1), fl, T.fontSizeSmall - 4, "#E6C27A", TextAnchor.MiddleCenter, true); } } /* gamedev_end1: brass flag with moons left */
      if (known && blockGood[pi] >= 0 && blockKnown[pi]) BlkMapIcon(map, pi, p); /* gamedev_blockade_warning */
      if (known && pi != port) { int k = pi; foreach (var go in new[] { m.gameObject, nb.gameObject }) { var bt = go.AddComponent<Button>(); bt.onClick.AddListener(() => { chartDest = k; Refresh(); }); } } }
    /* v0.6i: you-are-here ship marker (~64px at 1080), rotated toward destination or NE */
    { var here = P(S.ports[port].name); var mtex = Resources.Load<Texture2D>("Art/ui_map_ship_marker"); float ms = 104; /* k4: bigger you-are-here marker */
      var mr = Px(map, "ShipHere", here.x - ms / 2, here.y - ms / 2, ms, ms);
      mr.pivot = new Vector2(0.5f, 0.5f); mr.anchoredPosition = new Vector2(here.x, -here.y); /* final: rotate about the marker centre (Px pivot is top-left, so the route heading swung it off the port) */
      if (mtex) { var mri = mr.gameObject.AddComponent<RawImage>(); mri.texture = mtex; mri.raycastTarget = false; }
      else { var mi = mr.gameObject.AddComponent<Image>(); mi.sprite = Tri(); mi.color = UITheme.C(T.gold); mi.raycastTarget = false; }
      float ang = -45f; if (chartDest >= 0 && chartDest != port) { var to = P(S.ports[chartDest].name) - here; ang = Mathf.Atan2(-to.y, to.x) * Mathf.Rad2Deg - 90f; }
      mr.localRotation = Quaternion.Euler(0, 0, ang); }
    Btn(content, "‹  Town", 16, top + 14, 240, 84, () => { tab = -1; chartDest = -1; chartCentered = false; Refresh(); });
    var card = R(content, "RouteCard", 0, 0, 1, 0); card.pivot = new Vector2(0.5f, 0); card.offsetMin = new Vector2(16, 16); card.offsetMax = new Vector2(-16, 16 + (chartDest >= 0 && chartDest != port ? Mathf.Max(cardH, 640) : 120)); Box(card, T.panel, T.gold);
    var ci = R(card, "in", 0, 1, 0, 1); ci.pivot = new Vector2(0, 1); ci.sizeDelta = new Vector2(1048, 640); ci.anchoredPosition = Vector2.zero;
    if (chartDest < 0 || chartDest == port) { Label(ci, "Tap a port on the chart to plan a route. Drag to pan the map.", 26, 14, 1000, 90, T.fontSize, T.parchment, TextAnchor.MiddleCenter); return; }
    int dp = chartDest; float ap = AmbushChanceFor(dp); bool risky = RiskyRoute(port, dp); if (routeLogged != dp) { routeLogged = dp; Debug.Log("GB CARD show Route to " + S.ports[dp].name); } /* gamedev_t9 */
    Label(ci, $"To {S.ports[dp].name}", 100, 16, 440, 76, /* final3: title starts 100 px in, clear of the ~95–110 px top-left bracket */ T.fontSizeTitle + 4, T.parchment, TextAnchor.MiddleLeft, true);
    bool calmR = sails + 1 <= S.pirateFreeSails || sails + 1 < safeUntilSail; bool fogCard = InOpening && !storyAmbushDone && dp == Muscat && hold[Gp] > 0; var routeLbl = Label(ci, fogCard ? "Open sea route" : risky ? "Open water (pirate odds ×2)" : calmR ? "Safe coastal route" : "Coastal route, calm seas", 548, 62, 370, 34, /* art_t13_route: below the gold top rule (~y52) with ~10 px gap, above the danger row (y100); ends x918 = cardW-130 */ /* art_t8_brackets: ends at x 900 (148 px inside), clear of the top-right bracket */ /* art_t7_fixes: ends at x 938 (110 px inside the 1048 card), clear of the top-right bracket */ T.fontSizeSmall, risky ? T.down : T.up, TextAnchor.MiddleRight); routeLbl.horizontalOverflow = HorizontalWrapMode.Wrap; routeLbl.verticalOverflow = VerticalWrapMode.Truncate; routeLbl.resizeTextForBestFit = true; routeLbl.resizeTextMinSize = 18; routeLbl.resizeTextMaxSize = T.fontSizeSmall; /* art_t12b_route: x548-938 (110 px inside 1048, 8 px right of title end 540), best-fit keeps it one line */
    float y = 100; bool fogAhead = InOpening && !storyAmbushDone && dp == Muscat && hold[Gp] > 0; string danger = fogAhead ? $"<color={T.gold}>Moderate pirate danger (25%)</color>" : DangerLabel(ap, sails + 1 <= S.pirateFreeSails || sails + 1 < safeUntilSail);
    Label(ci, danger, 100, y, 848, /* final modal_queue_back: x 100-948 like title/SET SAIL */ 56, T.fontSize, T.text); y += 62; if (fogAhead) { Label(ci, $"<color={T.gold}>Fog banks reported on this route</color>", 100, y, 848, /* final modal_queue_back: x 100-948 like title/SET SAIL */ 50, T.fontSizeSmall, T.text); y += 54; }
    var bannedHere = Enumerable.Range(0, S.goods.Length).Where(g => Banned(g, dp) || bans.Any(b => b.good == g && b.port == dp && b.start <= week + 1 && b.end > week)).Select(g => S.goods[g].name).ToList();
    Label(ci, bannedHere.Count > 0 ? $"Banned on arrival (back alley pays ×{S.contraband.bannedSellMult:0}): " + string.Join(", ", bannedHere) : "No bans reported.", 100, y, 848, /* final modal_queue_back: x 100-948 like title/SET SAIL */ 56, T.fontSizeSmall, T.textMuted); y += 58;
    if (blockGood[dp] >= 0 && hold[blockGood[dp]] > 0) { Label(ci, $"<color={BlkAmber}>{BlkRoutePreview(dp)}</color>", 100, y, 848, 56, T.fontSizeSmall, T.text); y += 58; } /* gamedev_blockade_warning: amber route preview */
    else if (blockGood[dp] >= 0 && blockKnown[dp]) { Label(ci, $"<color={T.down}>Blockade: {S.goods[blockGood[dp]].name} will be seized on arrival.</color>", 100, y, 848, /* final modal_queue_back: x 100-948 like title/SET SAIL */ 56, T.fontSizeSmall, T.text); y += 58; }
    bool carries = Enumerable.Range(0, S.goods.Length).Any(g => hold[g] > 0 && bannedHere.Contains(S.goods[g].name));
    if (carries) { Label(ci, CustomsWaveReason(dp) == "papers" ? $"<color={T.up}>Customs: waved through (Farida's papers)</color>" /* gamedev_l366 */ : (InOpening && OpenFlag("firstRunCustomsAlwaysPass")) || sails + 1 < OpenInt("customsRiskFromVoyage", 4) ? $"<color={T.up}>Customs: likely waved through</color>" /* art_t5_fixes */ : $"<color={T.down}>Customs risk on docking: {Risk(dp):0}%</color>", 100, y, 848, 56, T.fontSizeSmall, T.textMuted); y += 58; } /* art_t5_fixes: customs/Farida line on its own row so "Prices unknown" never squeezes against it */
    Label(ci, "Prices unknown until you dock." /* art_t5_fixes */ + (HullCur < HullMax * 0.6f ? $"   <color={T.down}>Hull {HullCur:0}/{HullMax:0}</color>" : ""), 100, y, 848, /* final modal_queue_back: x 100-948 like title/SET SAIL */ 56, T.fontSizeSmall, T.textMuted); y += 70;
    float routeH = Mathf.Max(cardH, y + 140 + 24); card.offsetMax = new Vector2(-16, 16 + routeH); ci.sizeDelta = new Vector2(1048, routeH); /* final2: card sized to its content (was fixed 640 with Set Sail pinned at 470 → big empty gap); never shorter than the chart cut-out */
    SetSailBtn(ci, 100, y, 848, 140, /* final3: x 100–948 so the rope ends clear the bottom brackets */ $"SET SAIL FOR {S.ports[dp].name.ToUpper()}", () => { Debug.Log("GB CARD choose Route to " + S.ports[dp].name + " Set sail"); routeLogged = -1; /* gamedev_t9 */ chartDest = -1; chartScroll = -1; chartCentered = false; StartCoroutine(Voyage(dp)); });
  }

  /// one danger wording for town and chart (design ambush.dangerLabels); pirate-free voyages are "Calm waters"
  public string DangerLabel(float ap, bool free) { if (free) return $"<color={T.up}>Calm waters</color>";
    float lo = Design.F(DZ?.Path(DZ.settings, "ambush.dangerLabels.low"), 0.15f), mo = Design.F(DZ?.Path(DZ.settings, "ambush.dangerLabels.moderate"), 0.3f), hi = Design.F(DZ?.Path(DZ.settings, "ambush.dangerLabels.high"), 0.45f);
    string pct = $" ({ap * 100:0}%)"; return ap <= lo ? $"<color={T.up}>Low pirate danger{pct}</color>" : ap <= mo ? $"<color={T.gold}>Moderate pirate danger{pct}</color>" : ap <= hi ? $"<color={T.down}>High pirate danger{pct}</color>" : $"<color={T.down}><b>Deadly waters{pct}</b></color>"; }
  public int giftPending; public string gbForce = "";
  public bool prologuePending; public float lastInput; public int nudges;
  /// Act 1 beat 1: the burning dhow. One duel that can't be lost (about three STRIKE taps), the raider drops the Qarin seal, then Gwadar at dawn.
  IEnumerator Prologue() {
    fighting = true; Clear(content);
    /* key-art lighting, cheap: cool moonlight one side, warm firelight from the burning sail on the other, UI embers; restored after */
    var amb0 = RenderSettings.ambientLight; RenderSettings.ambientLight = new Color(0.07f, 0.09f, 0.2f); var lights = FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.enabled && l.type == LightType.Directional).ToList(); foreach (var l in lights) l.enabled = false;
    var moon = new GameObject("Moon").AddComponent<Light>(); moon.type = LightType.Directional; moon.color = new Color(0.55f, 0.68f, 1f); moon.intensity = 0.3f; moon.shadows = LightShadows.Soft; moon.transform.rotation = Quaternion.LookRotation(new Vector3(0.6f, -0.5f, 0.5f));
    var fire = new GameObject("SailFire").AddComponent<Light>(); fire.type = LightType.Point; fire.color = new Color(1f, 0.52f, 0.18f); fire.range = 7; fire.intensity = 4f; fire.transform.position = ship.TransformPoint(new Vector3(1.0f, 5.5f, 2.0f)); /* lights the sail, not the raider face */ StartCoroutine(Flicker(fire));
    bool embersOn = false; /* UI embers replaced by real fire particles on the sail */
    var night = Stretch(root, "PrologueNight", 0, 0, 0, 0); night.SetAsFirstSibling(); var ni = night.gameObject.AddComponent<Image>(); ni.color = new Color(0.04f, 0.07f, 0.2f, 0.55f); ni.raycastTarget = false;
    ni.color = new Color(0.01f, 0.03f, 0.14f, 0.62f); night.offsetMax = new Vector2(0, 400); night.offsetMin = new Vector2(0, -400); /* cover the safe-area gap too: no white band */ /* night-blue tint */
    if (town3D == null) BuildTown(); town3D.gameObject.SetActive(false);
    var deck = new GameObject("PrologueDeck").transform; deck.SetParent(ship, false);
    GameObject Fig(int kind, Vector3 lp, float ry) { var g = new GameObject("fig"); g.transform.SetParent(deck, false); g.AddComponent<MeshFilter>().sharedMesh = FolkMesh(kind); g.AddComponent<MeshRenderer>().sharedMaterial = townMat; g.transform.localPosition = lp; g.transform.localRotation = Quaternion.Euler(0, ry, 0); g.transform.localScale = Vector3.one * 1.15f; return g; }
    GameObject Fighter(bool raider, Vector3 lp, float ry) { var g = new GameObject(raider ? "Raider" : "Hasan"); g.transform.SetParent(deck, false); var parts = FighterMesh(raider);
      foreach (var kv in parts) { var pg = new GameObject(kv.Key); pg.transform.SetParent(g.transform, false); pg.AddComponent<MeshFilter>().sharedMesh = kv.Value; var mr = pg.AddComponent<MeshRenderer>(); if (kv.Key == "F_Face") { var fm = new Material(Resources.Load<Material>("Mats/F_Skin")); var ft = FighterArt.FaceTex(raider); fm.SetTexture("_BaseMap", ft); fm.SetColor("_BaseColor", Color.white); fm.SetColor("_EmissionColor", new Color(0.09f, 0.06f, 0.045f)); mr.sharedMaterial = fm; } else mr.sharedMaterial = Resources.Load<Material>("Mats/" + kv.Key); if (kv.Key == "Turban" || kv.Key == "F_Hair" || kv.Key == "F_Beard") { mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; } if (kv.Key == "Turban" || kv.Key == "F_Hair" || kv.Key == "F_Beard" || kv.Key == "F_Face" || kv.Key == "F_Eye" || kv.Key == "F_EyeW") mr.receiveShadows = false; /* head and shells: no shadow acne at duel distance */ }
      g.transform.localPosition = lp; g.transform.localRotation = Quaternion.Euler(0, ry, 0); g.transform.localScale = Vector3.one * 1.05f; return g; }
    Fighter(false, new Vector3(-0.1f, 1.3f, -1.85f), -25); Fighter(true, new Vector3(0.05f, 1.3f, -0.5f), 200); /* per duel_fighters_turnaround: lean Hasan, indigo turban with tail; bulkier raider, crimson sash */
    var rim = new GameObject("MoonRim").AddComponent<Light>(); rim.type = LightType.Spot; rim.color = new Color(0.6f, 0.72f, 1f); rim.intensity = 1.6f; rim.range = 9; rim.spotAngle = 50; rim.transform.SetParent(deck, false); rim.transform.localPosition = new Vector3(-3.5f, 4.5f, -1.4f); rim.transform.LookAt(ship.TransformPoint(new Vector3(0, 2.2f, -1.4f))); /* cool moon key on both faces; no orange glow */
    var fp = new GameObject("SailFire"); fp.transform.SetParent(ship, false); fp.transform.localPosition = Vector3.zero; fp.transform.localScale = Vector3.one; /* emit in ship space, along the sail foot */
    fp.AddComponent<MeshFilter>().sharedMesh = new Mesh(); fp.AddComponent<MeshRenderer>().sharedMaterial = Resources.Load<Material>("Mats/Fire"); StartCoroutine(FireSim2(fp.transform)); /* own lightweight particle sim: avoids pulling the ParticleSystem module (+590 KB libunity) */
    float fov0 = cam.fieldOfView; cam.fieldOfView = DuelFov; cam.transform.position = ship.TransformPoint(DuelCamPos); cam.transform.LookAt(ship.TransformPoint(DuelCamLook)); cam.transform.rotation = Quaternion.AngleAxis(-DuelTilt, cam.transform.right) * cam.transform.rotation; SunPatch(fp.transform);
    { var rimF = new GameObject("FireRim").AddComponent<Light>(); rimF.type = LightType.Point; rimF.color = new Color(1f, 0.541f, 0.239f); /* #FF8A3D */ rimF.range = 4.5f; rimF.intensity = 1.6f; rimF.shadows = LightShadows.None; rimF.transform.SetParent(fp.transform, false); rimF.transform.localPosition = new Vector3(1.6f, 3.7f, -2.3f); /* v0.6g7: high right, behind the fighters by the fire: warm rim on kurta, turban, raider's shoulder */ }
    StartCoroutine(MeasureFighters()); /* v0.6g5: beam view swung aft and widened: both fighters above the strike panel, the sail's leech burning above the stern cabin */ /* close on the two fighters, sail foot burning above */
    var shipR = ship.GetComponentsInChildren<MeshRenderer>().Where(r => !r.transform.IsChildOf(deck) && r.gameObject != fp).ToList(); var nightMpb = new MaterialPropertyBlock(); nightMpb.SetColor("_BaseColor", new Color(0.32f, 0.34f, 0.48f)); nightMpb.SetColor("_Color", new Color(0.32f, 0.34f, 0.48f)); var sailMpb = new MaterialPropertyBlock(); sailMpb.SetColor("_BaseColor", new Color(0.66f, 0.54f, 0.42f)); sailMpb.SetColor("_Color", new Color(0.66f, 0.54f, 0.42f)); foreach (var r in shipR) r.SetPropertyBlock(r.sharedMaterials.Any(m => m && m.name.Contains("Sail")) ? sailMpb : nightMpb); /* v0.6g5: sail cloth stays warm and readable under the night tint */ /* night on the cloth: sail no longer glows grey-white */
    var bg0 = cam.backgroundColor; var fogc0 = RenderSettings.fogColor; cam.backgroundColor = new Color(0.02f, 0.035f, 0.1f); RenderSettings.fogColor = new Color(0.03f, 0.05f, 0.13f); /* real night: navy sky, no grey haze */ /* down on the deck: Hasan, raider and the burning sail in frame */ if (town3D) town3D.gameObject.SetActive(false); sea3D.gameObject.SetActive(true); backdrop.color = Color.clear;
    if (pirateShip == null) pirateShip = Dhow.Build(sea3D, T, true, "PirateDhow"); pirateShip.gameObject.SetActive(true); pirateShip.position = ship.position + new Vector3(6, 0, 4); pirateShip.rotation = Quaternion.Euler(0, 200, 0); fire_pos = pirateShip.position + new Vector3(-1.5f, 5f, 0);

    if (duelTest) { yield return DuelTestShots(); yield break; }
    core = new CombatCore(S.combat, Rnd.Range(0, int.MaxValue), false, 0, difficulty);
    yield return AskCard("The burning dhow", "Night off Gwadar. Your sails are on fire and masked raiders are over the rail.\n\nRAIDER: \"Hasan Tazere. They said you'd burn slower.\"\nHASAN: \"They said wrong. Get off my deck.\"", ("Draw your blade", null));
    yield return PrologueDuel(); duelWon = true; duelsFought++;
    yield return AskCard("Keep your ship", "RAIDER: \"Keep your ship. You won't keep your luck.\"", ("Go after him", null));
    yield return PrologueChoice();
    yield return AskCard("A scorched seal", "On the deck lies a SCORCHED SEAL: a falcon in flames.\nHASAN: \"A falcon in flames... House Qarin?\"", ("Limp into Gwadar", null));
    foreach (var r in shipR) if (r) r.SetPropertyBlock(null); cam.fieldOfView = fov0; cam.backgroundColor = bg0; RenderSettings.fogColor = fogc0; Destroy(deck.gameObject); Destroy(fp); Destroy(night.gameObject); Destroy(moon.gameObject); Destroy(fire.gameObject); foreach (var l in lights) if (l) l.enabled = true; RenderSettings.ambientLight = amb0;
    pirateShip.gameObject.SetActive(false); fighting = false; { int gw = Array.FindIndex(S.ports, pp => pp.name == "Gwadar"); if (gw >= 0 && sea3D) { sea3D.gameObject.SetActive(true); backdrop.color = Color.clear; Clear(content); TopBar(); ShowSeaLandmark(gw); Debug.Log("GB ARRIVAL landmark Gwadar (intro)"); yield return ArrivalHold("Gwadar", week); ShowSeaLandmark(-1); sea3D.gameObject.SetActive(false); } } /* gamedev_t7: intro limps into Gwadar through the same arrival hold */ tab = -1; sails = OpenFlag("voyageCountExcludesDuel") ? 0 : Math.Max(sails, 1); /* limping into Gwadar is voyage 1: Gwadar->Debal is 2, Debal->Muscat is 3 (fog) */ msg = "Dawn at Gwadar. Hasan's dhow creaks against the pier. Tap the bazaar."; lastInput = Time.unscaledTime; Refresh(); }
  /// idle 45 s in town during the opening: the goal chip pulses and repeats the current goal
  public void Nudge() { nudges++; msg = "Next: " + OpeningMediumGoalAt(storyStep); /* gamedev_t10_next: medium goal */ nudgeT = Time.unscaledTime; Refresh(); }
  public float nudgeT = -99; Vector3 fire_pos;
  IEnumerator FireSim(Transform tr) {
    const int N = 160; var pos = new Vector3[N]; var vel = new Vector3[N]; var age = new float[N]; var life = new float[N]; var size = new float[N];
    var mf = tr.GetComponent<MeshFilter>(); var m = mf.sharedMesh; m.MarkDynamic(); var v = new Vector3[N * 4]; var c = new Color[N * 4]; var uv = new Vector2[N * 4]; var t = new int[N * 6];
    for (int i = 0; i < N; i++) { age[i] = Rnd.value * 1.5f; life[i] = 0; uv[i * 4] = new Vector2(0, 0); uv[i * 4 + 1] = new Vector2(1, 0); uv[i * 4 + 2] = new Vector2(1, 1); uv[i * 4 + 3] = new Vector2(0, 1); int b = i * 4; t[i * 6] = b; t[i * 6 + 1] = b + 2; t[i * 6 + 2] = b + 1; t[i * 6 + 3] = b; t[i * 6 + 4] = b + 3; t[i * 6 + 5] = b + 2; }
    m.vertices = v; m.uv = uv; m.colors = c; m.triangles = t; m.bounds = new Bounds(Vector3.zero, Vector3.one * 30);
    var hot = new Color(1f, 0.78f, 0.3f); var mid = new Color(1f, 0.36f, 0.04f); var smoke = new Color(0.55f, 0.07f, 0.0f); /* white-yellow core -> orange -> red, additive */
    while (tr) { float dt = Time.deltaTime; var cam = Camera.main ? Camera.main.transform : null; Vector3 r = cam ? tr.InverseTransformDirection(cam.right) : Vector3.right, u = cam ? tr.InverseTransformDirection(cam.up) : Vector3.up;
      for (int i = 0; i < N; i++) { age[i] += dt; if (age[i] >= life[i]) { age[i] = 0; life[i] = Rnd.Range(0.7f, 2.0f); float e = 0.2f + Rnd.value * 0.8f; bool spark = i % 12 == 0; /* the far half of the sail foot, not in the lens */ var A = new Vector3(0.15f, 2.45f, 6.5f); var C = new Vector3(0.4f, 2.35f, -3.4f); pos[i] = Vector3.Lerp(A, C, e) + new Vector3(0, Rnd.Range(0f, 0.9f), 0); /* flame tongues licking up from the sail foot */ if (!spark) { float k = Rnd.Range(0.35f, 0.75f); pos[i] = Vector3.Lerp(new Vector3(0.3f, 2.6f, 8.0f), new Vector3(0.3f, 12.5f, -4.6f), k) + new Vector3(-0.15f, Rnd.Range(-0.3f, 0.2f), 0); } /* flame strip on the yard edge */
          vel[i] = spark ? new Vector3(0, Rnd.Range(1.2f, 2.6f), Rnd.Range(-0.9f, -0.2f)) : new Vector3(0, Rnd.Range(0.5f, 1.1f), Rnd.Range(-0.15f, 0.15f)); size[i] = spark ? Rnd.Range(0.05f, 0.1f) : Rnd.Range(0.25f, 0.5f); life[i] = spark ? Rnd.Range(1.2f, 2.0f) : Rnd.Range(0.35f, 0.8f); } /* tongues climb the sail toward the peak */
        pos[i] += vel[i] * dt; float a = age[i] / life[i]; Color col = a < 0.5f ? Color.Lerp(hot, mid, a * 2) : Color.Lerp(mid, smoke, (a - 0.5f) * 2); col.a = (1 - a) * 0.75f; float sz = size[i] * (1 - a * 0.6f);
        int b = i * 4; v[b] = pos[i] - r * sz - u * sz; v[b + 1] = pos[i] + r * sz - u * sz; v[b + 2] = pos[i] + r * sz + u * sz; v[b + 3] = pos[i] - r * sz + u * sz; c[b] = c[b + 1] = c[b + 2] = c[b + 3] = col; }
      m.vertices = v; m.colors = c; yield return null; } }
  IEnumerator Flicker(Light l) { while (l) { l.intensity = 1.1f + Mathf.PerlinNoise(Time.time * 7, 0.3f) * 0.8f; yield return null; } }
  /// prologue duel: exactly three STRIKE taps, unlosable. 1: the raider blocks, 2: an opening, 3: the finishing blow in slow motion.
  IEnumerator PrologueDuel() {
    var pnl = Px(root, "Duel", 30, 1480, 1020, 680); /* art_t7_fixes: raised 120 px - the t6 card ran ~30 px off the bottom of the screen */ Box(pnl, T.panel, T.gold);
    var dTitle = Label(pnl, "Blade to blade on a burning deck", 130, 40, 640, /* art_t11_fixes: off the top rail, between the corner brackets (AskCore geometry) */ /* art_t7_fixes: x 100–900, clear of the top-right bracket */ /* art_t4_fixes: clear of the left bracket arm */ 52, T.fontSizeTitle - 4, T.parchment, TextAnchor.MiddleLeft, true); Box(Px(pnl, "rule", 130, 100, 640, 2), T.brass); /* art_t11_fixes: rule under title */
    dTitle.verticalOverflow = VerticalWrapMode.Truncate; dTitle.resizeTextForBestFit = true; dTitle.resizeTextMinSize = 20; dTitle.resizeTextMaxSize = T.fontSizeTitle - 4; /* art_t8_duel: always one line at w800, never wraps into the line below */
    var score = Label(pnl, "Tap STRIKE as the ring closes on the gold band.", 130, 108, 640, /* art_t11_fixes: below the rule, y 108-146 */ /* art_t8_duel: y 80, w 800 */ /* art_t7_fixes */ /* art_t4_fixes */ 38, T.fontSize, T.text);
    score.verticalOverflow = VerticalWrapMode.Truncate; score.resizeTextForBestFit = true; score.resizeTextMinSize = 18; score.resizeTextMaxSize = T.fontSize; /* art_t8_duel: y 80–140, clear of title (12–72) and ring (150) */
    var arena = Px(pnl, "arena", 110, 150, 380, 380); /* art_t7_fixes: ring x 110–490, y 150–530 (150 px above the card bottom) */
    var bg = Px(arena, "bg", 0, 0, 380, 380) /* art_t7_fixes */.gameObject.AddComponent<Image>(); bg.sprite = Ring(0, 1); bg.color = UITheme.C(T.ink);
    var goldR = Px(arena, "gold", 0, 0, 380, 380) /* art_t7_fixes */.gameObject.AddComponent<Image>(); goldR.sprite = Ring(0.7f, 1); goldR.color = UITheme.C(T.gold) * new Color(1, 1, 1, 0.85f); goldR.rectTransform.pivot = new Vector2(.5f, .5f); goldR.rectTransform.anchoredPosition = new Vector2(190, -190); /* art_t8_duel: scale about the centre so the band sits concentric in the disc */ goldR.rectTransform.localScale = Vector3.one * 0.6f;
    var closing = Px(arena, "closing", 0, 0, 380, 380) /* art_t7_fixes */.gameObject.AddComponent<Image>(); closing.sprite = Ring(0.95f, 1); closing.color = UITheme.C(T.parchment); closing.rectTransform.pivot = new Vector2(.5f, .5f); closing.rectTransform.anchoredPosition = new Vector2(190, -190); /* art_t8_duel: closing ring shrinks to the disc centre, not its top-left */
    var res = Label(pnl, "", 500, 170, 440, 116, /* art_t13_block: x500-940 y170-286, 14 px above STRIKE (300); two lines max */ /* art_t7_fixes */ T.fontSizeTitle - 4, T.parchment, TextAnchor.MiddleCenter, true); res.verticalOverflow = VerticalWrapMode.Truncate; res.resizeTextForBestFit = true; res.resizeTextMinSize = 22; res.resizeTextMaxSize = T.fontSizeTitle - 4; /* art_t13_block */
    bool tapped = false; var strikeBtn = Btn(pnl, "STRIKE", 520, 300, 380, 190, /* art_t7_fixes: x 520–900, bottom 190 px above the card edge */ () => tapped = true, true, true);
    string[] beats = { "A clean hit! The raider staggers!", "An opening!", "The finishing blow!" }; /* gamedev_t11_prologue: hit beats in order */
    int hits = 0, missStreak = 0, tapN = 0; score.text = "Hits 0 of 3"; /* gamedev_t11_prologue: a block no longer uses up a strike; ends after 3 band hits */
    while (hits < 3) { tapped = false; float sc = 1f; tapN++;
      for (float t = 0; !tapped; t += Time.unscaledDeltaTime / Mathf.Max(0.5f, S.combat.ringSeconds)) { sc = Mathf.Max(0.05f, 1 - Mathf.Repeat(t, 1f)); closing.rectTransform.localScale = Vector3.one * sc; yield return null; }
      bool onBand = sc >= 0.42f && sc <= 0.60f; bool forced = !onBand && missStreak >= 3; /* gamedev_t11_prologue: gold band radii 0.42-0.60; anti-stuck after 3 misses in a row */
      string beat; if (onBand || forced) { beat = beats[hits]; hits++; missStreak = 0; } else { missStreak++; beat = (sc > 0.60f ? "Too early!" : "Too late!") + "\nWatch the gold band!"; /* gamedev_t12_blockline: shorter, clear of STRIKE */ }
      Debug.Log($"GB DUEL prologue tap={tapN} scale={sc:0.000} onBand={onBand} hits={hits} missStreak={missStreak} forced={forced}"); /* gamedev_t11_prologue */
      res.text = beat; score.text = $"Hits {hits} of 3"; if (playthrough) PLog2("duel strike", beat.Replace("\n", " "));
      if (hits == 3) { Destroy0(strikeBtn.gameObject); Destroy0(arena.gameObject); /* no 4th tap during the slow-motion finish */ float ts = Time.timeScale; Time.timeScale = 0.2f; var p0 = cam.transform.position; var target = ship.TransformPoint(new Vector3(0, 1.2f, -1.4f));
        for (float u = 0; u < 1.4f; u += Time.unscaledDeltaTime) { cam.transform.position = Vector3.Lerp(p0, target + (p0 - target).normalized * 2.6f, Mathf.SmoothStep(0, 1, u / 1.4f)); cam.transform.LookAt(target); yield return null; }
        Time.timeScale = ts; }
      else yield return new WaitForSecondsRealtime(0.7f); }
    Destroy0(pnl.gameObject); }
  IEnumerator Embers(Func<bool> on, Light fire) {
    fire.transform.position = fire_pos; var list = new List<(RectTransform r, Vector2 v, float life)>();
    while (on()) { if (fire) fire.intensity = 3.4f + Mathf.PerlinNoise(Time.time * 6, 0) * 1.6f;
      if (list.Count < 40) { var r = Px(root, "ember", UnityEngine.Random.Range(600f, 1060f), UnityEngine.Random.Range(1300f, 2100f), 10, 10); var im = r.gameObject.AddComponent<Image>(); im.sprite = Dot(); im.color = new Color(1f, UnityEngine.Random.Range(0.45f, 0.75f), 0.15f, 0.9f); im.raycastTarget = false; r.SetAsFirstSibling(); list.Add((r, new Vector2(UnityEngine.Random.Range(-40f, 20f), UnityEngine.Random.Range(60f, 160f)), UnityEngine.Random.Range(1.5f, 3.5f))); }
      for (int i = list.Count - 1; i >= 0; i--) { var e = list[i]; e.life -= Time.deltaTime; e.r.anchoredPosition += new Vector2(e.v.x, e.v.y) * Time.deltaTime; if (e.life <= 0) { Destroy(e.r.gameObject); list.RemoveAt(i); } else list[i] = e; }
      yield return null; }
    foreach (var e in list) if (e.r) Destroy(e.r.gameObject); }
}
