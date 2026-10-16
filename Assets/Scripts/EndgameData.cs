using System;
using System.Collections.Generic;
/* gamedev_end1: ALL endgame first-slice tunables and strings. Loaded from Resources/endgame_data.json, which is Game Researcher's
   design/endgame_data_slice1.json shape (research/endgame-data 2018624, draft, not signed off): drop a new version of that file in and it is used as-is.
   The constants below are only fallbacks if a key is missing. */
public class EgTemplate { public string id, port, kind, title, giver, good, dest, risk; public int qty, moons, coins, renown, deposit, failRenown; }
public static class EndgameData {
  static Dictionary<string, object> root; static Dictionary<string, object> R { get { if (root == null) { var ta = UnityEngine.Resources.Load<UnityEngine.TextAsset>("endgame_data"); root = ta ? Design.D(MiniJson.Parse(ta.text)) : new Dictionary<string, object>(); UnityEngine.Debug.Log("GB ENDGAME data " + (ta ? "endgame_data.json version=" + (root.TryGetValue("version", out var v) ? v : "?") : "MISSING, fallbacks")); } return root; } }
  static Dictionary<string, object> Sec(string k) => Design.D(R.TryGetValue(k, out var o) ? o : null);
  static int I(Dictionary<string, object> d, string k, int def) => d.TryGetValue(k, out var o) && o is double x ? (int)x : def;
  static string Str(Dictionary<string, object> d, string k, string def) => d.TryGetValue(k, out var o) && o is string s ? s : def;

  // ---- timing ----
  public static int BoardSize => I(Sec("timing"), "boardSize", 3);
  public static int MaxActive => I(Sec("timing"), "maxActive", 2);
  public static int DeliveryMoons => I(Sec("timing"), "deliveryMoons", 2);
  public static int RushMoons => I(Sec("timing"), "rushMoons", 1);
  /* design sign-off condition (17:22): reward caps, clamped in code too. timing.rewardCap {delivery, rush} in the data */
  public static int CapDelivery => I(Design.D(Sec("timing").TryGetValue("rewardCap", out var o) ? o : null), "delivery", 900);
  public static int CapRush => I(Design.D(Sec("timing").TryGetValue("rewardCap", out var o) ? o : null), "rush", 1200);
  public static int Cap(string kind) => kind == "rush" ? CapRush : CapDelivery;
  /* Deckhand: one starter delivery per board; the full board (3, rush included) opens at Trader (rank index 1) */
  public const int StarterBoardSize = 1, FullBoardRank = 1;
  public static readonly string[] BoardPorts = { "Gwadar", "Debal", "Muscat" };

  // ---- Renown (Jeff 17:17: none from selling except the first sale of a good at a port) ----
  public static int RenownDelivery => I(Sec("renown"), "delivery", 20);
  public static int RenownRush => I(Sec("renown"), "rush", 30);
  public static int RenownFightWin => I(Sec("renown"), "fightWin", 5);
  public static int RenownFirstSale => I(Sec("renown"), "firstSaleBonus", 3);
  public static int FailDelivery => I(Sec("renown"), "failDelivery", -10);
  public static int FailRush => I(Sec("renown"), "failRush", -15);
  public const bool RenownFloorAtRank = true; /* "Renown never drops below current rank threshold" */

  // ---- ranks (first 3 in slice 1) ----
  static List<Dictionary<string, object>> ranks; static List<Dictionary<string, object>> Ranks { get { if (ranks == null) { ranks = new List<Dictionary<string, object>>(); if (R.TryGetValue("ranks", out var o) && o is List<object> l) foreach (var x in l) ranks.Add(Design.D(x)); if (ranks.Count == 0) { ranks.Add(new Dictionary<string, object> { { "name", "Deckhand" }, { "renown", 0.0 } }); ranks.Add(new Dictionary<string, object> { { "name", "Trader" }, { "renown", 100.0 } }); ranks.Add(new Dictionary<string, object> { { "name", "Merchant" }, { "renown", 300.0 } }); } } return ranks; } }
  public static int RankCount => Math.Min(3, Ranks.Count);
  public static string RankName(int i) => Str(Ranks[i], "name", "?");
  public static int RankRenown(int i) => I(Ranks[i], "renown", 0);
  public static string RankPromo(int i) => Str(Ranks[i], "promo", "You are now a " + RankName(i) + ".");
  public const string RankUpTitle = "A new title", RankUpButton = "Splendid"; /* not in the draft data yet */

