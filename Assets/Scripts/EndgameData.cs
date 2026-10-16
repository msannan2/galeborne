using System;
/* gamedev_end1: ALL endgame first-slice tunables and strings live here, so Game Researcher's research/endgame-data numbers and copy can drop in.
   Placeholder numbers: ~25 Renown per contract on average => Trader (100) after ~4 contracts, Merchant (300) after ~12. */
public static class EndgameData {
  // ---- ranks (first 3 only in slice 1) ----
  public static readonly string[] RankNames = { "Deckhand", "Trader", "Merchant" };
  public static readonly int[] RankRenown = { 0, 100, 300 };
  public const string RankUpTitle = "A new title";
  public static readonly string[] RankUpBody = { "", "The harbour talks about you now. You are a Trader.", "Your name opens doors from Gwadar to Muscat. You are a Merchant." };
  public const string RankUpButton = "Splendid";
  public const string NextRankLine = "Next rank: {rank}, {have} / {need} Renown"; /* Journal */
  public const string TopRankLine = "Rank: {rank}, {have} Renown";

  // ---- Renown sources (Jeff 17:17: no Renown from selling, except first sale of a good at a port) ----
  public const int RenownDelivery = 20, RenownRush = 32, RenownFightWin = 6, RenownFirstSale = 2;
  public const int RenownFailPenalty = 10;
  // public const int RenownBounty = 0; // bounties come later (Captain rank)

  // ---- contracts ----
  public static readonly string[] BoardPorts = { "Gwadar", "Debal", "Muscat" };
  public const int BoardSize = 3, MaxActive = 2;
  public const int DeliveryMoons = 2, RushMoons = 1; /* rush: half the time */
  public const float RushPayMult = 2f;
  public const int QtyMin = 4, QtyMax = 9;
  public const float PayPerUnitOfBase = 0.6f; public const int PayFlat = 60; /* delivery pay = qty * basePrice(dest) * 0.6 + 60 */
  public const float DepositFrac = 0.15f; /* paid on accept, returned on delivery */
  public const bool BoardNeedsOpeningDone = true;

  // ---- strings (keyed by port for the port voice) ----
  public static string TitleFor(string kind, string good, string dest) => kind == "rush" ? $"Rush {good.ToLower()} to {dest}" : $"{good} for {dest}";
  public static string GiverLine(string port) => port == "Gwadar" ? "\"Plain job, Captain. Get it there, get paid.\"" : port == "Debal" ? "\"Quick hands, quick silver, my friend. The buyer won't wait!\"" : port == "Muscat" ? "\"The house of the buyer expects discretion and punctuality.\"" : "\"Get it there in time.\"";
  public const string RiskDelivery = "Low risk. Pirates may still find you at sea.";
  public const string RiskRush = "Tight deadline. A storm or a fight can cost you the time.";
  public static string RowLine(string kind, string dest, int moons, int pay) => $"{(kind == "rush" ? "Rush" : "Delivery")} · {dest} · {moons} moon{(moons == 1 ? "" : "s")} · {pay:N0} coins";
  public static string MoonsLeft(int moons) => moons <= 0 ? "Due this week" : $"{moons} moon{(moons == 1 ? "" : "s")} left";
  public static string RewardLine(int pay, int renown) => $"{pay:N0} coins · +{renown} Renown";
  public static string DepositLine(int dep) => dep > 0 ? $"Deposit {dep:N0} coins, returned on delivery" : "No deposit";
  public static string DeliveredBody(int pay, int renown) => $"Delivered. +{pay:N0} coins, +{renown} Renown.";
  public static string FailedBody(int dep) => $"The buyer found another captain. −{RenownFailPenalty} Renown" + (dep > 0 ? ", deposit lost." : ".");
  public const string BoardTitle = "Contract board", BoardEmpty = "The board is bare. New notices go up each moon.", BoardLocked = "The keeper's board is for captains who have made their name. Finish the Muscat run first.";
  public const string Taken = "Accepted", FullNote = "You can hold 2 contracts at once.", CantAfford = "You can't cover the deposit.";
  public const string AcceptBtn = "Accept", NotNowBtn = "Not now";
  public const string AmberColor = "#E8A33A", RedColor = "#D0503A"; /* same amber/red as the debt line */
}
