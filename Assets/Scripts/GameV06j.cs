using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Rnd = UnityEngine.Random;

/// v0.6j: journal, accept/decline jobs from Design/v06j_jobs, town services, hull limp / Muscat patch, keepers on every building.
public partial class Game {
  static object JGet(Dictionary<string, object> d, string k, object def = null) => d != null && d.TryGetValue(k, out var v) && v != null ? v : def;

  public class JobDef {
    public string id, building, giver, title, summary, greeting, journalText, destination, deadline, reward, risk, hint;
    public bool contraband, oneShot; public int deadlineDays, deadlineMoons; public int coins, honorReward, fearReward, sultanReward;
    public Dictionary<string, object> gate, highHonorModifiers, onCaught, objective;
    public List<string> towns = new List<string>();
  }
  public class ActiveJob {
    public string id, title, giver, destination, reward, risk, deadline, hint, status;
    public int coins, honorReward, fearReward, sultanReward; public bool contraband; public int acceptWeek, dueWeek;
    public Dictionary<string, object> objective, onCaught, highHonorModifiers;
  }

  List<JobDef> jobPool = new List<JobDef>();
  public List<ActiveJob> journal = new List<ActiveJob>();
  Dictionary<string, List<string>> declinedByBuilding = new Dictionary<string, List<string>>();
  Dictionary<string, int> offerCursor = new Dictionary<string, int>();
  public HashSet<string> oneShotUsed = new HashSet<string>(); public float seaMerchantMaxFrac = 0.8f, gwadarRepairMult = 0.8f; public int patchDebtCoins = 45;
  int journalTab;
  public int harbourDebt; public int sultanStanding; public int harbourDebtDueWeek = -1; int patchDebtDueMoons = 2; float patchPaidPct = 0.5f, patchFreePct = 0.25f, patchLeavePct = 0.25f;
  const int JobCap = 3;
  int highHonorThreshold = 4;
  Dictionary<string, object> townServices = new Dictionary<string, object>();

  bool OpeningDone => !InOpening || (openingGpSold && yusufSeal) || storyStep >= StDone;
  bool JournalUnlocked => OpeningDone;
  int ActiveJobCount => journal.Count(j => j.status == "active");
  string BuildingKey(string building) => port + ":" + building;

