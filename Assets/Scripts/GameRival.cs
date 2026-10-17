using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using ED = EndgameData;
using Rnd = UnityEngine.Random;

/* gamedev_v07: Captain Rashid of the Red Dhow (light rival, never fights) + grand contracts (endgame sequence of 3).
   Tunables and strings: Resources/rival_data.json and endgame_data.json "endgameContracts". State lives in EgState (saved with the game). */
[Serializable] public class RivalState { public int tauntIdx; public int at = -1, next = -1, arriveWeek = -1, metWeek = -100, metPort = -1, boughtGood = -1, boughtPort = -1, boughtWeek = -1, sightings, raceWins, raceLosses; }

public static class RivalData {
  static Dictionary<string, object> root; static Dictionary<string, object> R { get { if (root == null) { var ta = Resources.Load<TextAsset>("rival_data"); root = ta ? Design.D(MiniJson.Parse(ta.text)) : new Dictionary<string, object>(); Debug.Log("GB RIVAL data " + (ta ? "rival_data.json version=" + (root.TryGetValue("version", out var v) ? v : "?") : "MISSING, fallbacks")); } return root; } }
  static Dictionary<string, object> Sec(string k) => Design.D(R.TryGetValue(k, out var o) ? o : null);
  public static float F(string k, float def) => Sec("tuning").TryGetValue(k, out var o) ? Design.F(o, def) : def;
  public static bool B(string k, bool def) => Sec("tuning").TryGetValue(k, out var o) && o is bool b ? b : def;
  public static string S(string k, string def = "") => Sec("strings").TryGetValue(k, out var o) && o is string s ? s : def;
  public static string Art(string k, string def) => Sec("art").TryGetValue(k, out var o) && o is string s ? s : def;
  public static float ArtF(string k, float def) => Sec("art").TryGetValue(k, out var o) ? Design.F(o, def) : def;
  public static readonly string[] Moods = { "neutral", "delighted", "offended", "suspicious" };
  /* Game Designer's portrait mood per line (lineMoods in the data) */
  public static int Mood(string line) { var m = Sec("lineMoods"); string s = m.TryGetValue(line, out var o) && o is string x ? x : "neutral"; return Mathf.Max(0, Array.IndexOf(Moods, s)); }
  public static readonly string[] LineKeys = { "inPort", "taunt1", "taunt2", "taunt3", "raceWin", "raceLoss", "rumour", "ticker" };
  public static int FromMoon => Mathf.RoundToInt(F("fromMoon", 3));
  public static float PortChance => F("portChance", 0.25f);
  public static string Name => S("name", "Captain Rashid");
  public static string Line(string k) => S(k, k);
  // grand contracts (endgame_data.json "endgameContracts")
}

public partial class Game {
  RivalState Rv { get { if (eg.rival == null) eg.rival = new RivalState(); return eg.rival; } }
  bool RivalOn => Moon >= RivalData.FromMoon - 1; /* moon 3 = Moon index 2 */
  bool RivalHere => Rv.metPort == port && Rv.metWeek == week;
  bool rdForceNext, rdForceRace, rdForceGrand, rdBoardForce, rdNoRoll; /* debug flags (+ qa: rdNoRoll = no 1-in-4 roll) */
  readonly Queue<(string t, string b, int mood)> rvCards = new Queue<(string, string, int)>();
  int Wk => Mathf.Max(1, S.weeksPerMoon);

  static bool FlagOn(string arg, string env) { var a = Environment.GetCommandLineArgs(); if (Array.IndexOf(a, arg) >= 0) return true; var e = Environment.GetEnvironmentVariable(env); return !string.IsNullOrEmpty(e) && e != "0" && e.ToLowerInvariant() != "false"; }
  void RivalReadFlags() {
    rdForceNext = FlagOn("-gbredDhowNext", "GB_REDDHOW_NEXT") || FlagOn("-gbreddhownext", "GB_REDDHOW_NEXT");
    rdForceRace = FlagOn("-gbracecontract", "GB_RACE_CONTRACT"); rdForceGrand = FlagOn("-gbendgamecontract", "GB_ENDGAME_CONTRACT"); rdBoardForce = rdForceRace || rdForceGrand;
    if (rdForceNext) Debug.Log("GB RIVAL force: the Red Dhow will be in the next port you dock at");
    if (rdForceRace) Debug.Log("GB RIVAL force: the board posts a race contract (Rashid is chasing it) at the next board port you are in");
    if (rdForceGrand) Debug.Log("GB GRAND force: jump to the first grand contract (Merchant rank, sequence started)");
  }

