using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using ED = EndgameData;
using Rnd = UnityEngine.Random;

/* gamedev_end1: endgame first slice - Renown, the first 3 ranks, delivery/rush contracts at the Gwadar/Debal/Muscat tavern boards. Data + strings in EndgameData. */
[Serializable] public class EgContract { public string id, tpl, kind, from, to, title, giver, risk, status = "offer"; public int good, qty, pay, renown, deposit, failRenown, moons, dueWeek = -1; } /* gamedev_end1: offer|active|done|failed */
[Serializable] public class EgState { public int renown, rank; public List<string> firstSales = new List<string>(); public List<EgContract> board = new List<EgContract>(); public List<EgContract> active = new List<EgContract>(); public int boardMoon = -1, seq; } /* gamedev_end1: saved with the game */

public partial class Game {
  public EgState eg = new EgState(); /* gamedev_end1 */
  readonly Queue<(string t, string b, string btn)> egCards = new Queue<(string, string, string)>(); /* gamedev_end1: rank-up / delivered / failed cards, shown when nothing else is up */
  bool EgForce => gbForce.Contains("endgame"); /* gamedev_end1: -gbforce endgame unlocks the board before the opening ends */
  bool EgBoardOpen => EgForce || OpeningDone; /* gamedev_end1 */
  void EgTestCloseCard() { var bs = dialog ? dialog.GetComponentsInChildren<Button>() : null; if (bs != null && bs.Length > 0) bs[bs.Length - 1].onClick.Invoke(); } /* gamedev_title1: close a test card the normal way (last button: Onward/Flee/Leave) so its panel + dim go */
  bool egTestQuiet; /* gamedev_title1: endgame_test shows cards itself */
  void EgReset() { eg = new EgState(); egCards.Clear(); } /* gamedev_end1 */
  public string RankName => ED.RankName(Mathf.Clamp(eg.rank, 0, ED.RankCount - 1));
  public void AddRenown(int n, string why) { /* gamedev_end1: the ONLY way Renown changes */
    if (n == 0) return; int floor = ED.RenownFloorAtRank ? ED.RankRenown(eg.rank) : 0; eg.renown = Mathf.Max(floor, eg.renown + n); Debug.Log($"GB RENOWN {(n > 0 ? "+" : "")}{n} ({why}) -> {eg.renown} rank={RankName}");
    while (eg.rank + 1 < ED.RankCount && eg.renown >= ED.RankRenown(eg.rank + 1)) { eg.rank++; Debug.Log("GB RENOWN rank up -> " + RankName); egCards.Enqueue((ED.RankUpHeading(eg.rank), ED.RankPromo(eg.rank), ED.RankUpButtonFor(eg.rank))); /* gamedev_end1b: per-rank heading + button, no rival line */ eg.boardMoon = -1; /* full board at Trader: re-post now */ }
  }
  public string RenownLine() => eg.rank + 1 < ED.RankCount ? ED.NextRank(ED.RankName(eg.rank + 1), eg.renown, ED.RankRenown(eg.rank + 1)) : $"{RankName}, {eg.renown:N0} Renown"; /* gamedev_end1 */
  void EgFirstSale(int g) { string k = port + ":" + g; if (eg.firstSales.Contains(k)) return; eg.firstSales.Add(k); AddRenown(ED.RenownFirstSale, "first sale of " + S.goods[g].name + " at " + S.ports[port].name); msg = ED.FirstSale(GoodName(g, 1), ED.RenownFirstSale); } /* gamedev_end1 */
  void EgFightWon(string how) => AddRenown(ED.RenownFightWin, "fight won: " + how); /* gamedev_end1 */
  void EgUpdate() { /* gamedev_end1: from Update - show queued cards one at a time when the screen is free */
    if (egTestQuiet || egCards.Count == 0 || onTitle || fighting || sailing || dialog != null || CardBusy) return;
    var c = egCards.Dequeue(); Refresh(); /* gamedev_badge1: top-bar badge shows the new rank with its card */ StartCoroutine(Ask(c.t, c.b, (c.btn, null)));
  }