  void LoadJobsV06j() {
    jobPool.Clear();
    var ta = Resources.Load<TextAsset>("Design/v06j_jobs");
    if (ta == null) { Debug.LogWarning("GB v06j: Design/v06j_jobs missing — stubs only"); SeedStubJobs(); return; }
    var root = (Dictionary<string, object>)MiniJson.Parse(ta.text);
    highHonorThreshold = (int)Design.F(JGet(root, "highHonorThreshold", 4), 4);
    if (root.ContainsKey("townServices")) townServices = Design.D(root["townServices"]);
    if (root.ContainsKey("muscatEmergencyPatch")) { var mp = Design.D(root["muscatEmergencyPatch"]); patchPaidPct = Design.F(JGet(mp, "paidHullPercent", 50)) / 100f; patchFreePct = Design.F(JGet(mp, "freeHullPercent", 25)) / 100f; patchLeavePct = Design.F(JGet(mp, "minLeaveHullPercent", 25)) / 100f; patchDebtDueMoons = (int)Design.F(JGet(mp, "debtDueMoons", 2), 2); patchDebtCoins = (int)Design.F(JGet(mp, "debtCoins", 45), 45); }
    seaMerchantMaxFrac = Design.F(JGet(root, "seaMerchantOfferMaxCoinsFrac", 0.8f), 0.8f); gwadarRepairMult = Design.F(JGet(root, "gwadarRepairMult", 0.8f), 0.8f);
    if (root.ContainsKey("highHonorThreshold")) highHonorThreshold = (int)Design.F(root["highHonorThreshold"], 4);
    foreach (var o in (List<object>)root["jobs"]) {
      var d = Design.D(o); var jd = new JobDef {
        id = (string)d["id"], building = (string)d["building"], giver = (string)JGet(d, "giver", ""),
        title = (string)d["title"], summary = (string)JGet(d, "summary", ""), greeting = (string)JGet(d, "greeting", ""),
        journalText = (string)JGet(d, "journalText", ""), destination = (string)JGet(d, "destination", ""),
        deadline = (string)JGet(d, "deadline", ""), reward = (string)JGet(d, "reward", ""), risk = (string)JGet(d, "risk", ""),
        hint = (string)JGet(d, "hint", ""), contraband = d.ContainsKey("contraband") && d["contraband"] is bool bc && bc, oneShot = d.ContainsKey("oneShot") && d["oneShot"] is bool os && os,
        deadlineMoons = d.ContainsKey("deadlineMoons") && d["deadlineMoons"] != null ? (int)Design.F(d["deadlineMoons"]) : 0,
        deadlineDays = d.ContainsKey("deadlineDays") && d["deadlineDays"] != null ? (int)Design.F(d["deadlineDays"]) : 0,
        gate = d.ContainsKey("gate") ? Design.D(d["gate"]) : new Dictionary<string, object>(),
        highHonorModifiers = d.ContainsKey("highHonorModifiers") ? Design.D(d["highHonorModifiers"]) : null,
        onCaught = d.ContainsKey("onCaught") ? Design.D(d["onCaught"]) : null,
        objective = d.ContainsKey("objective") ? Design.D(d["objective"]) : null,
      };
      if (d.ContainsKey("towns")) foreach (var t in (List<object>)d["towns"]) jd.towns.Add((string)t);
      if (d.ContainsKey("rewards")) { var r = Design.D(d["rewards"]); jd.coins = (int)Design.F(JGet(r, "coins", 0)); jd.honorReward = (int)Design.F(JGet(r, "honor", 0)); jd.fearReward = (int)Design.F(JGet(r, "fear", 0)); jd.sultanReward = (int)Design.F(JGet(r, "sultan", 0)); }
      jobPool.Add(jd);
    }
    Debug.Log($"GB v06j loaded {jobPool.Count} jobs, honorThresh {highHonorThreshold}");
  }
  void SeedStubJobs() {
    jobPool.Add(new JobDef { id = "tav_debal_rumour_yusuf", building = "tavern", giver = "Debal tavern", towns = { "Debal" }, title = "Rumour for Yusuf", summary = "Carry a tip about Qarin buyers to Gwadar's tavern.", greeting = "Word travels if you carry it. Interested?", destination = "Gwadar tavern", reward = "40 coins", coins = 40, hint = "Gwadar's tavern keeper will pay for the tip.", gate = new Dictionary<string, object> { { "type", "always" } },
      objective = new Dictionary<string, object> { { "type", "deliver" }, { "to", "Gwadar tavern" } } });
  }