  // ---------------- portraits ----------------
  Texture2D RivalPortrait(int mood) { var t = Resources.Load<Texture2D>(RivalData.Art("portraitPrefix", "Keepers/k7_") + Mathf.Clamp(mood, 0, 3)); if (!t) t = Resources.Load<Texture2D>(RivalData.Art("portraitPrefix", "Keepers/k7_") + "0"); return t; } /* graceful: null = no portrait drawn */
  RawImage RivalFace(Transform p, float x, float y, float s, int mood, string line) {
    var fr = Px(p, "RivalFace " + line, x, y, s, s); Box(fr, T.ink, T.gold); var t = RivalPortrait(mood); if (!t) return null;
    var im = Px(fr, "img", 4, 4, s - 8, s - 8).gameObject.AddComponent<RawImage>(); im.texture = t; im.raycastTarget = false; im.gameObject.name = "RivalPortrait " + line + " k7_" + mood; return im; }

  // ---------------- arrival ----------------
  int RivalCheapestGood(int p) { var sg = StoryGoods(); int best = -1; float bp = float.MaxValue;
    for (int g = 0; g < S.goods.Length; g++) { if (S.goods[g].contraband || sg.Contains(g) || (stock != null && stock[p, g] <= 0)) continue; if (price[p, g] < bp) { bp = price[p, g]; best = g; } } return best; }
  void RivalPickNext(int from) { var opts = ED.BoardPorts.Select(PortIdx).Where(i => i >= 0 && i != from).ToList(); if (opts.Count == 0) return; Rv.next = opts[Rnd.Range(0, opts.Count)]; Rv.arriveWeek = week + Mathf.Max(1, Mathf.RoundToInt(RivalData.F("raceRouteWeeks", 1))); Debug.Log($"GB RIVAL heads for {S.ports[Rv.next].name} (there from week {Rv.arriveWeek})"); }
  public bool RivalRollArrival() => Rnd.value < RivalData.PortChance;
  void RivalOnDock() { /* from both arrival paths, after EgOnDock */
    bool forced = rdForceNext; rdForceNext = false;
    if (!forced && (!RivalOn || !IsBoardPort(port))) return;
    if (Rv.next < 0) RivalPickNext(-1);
    bool rumoured = Rv.next == port && week >= Rv.arriveWeek, roll = !rdNoRoll && RivalRollArrival(); bool inPort = forced || rumoured || roll;
    Debug.Log($"GB RIVAL arrival {S.ports[port].name} week {week}: forced={forced} rumoured={rumoured} roll={roll} -> {(inPort ? "IN PORT" : "not here")}");
    if (!inPort) return;
    var r = Rv; r.at = port; r.metPort = port; r.metWeek = week; r.sightings++;
    int g = RivalCheapestGood(port); if (g >= 0) { int s0 = stock != null ? stock[port, g] : 0; if (stock != null) stock[port, g] = Mathf.FloorToInt(stock[port, g] * RivalData.F("cheapestStockMult", 0.5f)); float p0 = price[port, g]; price[port, g] *= RivalData.F("cheapestPriceMult", 1.2f); r.boughtGood = g; r.boughtPort = port; r.boughtWeek = week;
      Debug.Log($"GB RIVAL bought the cheapest good at {S.ports[port].name}: {S.goods[g].id} stock {s0}->{(stock != null ? stock[port, g] : 0)} price {p0:0.0}->{price[port, g]:0.0}"); }
    var race = eg.active.FirstOrDefault(c => c.race); if (race != null) { r.next = PortIdx(race.to); r.arriveWeek = race.rivalWeek; } else RivalPickNext(port);
    string tk = RivalData.Line("ticker"); /* the ticker line is for this arrival only: news keeps a dated history line (the dock message quotes news[0], so a copy of the ticker there repeated it on the next arrival) */
    news.Insert(0, RivalData.S("newsSeen", "The Red Dhow called at {port} ({date}).").Replace("{port}", S.ports[port].name).Replace("{date}", DateStr) + (g >= 0 ? " " + RivalData.S("tickerBought", "").Replace("{good}", S.goods[g].name.ToLower()) : ""));
    var m0 = (msg ?? "").Replace(tk, "").Replace("  ", " ").TrimEnd(); msg = m0 + (m0.Length > 0 ? " " : "") + tk; /* exactly once */
    string tl = RivalNextTaunt(); rvCards.Enqueue((RivalData.Name, RivalData.Line(tl), RivalData.Mood(tl))); Debug.Log("GB RIVAL meet card " + tl);
  }
  void RivalNewWeek() { /* from EgNewWeek, after week++ */
    var r = Rv;
    if (r.boughtGood >= 0 && week > r.boughtWeek) { price[r.boughtPort, r.boughtGood] /= RivalData.F("cheapestPriceMult", 1.2f); Debug.Log($"GB RIVAL visit over: {S.goods[r.boughtGood].id} price at {S.ports[r.boughtPort].name} back to {price[r.boughtPort, r.boughtGood]:0.0}"); r.boughtGood = -1; }
    bool tieP = RivalData.B("raceTieGoesToPlayer", true);
    foreach (var c in eg.active.Where(c => c.race).ToList()) if (tieP ? week > c.rivalAt : week >= c.rivalAt) RivalTookRace(c); /* he lands at a fractional week; you dock on whole weeks */
    if (RivalOn && r.next >= 0 && week > r.arriveWeek + Mathf.RoundToInt(RivalData.F("staysWeeks", 2))) { r.at = r.next; Debug.Log($"GB RIVAL moved on from {S.ports[r.at].name}"); RivalPickNext(r.at); }
    else if (RivalOn && r.next < 0) RivalPickNext(-1);
  }
  void RivalTookRace(EgContract c) {
    c.status = "taken"; eg.active.Remove(c); Rv.raceWins++; if (RivalData.B("raceLossDepositBack", true)) coins += c.deposit; int rl = Mathf.RoundToInt(RivalData.F("raceLossRenown", 0));
    Debug.Log($"GB RIVAL race lost by the player: {c.id} {c.qty}x{S.goods[c.good].id} to {c.to}, Rashid arrived week {c.rivalAt:0.00} (now {week}), deposit {(RivalData.B("raceLossDepositBack", true) ? "returned" : "lost")} {c.deposit}");
    rvCards.Enqueue((RivalData.S("raceLostTitle", "The Red Dhow got there first"), RivalData.S("raceLostBody", "").Replace("{good}", GoodName(c.good, c.qty)).Replace("{port}", c.to) + "\n\n“" + RivalData.Line("raceWin") + "”", RivalData.Mood("raceWin")));
    if (rl != 0) AddRenown(rl, "race lost to Rashid"); }
  void RivalRaceWonByPlayer(EgContract c) { Rv.raceLosses++; Debug.Log($"GB RIVAL race won by the player: {c.id} delivered week {week}, Rashid due week {c.rivalAt:0.00}");
    rvCards.Enqueue((RivalData.S("raceWonTitle", "You beat the Red Dhow"), "“" + RivalData.Line("raceLoss") + "”", RivalData.Mood("raceLoss"))); }
  /* Jeff (final): Rashid's time = the player's SHORTEST FULL TRIP from accept + uniform(raceSpreadMinWeeks, raceSpreadMaxWeeks) = trip + U(-1, +2) weeks.
     Trip: carrying enough (or it is on sale where you accept) = the delivery leg; otherwise the leg to the nearest source port + the delivery leg. Every leg is one week.
     He wins only if strictly earlier than your whole-week docking (tie to you): ~2 in 3 for a player who sails the shortest trip. */
  public int RaceSourcePort(EgContract c) { int dp = PortIdx(c.to); if (stock != null && port != dp && stock[port, c.good] >= c.qty && !Banned(c.good, port)) return port;
    return Enumerable.Range(0, S.ports.Length).Where(p => p != dp && p != port && (stock == null || stock[p, c.good] > 0)).Select(p => (int?)p).FirstOrDefault() ?? -1; }
  public int RaceTripWeeks(EgContract c) { int leg = Mathf.Max(1, Mathf.RoundToInt(RivalData.F("raceRouteWeeks", 1))); if (hold[c.good] >= c.qty) return leg; int src = RaceSourcePort(c); return src == port || src < 0 ? leg : leg + leg; }
  public float RivalRaceTime(int tripWeeks) => Mathf.Max(RivalData.F("raceMinWeeks", 0), tripWeeks + Rnd.Range(RivalData.F("raceSpreadMinWeeks", -1f), RivalData.F("raceSpreadMaxWeeks", 2f)));
  /* Game Designer: taunts cycle taunt1 -> taunt2 -> taunt3 -> taunt1 (meetings in port and taking a race share the cycle), saved in RivalState.tauntIdx */
  public string RivalNextTaunt() { string k = "taunt" + (Rv.tauntIdx % 3 + 1); Rv.tauntIdx++; return k; }
  void RivalOnAccept(EgContract c) { if (!c.race) return; int trip = RaceTripWeeks(c); c.rivalAt = week + RivalRaceTime(trip); c.rivalWeek = Mathf.CeilToInt(c.rivalAt); Rv.next = PortIdx(c.to); Rv.arriveWeek = c.rivalWeek; Debug.Log($"GB RIVAL race on: {c.id} to {c.to}, your shortest trip {trip} week(s), Rashid arrives week {c.rivalAt:0.00} (accepted week {week})");
    string tl = RivalNextTaunt(); rvCards.Enqueue((RivalData.Name, "“" + RivalData.Line(tl) + "”\n\n" + RivalData.S("raceTag", ""), RivalData.Mood(tl))); }
  void RivalMarkRace(bool forced) { /* from EgRefreshBoards: occasionally one notice is a race */
    if (eg.active.Any(c => c.race) || eg.board.Any(b => b.race)) { if (forced) Debug.Log("GB RIVAL race contract already up"); return; }
    if (!forced && (!RivalOn || Rnd.value >= RivalData.F("raceChance", 0.34f))) return;
    var here = IsBoardPort(port) ? eg.board.Where(b => b.from == S.ports[port].name).ToList() : new List<EgContract>(); var pick = (forced && here.Count > 0 ? here : eg.board).OrderBy(_ => Rnd.value).FirstOrDefault(); if (pick == null) return;
    pick.race = true; Debug.Log($"GB RIVAL race contract posted{(forced ? " (forced)" : "")}: {pick.id} {pick.title} {pick.from}->{pick.to}"); }