  // ---- contracts ----
  int Wpm => Mathf.Max(1, S.weeksPerMoon);
  int EgWeeksLeft(EgContract c) => c.dueWeek - week; /* 0 = last week, <0 = failed */
  int EgMoonsLeft(EgContract c) => Mathf.Max(1, Mathf.CeilToInt((EgWeeksLeft(c) + 1) / (float)Wpm));
  string EgMoonsText(EgContract c) => c.status == "offer" ? ED.MoonsLeft(c.moons, false) : ED.MoonsLeft(EgMoonsLeft(c), EgWeeksLeft(c) <= 0);
  string EgMoonsColor(EgContract c) => c.status != "offer" && EgWeeksLeft(c) <= 0 ? ED.RedColor : ED.AmberColor; /* gamedev_end1c: amber, red only in the final week of the last moon (Game Designer) - a fresh 1-moon rush is amber */
  bool IsBoardPort(int p) => p >= 0 && ED.BoardPorts.Contains(S.ports[p].name);
  void EgRefreshBoards(bool force = false) { /* gamedev_end1: new notices every moon (and at rank-up) */
    if (!force && eg.boardMoon == Moon) return; eg.boardMoon = Moon; eg.board.RemoveAll(b => b.status == "offer");
    int n = eg.rank >= ED.FullBoardRank ? ED.BoardSize : ED.StarterBoardSize; var stats = new List<string>();
    foreach (var pn in ED.BoardPorts) { var pool = ED.Templates.Where(t => t.port == pn && t.dest != pn && (eg.rank >= ED.FullBoardRank || t.kind == "delivery") && G(t.good) >= 0 && PortIdx(t.dest) >= 0 && !eg.active.Any(a => a.tpl == t.id)).OrderBy(_ => Rnd.value).Take(n).ToList();
      foreach (var t in pool) { int pay = Mathf.Min(t.coins, ED.Cap(t.kind)); /* design cap, clamped in code */
        eg.board.Add(new EgContract { id = "eg" + (++eg.seq), tpl = t.id, kind = t.kind, from = pn, to = t.dest, title = t.title, giver = t.giver, risk = t.risk, good = G(t.good), qty = t.qty, pay = pay, renown = t.renown, deposit = Mathf.Min(t.deposit, pay), failRenown = t.failRenown, moons = t.moons }); }
      stats.Add(pn + "=" + pool.Count); }
    Debug.Log($"GB CONTRACT board refresh moon={week / Wpm} rank={RankName} " + string.Join(" ", stats));
  }
  bool EgAccept(EgContract c) {
    if (eg.active.Count >= ED.MaxActive) { msg = ED.Full; return false; } if (coins < c.deposit) { msg = ED.CantAfford; return false; }
    coins -= c.deposit; c.status = "active"; c.dueWeek = week + c.moons * Wpm - 1; eg.board.Remove(c); eg.active.Add(c);
    Debug.Log($"GB CONTRACT accept {c.id} {c.kind} {c.qty}x{S.goods[c.good].id} {c.from}->{c.to} pay={c.pay} dep={c.deposit} dueWeek={c.dueWeek} (week {week})"); msg = c.title + ": " + EgMoonsText(c) + "."; Save(); return true;
  }
  void EgOnDock() { /* gamedev_end1: deliver on arrival if the cargo is in the hold (story cargo is never used) */
    var sg = StoryGoods();
    foreach (var c in eg.active.ToList()) { if (S.ports[port].name != c.to) continue;
      if (hold[c.good] < c.qty || sg.Contains(c.good)) { Debug.Log($"GB CONTRACT at {c.to} but hold {hold[c.good]}/{c.qty} {S.goods[c.good].id}: not delivered"); msg = ED.NeedCargo; continue; }
      hold[c.good] -= c.qty; coins += c.pay + c.deposit; c.status = "done"; eg.active.Remove(c);
      Debug.Log($"GB CONTRACT delivered {c.id} +{c.pay} coins +{c.renown} renown, deposit {c.deposit} back");
      egCards.Enqueue((c.title, ED.Delivered(c.pay, c.renown) + "\n\n" + ED.Deposit(c.deposit).Replace(", returned on delivery", " returned."), "Continue")); AddRenown(c.renown, "contract " + c.kind); }
  }
  void EgNewWeek() { /* gamedev_end1: from NewWeek, after week++ */
    foreach (var c in eg.active.ToList()) if (EgWeeksLeft(c) < 0) { c.status = "failed"; eg.active.Remove(c);
      Debug.Log($"GB CONTRACT failed {c.id} {c.failRenown} renown, deposit {c.deposit} lost"); egCards.Enqueue((c.title, ED.Failed(c.failRenown), "Continue")); AddRenown(c.failRenown, "contract failed"); } /* never touches cargo or the ship */
    EgRefreshBoards();
  }