  bool FlagOn(string f) => f switch {
    "opening_muscat_sale" => openingGpSold, "opening_complete" => OpeningDone, "farida_ally" => faridaState == 1,
    "farida_rival" => faridaState == 2, "farida_turned_in" => faridaState == 2, "met_farida" => faridaState >= 1, "yusuf_seal" => yusufSeal, _ => false };
  int StatVal(string s) => s switch { "honor" => honor, "fear" => fear, "sultan" => sultanStanding > 0 ? sultanStanding : Mathf.Max(0, rep[Muscat] / 5), _ => 0 };
  bool Cmp(int a, string op, int b) => op switch { ">=" => a >= b, ">" => a > b, "<=" => a <= b, "<" => a < b, "==" => a == b, _ => false };
  bool EvalCond(Dictionary<string, object> c) {
    if (c == null || c.Count == 0) return true;
    if (c.ContainsKey("notFlag")) return !FlagOn((string)c["notFlag"]);
    if (c.ContainsKey("flag")) return FlagOn((string)c["flag"]);
    if (c.ContainsKey("stat")) return Cmp(StatVal((string)c["stat"]), (string)JGet(c, "op", ">="), (int)Design.F(c["value"]));
    if (c.ContainsKey("type") && (string)c["type"] == "service") {
      string town = (string)JGet(c, "town", S.ports[port].name); string b = (string)JGet(c, "building", "");
      return TownHasService(town, b);
    }
    return true;
  }
  bool EvalGate(Dictionary<string, object> g) {
    if (g == null || g.Count == 0) return true;
    string type = (string)JGet(g, "type", "always");
    if (type == "always") return true;
    if (type == "flag") return FlagOn((string)g["flag"]);
    if (type == "notFlag") return !FlagOn((string)g["notFlag"]);
    if (type == "service") return EvalCond(g);
    if (type == "any" && g.ContainsKey("of")) return ((List<object>)g["of"]).Select(Design.D).Any(EvalCond);
    if (type == "all" && g.ContainsKey("of")) return ((List<object>)g["of"]).Select(Design.D).All(EvalCond);
    if (g.ContainsKey("stat")) return EvalCond(g);
    return true;
  }
  bool TownHasService(string town, string building) {
    if (!townServices.ContainsKey(town)) return true;
    var ts = Design.D(townServices[town]);
    if (building == "shipwright") return ts.ContainsKey("shipwright") && ts["shipwright"] != null;
    return true;
  }
  string ShipwrightMode(string town) {
    if (!townServices.ContainsKey(town)) return "full";
    var ts = Design.D(townServices[town]); if (!ts.ContainsKey("shipwright") || ts["shipwright"] == null) return "none";
    return (string)ts["shipwright"];
  }

  bool JobFits(JobDef j, string building) {
    if (j.building != building) {
      if (!(building == "harbour" && (j.building == "harbour" || (j.building == "shipwright" && ShipwrightMode(S.ports[port].name) == "none")))) return false;
    }
    if (j.towns.Count > 0 && !j.towns.Contains(S.ports[port].name)) return false;
    if (journal.Any(a => a.id == j.id && a.status == "active")) return false;
    if (j.oneShot && (oneShotUsed.Contains(j.id) || journal.Any(a => a.id == j.id))) return false;
    if (!EvalGate(j.gate)) return false;
    if (j.contraband && honor >= highHonorThreshold && j.highHonorModifiers != null) {
      float w = Design.F(JGet(j.highHonorModifiers, "offerWeight", 1f), 1f);
      if (Rnd.value > w) return false;
    }
    return true;
  }
  List<JobDef> OffersFor(string building, int max = 3) {
    string key = BuildingKey(building);
    if (!declinedByBuilding.TryGetValue(key, out var declined)) declined = declinedByBuilding[key] = new List<string>();
    var pool = jobPool.Where(j => JobFits(j, building) && !declined.Contains(j.id)).ToList();
    if (pool.Count == 0) { declined.RemoveAll(id => jobPool.Any(x => x.id == id && !x.oneShot)); pool = jobPool.Where(j => JobFits(j, building) && !declined.Contains(j.id)).ToList(); }
    if (pool.Count == 0) return new List<JobDef>();
    int cur = offerCursor.ContainsKey(key) ? offerCursor[key] : 0;
    var ordered = pool.Skip(cur % pool.Count).Concat(pool.Take(cur % pool.Count)).ToList();
    return ordered.Take(max).ToList();
  }

