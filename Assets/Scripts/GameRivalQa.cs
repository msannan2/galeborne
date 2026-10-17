using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using ED = EndgameData;
using Rnd = UnityEngine.Random;

/* gamedev_v07: scripted checks for Rashid of the Red Dhow and the grand contracts. -gbforce v07_test (standalone, needs -gbsavedir) or inside -gbforce qa_run.
   Logs "GB V07TEST PASS|FAIL ...", screenshots to <savedir>/v07test. */
public partial class Game {
  int v07Fails, v07Checks;
  static Color[] ReadTexPixels(Texture tex, out int w, out int h) { w = tex.width; h = tex.height; var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); var prev = RenderTexture.active; Graphics.Blit(tex, rt); RenderTexture.active = rt;
    var t = new Texture2D(w, h, TextureFormat.RGBA32, false); t.ReadPixels(new Rect(0, 0, w, h), 0, 0); t.Apply(); RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt); var px = t.GetPixels(); Destroy0(t); return px; }
  static float RedFrac(Color[] px, int w, int h, RectInt r, int N) { int red = 0, n = 0; for (int y = r.y + 4; y < r.yMax - 4; y += 2) for (int x = r.x + 4; x < r.xMax - 4; x += 2) { var c = px[(y * h / N) * w + x * w / N]; n++; if (ShipArt.IsRed(c)) red++; } return n == 0 ? 0 : red / (float)n; }
  Button FindBtn(Transform p, string label) => p ? p.GetComponentsInChildren<Button>(true).FirstOrDefault(b => { var t = b.GetComponentInChildren<Text>(); return t && t.text == label; }) : null;
  string FaceIn(Transform p) { var ri = p ? p.GetComponentsInChildren<RawImage>(true).FirstOrDefault(x => x.gameObject.name.StartsWith("RivalPortrait")) : null; return ri && ri.texture ? ri.texture.name : "none"; }

  /// gamedev_v07: real voyages with the gameplay camera. Reads the frame the player sees (ScreenCapture after the camera + UI drew) and counts
  /// red-sail pixels; asserts the Red Dhow is in that frame for >= 3 continuous seconds, and that the dock ticker appears once per arrival.
  static Rect ScreenRect(Graphic g) { var c = new Vector3[4]; g.rectTransform.GetWorldCorners(c); return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y); }
  static float ScreenPx(Graphic g) { if (!g) return 0; var c = new Vector3[4]; g.rectTransform.GetWorldCorners(c); return Mathf.Abs(c[1].y - c[0].y); } /* overlay canvas: world corners are screen pixels */
  static int RedFramePixels(Texture2D t) { var px = t.GetPixels32(); int w = t.width, n = 0; for (int y = 0; y < t.height; y += 2) for (int x = 0; x < w; x += 2) if (SailRed(px[y * w + x])) n++; return n; }
  /* red-sail pixel in a lit frame: hue within 12 deg of red, saturated; excludes the brown hulls (hue ~20 deg) that ShipArt.IsRed admits */
  static bool SailRed(Color c) { Color.RGBToHSV(c, out float h, out float s, out float v); return (h <= 12f / 360f || h >= 350f / 360f) && s > 0.5f && v > 0.1f; }
  /// gamedev_v07 sealed cargo (Game Designer): with 20 sealed dates aboard, squalls / squall lines / pirates / customs take other cargo first,
  /// then gold, then hull; the dates stay 20 and the contract completes on arrival.
  IEnumerator V07Sealed(Action<bool, string> Chk, Action<string> L, string dir) {
    int hs0 = S.holdSize; Init(); egTestQuiet = true; storyStep = StDone; port = PortIdx("Gwadar"); week = 3 * Wk; tab = -1; rvCards.Clear(); egCards.Clear(); Refresh();
    int dt = G("dates"), cu = G("copper"), db = PortIdx("Debal"); AddRenown(ED.RankRenown(2) - eg.renown, "qa sealed"); egCards.Clear(); GrandEnsure(); var c = Big; GrandAccept(c);
    Chk(GrandActive != null && c.good == dt && c.qty == 20, $"sealed setup: grand contract {c?.tpl} active ({c?.qty}x{(c != null ? S.goods[c.good].id : "?")} -> {c?.to})");
    // order: other cargo -> gold -> hull (the code path pirate toll and defeat use)
    S.holdSize = 22; System.Array.Clear(hold, 0, hold.Length); hold[dt] = 20; hold[cu] = 2; coins = 100; hullNow = -1; float h0 = HullCur; int unit = Mathf.CeilToInt(price[port, dt]);
    Chk(SealedQty(dt) == 20 && LosableQty(dt) == 0 && LosableGoods().SequenceEqual(new[] { cu }), $"20 dates sealed, losable: {string.Join(",", LosableGoods().Select(g => S.goods[g].id))}");
    var sealedTexts = new List<string>(); string seal0 = GStr("sealed", "Your sealed").Split('{')[0].Trim();
    string s1 = PayCargoSealed(3, "qa toll 3"); sealedTexts.Add(s1); Chk(hold[dt] == 20 && hold[cu] == 0 && coins == 100 - unit && Mathf.Approximately(HullCur, h0), $"toll of 3: 2 copper first, then 1 unit in gold ({unit}); dates {hold[dt]}, coins {coins}, hull {HullCur:0} ('{s1}')");
    coins = 0; string s2 = PayCargoSealed(2, "qa toll 2"); sealedTexts.Add(s2); Chk(hold[dt] == 20 && coins == 0 && HullCur < h0, $"no gold left: the hull takes it ({h0:0} -> {HullCur:0}); dates {hold[dt]} ('{s2}')");
    S.holdSize = hs0; System.Array.Clear(hold, 0, hold.Length); hold[dt] = 20; hullNow = -1; coins = 300;
    // real events: classic squall, squall line (cut loss), customs seize of a banned good, lighthouse "spare cargo" (off)
    for (int k = 0; k < 4; k++) { int c0 = coins; float hh = HullCur; yield return QaVe("squall", 0, k == 0 ? "sealed_squall" : null); Chk(hold[dt] == 20, $"squall {k + 1}: dates stay 20 (coins {c0} -> {coins}, hull {hh:0} -> {HullCur:0}) card '{qvCard.Replace("\n", " ")}'"); if (k == 0) { L("sealed squall card: " + qvCard); } sealedTexts.Add(qvCard); }
    for (int k = 0; k < 4; k++) { yield return QaVe("squallLine", 1); Chk(hold[dt] == 20, $"squall line 'cut loss' {k + 1}: dates stay 20 ('{qvOut}')"); sealedTexts.Add(qvOut); }
    yield return QaVe("lighthouse", 1); Chk(hold[dt] == 20 && !qvTapOk, "lighthouse 'spare cargo' is off when only sealed cargo is aboard");
    { int c0 = coins; float hh = HullCur; string sx = PayCargoSealed(Mathf.CeilToInt(HoldUsed * S.combat.loseCargoPct), "qa defeat"); sealedTexts.Add(sx); Chk(hold[dt] == 20 && (coins < c0 || HullCur < hh), $"pirate defeat loss: dates stay 20, gold/hull pay instead ('{sx}')"); }
    { int tollable = LosableGoods().Sum(g => LosableQty(g)); Chk(tollable == 0 && SealedTotal == 20, "pirate toll: nothing tollable but the toll stays offered (paid in gold, then hull)"); }
    { int mu = PortIdx("Muscat"); bans.Add(new Ban { port = mu, good = dt, start = week - 1, end = week + 10 }); bool bn = Banned(dt, mu); yield return QaVe("customsLaunch", 0); Chk(bn && hold[dt] == 20, $"customs launch with dates banned in Muscat: sealed dates stay ({hold[dt]}; '{qvCard.Replace("\n", " ")}')"); bans.RemoveAll(b => b.good == dt && b.port == mu); }
    // the contract completes on arrival (real voyage, forced squall on the way, no gold so the hull pays)
    coins = 0; hullNow = -1; storyAmbushDone = true; sails = 20; float sMin = S.sailSecondsMin, sMax = S.sailSecondsMax; S.sailSecondsMin = S.sailSecondsMax = 8f; veForceNext = "squall"; float ec0 = S.encounterChance; S.encounterChance = 1f; Refresh(); int rn0 = eg.renown;
    StartCoroutine(Voyage(db)); yield return null; float tr = Time.time; bool shot = false; List<string> hullCard = null;
    while (sailing && Time.time - tr < 60f) { if (dialog != null) { if (!shot && qvCardText().Contains(seal0)) { shot = true; hullCard = dialog.GetComponentsInChildren<Text>().Select(t => t.text).ToList(); sealedTexts.Add(qvCardText()); yield return ShotCo(Path.Combine(dir, "sealed_cargo.png")); } EgTestCloseCard(); } yield return null; }
    yield return new WaitForSeconds(0.5f); S.sailSecondsMin = sMin; S.sailSecondsMax = sMax; veForceNext = ""; S.encounterChance = ec0;
    string want = "A black squall heels the dhow over. Your sealed dates stay lashed below. The hull takes the rest.";
    Chk(hullCard != null && hullCard.Contains(want), $"sealed squall, no gold (hull pays): card text is exactly '{want}' (got '{(hullCard != null ? string.Join(" | ", hullCard) : "no card")}')");
    { int Lash(string t) { int n = 0, i = 0; t = t.ToLower(); while ((i = t.IndexOf("lash", i)) >= 0) { n++; i += 4; } return n; } var sealedOnes = sealedTexts.Where(t => t != null && t.Contains(seal0)).ToList();
      Chk(sealedOnes.Count >= 6 && sealedOnes.All(t => Lash(t) == 1), $"'lash' appears exactly once in every sealed-cargo outcome text ({sealedOnes.Count} texts; counts {string.Join(",", sealedOnes.Select(Lash))})"); foreach (var t in sealedOnes) L("sealed text: " + t.Replace("\n", " ")); }
    Chk(port == db && c.status == "done" && hold[dt] == 0 && eg.renown == rn0 + c.renown, $"arrival at Debal with 20 sealed dates: grand contract {c.status}, +{eg.renown - rn0} Renown, coins {coins} (sealed card shot {shot})");
    while (dialog != null) { EgTestCloseCard(); yield return null; } egCards.Clear(); rvCards.Clear(); }
  string qvCardText() => dialog ? string.Join(" | ", dialog.GetComponentsInChildren<Text>().Select(t => t.text)) : "";

  IEnumerator V07SeaVoyages(Action<bool, string> Chk, Action<string> L, string dir) {
    IEnumerator Shot2(string n) { tab = -1; Refresh(); yield return null; yield return null; yield return ShotCo(Path.Combine(dir, n + ".png")); yield return new WaitForSeconds(0.3f); }
    float sMin = S.sailSecondsMin, sMax = S.sailSecondsMax, enc = S.encounterChance; bool ffl = S.debugFast; S.sailSecondsMin = S.sailSecondsMax = 14f; S.encounterChance = 0f; S.debugFast = false;
    int gw = PortIdx("Gwadar"), db = PortIdx("Debal"), mu = PortIdx("Muscat"); string tk = RivalData.Line("ticker"); int Count(string s) { int n = 0, k = 0; while (s != null && (k = s.IndexOf(tk, k, StringComparison.Ordinal)) >= 0) { n++; k += tk.Length; } return n; }
    storyStep = StDone; storyAmbushDone = true; sails = 20; safeUntilSail = 0; veForce = veForceNext = ""; week = 9; port = gw; tab = -1; dialog = null; eg.active.Clear();
    // voyage 1: Gwadar -> Debal; he is bound for Debal (on this leg) and forced in port there
    Rv.at = -1; Rv.next = db; Rv.arriveWeek = week + 1; rdForceNext = true; rdNoRoll = false; Refresh(); yield return null;
    int thr = Mathf.RoundToInt(RivalData.F("qaSeaRedPixels", 120)), pre = 0, peak = 0; bool shown = false, shot = false; float runStart = -1, lastRed = -1, best = 0, showT = -1; var tr = Time.time;
    StartCoroutine(Voyage(db)); yield return null; Chk(sailing && rdSeaPending, "voyage 1 (Gwadar -> Debal, he is on this leg): Red Dhow scheduled at sea");
    while (sailing && Time.time - tr < 60f) {
      if (dialog != null) EgTestCloseCard();
      yield return new WaitForEndOfFrame(); var t = ScreenCapture.CaptureScreenshotAsTexture(); int red = RedFramePixels(t); bool vis = redDhow && redDhow.gameObject.activeSelf;
      if (vis && !shown) { shown = true; showT = Time.time; } if (!shown) pre = Mathf.Max(pre, red); if (vis) peak = Mathf.Max(peak, red);
      if (vis && red >= thr) { if (runStart < 0 || Time.time - lastRed > 0.3f) runStart = Time.time; lastRed = Time.time; best = Mathf.Max(best, lastRed - runStart); }
      if (vis && !shot && red >= thr && Time.time - showT > 1.5f) { shot = true; File.WriteAllBytes(Path.Combine(dir, "sea_red_dhow.png"), t.EncodeToPNG()); L($"sea frame saved: {red} red px (of every 4th), cam {cam.name} at {cam.transform.position}"); }
      Destroy(t); }
    yield return new WaitForSeconds(0.5f);
    Chk(shown && pre < thr, $"before he shows, the gameplay frame has no red-sail pixels ({pre} < {thr}): the check can fail");
    Chk(best >= 3f, $"Red Dhow visible at sea: red-sail pixels in the gameplay camera frame for {best:0.0} s continuous (want >= 3 s; peak {peak} px, threshold {thr})");
    bool OtherPort(string m) => S.ports.Any(pp => pp.name != S.ports[port].name && m.Contains(pp.name));
    Chk(port == db && !OtherPort(msg) && !msg.Contains("called at"), $"dock message names only the port you docked at (no older news line from another port): '{msg}'"); yield return Shot2("dock_message_debal");
    Chk(port == db && RivalHere && Count(msg) == 1, $"voyage 1 arrival at Debal with the Red Dhow in port: ticker '{tk}' shown {Count(msg)}x (want 1) in '{msg}'");
    if (content && content.Find("TownTitle")) { var tx = content.Find("TownTitle").GetComponentInChildren<Text>(); Chk(tx && tx.text == S.ports[port].name.ToUpper(), $"after a real arrival the town header '{tx?.text}' matches the port {S.ports[port].name}"); }
    while (dialog != null) { EgTestCloseCard(); yield return null; } rvCards.Clear();
    // voyage 2: Debal -> Gwadar; no roll, he is not bound there: no ticker at all (the last ticker must not leak into the next arrival)
    rdForceNext = false; rdNoRoll = true; Rv.next = mu; Rv.arriveWeek = week + 5; tr = Time.time; StartCoroutine(Voyage(gw)); yield return null;
    while (sailing && Time.time - tr < 60f) { if (dialog != null) EgTestCloseCard(); yield return null; } yield return new WaitForSeconds(0.5f);
    Chk(port == gw && !RivalHere && Count(msg) == 0 && !msg.Contains("Red Dhow") && !OtherPort(msg) && (news == null || news.Count == 0 || Count(news[0]) == 0), $"voyage 2 arrival at Gwadar after Debal-with-Rashid: no Red Dhow line, no other port's news in the dock message '{msg}'"); yield return Shot2("dock_message_gwadar");
    while (dialog != null) { EgTestCloseCard(); yield return null; } rvCards.Clear(); rdNoRoll = false;
    S.sailSecondsMin = sMin; S.sailSecondsMax = sMax; S.encounterChance = enc; S.debugFast = ffl; }

  IEnumerator V07SelfTest(bool standalone = true) {
    int fails = 0, checks = 0; void L(string s) => Debug.Log("GB V07TEST " + s); void Chk(bool ok, string what) { checks++; if (!ok) fails++; L((ok ? "PASS " : "FAIL ") + what); }
    if (!SaveDirIsolated) { L("REFUSED: needs -gbsavedir"); Application.Quit(4); yield break; }
    string dir = Path.Combine(SaveDir, "v07test"); Directory.CreateDirectory(dir); IEnumerator Shot(string n) { yield return null; yield return null; HeaderCheck(n); yield return ShotCo(Path.Combine(dir, n + ".png")); yield return new WaitForSeconds(0.3f); }
    void HeaderCheck(string n) { var tt = content ? content.Find("TownTitle") : null; var tx = tt ? tt.GetComponentInChildren<Text>() : null; if (tx) Chk(tx.text == S.ports[port].name.ToUpper(), $"shot {n}: town header '{tx.text}' matches the current port {S.ports[port].name}"); } /* gamedev_v07: the qa used to move `port` without redrawing the town (stale MUSCAT header) */
    bool flagN0 = rdForceNext, flagR0 = rdForceRace, flagG0 = rdForceGrand, flagB0 = rdBoardForce; rdForceNext = rdForceRace = rdForceGrand = false;
    void Fresh() { Init(); egTestQuiet = true; storyStep = StDone; coins = 8000; port = PortIdx("Gwadar"); week = 0; tab = -1; rvCards.Clear(); egCards.Clear(); rdForceNext = rdForceRace = rdForceGrand = rdNoRoll = false; rdRaceSticky = -1; Refresh(); }
    if (standalone) yield return new WaitForSeconds(1f); onTitle = false; Fresh(); Refresh(); yield return null;
    int gw = PortIdx("Gwadar"), db = PortIdx("Debal"), mu = PortIdx("Muscat");

    // ---- data: Game Designer's verbatim lines + portrait moods ----
    var want = new Dictionary<string, (string text, int mood)> { { "inPort", ("Your hold smells of my profits, friend.", 3) }, { "taunt1", ("The Red Dhow was here first. As always.", 0) }, { "taunt2", ("I left you the scraps. Generous, no?", 1) }, { "taunt3", ("Following me? Wise. Late, but wise.", 3) },
      { "raceWin", ("The sea favours the swift. Try again, little dhow.", 1) }, { "raceLoss", ("Hm. Well sailed. Enjoy it while it lasts.", 2) }, { "rumour", ("They say the Red Dhow makes for {port}.", 3) }, { "ticker", ("The Red Dhow is in port.", 0) } };
    foreach (var kv in want) Chk(RivalData.Line(kv.Key) == kv.Value.text && (kv.Key == "ticker" || RivalData.Mood(kv.Key) == kv.Value.mood), $"line '{kv.Key}' = \"{RivalData.Line(kv.Key)}\" mood k7_{RivalData.Mood(kv.Key)} (want k7_{kv.Value.mood})");
    Chk(Enumerable.Range(0, 4).All(m => RivalPortrait(m) && RivalPortrait(m).name == "k7_" + m), "Rashid portraits Keepers/k7_0..3 load (" + string.Join(",", Enumerable.Range(0, 4).Select(m => RivalPortrait(m) ? RivalPortrait(m).name : "missing")) + ")");
    Chk(RivalData.Name == "Captain Rashid" && RivalData.FromMoon == 3 && Mathf.Abs(RivalData.PortChance - 0.25f) < 1e-4f, $"rival data: {RivalData.Name}, from moon {RivalData.FromMoon}, port chance {RivalData.PortChance}");

    // ---- debug flags (env var form; the -gb args go through the same FlagOn) ----
    foreach (var (env, nm) in new[] { ("GB_REDDHOW_NEXT", "next"), ("GB_RACE_CONTRACT", "race"), ("GB_ENDGAME_CONTRACT", "grand") }) { Environment.SetEnvironmentVariable(env, "1"); RivalReadFlags(); bool on = nm == "next" ? rdForceNext : nm == "race" ? rdForceRace : rdForceGrand; Environment.SetEnvironmentVariable(env, null); RivalReadFlags(); bool off = !(nm == "next" ? rdForceNext : nm == "race" ? rdForceRace : rdForceGrand); Chk(on && off, $"debug flag {env} turns on {nm} (and off when unset)"); }
    rdForceNext = rdForceRace = rdForceGrand = rdBoardForce = false;

    // ---- rival: moon gate, roll rate, in port ----
    { int s0 = Rv.sightings; for (int i = 0; i < 40; i++) RivalOnDock(); Chk(!RivalOn && Rv.sightings == s0, $"moon 1: no Red Dhow in 40 arrivals (RivalOn {RivalOn})"); }
    rdForceNext = true; RivalOnDock(); Chk(RivalHere && Rv.sightings == 1, "-gbredDhowNext: the Red Dhow is in the next port even before moon 3"); Fresh();
    week = (RivalData.FromMoon - 1) * Wk; Chk(RivalOn && Moon == 2, $"moon 3 (week {week}): rival active");
    { int hit = 0, N = 4000; for (int i = 0; i < N; i++) if (RivalRollArrival()) hit++; float f = hit / (float)N; Chk(Mathf.Abs(f - 0.25f) < 0.03f, $"arrival roll: {hit}/{N} = {f:P1} (want ~1 in 4)"); }
    { int g = RivalCheapestGood(port); int s0 = stock[port, g]; float p0 = price[port, g]; msg = "Docked at Gwadar."; rdForceNext = true; RivalOnDock();
      Chk(RivalHere && g >= 0 && stock[port, g] == Mathf.FloorToInt(s0 * 0.5f) && Mathf.Abs(price[port, g] - p0 * 1.2f) < 0.01f, $"in port: bought the cheapest good {S.goods[g].id}: stock {s0}->{stock[port, g]}, price {p0:0.0}->{price[port, g]:0.0} (+20%)");
      Chk(msg.Contains("The Red Dhow is in port.") && news[0].StartsWith("The Red Dhow called at Gwadar"), $"dock ticker: '{msg}' / news '{news[0]}'");
      Chk(Rv.next >= 0 && Rv.next != port && ED.BoardPorts.Contains(S.ports[Rv.next].name), $"he heads next for {(Rv.next >= 0 ? S.ports[Rv.next].name : "-")}");
      var mc = rvCards.LastOrDefault(); Chk(mc.b == RivalData.Line("taunt1") && mc.mood == 0, $"meeting card: taunt1 with k7_{mc.mood}");
      tab = 8; Refresh(); yield return null; yield return null; var ip = content.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.gameObject.name == "RivalInPort"); var rr = content.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.gameObject.name == "RivalRumour");
      var ipFace = content.GetComponentsInChildren<RawImage>(true).FirstOrDefault(x => x.gameObject.name.StartsWith("RivalPortrait inPort")); var rrFace = content.GetComponentsInChildren<RawImage>(true).FirstOrDefault(x => x.gameObject.name.StartsWith("RivalPortrait rumour"));
      Chk(ip && ip.text.Contains(want["inPort"].text) && ipFace && ipFace.texture && ipFace.texture.name == "k7_3", $"tavern in-port line with suspicious portrait ({(ipFace && ipFace.texture ? ipFace.texture.name : "none")})");
      string rumour = want["rumour"].text.Replace("{port}", S.ports[Rv.next].name); Chk(rr && rr.text.Contains(rumour) && rrFace && rrFace.texture && rrFace.texture.name == "k7_3", $"tavern rumour '{rumour}' with suspicious portrait ({(rrFace && rrFace.texture ? rrFace.texture.name : "none")})");
      yield return Shot("tavern_rashid_in_port");
      float p1 = price[port, g]; week++; RivalNewWeek(); Chk(Mathf.Abs(price[port, g] - p1 / 1.2f) < 0.01f && Rv.boughtGood < 0, $"visit over: {S.goods[g].id} price back {p1:0.0}->{price[port, g]:0.0}");
      Refresh(); yield return null; yield return null; Chk(!content.GetComponentsInChildren<Text>(true).Any(t => t.gameObject.name == "RivalInPort") && content.GetComponentsInChildren<Text>(true).Any(t => t.gameObject.name == "RivalRumour"), "next week: no in-port line, the rumour is still there (always a rumour)");
      yield return Shot("tavern_rashid_rumour"); tab = -1; Refresh(); }
    { int nx = Rv.next; port = nx; week = Mathf.Max(week, Rv.arriveWeek); int s0 = Rv.sightings; Rv.metWeek = -100; RivalOnDock(); Chk(Rv.sightings == s0 + 1 && RivalHere && port == nx, $"rumour is true: docking at {S.ports[nx].name} from week {Rv.arriveWeek} finds him there"); }
    { int x = Rv.next; Rv.arriveWeek = week; int st = Mathf.RoundToInt(RivalData.F("staysWeeks", 2)); for (int i = 0; i <= st; i++) { week++; RivalNewWeek(); } Chk(Rv.at == x && Rv.next != x && Rv.next >= 0, $"unvisited, he moves on after {st} weeks: at {S.ports[x].name}, now heading for {S.ports[Mathf.Max(0, Rv.next)].name}"); }
    { var r = Rv; int a = r.at, n = r.next, aw = r.arriveWeek, si = r.sightings, mp = r.metPort, mw = r.metWeek; Save(); eg = new EgState(); Load(); var q = Rv; Chk(q.at == a && q.next == n && q.arriveWeek == aw && q.sightings == si && q.metPort == mp && q.metWeek == mw, $"save/load rival: at {q.at} next {q.next} arrive {q.arriveWeek} sightings {q.sightings} met {q.metPort}@{q.metWeek}"); }
    // ---- portrait cards ----
    IEnumerator CardCheck(string key, string title, string body, string shot) { tab = -1; Refresh(); /* the test moves `port` directly: redraw the town (stale header root cause) */ int m = RivalData.Mood(key); var co = StartCoroutine(RivalCard(title, body, m)); yield return null; yield return null; var d = dialog; var tx = d ? d.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.gameObject.name == "RivalLine") : null;
      Chk(d && d.name == "RivalCard" && tx && tx.text.Contains(want[key].text) && FaceIn(d) == "k7_" + want[key].mood, $"card '{key}': text shown, portrait {FaceIn(d)} (want k7_{want[key].mood})"); if (shot != null) yield return Shot(shot); var b = d ? FindBtn(d, "Continue") : null; if (b) b.onClick.Invoke(); yield return null; yield return null; }
    yield return CardCheck("taunt1", RivalData.Name, RivalData.Line("taunt1"), "card_taunt1");
    yield return CardCheck("taunt2", RivalData.Name, RivalData.Line("taunt2"), null);
    // ---- the Red Dhow at sea: red sail, no emission; pirates have no red on the sail ----
    { var m = RedSailMaterial(); var tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null; float rf = 0; if (tex) { var px = ReadTexPixels(tex, out int w, out int h); rf = px.Count(ShipArt.IsRed) / (float)px.Length; }
      Chk(!m.IsKeywordEnabled("_EMISSION") && (!m.HasProperty("_EmissionColor") || m.GetColor("_EmissionColor").maxColorComponent < 0.001f) && Mathf.Abs(m.GetFloat("_Smoothness") - 0.1f) < 1e-3f && m.GetFloat("_Cull") == 0f, $"Red Dhow sail material: Lit ({m.shader.name}), no emission, smoothness {m.GetFloat("_Smoothness"):0.00}, double-sided");
      var bc = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.clear; var wantBc = UITheme.C("#C24A38"); Chk(tex && tex.name == "red_sail_canvas" && rf > 0.8f && Mathf.Abs(bc.r - wantBc.r) + Mathf.Abs(bc.g - wantBc.g) + Mathf.Abs(bc.b - wantBc.b) < 0.01f && !m.shaderKeywords.Any(k => k.ToLower().Contains("rim")), $"Red Dhow sail: red_sail_canvas ({rf:P0} red) x base colour #{ColorUtility.ToHtmlStringRGB(bc)} (want C24A38), no rim keyword");
      var d = BuildRedDhow(); var rl = d ? d.GetComponentsInChildren<Renderer>(true).Where(x => x.gameObject.name == "RedLateen").ToList() : new List<Renderer>(); Chk(rl.Count > 0 && rl.All(x => x.sharedMaterial == m), $"Red Dhow built with {rl.Count} red sail renderer(s)");
      var pm = Resources.Load<Material>("Mats/ShipPirate"); var pt = pm ? pm.GetTexture("_BaseMap") : null; if (pt) { var px = ReadTexPixels(pt, out int w, out int h); float sailRed = RedFrac(px, w, h, ShipArt.SAIL, ShipArt.N), penRed = RedFrac(px, w, h, new RectInt(ShipArt.PENNANT.x, ShipArt.PENNANT.y, ShipArt.PENNANT.width, ShipArt.PENNANT.height / 2), ShipArt.N);
        Chk(sailRed < 0.005f, $"pirate sail (canvas, stripe, skull ring) has no red: {sailRed:P2} red pixels in the SAIL cell");
        Chk(penRed > 0.5f, $"pirate pennant keeps its red (allowed on the pennant only): {penRed:P0} of its lower half"); } else Chk(false, "pirate atlas missing");
      Rv.next = db; RivalSeaStart(db, 10f); bool on = rdSeaPending; Rv.next = mu; rdSeaPending = false; RivalSeaStart(db, 10f); bool off = !rdSeaPending; rdSeaPending = false; Chk(on && off, "at sea: the Red Dhow shows on the leg to his next port, not on others");
      }
    yield return V07SeaVoyages(Chk, L, dir); /* real voyages: the Red Dhow in the gameplay camera frame for >= 3 s, the ticker once per arrival */

    // ---- race contracts ----
    Fresh(); week = 2 * Wk; AddRenown(ED.RankRenown(1), "test"); egCards.Clear(); EgRefreshBoards(true); foreach (var b in eg.board) b.race = false; RivalMarkRace(true);
    var race = eg.board.FirstOrDefault(b => b.race); Chk(race != null && race.from == "Gwadar" && eg.board.Count(b => b.race) == 1, $"race contract posted at Gwadar: {race?.title} -> {race?.to}");
    { var co = StartCoroutine(EgBoardCo()); yield return null; yield return null; var bd = dialog; var rows = bd ? bd.GetComponentsInChildren<Button>(true).Where(b => b.GetComponentsInChildren<Text>(true).Any(t => t.gameObject.name == "RaceRow" || t.text.Contains(" · "))).ToList() : new List<Button>();
      var raceRow = rows.FirstOrDefault(b => b.GetComponentsInChildren<Text>(true).Any(t => t.gameObject.name == "RaceRow")); var icon = raceRow ? raceRow.GetComponentsInChildren<Image>(true).FirstOrDefault(i => i.gameObject.name == "RaceIcon") : null; var rowTxt = raceRow ? raceRow.GetComponentsInChildren<Text>(true).First(t => t.gameObject.name == "RaceRow") : null;
      float ipx = ScreenPx(icon), sf = scaler ? scaler.GetComponent<Canvas>().scaleFactor : 1f, cap = rowTxt ? rowTxt.fontSize * sf * 0.7f : 999f;
      Chk(icon && icon.sprite && icon.sprite.name.StartsWith("rival_sail_icon") && icon.preserveAspect && ipx >= RivalData.ArtF("boardIconMinPx", 40) - 0.5f && ipx >= cap && rowTxt && icon.rectTransform.anchoredPosition.x + icon.rectTransform.rect.width <= rowTxt.rectTransform.anchoredPosition.x,
        $"board: red-sail icon ({(icon && icon.sprite ? icon.sprite.name : "none")}) measured {ipx:0.#} screen px in a {Screen.width}x{Screen.height} window (canvas scale {sf:0.###}, {icon?.rectTransform.rect.width:0} canvas units; want >= {RivalData.ArtF("boardIconMinPx", 40)} px and >= the row text cap height {cap:0.#} px), left of the race notice"); L($"ICONPX board {ipx:0.#} px, screen dpi {Screen.dpi:0}, canvas scale {sf:0.###}");
      Chk(rows.Count(b => b.GetComponentsInChildren<Image>(true).Any(i => i.gameObject.name == "RaceIcon")) == 1, $"only the race notice has the icon ({rows.Count} rows)"); yield return Shot("board_race_icon");
      var close = FindBtn(bd, "Close"); if (close) close.onClick.Invoke(); yield return null; yield return null; }
    { var dm = ModalDim("EgDim"); var cc = EgCard(race, () => { }, () => { }); yield return null; var ic = cc.GetComponentsInChildren<Image>(true).FirstOrDefault(i => i.gameObject.name == "RaceIconCard"); var tag = cc.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.gameObject.name == "RaceTag");
      var ttl = ic ? ic.transform.parent.GetComponent<Text>() : null; Chk(ic && ic.sprite && ttl && ttl.text == race.title && ic.rectTransform.anchoredPosition.x + 15 <= -Mathf.Min(ttl.preferredWidth, ttl.rectTransform.rect.width) / 2 - 5 && tag && tag.text == RivalData.S("raceTag"), $"race card: icon left of the title, tag '{(tag ? tag.text : "")}'");
      { var rw = cc.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.gameObject.name == "RaceWindow"); int trip = RaceTripWeeks(race); float smn = 99, smx = -1; for (int k = 0; k < 4000; k++) { float rt = RivalRaceTime(trip); smn = Mathf.Min(smn, rt); smx = Mathf.Max(smx, rt); } int lo = Mathf.RoundToInt(smn), hi = Mathf.RoundToInt(smx); /* the code's actual min/max, sampled */ var w = RaceWindow(race); float cpx = ScreenPx(ic), ttl2 = ttl ? ttl.fontSize * (scaler ? scaler.GetComponent<Canvas>().scaleFactor : 1f) * 0.7f : 999;
        var acc = FindBtn(cc, ED.S("accept", "Accept")); bool clear = rw && acc && ScreenRect(rw).yMin > ScreenRect(acc.GetComponent<Graphic>()).yMax; Chk(clear, "race window line sits above the Accept button (not hidden behind it)");
        Chk(rw && w.lo == lo && w.hi == hi && rw.text == $"Red Dhow: {lo}–{hi} weeks", $"race card window line '{rw?.text}' (trip {trip} week(s); RivalRaceTime sampled {smn:0.00}..{smx:0.00} weeks; want {trip - 1}–{trip + 2})"); Chk(trip == 1 && lo == 0 && hi == 3, $"carrying (1-week trip): card and code both 0–3 weeks (card {w.lo}–{w.hi}, code {smn:0.00}..{smx:0.00})");
        int h0 = hold[race.good]; var sx = stock[port, race.good]; hold[race.good] = 0; stock[port, race.good] = 0; var w2 = RaceWindow(race); int t2 = RaceTripWeeks(race); float m2 = 99, x2 = -1; for (int k = 0; k < 4000; k++) { float rt = RivalRaceTime(t2); m2 = Mathf.Min(m2, rt); x2 = Mathf.Max(x2, rt); } hold[race.good] = h0; stock[port, race.good] = sx;
        Chk(t2 == 2 && w2.lo == 1 && w2.hi == 4 && Mathf.RoundToInt(m2) == w2.lo && Mathf.RoundToInt(x2) == w2.hi, $"buying first (2-week trip): card {w2.lo}–{w2.hi} weeks = code {m2:0.00}..{x2:0.00} (want 1–4)");
        Chk(cpx >= RivalData.ArtF("boardIconMinPx", 40) - 0.5f && cpx >= ttl2, $"race card icon measured {cpx:0.#} screen px (title cap height {ttl2:0.#} px)"); L($"ICONPX card {cpx:0.#} px"); }
      yield return Shot("contract_card_race"); Destroy0(cc.gameObject); Destroy0(dm.gameObject); dialog = null; yield return null; }
    { coins = 8000; int r0 = eg.renown; int ti0 = Rv.tauntIdx; Chk(EgAccept(race) && race.rivalAt >= week && race.rivalAt <= week + 3, $"race accepted: Rashid due week {race.rivalAt:0.00} (now {week})");
      var tc = rvCards.LastOrDefault(); string tk = "taunt" + (ti0 % 3 + 1); Chk(tc.b.Contains(want[tk].text) && tc.mood == want[tk].mood && Rv.tauntIdx == ti0 + 1, $"accepting a race: next taunt in the cycle ({tk}) with k7_{tc.mood}"); yield return CardCheck(tk, tc.t, tc.b, null); rvCards.Clear();
      race.rivalAt = week + 1; hold[race.good] += race.qty; NewWeek(); Chk(race.status == "active", "tie week: Rashid due this week, the race is still open when you dock (tie goes to the player)");
      port = PortIdx(race.to); int c1 = coins; EgOnDock(); Chk(race.status == "done" && coins == c1 + race.pay + race.deposit && Rv.raceLosses == 1, $"race WON: full pay {race.pay} + deposit {race.deposit}");
      var wc = rvCards.LastOrDefault(); Chk(wc.b.Contains(want["raceLoss"].text) && wc.mood == 2, $"he loses the race: '{wc.b}' with k7_{wc.mood}"); yield return CardCheck("raceLoss", wc.t, wc.b, "card_rashid_loses"); rvCards.Clear(); }
    { EgRefreshBoards(true); foreach (var b in eg.board) b.race = false; RivalMarkRace(true); var r2 = eg.board.FirstOrDefault(b => b.race); if (r2 == null) { EgRefreshBoards(true); RivalMarkRace(true); r2 = eg.board.FirstOrDefault(b => b.race); }
      port = PortIdx(r2.from); coins = 8000; EgAccept(r2); rvCards.Clear(); r2.rivalAt = week + 1.5f; Save(); eg = new EgState(); Load(); var r2l = eg.active.FirstOrDefault(c => c.id == r2.id); Chk(r2l != null && r2l.race && Mathf.Abs(r2l.rivalAt - r2.rivalAt) < 1e-4f, $"save/load race: {r2l?.id} race={r2l?.race} rivalAt {r2l?.rivalAt:0.00}"); r2 = r2l;
      int c0 = coins, rn = eg.renown; NewWeek(); Chk(r2.status == "active", "week 1: still racing"); NewWeek();
      Chk(r2.status == "taken" && !eg.active.Contains(r2) && coins == c0 + r2.deposit && eg.renown == rn && Rv.raceWins == 1, $"race LOST: '{r2.status}', deposit {r2.deposit} returned, renown {rn}->{eg.renown}");
      var lc = rvCards.LastOrDefault(); Chk(lc.t == RivalData.S("raceLostTitle") && lc.b.Contains(want["raceWin"].text) && lc.mood == 1, $"he wins the race: '{lc.t}' with k7_{lc.mood}"); yield return CardCheck("raceWin", lc.t, lc.b, "card_rashid_wins"); rvCards.Clear(); }
    { var rc = new EgContract { id = "sim", race = true, good = G("copper"), qty = 5, to = "Muscat", status = "active" }; port = gw; int N = 1000;
      foreach (var cas in new[] { "carrying (delivery leg only)", "source then deliver" }) { bool carry = cas.StartsWith("carrying"); hold[rc.good] = carry ? 5 : 0; int st0 = stock[gw, rc.good]; if (!carry) stock[gw, rc.good] = 0;
        int trip = RaceTripWeeks(rc), src = RaceSourcePort(rc); int wins = 0; float mn = 99, mx = -1; for (int i = 0; i < N; i++) { float t = RivalRaceTime(trip); mn = Mathf.Min(mn, t); mx = Mathf.Max(mx, t); if (!(trip > t)) wins++; } float wr = wins / (float)N; stock[gw, rc.good] = st0;
        Chk(trip == (carry ? 1 : 2) && wr >= 0.60f && wr <= 0.74f && mn >= trip - 1 - 1e-4f && mx <= trip + 2 + 1e-4f, $"race timing, {cas}: player's shortest trip {trip} week(s){(carry ? "" : $" via {(src >= 0 ? S.ports[src].name : "?")}")}, Rashid = trip + U({RivalData.F("raceSpreadMinWeeks", 0)}, +{RivalData.F("raceSpreadMaxWeeks", 0)}) weeks = {mn:0.00}..{mx:0.00}; player sailing that trip wins {wins}/{N} = {wr:P1} (want 60-74%)"); }
      hold[rc.good] = 0; Chk(RaceTripWeeks(rc) == 1, "not carrying but on sale where you accept: trip = the delivery leg (1 week)"); }
    { var r = Rv; r.tauntIdx = 0; var got = new List<(string, int)>(); for (int i = 0; i < 4; i++) { if (i == 2) { Save(); eg = new EgState(); Load(); } rvCards.Clear(); Rv.metWeek = -100; rdForceNext = true; RivalOnDock(); var c = rvCards.LastOrDefault(); got.Add((c.b, c.mood)); week++; }
      bool cyc = got.Count == 4 && got[0] == (want["taunt1"].text, 0) && got[1] == (want["taunt2"].text, 1) && got[2] == (want["taunt3"].text, 3) && got[3] == (want["taunt1"].text, 0);
      Chk(cyc && Rv.tauntIdx == 4, $"taunts cycle taunt1 -> taunt2 -> taunt3 -> taunt1 (moods {string.Join("/", got.Select(g => "k7_" + g.Item2))}), kept across save/load (idx {Rv.tauntIdx})"); rvCards.Clear(); }

    // ---- grand contracts ----
    Fresh(); { int f0 = fame; AddRenown(ED.RankRenown(2) - 1, "test"); Chk(eg.rank == 1 && Big == null, $"at {eg.renown} Renown (Trader): no grand contract");
      AddRenown(1, "test"); var b1 = Big; Chk(eg.rank == 2 && b1 != null && b1.status == "offer" && eg.bigStage == 0, $"Merchant ({eg.renown}): the grand sequence starts, offer 1 = {b1?.title}");
      var seq = GrandSeq; Chk(seq.Count == 3 && seq.Select(d => (string)d["dest"]).Distinct().Count() == 3, "sequence of 3, each to a different port: " + string.Join(", ", seq.Select(d => $"{d["good"]} x{d["qty"]} -> {d["dest"]}")));
      Chk((string)seq[0]["good"] == "dates" && (string)seq[0]["dest"] == "Debal" && (string)seq[1]["good"] == "copper" && (string)seq[1]["dest"] == "Muscat" && (string)seq[2]["good"] == "indigo" && (string)seq[2]["dest"] == "Gwadar" && seq.All(d => Design.F(d["qty"]) == 20), "ruling: Dates x20 -> Debal, Copper x20 -> Muscat, Indigo x20 -> Gwadar");
      Chk(seq.All(d => Design.F(d["qty"]) <= baseHold) && shipTier[3] == 0, $"x20 fits the base hold ({baseHold}) - no Hold upgrade needed");
      int exp = Mathf.Min(GrandCap, Mathf.RoundToInt(20 * GrandNormalPrice(G("dates"), db) * GrandPriceMult) + GrandBonus); Chk(b1.pay == exp && b1.renown == 3 * ED.RenownDelivery && b1.moons == 2 && b1.deposit == 0 && fame == f0, $"offer 1: pay {b1.pay} = 20 x Debal normal price {GrandNormalPrice(G("dates"), db):0.0} + {GrandBonus} (cap {GrandCap}), Renown {b1.renown} (3x {ED.RenownDelivery}), 2 moons, no deposit, fame unchanged {fame}");
      Chk(GrandCap > ED.CapDelivery && GrandCap > ED.CapRush, $"own cap {GrandCap} (not the {ED.CapDelivery}/{ED.CapRush} caps)"); }
    { EgRefreshBoards(true); var two = eg.board.Take(2).ToList(); coins = 8000; foreach (var b in two) EgAccept(b); Chk(eg.active.Count == 2, "2 normal contracts held");
      var co = StartCoroutine(EgBoardCo()); yield return null; yield return null; var bd = dialog; var gr = bd ? bd.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.gameObject.name == "GrandRow") : null; Chk(gr != null && gr.text.Contains("1 of 3"), $"board lists the grand contract: '{(gr ? gr.text : "")}'"); yield return Shot("board_grand"); var close = FindBtn(bd, "Close"); if (close) close.onClick.Invoke(); yield return null; yield return null;
      var dm = ModalDim("EgDim"); var cc = EgCard(Big, () => { }, () => { }); yield return null; var acc = cc.GetComponentsInChildren<Button>(true).First(); var gt = cc.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.gameObject.name == "GrandTag");
      Chk(acc.interactable && gt != null && !cc.GetComponentsInChildren<Text>(true).Any(t => t.text == ED.Full), $"grand card: Accept enabled with 2 contracts held; '{(gt ? gt.text.Replace("\n", " / ") : "")}'"); yield return Shot("contract_card_grand"); Destroy0(cc.gameObject); Destroy0(dm.gameObject); dialog = null; yield return null;
      var big = Big; Chk(EgAccept(big) && big.status == "active" && eg.active.Count == 2 && big.dueWeek == week + 2 * Wk - 1, $"grand accepted in its own slot: normal {eg.active.Count}/{ED.MaxActive}, due week {big.dueWeek}");
      EgRefreshBoards(true); var third = eg.board.First(); Chk(!EgAccept(third) && msg == ED.Full, "a 3rd normal contract is still refused (grand doesn't use a normal slot)");
      foreach (var a in eg.active) a.dueWeek = big.dueWeek + 4; /* the grand one is the most urgent */ var hud = JournalGoalsSummary(); Chk(hud.Item2.Contains($"{S.goods[big.good].name} to {big.to}") && hud.Item2.Contains(EgMoonsColor(big)), $"HUD Next shows the grand contract in its deadline colour: {hud.Item2}");
      tab = -1; Refresh(); yield return Shot("hud_next_grand"); eg.active.Clear();
      int rn = eg.renown, f0 = fame; for (int i = 0; i < 2 * Wk; i++) NewWeek(); var nb = Big; Chk(big.status == "failed" && nb != null && nb.status == "offer" && nb.tpl == "eg_big1" && eg.bigStage == 0 && eg.bigFails == 1 && eg.renown == Mathf.Max(ED.RankRenown(2), rn + GrandFailRenown) && fame == f0, $"grand FAILED after 2 moons: same contract re-posted ({nb?.tpl}), stage {eg.bigStage + 1}, renown {rn}->{eg.renown} (floor {ED.RankRenown(2)}), fame unchanged"); }
    { int stages = 0; foreach (var (good, dest) in new[] { ("dates", "Debal"), ("copper", "Muscat"), ("indigo", "Gwadar") }) { var c = Big; Chk(c != null && c.status == "offer" && S.goods[c.good].id == good && c.to == dest && c.qty == 20, $"stage {stages + 1}: {c?.qty}x{(c != null ? S.goods[c.good].id : "?")} -> {c?.to}");
        if (c != null) { var pc = Design.D(GS.TryGetValue("payCheck", out var po) ? po : null); var ex = (pc.TryGetValue("expected", out var eo) ? eo : null) as List<object> ?? new List<object>(); float tol = pc.TryGetValue("tolerance", out var to) ? Design.F(to, 0.15f) : 0.15f; int e = stages < ex.Count ? Mathf.RoundToInt(Design.F(ex[stages], 0)) : 0;
          Chk(e > 0 && Mathf.Abs(c.pay - e) <= e * tol && c.pay < GrandCap * 0.75f && c.pay == Mathf.Min(GrandCap, Mathf.RoundToInt(20 * GrandNormalPrice(c.good, PortIdx(dest)) * GrandPriceMult) + GrandBonus), $"stage {stages + 1} pay {c.pay} = 20 x {dest} normal {good} price {GrandNormalPrice(c.good, PortIdx(dest)):0.0} x {GrandPriceMult:0.##} + {GrandBonus}; data expects {e} ±{tol:P0} (endgame_data.json payCheck), well under the {GrandCap} cap"); }
        if (c == null) break; port = PortIdx(dest) == gw ? db : gw; EgAccept(c); if (stages == 1) { int dw = c.dueWeek; Save(); eg = new EgState(); Load(); c = Big; Chk(c != null && c.status == "active" && c.tpl == "eg_big2" && c.dueWeek == dw && eg.bigStage == 1, $"save/load mid-sequence: stage {eg.bigStage + 1}, {c?.tpl} active, due {c?.dueWeek}"); }
        NewWeek(); hold[c.good] = 20; port = PortIdx(dest); int c0 = coins, r0 = eg.renown, f0 = fame; EgOnDock(); stages++;
        Chk(c.status == "done" && coins == c0 + c.pay && eg.renown == r0 + c.renown && fame == f0 && hold[c.good] == 0, $"stage {stages} delivered: +{c.pay} coins, +{c.renown} Renown, fame unchanged {fame}"); }
      Chk(eg.runComplete && Big == null && eg.bigStage == 3 && egCards.Any(x => x.t == GStr("runComplete", "Run complete")), "3rd completion finishes the run (Run complete card)");
      GrandEnsure(); EgRefreshBoards(true); Chk(Big == null, "no grand contract after the run is finished"); Save(); eg = new EgState(); Load(); Chk(eg.runComplete && eg.bigStage == 3, "save/load: run complete kept");
      var rc = egCards.FirstOrDefault(x => x.t == GStr("runComplete", "Run complete")); if (rc.t != null) { StartCoroutine(Ask(rc.t, rc.b, (rc.btn, null))); yield return null; yield return Shot("run_complete"); EgTestCloseCard(); yield return null; } egCards.Clear(); }
    // ---- debug flags in play ----
    Fresh(); rdForceGrand = true; RivalUpdate(); Chk(eg.rank == 2 && Big != null && Big.tpl == "eg_big1" && EgBoardOpen, $"-gbendgamecontract: Merchant, first grand contract on the board ({Big?.tpl})");
    Fresh(); rdForceRace = true; rdBoardForce = true; RivalUpdate(); Chk(eg.board.Any(b => b.race && b.from == "Gwadar") && EgBoardOpen, "-gbracecontract: a race contract on the Gwadar board");
    Fresh(); rdForceGrand = true; rdForceRace = true; rdBoardForce = true; RivalUpdate(); for (int k = 0; k < 5; k++) { RivalUpdate(); EgRefreshBoards(); yield return null; } EgRefreshBoards(true);
    Chk(Big != null && eg.board.Any(b => b.race && b.from == "Gwadar") && EgBoardOpen, $"-gbracecontract + -gbendgamecontract: the forced race survives the Merchant board refresh (grand {Big?.tpl}, race {eg.board.FirstOrDefault(b => b.race)?.title})");
    // ---- rumour during a race names the race's destination, never his current port ----
    { Fresh(); week = 3 * Wk; var rc = new EgContract { id = "rq", race = true, good = G("tea"), qty = 4, to = "Muscat", status = "active", rivalAt = week + 2, rivalWeek = week + 2 }; eg.active.Add(rc);
      port = gw; Rv.next = mu; Chk(RivalRumourPort() == mu, $"race on, you at Gwadar: rumour names {S.ports[RivalRumourPort()].name} (the race's destination Muscat)");
      port = mu; Rv.next = mu; Rv.arriveWeek = week; rdNoRoll = true; RivalOnDock(); Chk(!RivalHere, "race to Muscat on: docking at Muscat before his time, he is not in port there");
      Chk(RivalRumourPort() != mu && RivalRumourPort() >= 0, $"race on, you at Muscat (the race port): rumour names {(RivalRumourPort() >= 0 ? S.ports[RivalRumourPort()].name : "none")}, never Muscat");
      tab = 8; Refresh(); yield return null; var rr = content ? content.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.gameObject.name == "RivalRumour") : null; Chk(rr && !rr.text.Contains("Muscat"), $"tavern at Muscat during the race: '{rr?.text}'");
      rdForceNext = true; RivalOnDock(); Chk(RivalHere && RivalRumourPort() != port, $"forced in port at the race port: rumour still names another port ({S.ports[RivalRumourPort()].name})"); tab = -1; eg.active.Remove(rc); rdNoRoll = false; }
    yield return V07Sealed(Chk, L, dir);

    rdForceNext = flagN0; rdForceRace = flagR0; rdForceGrand = flagG0; rdBoardForce = flagB0; v07Fails = fails; v07Checks = checks; L($"{checks} checks, {(fails == 0 ? "ALL PASS" : fails + " FAIL")}");
    if (!standalone) { Fresh(); yield break; }
    yield return new WaitForSeconds(0.5f); Application.Quit(fails == 0 ? 0 : 1);
  }
}
