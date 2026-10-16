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
      var jr = Named("JournalRank"); var jc = Find("Close"); Chk(jr != null && jc != null && SR2(jr).xMax < SR2(jc.GetComponent<RectTransform>()).xMin, "journal header rank/Renown line ends left of Close");
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
    log.Close();
    // ---- endgame logic: deliver, fail, rank-up, save/load, cards (existing self-test) ----
    egFails = 0; yield return EgSelfTest(false);
    log = new StreamWriter(Path.Combine(dir, "qa_results.txt"), true); L($"endgame self-test: {(egFails == 0 ? "PASS" : egFails + " FAIL")} (GB EGTEST lines in the player log; its screenshots are in {Path.Combine(SaveDir, "egtest")})"); fails += egFails;
    L(fails == 0 ? $"QA ALL PASS ({n} checks + endgame self-test)" : $"QA {fails} FAIL"); log.Close(); yield return new WaitForSeconds(0.5f); Application.Quit(fails == 0 ? 0 : 1);
  }
}
