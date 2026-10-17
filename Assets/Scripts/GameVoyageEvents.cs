using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Rnd = UnityEngine.Random;

/// gamedev_voyage_events: wording variants for the voyage/encounter cards from the committed Resources/Design/voyage_events.json
/// (research/voyage-events be8abe3). Variants change wording only; outcomes stay as coded. Families never repeat back to back, across voyages too (lastFamily is saved).
public partial class Game {
  Dictionary<string, object> veRoot; public string lastFamily = ""; public int veForceVariant = -1; public string veForce = "";
  Dictionary<string, object> VE { get { if (veRoot == null) { var ta = Resources.Load<TextAsset>("Design/voyage_events"); veRoot = ta ? Design.D(MiniJson.Parse(ta.text)) : new Dictionary<string, object>(); int nNew = ta && veRoot.TryGetValue("new", out var nn) && nn is List<object> nl ? nl.Count : 0; Debug.Log($"GB VEVENTS loaded {(ta ? VeIds.Count : 0)} existing card texts + {nNew} new events; encounter pool {VePoolIds.Length} events: {string.Join(", ", VePoolIds)}"); /* gamedev_ve_pool: the old line counted only the 7 'existing' text blocks (horizon/storm/wreckage decision cards + 4 encounters), never the new events */ } return veRoot; } }
  public Dictionary<string, object> VeEvent(string id) => Design.D(Design.D(VE.ContainsKey("existing") ? VE["existing"] : null).TryGetValue(id, out var e) ? e : null);
  public List<string> VeIds => Design.D(VE.ContainsKey("existing") ? VE["existing"] : null).Keys.ToList();
  public List<Dictionary<string, object>> VeVariants(string id) { var e = VeEvent(id); return e.TryGetValue("variants", out var v) && v is List<object> l ? l.Select(Design.D).ToList() : new List<Dictionary<string, object>>(); }
  public string VeFamily(string id) { var e = VeEvent(id); if (e.Count == 0) e = VeNew(id); return e.TryGetValue("family", out var f) ? f as string ?? id : id; }
  readonly Dictionary<string, int> veLast = new Dictionary<string, int>();
  /// a random variant (not the same one twice in a row for this event); empty dict = fall back to the coded text
  public Dictionary<string, object> VePick(string id) { var vs = VeVariants(id); if (vs.Count == 0) return new Dictionary<string, object>();
    int i = veForceVariant >= 0 ? Mathf.Min(veForceVariant, vs.Count - 1) : Rnd.Range(0, vs.Count); if (veForceVariant < 0 && vs.Count > 1 && veLast.TryGetValue(id, out int li) && li == i) i = (i + 1 + Rnd.Range(0, vs.Count - 1)) % vs.Count;
    veLast[id] = i; Debug.Log($"GB VEVENT {id} variant {i + 1}/{vs.Count} family {VeFamily(id)}"); return vs[i]; }
  public static string VeS(Dictionary<string, object> v, string key, string fb) => v != null && v.TryGetValue(key, out var o) && o is string s ? s : fb;
  public static string VeOpt(Dictionary<string, object> v, int i, string fb) => v != null && v.TryGetValue("options", out var o) && o is List<object> l && i < l.Count && l[i] is string s ? s : fb;
  public static string VeFill(string s, params (string k, string v)[] kv) { foreach (var (k, v) in kv) s = s.Replace("{" + k + "}", v); return s; }
  public int qaExceptions; bool veHooked; public void VeHookExceptions() { if (veHooked) return; veHooked = true; Application.logMessageReceived += (c, st, t) => { if (t == LogType.Exception) qaExceptions++; }; }
  void VeShown(string id) { lastFamily = VeFamily(id); }
  /// decision kinds (0 horizon, 1 storm, 2 wreckage) still free this voyage and not the family just shown
  static readonly string[] DecisionIds = { "horizon", "storm", "wreckage" };
  public List<int> VeFreeKinds(int usedMask) => Enumerable.Range(0, 3).Where(k => (usedMask & (1 << k)) == 0 && VeFamily(DecisionIds[k]) != lastFamily).ToList();
  /// encounter (non-pirate) types with their event ids: the family just shown is rerolled away when another type exists
  public static readonly string[] EncIds = { "wreckAdrift", "merchant", "squall", "rumor", "pearlDiver", "leakyDhow", "customsLaunch", "squallLine", "lighthouse" };
  public static readonly string[] VePoolIds = { "pirate", "wreckAdrift", "merchant", "squall", "rumor", "pearlDiver", "leakyDhow", "customsLaunch", "squallLine", "lighthouse" }; /* gamedev_ve_pool: the full 10-event voyage pool */
  public string veForceNext = ""; /* gamedev_ve_force: -gbforceevent <id> / GB_FORCE_EVENT=<id>: the next voyage gets this encounter */
  public void VeReadForceArg() { var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-gbforceevent"); string v = i >= 0 && i + 1 < a.Length ? a[i + 1] : System.Environment.GetEnvironmentVariable("GB_FORCE_EVENT"); if (!string.IsNullOrEmpty(v)) { if (v == "hailWarning" || v == "hailTrade" || v == "hailRumour" || v == "hailRumor") VeReadForceHailArg(v.Substring(4)); /* gamedev_force_hail alias */ else if (System.Array.IndexOf(VePoolIds, v) >= 0) { veForceNext = v; Debug.Log("GB VEVENTS force next event: " + v); } else Debug.Log("GB VEVENTS force: unknown event '" + v + "' (valid: " + string.Join(", ", VePoolIds) + ", hailWarning, hailTrade, hailRumour)"); } }
  /// gamedev_ve_pool: the weight table the normal voyage roll uses (pirate first, then EncIds order), 0 = not eligible on this leg
  public float[] VePoolWeights(int from, int to) { var e = EncWeights(from, to); var r = new float[e.Length + 1]; r[0] = S.weights.pirate; System.Array.Copy(e, 0, r, 1, e.Length); return r; }
  public string VeWeightLog(int from, int to, float ap, bool encountered) { var w = VeVoyageWeights(from, to, vePirateMult); var ok = VeEligible(w); float tot = ok.Sum(i => w[i]); /* gamedev_encounter_balance: one roll, one pool */
    return $"GB VEVENTS weights {S.ports[from].name}->{S.ports[to].name} moon {MoonOfYear}{(Monsoon ? " (monsoon)" : "")} lastFamily '{lastFamily}' lastEnc '{lastEncFamily}': " + string.Join(" ", Enumerable.Range(0, w.Length).Select(i => $"{VePoolIds[i]} {w[i]:0.#}{(w[i] > 0 && !ok.Contains(i) ? "x" : "")}")) + $" | eligible {ok.Count} (weight {tot:0.#}; x = no-repeat) | encounterChance {S.encounterChance:0.###} pirate {ap:0.###} (pirateMult {vePirateMult:0.##}, route label {RouteLabelName(ap)}) encountered {encountered}"; }
  public int VePickEncounter(float[] w) { var ok = Enumerable.Range(0, w.Length).Where(i => w[i] > 0 && VeFamily(EncIds[i]) != lastFamily).ToList(); if (ok.Count == 0) ok = Enumerable.Range(0, w.Length).Where(i => w[i] > 0).ToList(); if (ok.Count == 0) return 3;
    float tot = ok.Sum(i => w[i]), r = Rnd.value * tot; foreach (int i in ok) { if ((r -= w[i]) < 0) return i; } return ok[ok.Count - 1]; }
}