  void AcceptJob(JobDef j) {
    if (ActiveJobCount >= JobCap) { msg = "Journal is full (3/3). Finish a job first."; return; }
    int pay = j.coins;
    if (j.contraband && honor >= highHonorThreshold && j.highHonorModifiers != null) pay = Mathf.RoundToInt(pay * Design.F(JGet(j.highHonorModifiers, "coinsMult", 1.25f), 1.25f));
    journal.Add(new ActiveJob {
      id = j.id, title = j.title, giver = j.giver, destination = j.destination, reward = pay != j.coins ? $"{pay} coins (honourable terms)" : j.reward,
      risk = j.risk, deadline = j.deadline, hint = j.hint, status = "active", coins = pay, honorReward = j.honorReward, fearReward = j.fearReward,
      sultanReward = j.sultanReward, contraband = j.contraband, acceptWeek = week, dueWeek = j.deadlineMoons > 0 ? week + j.deadlineMoons * Mathf.Max(1, S.weeksPerMoon) : (j.deadlineDays > 0 ? week + j.deadlineDays : -1), objective = j.objective, onCaught = j.onCaught, highHonorModifiers = j.highHonorModifiers
    });
    if (j.oneShot) oneShotUsed.Add(j.id);
    msg = $"Accepted: {j.title}."; if (playthrough) PLog2("job accept", j.id + " — " + j.title);
    string key = BuildingKey(j.building); offerCursor[key] = (offerCursor.ContainsKey(key) ? offerCursor[key] : 0) + 1;
  }
  void DeclineJob(JobDef j) {
    string key = BuildingKey(j.building);
    if (!declinedByBuilding.ContainsKey(key)) declinedByBuilding[key] = new List<string>();
    if (!declinedByBuilding[key].Contains(j.id)) declinedByBuilding[key].Add(j.id);
    offerCursor[key] = (offerCursor.ContainsKey(key) ? offerCursor[key] : 0) + 1;
    if (j.oneShot) oneShotUsed.Add(j.id);
    msg = "You decline. Perhaps another time."; if (playthrough) PLog2("job decline", j.id);
  }
  void CompleteJob(ActiveJob a) {
    a.status = "done"; coins += a.coins; honor += a.honorReward; fear += a.fearReward; sultanStanding += a.sultanReward;
    if (a.sultanReward != 0) rep[Muscat] += a.sultanReward * 3;
    msg = $"Job done: {a.title}. +{a.coins} coins."; if (playthrough) PLog2("job done", a.id);
    var drop = journal.Where(j => j.status != "active").OrderByDescending(j => j.acceptWeek).Skip(10).ToList();
    foreach (var d in drop) journal.Remove(d);
  }
  void TryCompleteJobsOnEnterBuilding(string building) {
    foreach (var a in journal.Where(j => j.status == "active").ToList()) {
      if (a.objective == null) continue;
      string type = (string)JGet(a.objective, "type", "");
      string to = (string)JGet(a.objective, "to", a.destination ?? "");
      if (type == "deliver" && building == "tavern" && to.IndexOf(S.ports[port].name, StringComparison.OrdinalIgnoreCase) >= 0) CompleteJob(a);
      if (a.id.Contains("alms") && building == "temple" && a.giver.IndexOf(S.ports[port].name, StringComparison.OrdinalIgnoreCase) >= 0) CompleteJob(a);
    }
  }
  void TryCompleteJobsOnDock() {
    foreach (var a in journal.Where(j => j.status == "active").ToList()) {
      if (a.objective == null) continue;
      string type = (string)JGet(a.objective, "type", "");
      string to = (string)JGet(a.objective, "to", a.destination ?? "");
      if ((type == "escort" || type == "deliver" || type == "haul") && to.IndexOf(S.ports[port].name, StringComparison.OrdinalIgnoreCase) >= 0) CompleteJob(a);
    }
  }
  void OnJobCustomsCaught() {
    foreach (var a in journal.Where(j => j.status == "active" && j.contraband).ToList()) {
      var hit = a.onCaught;
      if (honor >= highHonorThreshold && a.highHonorModifiers != null && a.highHonorModifiers.ContainsKey("onCaught")) hit = Design.D(a.highHonorModifiers["onCaught"]);
      if (hit != null) { honor += (int)Design.F(JGet(hit, "honor", 0)); sultanStanding += (int)Design.F(JGet(hit, "sultan", 0)); }
      a.status = "failed"; if (playthrough) PLog2("job failed", a.id + " (customs)");
    }
  }
  void OnJobCustomsPass() {
    foreach (var a in journal.Where(j => j.status == "active" && j.contraband).ToList()) {
      string to = (string)(a.objective != null ? JGet(a.objective, "to", a.destination) : a.destination) ?? "";
      if (to.IndexOf(S.ports[port].name, StringComparison.OrdinalIgnoreCase) >= 0) CompleteJob(a);
    }
  }

