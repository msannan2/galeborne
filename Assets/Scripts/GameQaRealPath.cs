using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

/// gamedev_encounter_fix: qa harness that sails full voyages through the player's own Voyage() coroutine
/// (roll at departure -> decision cards -> 45% encounter -> arrival -> dock), tapping each card's real buttons.
public partial class Game {
  IEnumerator QaRealPath(System.Action<bool, string> Chk, System.Action<string> L) {
    { tab = -1; dialog = null; Refresh(); yield return null; Save(); string rp_bakA = SavePath + ".qa_rp", rp_bakB = SavePathB + ".qa_rp"; if (File.Exists(SavePath)) File.Copy(SavePath, rp_bakA, true); if (File.Exists(SavePathB)) File.Copy(SavePathB, rp_bakB, true);
      float rp_sMin = S.sailSecondsMin, rp_sMax = S.sailSecondsMax, rp_ts0 = Time.timeScale; int rp_fr0 = Application.targetFrameRate; bool rp_cam0 = cam && cam.enabled, rp_firstRun0 = firstRun; int rp_wk0 = week;
      int rp_rolled = 0, rp_picked = 0, rp_shown = 0, rp_Weights = 0, rp_Rep = 0, rp_Wx = 0, rp_stuck = 0, rp_Pir = 0; string rp_prevShown = ""; var rp_rolls = new System.Collections.Generic.List<float>();
      void rp_RpLog(string rp_c, string rp_st, LogType rp_t) { if (rp_c.StartsWith("GB VEVENTS weights") && !rp_c.Contains("qa synthetic")) { rp_Weights++; if (rp_c.Contains("encountered True")) rp_rolled++; } else if (rp_c.StartsWith("GB VEVENTS pick")) rp_picked++; else if (rp_c.StartsWith("GB ENCOUNTER show ")) { rp_shown++; string rp_id = rp_c.Substring(18).Trim(); if (rp_id == "pirate") rp_Pir++; if (rp_id == rp_prevShown) rp_Rep++; if (rp_prevShown != "" && VeFamily(rp_prevShown) == "weather" && VeFamily(rp_id) == "weather") rp_Wx++; rp_prevShown = rp_id; } }
      Application.logMessageReceived += rp_RpLog; lastFamily = ""; lastEncFamily = ""; lastEncId = ""; ambushHalveNext = false;
      S.sailSecondsMin = S.sailSecondsMax = 1.2f; Time.timeScale = 6f; Application.targetFrameRate = -1; if (cam) cam.enabled = false; firstRun = false; storyAmbushDone = true; veForce = ""; veForceNext = ""; veForceHail = "";
      string[] rp_pref = { "Pay toll", "Pay nothing", "Bait", "Decline", "Hold your course", "Sail on", "Continue", "Noted", "Reef sails", "Leave it", "OK", "Close", "Collect", "Take" };
      const int rp_RN = 320; int rp_db = PortIdx("Debal"), rp_mu = PortIdx("Muscat"); port = rp_db;
      for (int rp_v = 0; rp_v < rp_RN; rp_v++) { week = rp_wk0; coins = 5000; System.Array.Clear(hold, 0, hold.Length); hold[G("copper")] = 6; hullNow = -1; sails = S.pirateFreeSails + 10; safeUntilSail = 0; harbourDebt = 0; tab = -1; dialog = null;
        int rp_to = port == rp_db ? rp_mu : rp_db; StartCoroutine(Voyage(rp_to)); yield return null; float rp_t0 = Time.realtimeSinceStartup;
        while (sailing && Time.realtimeSinceStartup - rp_t0 < 30f) { if (dialog != null) { var rp_bs = dialog.GetComponentsInChildren<UnityEngine.UI.Button>().Where(rp_b => rp_b.interactable).ToList(); string rp_Lb(UnityEngine.UI.Button rp_b) { var rp_tx = rp_b.GetComponentInChildren<UnityEngine.UI.Text>(); return rp_tx ? rp_tx.text : ""; }
            var rp_pick = rp_pref.Select(rp_pf => rp_bs.FirstOrDefault(rp_b => rp_Lb(rp_b).StartsWith(rp_pf))).FirstOrDefault(rp_b => rp_b != null) ?? rp_bs.LastOrDefault(); if (rp_pick != null) rp_pick.onClick.Invoke(); }
          yield return null; }
        if (sailing) { rp_stuck++; L($"real-path voyage {rp_v} stuck (dialog {(dialog ? dialog.name : "none")})"); break; } if (rp_rolls.Count < rp_Weights) rp_rolls.Add(veLastRoll); }
      Application.logMessageReceived -= rp_RpLog; S.sailSecondsMin = rp_sMin; S.sailSecondsMax = rp_sMax; Time.timeScale = rp_ts0; Application.targetFrameRate = rp_fr0; if (cam) cam.enabled = rp_cam0; firstRun = rp_firstRun0;
      if (File.Exists(rp_bakA)) File.Copy(rp_bakA, SavePath, true); if (File.Exists(rp_bakB)) File.Copy(rp_bakB, SavePathB, true); Load(); tab = -1; dialog = null; Refresh(); yield return null;
      float rp_rate = rp_shown / (float)rp_RN, rp_sd = Mathf.Sqrt(0.25f / rp_RN), rp_mean = rp_rolls.Count > 0 ? rp_rolls.Average() : 0, rp_lowFrac = rp_rolls.Count > 0 ? rp_rolls.Count(rp_x => rp_x < 0.5f) / (float)rp_rolls.Count : 0;
      Chk(rp_stuck == 0 && rp_Weights == rp_RN && Mathf.Abs(rp_rate - 0.5f) <= 4 * rp_sd && rp_picked == rp_rolled && rp_shown == rp_rolled, $"gamedev_encounter_fix: REAL voyage path, {rp_RN} full voyages: {rp_shown} encounters shown ({rp_rate:P1}, want 50% +/- {4 * rp_sd:P1}); rolled True {rp_rolled}, picked {rp_picked}, shown {rp_shown} (none lost after the roll); roll mean {rp_mean:0.000}, {rp_lowFrac:P1} of rolls < 0.5; {rp_Pir} pirates; stuck {rp_stuck}");
      Chk(rp_Rep == 0 && rp_Wx == 0, $"gamedev_encounter_fix: REAL voyage path: {rp_Rep} immediate repeats of an encounter id, {rp_Wx} squall/squallLine back to back over {rp_shown} encounters"); }
  }
}
