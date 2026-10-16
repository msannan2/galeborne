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
    void QaTapFire() { var b = fireTxt ? fireTxt.transform.parent.GetComponent<Button>() : null; if (b) UnityEngine.EventSystems.ExecuteEvents.Execute(b.gameObject, new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current), UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler); } /* gamedev_toll_fix: the same click a tap sends to the FIRE button */
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
        news.Insert(0, "Rumor (Week 2 · Moon 1 · Year 1): Muscat's customs seize gunpowder on sight, but the garrison still pays a fortune for it under the table. Farida in Debal's Back Alley sells it cheap. Sell it in Muscat's Back Alley."); /* research_gp1: once, not 5x */ foreach (var fill in new[] { "Rumor (Week 2 · Moon 1 · Year 1): Sailors from Debal say the dates harvest failed along the whole river. The palm growers are weeping, the merchants are smiling, and someone with a full hold will get very rich.", "Rumor (Week 3 · Moon 1 · Year 1): Don't take copper to Muscat this moon. Half the coast had the same idea, the quay is stacked with it, and the smiths there are paying less than they did last year.", "Rumor (Week 3 · Moon 1 · Year 1): Black sails were seen off Gwadar two nights running. The fishermen stay close to shore, and every captain with a rich hold is hiring extra hands for the crossing.", "Rumor (Week 4 · Moon 1 · Year 1): The old navigator says the stars warn of a storm on the road to Debal. Sail slow and arrive late, or sail brave and pray the mast holds when the squall comes." }) news.Insert(0, fill); /* research_gp1: 4 different fillers keep the long-list scroll check meaningful without duplicates */
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
    // ---- gamedev_customs_save: coins paid on the leg, quit on the customs card at Muscat, Continue -> Muscat with the coins after the toll, customs replayed ----
    { tab = -1; dialog = null; int mu = PortIdx("Muscat"), gp = G("gunpowder"); port = PortIdx("Debal"); hold[gp] = Mathf.Max(hold[gp], 2); storyAmbushDone = true; safeUntilSail = sails + 100; customsPending = -1; var qaBan = new Ban { good = gp, port = mu, start = 0, end = int.MaxValue / 2 }; bans.Add(qaBan); Refresh(); yield return null;
      Chk(CarriesBanned(mu), "customs check: gunpowder in the hold is banned at Muscat");
      StartCoroutine(Voyage(mu)); yield return null; int c0 = coins; coins -= 88; Save(); /* the toll paid mid-leg, saved like the pirate-toll outcome (port still Debal) */ int cAfter = coins; bool cc = false; float tq = Time.realtimeSinceStartup;
      while (Time.realtimeSinceStartup - tq < 90f) { skipVoyage = true; ff = true;
        if (dialog != null) { if (dialog.GetComponentsInChildren<Text>().Any(t => t.text.StartsWith("Customs at"))) { cc = true; break; } EgTestCloseCard(); }
        yield return null; }
      Chk(cc && sailing && port == mu, $"voyage Debal->Muscat reaches the customs card (port {S.ports[port].name}, coins {coins})");
      OnApplicationQuit(); /* the real quit path, customs card still open */
      port = PortIdx("Debal"); coins = c0; customsPending = -1; saveSeq = 0; bool ok = Load();
      Chk(ok && port == mu && coins == cAfter && customsPending == mu, $"Continue after quitting on the customs card resumes at Muscat with coins after the toll {cAfter} (loaded {lastLoadedFrom}: {S.ports[port].name}, coins {coins}, customsPending {customsPending})");
      for (int k = 0; k < 400 && sailing; k++) { EgTestCloseCard(); yield return null; } /* let the live voyage coroutine finish */
      dialog = null; saveSeq = 0; ok = Load(); StartCoroutine(ResumeCustoms()); bool rc = false;
      for (int k = 0; k < 400; k++) { if (dialog != null) { rc |= dialog.GetComponentsInChildren<Text>().Any(t => t.text.StartsWith("Customs at")); EgTestCloseCard(); } else if (rc && !sailing) break; yield return null; }
      saveSeq = 0; ok = Load();
      Chk(rc && ok && port == mu && customsPending == -1 && !sailing, $"Continue replays the customs card, then docks at Muscat with nothing pending ({S.ports[port].name}, pending {customsPending})"); bans.RemoveAll(b => b.good == gp && b.port == mu && b.end == int.MaxValue / 2);
      tab = -1; Refresh(); yield return null; }
    // ---- gamedev_blockade_warning: guaranteed departure rumour, map icon after hearing, amber route preview, pay-off / let-take / greyed card ----
    { tab = -1; dialog = null; int cu = G("copper"), mu = PortIdx("Muscat"), db = PortIdx("Debal"); int c0 = coins, h0 = hold[cu]; port = db; customsPending = -1;
      blockGood[mu] = cu; blockEnd[mu] = week + 50; blockStart[mu] = week; blockKnown[mu] = false; blockHeard[mu] = null; hold[cu] = 8; Refresh(); yield return null;
      RectTransform BNamed(string nm) => FindObjectsByType<RectTransform>(FindObjectsSortMode.None).Where(r => r.gameObject.activeInHierarchy).FirstOrDefault(r => r.name == nm);
      bool AnyT(string s0) => FindObjectsByType<Text>(FindObjectsSortMode.None).Any(t => t.gameObject.activeInHierarchy && t.text.Contains(s0));
      var vars = BlkRumourVariants(); Chk(vars.Count == 3, $"3 blockade rumour variants load from v06_events_content ({vars.Count})");
      Chk(BlkS.ContainsKey("arrivalCard") && BlkRoutePreview(mu) == "Blockade at Muscat: your copper will be seized.", $"blockade_strings.json loads via Resources ({BlkRoutePreview(mu)})");
      chartCenterOn = mu; chartCentered = false; chartDest = mu; tab = 4; Refresh(); yield return null; yield return null;
      Chk(BNamed("blockade Muscat") == null, "sea map: no blockade icon before the player has heard of it");
      Chk(AnyT("Blockade at Muscat: your copper will be seized."), "route preview to Muscat with copper aboard shows the amber blockade line"); yield return Shot("blk_route_preview");
      hold[cu] = 0; Refresh(); yield return null; Chk(!AnyT("your copper will be seized"), "route preview has no blockade line when the hold has no copper"); hold[cu] = 8;
      int present = 0; for (int k = 0; k < 6; k++) { tab = -1; Refresh(); yield return null; OpenBuilding(8); yield return null; yield return null; var br = BNamed("BlockadeRumour"); if (br != null && vars.Any(v => br.GetComponent<Text>().text.Contains(v))) present++; if (k == 0) yield return Shot("blk_tavern_rumour"); }
      Chk(present == 6, $"blockade rumour is always in the departure port's tavern while active ({present}/6 opens)");
      Chk(blockKnown[mu], "hearing the tavern rumour marks the blockade as known");
      tab = -1; Refresh(); yield return null; chartCentered = false; chartDest = -1; tab = 4; Refresh(); yield return null; yield return null;
      var ic = BNamed("blockade Muscat"); Chk(ic != null && Mathf.Abs(ic.rect.width - 44) < 1 && ic.GetComponent<Image>().sprite != null, $"sea map: blockade icon shown at Muscat after hearing (44 px, sprite {(ic ? ic.GetComponent<Image>().sprite?.name : "-")})"); yield return Shot("blk_map_icon");
      if (ic) { ic.GetComponent<Button>().onClick.Invoke(); yield return null; yield return null; Chk(dialog != null && AnyT("Blockade: copper seized"), "tapping the icon shows 'Blockade: copper seized'"); yield return Shot("blk_map_plaque"); EgTestCloseCard(); yield return null; }
      blockKnown[mu] = false; bool hr = BlkHailReveal(); Chk(hr && blockKnown[mu] && msg.Contains("Soldiers search copper at Muscat"), $"Hail her warning reveals the blockade ({msg})");
      tab = -1; chartCenterOn = -1; chartCentered = false; Refresh(); yield return null;
      port = mu; blockStart[mu] = week - 1; OnDockV06(); Chk(blockPending == mu, "docking at Muscat with copper during a known blockade queues the blockade card");
      int fee = BlkFee(mu); Chk(fee == Mathf.CeilToInt(0.30f * 8 * price[mu, cu]) && fee > 0, $"fee = ceil(0.30 x copper value) = {fee}");
      coins = fee + 500; StartCoroutine(BlockadeCard()); yield return null; yield return null; yield return Shot("blk_bribe_affordable"); { var mk = seaMarks.TryGetValue("Muscat", out var mm) ? mm : null; Chk(blkFramed && customsFrame && backdrop.color.a < 0.01f && (dimmer == null || !dimmer.gameObject.activeSelf) && mk != null && mk.activeInHierarchy && sea3D.gameObject.activeSelf && (town3D == null || !town3D.gameObject.activeSelf), $"blockade_polish: arrival card sits over the Muscat harbour landmark like Customs (framed {blkFramed}, backdrop a {backdrop.color.a:0.00}, landmark {(mk ? mk.activeInHierarchy : false)})"); yield return new WaitForSeconds(0.4f); yield return Shot("polish_blk_card_harbour"); }
      var pb = Find("Pay them off (" + fee + " coins)"); Chk(pb != null && pb.interactable && AnyT("Soldiers board to seize your copper."), "arrival card: 'Blockade at Muscat' with an enabled Pay them off button");
      if (pb) pb.onClick.Invoke(); yield return null; yield return null; Chk(hold[cu] == 8 && coins == 500 && blockPending == -1, $"pay them off: fee paid, copper kept (copper {hold[cu]}, coins {coins})");
      blockPending = mu; StartCoroutine(BlockadeCard()); yield return null; yield return null; EgTestCloseCard(); yield return null; yield return null; Chk(hold[cu] == 0 && coins == 500 && msg.Contains("seize"), $"let them take it: copper seized, no coins taken (copper {hold[cu]}, coins {coins})");
      hold[cu] = 8; fee = BlkFee(mu); coins = fee - 1; blockPending = mu; StartCoroutine(BlockadeCard()); yield return null; yield return null; yield return Shot("blk_bribe_unaffordable");
      var ub = Find("Pay them off (" + fee + " coins)"); Chk(ub != null && !ub.interactable && AnyT("Not enough coins"), "unaffordable pay-off is greyed with 'Not enough coins'"); { var ubt = ub ? ub.GetComponentInChildren<Text>() : null; var rs = dialog ? dialog.GetComponentsInChildren<Text>().FirstOrDefault(t => t.gameObject.name == "OptionReason") : null; var ubr = ub ? (RectTransform)ub.transform : null; var rr = rs ? (RectTransform)rs.transform : null;
        Vector3[] bc = new Vector3[4], rc = new Vector3[4]; if (ubr) ubr.GetWorldCorners(bc); if (rr) rr.GetWorldCorners(rc);
        Chk(ubt != null && ubt.text == "Pay them off (" + fee + " coins)" && rs != null && rs.text == "Not enough coins" && rs.fontSize < ubt.fontSize && ubt.transform.GetSiblingIndex() == ubt.transform.parent.childCount - 1 && ub.GetComponent<Image>().sprite != null && ub.GetComponent<Image>().sprite.name != "ui_teak_panel" && Mathf.Abs(ub.GetComponent<Image>().color.a - GreyPlateAlpha) < 0.01f && Mathf.Abs(ub.GetComponent<Image>().pixelsPerUnitMultiplier - Mathf.Max(1, T.borderPx) / 2f) < 0.01f && ub.GetComponent<Image>().sprite.texture.GetPixel(8, 8) == UITheme.C(GreyPlateHex) && ub.GetComponent<Image>().sprite.texture.GetPixel(0, 8) == UITheme.C(GreyOutlineHex) && ub.transition == Selectable.Transition.None && Mathf.Abs(ubt.color.r - UITheme.C(GreyLabelHex).r) < 0.01f && Mathf.Abs(ubt.color.b - UITheme.C(GreyLabelHex).b) < 0.01f && Mathf.Abs(ubt.color.a - GreyLabelAlpha) < 0.01f && rc[1].y <= bc[0].y + 0.5f && Mathf.Abs((rc[0].x + rc[2].x) - (bc[0].x + bc[2].x)) < 2f, $"blockade_polish: greyed button label is exactly '{ubt?.text}', 'Not enough coins' is a smaller line under it (size {rs?.fontSize} < {ubt?.fontSize})");
        bool Fits(Button bb) { var t = bb ? bb.GetComponentInChildren<Text>() : null; if (!t) return false; Canvas.ForceUpdateCanvases(); var rt = (RectTransform)t.transform; var gen = t.cachedTextGenerator; return t.preferredWidth <= rt.rect.width + 0.5f && t.fontSize >= 14 && rt.offsetMin.x >= OptionLabelPad - 0.1f && -rt.offsetMax.x >= OptionLabelPad - 0.1f && gen.characterCountVisible >= t.text.Replace(" ", "").Length && gen.characterCountVisible > 0; }
        var tk = dialog ? dialog.GetComponentsInChildren<Button>().FirstOrDefault(x => x.GetComponentInChildren<Text>()?.text == "Let them take it") : null; Chk(Fits(ub) && Fits(tk), $"gamedev_final_strings: greyed pay label and 'Let them take it' fit inside their plates with {OptionLabelPad} px padding (one line; pay label size {ub?.GetComponentInChildren<Text>()?.fontSize}, width {ub?.GetComponentInChildren<Text>()?.preferredWidth:0} of {((RectTransform)ub?.GetComponentInChildren<Text>()?.transform)?.rect.width:0})");
{ /* gamedev_final_strings: RENDERED colours of the greyed plate and label from a real screen capture */ yield return new WaitForEndOfFrame(); var cap = ScreenCapture.CaptureScreenshotAsTexture(); var cv = ub.GetComponentInParent<Canvas>().rootCanvas; Camera ucam = cv.renderMode == RenderMode.ScreenSpaceOverlay ? null : cv.worldCamera;
          Vector3[] bw = new Vector3[4]; ((RectTransform)ub.transform).GetWorldCorners(bw); Vector2 b0 = RectTransformUtility.WorldToScreenPoint(ucam, bw[0]), b2 = RectTransformUtility.WorldToScreenPoint(ucam, bw[2]); int my2 = Mathf.RoundToInt((b0.y + b2.y) / 2);
          Color Px2(float x, float y) => cap.GetPixel(Mathf.Clamp(Mathf.RoundToInt(x), 0, cap.width - 1), Mathf.Clamp(Mathf.RoundToInt(y), 0, cap.height - 1));
          Color bgc = Px2(b0.x - 5, my2), plc = Px2(b0.x + 4, my2), want = Color.Lerp(bgc, UITheme.C(GreyPlateHex), GreyPlateAlpha);
          var lr = (RectTransform)ubt.transform; Vector3[] lw = new Vector3[4]; lr.GetWorldCorners(lw); Vector2 l0 = RectTransformUtility.WorldToScreenPoint(ucam, lw[0]), l2 = RectTransformUtility.WorldToScreenPoint(ucam, lw[2]); Color dark = Color.white, lite = Color.black;
          for (float x = l0.x; x < l2.x; x += 1) for (float y = l0.y; y < l2.y; y += 1) { var c = Px2(x, y); if (c.r + c.g + c.b < dark.r + dark.g + dark.b) dark = c; if (c.r + c.g + c.b > lite.r + lite.g + lite.b) lite = c; }
          Color wantL = Color.Lerp(plc, UITheme.C(GreyLabelHex), GreyLabelAlpha); float D(Color p, Color q) => Mathf.Max(Mathf.Abs(p.r - q.r), Mathf.Abs(p.g - q.g), Mathf.Abs(p.b - q.b)) * 255f; Color ink = GreyLabelAlpha > 0 && UITheme.C(GreyLabelHex).grayscale > UITheme.C(GreyPlateHex).grayscale ? lite : dark; /* light label on a dark plate: the brightest glyph pixel */
          var tk2 = dialog.GetComponentsInChildren<Button>().FirstOrDefault(x => x.GetComponentInChildren<Text>()?.text == "Let them take it"); Vector3[] tw = new Vector3[4]; ((RectTransform)tk2.transform).GetWorldCorners(tw); Vector2 t0 = RectTransformUtility.WorldToScreenPoint(ucam, tw[0]); Color tkc = Px2(t0.x + 6, my2); Destroy(cap);
          Chk(D(plc, want) <= 14 && D(ink, wantL) <= 18 && D(Px2(b0.x + 1, my2), plc) <= 8 /* no outline */ && plc.grayscale < tkc.grayscale /* dimmer than the active Let them take it plate */, $"gamedev_final_strings: rendered greyed plate #{ColorUtility.ToHtmlStringRGB(plc)} (want #{ColorUtility.ToHtmlStringRGB(want)} = {GreyPlateHex} at {GreyPlateAlpha} over the card), label ink #{ColorUtility.ToHtmlStringRGB(ink)} (want #{ColorUtility.ToHtmlStringRGB(wantL)} = {GreyLabelHex} at {GreyLabelAlpha}) - no disabled dimming; no outline (edge #{ColorUtility.ToHtmlStringRGB(Px2(b0.x + 1, my2))}); active plate #{ColorUtility.ToHtmlStringRGB(tkc)} lum {tkc.grayscale:0.00} vs greyed {plc.grayscale:0.00}"); }
yield return Shot("polish_blk_greyed_button"); }
      EgTestCloseCard(); yield return null; yield return null; Chk(hold[cu] == 0 && coins == fee - 1, "greyed card still lets them take it");
      blockGood[mu] = -1; blockKnown[mu] = false; blockPending = -1; port = db; coins = c0; hold[cu] = h0; tab = -1; dialog = null; Refresh(); yield return null; }
    // ---- gamedev_continue: blockade seizure on docking is never silent; HUD Next shows the urgent contract ----
    { tab = -1; dialog = null; int cu = G("copper"), mu = PortIdx("Muscat"); port = PortIdx("Debal"); hold[cu] = 8; blockGood[mu] = cu; blockEnd[mu] = week + 50; blockStart[mu] = -1000; blockKnown[mu] = true; safeUntilSail = sails + 100; Refresh(); yield return null;
      StartCoroutine(Voyage(mu)); bool card = false; float tq = Time.realtimeSinceStartup;
      while (Time.realtimeSinceStartup - tq < 90f) { skipVoyage = true; ff = true; if (blockGood[mu] != cu) blockGood[mu] = cu;
        if (dialog != null) { if (dialog.GetComponentsInChildren<Text>().Any(t => t.text.StartsWith("Blockade at Muscat"))) { card = true; break; } EgTestCloseCard(); }
        yield return null; }
      yield return Shot("cont_blockade_card"); if (card) { EgTestCloseCard(); yield return null; } /* gamedev_blockade_warning: Let them take it */ Chk(card && hold[cu] == 0, $"blockade seizure on docking shows a 'Blockade at Muscat' card (copper now {hold[cu]})");
      for (int k = 0; k < 40 && sailing; k++) { EgTestCloseCard(); yield return null; } blockGood[mu] = -1;
      Chk(msg.Contains("seize"), $"after the card the dock message still names the seizure ({msg})");
      if (eg.active.Count == 0) { coins += 5000; EgRefreshBoards(); foreach (var b in eg.board.ToList()) { if (eg.active.Count >= 1) break; EgAccept(b); } }
      var u = eg.active.OrderBy(c => c.dueWeek).FirstOrDefault(); var g3 = Goals3();
      Chk(u != null && g3.Item2.Contains($"{S.goods[u.good].name} to {u.to}") && g3.Item2.Contains(EgMoonsColor(u)) && !g3.Item2.Contains("Journal empty"), $"HUD Next shows the most urgent contract in its deadline colour ({g3.Item2})");
      tab = -1; Refresh(); yield return null; }
    // ---- research_gp1: gunpowder goal survives the Journal unlock, flags Muscat, clears on the Muscat alley sale ----
    { int ss0 = storyStep, gp0 = hold[Gp], c0 = coins, pt0 = port; bool sold0 = openingGpSold, fb0 = fallbackFired, fr0 = firstRun; float pr0 = price[Muscat, Gp]; int rn0 = eg.renown, rk0 = eg.rank; var fs0 = eg.firstSales.ToList();
      storyStep = StDone; openingGpSold = false; fallbackFired = false; hold[Gp] = 0;
      { string gpId = S.goods[Gp].id; Chk(Goals3().Item1 == GpBuyGoal && GpBuyGoal == "Buy gunpowder from Farida in Debal's Back Alley.", $"gunpowder buy goal shown before buying (Now: {Goals3().Item1})"); /* research_gp2 */
        Chk(AlleySells(Debal).Contains(gpId), "Farida (Debal Back Alley) sells gunpowder before the Muscat sale");
        Chk(A1s("goalMediumOpening", "") == "Get gunpowder from Debal to Muscat's Back Alley.", $"goalMediumOpening from built data ({A1s("goalMediumOpening", "")})"); }
      hold[Gp] = 2;
      Chk(GpSellGoal == "Sell the gunpowder in Muscat's Back Alley.", "gunpowder sell goal text");
      Chk(JournalUnlocked && Goals3().Item1 == GpSellGoal, $"gunpowder goal shown after Journal unlock (Now: {Goals3().Item1})");
      tab = 4; chartDest = -1; chartCentered = false; Refresh(); yield return null; yield return null; yield return Shot("gp_map_flag");
      var flags = FindObjectsByType<RectTransform>(FindObjectsSortMode.None).Where(r => r.name.StartsWith("egflag ")).Select(r => r.name.Substring(7)).ToList();
      var gpOnly = flags.Where(f => EgMapFlag(PortIdx(f)) == null).ToList(); Chk(flags.Contains("Muscat") && gpOnly.All(f => f == "Muscat"), $"sea map flag on Muscat while gunpowder is held; other flags are contracts only (flags: {string.Join(",", flags)})");
      tab = -1; port = Muscat; Refresh(); yield return null; Sell(Gp); Sell(Gp);
      Chk(openingGpSold && Goals3().Item1 != GpSellGoal && msg == GpSoldLine, $"gunpowder goal clears on the Muscat sale (Now: {Goals3().Item1}; msg: {msg})");
      Chk(GpMapFlag(Muscat) == null, "Muscat gunpowder flag gone after the sale"); Chk(!AlleySells(Debal).Contains(S.goods[Gp].id), "Farida stops selling gunpowder after the Muscat sale"); Chk(Goals3().Item1 != GpBuyGoal, "no gunpowder buy goal after the sale"); /* research_gp2 */
      /* research_gp3: text follow-ups */
      Chk(QarinPowderLine.Contains("My thanks") && !QarinPowderLine.Contains("Bring powder"), $"Qarin letter thanks you once gunpowder is sold ({QarinPowderLine})");
      { int ab0 = tab; port = Muscat; tab = 1; keeperTrig = "greet"; Refresh(); yield return null; yield return null;
        var tx = FindObjectsByType<Text>(FindObjectsSortMode.None).Select(t => t.text).ToList();
        Chk(MuscatFence == "Salim the fence" && tx.Any(t => t.Contains(MuscatFence)) && !tx.Any(t => t.Contains("Farida of the back alley")), "Muscat Back Alley keeper is Salim the fence, not Farida");
        Chk(Line("salim", "greet").text == "Powder? Quietly, captain. The garrison pays well." && Line("salim", "sold").text == "Pleasure doing business. Forget my face.", "Salim has his own greet and after-sale lines"); /* research_gp4 */
        tab = ab0; Refresh(); yield return null; }
      /* research_gp5: Salim's after-sale line on the REAL path: Sell all button in Muscat's alley, re-entry, save + reload */
      { const string SGreet = "Powder? Quietly", SSold = "Forget my face"; bool hadBan = Banned(Gp, Muscat); var qBan = new Ban { good = Gp, port = Muscat, start = 0, end = int.MaxValue / 2 }; if (!hadBan) bans.Add(qBan);
        System.Func<List<string>> shown = () => FindObjectsByType<Text>(FindObjectsSortMode.None).Where(t => t.isActiveAndEnabled).Select(t => t.text).ToList();
        openingGpSold = false; hold[Gp] = 3; port = Muscat; tab = -1; Refresh(); yield return null; OpenBuilding(1); yield return null; yield return null;
        var t0 = shown(); Chk(t0.Any(t => t.Contains(SGreet)) && !t0.Any(t => t.Contains(SSold)), "Salim greets before the sale (real alley open)");
        var sb = LiveButtons().FirstOrDefault(b => LabelOf(b) == "Sell all" && b.transform.parent.GetComponentsInChildren<Text>().Any(t => t.text == S.goods[Gp].name));
        Chk(sb != null, "Sell all button for gunpowder in Muscat's alley"); if (sb != null) sb.onClick.Invoke(); yield return null; yield return null;
        var t1 = shown(); Chk(openingGpSold && hold[Gp] == 0 && t1.Any(t => t.Contains(SSold)) && !t1.Any(t => t.Contains(SGreet)), $"after Sell all Salim shows the after-sale line (sold {openingGpSold}, held {hold[Gp]})");
        tab = -1; Refresh(); yield return null; OpenBuilding(1); yield return null; yield return null;
        var t2 = shown(); Chk(t2.Any(t => t.Contains(SSold)) && !t2.Any(t => t.Contains(SGreet)), "re-entering the alley: after-sale line, no greeting");
        tab = -1; Refresh(); Save(); yield return null; openingGpSold = false; bool ld = Load(); Refresh(); yield return null; port = Muscat; OpenBuilding(1); yield return null; yield return null;
        var t3 = shown(); Chk(ld && openingGpSold && t3.Any(t => t.Contains(SSold)) && !t3.Any(t => t.Contains(SGreet)), $"after save + reload: after-sale line, no greeting (loaded {ld}, sold {openingGpSold})");
        tab = -1; if (!hadBan) bans.RemoveAll(b => b.good == Gp && b.port == Muscat && b.end == int.MaxValue / 2); Refresh(); yield return null; }
      openingGpSold = false; fallbackFired = false; hold[Gp] = 0;
      Chk(OpeningGoalLineAt(4) == GpBuyGoal, $"opening step 4 goal before buying ({OpeningGoalLineAt(4)})");
      hold[Gp] = 2;
      Chk(OpeningGoalLineAt(4) == GpSellGoal && OpeningGoalLineAt(5) == GpSellGoal && OpeningGoalLineAt(6) == GpSellGoal, $"opening steps 4-6 goal with kegs aboard ({OpeningGoalLineAt(5)})");
      Chk(GpBuyGoal.Length <= 50 && GpSellGoal.Length <= 50 && !JournalOpeningGoals.Any(j => j.Contains("then sail for Muscat") || j.Contains("Keep it from customs")), "opening gunpowder lines are the short ruled texts (fit one line)");
      Chk(OpeningMediumGoalAt(4) == "Get gunpowder from Debal to Muscat's Back Alley." && OpeningMediumGoalAt(6) == OpeningMediumGoalAt(4) && !OpeningMediumGoals.Any(m => m.Contains("Make a fortune")), $"opening medium goal is the ruled text ({OpeningMediumGoalAt(5)})");
      storyStep = ss0; hold[Gp] = gp0; coins = c0; port = pt0; openingGpSold = sold0; fallbackFired = fb0; firstRun = fr0; price[Muscat, Gp] = pr0; msg = ""; eg.renown = rn0; eg.rank = rk0; eg.firstSales.Clear(); eg.firstSales.AddRange(fs0); if (news.Count > 0 && news[0] == GpSoldLine) news.RemoveAt(0); Refresh(); yield return null; }
    // ---- gamedev_ring: naval range ring (radius = fire range, state flips at the threshold, plaque = fire-check distance, under the hulls) ----
    { tab = -1; dialog = null; Refresh(); yield return null; bool town0 = town3D && town3D.gameObject.activeSelf; if (town3D) town3D.gameObject.SetActive(false); var amb0 = RenderSettings.ambientLight; RenderSettings.ambientLight = new Color(0.40f, 0.46f, 0.58f); /* at sea, as Voyage() sets it */ StartCoroutine(Fight(true)); for (int k = 0; k < 6; k++) yield return null; combatPaused = true;
      var H = core.H; var P = core.P; H.heading = 0; H.rudder = 0; H.reloadT = 0; H.ammo = 0; P.heading = 180;
      IEnumerator At(float d) { P.x = H.x + d; P.z = H.z; for (int k = 0; k < 3; k++) yield return null; }
      bool radiusOk = true, flipOk = true, plaqueOk = true; string why = "";
      for (int a = 0; a < 4; a++) { H.ammo = a; float rg = core.FireRange(H);
        yield return At(rg + 25f); float sideR = rangeRing.localScale.x * RingStrokePx / RingSpritePx;
        if (Mathf.Abs(ringRadiusM - rg) > 1e-4f || Mathf.Abs(ringRadiusWorld - rg * K) > 1e-3f || Mathf.Abs(sideR - rg * K) > 1e-3f || Mathf.Abs((rangeMarker.position - new Vector3(rangeRing.position.x, rangeMarker.position.y, rangeRing.position.z)).magnitude - rg * K) > 1e-2f) { radiusOk = false; why += $" r{a}:{ringRadiusM}/{rg}"; }
        yield return At(rg + 0.02f); bool o1 = !ringInRange && !core.InFireRange(H, P); int pl = ringPlaqueN; bool pt = rangePlaqueTxt.text.StartsWith($"Range {Mathf.CeilToInt(core.FireDist(H, P))} m · ");
        if (pl != Mathf.CeilToInt(core.FireDist(H, P)) || !pt || pl <= Mathf.FloorToInt(rg) && rg == Mathf.Floor(rg)) { plaqueOk = false; why += $" p{a}:{rangePlaqueTxt.text}/{core.FireDist(H, P):0.00}"; }
        if ((H.ammo != 3 || H.fireLeft > 0) && core.InArc(H, P) && H.gunHp > 0) { var fbr = FireBlockReason(); if (fbr != $"Out of range: {Mathf.CeilToInt(core.FireDist(H, P))} m, {S.combat.ammo[a].name} reaches {rg:0} m") { plaqueOk = false; why += $" msg{a}:{fbr}"; } }
        int pulses0 = ringPulses; yield return At(rg - 0.02f); bool i1 = ringInRange && core.InFireRange(H, P) && rangePlaqueTxt.text == "In range" && ringPulses == pulses0 + 1 && fireRangeBorder.gameObject.activeSelf;
        yield return At(rg - 5f); bool i2 = ringInRange && ringPulses == pulses0 + 1; /* one pulse per entry */
        if (!(o1 && i1 && i2)) { flipOk = false; why += $" f{a}:{o1}/{i1}/{i2}"; } L($"ring ammo {S.combat.ammo[a].name}: range {rg} m, world radius {rg * K:0.00}"); }
      H.ammo = 0; float r0 = core.FireRange(H);
      Chk(radiusOk, "range ring radius (ring, sprite stroke, marker) equals the loaded shot's fire range, all 4 shots" + why);
      Chk(flipOk, "range ring flips dashed->solid exactly at the fire-range threshold (+/-0.02 m), one pulse per entry, FIRE border in range");
      Chk(plaqueOk, "plaque number = ceil(fire-check distance), same distance and range figures as 'Out of range: N m, X reaches R m'");
      { H.reloadT = 0; yield return At(179.6f); string m180 = FireBlockReason(); Chk(core.InArc(H, P) && m180 == "Out of range: 180 m, Round reaches 120 m" && m180 == OutOfRangeMsg() && ringPlaqueN == 180, $"gamedev_ambush: out-of-range FIRE message shows the plaque distance and the shot's reach ('{m180}', plaque {ringPlaqueN})"); }
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
      yield return At(r0 + 30f); yield return new WaitForSeconds(0.4f); yield return Shot("ring_out_of_range"); yield return At(r0 + 8f); yield return new WaitForSeconds(0.2f); yield return Shot("ring_out_of_range_near"); yield return At(r0 - 20f); yield return new WaitForSeconds(0.1f); yield return Shot("ring_entering_pulse"); yield return new WaitForSeconds(0.6f); yield return Shot("ring_in_range");
      P.hull = 0; combatPaused = false; float tq = Time.realtimeSinceStartup; while (fighting && Time.realtimeSinceStartup - tq < 90f) { if (dialog != null) EgTestCloseCard(); yield return null; }
      Chk(!fighting && (rangeRing == null || !rangeRing.gameObject.activeSelf), "after the fight the range ring is hidden"); if (town3D) town3D.gameObject.SetActive(town0); RenderSettings.ambientLight = amb0; tab = -1; Refresh(); yield return null; }
    // ---- gamedev_ambush: fog-ambush approach (Game Designer ruling: ~140 m start, 120 m in ~8-10 s at full sail, 30 s idle toll, both numbers in the message) ----
    { var cc = S.combat; float st = cc.storyStartRange; bool startOk = true; float tMin = 99, tMax = 0; bool idleClose = true; string sw = "";
      for (int sd = 0; sd < 20; sd++) { var q = new CombatCore(cc, 1000 + sd, false, 0, difficulty); q.SetupStoryApproach(st); q.idleTollSeconds = cc.storyIdleTollSeconds;
        if (Mathf.Abs(q.Range - 140f) > 0.5f || q.InFireRange(q.H, q.P)) { startOk = false; sw += $" s{sd}:{q.Range:0.0}"; }
        float tr = -1; for (int i = 0; i < 1500 && q.outcome == Outcome.None; i++) { q.H.rudder = CombatCore.Steer(q.H, CombatCore.Bearing(q.P.x - q.H.x, q.P.z - q.H.z)); q.H.fullSail = true; q.Step(0.02f); if (q.Range <= cc.ammo[0].range) { tr = q.t; break; } }
        if (tr < 0) tr = 99; tMin = Mathf.Min(tMin, tr); tMax = Mathf.Max(tMax, tr); if (q.idleT > 0.1f) idleClose = false; }
      L($"ambush approach: full sail at the pirate reaches {cc.ammo[0].range:0} m in {tMin:0.00}-{tMax:0.00} s (20 seeds)");
      Chk(startOk, $"gamedev_ambush: the fog-ambush pirate appears at 140 m, outside Round range (storyStartRange {st}){sw}");
      Chk(tMax <= 10f && tMin >= 5f, $"gamedev_ambush: full sail toward the pirate reaches Round range within 10 s ({tMin:0.00}-{tMax:0.00} s)");
      Chk(idleClose, "gamedev_ambush: the toll clock does not advance while closing in");
      // idle clock on still ships, closing ships and a firing ship
      var z = new CombatCore(cc, 7, false, 0, difficulty); z.SetupStoryApproach(st); z.idleTollSeconds = cc.storyIdleTollSeconds; float tf = -1, tt = 0;
      for (int i = 0; i < 2000; i++) { tt += 0.02f; if (z.IdleTick(0.02f)) { tf = tt; break; } }
      var y = new CombatCore(cc, 8, false, 0, difficulty); y.SetupStoryApproach(st); y.idleTollSeconds = cc.storyIdleTollSeconds; bool yFired = false; float k0 = 0;
      for (int i = 0; i < 2000; i++) { y.P.x -= y.P.x / Mathf.Max(1f, y.Range) * 0.02f; y.P.z -= y.P.z / Mathf.Max(1f, y.Range) * 0.02f; if (y.IdleTick(0.02f)) yFired = true; } k0 = y.idleT; /* 40 s of closing at 1 m/s */
      var w = new CombatCore(cc, 9, false, 0, difficulty); w.SetupStoryApproach(st); w.idleTollSeconds = cc.storyIdleTollSeconds; bool wFired = false; float wReload = w.H.st.reload;
      for (int i = 0; i < 2000; i++) { if (i % Mathf.RoundToInt(wReload * 0.75f / 0.02f) == 0) w.Fire(w.H, w.P); if (w.IdleTick(0.02f)) wFired = true; } /* 40 s of steady firing, no movement */
      var v = new CombatCore(cc, 10, false, 0, difficulty); v.SetupStoryApproach(st); v.idleTollSeconds = cc.storyIdleTollSeconds; v.Fire(v.H, v.P); float vi = -1; for (int i = 0; i < 2000; i++) { if (v.IdleTick(0.02f)) { vi = (i + 1) * 0.02f; break; } } /* one shot, then idle */
      L($"ambush toll clock: still ships toll at {tf:0.00} s; 40 s closing idle {k0:0.00} s; 40 s firing idle {w.idleT:0.00} s; one shot then still: toll at {vi:0.00} s (reload {wReload})");
      Chk(tf > 29.9f && tf < 30.1f, $"gamedev_ambush: with no closing and no firing the toll fires after 30 s ({tf:0.00} s)");
      Chk(!yFired && k0 < 0.05f, $"gamedev_ambush: toll clock paused while the gap shrinks (40 s closing, idle {k0:0.00} s)");
      Chk(!wFired && w.idleT < 0.05f && Mathf.Abs(vi - (30f + wReload + 0.5f)) < 0.1f, $"gamedev_toll_fix: a shot resets the idle clock and holds it for a reload + 0.5 s (40 s firing idle {w.idleT:0.00} s; one shot then still tolls at {vi:0.00} s)");
      var u = new CombatCore(cc, 11, false, 0, difficulty); u.SetupStoryApproach(st); u.idleTollSeconds = cc.storyIdleTollSeconds; for (int i = 0; i < 5000 && u.outcome == Outcome.None; i++) { u.H.rudder = CombatCore.Steer(u.H, CombatCore.Bearing(u.H.x - u.P.x, u.H.z - u.P.z)); u.Step(0.02f); }
      Chk(u.outcome == Outcome.HasanStruck && u.idleTollFired && u.t >= 30f, $"gamedev_ambush: sailing away ends the story fight with the toll once 30 idle seconds add up (outcome {u.outcome}, fight {u.t:0.0} s, idle {u.idleT:0.0} s)"); }
    // ---- gamedev_ambush: hand-style fog ambush in the live game: steer at the pirate at full sail through the real rudder input, solid ring, FIRE button, hit ----
    { tab = -1; dialog = null; Refresh(); yield return null; bool town0 = town3D && town3D.gameObject.activeSelf; if (town3D) town3D.gameObject.SetActive(false); var amb0 = RenderSettings.ambientLight; RenderSettings.ambientLight = new Color(0.40f, 0.46f, 0.58f);
      bool af0 = ambushForced, sad0 = storyAmbushDone, bsp0 = boardedStoryPirate; ambushForced = true; StartCoroutine(Fight(true)); yield return null; yield return null;
      var H = core.H; var P = core.P; float r0 = core.Range; H.ammo = 0; H.aim = Part.Hull; H.fullSail = true; bool solid = false, hit = false, cancel = false; float tIn = -1, tHit = -1, hull0 = P.hull; int phase = 0; string lastMsg = "";
      core.OnBroadside += (a, b, shots, hits) => { if (a == core.H && hits > 0 && !hit) { hit = true; tHit = core.t; } };
      yield return Shot("ambush_hand_0_start"); float rq = Time.realtimeSinceStartup;
      while (fighting && core.outcome == Outcome.None && core.t < 40f && !hit && Time.realtimeSinceStartup - rq < 120f) {
        float br = CombatCore.Bearing(P.x - H.x, P.z - H.z);
        if (!ringInRange && tIn < 0) qaRudder = CombatCore.Steer(H, br); else { if (tIn < 0) { tIn = core.t; solid = ringInRange && rangeRingMat.mainTexture == rangeTexSolid && rangePlaqueTxt.text == (core.InArc(H, P) ? "In range" : "In range · Turn broadside") && fireRangeBorder.gameObject.activeSelf == core.InArc(H, P); /* gamedev_arc: border only when the guns bear */ } qaRudder = CombatCore.Steer(H, CombatCore.Norm(br - CombatCore.Side(H, br))); /* turn her abeam */ }
        if (phase == 0 && core.t > 3f) { phase = 1; yield return Shot("ambush_hand_1_closing"); }
        if (phase == 1 && tIn >= 0) { phase = 2; yield return Shot("ambush_hand_2_in_range"); }
        if (tIn >= 0 && core.CanFire(H, P) && !firePressed) { firePressed = true; /* the FIRE button's own path */ }
        else if (phase == 1 && !firePressed && core.t > 4f && !cancel) { cancel = true; firePressed = true; yield return null; lastMsg = hudMsg.text; }
        yield return null; }
      qaRudder = float.NaN; yield return new WaitForSeconds(0.3f); yield return Shot("ambush_hand_3_hit");
      L($"ambush hand run: start {r0:0.0} m, in Round range at {tIn:0.00} s (fight time), first hit at {tHit:0.00} s, pirate hull {hull0:0}->{P.hull:0}, early FIRE said '{lastMsg}', idle {core.idleT:0.00} s, outcome {core.outcome}");
      Chk(Mathf.Abs(r0 - 140f) < 0.5f, $"gamedev_ambush: live fog ambush starts at 140 m ({r0:0.0})");
      Chk(tIn > 0 && tIn <= 10f && solid, $"gamedev_ambush: by hand (rudder input) the ring turns solid with 'In range' and the brass FIRE border within 10 s ({tIn:0.00} s)");
      Chk(hit && P.hull < hull0, $"gamedev_ambush: by hand the FIRE button lands a hit once in range (hit at {tHit:0.00} s, hull {hull0:0}->{P.hull:0})");
      Chk(lastMsg.StartsWith("Out of range: ") && lastMsg.Contains(", Round reaches 120 m") || lastMsg.StartsWith("Not in arc"), $"gamedev_ambush: FIRE before range in the live fight explains why (both numbers when abeam, else the arc hint) ('{lastMsg}')");
      P.hull = 0; float tq = Time.realtimeSinceStartup; while (fighting && Time.realtimeSinceStartup - tq < 90f) { if (dialog != null) EgTestCloseCard(); yield return null; }
      ambushForced = af0; storyAmbushDone = sad0; boardedStoryPirate = bsp0; if (town3D) town3D.gameObject.SetActive(town0); RenderSettings.ambientLight = amb0; tab = -1; Refresh(); yield return null; }
    // ---- gamedev_rudder: rudder stays where the drag leaves it, snaps to 0 near centre, one-time story hint, drag zones ----
    { tab = -1; dialog = null; Refresh(); yield return null; bool town0 = town3D && town3D.gameObject.activeSelf; if (town3D) town3D.gameObject.SetActive(false); var amb0 = RenderSettings.ambientLight; RenderSettings.ambientLight = new Color(0.40f, 0.46f, 0.58f);
      bool af0 = ambushForced, sad0 = storyAmbushDone, bsp0 = boardedStoryPirate, shs0 = steerHintSeen;
      steerHintSeen = false; Chk(!ShowSteerHint(false) && ShowSteerHint(true), "gamedev_rudder: the steer hint is for the story fog fight only (not ordinary fights)");
      steerHintSeen = false; ambushForced = true; StartCoroutine(Fight(true)); yield return null; yield return null; yield return new WaitForSeconds(0.4f);
      bool hint1 = SteerHintVisible; string hintTxt = steerHint ? steerHint.GetComponentInChildren<Text>().text : ""; yield return Shot("rudder_0_hint");
      Chk(hint1 && hintTxt == "Drag to steer toward her." && steerHintSeen, $"gamedev_rudder: story fight shows 'Drag to steer toward her.' and marks it seen ('{hintTxt}')");
      core.H.rudder = 0; float W = Screen.width, Hh = Screen.height; var sea = new Vector2(W * 0.40f, Hh * 0.33f);
      var bar = rudderBar.rectTransform.parent as RectTransform; var cv = rudderBar.canvas; Camera ccam = cv.renderMode == RenderMode.ScreenSpaceOverlay ? null : cv.worldCamera; Vector2 barP = RectTransformUtility.WorldToScreenPoint(ccam, bar.position);
      int zones = 0; string zl = ""; foreach (var zp in new[] { sea, barP, new Vector2(W * 0.5f, Hh * 0.45f), new Vector2(W * 0.2f, Hh * 0.40f), new Vector2(W * 0.6f, Hh * 0.28f) }) { bool ok = SteerStartsAt(zp); if (ok) zones++; zl += $"({zp.x:0},{zp.y:0})={(ok ? "drag" : "blocked")}[{lastSteerHit}] "; }
      L("rudder drag zones: " + zl);
      Chk(SteerStartsAt(sea) && SteerStartsAt(barP), $"gamedev_rudder: a drag starts on the open sea and on the rudder bar itself, no dead zone there ({zl})");
      // drag right ~0.15 W, release, hold
      qaPtrOn = true; qaPtrPos = sea; qaPtrDown = true; yield return null; yield return null;
      for (int i = 1; i <= 10; i++) { qaPtrPos = sea + new Vector2(W * 0.015f * i, 0); yield return null; }
      bool hint2 = SteerHintVisible; float rDrag = core.H.rudder; qaPtrDown = false; yield return null; yield return null; yield return new WaitForSeconds(0.6f);
      float rHeld = core.H.rudder; yield return Shot("rudder_1_held_after_release");
      Chk(!hint2, "gamedev_rudder: the hint hides on the first drag");
      Chk(rDrag > 0.5f && Mathf.Abs(rHeld - rDrag) < 0.001f, $"gamedev_rudder: rudder holds its value after the finger lifts ({rDrag:0.000} -> {rHeld:0.000} after 0.6 s)");
      // second drag continues from the held value (no jump), then back to near centre and release -> snaps to 0
      qaPtrPos = sea; qaPtrDown = true; yield return null; yield return null; float rCont = core.H.rudder;
      float back = -(rHeld - 0.07f) * W * 0.22f; for (int i = 1; i <= 10; i++) { qaPtrPos = sea + new Vector2(back * i / 10f, 0); yield return null; }
      float rNear = core.H.rudder; qaPtrDown = false; yield return null; yield return null; float rSnap = core.H.rudder;
      Chk(Mathf.Abs(rCont - rHeld) < 0.001f, $"gamedev_rudder: a new drag picks the rudder up where it was left ({rHeld:0.000} -> {rCont:0.000})");
      Chk(Mathf.Abs(rNear) < RudderSnapBand && Mathf.Abs(rNear) > 0.01f && rSnap == 0f, $"gamedev_rudder: released near centre ({rNear:0.000}) the rudder snaps to exactly 0 ({rSnap})");
      // a tap on a button (FIRE) never starts a steer
      var fireR = fireTxt.transform.parent as RectTransform; Vector2 fireP = RectTransformUtility.WorldToScreenPoint(ccam, fireR.position);
      Chk(!SteerStartsAt(fireP), $"gamedev_rudder: pressing FIRE does not grab the rudder ({fireP.x:0},{fireP.y:0})");
      // ---- gamedev_arc: FIRE lit only with range AND arc; 'In range · Turn broadside'; arc wedges; FIRE tap in range resets the idle clock ----
      { qaPtrOn = false; var H = core.H; var P = core.P; float arcD = core.C.arcDeg, rg = core.FireRange(H); H.ammo = 0; H.reloadT = 0;
        IEnumerator Place(float relDeg, float dist, int frames) { for (int f = 0; f < frames; f++) { H.x = H.z = 0; H.heading = 0; H.rudder = 0; H.reloadT = 0; P.x = Mathf.Sin(relDeg * Mathf.Deg2Rad) * dist; P.z = Mathf.Cos(relDeg * Mathf.Deg2Rad) * dist; yield return null; } }
        yield return Place(0, rg * 0.5f, 4); bool ahIn = core.InFireRange(H, P), ahArc = core.InArc(H, P); string plA = rangePlaqueTxt.text; bool borderA = fireRangeBorder.gameObject.activeSelf, canA = core.CanFire(H, P); Color fbA = fireBg.color; Color ftA = fireTxt.color;
        core.idleT = 20f; QaTapFire(); yield return Place(0, rg * 0.5f, 1); float idleAfter = core.idleT; string msgA = hudMsg.text;
        yield return Place(0, rg * 0.5f, 2); yield return Shot("rudder_2_wedges_turn_broadside");
        Chk(ahIn && !ahArc && plA == "In range · Turn broadside" && !borderA && !canA && ftA == UITheme.C(T.textMuted) && fbA != UITheme.C(T.button), $"gamedev_arc: in range but guns not bearing: plaque '{plA}', no brass border, FIRE dim");
        Chk(idleAfter < 0.5f && msgA.StartsWith("Not in arc"), $"gamedev_arc: a FIRE tap in range resets the idle toll clock even out of arc (20.0 -> {idleAfter:0.00} s, msg '{msgA}')");
        yield return Place(90, rg * 0.5f, 4); bool bsArc = core.InArc(H, P), canB = core.CanFire(H, P); string plB = rangePlaqueTxt.text; bool borderB = fireRangeBorder.gameObject.activeSelf; Color fbB = fireBg.color, ftB = fireTxt.color;
        Chk(bsArc && canB && plB == "In range" && borderB && ftB == UITheme.C(T.gold) && fbB == UITheme.C(T.button), $"gamedev_arc: in range and abeam: plaque '{plB}', brass border on, FIRE lit");
        // single source: FIRE lit == CanFire == InArc && InFireRange at sampled bearings
        bool src = true; string sw = ""; foreach (float b in new[] { 30f, 39f, 41f, 60f, 139f, 141f, 200f, 270f, 320f, 330f }) { yield return Place(b, rg * 0.5f, 2); bool lit = fireTxt.color == UITheme.C(T.gold), bord = fireRangeBorder.gameObject.activeSelf; bool want = core.InArc(H, P) && core.InFireRange(H, P); if (lit != want || bord != want || core.CanFire(H, P) != want) { src = false; sw += $" {b}:{lit}/{bord}/{want}"; } }
        Chk(src, $"gamedev_arc: FIRE and its border follow CanFire/InArc exactly at bearings 30..330 (arc {arcD:0}°){sw}");
        float wIn1 = ArcWedgeAlphaAt(90 - arcD + 3, 0.6f), wIn2 = ArcWedgeAlphaAt(270 + arcD - 3, 0.6f), wOut1 = ArcWedgeAlphaAt(90 - arcD - 4, 0.6f), wOut2 = ArcWedgeAlphaAt(0, 0.6f), wOut3 = ArcWedgeAlphaAt(90, 1.05f), wPort = ArcWedgeAlphaAt(270, 0.6f), wStb = ArcWedgeAlphaAt(90, 0.6f);
        bool wPose = arcWedges && arcWedges.gameObject.activeSelf && Mathf.Abs(arcWedges.localScale.x - 2f * rg * K / ArcTexFill) < 0.01f && Mathf.Abs(Mathf.DeltaAngle(arcWedges.eulerAngles.y, H.heading)) < 0.5f && arcWedgeMat.shader.name == "Galeborne/RangeRing" && arcWedgeMat.GetColor("_Color") == Color.white; /* art_arc_restyle: colours baked in the texture */
        Chk(wPose && wIn1 > 0.05f && wIn2 > 0.05f && wPort > 0.05f && wStb > 0.05f && wOut1 < 0.02f && wOut2 < 0.02f && wOut3 < 0.02f, $"gamedev_arc: port and starboard wedges match the true arc (±{arcD:0}° off the beam, radius = {rg:0} m, RangeRing shader at ring opacity) in {wIn1:0.00}/{wIn2:0.00} out {wOut1:0.00}/{wOut2:0.00}/{wOut3:0.00}");
        { /* art_arc_restyle */ Color brA = UITheme.C(ArcEdgeHex); bool Near(float x, float y, float tol = 0.012f) => Mathf.Abs(x - y) <= tol; bool Brass(Color c) => Near(c.r, brA.r) && Near(c.g, brA.g) && Near(c.b, brA.b);
          yield return Place(0, rg * 0.5f, 3); float sc = Screen.width / 1080f, wantE = ArcEdgePx1080 * sc * ArcTexN / Mathf.Max(1f, arcWedgeScreenPx);
          Color e0 = ArcEdgeSample(0), e1 = ArcEdgeSample(1), f0 = ArcFillSample(0), f1 = ArcFillSample(1);
          Chk(arcActiveSide == -1 && Brass(e0) && Brass(e1) && Brass(f0) && Brass(f1) && Near(e0.a, ArcEdgeAlpha) && Near(e1.a, ArcEdgeAlpha) && Near(f0.a, ArcFillAlpha) && Near(f1.a, ArcFillAlpha) && Near(ArcFillSample(0).a, ArcWedgeColorAt(90 + arcD * 0.5f, 0.3f).a),
            $"art_arc_restyle: out of arc both wedges at base: brass {ArcEdgeHex} edge a {e0.a:0.00}/{e1.a:0.00} (want {ArcEdgeAlpha}), flat fill a {f0.a:0.00}/{f1.a:0.00} (want {ArcFillAlpha})");
          Chk(Mathf.Abs(arcEdgeTexels - Mathf.Clamp(Mathf.Round(wantE * 2) / 2, 1, 48)) < 0.01f && arcOutlineTexels > 0, $"art_arc_restyle: edge {ArcEdgePx1080} px at 1080 scales with screen ({Screen.width} px wide, wedge {arcWedgeScreenPx:0} px → edge {arcEdgeTexels} texels, outline {arcOutlineTexels})");
          yield return Shot("arc_out_of_arc");
          yield return Place(90, rg * 0.5f, 3); e0 = ArcEdgeSample(0); e1 = ArcEdgeSample(1); f0 = ArcFillSample(0); f1 = ArcFillSample(1);
          Chk(core.InArc(H, P) && core.InFireRange(H, P) && arcActiveSide == 0 && Brass(e0) && Brass(f0) && Near(e0.a, ArcActiveEdgeAlpha) && Near(f0.a, ArcActiveFillAlpha) && Mathf.Abs(arcActiveEdgeTexels - Mathf.Clamp(Mathf.Round(ArcActiveEdgePx1080 * Screen.width / 1080f * ArcTexN / Mathf.Max(1f, arcWedgeScreenPx) * 2f) / 2f, 1f, 64f)) < 0.01f && arcActiveEdgeTexels >= arcEdgeTexels && /* same scaling rule as the base edge (1-texel floor) */ Near(e1.a, ArcEdgeAlpha) && Near(f1.a, ArcFillAlpha) && Brass(e1) && Brass(f1),
            $"art_arc_restyle: ready edge {ArcActiveEdgePx1080 / 2:0} px at 540 ({arcActiveEdgeTexels} texels vs base {arcEdgeTexels}); in range and arc the pirate's (starboard) wedge is boosted edge {e0.a:0.00} fill {f0.a:0.00}, the port wedge stays {e1.a:0.00}/{f1.a:0.00}, brass only (no green)");
          yield return Shot("arc_in_arc_boosted");
          yield return Place(90, rg * 1.6f, 3); Chk(arcActiveSide == -1 && Near(ArcEdgeSample(0).a, ArcEdgeAlpha), "art_arc_restyle: abeam but out of range: no boost"); }
        yield return Place(70, rg * 0.5f, 3); yield return Shot("rudder_3_wedges_in_arc");
        { /* gamedev_msgprio: 137 m bow-on (out of arc AND out of range): the range message wins */ yield return Place(0, 137f, 2); QaTapFire(); yield return null; yield return null; string m137 = hudMsg.text;
          Chk(!core.InArc(H, P) && System.Text.RegularExpressions.Regex.IsMatch(m137, "^Out of range: 13[78] m, " + S.combat.ammo[0].name + " reaches " + core.FireRange(H).ToString("0") + " m$") /* she drifts a fraction of a metre between placement and the tap */, $"gamedev_msgprio: out of range and out of arc at 137 m shows the range message first ('{m137}')"); }
        { /* gamedev_board: story boarding gate at 75% pirate hull */ var cc0 = S.combat; CombatCore Bq(bool story, float frac) { var q = new CombatCore(cc0, 5, false, 0, difficulty); if (story) { q.SetupStoryApproach(cc0.storyStartRange); q.ApplyStoryBalance(); } q.H.x = q.H.z = 0; q.P.x = cc0.boardRange * 0.5f; q.P.z = 0; q.P.sails = cc0.disableSails; q.P.hull = q.P.hull0 * frac; return q; }
          var b80 = Bq(true, 0.80f); var b75 = Bq(true, 0.75f); var n80 = Bq(false, 0.80f);
          Chk(!b80.BoardAllowed(b80.H, b80.P) && !b80.BoardAllowed(b80.P, b80.H), "gamedev_board: story fight: no boarding either way while the pirate hull is above 75% (80%)");
          Chk(b75.BoardAllowed(b75.H, b75.P) && n80.BoardAllowed(n80.H, n80.P), "gamedev_board: boarding allowed at 75% pirate hull in the story fight; later fights unchanged (80% still boards)");
          int ig0 = duelTapsIgnored; var dco = StartCoroutine(Duel(true)); yield return null; var sb = duelPanel ? duelPanel.GetComponentsInChildren<Button>().FirstOrDefault(b => b.GetComponentInChildren<Text>()?.text == "STRIKE") : null;
          bool sbFound = sb != null; if (sb) sb.onClick.Invoke(); int ig1 = duelTapsIgnored; yield return new WaitForSecondsRealtime(0.6f); if (sb) sb.onClick.Invoke(); int ig2 = duelTapsIgnored; StopCoroutine(dco); if (duelPanel) Destroy0(duelPanel.gameObject); yield return null;
          Chk(sbFound && ig1 == ig0 + 1 && ig2 == ig1, $"gamedev_board: a STRIKE tap in the first 0.5 s of the boarding card is ignored, one after 0.6 s counts (ignored {ig1 - ig0}, then {ig2 - ig1})"); }
        // gamedev_toll_fix (a): 90 s of fight firing on every reload through the real FIRE button (pointer click on the button) never trips the idle toll
        { Chk(core.storyBalance && Mathf.Abs(core.P.hull0 - core.C.pirate.hull * CombatCore.StoryPirateHullMult) < 0.01f && core.pirateDmgMult == CombatCore.StoryPirateDamageMult, $"gamedev_balance (c): the live story fog fight is scaled (pirate hull0 {core.P.hull0:0.#} = {CombatCore.StoryPirateHullMult} x {core.C.pirate.hull:0.#}, damage x{core.pirateDmgMult})");
          float toll0 = core.C.tollSeconds, max0 = core.C.maxFightSeconds; core.C.tollSeconds = core.C.maxFightSeconds = 1e5f; /* only the idle clock may end this window */ float ts0 = Time.timeScale; Time.timeScale = 4f; float t0 = core.t, rq2 = Time.realtimeSinceStartup; int taps = 0, shots = 0; float maxIdle = 0; core.idleT = 0; System.Action<CShip, CShip, int, int> cnt = (a, b, n, h) => { if (a == core.H) shots++; }; core.OnBroadside += cnt;
          while (fighting && core.outcome == Outcome.None && core.t - t0 < 90f && Time.realtimeSinceStartup - rq2 < 120f) {
            H.x = H.z = 0; H.heading = 0; H.rudder = 0; P.x = rg * 0.5f; P.z = 0; H.hull = H.hull0; H.crew = H.crew0; H.gunHp = H.gun0; H.sails = H.sails0; P.hull = P.hull0; P.crew = P.crew0; P.sails = P.sails0; H.burnT = P.burnT = 0;
            if (H.reloadT <= 0) { QaTapFire(); taps++; } maxIdle = Mathf.Max(maxIdle, core.idleT); yield return null; }
          core.OnBroadside -= cnt; Time.timeScale = ts0; core.C.tollSeconds = toll0; core.C.maxFightSeconds = max0; float ran = core.t - t0;
          Chk(ran >= 90f && core.outcome == Outcome.None && !core.idleTollFired && shots >= 10 && maxIdle < core.idleTollSeconds, $"gamedev_toll_fix (a): {ran:0} s firing through the real FIRE button on every reload: no toll ({taps} taps, {shots} broadsides, idle clock peak {maxIdle:0.0} s of {core.idleTollSeconds:0})"); }
        // gamedev_balance (b)/(c): headless fire-every-reload broadside story fights with the same CombatCore
        { var cc = S.combat; var res = new System.Collections.Generic.List<(Outcome o, float t, float hull)>(); for (int sd2 = 1; sd2 <= 60; sd2++) res.Add(CombatCore.SimStory(cc, sd2, 0, difficulty, true, true, cc.storyStartRange, cc.storyIdleTollSeconds));
          var wins = res.Where(r => r.o == Outcome.Sunk || r.o == Outcome.Surrender).ToList(); float tMed = wins.Count > 0 ? wins.OrderBy(r => r.t).ElementAt(wins.Count / 2).t : 0, hMed = wins.Count > 0 ? wins.OrderBy(r => r.hull).ElementAt(wins.Count / 2).hull : 0, hMin = wins.Count > 0 ? wins.Min(r => r.hull) : 0;
          Debug.Log($"GB BALANCE story sim broadside: wins {wins.Count}/60 median {tMed:0} s hull median {hMed:P0} min {hMin:P0}");
          Chk(wins.Count == res.Count && hMin >= 0.35f && !res.Any(r => r.o == Outcome.HasanStruck), $"gamedev_balance (b): fire-every-reload broadside story fight wins {wins.Count}/60 (median {tMed:0} s, hull left median {hMed:P0}, min {hMin:P0}), no toll");
          var bow = res.Select((_, i) => CombatCore.SimStory(cc, i + 1, 0, difficulty, false, true, cc.storyStartRange, cc.storyIdleTollSeconds)).ToList(); Debug.Log("GB BALANCE story sim bow-on: " + string.Join(" ", bow.GroupBy(r => r.o).Select(g => g.Key + ":" + g.Count())));
          var nq = new CombatCore(cc, 7, false, 0, difficulty); var nq1 = new CombatCore(cc, 7, false, 1, difficulty);
          Chk(!nq.storyBalance && !nq1.storyBalance && nq.P.hull0 == cc.pirate.hull && nq.pirateDmgMult == 1f && nq1.pirateDmgMult == 1f, $"gamedev_balance (c): later (non-story) fights keep full pirate hull {nq.P.hull0:0.#} and damage x{nq.pirateDmgMult}");
          // gamedev_timeout (Game Designer): the story fight cap is a PLAYER toll, never a win
          var tq2 = new CombatCore(cc, 11, false, 0, difficulty); tq2.SetupStoryApproach(cc.storyStartRange); tq2.ApplyStoryBalance(); tq2.idleTollSeconds = 0; tq2.P.hull = tq2.P.hull0 * 0.2f; /* pirate far behind on damage */ tq2.t = cc.tollSeconds; var to = tq2.Step(0.05f);
          Chk(to == Outcome.HasanStruck && tq2.storyCapToll && cc.tollSeconds < cc.storySafetyCapSeconds, $"gamedev_timeout: the story fight at the {cc.tollSeconds:0} s limit ends in the player toll even when the pirate is losing (outcome {to}); the {cc.storySafetyCapSeconds:0} s safety cap is only a backstop");
          var bowAll = bow.Concat(res).ToList(); Chk(!bowAll.Any(r => r.o == Outcome.TollPaid || r.o == Outcome.Timeout) && bowAll.Where(r => r.t >= cc.tollSeconds).All(r => r.o == Outcome.HasanStruck), $"gamedev_timeout: no win state on a timeout ({bowAll.Count(r => r.t >= cc.tollSeconds)} sims reached the limit, all player toll; no pirate toll)");
          int bowToll = bow.Count(r => r.o == Outcome.HasanStruck); Chk(bowToll >= bow.Count * 0.8f, $"gamedev_board: never turning broadside ends in the player toll ({bowToll}/{bow.Count})");
          int bowLoss = bow.Count(r => r.o == Outcome.HasanStruck || r.o == Outcome.HasanSunk || r.o == Outcome.HasanBoarded); Debug.Log($"GB BALANCE story sim bow-on after timeout rule: loss/player toll {bowLoss}/{bow.Count}");
          Chk(bowLoss >= bow.Count * 0.8f, $"gamedev_timeout: never turning broadside mostly loses or pays the toll ({bowLoss}/{bow.Count}: " + string.Join(" ", bow.GroupBy(r => r.o).Select(g => g.Key + ":" + g.Count())) + ")"); }
      }
      qaPtrOn = false; qaPtrDown = false;
      core.P.hull = 0; float tq = Time.realtimeSinceStartup; while (fighting && Time.realtimeSinceStartup - tq < 90f) { if (dialog != null) EgTestCloseCard(); yield return null; }
      { /* gamedev_flee: a fled pirate is a win with half the spoils */ var cq = core; int c0 = coins, f0 = fame, h0 = fameHalf; var cc = S.combat; core = new CombatCore(cc, 3); core.pirateFled = true; coins = 500; fameHalf = 0;
        var fco = StartCoroutine(Resolve(Outcome.Escaped)); yield return null; yield return null; var ft = dialog ? dialog.GetComponentsInChildren<Text>().Select(t => t.text).ToList() : new System.Collections.Generic.List<string>();
        bool card = ft.Contains("She flees") && ft.Any(t => t.StartsWith(FleeCardText) && !t.Contains("½") && !t.Contains("fame") && !System.Text.RegularExpressions.Regex.IsMatch(t, @"\b0 ")); yield return new WaitForSeconds(0.3f); yield return Shot("polish_flee_card"); EgTestCloseCard(); yield return null; yield return null;
        int got = coins - 500; bool half = got >= (int)cc.sinkCoinsMin / 2 && got <= (int)cc.sinkCoinsMax / 2 && fame == f0 && fameHalf == 1;
        core = new CombatCore(cc, 4); core.pirateFled = true; StartCoroutine(Resolve(Outcome.Escaped)); yield return null; yield return null; bool fameLine = dialog != null && dialog.GetComponentsInChildren<Text>().Any(t => t.text.EndsWith("+1 fame")); EgTestCloseCard(); yield return null; yield return null; bool two = fame == f0 + 1 && fameHalf == 0 && fameLine && FleeBody(0, "Rugs", 59, false) == FleeCardText + "\n\nHalf loot: 59 coins." && FleeBody(0, "Rugs", 0, true) == FleeCardText + "\n\n+1 fame" && FleeBody(3, "Rugs", 40, true) == FleeCardText + "\n\nHalf loot: 3 Rugs and 40 coins.\n+1 fame";
        core = new CombatCore(cc, 5); StartCoroutine(Resolve(Outcome.Escaped)); yield return null; yield return null; bool plain = dialog != null && dialog.GetComponentsInChildren<Text>().Any(t => t.text == "She escapes"); EgTestCloseCard(); yield return null; yield return null;
        Chk(card && half && two && plain, $"gamedev_flee: fled pirate = win card '{FleeCardText}' with half loot ({got} coins, half of {cc.sinkCoinsMin:0}-{cc.sinkCoinsMax:0}) and half fame (two flees = +1 fame); other escapes unchanged");
        core = cq; coins = c0; fame = f0; fameHalf = h0; }
      { /* gamedev_timeout: the live resolution of a story-fight timeout deducts the toll and gives no loot/fame */ int c0 = coins, f0 = fame; coins = 500; int want = StoryTollCoins(); storyCapHit = true; var rco = StartCoroutine(Resolve(Outcome.HasanStruck)); yield return null; yield return null;
        bool tollCard = dialog != null && dialog.GetComponentsInChildren<Text>().Any(t => t.text == "The pirates take a toll"); yield return Shot("timeout_player_toll"); EgTestCloseCard(); yield return null; yield return null;
        Chk(tollCard && coins == 500 - want && want == 50 && fame == f0 && !storyCapHit, $"gamedev_timeout: running out the clock -> 'The pirates take a toll', {want} coins deducted (500 -> {coins}), no fame"); coins = c0; }
      // second story fight in the same save: no hint
      ambushForced = true; StartCoroutine(Fight(true)); yield return null; yield return null; yield return new WaitForSeconds(0.3f); bool hint3 = SteerHintVisible;
      core.P.hull = 0; tq = Time.realtimeSinceStartup; while (fighting && Time.realtimeSinceStartup - tq < 90f) { if (dialog != null) EgTestCloseCard(); yield return null; }
      Chk(!hint3, "gamedev_rudder: the hint shows once per save (not again in the next story fight)");
      // persisted through save/load
      steerHintSeen = true; var sd = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(new SaveData { steerHintSeen = true })); Chk(sd.steerHintSeen && JsonUtility.ToJson(new SaveData()).Contains("\"steerHintSeen\":false"), "gamedev_rudder: the hint flag is a save field");
      ambushForced = af0; storyAmbushDone = sad0; boardedStoryPirate = bsp0; steerHintSeen = shs0; if (town3D) town3D.gameObject.SetActive(town0); RenderSettings.ambientLight = amb0; tab = -1; Refresh(); yield return null; }
    log.Close();
    // ---- endgame logic: deliver, fail, rank-up, save/load, cards (existing self-test) ----
    egFails = 0; yield return EgSelfTest(false);
    log = new StreamWriter(Path.Combine(dir, "qa_results.txt"), true); L($"endgame self-test: {(egFails == 0 ? "PASS" : egFails + " FAIL")} (GB EGTEST lines in the player log; its screenshots are in {Path.Combine(SaveDir, "egtest")})"); fails += egFails;
    L(fails == 0 ? $"QA ALL PASS ({n} checks + endgame self-test)" : $"QA {fails} FAIL"); log.Close(); yield return new WaitForSeconds(0.5f); Application.Quit(fails == 0 ? 0 : 1);
  }
}