  void OpenJournal() { StartCoroutine(JournalCo()); }
  IEnumerator JournalCo() {
    bool done = false; int shownTab = -1;
    while (!done) {
      if (shownTab != journalTab || dialog == null) {
        if (dialog) Destroy0(dialog.gameObject);
        shownTab = journalTab;
        dialog = R(root, "Journal", 0, 0, 1, 1); dialog.offsetMin = new Vector2(20, 40); dialog.offsetMax = new Vector2(-20, -40);
        Box(dialog, T.panel, T.gold);
        var inner = Stretch(dialog, "in", 28, 28, 28, 28);
        Label(inner, "Journal", 20, 10, 700, 60, T.fontSizeTitle + 6, T.parchment, TextAnchor.MiddleLeft, true);
        Btn(inner, "Active", 20, 80, 220, 70, () => { journalTab = 0; shownTab = -1; }, journalTab != 0, journalTab == 0);
        Btn(inner, "Done", 260, 80, 220, 70, () => { journalTab = 1; shownTab = -1; }, journalTab != 1, journalTab == 1);
        Btn(inner, "Close", 740, 10, 220, 70, () => { done = true; });
        var list = journalTab == 0 ? journal.Where(j => j.status == "active").ToList() : journal.Where(j => j.status != "active").Take(10).ToList();
        float y = 170;
        if (!JournalUnlocked) Label(inner, "The journal opens after the Muscat sale and Yusuf's seal.", 20, y, 960, 80, T.fontSize, T.textMuted);
        else if (list.Count == 0) Label(inner, journalTab == 0 ? "No active jobs. Talk to keepers for offers." : "No finished jobs yet.", 20, y, 960, 80, T.fontSize, T.textMuted);
        else foreach (var a in list) {
          var card = Px(inner, "e", 20, y, 960, 200); Box(card, T.panelAlt, T.brass);
          Label(card, a.title, 16, 8, 700, 44, T.fontSizeTitle - 4, T.parchment, TextAnchor.MiddleLeft, true);
          Label(card, $"{a.giver}  →  {a.destination}", 16, 52, 920, 36, T.fontSizeSmall, T.textMuted);
          Label(card, $"Reward: {a.reward}" + (string.IsNullOrEmpty(a.risk) ? "" : $"   Risk: {a.risk}"), 16, 90, 920, 36, T.fontSizeSmall, T.text);
          if (!string.IsNullOrEmpty(a.deadline)) Label(card, a.deadline, 16, 126, 600, 30, T.fontSizeSmall - 2, T.textMuted);
          if (a.status == "active" && !string.IsNullOrEmpty(a.hint)) { var hint = a.hint; Btn(card, "Hint", 720, 130, 220, 56, () => { msg = hint; }); }
          Label(card, a.status.ToUpper(), 720, 10, 220, 40, T.fontSizeSmall - 2, a.status == "active" ? T.gold : T.textMuted, TextAnchor.MiddleRight);
          y += 220;
        }
        Label(inner, $"Active {ActiveJobCount}/{JobCap}", 20, 1540, 400, 40, T.fontSizeSmall, T.textMuted);
      }
      yield return null;
    }
    if (dialog) { Destroy0(dialog.gameObject); dialog = null; }
    { var dq = root.Find("QuestDim"); if (dq) Destroy0(dq.gameObject); }
    Refresh();
  }

