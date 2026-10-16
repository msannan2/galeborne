using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Rnd = UnityEngine.Random;

/// gamedev_voyage_events2: the 5 new voyage events and the Hail payoffs from design/voyage_events.json (be8abe3), weights per Game Designer's ruling (relative to merchant).
public partial class Game {
  public const float WPearl = 6f, WLeaky = 6f, WLighthouse = 6f, WCustoms = 6f, WSquallLine = 4f, MonsoonMult = 2.5f; /* final ruling: absolute weights beside pirate 35 / wreck 20 / merchant 20 / squall 15 / rumor 10 */ public const int MonsoonFrom = 6, MonsoonTo = 9;
  public const int HailWRumour = 50, HailWTrade = 30, HailWWarning = 20, HailTradeMaxQ = 4; public const float HailTradeCoinFrac = 0.8f;
  public const int PearlCoins = 60, FloatCost = 10, NewsCost = 15, LeakyMaxQ = 5, CustomsFeeMin = 20, CustomsFeeMax = 150, RunHullLoss = 10, HeaveHullLoss = 5; public const float CustomsFeePct = 0.05f, RunEscape = 0.55f, CutLossChance = 0.4f;
  public bool ambushHalveNext, hailWarned; public float veRoll = -1; public string veForceHail = "";
  float VeR() => veRoll >= 0 ? veRoll : Rnd.value;
  public Dictionary<string, object> VeNew(string id) => (VE.TryGetValue("new", out var n) && n is List<object> l ? l.Select(Design.D).FirstOrDefault(e => (string)e["id"] == id) : null) ?? new Dictionary<string, object>();
  Dictionary<string, object> VeHail => Design.D(VE.TryGetValue("hailPayoffs", out var h) ? h : null);
  public int MoonOfYear => Moon % S.moonsPerYear + 1; public bool Monsoon => MoonOfYear >= MonsoonFrom && MoonOfYear <= MonsoonTo;
  bool Leg(int a, int b, string x, string y) => (S.ports[a].name == x && S.ports[b].name == y) || (S.ports[a].name == y && S.ports[b].name == x);
  bool Touches(int a, int b, params string[] ns) => ns.Contains(S.ports[a].name) || ns.Contains(S.ports[b].name);
  /// encounter weights in EncIds order; existing four unchanged, new ones relative to merchant and gated by route
  public float[] EncWeights(int from, int to) { var w = S.weights; const float m = 1f;
    return new[] { w.wreck, w.merchant, w.squall, w.rumor,
      Touches(from, to, "Gwadar", "Muscat") ? WPearl * m : 0, Touches(from, to, "Gwadar", "Muscat") ? WLeaky * m : 0, Touches(from, to, "Muscat") ? WCustoms * m : 0,
      WSquallLine * m * (Monsoon ? MonsoonMult : 1), (Leg(from, to, "Gwadar", "Muscat") || Leg(from, to, "Debal", "Muscat")) ? WLighthouse * m : 0 }; }
  float VeTakeAmbushHalve() { if (!ambushHalveNext) return 1f; ambushHalveNext = false; Debug.Log("GB VEVENT lighthouse warning: this voyage's ambush chance halved"); return 0.5f; }
  void LoseWeek() => NewWeek();
  void HullHit(float d) { hullNow = Mathf.Max(1, HullCur - d); }
  void ShortageRumour(int p, int g) { price[p, g] *= S.rumorSpike; news.Insert(0, $"Rumor ({DateStr}): {S.ports[p].name} is short of {S.goods[g].name.ToLower()}."); }
  void GlutRumour(int p, int g) { var ev = ((List<object>)DZ.market["events"]).Select(Design.D).FirstOrDefault(e => (string)e["id"] == "glut"); if (ev != null) StartEvent(p, ev, g); news.Insert(0, $"Rumor ({DateStr}): {S.ports[p].name} is drowning in {S.goods[g].name.ToLower()}."); }
  string Off(string label, string why) => AskOff + label + "\n" + why;
  string NewBody(string id) { var vs = Design.D(VeNew(id)).TryGetValue("variants", out var v) && v is List<object> l ? l.Select(Design.D).ToList() : new List<Dictionary<string, object>>(); if (vs.Count == 0) return "";
    int i = veForceVariant >= 0 ? Mathf.Min(veForceVariant, vs.Count - 1) : Rnd.Range(0, vs.Count); if (veForceVariant < 0 && vs.Count > 1 && veLast.TryGetValue(id, out int li) && li == i) i = (i + 1) % vs.Count; veLast[id] = i;
    Debug.Log($"GB VEVENT {id} variant {i + 1}/{vs.Count} family {VeFamily(id)}"); return VeS(vs[i], "body", ""); }
  Dictionary<string, object> NewOpt(string id, int i) { var o = VeNew(id).TryGetValue("options", out var ol) && ol is List<object> l && i < l.Count ? Design.D(l[i]) : new Dictionary<string, object>(); return o; }
  public IEnumerator NewEvent(string id) {
    var e = VeNew(id); string title = VeS(e, "title", id), body = NewBody(id), outT = null; VeShown(id); int free = S.holdSize - HoldUsed;
    string L(int i) => VeS(NewOpt(id, i), "label", "?"); string O(int i, string k = "out") => VeS(NewOpt(id, i), k, "");
    if (id == "pearlDiver") { int pg = G("pearls"), db = Debal;
      yield return Ask(title, body,
        (L(0), () => { LoseWeek(); if (free > 0 && pg >= 0) hold[pg] += 1; else coins += PearlCoins; outT = O(0) + (free > 0 ? "" : $" No room for it: he pays {PearlCoins} coins instead."); }),
        (coins >= FloatCost ? L(1) : Off(L(1), "Not enough coins"), () => { coins -= FloatCost; ShortageRumour(db, pg); outT = O(1); }),
        (L(2), () => outT = O(2)));
    } else if (id == "leakyDhow") { int dg = G("dates"), gw = GwadarIdx, unit = Mathf.Max(1, Mathf.RoundToInt(price[gw, dg] * 0.5f)), room = Mathf.Min(LeakyMaxQ, free), n = Mathf.Min(room, coins / unit);
      string lab = VeFill(L(0), ("n", Mathf.Max(n, room).ToString()), ("cost", (unit * Mathf.Max(n, room)).ToString()));
      yield return Ask(title, body,
        (room <= 0 ? Off(lab, VeS(NewOpt(id, 0), "greyWhenFull", "Hold full")) : n <= 0 ? Off(lab, "Not enough coins") : lab, () => { coins -= unit * n; hold[dg] += n; outT = VeFill(O(0), ("goods", GoodCount(dg, n))); }),
        (L(1), () => { LoseWeek(); ShortageRumour(Debal, G("salt")); outT = O(1); }),
        (L(2), () => outT = O(2)));
    } else if (id == "customsLaunch") { int mu = Muscat; var banned = LosableGoods().Where(g => Banned(g, mu)).ToList(); int fee = Mathf.Clamp(Mathf.RoundToInt(coins * CustomsFeePct), CustomsFeeMin, CustomsFeeMax);
      if (banned.Count == 0) { Debug.Log("GB VEVENT customsLaunch: no banned goods, waved through"); yield return Ask(title, "Customs waved you through.", ("Continue", () => msg = "Customs waved you through.")); Refresh(); yield break; }
      yield return Ask(title, body,
        (L(0), () => { LoseWeek(); foreach (int g in banned) hold[g] = 0; outT = O(0) + " Seized: " + string.Join(", ", banned.Select(g => S.goods[g].name)) + "."; }),
        (coins >= fee ? L(1) : Off(L(1), "Not enough coins"), () => { coins -= fee; outT = VeFill(O(1), ("fee", fee.ToString())); }),
        (L(2), () => { if (VeR() < RunEscape) outT = O(2); else { int f2 = Mathf.Min(coins, fee * 2); coins -= f2; HullHit(RunHullLoss); outT = VeFill(O(2, "outFail"), ("fee", f2.ToString())); } }));
    } else if (id == "squallLine") {
      yield return Ask(title, body,
        (L(0), () => { LoseWeek(); outT = O(0); }),
        (L(1), () => { var held = LosableGoods(); if (held.Count > 0 && VeR() < CutLossChance) { int g = held[Rnd.Range(0, held.Count)], lost = Mathf.Min(hold[g], Rnd.Range(1, 3)); hold[g] -= lost; outT = VeFill(O(1, "outFail"), ("lost", GoodCount(g, lost))); } else outT = O(1); }),
        (L(2), () => { HullHit(HeaveHullLoss); outT = O(2); }));
    } else if (id == "lighthouse") { var held = LosableGoods(); int cg = held.Count > 0 ? held.OrderBy(g => price[port, g]).First() : -1;
      yield return Ask(title, body,
        (coins >= NewsCost ? L(0) : Off(L(0), "Not enough coins"), () => { coins -= NewsCost; int p = Rnd.Range(0, S.ports.Length); var want = S.ports[p].want; int g = G(want[Rnd.Range(0, want.Length)]); ShortageRumour(p, g); outT = VeFill(O(0), ("port", S.ports[p].name), ("good", S.goods[g].name.ToLower())); }),
        (cg >= 0 ? L(1) : Off(L(1), VeS(NewOpt(id, 1), "disabledWhenNoLosable", "Nothing to spare")), () => { hold[cg] -= 1; ambushHalveNext = true; outT = VeFill(O(1), ("goods", GoodCount(cg, 1))); }),
        (L(2), () => outT = O(2)));
    }
    if (!string.IsNullOrEmpty(outT)) { msg = outT; yield return Ask(title, outT, ("Continue", null)); }
    Refresh(); }
  /// Hail warning port: the next port on the route with ambush or blockade risk (Gwadar sits between Debal and Muscat), else the destination
  /// Hail warning port: the destination if its leg has ambush risk, else the next port by ambush risk from the destination; -1 = no risk anywhere (rumour instead)
  public int HailWarnPort(int from, int to) { if (RiskyRoute(from, to)) return to; for (int q = 0; q < S.ports.Length; q++) if (q != to && RiskyRoute(to, q)) return q; return -1; }
  /// Hail trade source: the port just left, unless the destination has the same cheap list, then another port
  public int HailTradePort(int from, int to) { bool Same(int a, int b) => new HashSet<string>(S.ports[a].cheap).SetEquals(S.ports[b].cheap); if (!Same(from, to)) return from;
    for (int q = 0; q < S.ports.Length; q++) if (q != from && q != to && !Same(q, to)) return q; return from; }
  public IEnumerator HailPayoff() { var h = VeHail; float r = Rnd.value * (HailWRumour + HailWTrade + HailWWarning);
    string kind = veForceHail != "" ? veForceHail : r < HailWRumour ? "rumour" : r < HailWRumour + HailWTrade ? "trade" : "warning";
    int tg = -1, unit = 0, q = 0;
    if (kind == "warning" && HailWarnPort(port, dest) < 0) { Debug.Log("GB HAIL warning: no ambush risk anywhere -> rumour"); kind = "rumour"; }
    int tp = HailTradePort(port, dest);
    if (kind == "trade") { var cheap = S.ports[tp].cheap.Select(G).Where(g => g >= 0 && !S.goods[g].contraband).ToList(); if (cheap.Count > 0) { tg = cheap.OrderBy(g => price[tp, g]).First(); unit = Mathf.Max(1, Mathf.RoundToInt(price[tp, tg])); q = Mathf.Min(HailTradeMaxQ, S.holdSize - HoldUsed, Mathf.FloorToInt(coins * HailTradeCoinFrac / unit)); }
      if (q < 1) { Debug.Log($"GB HAIL trade can't be offered (q {q}) -> rumour"); kind = "rumour"; } }
    var kd = Design.D(h.TryGetValue(kind, out var ko) ? ko : null); var vs = kd.TryGetValue("variants", out var vo) && vo is List<object> vl ? vl.Select(Design.D).ToList() : new List<Dictionary<string, object>>();
    var v = vs.Count > 0 ? vs[veForceVariant >= 0 ? Mathf.Min(veForceVariant, vs.Count - 1) : Rnd.Range(0, vs.Count)] : new Dictionary<string, object>(); string title = VeS(v, "title", "Hail");
    Debug.Log($"GB HAIL payoff {kind}"); lastHail = kind;
    if (kind == "rumour") { int p = Rnd.Range(0, S.ports.Length); bool glut = Rnd.value < 0.5f; var pool = glut ? S.ports[p].cheap : S.ports[p].want; int g = G(pool[Rnd.Range(0, pool.Length)]);
      if (glut) GlutRumour(p, g); else ShortageRumour(p, g); string b = VeFill(VeS(v, glut ? "bodyGlut" : "bodyShort", ""), ("port", S.ports[p].name), ("good", S.goods[g].name.ToLower())); msg = b;
      yield return Ask(title, b, ("Continue", null)); }
    else if (kind == "trade") { (string, string)[] f = { ("goods", GoodCount(tg, q)), ("unit", unit.ToString()), ("cost", (unit * q).ToString()) };
      yield return Ask(title, VeFill(VeS(v, "body", ""), f), (VeFill(VeOpt(v, 0, "Buy ({cost})"), f), () => { coins -= unit * q; hold[tg] += q; msg = $"Bought {GoodCount(tg, q)} at sea."; }), (VeOpt(v, 1, "Decline"), () => msg = "You wave her on.")); }
    else { hailWarned = true; int wp = HailWarnPort(port, dest); string b = VeFill(VeS(v, "body", ""), ("port", S.ports[wp].name)); msg = b; yield return Ask(title, b, ("Continue", null)); }
    Refresh(); }
  public string lastHail = "";
}
