using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// gamedev_v071 (Jeff): the arrival/dock path must survive slow frames and failures. Voyages run through SafeRun (a coroutine
/// executor that catches exceptions and keeps a heartbeat). A watchdog finishes the dock if the player is ever left sailing with
/// no voyage running, no card/modal/fight/pause and no heartbeat for DockWatchdogSec real seconds, and logs GB DOCK RECOVER.
public partial class Game {
  bool voyageAlive, qaThrowAtDock; int dockTo = -1, dockStage = 3; float voyageBeat, dockIdleSince = -1;
  public int dockRecovers; public const float DockWatchdogSec = 8f, DockBeatLostSec = 30f; float dockWatchdogSec = DockWatchdogSec; int dockHookRuns; /* qa shortens dockWatchdogSec for the forced tests only */
  int stallMs = -1; System.Random stallRng; /* -gbstall <ms> / GB_STALL: random frame stalls for load testing (own RNG: game randomness untouched) */

  /// runs a coroutine tree step by step: nested IEnumerators run inline (same frame semantics as Unity), every other yield goes to Unity;
  /// an exception anywhere ends the run with a log line instead of silently killing the coroutine and leaving the player stuck
  IEnumerator SafeRun(IEnumerator root, string what) {
    var st = new Stack<IEnumerator>(); st.Push(root);
    while (st.Count > 0) { var top = st.Peek(); bool more; object cur = null;
      try { more = top.MoveNext(); if (more) cur = top.Current; } catch (Exception e) { Debug.Log($"GB COROUTINE {what} failed: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); yield break; }
      if (!more) { st.Pop(); continue; }
      if (cur is IEnumerator ie) { st.Push(ie); continue; }
      voyageBeat = Time.unscaledTime; yield return cur; voyageBeat = Time.unscaledTime; } }

  bool DockBlocked => dialog != null || modalDepth > 0 || fighting || pauseCard != null || onTitle;
  void DockWatchdog() {
    if (stallMs > 0) { if (stallRng.NextDouble() < 0.3) System.Threading.Thread.Sleep(stallRng.Next(stallMs + 1)); }
    bool idle = sailing && !DockBlocked && Time.timeScale > 0f && (!voyageAlive || Time.unscaledTime - voyageBeat > DockBeatLostSec); /* a live voyage only counts if its heartbeat is gone for 30 s (long waits inside a voyage are normal) */
    if (!idle) { dockIdleSince = -1; return; } if (dockIdleSince < 0) { dockIdleSince = Time.unscaledTime; return; }
    if (Time.unscaledTime - dockIdleSince >= (voyageAlive ? 0f : dockWatchdogSec)) DockRecover(voyageAlive ? "voyage heartbeat lost" : "no voyage running"); }

  void DockRecover(string why) {
    int to = dockTo >= 0 ? dockTo : dest; dockRecovers++; dockIdleSince = -1;
    Debug.Log($"GB DOCK RECOVER ({why}): stage {dockStage}, at {S.ports[port].name}, bound for {(to >= 0 ? S.ports[to].name : "?")}");
    try { if (dockStage < 1 && to >= 0) { NewWeek(); port = to; } ShowSeaLandmark(-1); if (sea3D) sea3D.gameObject.SetActive(false); if (pirateShip) pirateShip.gameObject.SetActive(false);
      if (dockStage < 2) { customsPending = -1; dockHookRuns++; msg = $"Docked at {S.ports[port].name}. " + DockNews(); OnDockV06(); TryCompleteJobsOnDock(); EgOnDock(); RivalOnDock(); FallbackCheck(); } }
    catch (Exception e) { Debug.Log("GB DOCK RECOVER: a dock hook failed, docking anyway: " + e.Message); }
    voyageAlive = false; veWxCard = false; tithe = false; sailing = false; fighting = false; skipVoyage = false; dockedSave = false; crewInjured = false; dockStage = 3; tab = -1; Refresh(); Save(); }

  void DockGuardFlags() { var a = Environment.GetCommandLineArgs(); int i = Array.IndexOf(a, "-gbstall"); string ev = Environment.GetEnvironmentVariable("GB_STALL");
    if (i >= 0 && i + 1 < a.Length && int.TryParse(a[i + 1], out int ms)) stallMs = ms; else if (int.TryParse(ev, out int ms2)) stallMs = ms2;
    if (stallMs > 0) { stallRng = new System.Random(1234); Debug.Log($"GB STALL on: 30% of frames sleep 0..{stallMs} ms (load test)"); } }
}

public partial class Game {
  /// gamedev_v071 qa: the watchdog never fires in a normal run; it does recover a forced stuck dock (dead voyage after arrival, and an
  /// exception inside a real voyage's dock sequence), and it never fires while a card is open
  IEnumerator QaDockWatchdog(Action<bool, string> Chk, Action<string> L) {
    Chk(dockRecovers == 0, $"gamedev_v071: no GB DOCK RECOVER in this qa run so far ({dockRecovers})");
    int db = PortIdx("Debal"), mu = PortIdx("Muscat"); tab = -1; dialog = null; port = db; Refresh(); yield return null;
    // (a) the voyage coroutine is gone after arrival; a card is open first (must NOT fire), then nothing (must fire)
    Chk(dockWatchdogSec == 8f && DockWatchdogSec == 8f, $"real-play watchdog time is {DockWatchdogSec} s (qa uses 3 s below to keep the run short)"); dockWatchdogSec = 3f;
    int r0 = dockRecovers, h0 = dockHookRuns; int w0 = week; sailing = true; voyageAlive = false; dockTo = mu; NewWeek(); port = mu; dockStage = 1; msg = "";
    var hold0 = StartCoroutine(Ask("qa hold", "A card stays open past the watchdog time.", ("OK", null))); float t0 = Time.unscaledTime;
    while (Time.unscaledTime - t0 < dockWatchdogSec + 2f) yield return null; bool quietUnderCard = dockRecovers == r0 && sailing;
    EgTestCloseCard(); yield return null; float t1 = Time.unscaledTime; while (sailing && Time.unscaledTime - t1 < dockWatchdogSec + 6f) yield return null; float took = Time.unscaledTime - t1;
    Chk(quietUnderCard, $"watchdog stays quiet while a card is open ({dockWatchdogSec + 2f:0} s with a card, sailing kept)");
    Chk(!sailing && dockRecovers == r0 + 1 && port == mu && dockHookRuns == h0 + 1 && week == w0 + 1 && took >= dockWatchdogSec - 0.5f, $"forced stuck dock (voyage gone after arrival, no card): GB DOCK RECOVER after {took:0.0} s, docked at {S.ports[port].name}, week {w0}->{week} (not advanced twice), dock hooks run {dockHookRuns - h0}x, msg '{msg}'");
    while (dialog != null) { EgTestCloseCard(); yield return null; }
    // (b) an exception inside a real voyage's dock sequence: SafeRun logs it, the watchdog finishes the dock
    string fail = ""; void Lg(string c, string st, LogType t) { if (c.StartsWith("GB COROUTINE Voyage failed")) fail = c.Split('\n')[0]; }
    Application.logMessageReceived += Lg; float sMin = S.sailSecondsMin, sMax = S.sailSecondsMax, ec = S.encounterChance; S.sailSecondsMin = S.sailSecondsMax = 1f; S.encounterChance = 0;
    r0 = dockRecovers; port = db; tab = -1; Refresh(); yield return null; qaThrowAtDock = true; StartCoroutine(Voyage(mu)); yield return null; t0 = Time.unscaledTime;
    while (sailing && Time.unscaledTime - t0 < 60f) { if (dialog != null) EgTestCloseCard(); yield return null; }
    Application.logMessageReceived -= Lg; S.sailSecondsMin = sMin; S.sailSecondsMax = sMax; S.encounterChance = ec; qaThrowAtDock = false; dockWatchdogSec = DockWatchdogSec;
    Chk(fail != "" && !sailing && dockRecovers == r0 + 1 && port == mu && !voyageAlive, $"exception in a real voyage's dock sequence: '{fail}' -> GB DOCK RECOVER, docked at {S.ports[port].name} in {Time.unscaledTime - t0:0.0} s");
    while (dialog != null) { EgTestCloseCard(); yield return null; } tab = -1; Refresh(); yield return null; }
}