  IEnumerator QuestMenu(string building, string keeperName) {
    var offers = OffersFor(building);
    bool closed = false;
    while (!closed) {
      if (dialog) Destroy0(dialog.gameObject);
      /* v0.6k: ink panel sized to cards; title fully inside with headroom */
      int n = Mathf.Max(1, offers.Count); float pad = 24f;
      float panelH = Mathf.Clamp(160 + n * 368 + 110, 400, 1700); /* k4: taller offer cards */
      float panelW = 940f; float left = (1080f - panelW) * 0.5f;
      float top = 100f; /* fully cover keeper "Innkeeper of the Salt &…" title */
      var dimQ = R(root, "QuestDim", 0, 0, 1, 1); var di = dimQ.gameObject.AddComponent<Image>(); di.color = new Color(0.05f, 0.03f, 0.02f, 0.72f); di.raycastTarget = true;
      dialog = Px(root, "QuestMenu", left, top, panelW, panelH); Box(dialog, T.ink, T.gold); dialog.SetAsLastSibling();
      string title = keeperName ?? ""; if (title.Length > 36) title = title.Substring(0, 34) + "…";
      float iw = panelW - pad * 2;
      Label(dialog, title, pad, 36, iw, 56, T.fontSizeTitle - 4, T.parchment, TextAnchor.MiddleCenter, true);
      Label(dialog, offers.Count > 0 ? offers[0].greeting : "Nothing for you today, Captain.", pad, 96, iw, 44, T.fontSizeSmall, T.text, TextAnchor.MiddleCenter);
      float y = 150;
      if (ActiveJobCount >= JobCap) { Label(dialog, "Journal is full (3/3). Finish a job first.", pad, y, iw, 44, T.fontSizeSmall, T.down, TextAnchor.MiddleCenter); y += 50; }
      if (offers.Count == 0) { Label(dialog, "No offers match your standing right now.", pad, y, iw, 50, T.fontSize, T.textMuted, TextAnchor.MiddleCenter); y += 70; }
      for (int i = 0; i < offers.Count; i++) {
        var j = offers[i]; int pay = j.coins;
        if (j.contraband && honor >= highHonorThreshold && j.highHonorModifiers != null) pay = Mathf.RoundToInt(pay * Design.F(JGet(j.highHonorModifiers, "coinsMult", 1.25f), 1.25f));
        var row = Px(dialog, "off" + i, pad, y, iw, 352); Box(row, T.panelAlt, T.brass);
        Label(row, j.title, 16, 8, iw - 32, 44, T.fontSizeTitle - 6, T.parchment, TextAnchor.MiddleLeft, true);
        Label(row, j.summary, 16, 52, iw - 32, 64, T.fontSizeSmall, T.text);
        Label(row, $"Reward: {(pay != j.coins ? pay + " coins" : j.reward)}", 16, 118, iw - 32, 40, T.fontSizeSmall, T.gold); FitLine(row);
        if (!string.IsNullOrEmpty(j.risk)) { Label(row, $"Risk: {j.risk}", 16, 158, iw - 32, 40, T.fontSizeSmall, T.down); FitLine(row); } /* k4: risk/ambush on its own line, never over the deadline */
        string dl = !string.IsNullOrEmpty(j.deadline) ? j.deadline : (j.deadlineMoons > 0 ? $"Within {j.deadlineMoons} moon{(j.deadlineMoons > 1 ? "s" : "")}" : "No deadline");
        Label(row, dl, 16, 202, iw - 32, 40, T.fontSizeSmall - 2, T.textMuted); FitLine(row);
        bool full = ActiveJobCount >= JobCap; var jj = j;
        Btn(row, "Accept", 16, 252, (iw - 48) * 0.5f, 84, () => { if (!full) { AcceptJob(jj); closed = true; } }, !full, true);
        Btn(row, "Decline", 16 + (iw - 48) * 0.5f + 16, 252, (iw - 48) * 0.5f, 84, () => { DeclineJob(jj); closed = true; });
        y += 368;
      }
      Btn(dialog, "Leave", pad, panelH - 96, iw, 80, () => { closed = true; });
      while (dialog != null && !closed) yield return null;
    }
    if (dialog) { Destroy0(dialog.gameObject); dialog = null; }
    Refresh();
  }

