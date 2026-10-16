using System; using System.Collections.Generic; using System.Linq; using System.Text;
/// Every fight lands in exactly one bucket. WIN: pirate sunk / disabled+surrendered / boarding duel won (either side boarding).
/// WIN also: losing pirate pays a toll at the time limit (tollSeconds). LOSS: Hasan sunk / boarding duel lost / Hasan strikes his colours / toll paid (bots never pay the toll, so that bucket is folded out). DRAW: either ship escapes, or night falls (time limit).
/// Win rate = wins / (wins + losses); draws excluded. Every boarding duel is played out.
public static class CombatSim {
  static readonly string[] Buckets = { "WIN  pirate sunk", "WIN  pirate surrendered (disabled or crew broken)", "WIN  boarding duel won (Hasan boarded)", "WIN  boarding duel won (repelled pirate boarders)",
    "LOSS Hasan sunk", "LOSS boarding duel lost (Hasan boarded)", "LOSS boarding duel lost (pirate boarded Hasan)", "LOSS Hasan struck colours (crew broken / crippled)", "DRAW pirate escaped", "DRAW night fell (time limit)", "WIN  losing pirate paid toll and struck (140 s)" };
  public static string Run(CombatCfg C, int n) { var sb = new StringBuilder(); for (int d = 0; d < Math.Max(1, C.difficulties?.Length ?? 0); d++) foreach (var b in C.bots) for (int tier = 0; tier < C.pirateTiers.Length; tier++) sb.Append(RunBot(C, b, n, tier, d)).Append("\n"); return sb.ToString(); }
  public static string RunBot(CombatCfg C, BotProfile bot, int n, int tier, int diff = 1) {
    var count = new int[Buckets.Length]; var planUse = new int[4]; var planWin = new int[4]; var planLen = new double[4]; double totT = 0; var lens = new List<float>(); int duels = 0, duelWins = 0;
    for (int i = 0; i < n; i++) {
      var c = new CombatCore(C, 1000 + i, false, tier, diff); if (duels < C.noviceDuels) c.noviceZoneBonus = C.noviceDuelBonus; /* only the first few duels of a career, as in the game */ Outcome o = Outcome.None;
      while (o == Outcome.None) { c.HasanBot(bot); o = c.Step(0.1f); if (o == Outcome.None && c.wantBoard && c.BoardAllowed(c.H, c.P)) o = Outcome.BoardReady; }
      float len = c.t; int bucket;
      if (o == Outcome.Surrender || (o == Outcome.Disabled && !(c.plan == 3 || c.R.NextDouble() < bot.boardDisabledChance))) bucket = 1; // takes the surrender
      else if (o == Outcome.Disabled || o == Outcome.BoardReady) { duels++; len += C.ringSeconds * C.duelRounds + 3; bool w = c.SimDuel(true, bot.duelHitMult); if (w) duelWins++; bucket = w ? 2 : 5; }
      else if (o == Outcome.HasanBoarded) { duels++; len += C.ringSeconds * C.duelRounds + 3; bool w = c.SimDuel(false, bot.duelHitMult); if (w) duelWins++; bucket = w ? 3 : 6; }
      else if (o == Outcome.Sunk) bucket = 0; else if (o == Outcome.HasanSunk) bucket = 4; else if (o == Outcome.HasanStruck) bucket = 7; else if (o == Outcome.Escaped) bucket = 8; else if (o == Outcome.TollPaid) bucket = 10; else bucket = 9;
      count[bucket]++; planUse[c.plan]++; planLen[c.plan] += len; if (bucket <= 3 || bucket == 10) planWin[c.plan]++; totT += len; lens.Add(len); }
    lens.Sort(); int W = count[0] + count[1] + count[2] + count[3] + count[10], L = count[4] + count[5] + count[6] + count[7], D = count[8] + count[9];
    var sb = new StringBuilder();
    sb.Append($"=== Bot: {bot.name}  vs {C.pirateTiers[tier].name} pirates  on {(C.difficulties != null && C.difficulties.Length > 0 ? C.difficulties[diff].name : "-")}  ({n} fights) ===\n");
    for (int k = 0; k < Buckets.Length; k++) sb.Append($"  {Buckets[k],-50} {count[k],5}\n");
    sb.Append($"  {"TOTAL",-50} {count.Sum(),5}\n");
    sb.Append($"Wins {W}  Losses {L}  Draws {D}  ->  win rate W/(W+L) = {100.0 * W / Math.Max(1, W + L):0.0}%\n");
    sb.Append($"Share of wins: sunk {100.0 * count[0] / Math.Max(1, W):0}%, boarding duels {100.0 * (count[2] + count[3]) / Math.Max(1, W):0}%, surrender {100.0 * count[1] / Math.Max(1, W):0}%, toll {100.0 * count[10] / Math.Max(1, W):0}%\n");
    sb.Append($"Boarding duels fought {duels}, won {duelWins} = {100.0 * duelWins / Math.Max(1, duels):0.0}%\n");
    sb.Append("Bot plan used (wins): " + string.Join(", ", Enumerable.Range(0, 4).Select(k => $"{CombatCore.PlanNames[k]} {planUse[k]} (won {planWin[k]}, avg {planLen[k] / Math.Max(1, planUse[k]):0} s)")) + "\n");
    sb.Append($"Average fight length {totT / n:0.0} s (median {lens[n / 2]:0}, p10 {lens[n / 10]:0}, p90 {lens[n * 9 / 10]:0})\n");
    return sb.ToString(); }
}