  // ---------------- grand contracts ----------------
  static Dictionary<string, object> GrandSec() { var ta = Resources.Load<TextAsset>("endgame_data"); return ta ? Design.D(Design.D(MiniJson.Parse(ta.text)).TryGetValue("endgameContracts", out var o) ? o : null) : new Dictionary<string, object>(); }
  static Dictionary<string, object> grandSec; static Dictionary<string, object> GS => grandSec ??= GrandSec();
  static int GI(string k, int def) => GS.TryGetValue(k, out var o) && o is double x ? (int)x : def;
  static string GStr(string k, string def) => Design.D(GS.TryGetValue("strings", out var o) ? o : null).TryGetValue(k, out var v) && v is string s ? s : def;
  static List<Dictionary<string, object>> GrandSeq => (GS.TryGetValue("sequence", out var o) && o is List<object> l ? l : new List<object>()).Select(Design.D).ToList();
  public int GrandMoons => GI("moons", 2); public int GrandBonus => GI("bonusCoins", 1000); public int GrandCap => GI("cap", 6000); public int GrandRenown => ED.RenownDelivery * GI("renownMult", 3); public int GrandFailRenown => GI("failRenown", -30);
  public EgContract Big => eg.big != null && !string.IsNullOrEmpty(eg.big.id) ? eg.big : null; /* JsonUtility never stores null: an empty id = no grand contract */
  EgContract GrandOffer => Big != null && Big.status == "offer" ? Big : null; EgContract GrandActive => Big != null && Big.status == "active" ? Big : null;
  IEnumerable<EgContract> EgActiveAll() { foreach (var c in eg.active) yield return c; if (GrandActive != null) yield return GrandActive; }
  /* gamedev_v07 pay fix: was qty x the LIVE price at the destination (weekly drift up to 2x base, market events, Rashid's +20%), frozen at offer time.
     Now qty x the destination's NORMAL price (the good's design mid price x that port's cheap/want multiplier: no per-save roll, drift, events or Rashid) x payPriceMult + bonusCoins, capped. */
  public float GrandNormalPrice(int good, int dest) => (S.goods[good].min + S.goods[good].max) * 0.5f * PortMult(dest, good);
  public float GrandPriceMult => GS.TryGetValue("payPriceMult", out var o) ? Design.F(o, 1f) : 1f;
  public int GrandPay(int good, int qty, int dest) => Mathf.Min(GrandCap, Mathf.RoundToInt(qty * GrandNormalPrice(good, dest) * GrandPriceMult) + GrandBonus);
  void GrandEnsure() { /* Merchant (rank 3, 300 Renown) starts the sequence; one at a time */
    if (eg.runComplete || eg.rank < 2 || Big != null) return; if (eg.bigStage < 0) { eg.bigStage = 0; Debug.Log("GB GRAND sequence starts (Merchant reached)"); } GrandPost(); }
  void GrandPost() { var seq = GrandSeq; if (eg.bigStage >= seq.Count) return; var d = seq[eg.bigStage]; string Sd(string k, string def) => d.TryGetValue(k, out var o) && o is string s ? s : def;
    int g = G(Sd("good", "dates")), dp = PortIdx(Sd("dest", "Debal")); int qty = d.TryGetValue("qty", out var q) && q is double qd ? (int)qd : 20; if (g < 0 || dp < 0) { Debug.LogWarning("GB GRAND bad sequence entry " + eg.bigStage); return; }
    var buy = d.TryGetValue("buyAt", out var ba) && ba is List<object> bl ? bl.OfType<string>().ToList() : new List<string>();
    eg.big = new EgContract { id = "grand" + (++eg.seq), tpl = Sd("id", "eg_big" + (eg.bigStage + 1)), kind = "grand", from = string.Join("/", buy), to = S.ports[dp].name, title = Sd("title", "Grand contract"), giver = Sd("giver", ""), risk = Sd("risk", ""), good = g, qty = qty, pay = GrandPay(g, qty, dp), renown = GrandRenown, deposit = 0, failRenown = GrandFailRenown, moons = GrandMoons, status = "offer" };
    Debug.Log($"GB GRAND offer {eg.bigStage + 1}/3 {eg.big.tpl}: {qty}x{S.goods[g].id} -> {eg.big.to} pay {eg.big.pay} (20 x normal price {GrandNormalPrice(g, dp):0.0} x {GrandPriceMult:0.##} + {GrandBonus}, cap {GrandCap}; live price {price[dp, g]:0.0}) renown {GrandRenown} moons {GrandMoons}"); }
  bool GrandAccept(EgContract c) { c.status = "active"; c.dueWeek = week + c.moons * Wk - 1; Debug.Log($"GB GRAND accept {c.tpl} dueWeek {c.dueWeek} (own slot; {eg.active.Count}/{ED.MaxActive} normal contracts)"); msg = c.title + ": " + EgMoonsText(c) + "."; Save(); return true; }
  void GrandOnDock() { var c = GrandActive; if (c == null || S.ports[port].name != c.to) return; var sg = StoryGoods();
    if (hold[c.good] < c.qty || sg.Contains(c.good)) { Debug.Log($"GB GRAND at {c.to} but hold {hold[c.good]}/{c.qty} {S.goods[c.good].id}: not delivered"); msg = ED.NeedCargo; return; }
    int f0 = fame; hold[c.good] -= c.qty; coins += c.pay; c.status = "done"; Debug.Log($"GB GRAND delivered {eg.bigStage + 1}/3 {c.tpl} +{c.pay} coins +{c.renown} renown (fame unchanged {fame})");
    egCards.Enqueue((c.title, ED.Delivered(c.pay, c.renown), "Continue")); AddRenown(c.renown, "grand contract"); fame = f0; eg.big = null; eg.bigStage++;
    if (eg.bigStage >= GrandSeq.Count) { eg.runComplete = true; Debug.Log("GB GRAND run complete: all 3 grand contracts delivered"); egCards.Enqueue((GStr("runComplete", "Run complete"), GStr("runCompleteBody", "The run is finished."), "Onward")); }
    else GrandPost(); }
  void GrandNewWeek() { var c = GrandActive; if (c == null || EgWeeksLeft(c) >= 0) return; c.status = "failed"; Debug.Log($"GB GRAND failed {c.tpl} at stage {eg.bigStage + 1}: {c.failRenown} renown; the same contract is posted again"); eg.bigFails++;
    egCards.Enqueue((c.title, ED.Failed(c.failRenown).Replace(", deposit lost", ""), "Continue")); eg.big = null; AddRenown(c.failRenown, "grand contract failed"); GrandPost(); }
  string GrandTag(EgContract c) => GStr("offerTag", "Grand contract · {n} of 3").Replace("{n}", (eg.bigStage + 1).ToString());