  /// k4: last label added to row stays on one line, shrinking (min 18) instead of spilling into the next line
  static void FitLine(RectTransform row) { var t = row.GetChild(row.childCount - 1).GetComponent<Text>(); if (!t) return; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate; t.resizeTextForBestFit = true; t.resizeTextMinSize = 18; t.resizeTextMaxSize = t.fontSize; }

  void QuestBtn(RectTransform led, string building, string keeperName, float y = 1320) {
    Btn(led, "Ask about work", 26, y, 980, 110, () => StartCoroutine(QuestMenu(building, keeperName)), true, true);
  }

  bool BuildingAvailable(int tab) {
    string town = S.ports[port].name;
    if (tab == 7) return true; /* Muscat plaque opens HarbourScreen via ShipwrightScreen */
    if (tab == 1 && town == "Gwadar") return false;
    return true;
  }
  /// Muscat: shipwright plaque opens harbour master
  void HarbourScreen(RectTransform led) {
    LedgerHeader(led, "Muscat Harbour");
    Label(led, $"No shipwright here. The dock can patch your hull. Hull {HullCur:0}/{HullMax:0}.", 26, 80, 980, 80, T.fontSize, T.text);
    float y = 180;
    int toPaid = Mathf.CeilToInt(HullMax * patchPaidPct); int toFree = Mathf.CeilToInt(HullMax * patchFreePct);
    if (HullCur < toPaid) {
      int fee = Mathf.Max(20, Mathf.CeilToInt((toPaid - HullCur) * S.shipwright.repairPerHullPoint * 0.6f));
      if (harbourDebt > 0 && harbourDebtDueWeek >= 0 && week > harbourDebtDueWeek) fee = Mathf.CeilToInt(fee * 2f); /* unpaid debt: dock fees ×2 */
      Btn(led, $"Dock patch to {patchPaidPct * 100:0}% · {fee:N0}", 26, y, 980, 110, () => {
        if (coins >= fee) { coins -= fee; hullNow = toPaid; msg = "Dockhands patch your hull to half strength."; }
        else { hullNow = Mathf.Max(HullCur, toFree); harbourDebt += patchDebtCoins; harbourDebtDueWeek = week + patchDebtDueMoons * Mathf.Max(1, S.weeksPerMoon); msg = $"Broke — emergency patch to {patchFreePct * 100:0}%. Debt due in {patchDebtDueMoons} moons ({harbourDebt} coins)."; }
        Refresh();
      }, true, true); y += 130;
    }
    if (harbourDebt > 0) {
      string due = harbourDebtDueWeek < 0 ? "" : (week > harbourDebtDueWeek ? " OVERDUE (fees ×2, Honor at risk)" : $" due moon {harbourDebtDueWeek / Mathf.Max(1, S.weeksPerMoon) + 1}");
      Label(led, $"Harbour debt: {harbourDebt} coins{due}", 26, y, 980, 50, T.fontSize, T.down); y += 60;
      Btn(led, $"Pay debt · {harbourDebt:N0}", 26, y, 980, 100, () => { if (coins >= harbourDebt) { coins -= harbourDebt; harbourDebt = 0; harbourDebtDueWeek = -1; msg = "Debt cleared."; Refresh(); } }, coins >= harbourDebt); y += 120;
    }
    QuestBtn(led, "harbour", "Harbour master of " + S.ports[port].name, y + 20);
  }

  bool CanLeavePort(out string reason) {
    reason = null;
    float pct = HullMax <= 0 ? 1 : HullCur / HullMax;
    if (pct >= patchLeavePct - 0.001f) return true;
    reason = $"Hull below {patchLeavePct * 100:0}%. Patch at the yard or harbour before you cast off.";
    return false;
  }
  float LimpSailMult => HullMax <= 0 ? 1f : (HullCur / HullMax < 0.5f ? 0.7f : 1f);

