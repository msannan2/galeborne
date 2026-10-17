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

  IEnumerator V07SelfTest(bool standalone = true) {
    int fails = 0, checks = 0; void L(string s) => Debug.Log("GB V07TEST " + s); void Chk(bool ok, string what) { checks++; if (!ok) fails++; L((ok ? "PASS " : "FAIL ") + what); }
    if (!SaveDirIsolated) { L("REFUSED: needs -gbsavedir"); Application.Quit(4); yield break; }
    string dir = Path.Combine(SaveDir, "v07test"); Directory.CreateDirectory(dir); IEnumerator Shot(string n) { yield return null; yield return null; yield return ShotCo(Path.Combine(dir, n + ".png")); yield return new WaitForSeconds(0.3f); }
    bool flagN0 = rdForceNext, flagR0 = rdForceRace, flagG0 = rdForceGrand, flagB0 = rdBoardForce; rdForceNext = rdForceRace = rdForceGrand = false;
    void Fresh() { Init(); egTestQuiet = true; storyStep = StDone; coins = 8000; port = PortIdx("Gwadar"); week = 0; tab = -1; rvCards.Clear(); egCards.Clear(); rdForceNext = rdForceRace = rdForceGrand = false; }
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
      Chk(msg.Contains("The Red Dhow is in port.") && news[0].StartsWith("The Red Dhow is in port."), $"dock ticker: '{msg}' / news '{news[0]}'");
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
    IEnumerator CardCheck(string key, string title, string body, string shot) { int m = RivalData.Mood(key); var co = StartCoroutine(RivalCard(title, body, m)); yield return null; yield return null; var d = dialog; var tx = d ? d.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.gameObject.name == "RivalLine") : null;
      Chk(d && d.name == "RivalCard" && tx && tx.text.Contains(want[key].text) && FaceIn(d) == "k7_" + want[key].mood, $"card '{key}': text shown, portrait {FaceIn(d)} (want k7_{want[key].mood})"); if (shot != null) yield return Shot(shot); var b = d ? FindBtn(d, "Continue") : null; if (b) b.onClick.Invoke(); yield return null; yield return null; }
    yield return CardCheck("taunt1", RivalData.Name, RivalData.Line("taunt1"), "card_taunt1");
    yield return CardCheck("taunt2", RivalData.Name, RivalData.Line("taunt2"), null);
    // ---- the Red Dhow at sea: red sail, no emission; pirates have no red on the sail ----
    { var m = RedSailMaterial(); var tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null; float rf = 0; if (tex) { var px = ReadTexPixels(tex, out int w, out int h); rf = px.Count(ShipArt.IsRed) / (float)px.Length; }
      Chk(!m.IsKeywordEnabled("_EMISSION") && (!m.HasProperty("_EmissionColor") || m.GetColor("_EmissionColor").maxColorComponent < 0.001f) && Mathf.Abs(m.GetFloat("_Smoothness") - 0.1f) < 1e-3f && m.GetFloat("_Cull") == 0f, $"Red Dhow sail material: Lit ({m.shader.name}), no emission, smoothness {m.GetFloat("_Smoothness"):0.00}, double-sided");
      Chk(tex && tex.name == "red_sail_canvas" && rf > 0.8f, $"Red Dhow sail texture {(tex ? tex.name : "none")} is red: {rf:P0} red pixels");
      var d = BuildRedDhow(); var rl = d ? d.GetComponentsInChildren<Renderer>(true).Where(x => x.gameObject.name == "RedLateen").ToList() : new List<Renderer>(); Chk(rl.Count > 0 && rl.All(x => x.sharedMaterial == m), $"Red Dhow built with {rl.Count} red sail renderer(s)");
      var pm = Resources.Load<Material>("Mats/ShipPirate"); var pt = pm ? pm.GetTexture("_BaseMap") : null; if (pt) { var px = ReadTexPixels(pt, out int w, out int h); float sailRed = RedFrac(px, w, h, ShipArt.SAIL, ShipArt.N), penRed = RedFrac(px, w, h, new RectInt(ShipArt.PENNANT.x, ShipArt.PENNANT.y, ShipArt.PENNANT.width, ShipArt.PENNANT.height / 2), ShipArt.N);
        Chk(sailRed < 0.005f, $"pirate sail (canvas, stripe, skull ring) has no red: {sailRed:P2} red pixels in the SAIL cell");
        Chk(penRed > 0.5f, $"pirate pennant keeps its red (allowed): {penRed:P0} of its lower half"); } else Chk(false, "pirate atlas missing");
      Rv.next = db; RivalSeaStart(db, 10f); bool on = rdSeaPending; Rv.next = mu; rdSeaPending = false; RivalSeaStart(db, 10f); bool off = !rdSeaPending; rdSeaPending = false; Chk(on && off, "at sea: the Red Dhow shows on the leg to his next port, not on others");
      if (town3D) town3D.gameObject.SetActive(false); sea3D.gameObject.SetActive(true); backdrop.color = Color.clear; bool s0 = sailing; sailing = true; Refresh(); RedDhowShow(); rdSeaHideAt = Time.time + 10; yield return new WaitForSeconds(0.6f); yield return Shot("sea_red_dhow");
      Chk(redDhow && redDhow.gameObject.activeSelf, "Red Dhow visible at sea"); sailing = s0; redDhow.gameObject.SetActive(false); sea3D.gameObject.SetActive(false); tab = -1; Refresh(); yield return null; }

    // ---- race contracts ----
    Fresh(); week = 2 * Wk; AddRenown(ED.RankRenown(1), "test"); egCards.Clear(); EgRefreshBoards(true); foreach (var b in eg.board) b.race = false; RivalMarkRace(true);
    var race = eg.board.FirstOrDefault(b => b.race); Chk(race != null && race.from == "Gwadar" && eg.board.Count(b => b.race) == 1, $"race contract posted at Gwadar: {race?.title} -> {race?.to}");
    { var co = StartCoroutine(EgBoardCo()); yield return null; yield return null; var bd = dialog; var rows = bd ? bd.GetComponentsInChildren<Button>(true).Where(b => b.GetComponentsInChildren<Text>(true).Any(t => t.gameObject.name == "RaceRow" || t.text.Contains(" · "))).ToList() : new List<Button>();
      var raceRow = rows.FirstOrDefault(b => b.GetComponentsInChildren<Text>(true).Any(t => t.gameObject.name == "RaceRow")); var icon = raceRow ? raceRow.GetComponentsInChildren<Image>(true).FirstOrDefault(i => i.gameObject.name == "RaceIcon") : null; var rowTxt = raceRow ? raceRow.GetComponentsInChildren<Text>(true).First(t => t.gameObject.name == "RaceRow") : null;
      Chk(icon && icon.sprite && icon.sprite.name.StartsWith("rival_sail_icon") && icon.preserveAspect && Mathf.Abs(icon.rectTransform.rect.width - 30) < 0.5f && rowTxt && icon.rectTransform.anchoredPosition.x + 30 <= rowTxt.rectTransform.anchoredPosition.x, $"board: red-sail icon ({(icon && icon.sprite ? icon.sprite.name : "none")}, {(icon ? icon.rectTransform.rect.width : 0):0} px) left of the race notice");
      Chk(rows.Count(b => b.GetComponentsInChildren<Image>(true).Any(i => i.gameObject.name == "RaceIcon")) == 1, $"only the race notice has the icon ({rows.Count} rows)"); yield return Shot("board_race_icon");
      var close = FindBtn(bd, "Close"); if (close) close.onClick.Invoke(); yield return null; yield return null; }
    { var dm = ModalDim("EgDim"); var cc = EgCard(race, () => { }, () => { }); yield return null; var ic = cc.GetComponentsInChildren<Image>(true).FirstOrDefault(i => i.gameObject.name == "RaceIconCard"); var tag = cc.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.gameObject.name == "RaceTag");
      var ttl = ic ? ic.transform.parent.GetComponent<Text>() : null; Chk(ic && ic.sprite && ttl && ttl.text == race.title && ic.rectTransform.anchoredPosition.x + 15 <= -Mathf.Min(ttl.preferredWidth, ttl.rectTransform.rect.width) / 2 - 5 && tag && tag.text == RivalData.S("raceTag"), $"race card: icon left of the title, tag '{(tag ? tag.text : "")}'"); yield return Shot("contract_card_race"); Destroy0(cc.gameObject); Destroy0(dm.gameObject); dialog = null; yield return null; }
    { coins = 8000; int r0 = eg.renown; Chk(EgAccept(race) && race.rivalWeek >= week + 1 && race.rivalWeek <= week + 3, $"race accepted: Rashid due week {race.rivalWeek} (now {week})");
      var tc = rvCards.LastOrDefault(); Chk(tc.b.Contains(want["taunt3"].text) && tc.mood == 3, $"accepting a race: taunt3 card with k7_{tc.mood}"); yield return CardCheck("taunt3", tc.t, tc.b, null); rvCards.Clear();
      race.rivalWeek = week + 1; hold[race.good] += race.qty; NewWeek(); Chk(race.status == "active", "tie week: Rashid due this week, the race is still open when you dock (tie goes to the player)");
      port = PortIdx(race.to); int c1 = coins; EgOnDock(); Chk(race.status == "done" && coins == c1 + race.pay + race.deposit && Rv.raceLosses == 1, $"race WON: full pay {race.pay} + deposit {race.deposit}");
      var wc = rvCards.LastOrDefault(); Chk(wc.b.Contains(want["raceLoss"].text) && wc.mood == 2, $"he loses the race: '{wc.b}' with k7_{wc.mood}"); yield return CardCheck("raceLoss", wc.t, wc.b, "card_rashid_loses"); rvCards.Clear(); }
    { EgRefreshBoards(true); foreach (var b in eg.board) b.race = false; RivalMarkRace(true); var r2 = eg.board.FirstOrDefault(b => b.race); if (r2 == null) { EgRefreshBoards(true); RivalMarkRace(true); r2 = eg.board.FirstOrDefault(b => b.race); }
      port = PortIdx(r2.from); coins = 8000; EgAccept(r2); rvCards.Clear(); r2.rivalWeek = week + 1; Save(); eg = new EgState(); Load(); var r2l = eg.active.FirstOrDefault(c => c.id == r2.id); Chk(r2l != null && r2l.race && r2l.rivalWeek == r2.rivalWeek, $"save/load race: {r2l?.id} race={r2l?.race} rivalWeek {r2l?.rivalWeek}"); r2 = r2l;
      int c0 = coins, rn = eg.renown; NewWeek(); Chk(r2.status == "active", "week 1: still racing"); NewWeek();
      Chk(r2.status == "taken" && !eg.active.Contains(r2) && coins == c0 + r2.deposit && eg.renown == rn && Rv.raceWins == 1, $"race LOST: '{r2.status}', deposit {r2.deposit} returned, renown {rn}->{eg.renown}");
      var lc = rvCards.LastOrDefault(); Chk(lc.t == RivalData.S("raceLostTitle") && lc.b.Contains(want["raceWin"].text) && lc.mood == 1, $"he wins the race: '{lc.t}' with k7_{lc.mood}"); yield return CardCheck("raceWin", lc.t, lc.b, "card_rashid_wins"); rvCards.Clear(); }
    { int N = 4000, mn = 99, mx = 0, ge2 = 0; for (int i = 0; i < N; i++) { int w = RivalRaceWeeks(); mn = Mathf.Min(mn, w); mx = Mathf.Max(mx, w); if (w >= 2) ge2++; } float f2 = ge2 / (float)N;
      Chk(mn == 1 && mx == 3 && Mathf.Abs(f2 - 0.375f) < 0.03f, $"Rashid's race time = route (1 week) +/- half a moon: {mn}..{mx} weeks; a direct player (1 week) always wins, a 2-week player wins {f2:P1} (want ~37.5%)"); }

    // ---- grand contracts ----
    Fresh(); { int f0 = fame; AddRenown(ED.RankRenown(2) - 1, "test"); Chk(eg.rank == 1 && Big == null, $"at {eg.renown} Renown (Trader): no grand contract");
      AddRenown(1, "test"); var b1 = Big; Chk(eg.rank == 2 && b1 != null && b1.status == "offer" && eg.bigStage == 0, $"Merchant ({eg.renown}): the grand sequence starts, offer 1 = {b1?.title}");
      var seq = GrandSeq; Chk(seq.Count == 3 && seq.Select(d => (string)d["dest"]).Distinct().Count() == 3, "sequence of 3, each to a different port: " + string.Join(", ", seq.Select(d => $"{d["good"]} x{d["qty"]} -> {d["dest"]}")));
      Chk((string)seq[0]["good"] == "dates" && (string)seq[0]["dest"] == "Debal" && (string)seq[1]["good"] == "copper" && (string)seq[1]["dest"] == "Muscat" && (string)seq[2]["good"] == "indigo" && (string)seq[2]["dest"] == "Gwadar" && seq.All(d => Design.F(d["qty"]) == 20), "ruling: Dates x20 -> Debal, Copper x20 -> Muscat, Indigo x20 -> Gwadar");
      Chk(seq.All(d => Design.F(d["qty"]) <= baseHold) && shipTier[3] == 0, $"x20 fits the base hold ({baseHold}) - no Hold upgrade needed");
      int exp = Mathf.Min(GrandCap, Mathf.RoundToInt(20 * price[db, G("dates")]) + 1000); Chk(b1.pay == exp && b1.renown == 3 * ED.RenownDelivery && b1.moons == 2 && b1.deposit == 0 && fame == f0, $"offer 1: pay {b1.pay} = sale value at Debal + 1,000 (cap {GrandCap}), Renown {b1.renown} (3x {ED.RenownDelivery}), 2 moons, no deposit, fame unchanged {fame}");
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
        if (c == null) break; port = PortIdx(dest) == gw ? db : gw; EgAccept(c); if (stages == 1) { int dw = c.dueWeek; Save(); eg = new EgState(); Load(); c = Big; Chk(c != null && c.status == "active" && c.tpl == "eg_big2" && c.dueWeek == dw && eg.bigStage == 1, $"save/load mid-sequence: stage {eg.bigStage + 1}, {c?.tpl} active, due {c?.dueWeek}"); }
        NewWeek(); hold[c.good] = 20; port = PortIdx(dest); int c0 = coins, r0 = eg.renown, f0 = fame; EgOnDock(); stages++;
        Chk(c.status == "done" && coins == c0 + c.pay && eg.renown == r0 + c.renown && fame == f0 && hold[c.good] == 0, $"stage {stages} delivered: +{c.pay} coins, +{c.renown} Renown, fame unchanged {fame}"); }
      Chk(eg.runComplete && Big == null && eg.bigStage == 3 && egCards.Any(x => x.t == GStr("runComplete", "Run complete")), "3rd completion finishes the run (Run complete card)");
      GrandEnsure(); EgRefreshBoards(true); Chk(Big == null, "no grand contract after the run is finished"); Save(); eg = new EgState(); Load(); Chk(eg.runComplete && eg.bigStage == 3, "save/load: run complete kept");
      var rc = egCards.FirstOrDefault(x => x.t == GStr("runComplete", "Run complete")); if (rc.t != null) { StartCoroutine(Ask(rc.t, rc.b, (rc.btn, null))); yield return null; yield return Shot("run_complete"); EgTestCloseCard(); yield return null; } egCards.Clear(); }
    // ---- debug flags in play ----
    Fresh(); rdForceGrand = true; RivalUpdate(); Chk(eg.rank == 2 && Big != null && Big.tpl == "eg_big1" && EgBoardOpen, $"-gbendgamecontract: Merchant, first grand contract on the board ({Big?.tpl})");
    Fresh(); rdForceRace = true; rdBoardForce = true; RivalUpdate(); Chk(eg.board.Any(b => b.race && b.from == "Gwadar") && EgBoardOpen, "-gbracecontract: a race contract on the Gwadar board");

    rdForceNext = flagN0; rdForceRace = flagR0; rdForceGrand = flagG0; rdBoardForce = flagB0; v07Fails = fails; v07Checks = checks; L($"{checks} checks, {(fails == 0 ? "ALL PASS" : fails + " FAIL")}");
    if (!standalone) { Fresh(); yield break; }
    yield return new WaitForSeconds(0.5f); Application.Quit(fails == 0 ? 0 : 1);
  }
}