  // ---------------- per-frame: flags, cards, the Red Dhow at sea ----------------
  bool rdSeaPending; float rdSeaShowAt, rdSeaHideAt; Transform redDhow; Material redSailMat;
  void RivalUpdate() {
    if (onTitle || S == null) return;
    if (!sailing && !fighting) {
      if (rdForceGrand) { rdForceGrand = false; storyStep = Mathf.Max(storyStep, StDone); if (eg.rank < 2) AddRenown(ED.RankRenown(2) - eg.renown, "debug -gbendgamecontract"); while (egCards.Count > 0) egCards.Dequeue(); GrandEnsure(); msg = Big != null ? $"Debug: grand contract 1 on the board ({Big.title})." : "Debug: grand contract not posted"; Debug.Log("GB GRAND force applied: rank " + RankName + ", offer " + (Big != null ? Big.tpl : "none")); Save(); Refresh(); }
      if (rdForceRace && IsBoardPort(port)) { rdForceRace = false; if (!eg.board.Any(b => b.from == S.ports[port].name)) EgRefreshBoards(true); RivalMarkRace(true); Save(); }
    }
    if (redDhow != null && redDhow.gameObject.activeSelf) { RedDhowPlace(); if (!sailing || Time.time > rdSeaHideAt) { redDhow.gameObject.SetActive(false); Debug.Log("GB RIVAL sea hide"); } }
    if (rdSeaPending && sailing && Time.time >= rdSeaShowAt) { rdSeaPending = false; RedDhowShow(); }
    if (!sailing) rdSeaPending = false;
    if (egTestQuiet || rvCards.Count == 0 || sailing || fighting || dialog != null || CardBusy) return;
    var c = rvCards.Dequeue(); StartCoroutine(RivalCard(c.t, c.b, c.mood));
  }
  void RivalSeaStart(int to, float dur) { if (!RivalOn || Rv.next != to && !eg.active.Any(c => c.race && PortIdx(c.to) == to)) return; rdSeaPending = true; rdSeaShowAt = Time.time + dur * RivalData.F("seaShowFrom", 0.12f); rdSeaHideAt = Time.time + dur * RivalData.F("seaShowTo", 0.55f); Debug.Log($"GB RIVAL sea: the Red Dhow is on the {S.ports[to].name} leg"); }
  public Material RedSailMaterial() { if (redSailMat) return redSailMat;
    var lit = Shader.Find("Universal Render Pipeline/Lit"); var m = lit ? new Material(lit) : new Material(Shader.Find("Sprites/Default")); m.name = "RedDhowSail"; var tex = Resources.Load<Texture2D>(RivalData.Art("sailTexture", "Art/Ships/red_sail_canvas"));
    if (tex) { tex.wrapMode = TextureWrapMode.Repeat; var bc = UITheme.C(RivalData.Art("sailBaseColor", "#C24A38")); if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex); if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex); if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", bc); if (m.HasProperty("_Color")) m.SetColor("_Color", bc); } /* Jeff: base colour #C24A38 multiplied over the canvas */
    else { var fc = UITheme.C(RivalData.Art("sailFallbackColor", "#8E2F25")); if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", fc); if (m.HasProperty("_Color")) m.SetColor("_Color", fc); }
    if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", RivalData.ArtF("sailSmoothness", 0.1f)); if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f); if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
    m.DisableKeyword("_EMISSION"); if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack; /* art spec: NO emission (the player's cream lateen glows; this must not) */
    Debug.Log("GB RIVAL red sail material: " + (tex ? "red_sail_canvas x " + RivalData.Art("sailBaseColor", "#C24A38") : "fallback #8E2F25") + ", no emission, no rim"); return redSailMat = m; }
  Transform BuildRedDhow() {
    if (redDhow) return redDhow; if (!sea3D) return null;
    var holder = new GameObject("RedDhow").transform; holder.SetParent(sea3D, false); /* holder: the Kenney FBX keeps its own lay-flat rotation */ var k = FreeModels.SpawnKenneyDhow(holder, "RedDhowModel"); bool kenney = k != null; if (!kenney) k = Dhow.Build(holder, T, false, "RedDhowModel");
    var sm = RedSailMaterial(); int n = 0;
    foreach (var r in k.GetComponentsInChildren<Renderer>(true)) { string nm = r.gameObject.name.ToLower(); if (nm.Contains("creamlateen") || (nm.Contains("sail") && !nm.Contains("yard"))) { r.sharedMaterial = sm; r.gameObject.name = "RedLateen"; n++; } }
    Debug.Log("GB RIVAL Red Dhow parts: " + string.Join(", ", k.GetComponentsInChildren<Renderer>(true).Select(x => x.gameObject.name + (x.enabled ? "" : "(off)") + "[" + (x.sharedMaterial ? x.sharedMaterial.name : "-") + "]")));
    Debug.Log($"GB RIVAL Red Dhow built ({(kenney ? "Kenney" : "procedural")}), red sail renderers {n}"); holder.gameObject.SetActive(false); return redDhow = holder; }
  /* gamedev_v07 sea fix: he was placed once in world space (camera-relative at show time) and drifted off; Game Tester saw no Red Dhow.
     Now he is re-placed every frame on the sea plane under a point of the gameplay camera's viewport (left of the player's sail, just under the horizon),
     sliding slowly across, so he stays in the frame the player sees for the whole show window. */
  float rdShowT;
  bool RedDhowPlace() { if (!redDhow || !cam) return false; float a = Mathf.Clamp01((Time.time - rdShowT) / Mathf.Max(1f, rdSeaHideAt - rdShowT));
    var vp = new Vector3(Mathf.Lerp(RivalData.F("seaViewX0", 0.16f), RivalData.F("seaViewX1", 0.34f), a), RivalData.F("seaViewY", 0.6f), 0); var ray = cam.ViewportPointToRay(vp);
    if (ray.direction.y > -0.002f) return false; float d = Mathf.Min(RivalData.F("seaMaxDist", 70f), -ray.origin.y / ray.direction.y); var pos = ray.origin + ray.direction * d; pos.y = 0.2f; redDhow.position = pos;
    var rr = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up); if (rr.sqrMagnitude > 0.01f) redDhow.rotation = Quaternion.LookRotation(rr.normalized, Vector3.up); return true; }
  void RedDhowShow() { var d = BuildRedDhow(); if (!d) return; rdShowT = Time.time; d.gameObject.SetActive(true); RedDhowPlace(); Debug.Log($"GB RIVAL sea show: the Red Dhow on the horizon at {d.position} (gameplay camera {(cam ? cam.name : "none")}, until +{rdSeaHideAt - Time.time:0.0}s)"); }
  IEnumerator RivalCard(string title, string body, int mood) { /* Ask-style card with Rashid's portrait on the left */
    while (CardBusy) yield return null;
    float bodyH = Mathf.Max(220f, CardBodyH(body, 400f)); var tf = CardTitleFit(title, 640, T.fontSizeTitle); float panelH = 132 + tf.extra + bodyH + 20 + 84 + 110; float top = Mathf.Max(180f, (1920f - panelH) * 0.38f);
    var myDim = ModalDim("AskDim"); modalDepth++; var my = Px(root, "RivalCard", 90, top, 900, panelH); dialog = my; Box(my, T.panel, T.gold); bool done = false; var be = PushBack(title, () => my != null, () => done = true);
    CardTitle(my, title, 130, 640, T.fontSizeTitle, T.parchment, TextAnchor.MiddleLeft, tf); float y = 132 + tf.extra;
    RivalFace(my, 130, y, 220, mood, "card"); var bt = Label(my, body, 370, y, 400, bodyH, T.fontSize, T.text, TextAnchor.UpperLeft); bt.gameObject.name = "RivalLine";
    Btn(my, "Continue", 100, y + bodyH + 20, 700, 84, () => done = true, true, true); Debug.Log($"GB CARD show {title} (Rashid k7_{mood})");
    try { while (!done) yield return null; } finally { backStack.Remove(be); if (my) Destroy0(my.gameObject); if (myDim) Destroy0(myDim.gameObject); if (dialog == my) dialog = null; modalDepth = Mathf.Max(0, modalDepth - 1); }
  }

  // ---------------- tavern ----------------
  float RivalTavernLines(RectTransform lst, float y) { if (!RivalOn || Rv.next < 0 && !RivalHere) return y;
    if (RivalHere) { RivalFace(lst, 26, y, 120, RivalData.Mood("inPort"), "inPort"); var t = Label(lst, $"<b>“{RivalData.Line("inPort")}”</b>", 160, y, 846, 120, T.fontSize, T.gold, TextAnchor.MiddleLeft); t.gameObject.name = "RivalInPort"; y += 136; }
    if (Rv.next >= 0) { RivalFace(lst, 26, y, 120, RivalData.Mood("rumour"), "rumour"); var t = Label(lst, $"<b>“{RivalData.Line("rumour").Replace("{port}", S.ports[Rv.next].name)}”</b>", 160, y, 846, 120, T.fontSize, T.gold, TextAnchor.MiddleLeft); t.gameObject.name = "RivalRumour"; y += 136; }
    return y; }

  // ---------------- board UI bits ----------------
  Sprite rivalIcon; bool rivalIconTried;
  Sprite RivalIcon() { if (rivalIconTried) return rivalIcon; rivalIconTried = true; rivalIcon = Resources.Load<Sprite>(RivalData.Art("boardIcon", "Art/UI/rival_sail_icon")); if (!rivalIcon) { var t = Resources.Load<Texture2D>(RivalData.Art("boardIcon", "Art/UI/rival_sail_icon")); if (t) rivalIcon = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(.5f, .5f)); } if (!rivalIcon) Debug.LogWarning("GB RIVAL board icon missing"); return rivalIcon; }
  Image RaceIcon(Transform p, float x, float yCentre) { var s = RivalIcon(); float sz = RivalData.ArtF("boardIconSize", 30); var r = Px(p, "RaceIcon", x, yCentre - sz / 2, sz, sz); var im = r.gameObject.AddComponent<Image>(); im.preserveAspect = true; im.color = Color.white; im.raycastTarget = false; if (s) im.sprite = s; else im.color = UITheme.C("#8E2F25"); return im; }
}
