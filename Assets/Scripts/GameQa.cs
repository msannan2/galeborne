using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/* gamedev_qa1: scripted QA run. -gbforce qa_run -gbsavedir /tmp/<dir>. No game features: it drives existing code paths
   (HandleBack = what the Android Back / Escape key calls, real Button.onClick for taps), prints "GB QA PASS|FAIL <check>",
   writes <savedir>/qa/qa_results.txt + screenshots, then runs the endgame self-test (GB EGTEST lines) and quits (exit 0 = all pass). */
public partial class Game {
  IEnumerator QaRun() {
    if (!SaveDirIsolated) { Debug.Log("GB QA REFUSED: run with -gbsavedir under /tmp"); Application.Quit(4); yield break; }
    string dir = Path.Combine(SaveDir, "qa"); Directory.CreateDirectory(dir); var log = new StreamWriter(Path.Combine(dir, "qa_results.txt")); int fails = 0, n = 0;
    void L(string s) { log.WriteLine(s); log.Flush(); Debug.Log("GB QA " + s); }
    void Chk(bool ok, string what) { n++; if (!ok) fails++; L((ok ? "PASS " : "FAIL ") + what); }
    IEnumerator Shot(string name) { yield return null; yield return ShotCo(Path.Combine(dir, name + ".png")); yield return new WaitForSeconds(0.3f); }
    IEnumerator Back() { yield return null; HandleBack(); yield return null; yield return null; }
    yield return new WaitForSeconds(1f); onTitle = false; Init(); egTestQuiet = true; storyStep = StDone; coins = 3000; port = PortIdx("Gwadar"); tab = -1; Refresh(); yield return null;

    // ---- Back: every town tab ----
    for (int t = 0; t < 10; t++) { if (t == 4) continue; tab = t; Refresh(); yield return Shot($"back_tab{t}_open"); yield return Back(); Chk(tab == -1, $"Back closes tab {t} ({TabNames[t]})"); }
    // ---- Back: sea map ----
    tab = 4; chartDest = -1; Refresh(); yield return Back(); Chk(tab == -1, "Back closes the sea map (no route)");
    tab = 4; chartDest = PortIdx("Debal"); Refresh(); yield return Shot("back_map_route"); yield return Back(); Chk(tab == 4 && chartDest == -1, "sea map with route: 1st Back clears the route, map stays open"); yield return Shot("back_map_route_cleared"); yield return Back(); Chk(tab == -1, "sea map: 2nd Back closes the map");
    // ---- Back: card over a tab ----
    tab = 0; Refresh(); StartCoroutine(AskD("QA dismissable card", "Back should close only this card.", ("OK", null))); yield return null; yield return null; yield return Shot("back_card_over_tab");
    yield return Back(); Chk(dialog == null && tab == 0, "dismissable card over a tab: 1st Back closes only the card"); yield return Back(); Chk(tab == -1, "then 2nd Back closes the tab");
    tab = 0; Refresh(); StartCoroutine(Ask("QA forced card", "Back must be ignored here.", ("Continue", null))); yield return null; yield return null; var forced = dialog;
    yield return Back(); Chk(dialog != null && dialog == forced && tab == 0, "forced card over a tab: Back ignored"); yield return Shot("back_forced_card"); EgTestCloseCard(); yield return null; yield return null; tab = -1; Refresh();
    // ---- Back: ignore cases ----
    tab = -1; Refresh(); yield return Back(); Chk(tab == -1 && !onTitle, "town with nothing open: Back ignored");
    sailing = true; tab = -1; yield return Back(); Chk(sailing && tab == -1, "sailing: Back ignored"); sailing = false;
    fighting = true; tab = 2; yield return Back(); Chk(fighting && tab == 2, "combat: Back ignored"); fighting = false; tab = -1;
    onTitle = true; tab = 3; yield return Back(); Chk(onTitle && tab == 3, "title screen: Back ignored"); onTitle = false; tab = -1; Refresh();

    // ---- endgame taps: tavern -> board -> row -> Accept ----
    Button Find(string label) => FindObjectsByType<Button>(FindObjectsSortMode.None).Where(b => b.isActiveAndEnabled).FirstOrDefault(b => b.GetComponentsInChildren<Text>().Any(t => t.text.StartsWith(label)));
    tab = 8; Refresh(); yield return null; yield return null; var bb = Find(EndgameData.BoardTitle); Chk(bb != null, "tavern shows the Contract board button"); yield return Shot("eg_tavern");
    if (bb != null) { bb.onClick.Invoke(); yield return null; yield return null; Chk(dialog != null && dialog.name == "EgBoard", "tap: board opens"); yield return Shot("eg_board");
      var row = dialog ? dialog.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name.StartsWith("Btn ") && b.GetComponentsInChildren<Text>().Any(t => t.text.Contains(" coins"))) : null; Chk(row != null, "board lists a contract row");
      if (row != null) { row.onClick.Invoke(); yield return null; yield return null; Chk(dialog != null && dialog.name == "EgCard", "tap: row opens the contract card"); yield return Shot("eg_card");
        yield return Back(); Chk(dialog != null && dialog.name == "EgBoard", "Back on the card = Not now (back to the list)"); row = dialog.GetComponentsInChildren<Button>().FirstOrDefault(b => b.GetComponentsInChildren<Text>().Any(t => t.text.Contains(" coins"))); row.onClick.Invoke(); yield return null; yield return null;
        int c0 = coins; var acc = Find(EndgameData.S("accept", "Accept")); acc?.onClick.Invoke(); for (int i = 0; i < 4; i++) yield return null; Chk(eg.active.Count == 1 && coins < c0 && dialog == null, $"tap Accept: 1 active contract, deposit {c0 - coins} taken, board closed"); }
    }
    // ---- gamedev_egfix: tavern pinned buttons, journal count/header, portrait card ----
    { Rect SR2(RectTransform r) { var c = new Vector3[4]; r.GetWorldCorners(c); var a = RectTransformUtility.WorldToScreenPoint(null, c[0]); var b = RectTransformUtility.WorldToScreenPoint(null, c[2]); return Rect.MinMaxRect(a.x, a.y, b.x, b.y); }
      RectTransform Named(string nm) => FindObjectsByType<RectTransform>(FindObjectsSortMode.None).Where(r => r.gameObject.activeInHierarchy).FirstOrDefault(r => r.name == nm);
      int port0 = port; var story0 = tavernStory; var news0 = news.ToList();
      foreach (var pn in new[] { "Gwadar", "Debal", "Muscat" }) {
        port = PortIdx(pn); tavernStory = "The tavern keeper slides you a letter sealed with the Qarin mark: \"To the captain who carries this seal: the Sultan's customs in Muscat look away for those who bear the Qarin mark. Bring powder, and ask for the man in the blue doorway. - Q.\"";
        for (int k = 0; k < 5; k++) news.Insert(0, "Rumor (Week 2 · Moon 1 · Year 1): Muscat's customs seize gunpowder on sight, but the garrison still pays a fortune for it under the table. Farida in the back alley has some, at Gwadar's price.");
        tab = 8; Refresh(); yield return null; yield return null; yield return Shot("egfix_tavern_" + pn);
        var msgR = Named("Msg"); var qb = Find("Ask about work"); var cb = Find(EndgameData.BoardTitle);
        if (msgR != null && qb != null && cb != null) { var mr = SR2(msgR); var qr = SR2(qb.GetComponent<RectTransform>()); var cr = SR2(cb.GetComponent<RectTransform>()); L($"rect {pn} ticker {mr} ask {qr} board {cr}");
          Chk(cr.yMin > mr.yMax && qr.yMin > mr.yMax && cr.yMax < Screen.height && !cr.Overlaps(qr), $"{pn} tavern (long list): Ask about work + Contract board pinned above the ticker"); }
        else Chk(false, $"{pn} tavern: ticker/Ask/Contract board found ({msgR != null}/{qb != null}/{cb != null})");
        var tvp = Named("TavVp"); Chk(tvp != null && tvp.GetComponent<ScrollRect>() != null && tvp.GetComponent<ScrollRect>().content.rect.height > tvp.rect.height, $"{pn} tavern: long list scrolls in its viewport");
        news.Clear(); news.AddRange(news0); }
      port = port0; tavernStory = story0; tab = -1; Refresh(); yield return null;
      coins += 5000; foreach (var b in eg.board.ToList()) { if (eg.active.Count >= EndgameData.MaxActive) break; EgAccept(b); }
      OpenJournal(); yield return null; yield return null; yield return Shot("egfix_journal");
      var jtxt = dialog ? dialog.GetComponentsInChildren<Text>().Select(t => t.text).ToList() : new List<string>();
      Chk(jtxt.Any(t => t.StartsWith($"Active {eg.active.Count}/2")) && eg.active.Count == 2, $"journal count reads 'Active {eg.active.Count}/2' (cap 2)");
      Chk(!(eg.active.Count > 0 && jtxt.Any(t => t.StartsWith("No active jobs"))), "journal: no 'No active jobs' while contracts are listed");
      var jr = Named("JournalRank"); var jc = Find("Close"); Chk(jr != null && jc != null && SR2(jr).xMin + jr.GetComponent<Text>().preferredWidth * jr.lossyScale.x < SR2(jc.GetComponent<RectTransform>()).xMin, "journal header rank/Renown line ends left of Close");
      { var jtt = Named("JournalTitle"); var jtx = jtt ? jtt.GetComponent<Text>() : null; Chk(jtx != null && jr != null && SR2(jtt).xMin + jtx.preferredWidth * jtt.lossyScale.x < SR2(jr).xMin, "journal header: rank line starts after the word Journal"); }
      yield return Back(); yield return null; yield return null;
      StartCoroutine(PortraitCard("Yusuf, harbour master of Debal", "YUSUF: \"They're buying every warehouse from Gwadar to Muscat. Nobody knows why.\"\n\nNew story goal: " + A1s("goalLong", "Find out what House Qarin is hoarding."), ArtSlot("Art/Portraits/yusuf"), ("I'll find out", null)));
      for (int k = 0; k < 4; k++) yield return null; yield return Shot("egfix_yusuf");
      var pr = Named("Portrait"); var fb = Find("I'll find out"); Text bt = dialog ? dialog.GetComponentsInChildren<Text>().FirstOrDefault(t => t.text.StartsWith("YUSUF")) : null;
      if (pr != null && fb != null && bt != null) { var prr = SR2(pr); Chk(!prr.Overlaps(SR2(bt.rectTransform)) && !prr.Overlaps(SR2(fb.GetComponent<RectTransform>())) && bt.preferredHeight <= bt.rectTransform.rect.height + 1, "Yusuf card: portrait clear of body text and button"); }
      else Chk(false, $"Yusuf card parts found ({pr != null}/{fb != null}/{bt != null})");
      fb?.onClick.Invoke(); for (int k = 0; k < 4; k++) yield return null; tab = -1; Refresh(); yield return null; }
    // ---- map flag vs plaques ----
    if (eg.active.Count > 0) { var ac = eg.active[0]; tab = 4; chartDest = -1; chartCentered = false; Refresh(); yield return null; yield return null; yield return Shot("eg_map_flag");
      Rect SR(RectTransform r) { var c = new Vector3[4]; r.GetWorldCorners(c); var a = RectTransformUtility.WorldToScreenPoint(null, c[0]); var b = RectTransformUtility.WorldToScreenPoint(null, c[2]); return Rect.MinMaxRect(a.x, a.y, b.x, b.y); }
      var all = FindObjectsByType<RectTransform>(FindObjectsSortMode.None); var flag = all.FirstOrDefault(r => r.name == "egflag " + ac.to);
      Chk(flag != null, $"sea map shows a flag for {ac.to}");
      if (flag != null) { var fr = SR(flag); L($"rect flag {ac.to}: {fr}"); bool any = false;
        foreach (var o in all.Where(r => r.name.StartsWith("plaque ") || r.name.StartsWith("town ") || r.name == "ShipHere")) { var orr = SR(o); bool ov = fr.Overlaps(orr); L($"rect {o.name}: {orr}{(ov ? "  OVERLAPS FLAG" : "")}"); any |= ov; }
        Chk(!any, "map flag overlaps no port plaque / port ring / ship marker"); }
      tab = -1; Refresh(); }
    // ---- gamedev_continue: dock at a new port, quit on the arrival card, Continue -> that port and week ----
    { tab = -1; dialog = null; port = PortIdx("Gwadar"); safeUntilSail = sails + 100; giftPending = 1; int w0 = week; Refresh(); yield return null;
      StartCoroutine(Voyage(PortIdx("Debal"))); bool gift = false; float tq = Time.realtimeSinceStartup;
      while (Time.realtimeSinceStartup - tq < 90f) { skipVoyage = true; ff = true;
        if (dialog != null) { bool g = dialog.GetComponentsInChildren<Text>().Any(t => t.text.StartsWith("A gift from Yusuf")); if (g) { gift = true; break; } EgTestCloseCard(); }
        yield return null; }
      Chk(gift && sailing && port == PortIdx("Debal"), $"voyage Gwadar->Debal reaches the arrival gift card (port {S.ports[port].name}, week {week})");
      int wDock = week; OnApplicationQuit(); /* the real quit path, with the arrival card still open */
      port = PortIdx("Gwadar"); week = w0; saveSeq = 0; bool ok = Load();
      Chk(ok && port == PortIdx("Debal") && week == wDock, $"Continue after quitting on the arrival card resumes at Debal week {wDock} (loaded {lastLoadedFrom}: {S.ports[port].name} week {week})");
      for (int k = 0; k < 40 && sailing; k++) { EgTestCloseCard(); yield return null; }
      port = PortIdx("Gwadar"); saveSeq = 0; ok = Load(); Chk(ok && port == PortIdx("Debal") && !sailing, $"after the voyage ends, Continue still resumes at Debal ({lastLoadedFrom})"); tab = -1; Refresh(); yield return null; }
    // ---- gamedev_continue: blockade seizure on docking is never silent; HUD Next shows the urgent contract ----
    { tab = -1; dialog = null; int cu = G("copper"), mu = PortIdx("Muscat"); port = PortIdx("Debal"); hold[cu] = 8; blockGood[mu] = cu; blockEnd[mu] = week + 50; safeUntilSail = sails + 100; Refresh(); yield return null;
      StartCoroutine(Voyage(mu)); bool card = false; float tq = Time.realtimeSinceStartup;
      while (Time.realtimeSinceStartup - tq < 90f) { skipVoyage = true; ff = true; if (blockGood[mu] != cu) blockGood[mu] = cu;
        if (dialog != null) { if (dialog.GetComponentsInChildren<Text>().Any(t => t.text.StartsWith("Blockade at Muscat"))) { card = true; break; } EgTestCloseCard(); }
        yield return null; }
      yield return Shot("cont_blockade_card"); Chk(card && hold[cu] == 0, $"blockade seizure on docking shows a 'Blockade at Muscat' card (copper now {hold[cu]})");
      for (int k = 0; k < 40 && sailing; k++) { EgTestCloseCard(); yield return null; } blockGood[mu] = -1;
      Chk(msg.Contains("seize"), $"after the card the dock message still names the seizure ({msg})");
      if (eg.active.Count == 0) { coins += 5000; EgRefreshBoards(); foreach (var b in eg.board.ToList()) { if (eg.active.Count >= 1) break; EgAccept(b); } }
      var u = eg.active.OrderBy(c => c.dueWeek).FirstOrDefault(); var g3 = Goals3();
      Chk(u != null && g3.Item2.Contains($"{S.goods[u.good].name} to {u.to}") && g3.Item2.Contains(EgMoonsColor(u)) && !g3.Item2.Contains("Journal empty"), $"HUD Next shows the most urgent contract in its deadline colour ({g3.Item2})");
      tab = -1; Refresh(); yield return null; }
    // ---- gamedev_ring: naval range ring (radius = fire range, state flips at the threshold, plaque = fire-check distance, under the hulls) ----
    { tab = -1; dialog = null; Refresh(); yield return null; StartCoroutine(Fight(true)); for (int k = 0; k < 6; k++) yield return null; combatPaused = true;
      var H = core.H; var P = core.P; H.heading = 0; H.rudder = 0; H.reloadT = 0; H.ammo = 0; P.heading = 180;
      IEnumerator At(float d) { P.x = H.x + d; P.z = H.z; for (int k = 0; k < 3; k++) yield return null; }
      bool radiusOk = true, flipOk = true, plaqueOk = true; string why = "";
      for (int a = 0; a < 4; a++) { H.ammo = a; float rg = core.FireRange(H);
        yield return At(rg + 25f); float sideR = rangeRing.localScale.x * RingStrokePx / RingSpritePx;
        if (Mathf.Abs(ringRadiusM - rg) > 1e-4f || Mathf.Abs(ringRadiusWorld - rg * K) > 1e-3f || Mathf.Abs(sideR - rg * K) > 1e-3f || Mathf.Abs((rangeMarker.position - new Vector3(rangeRing.position.x, rangeMarker.position.y, rangeRing.position.z)).magnitude - rg * K) > 1e-2f) { radiusOk = false; why += $" r{a}:{ringRadiusM}/{rg}"; }
        yield return At(rg + 0.02f); bool o1 = !ringInRange && !core.InFireRange(H, P); int pl = ringPlaqueN; bool pt = rangePlaqueTxt.text.StartsWith($"Range {Mathf.CeilToInt(core.FireDist(H, P))} m · ");
        if (pl != Mathf.CeilToInt(core.FireDist(H, P)) || !pt || pl <= Mathf.FloorToInt(rg) && rg == Mathf.Floor(rg)) { plaqueOk = false; why += $" p{a}:{rangePlaqueTxt.text}/{core.FireDist(H, P):0.00}"; }
        if ((H.ammo != 3 || H.fireLeft > 0) && core.InArc(H, P) && H.gunHp > 0) { var fbr = FireBlockReason(); if (fbr != $"Out of range for {S.combat.ammo[a].name} ({rg:0} m).") { plaqueOk = false; why += $" msg{a}:{fbr}"; } }
        int pulses0 = ringPulses; yield return At(rg - 0.02f); bool i1 = ringInRange && core.InFireRange(H, P) && rangePlaqueTxt.text == "In range" && ringPulses == pulses0 + 1 && fireRangeBorder.gameObject.activeSelf;
        yield return At(rg - 5f); bool i2 = ringInRange && ringPulses == pulses0 + 1; /* one pulse per entry */
        if (!(o1 && i1 && i2)) { flipOk = false; why += $" f{a}:{o1}/{i1}/{i2}"; } L($"ring ammo {S.combat.ammo[a].name}: range {rg} m, world radius {rg * K:0.00}"); }
      H.ammo = 0; float r0 = core.FireRange(H);
      Chk(radiusOk, "range ring radius (ring, sprite stroke, marker) equals the loaded shot's fire range, all 4 shots" + why);
      Chk(flipOk, "range ring flips dashed->solid exactly at the fire-range threshold (+/-0.02 m), one pulse per entry, FIRE border in range");
      Chk(plaqueOk, "plaque number = ceil(fire-check distance), same range figure as 'Out of range for X (N m)'");
      yield return At(r0 + 60f); yield return At(r0 + 50f); bool cl = rangePlaqueTxt.text.EndsWith("Closing"); yield return At(r0 + 55f); bool op = rangePlaqueTxt.text.EndsWith("Opening");
      Chk(cl && op, $"plaque reads Closing when the gap shrinks and Opening when it grows ({rangePlaqueTxt.text})");
      // under the hulls: ring passes right under Hasan; pixels covered by either hull must not change when the ring is drawn
      yield return At(r0); { int w = Screen.width / 2, h = Screen.height / 2; var rt = new RenderTexture(w, h, 24); var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color32[] Grab() { var pt = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = pt; var pa = RenderTexture.active; RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply(); RenderTexture.active = pa; return tex.GetPixels32(); }
        var A = Grab(); rangeRingR.enabled = rangeMarkerR.enabled = false; var B = Grab(); ship.gameObject.SetActive(false); pirateShip.gameObject.SetActive(false); var D = Grab(); ship.gameObject.SetActive(true); pirateShip.gameObject.SetActive(true); rangeRingR.enabled = rangeMarkerR.enabled = true;
        bool Df(Color32 x, Color32 y) => Mathf.Abs(x.r - y.r) + Mathf.Abs(x.g - y.g) + Mathf.Abs(x.b - y.b) > 6;
        int hullPx = 0, bad = 0, ringPx = 0; for (int i = 0; i < A.Length; i++) { bool hull = Df(B[i], D[i]); if (hull) { hullPx++; if (Df(A[i], B[i])) bad++; } else if (Df(A[i], B[i])) ringPx++; }
        rt.Release(); Destroy(rt); Destroy(tex);
        L($"ring vs hull pixels: hull {hullPx}, hull pixels changed by the ring {bad}, ring pixels on open water {ringPx}, ring queue {rangeRingMat.renderQueue}");
        Chk(hullPx > 200 && ringPx > 200 && bad <= hullPx / 200 && rangeRingMat.renderQueue > 2000 && rangeRingMat.renderQueue < 3000, $"range ring renders on the water below both hulls (hull px {hullPx}, changed {bad}, ring px {ringPx})"); }
      // daytime screenshots for review
      yield return At(r0 + 45f); yield return new WaitForSeconds(0.4f); yield return Shot("ring_out_of_range"); yield return At(r0 - 20f); yield return new WaitForSeconds(0.1f); yield return Shot("ring_entering_pulse"); yield return new WaitForSeconds(0.6f); yield return Shot("ring_in_range");
      P.hull = 0; combatPaused = false; float tq = Time.realtimeSinceStartup; while (fighting && Time.realtimeSinceStartup - tq < 90f) { if (dialog != null) EgTestCloseCard(); yield return null; }
      Chk(!fighting && (rangeRing == null || !rangeRing.gameObject.activeSelf), "after the fight the range ring is hidden"); tab = -1; Refresh(); yield return null; }
    log.Close();
    // ---- endgame logic: deliver, fail, rank-up, save/load, cards (existing self-test) ----
    egFails = 0; yield return EgSelfTest(false);
    log = new StreamWriter(Path.Combine(dir, "qa_results.txt"), true); L($"endgame self-test: {(egFails == 0 ? "PASS" : egFails + " FAIL")} (GB EGTEST lines in the player log; its screenshots are in {Path.Combine(SaveDir, "egtest")})"); fails += egFails;
    L(fails == 0 ? $"QA ALL PASS ({n} checks + endgame self-test)" : $"QA {fails} FAIL"); log.Close(); yield return new WaitForSeconds(0.5f); Application.Quit(fails == 0 ? 0 : 1);
  }
}
