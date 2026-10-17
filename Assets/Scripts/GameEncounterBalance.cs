using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Rnd = UnityEngine.Random;

/// gamedev_encounter_balance (v0.7, Game Designer ruling): ONE encounter roll per voyage (S.encounterChance = 0.5), then one pick from the single
/// weighted pool of 10 (pirate 35 beside the rest). The old separate per-leg ambush roll (AmbushChanceFor, capped 0.5 on every leg not touching Gwadar)
/// no longer decides pirates; only the scripted story fog ambush (and test -gbforce ambush) bypass the pool. Pirates follow the family no-repeat rule
/// like weather: never two pirate encounters in a row (lastFamily, plus the saved lastEncFamily so a decision card in between doesn't reset it).
public partial class Game {
  public string lastEncFamily = ""; /* saved: family of the last voyage ENCOUNTER (decision cards don't touch it) */
  public string vePicked = ""; /* the pool pick for this voyage's encounter (non-pirate id), consumed by Encounter */
  public float vePirateMult = 1f; /* this voyage's pirate weight multiplier: 0 on pirate-free sails, 0.5 after the lighthouse warning */
  public string veForceHailNext = ""; bool veHailFromFlag, veForceHorizonNext; /* gamedev_force_hail: -gbforcehail warning|trade|rumour */

  /// the single per-voyage roll
  public bool VeRollEncounter() => Rnd.value < S.encounterChance;
  /// the pool the normal voyage roll draws from: VePoolIds order (pirate first), pirate scaled by this voyage's multiplier
  public float[] VeVoyageWeights(int from, int to, float pirateMult) { var w = VePoolWeights(from, to); w[0] *= pirateMult; return w; }
  /// pool indices still drawable: weight > 0, not the family just shown, and no pirate right after a pirate encounter
  public List<int> VeEligible(float[] w) { bool noPirate = lastFamily == "pirate" || lastEncFamily == "pirate";
    var ok = Enumerable.Range(0, w.Length).Where(i => w[i] > 0 && VeFamily(VePoolIds[i]) != lastFamily && !(i == 0 && noPirate)).ToList();
    if (ok.Count == 0) ok = Enumerable.Range(1, w.Length - 1).Where(i => w[i] > 0).ToList(); return ok; }
  public int VePickPool(float[] w) { var ok = VeEligible(w); if (ok.Count == 0) return 4; /* rumor */ float tot = ok.Sum(i => w[i]), r = Rnd.value * tot; foreach (int i in ok) { if ((r -= w[i]) < 0) return i; } return ok[ok.Count - 1]; }
  /// chance THIS voyage meets a pirate given the current no-repeat state (encounterChance x pirate share of the eligible weight)
  public float VePirateChance(float[] w) { var ok = VeEligible(w); float tot = ok.Sum(i => w[i]); return ok.Contains(0) && tot > 0 ? S.encounterChance * w[0] / tot : 0; }
  /// one encounter shown: both no-repeat memories move to its family
  public void VeMarkEncounter(string id) { lastEncFamily = VeFamily(id); if (id == "pirate") lastFamily = "pirate"; }
  /// stationary pirate share of encounters for a fixed pool under the family no-repeat rule (Markov chain over the last family), for the qa rate check
  public float VeExpectedPirateShare(float[] w) { var fams = VePoolIds.Select(VeFamily).Distinct().ToList(); var pi = fams.ToDictionary(f => f, f => 1f / fams.Count);
    string lf0 = lastFamily, le0 = lastEncFamily;
    for (int it = 0; it < 300; it++) { var n = fams.ToDictionary(f => f, f => 0f); foreach (var s in fams) { lastFamily = s; lastEncFamily = s; var ok = VeEligible(w); float tot = ok.Sum(i => w[i]); foreach (int i in ok) n[VeFamily(VePoolIds[i])] += pi[s] * w[i] / tot; } pi = n; }
    lastFamily = lf0; lastEncFamily = le0; return pi["pirate"]; }