  void InitV06j() { if (jobPool.Count == 0) LoadJobsV06j(); }
  void TickHarbourDebt() {
    if (harbourDebt <= 0 || harbourDebtDueWeek < 0 || week <= harbourDebtDueWeek) return;
    /* overdue: Honor −1 once when crossing the due week */
    if (week == harbourDebtDueWeek + 1) { honor = Mathf.Max(0, honor - 1); news.Insert(0, "Harbour debt overdue in Muscat: Honor −1. Dock fees doubled until paid."); if (playthrough) PLog2("harbour debt", "overdue Honor -1"); }
  }

  /// post-opening three-goal HUD: top active job + story long goal
  string ShortStoryGoal(string full) {
    if (string.IsNullOrEmpty(full)) return full;
    if (full.IndexOf("Qarin", System.StringComparison.OrdinalIgnoreCase) >= 0 && full.IndexOf("hoard", System.StringComparison.OrdinalIgnoreCase) >= 0) return "What is Qarin hiding?";
    if (full.IndexOf("burned your ship", System.StringComparison.OrdinalIgnoreCase) >= 0) return "Who burned your ship?";
    return full.Length > 36 ? full.Substring(0, 34) + "…" : full;
  }
  (string, string, string) JournalGoalsSummary() {
    var top = journal.FirstOrDefault(j => j.status == "active");
    string s = top != null ? top.title : "Ask keepers for work (Journal)";
    string m = ActiveJobCount > 1 ? $"{ActiveJobCount} jobs active — open Journal" : (top != null ? $"To: {top.destination}" : "Journal empty");
    string l = yusufSeal ? "Find out what House Qarin is hoarding." : "Who burned your ship? The Qarin seal."; /* full line kept for journal; HUD shortens via ShortStoryGoal */
    return (s, m, l);
  }

  [Serializable] public class SavedJob { public string id, status; public int acceptWeek, dueWeek, coins; public string reward; }
  [Serializable] public class SavedDecl { public string key; public List<string> ids; }
  List<SavedJob> JournalToSave() => journal.Select(a => new SavedJob { id = a.id, status = a.status, acceptWeek = a.acceptWeek, dueWeek = a.dueWeek, coins = a.coins, reward = a.reward }).ToList();
  List<SavedDecl> DeclToSave() => declinedByBuilding.Select(kv => new SavedDecl { key = kv.Key, ids = kv.Value.ToList() }).ToList();
  void JournalFromSave(List<SavedJob> js, List<SavedDecl> dec, List<string> oneShots, List<string> curKeys, List<int> curVals) {
    if (jobPool.Count == 0) LoadJobsV06j();
    journal.Clear(); declinedByBuilding.Clear(); offerCursor.Clear(); oneShotUsed.Clear();
    if (js != null) foreach (var s in js) { var j = jobPool.FirstOrDefault(x => x.id == s.id);
      journal.Add(new ActiveJob { id = s.id, status = s.status, acceptWeek = s.acceptWeek, dueWeek = s.dueWeek, coins = s.coins, reward = s.reward,
        title = j?.title ?? s.id, giver = j?.giver, destination = j?.destination, risk = j?.risk, deadline = j?.deadline, hint = j?.hint,
        honorReward = j?.honorReward ?? 0, fearReward = j?.fearReward ?? 0, sultanReward = j?.sultanReward ?? 0, contraband = j?.contraband ?? false,
        objective = j?.objective, onCaught = j?.onCaught, highHonorModifiers = j?.highHonorModifiers }); }
    if (dec != null) foreach (var d in dec) declinedByBuilding[d.key] = d.ids ?? new List<string>();
    if (oneShots != null) foreach (var o in oneShots) oneShotUsed.Add(o);
    if (curKeys != null && curVals != null) for (int i = 0; i < Mathf.Min(curKeys.Count, curVals.Count); i++) offerCursor[curKeys[i]] = curVals[i];
  }
}