  // ---- UI ----
  Button EgBoardBtn(RectTransform led, float y, float x = 26, float w = 980) { /* gamedev_end1: tavern entry */
    if (!IsBoardPort(port)) return null; EgRefreshBoards(); int n = eg.board.Count(b => b.from == S.ports[port].name);
    return Btn(led, EgBoardOpen ? $"{ED.BoardTitle} ({n})" : ED.BoardTitle, x, y, w, 110, () => { if (!EgBoardOpen) { msg = ED.BoardLocked; Refresh(); return; } StartCoroutine(EgBoardCo()); }, true, false);
  }
  IEnumerator EgBoardCo() {
    bool closed = false; EgContract open = null; bool rebuild = true; RectTransform my = null, dim = null;
    var be = PushBack(ED.BoardTitle, () => !closed, () => { if (open != null) { open = null; rebuild = true; } else closed = true; }); /* Back: card = Not now, list = close */
    while (!closed) {
      if (rebuild) { rebuild = false; if (my) Destroy0(my.gameObject); if (!dim) { dim = ModalDim("EgDim"); } 
        if (open == null) my = EgList(c => { open = c; rebuild = true; }, () => closed = true);
        else my = EgCard(open, () => { if (EgAccept(open)) { open = null; closed = true; } else { open = null; rebuild = true; } }, () => { open = null; rebuild = true; });
        dialog = my; }
      yield return null; if (my == null) closed = true; }
    backStack.Remove(be); if (my) Destroy0(my.gameObject); if (dim) Destroy0(dim.gameObject); if (dialog == my) dialog = null; Refresh();
  }
  RectTransform EgList(Action<EgContract> pick, Action close) {
    var here = eg.board.Where(b => b.from == S.ports[port].name).ToList(); float H = 330 + Mathf.Max(1, here.Count) * 132 + 110;
    var p = Px(root, "EgBoard", 30, 300, 1020, H); Box(p, T.panel, T.gold);
    Label(p, ED.BoardTitle + " · " + S.ports[port].name, 130, 40, 760, 60, T.fontSizeTitle - 4, "#C9A55A", TextAnchor.MiddleLeft, true);
    Label(p, RenownLine() + $"   ·   Active {eg.active.Count}/{ED.MaxActive}", 130, 104, 760, 40, T.fontSizeSmall - 2, T.textMuted);
    float y = 160; if (here.Count == 0) { Label(p, ED.BoardEmpty, 130, y, 760, 100, T.fontSize, T.textMuted); y += 132; }
    foreach (var c in here) { var cc = c; var b = Btn(p, "", 110, y, 800, 116, () => pick(cc));
      Label(b.transform, ED.ListRow(c.kind == "rush" ? "Rush" : "Delivery", c.to, c.moons == 1 ? "1 moon" : c.moons + " moons", c.pay), 24, 0, 680, 116, T.fontSizeSmall, T.buttonText);
      var pip = Px(b.transform, "pip", 730, 40, 36, 36).gameObject.AddComponent<Image>(); pip.sprite = Dot(); pip.color = UITheme.C(c.kind == "rush" ? ED.AmberColor : "#5FA35A"); pip.raycastTarget = false; y += 132; }
    if (eg.rank < ED.FullBoardRank) { Label(p, $"The full board opens at {ED.RankName(ED.FullBoardRank)}.", 130, y, 760, 50, T.fontSizeSmall - 2, T.textMuted); }
    Btn(p, "Close", 110, H - 110 - 84 + 60, 800, 84, close); return p;
  }
  RectTransform EgCard(EgContract c, Action accept, Action notNow) { /* layout per contract_card_mock.jpg, existing styles: title x130, body from y132, 110 px bottom gap */
    float H = 1010; var p = Px(root, "EgCard", 30, 260, 1020, H); Box(p, T.panel, T.gold);
    var tf = CardTitle(p, c.title, 130, 760, T.fontSizeTitle - 2, "#C9A55A", TextAnchor.MiddleCenter, CardTitleFit(c.title, 760, T.fontSizeTitle - 2)); /* gamedev_title1: shared title (was y30 h70, rule y106) */
    float y = 132 + tf.extra; H += tf.extra; p.sizeDelta = new Vector2(p.sizeDelta.x, H); Label(p, $"<i>{c.giver}</i>", 130, y, 760, 56, T.fontSizeSmall, T.text, TextAnchor.MiddleCenter); y += 72;
    void Row(string head, string val, string col) { Label(p, head, 130, y, 760, 38, T.fontSizeSmall - 4, T.textMuted); Label(p, val, 130, y + 36, 760, 50, T.fontSize, col); Box(Px(p, "sep", 130, y + 98, 760, 1), T.brass); y += 108; }
    Row(ED.S("cargo", "Cargo"), GoodCount(c.good, c.qty), T.parchment); Row(ED.S("deliverTo", "Deliver to"), c.to, T.parchment); Row(ED.S("deadline", "Deadline"), EgMoonsText(c), EgMoonsColor(c));
    Label(p, ED.Reward(c.pay, c.renown), 130, y + 6, 760, 54, T.fontSize, T.parchment, TextAnchor.MiddleCenter, true); Label(p, $"<i>{ED.Deposit(c.deposit)}</i>", 130, y + 60, 760, 40, T.fontSizeSmall - 4, T.textMuted, TextAnchor.MiddleCenter); y += 116;
    Box(Px(p, "sep2", 130, y, 760, 1), T.brass); Label(p, c.risk, 130, y + 10, 760, 60, T.fontSizeSmall - 2, T.text, TextAnchor.MiddleCenter); y += 86;
    bool can = eg.active.Count < ED.MaxActive && coins >= c.deposit; string why = eg.active.Count >= ED.MaxActive ? ED.Full : coins < c.deposit ? ED.CantAfford : "";
    if (why != "") Label(p, why, 130, y - 6, 760, 36, T.fontSizeSmall - 4, T.down, TextAnchor.MiddleCenter);
    float by = H - 110 - 96; Btn(p, ED.S("accept", "Accept"), 110, by, 390, 96, accept, can, true); Btn(p, ED.S("notNow", "Not now"), 520, by, 390, 96, notNow);
    return p;
  }
  string EgJournalBlock() { /* gamedev_end1: under the goal line: next rank + active contracts with moons left */
    var s = RenownLine(); foreach (var c in eg.active) s += $"\n• {c.title}: {GoodCount(c.good, c.qty)} to {c.to}, <color={EgMoonsColor(c)}>{EgMoonsText(c)}</color>"; return s; }
  string EgMapFlag(int pi) { /* gamedev_end1: brass flag text on the sea map for a contract destination */
    var cs = eg.active.Where(c => PortIdx(c.to) == pi).ToList(); if (cs.Count == 0) return null; var c = cs.OrderBy(EgWeeksLeft).First();
    return $"<color={EgMoonsColor(c)}>⚑ {EgMoonsText(c)}</color>"; }

