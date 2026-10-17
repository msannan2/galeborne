using ED = EndgameData;
using System.Linq;
using UnityEngine;

/// gamedev_tip (v0.7.1 follow-up, Game Designer ruling): a merchant meeting a hold full of sealed grand-contract goods gives a free price tip
/// instead of an (unaffordable, hidden) offer; and news/dock sentences are joined with a full stop.
public partial class Game {
  public const string MerchantTipFmt = "A trader eyes your sealed hold and laughs. 'Nothing for me there. But there's a fine price for {good} in {port} this moon.'";
  public string MerchantTipText(int g, int p) => MerchantTipFmt.Replace("{good}", S.goods[g].name.ToLower()).Replace("{port}", S.ports[p].name);
  /// the good with the best sale price in any port other than the current destination (contraband excluded)
  public (int g, int p) MerchantTip() { int bg = -1, bp = -1; float best = -1;
    for (int p = 0; p < S.ports.Length; p++) { if (p == dest) continue; for (int g = 0; g < S.goods.Length; g++) { if (S.goods[g].contraband) continue; if (price[p, g] > best) { best = price[p, g]; bg = g; bp = p; } } }
    return (bg, bp); }
  /// joins sentences with one space, adding a full stop to any part that does not already end a sentence
  public static string JoinSentences(params string[] parts) {
    var ps = parts.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
    for (int i = 0; i < ps.Count - 1; i++) { char e = ps[i][ps[i].Length - 1]; if (!(".!?:…".IndexOf(e) >= 0 || ((e == '\'' || e == '"' || e == ')') && ps[i].Length > 1 && ".!?".IndexOf(ps[i][ps[i].Length - 2]) >= 0))) ps[i] += "."; }
    return string.Join(" ", ps); }
}