  /// gamedev_force_hail: -gbforcehail warning|trade|rumour (or GB_FORCE_HAIL, or -gbforceevent hailWarning|hailTrade|hailRumour):
  /// the next voyage's first decision card is "Sail on the horizon"; tapping "Hail her" pays that kind
  public void VeReadForceHailArg(string fromEvent = null) { var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-gbforcehail");
    string v = fromEvent ?? (i >= 0 && i + 1 < a.Length ? a[i + 1] : System.Environment.GetEnvironmentVariable("GB_FORCE_HAIL")); if (string.IsNullOrEmpty(v)) return;
    v = v.ToLower(); if (v == "rumor") v = "rumour";
    if (v == "warning" || v == "trade" || v == "rumour") { veForceHailNext = v; Debug.Log($"GB HAIL force next voyage: {v} (first decision card is 'Sail on the horizon'; tap 'Hail her'" + (v == "warning" ? "; needs a leg with ambush risk somewhere ahead, a leg into Gwadar falls back to rumour" : "") + ")"); }
    else Debug.Log("GB HAIL force: unknown kind '" + v + "' (valid: warning, trade, rumour)"); }
  /// voyage start: arm a pending hail force for this voyage only
  void VeArmHailForce() { if (veHailFromFlag) { veForceHail = ""; veHailFromFlag = false; veForceHorizonNext = false; }
    if (veForceHailNext != "") { veForceHail = veForceHailNext; veForceHailNext = ""; veHailFromFlag = true; veForceHorizonNext = true; Debug.Log("GB HAIL forced this voyage: " + veForceHail); } }

  // ---- gamedev_route_label (Game Designer ruling): the route danger label is the REAL pirate chance of the next voyage under the one-roll system, no percentage shown ----
  public const float RouteQuietBelow = 0.10f, RouteDeadlyAbove = 0.20f; /* Quiet < 10% <= Pirate <= 20% < Deadly */
  public bool NextSailFree => sails + 1 <= S.pirateFreeSails || sails + 1 < safeUntilSail; /* the same test Voyage makes after sails++ */
  public bool FogAhead(int to) => InOpening && !storyAmbushDone && to == Muscat && hold[Gp] > 0; /* the scripted story fog ambush: certain */
  /// encounterChance x pirate weight share of the leg's pool, with this voyage's modifiers: free sail 0, lighthouse warning x0.5 on the pirate weight,
  /// no pirate right after a pirate encounter (lastEncFamily). lastFamily's other families are not taken out: the voyage's decision cards replace lastFamily before the pick.
  public float RoutePirateChance(int from, int to) { if (to < 0 || to == from) return 0; if (FogAhead(to)) return 1f; if (NextSailFree || lastEncFamily == "pirate") return 0;
    var w = VeVoyageWeights(from, to, ambushHalveNext ? 0.5f : 1f); float tot = w.Where(x => x > 0).Sum(); return tot > 0 ? S.encounterChance * w[0] / tot : 0; }
  public static string RouteLabelName(float c) => c < RouteQuietBelow ? "Quiet waters" : c <= RouteDeadlyAbove ? "Pirate waters" : "Deadly waters";
  public const string QuietHex = "#D9C08A", PirateHex = "#E0B85A", DeadlyHex = "#C0685A"; /* gamedev_route_art: Chief Art Designer tier colours */
  public static string RouteTierHex(float c) => c < RouteQuietBelow ? QuietHex : c <= RouteDeadlyAbove ? PirateHex : DeadlyHex;
  public string RouteLabelFor(float c) => $"<color={RouteTierHex(c)}>{RouteLabelName(c)}</color>";

  // ---- gamedev_route_art: sea-map labels (port plaques, contract/gunpowder flags) are nudged inward so a label that shows at all shows whole ----
  public readonly List<RectTransform> chartLabels = new List<RectTransform>(); readonly Dictionary<RectTransform, Vector2> chartLabelHome = new Dictionary<RectTransform, Vector2>();
  public const float ChartLabelPad = 8f;
  /// view = the map viewport (W x H) in map coordinates: x from -pan.x, y-down from pan.y. Labels fully outside the view stay home (masked, not drawn).
  void ClampChartLabels(float W, float H) { float vl = -chartPanPx.x, vr = vl + W, vt = chartPanPx.y, vb = vt + H;
    foreach (var l in chartLabels) { if (l == null || !chartLabelHome.TryGetValue(l, out var h)) continue; float x = h.x, yt = -h.y, w = l.sizeDelta.x, hh = l.sizeDelta.y;
      if (x + w <= vl || x >= vr || yt + hh <= vt || yt >= vb) { l.anchoredPosition = h; continue; }
      x = Mathf.Clamp(x, vl + ChartLabelPad, vr - ChartLabelPad - w); yt = Mathf.Clamp(yt, vt + ChartLabelPad, vb - ChartLabelPad - hh); l.anchoredPosition = new Vector2(x, -yt); } }
  public string RouteLabel(int from, int to) => RouteLabelFor(RoutePirateChance(from, to));
}