  /* gamedev_end1: -gbforce endgame_test (with -gbsavedir under /tmp): scripted smoke test of the slice, logs "GB EGTEST ...", screenshots the card, quits.
     -gbforce endgame (any run): opens the board before the opening ends. */
  int egFails; /* gamedev_qa1 */
  IEnumerator EgSelfTest(bool standalone = true) {
    int fails = 0; void L(string s) => Debug.Log("GB EGTEST " + s); void Chk(bool ok, string what) { if (!ok) fails++; L((ok ? "PASS " : "FAIL ") + what); }
    if (!SaveDirIsolated) { L("REFUSED: needs -gbsavedir"); Application.Quit(4); yield break; }
    if (standalone) yield return new WaitForSeconds(1f); onTitle = false; Init(); egTestQuiet = true; storyStep = StDone; coins = 3000; port = PortIdx("Gwadar"); tab = 8; Refresh(); yield return null;
    EgRefreshBoards(true); Chk(eg.board.Count == 3 && eg.board.All(b => b.kind == "delivery"), $"Deckhand boards: 1 starter delivery per port (got {eg.board.Count})");
    string bd = System.IO.Path.Combine(SaveDir, "egtest"); System.IO.Directory.CreateDirectory(bd); /* gamedev_badge1 */
    IEnumerator Bar(string n) { tab = -1; Refresh(); yield return null; yield return null; var md = topBar ? topBar.Find("Medallion") : null; var im = md ? md.GetComponent<Image>() : null; Chk(im && im.sprite && im.sprite.name == "rank_" + eg.rank, $"top bar badge {n}: {(im && im.sprite ? im.sprite.name : "fallback/none")}"); yield return ShotCo(System.IO.Path.Combine(bd, "topbar_" + n + ".png")); yield return new WaitForSeconds(0.4f); }
    yield return Bar("deckhand"); /* gamedev_badge1 */
    Chk(ED.Templates.Count == 24, $"24 templates loaded (got {ED.Templates.Count})");
    Chk(ED.Templates.All(t => Mathf.Min(t.coins, ED.Cap(t.kind)) <= (t.kind == "rush" ? 1200 : 900)), $"reward caps delivery {ED.CapDelivery} rush {ED.CapRush}");
    var c = eg.board.First(b => b.from == "Gwadar"); int c0 = coins; Chk(EgAccept(c), $"accept {c.title} deposit {c.deposit}"); Chk(coins == c0 - c.deposit, "deposit taken");
    hold[c.good] += c.qty; port = PortIdx(c.to); int c1 = coins; EgOnDock(); Chk(eg.renown == c.renown && coins == c1 + c.pay + c.deposit && c.status == "done", $"delivered: renown {eg.renown}, coins +{coins - c1}");
    AddRenown(ED.RankRenown(1) - eg.renown, "test top-up"); Chk(eg.rank == 1 && RankName == "Trader", "rank up to Trader at " + eg.renown);
    yield return Bar("trader"); /* gamedev_badge1 */
    EgRefreshBoards(); Chk(eg.board.Count(b => b.from == "Gwadar") == 3, "Trader: full 3-card board at Gwadar"); Chk(eg.board.All(b => b.to != b.from), $"no board offers a delivery to its own port ({eg.board.Count} cards)"); /* gamedev_selfdel */
    var r = eg.board.FirstOrDefault(b => b.kind == "rush") ?? eg.board[0]; port = PortIdx(r.from); int rn = eg.renown; var held = (int[])hold.Clone(); EgAccept(r);
    for (int i = 0; i < r.moons * Wpm; i++) NewWeek(); Chk(r.status == "failed" && eg.renown == Mathf.Max(ED.RankRenown(1), rn + r.failRenown) && hold.SequenceEqual(held), $"rush {r.kind} failed after {r.moons} moon(s): renown {rn}->{eg.renown} (floor {ED.RankRenown(1)}), cargo untouched");
    { /* gamedev_end1c checks */ EgRefreshBoards(); var rr = eg.board.FirstOrDefault(b => b.kind == "rush" && b.moons == 1);
      if (rr != null) { Chk(EgMoonsColor(rr) == ED.AmberColor, "fresh 1-moon rush offer is amber"); var cp = new EgContract { status = "active", dueWeek = week + Wpm - 1, moons = 1 }; Chk(EgMoonsColor(cp) == ED.AmberColor, "accepted 1-moon rush, week 1 of 4: amber"); cp.dueWeek = week; Chk(EgMoonsColor(cp) == ED.RedColor && EgMoonsText(cp) == ED.MoonsLeft(1, true), $"final week: red '{EgMoonsText(cp)}'"); }
      var b3 = eg.board.Take(3).ToList(); int acc = 0; coins += 5000; foreach (var b in b3) if (EgAccept(b)) acc++; Chk(acc == ED.MaxActive && eg.active.Count == ED.MaxActive && msg == ED.Full, $"2-active limit: accepted {acc} of {b3.Count}, msg '{msg}'");
      var extra = eg.board.FirstOrDefault(); if (extra != null) { var dm = ModalDim("EgDim"); var cc = EgCard(extra, () => { }, () => { }); yield return null; var acB = cc.GetComponentsInChildren<Button>().First(); Chk(!acB.interactable || !acB.enabled || cc.GetComponentsInChildren<Text>().Any(t => t.text == ED.Full), "card shows the limit + Accept disabled");
        var rt = acB.GetComponent<RectTransform>(); float bottomGap = cc.rect.height - (-rt.anchoredPosition.y + rt.rect.height); var risk = cc.GetComponentsInChildren<Text>().First(t => t.text == extra.risk).rectTransform; float aboveGap = -rt.anchoredPosition.y - (-risk.anchoredPosition.y + risk.rect.height);
        Chk(Mathf.Abs(bottomGap - 110) < 1, $"contract card: gap under the buttons {bottomGap:0} px (spec 110), gap above the buttons {aboveGap:0} px"); yield return ShotCo(System.IO.Path.Combine(bd, "contract_card_full.png")); yield return new WaitForSeconds(0.4f); Destroy0(cc.gameObject); Destroy0(dm.gameObject); dialog = null; yield return null; }
      foreach (var x in eg.active.ToList()) { eg.active.Remove(x); } }
    EgRefreshBoards(); var a2 = eg.board.First(); EgAccept(a2); Save(); int sr = eg.renown, sa = eg.active.Count; eg = new EgState(); Load(); Chk(eg.renown == sr && eg.active.Count == sa && eg.rank == 1, $"save/load: renown {eg.renown}, active {eg.active.Count}");
    { var rc = egCards.FirstOrDefault(x => x.t == ED.RankUpHeading(1)); Chk(rc.t != null && rc.btn == ED.RankUpButtonFor(1) && !rc.b.Contains("Enjoy it while"), $"Trader rank-up card: '{rc.t}' / '{rc.btn}', no rival line"); /* gamedev_end1b */
      if (rc.t != null) { var co = StartCoroutine(Ask(rc.t, rc.b, (rc.btn, null))); yield return null; yield return null; string d0 = System.IO.Path.Combine(SaveDir, "egtest"); System.IO.Directory.CreateDirectory(d0); yield return ShotCo(System.IO.Path.Combine(d0, "rankup_trader.png")); yield return new WaitForSeconds(0.5f); EgTestCloseCard(); yield return null; yield return null; } }
    while (egCards.Count > 0) egCards.Dequeue();
    { /* gamedev_end1c: Merchant rank-up card - promo line fits the body, clear of the button */ AddRenown(ED.RankRenown(2) - eg.renown, "test top-up"); var mc = egCards.FirstOrDefault(x => x.t == ED.RankUpHeading(2)); Chk(eg.rank == 2 && mc.t != null, $"rank up to Merchant: '{mc.t}'");
      if (mc.t != null) { StartCoroutine(Ask(mc.t, mc.b, (mc.btn, null))); yield return null; yield return null; var dlg = dialog; var body = dlg.GetComponentsInChildren<Text>().First(t => t.text == mc.b); var btn = dlg.GetComponentsInChildren<Button>().Last().GetComponent<RectTransform>();
        float bodyBottom = -body.rectTransform.anchoredPosition.y + body.preferredHeight, btnTop = -btn.anchoredPosition.y; Chk(body.preferredHeight <= body.rectTransform.rect.height + 1 && bodyBottom < btnTop, $"Merchant promo ({mc.b.Length} chars) fits: text h {body.preferredHeight:0} in box {body.rectTransform.rect.height:0}, bottom {bodyBottom:0} < button top {btnTop:0}");
        yield return ShotCo(System.IO.Path.Combine(bd, "rankup_merchant.png")); yield return new WaitForSeconds(0.4f); EgTestCloseCard(); yield return null; yield return null; }
      while (egCards.Count > 0) egCards.Dequeue(); }
    var lt = ED.Templates.OrderByDescending(t => t.giver.Length).First(); L($"longest giver line {lt.giver.Length} chars ({lt.id}): {lt.giver}");
    { int dp = PortIdx(lt.port); if (dp < 0 || lt.port == lt.dest) dp = ED.BoardPorts.Select(PortIdx).First(i => i >= 0 && S.ports[i].name != lt.dest); port = dp; Chk(S.ports[port].name != lt.dest, $"longest card: docked at {S.ports[port].name}, delivering to {lt.dest}"); } /* gamedev_selfdel */
    var shot = new EgContract { from = S.ports[port].name, title = lt.title, giver = lt.giver, risk = lt.risk, kind = lt.kind, to = lt.dest, good = G(lt.good), qty = lt.qty, pay = Mathf.Min(lt.coins, ED.Cap(lt.kind)), renown = lt.renown, deposit = lt.deposit, moons = lt.moons, status = "offer" };
    var dim = ModalDim("EgDim"); var card = EgCard(shot, () => { }, () => { }); yield return null; yield return null;
    var gt = card.GetComponentsInChildren<Text>().First(t => t.text.Contains(lt.giver)); Chk(gt.preferredHeight <= gt.rectTransform.rect.height + 1 || gt.cachedTextGenerator.lineCount <= 2, $"giver line fits: lines={gt.cachedTextGenerator.lineCount} prefH={gt.preferredHeight:0} boxH={gt.rectTransform.rect.height:0}");
    string dir = System.IO.Path.Combine(SaveDir, "egtest"); System.IO.Directory.CreateDirectory(dir); yield return ShotCo(System.IO.Path.Combine(dir, "contract_card_longest.png")); yield return new WaitForSeconds(0.5f); Destroy0(card.gameObject); Destroy0(dim.gameObject); dialog = null; yield return null; /* gamedev_title1: clear the card before the title shots */
    { /* gamedev_title1: title screenshots - long legacy strings, current data, and existing cards */
      IEnumerator Card(string n, IEnumerator co) { var h = StartCoroutine(co); yield return null; yield return null; yield return null; yield return ShotCo(System.IO.Path.Combine(dir, "title_" + n + ".png")); yield return new WaitForSeconds(0.4f); EgTestCloseCard(); yield return null; yield return null; }
      foreach (var (n, t) in new[] { ("rankup_long", "Your name carries on the quay"), ("delivered_long", "Dates for the Debal bazaar"), ("rankup_data", ED.RankUpHeading(1)), ("storm_oneline", "Storm front") }) {
        var f = CardTitleFit(t, 640, T.fontSizeTitle); L($"title '{t}': size {f.size}/{T.fontSizeTitle} lines {f.lines} extra {f.extra:0}"); yield return Card(n, Ask(t, t == "Storm front" ? "A black wall of rain marches across the Gulf. The mate waits on your word." : "The harbour knows your name now. Trader.", ("Onward", null))); }
      yield return Card("askcard_long", AskCard("Your name carries on the quay", "An AskCard with the long legacy heading.", ("Fight", null), ("Flee", null)));
      tab = 8; Refresh(); yield return null; foreach (var (qn, kn) in new[] { ("quest_short", "Innkeeper of Gwadar"), ("quest_long", "Innkeeper of the Salt & Lantern"), ("quest_verylong", "Harbourmaster of the Great Pearl Banks of Muscat") }) { var f = CardTitleFit(kn, 892, T.fontSizeTitle - 4); L($"quest title '{kn}': size {f.size} lines {f.lines} extra {f.extra:0}"); yield return Card(qn, QuestMenu("tavern", kn)); } } /* gamedev_title2 */
    egFails += fails; if (!standalone) yield break; /* gamedev_qa1: the QA run continues */
    L(fails == 0 ? "ALL PASS" : fails + " FAIL"); yield return new WaitForSeconds(0.5f); Application.Quit(fails == 0 ? 0 : 1);
  }
}