public partial class Game {
  /// gamedev_tip qa: sealed full hold + owed merchant -> the tip card (no trade, no cost); the normal merchant is unchanged; dock sentences keep their full stops
  System.Collections.IEnumerator V07MerchantTip(System.Action<bool, string> Chk, System.Action<string> L, string dir) {
    int hs0 = S.holdSize; Init(); egTestQuiet = true; storyStep = StDone; port = PortIdx("Gwadar"); week = 3 * Wk; tab = -1; rvCards.Clear(); egCards.Clear(); Refresh();
    int dt = G("dates"), mu = PortIdx("Muscat"); AddRenown(ED.RankRenown(2) - eg.renown, "qa tip"); egCards.Clear(); GrandEnsure(); var c = Big; GrandAccept(c);
    Chk(GrandActive != null && c.good == dt, $"tip setup: grand contract {c?.tpl} active ({c?.qty}x{(c != null ? S.goods[c.good].id : "?")})");
    string vf0 = veForce; dest = mu;
    System.Collections.Generic.List<string> Texts() => dialog ? dialog.GetComponentsInChildren<UnityEngine.UI.Text>().Select(t => t.text).ToList() : new System.Collections.Generic.List<string>();
    // (a) hold full of sealed goods, plenty of gold
    S.holdSize = c.qty; System.Array.Clear(hold, 0, hold.Length); hold[c.good] = c.qty; coins = 5000; lastFamily = ""; var h0 = (int[])hold.Clone(); int c0 = coins;
    var want = MerchantTip(); string wantText = want.g >= 0 ? MerchantTipText(want.g, want.p) : "?";
    float bestElsewhere = Enumerable.Range(0, S.ports.Length).Where(p => p != dest).SelectMany(p => Enumerable.Range(0, S.goods.Length).Where(g => !S.goods[g].contraband).Select(g => price[p, g])).Max();
    veForce = "merchant"; StartCoroutine(Encounter(false, false)); yield return null; yield return null; float tw = steadyClock; while (dialog == null && steadyClock - tw < 5f) yield return null;
    var tx = Texts(); bool hasBuy = dialog && dialog.GetComponentsInChildren<UnityEngine.UI.Button>().Any(b => b.GetComponentInChildren<UnityEngine.UI.Text>() && b.GetComponentInChildren<UnityEngine.UI.Text>().text.StartsWith("Buy"));
    yield return new WaitForSeconds(0.3f); yield return ShotCo(System.IO.Path.Combine(dir, "merchant_tip_card.png")); yield return new WaitForSecondsRealtime(0.7f); EgTestCloseCard(); yield return null; yield return null;
    Chk(tx.Contains("A merchant hails you") && tx.Contains(wantText) && !hasBuy, $"sealed full hold: the merchant hails with the tip card '{wantText}' (got '{string.Join(" | ", tx)}'), no Buy button");
    Chk(want.g >= 0 && want.p >= 0 && want.p != dest && !S.goods[want.g].contraband && Mathf.Approximately(price[want.p, want.g], bestElsewhere) && wantText == $"A trader eyes your sealed hold and laughs. 'Nothing for me there. But there's a fine price for {S.goods[want.g].name.ToLower()} in {S.ports[want.p].name} this moon.'",
      $"tip names the best sale price outside the destination {S.ports[dest].name}: {(want.g >= 0 ? S.goods[want.g].id : "-")} in {(want.p >= 0 ? S.ports[want.p].name : "-")} at {(want.g >= 0 ? price[want.p, want.g] : 0):0} (best elsewhere {bestElsewhere:0})");
    Chk(coins == c0 && hold.SequenceEqual(h0) && lastFamily == "merchant" && dialog == null, $"tip: no trade, no cost (coins {c0}->{coins}, hold unchanged {hold.SequenceEqual(h0)}), the encounter counts as shown ({lastFamily})");
    // (b) normal merchant (no sealed goods, room in the hold): the offer as before
    S.holdSize = hs0; System.Array.Clear(hold, 0, hold.Length); coins = 5000; lastFamily = ""; veForce = "merchant"; StartCoroutine(Encounter(false, false)); yield return null; yield return null; tw = steadyClock; while (dialog == null && steadyClock - tw < 5f) yield return null;
    tx = Texts(); bool buyB = dialog && dialog.GetComponentsInChildren<UnityEngine.UI.Button>().Any(b => b.GetComponentInChildren<UnityEngine.UI.Text>() && b.GetComponentInChildren<UnityEngine.UI.Text>().text.StartsWith("Buy"));
    yield return new WaitForSecondsRealtime(0.7f); while (dialog != null) { EgTestCloseCard(); yield return null; }
    Chk(buyB && !tx.Any(t => t.Contains("sealed hold")), $"normal merchant unchanged: offer with a Buy button, no tip ('{string.Join(" | ", tx)}')");
    // (c) sealed goods but room left: the normal offer, no tip
    hold[c.good] = c.qty; S.holdSize = c.qty + 10; coins = 5000; veForce = "merchant"; StartCoroutine(Encounter(false, false)); yield return null; yield return null; tw = steadyClock; while (dialog == null && steadyClock - tw < 5f) yield return null;
    tx = Texts(); buyB = dialog && dialog.GetComponentsInChildren<UnityEngine.UI.Button>().Any(b => b.GetComponentInChildren<UnityEngine.UI.Text>() && b.GetComponentInChildren<UnityEngine.UI.Text>().text.StartsWith("Buy"));
    yield return new WaitForSecondsRealtime(0.7f); while (dialog != null) { EgTestCloseCard(); yield return null; }
    Chk(buyB && !tx.Any(t => t.Contains("sealed hold")), $"sealed goods with room left: normal offer, no tip");
    veForce = vf0; S.holdSize = hs0; System.Array.Clear(hold, 0, hold.Length); tab = -1; Refresh(); yield return null;

    // dock text: sentences joined with a full stop
    Chk(JoinSentences("pepper piled on the docks", "Three ships arrived at once.") == "pepper piled on the docks. Three ships arrived at once." && JoinSentences("Shortage of pepper!", "The stalls.") == "Shortage of pepper! The stalls." && JoinSentences("He said 'go.'", "Then.") == "He said 'go.' Then.",
      "JoinSentences adds a full stop only where a sentence is not already ended");
    int pp = G("pepper"), bad = 0, n = 0; var badL = new System.Collections.Generic.List<string>();
    foreach (var e in ((System.Collections.Generic.List<object>)DZ.market["events"]).Select(Design.D)) { var ct = Content((string)e["id"]); if (!ct.ContainsKey("headline") || !ct.ContainsKey("text")) continue;
      int p = mu, eg = EventGood(p, e); /* the good the game itself picks for this event */ try { StartEvent(p, e, eg); } catch (System.Exception ex) { bad++; badL.Add($"{e["id"]}: {ex.GetType().Name}"); continue; } n++; string line = news[0], h = Fill((string)ct["headline"], p, eg).Trim(); bool ok = line.StartsWith(h) && line.Length > h.Length + 1 && (".!?".IndexOf(h[h.Length - 1]) >= 0 ? line[h.Length] == ' ' : line.Substring(h.Length, 2) == ". ");
      if (!ok) { bad++; badL.Add(line); } }
    var glut = ((System.Collections.Generic.List<object>)DZ.market["events"]).Select(Design.D).FirstOrDefault(e => (string)e["id"] == "glut"); string gl = "";
    if (glut != null) { StartEvent(mu, glut, pp); gl = news[0]; }
    Chk(gl == "pepper piled on the docks. Three ships arrived at once. Muscat is drowning in pepper, and nobody wants to buy.", $"glut news at the dock reads '{gl}'");
    port = mu; Rv.newsAtDock = news.Count - 1; string dm = $"Docked at {S.ports[port].name}. " + DockNews();
    Chk(bad == 0 && n > 0 && dm.Contains("docks. Three ships"), $"every market-event news line keeps its full stop between headline and text ({n} events, {bad} bad{(bad > 0 ? ": " + string.Join(" / ", badL) : "")}); dock message '{dm}'");
    Init(); egTestQuiet = true; storyStep = StDone; tab = -1; Refresh(); yield return null; }
}
