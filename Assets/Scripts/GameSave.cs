using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

[Serializable] public class SaveData {
  public float[] avgCost; public int difficulty = 1, fame, bankCoins, bankPort = -1, loanOwed, loanDueWeek, loanCap, fightsFought; public float loanRate; public int[] shipTier; public bool papersGiven; public int storyStep, rumorHeard, gpBanStart = -1, jackpotStartCoins = -1; public bool firstRun = true, fallbackFired, storyAmbushDone, qarinLetter, boardedStoryPirate; public float hullNow = -1, sailsNow = -1; public string lastAmbush; public int honor, fear, faridaState; public bool yusufSeal, openingGpSold; public int harbourDebt, sultanStanding, harbourDebtDueWeek = -1; public long seq; public int version = 1, duelsFought, coins, week, port, sails, safeUntilSail; public bool crate, tithe, crewInjured;
  public int[] hold, rep; public float[] price, lastPrice, basePrice; public List<Ban> bans; public Warehouse[] wh; public List<string> news, charms; public string msg;
  public List<Game.SavedJob> journal; public List<Game.SavedDecl> declined; public List<string> oneShotUsed, cursorKeys; public List<int> cursorVals;
}

public partial class Game {
  /// -gbsavedir <dir> (or GALEBORNE_SAVEDIR) redirects every save file: test runs never touch the shared default saves
  static string saveDir; static string SaveDir { get { if (saveDir == null) { var a = Environment.GetCommandLineArgs(); int i = Array.IndexOf(a, "-gbsavedir"); saveDir = i >= 0 && i + 1 < a.Length ? a[i + 1] : Environment.GetEnvironmentVariable("GALEBORNE_SAVEDIR"); if (string.IsNullOrEmpty(saveDir)) saveDir = Application.persistentDataPath; else Directory.CreateDirectory(saveDir); Debug.Log("GB SAVEDIR " + saveDir); } return saveDir; } }
  static bool SaveDirIsolated => SaveDir != Application.persistentDataPath;
  static string SavePath => Path.Combine(SaveDir, "galeborne_save.json");
  static string SavePathB => Path.Combine(SaveDir, "galeborne_save_b.json");
  static string VoyageBackup => Path.Combine(SaveDir, "galeborne_save_voyage.json");
  public static bool HasSave => File.Exists(SavePath) || File.Exists(SavePathB) || File.Exists(VoyageBackup);
  public long saveSeq; public bool saveBroken; public string lastLoadedFrom = "";
  float[] Flat(float[,] a) { int np = a.GetLength(0), ng = a.GetLength(1); var f = new float[np * ng]; for (int p = 0; p < np; p++) for (int g = 0; g < ng; g++) f[p * ng + g] = a[p, g]; return f; }
  void Unflat(float[] f, float[,] a) { int np = a.GetLength(0), ng = a.GetLength(1); if (f == null || f.Length != np * ng) return; for (int p = 0; p < np; p++) for (int g = 0; g < ng; g++) a[p, g] = f[p * ng + g]; }
  public void Save() {
    if (shotMode || onTitle || S == null || price == null) return;
    var d = new SaveData { difficulty = difficulty, fame = fame, bankCoins = bankCoins, bankPort = bankPort, loanOwed = loanOwed, loanDueWeek = loanDueWeek, loanCap = loanCap, fightsFought = fightsFought, loanRate = loanRate, shipTier = shipTier, papersGiven = papersGiven, storyStep = storyStep, rumorHeard = rumorHeard, gpBanStart = gpBanStart, jackpotStartCoins = jackpotStartCoins, firstRun = firstRun, fallbackFired = fallbackFired, storyAmbushDone = storyAmbushDone, qarinLetter = qarinLetter, boardedStoryPirate = boardedStoryPirate, hullNow = hullNow, sailsNow = sailsNow, lastAmbush = lastAmbush, honor = honor, fear = fear, faridaState = faridaState, yusufSeal = yusufSeal, openingGpSold = openingGpSold, harbourDebt = harbourDebt, sultanStanding = sultanStanding, harbourDebtDueWeek = harbourDebtDueWeek, duelsFought = duelsFought, coins = coins, week = week, port = port, sails = sails, safeUntilSail = safeUntilSail, crate = crate, tithe = tithe, crewInjured = crewInjured,
      hold = hold, avgCost = AvgCost, rep = rep, price = Flat(price), lastPrice = Flat(lastPrice), basePrice = Flat(basePrice), bans = bans, wh = wh, news = news, charms = charms, msg = msg, journal = JournalToSave(), declined = DeclToSave(), oneShotUsed = oneShotUsed.ToList(), cursorKeys = offerCursor.Keys.ToList(), cursorVals = offerCursor.Values.ToList() };
    if (saveSeq == 0) foreach (var sp in new[] { SavePath, SavePathB, VoyageBackup }) try { if (File.Exists(sp)) saveSeq = Math.Max(saveSeq, JsonUtility.FromJson<SaveData>(File.ReadAllText(sp))?.seq ?? 0); } catch { } /* continue numbering past any older game on disk */
    d.seq = ++saveSeq; string target = saveSeq % 2 == 1 ? SavePath : SavePathB; /* two rotating autosave slots: each save overwrites the older one */
    try { var tmp = target + ".tmp"; File.WriteAllText(tmp, JsonUtility.ToJson(d)); if (File.Exists(target)) File.Delete(target); File.Move(tmp, target); } catch (Exception e) { Debug.LogWarning("Save failed: " + e.Message); }
  }
  bool Apply(SaveData d) {
    try {
      if (d == null || d.hold == null || d.hold.Length != S.goods.Length) throw new Exception("save shape mismatch");
      difficulty = d.difficulty; fame = d.fame; bankCoins = d.bankCoins; bankPort = d.bankPort; loanOwed = d.loanOwed; loanDueWeek = d.loanDueWeek; loanCap = d.loanCap; fightsFought = d.fightsFought; loanRate = d.loanRate; papersGiven = d.papersGiven; storyStep = d.storyStep; rumorHeard = d.rumorHeard; gpBanStart = d.gpBanStart; jackpotStartCoins = d.jackpotStartCoins; firstRun = d.firstRun; fallbackFired = d.fallbackFired; storyAmbushDone = d.storyAmbushDone; qarinLetter = d.qarinLetter; boardedStoryPirate = d.boardedStoryPirate; hullNow = d.hullNow; sailsNow = d.sailsNow; lastAmbush = d.lastAmbush ?? "";
      KeepBases(); shipTier = d.shipTier != null && d.shipTier.Length == 4 ? d.shipTier : new int[4]; ApplyShipTiers(); tab = -1;
      honor = d.honor; fear = d.fear; faridaState = d.faridaState; yusufSeal = d.yusufSeal; openingGpSold = d.openingGpSold; harbourDebt = d.harbourDebt; sultanStanding = d.sultanStanding; harbourDebtDueWeek = d.harbourDebtDueWeek; duelsFought = d.duelsFought; coins = d.coins; week = d.week; port = d.port; sails = d.sails; safeUntilSail = d.safeUntilSail; crate = d.crate; tithe = d.tithe; crewInjured = d.crewInjured;
      hold = d.hold; avgCost = d.avgCost; rep = d.rep; Unflat(d.price, price); Unflat(d.lastPrice, lastPrice); Unflat(d.basePrice, basePrice);
      bans = d.bans ?? new List<Ban>(); EnsureDesignBans(); if (d.wh != null && d.wh.Length == wh.Length) wh = d.wh; news = d.news ?? new List<string>(); charms = d.charms ?? new List<string>();
      JournalFromSave(d.journal, d.declined, d.oneShotUsed, d.cursorKeys, d.cursorVals);
      var dm = d.msg ?? ""; while (dm.StartsWith("Welcome back, Captain. ")) dm = dm.Substring(23); msg = "Welcome back, Captain. " + dm; return true;
    } catch (Exception e) { Debug.LogWarning("Load failed: " + e.Message); return false; } }
  /// newest slot first, then the older slot, then the voyage-start backup; only if all fail does the title offer New Game alone
  public bool Load() {
    var cands = new List<(string path, SaveData d)>();
    foreach (var p in new[] { SavePath, SavePathB, VoyageBackup }) { if (!File.Exists(p)) continue; SaveData d = null; try { d = JsonUtility.FromJson<SaveData>(File.ReadAllText(p)); } catch (Exception e) { Debug.LogWarning($"Save {Path.GetFileName(p)} unreadable: {e.Message}"); }
      cands.Add((p, d)); }
    foreach (var c in cands.OrderByDescending(c => c.d == null ? long.MinValue : c.path == VoyageBackup ? c.d.seq - 1 : c.d.seq)) {
      if (c.d != null && Apply(c.d)) { saveSeq = Math.Max(c.d.seq, cands.Where(x => x.d != null).Select(x => x.d.seq).DefaultIfEmpty(0).Max()); lastLoadedFrom = Path.GetFileName(c.path); Debug.Log("GB SAVE loaded " + lastLoadedFrom + " seq " + c.d.seq); return true; }
      try { File.Copy(c.path, c.path + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), true); File.Delete(c.path); } catch { } /* broken slot set aside */ }
    lastLoadedFrom = ""; return false; }
  /// a copy of the current save at the start of every voyage
  public void SaveVoyageBackup() { Save(); try { var src = saveSeq % 2 == 1 ? SavePath : SavePathB; if (File.Exists(src)) File.Copy(src, VoyageBackup, true); } catch (Exception e) { Debug.LogWarning("Voyage backup failed: " + e.Message); } }
  void OnApplicationPause(bool paused) { NotePause(paused); if (paused && !sailing) Save(); if (!paused && wasPaused) { wasPaused = false; ShowPauseCard(); } }
  void OnApplicationQuit() { if (!sailing) Save(); }

  void ShowTitle() {
    onTitle = true; Clear(content);
    var tex = Resources.Load<Texture2D>("Art/port_muscat"); if (tex) backdrop.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .5f)); backdrop.color = Color.white;
    var pnl = Px(content, "Title", 140, 1080, 800, 620); Box(pnl, T.panel, T.gold);
    Label(pnl, "GALEBORNE", 0, 50, 800, 110, T.fontSizeTitle + 34, T.parchment, TextAnchor.MiddleCenter, true);
    Box(Px(pnl, "rule", 200, 170, 400, 2), T.brass);
    Label(pnl, "Trade, smuggle and fight across the monsoon seas", 40, 186, 720, 60, T.fontSizeSmall, T.textMuted, TextAnchor.MiddleCenter);
    bool has = HasSave; float y = 280;
    if (saveBroken) { has = false; Label(pnl, "Both voyage logs are damaged. Start a new voyage, or wait until we can mend them.", 40, 250, 720, 80, T.fontSizeSmall, T.text, TextAnchor.MiddleCenter); y += 80; }
    if (has) { Btn(pnl, "Continue", 120, y, 560, 110, () => { if (!Load()) { saveBroken = true; ShowTitle(); return; } onTitle = false; Refresh(); }, true, true); y += 140; }
    Btn(pnl, "New Game", 120, y, 560, 110, () => { onTitle = false; saveBroken = false; Init(); prologuePending = DZ == null || OpenFlag("startWithPrologueDuel"); tab = -1; Refresh(); StartCoroutine(Ask("Choose your difficulty", "You can change it any time from the ⚙ button in town.\n\nRelaxed: strong aim help at the breach, gentler pirates.\nCaptain: light aim help (recommended).\nHard: no help, fiercer pirates.",
      ("Relaxed", () => { difficulty = 0; Save(); }), ("Captain", () => { difficulty = 1; Save(); }), ("Hard", () => { difficulty = 2; Save(); }))); }, true, !has);
    Btn(pnl, "Credits", 250, 548, 300, 62, () => ShowCredits());
  }
}