  // ---- strings ----
  public static string S(string k, string def) => Str(Sec("strings"), k, def);
  public static string MoonsLeft(int moons, bool lastWeek) => lastWeek ? S("lastWeek", "Last week") : moons == 1 ? S("moonsLeft1", "1 moon left") : S("moonsLeftN", "{n} moons left").Replace("{n}", moons.ToString());
  public static string Reward(int coins, int renown) => S("reward", "{coins} coins · +{renown} Renown").Replace("{coins}", coins.ToString("N0")).Replace("{renown}", renown.ToString());
  public static string Deposit(int d) => S("deposit", "Deposit {d} coins, returned on delivery").Replace("{d}", d.ToString("N0"));
  public static string Delivered(int coins, int renown) => S("delivered", "Delivered. +{coins} coins, +{renown} Renown.").Replace("{coins}", coins.ToString("N0")).Replace("{renown}", renown.ToString());
  public static string Failed(int renown) => S("failed", "The buyer found another captain. {renown} Renown, deposit lost.").Replace("{renown}", (renown <= 0 ? "−" + (-renown) : "+" + renown));
  public static string Full => S("full", "You can hold 2 contracts.");
  public static string NextRank(string rank, int cur, int need) => S("nextRank", "Next rank: {rank}, {cur} / {need} Renown").Replace("{rank}", rank).Replace("{cur}", cur.ToString("N0")).Replace("{need}", need.ToString("N0"));
  public static string FirstSale(string good, int r) => S("firstSale", "First {good} sold here. +{renown} Renown.").Replace("{good}", good).Replace("{renown}", r.ToString());
  public static string ListRow(string type, string dest, string moons, int coins) => S("listRow", "{type} · {dest} · {moons} · {coins} coins").Replace("{type}", type).Replace("{dest}", dest).Replace("{moons}", moons).Replace("{coins}", coins.ToString("N0"));
  public const string BoardTitle = "Contract board", BoardEmpty = "The board is bare. New notices go up each moon.", BoardLocked = "The board is for captains who have made their name. Finish the Muscat run first.", CantAfford = "You can't cover the deposit.", NeedCargo = "Bring the cargo in your hold to deliver.";
  public const string AmberColor = "#E8A33A", RedColor = "#D0503A"; /* same amber/red as the debt line */

  // ---- contract templates (24: 8 per port, 4 delivery + 4 rush) ----
  static List<EgTemplate> tpl; public static List<EgTemplate> Templates { get { if (tpl == null) { tpl = new List<EgTemplate>(); foreach (var pk in Sec("contracts")) { var pd = Design.D(pk.Value); foreach (var kind in new[] { "delivery", "rush" }) if (pd.TryGetValue(kind, out var lo) && lo is List<object> l) foreach (var x in l) { var d = Design.D(x);
            tpl.Add(new EgTemplate { id = Str(d, "id", pk.Key + kind + tpl.Count), port = pk.Key, kind = kind, title = Str(d, "title", "Contract"), giver = Str(d, "giver", ""), good = Str(d, "good", "salt"), dest = Str(d, "dest", "Debal"), risk = Str(d, "risk", ""), qty = I(d, "qty", 5), moons = I(d, "moons", kind == "rush" ? RushMoons : DeliveryMoons), coins = I(d, "coins", 300), renown = I(d, "renown", kind == "rush" ? RenownRush : RenownDelivery), deposit = I(d, "deposit", 0), failRenown = I(d, "failRenown", kind == "rush" ? FailRush : FailDelivery) }); } } } return tpl; } }
}
