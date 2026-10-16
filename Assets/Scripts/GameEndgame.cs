using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using ED = EndgameData;

/* gamedev_end1: endgame first slice - Renown, the first 3 ranks, delivery/rush contracts at the tavern boards. Numbers/strings in EndgameData. */
[Serializable] public class EgContract { public string id, kind, from, to; public int good, qty, pay, renown, deposit, offeredMoon, dueWeek = -1; public string status = "offer"; } /* gamedev_end1: offer|active|done|failed */
[Serializable] public class EgState { public int renown, rank; public List<string> firstSales = new List<string>(); public List<EgContract> board = new List<EgContract>(); public List<EgContract> active = new List<EgContract>(); public int boardMoon = -1, seq; } /* gamedev_end1: saved with the game */

public partial class Game {
  public EgState eg = new EgState(); /* gamedev_end1 */
  readonly Queue<(string t, string b, string btn)> egCards = new Queue<(string, string, string)>(); /* gamedev_end1: rank-up / delivered / failed cards, shown when nothing else is up */
  bool EgForce => gbForce.Contains("endgame"); /* gamedev_end1: -gbforce endgame unlocks the board before the opening ends */
  void EgReset() { eg = new EgState(); egCards.Clear(); } /* gamedev_end1 */
  public string RankName => ED.RankNames[Mathf.Clamp(eg.rank, 0, ED.RankNames.Length - 1)];
  public void AddRenown(int n, string why) { /* gamedev_end1: the ONLY way Renown changes */
    if (n == 0) return; eg.renown = Mathf.Max(0, eg.renown + n); Debug.Log($"GB RENOWN {(n > 0 ? "+" : "")}{n} ({why}) -> {eg.renown} rank={RankName}");
    while (eg.rank + 1 < ED.RankRenown.Length && eg.renown >= ED.RankRenown[eg.rank + 1]) { eg.rank++; Debug.Log("GB RENOWN rank up -> " + RankName); egCards.Enqueue((ED.RankUpTitle + ": " + RankName, ED.RankUpBody[eg.rank], ED.RankUpButton)); }
  }
  public string RenownLine() => eg.rank + 1 < ED.RankRenown.Length ? ED.NextRankLine.Replace("{rank}", ED.RankNames[eg.rank + 1]).Replace("{have}", eg.renown.ToString("N0")).Replace("{need}", ED.RankRenown[eg.rank + 1].ToString("N0")) : ED.TopRankLine.Replace("{rank}", RankName).Replace("{have}", eg.renown.ToString("N0")); /* gamedev_end1 */
  void EgFirstSale(int g) { string k = port + ":" + g; if (eg.firstSales.Contains(k)) return; eg.firstSales.Add(k); AddRenown(ED.RenownFirstSale, "first sale of " + S.goods[g].name + " at " + S.ports[port].name); } /* gamedev_end1 */
  void EgFightWon(string how) => AddRenown(ED.RenownFightWin, "fight won: " + how); /* gamedev_end1 */
  void EgUpdate() { /* gamedev_end1: from Update - show queued cards one at a time when the screen is free */
    if (egCards.Count == 0 || onTitle || fighting || sailing || dialog != null || CardBusy) return;
    var c = egCards.Dequeue(); StartCoroutine(Ask(c.t, c.b, (c.btn, null)));
  }
}
